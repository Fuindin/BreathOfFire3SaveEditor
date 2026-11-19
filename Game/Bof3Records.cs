namespace BreathOfFire3SaveEditor.Game;

/// <summary>A consumable or other carried item.</summary>
public sealed record ItemInfo(string Name, int Price);

/// <summary>
/// A weapon as the game stores it. <paramref name="Weight"/> is subtracted from the
/// wielder's AGL; <paramref name="Element"/> is a bit set (see <see cref="Bof3Names.ElementBits"/>).
/// </summary>
public sealed record WeaponInfo(string Name, int EquipMask, int Element, int Weight, int Attack, int Price);

/// <summary>Shield, helmet or body armour. <paramref name="Slot"/> is 2, 3 or 4.</summary>
public sealed record ArmorInfo(string Name, int EquipMask, int Slot, int Weight, int Defense, int Price);

/// <summary>An accessory. Kind 5 is a wearable accessory, 10 is bait and 11 a fishing rod.</summary>
public sealed record AccessoryInfo(string Name, int EquipMask, int Kind, int Price);

/// <summary>Fixed names that are not stored in the item tables.</summary>
public static class Bof3Names
{
    /// <summary>The eight party records, in the order the game keeps them.</summary>
    public static readonly string[] Characters = ["Ryu", "Nina", "Garr", "Teepo", "Rei", "Momo", "Peco", "Whelp"];

    public static readonly string[] Masters =
    [
        "Bunyan", "Mygas", "Yggdrasil", "D'lonzo", "Fahl", "Durandal", "Giotto", "Hondara", "Emitai",
        "Deis", "Hachio", "Bais", "Lang", "Lee", "Wynn", "Ladon", "Meryleep",
    ];

    public static string MasterName(int id) =>
        id == 0xFF ? "None" : id < Masters.Length ? Masters[id] : $"Unknown ({id:X2})";

    /// <summary>Resistance bytes in record order.</summary>
    public static readonly string[] Resistances =
        ["Fire", "Ice", "Elec", "Earth", "Wind", "Holy", "Psionic", "Status", "Death"];

    public static readonly string[] AbilityGroups = ["Heal", "Assist", "Attack", "Skill"];

    public static readonly (int Bit, string Name)[] ElementBits =
        [(0x01, "Fire"), (0x02, "Ice"), (0x04, "Elec"), (0x08, "Earth"), (0x10, "Wind"), (0x20, "Holy")];

    public static string ElementText(int bits) =>
        string.Join("/", ElementBits.Where(e => (bits & e.Bit) != 0).Select(e => e.Name));

    public const int SlotShield = 2;
    public const int SlotHelm = 3;
    public const int SlotBody = 4;

    public static string NameOrId(string[] table, int id) =>
        id >= 0 && id < table.Length ? table[id] : $"#{id:X2}";
}
