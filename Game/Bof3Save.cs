using System.Text;

using BreathOfFire3SaveEditor.SaveFormat;

namespace BreathOfFire3SaveEditor.Game;

/// <summary>
/// Breath of Fire III data inside a DuckStation save state.
///
/// The game keeps the same block it writes to the memory card in RAM. In the
/// USA release (SLUS-00422) the party records start at 0x80144968 and the
/// inventory sits 0x5E8 bytes after them. The game also leaves an older copy of
/// the block at 0x800C1800 from the last load; that copy is ignored here because
/// the game does not read it back.
/// </summary>
public sealed class Bof3Save
{
    public const int CharacterCount = 8;
    public const int UsaCharacterAddress = 0x144968;
    private const int InventoryFromCharacters = 0x5E8;

    private Bof3Save(string path, DuckStationState state, int charactersAddress)
    {
        Path = path;
        State = state;
        CharactersAddress = charactersAddress;

        Characters = Enumerable.Range(0, CharacterCount)
            .Select(i => new Bof3Character(state, charactersAddress + i * Bof3Character.RecordSize, i))
            .ToList();
        Inventory = new Bof3Inventory(state, charactersAddress + InventoryFromCharacters);
    }

    public string Path { get; private set; }
    public DuckStationState State { get; }
    public int CharactersAddress { get; }
    public IReadOnlyList<Bof3Character> Characters { get; }
    public Bof3Inventory Inventory { get; }

    public static Bof3Save Load(string path)
    {
        var state = DuckStationState.Load(path);

        if (!state.Title.Contains("Breath of Fire III", StringComparison.OrdinalIgnoreCase) &&
            !state.Serial.Contains("00422", StringComparison.Ordinal))
        {
            // Not fatal: the records are found by shape below. This only makes the
            // error clearer when someone opens a state from another game.
            if (!TryFindCharacters(state, out _))
            {
                throw new InvalidDataException(
                    $"This save state is for \"{state.Title}\", not Breath of Fire III.");
            }
        }

        if (!TryFindCharacters(state, out int address))
        {
            throw new InvalidDataException(
                "Could not find the party in this save state. Make a state while the game is running " +
                "(not on the title screen or a loading screen) and try again.");
        }

        return new Bof3Save(path, state, address);
    }

    /// <summary>
    /// Uses the USA address when the records there look right, otherwise
    /// searches RAM for eight records ending with Whelp's, taking the last match
    /// so the live copy wins over the stale one lower in memory.
    /// </summary>
    private static bool TryFindCharacters(DuckStationState state, out int address)
    {
        var ram = state.RamSpan;
        if (AllValid(ram, UsaCharacterAddress))
        {
            address = UsaCharacterAddress;
            return true;
        }

        address = -1;
        byte[] whelp = Encoding.ASCII.GetBytes("Whelp");
        int from = 0;
        while (true)
        {
            int hit = ram[from..].IndexOf(whelp);
            if (hit < 0)
            {
                break;
            }

            int at = from + hit;
            int start = at - 7 * Bof3Character.RecordSize;
            if (AllValid(ram, start))
            {
                address = start;
            }

            from = at + 1;
        }

        return address >= 0;
    }

    private static bool AllValid(ReadOnlySpan<byte> ram, int start)
    {
        if (start < 0)
        {
            return false;
        }

        for (int i = 0; i < CharacterCount; i++)
        {
            if (!Bof3Character.LooksValid(ram, start + i * Bof3Character.RecordSize))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Writes the state. When overwriting, the file as it was is first copied to
    /// "&lt;name&gt;.editor.bak" (DuckStation keeps its own ".bak", so a different
    /// name avoids clobbering it).
    /// </summary>
    public void Save(string? path = null)
    {
        string target = path ?? Path;
        byte[] bytes = State.Build();

        if (File.Exists(target))
        {
            File.Copy(target, target + ".editor.bak", overwrite: true);
        }

        string temp = target + ".tmp";
        File.WriteAllBytes(temp, bytes);
        File.Move(temp, target, overwrite: true);
        Path = target;
    }
}
