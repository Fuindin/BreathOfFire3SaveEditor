using BreathOfFire3SaveEditor.SaveFormat;

namespace BreathOfFire3SaveEditor.Game;

/// <summary>
/// One 0xA4 byte party record, read and written in place in the state's RAM.
///
/// <code>
///   0x00  name[5]               0x06  level            0x08  u32 EXP
///   0x0E  weapon, shield, helm, armour, accessory 1, accessory 2
///   0x14  u16 HP                0x16  u16 AP           0x19  fatigue
///   0x1B  master (0xFF = none)
///   0x1C  "current" block: max HP, max AP, PWR, DEF, AGL, INT, ...  (totals the game uses)
///   0x3C  "base" block:    max HP, max AP, PWR, DEF, AGL, INT,
///         0x4A willpower, 0x4B resistances[9],
///         0x54 surprise, reprisal, critical, dodge, accuracy
///   0x5C  abilities: heal[10], assist[10], attack[10], skill[10]
///   0x84  apprenticing level    0x85  stat growth (signed) HP, AP, PWR, DEF, AGL, INT
/// </code>
///
/// The totals in the current block are base + equipment: PWR adds the weapon's
/// attack, DEF adds the shield/helm/armour defence, AGL loses each item's weight,
/// and accessories add their own bonuses on top.
/// </summary>
public sealed class Bof3Character
{
    public const int RecordSize = 0xA4;
    public const int MaxStat = 999;
    public const int MaxLevel = 99;
    public const uint MaxExp = 9_999_999;

    private readonly DuckStationState _state;
    private readonly int _address;

    // Whatever the totals hold beyond base + weapon/armour: the accessory bonuses.
    // Kept while the accessories are unchanged so editing a stat does not drop them.
    private readonly int[] _accessoryBonus = new int[4];
    private readonly int _loadedAccessory1;
    private readonly int _loadedAccessory2;

    public Bof3Character(DuckStationState state, int address, int index)
    {
        _state = state;
        _address = address;
        Index = index;

        _loadedAccessory1 = Equipment(4);
        _loadedAccessory2 = Equipment(5);
        int[] expected = ExpectedTotalsWithoutAccessories();
        for (int i = 0; i < 4; i++)
        {
            _accessoryBonus[i] = Total(i) - expected[i];
        }
    }

    public int Index { get; }
    public int Address => _address;
    public string ClassName => Bof3Names.Characters[Index];

    private byte U8(int o) => _state.Ram8(_address + o);
    private int U16(int o) => _state.Ram16(_address + o);
    private void Set8(int o, int v) => _state.SetRam8(_address + o, v);
    private void Set16(int o, int v) => _state.SetRam16(_address + o, v);

    public string Name
    {
        get => Bof3Text.Read(_state.RamSpan.Slice(_address, 5));
        set
        {
            byte[] bytes = Bof3Text.Write(value, 5);
            for (int i = 0; i < 5; i++)
            {
                Set8(i, bytes[i]);
            }
        }
    }

    public int Level
    {
        get => U8(0x06);
        set => Set8(0x06, Math.Clamp(value, 1, MaxLevel));
    }

    public uint Exp
    {
        get => _state.Ram32(_address + 0x08);
        set => _state.SetRam32(_address + 0x08, Math.Min(value, MaxExp));
    }

    public int Hp
    {
        get => U16(0x14);
        set => Set16(0x14, Math.Clamp(value, 0, MaxStat));
    }

    public int Ap
    {
        get => U16(0x16);
        set => Set16(0x16, Math.Clamp(value, 0, MaxStat));
    }

    /// <summary>
    /// Max HP. Writes both the base value and the current one, which can sit
    /// lower when the hidden max HP penalty is active.
    /// </summary>
    public int MaxHp
    {
        get => U16(0x3C);
        set
        {
            int v = Math.Clamp(value, 1, MaxStat);
            Set16(0x3C, v);
            Set16(0x1C, v);
            if (Hp > v)
            {
                Hp = v;
            }
        }
    }

    public int MaxAp
    {
        get => U16(0x3E);
        set
        {
            int v = Math.Clamp(value, 0, MaxStat);
            Set16(0x3E, v);
            Set16(0x1E, v);
            if (Ap > v)
            {
                Ap = v;
            }
        }
    }

    /// <summary>Max HP the game is currently using (lower than <see cref="MaxHp"/> after Disembowel and the like).</summary>
    public int CurrentMaxHp => U16(0x1C);

    public int CurrentMaxAp => U16(0x1E);

    /// <summary>Base stat: 0 PWR, 1 DEF, 2 AGL, 3 INT.</summary>
    public int Base(int stat) => U16(0x40 + stat * 2);

    /// <summary>Total stat including equipment: 0 PWR, 1 DEF, 2 AGL, 3 INT.</summary>
    public int Total(int stat) => U16(0x20 + stat * 2);

    public void SetBase(int stat, int value)
    {
        Set16(0x40 + stat * 2, Math.Clamp(value, 0, MaxStat));
        UpdateTotals();
    }

    public int Fatigue
    {
        get => U8(0x19);
        set => Set8(0x19, Math.Clamp(value, 0, 255));
    }

    public int Master => U8(0x1B);

    public int ApprenticeLevel => U8(0x84);

    public int Willpower => U8(0x4A);

    public int Resistance(int i) => U8(0x4B + i);

    public int Surprise => U8(0x54);
    public int Reprisal => U8(0x55);
    public int Critical => U8(0x56);
    public int Dodge => U8(0x57);
    public int Accuracy => U8(0x58);

    /// <summary>Signed stat growth: 0 HP, 1 AP, 2 PWR, 3 DEF, 4 AGL, 5 INT.</summary>
    public int Growth(int i) => (sbyte)U8(0x85 + i);

    /// <summary>The ten ability ids of a group (0 Heal, 1 Assist, 2 Attack, 3 Skill).</summary>
    public int[] Abilities(int group)
    {
        var ids = new int[10];
        for (int i = 0; i < 10; i++)
        {
            ids[i] = U8(0x5C + group * 10 + i);
        }

        return ids;
    }

    /// <summary>Slot: 0 weapon, 1 shield, 2 helm, 3 armour, 4 and 5 accessories.</summary>
    public int Equipment(int slot) => U8(0x0E + slot);

    public void SetEquipment(int slot, int id)
    {
        Set8(0x0E + slot, id);
        UpdateTotals();
    }

    /// <summary>Bit for this character in an item's equip mask.</summary>
    public int EquipBit => 1 << Index;

    public bool HasAccessoryChanges =>
        !SameAccessories(Equipment(4), Equipment(5), _loadedAccessory1, _loadedAccessory2);

    /// <summary>
    /// Rewrites the totals from base + equipment. Accessory bonuses cannot be
    /// derived from the item tables, so the amount seen at load is carried over
    /// while the same accessories are worn; the game restores the right figure
    /// the next time equipment is changed in its own menu.
    /// </summary>
    private void UpdateTotals()
    {
        int[] totals = ExpectedTotalsWithoutAccessories();
        bool keepBonus = !HasAccessoryChanges;
        for (int i = 0; i < 4; i++)
        {
            int value = totals[i] + (keepBonus ? _accessoryBonus[i] : 0);
            Set16(0x20 + i * 2, Math.Clamp(value, 0, MaxStat));
        }
    }

    private int[] ExpectedTotalsWithoutAccessories()
    {
        int weapon = Equipment(0);
        int attack = 0, defense = 0, weight = 0;

        if (weapon > 0 && weapon < Bof3Data.Weapons.Length)
        {
            attack = Bof3Data.Weapons[weapon].Attack;
            weight += Bof3Data.Weapons[weapon].Weight;
        }

        for (int slot = 1; slot <= 3; slot++)
        {
            int id = Equipment(slot);
            if (id > 0 && id < Bof3Data.Armor.Length)
            {
                defense += Bof3Data.Armor[id].Defense;
                weight += Bof3Data.Armor[id].Weight;
            }
        }

        return [Base(0) + attack, Base(1) + defense, Math.Max(0, Base(2) - weight), Base(3)];
    }

    private static bool SameAccessories(int a1, int a2, int b1, int b2) =>
        (a1 == b1 && a2 == b2) || (a1 == b2 && a2 == b1);

    /// <summary>Loose check that RAM at an address really holds a party record.</summary>
    public static bool LooksValid(ReadOnlySpan<byte> ram, int address)
    {
        if (address < 0 || address + RecordSize > ram.Length)
        {
            return false;
        }

        var r = ram.Slice(address, RecordSize);
        for (int i = 0; i < 5; i++)
        {
            if (!Bof3Text.IsNameByte(r[i]))
            {
                return false;
            }
        }

        int maxHp = r[0x3C] | (r[0x3D] << 8);
        return r[0x06] <= MaxLevel
            && maxHp <= 9999
            && r[0x0E] < Bof3Data.Weapons.Length
            && r[0x0F] < Bof3Data.Armor.Length
            && r[0x10] < Bof3Data.Armor.Length
            && r[0x11] < Bof3Data.Armor.Length
            && r[0x12] < Bof3Data.Accessories.Length
            && r[0x13] < Bof3Data.Accessories.Length;
    }
}
