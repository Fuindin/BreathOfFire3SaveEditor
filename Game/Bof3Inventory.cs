using BreathOfFire3SaveEditor.SaveFormat;

namespace BreathOfFire3SaveEditor.Game;

/// <summary>The four bags the game keeps, in record order.</summary>
public enum Bag
{
    Items = 0,
    Weapons = 1,
    Armor = 2,
    Accessories = 3,
}

/// <summary>One line of a bag: an item id and how many are carried.</summary>
public sealed class BagEntry
{
    public int Id { get; set; }
    public int Count { get; set; }
}

/// <summary>
/// The party inventory block.
///
/// <code>
///   +0x000  u32 zenny
///   +0x0FC  ids:        items[128], weapons[128], armour[128], accessories[128]
///   +0x2FC  quantities: same order
///   +0x4FC  key items[32]
/// </code>
///
/// Each bag is a packed list: entries run from slot 0 with no gaps and the rest
/// are zero. <see cref="WriteBag"/> keeps it that way.
/// </summary>
public sealed class Bof3Inventory
{
    public const int SlotsPerBag = 128;
    public const int MaxCount = 99;
    public const uint MaxZenny = 999_999;

    private const int IdTable = 0x0FC;
    private const int CountTable = 0x2FC;
    private const int VitalTable = 0x4FC;

    private readonly DuckStationState _state;

    public Bof3Inventory(DuckStationState state, int address)
    {
        _state = state;
        Address = address;
    }

    public int Address { get; }

    public uint Zenny
    {
        get => _state.Ram32(Address);
        set => _state.SetRam32(Address, Math.Min(value, MaxZenny));
    }

    public static int TableLength(Bag bag) => bag switch
    {
        Bag.Items => Bof3Data.Items.Length,
        Bag.Weapons => Bof3Data.Weapons.Length,
        Bag.Armor => Bof3Data.Armor.Length,
        _ => Bof3Data.Accessories.Length,
    };

    public static string ItemName(Bag bag, int id) => bag switch
    {
        Bag.Items => id < Bof3Data.Items.Length ? Bof3Data.Items[id].Name : $"#{id:X2}",
        Bag.Weapons => id < Bof3Data.Weapons.Length ? Bof3Data.Weapons[id].Name : $"#{id:X2}",
        Bag.Armor => id < Bof3Data.Armor.Length ? Bof3Data.Armor[id].Name : $"#{id:X2}",
        _ => id < Bof3Data.Accessories.Length ? Bof3Data.Accessories[id].Name : $"#{id:X2}",
    };

    /// <summary>The non-empty entries of a bag, in game order.</summary>
    public List<BagEntry> ReadBag(Bag bag)
    {
        var list = new List<BagEntry>();
        int b = (int)bag;
        for (int i = 0; i < SlotsPerBag; i++)
        {
            int id = _state.Ram8(Address + IdTable + b * SlotsPerBag + i);
            int count = _state.Ram8(Address + CountTable + b * SlotsPerBag + i);
            if (id != 0)
            {
                list.Add(new BagEntry { Id = id, Count = count });
            }
        }

        return list;
    }

    /// <summary>Writes a bag back packed from slot 0, dropping blank lines.</summary>
    public void WriteBag(Bag bag, IEnumerable<BagEntry> entries)
    {
        int b = (int)bag;
        var kept = entries.Where(e => e.Id > 0 && e.Id < TableLength(bag) && e.Count > 0)
                          .Take(SlotsPerBag)
                          .ToList();

        for (int i = 0; i < SlotsPerBag; i++)
        {
            int id = i < kept.Count ? kept[i].Id : 0;
            int count = i < kept.Count ? Math.Clamp(kept[i].Count, 1, MaxCount) : 0;
            _state.SetRam8(Address + IdTable + b * SlotsPerBag + i, id);
            _state.SetRam8(Address + CountTable + b * SlotsPerBag + i, count);
        }
    }

    public IEnumerable<int> VitalItems()
    {
        for (int i = 0; i < 32; i++)
        {
            int id = _state.Ram8(Address + VitalTable + i);
            if (id != 0)
            {
                yield return id;
            }
        }
    }
}
