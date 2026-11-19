using System.Runtime.InteropServices;

using BreathOfFire3SaveEditor.Game;
using BreathOfFire3SaveEditor.Ui;

namespace BreathOfFire3SaveEditor;

public sealed class MainForm : Form
{
    private const string AppTitle = "Breath of Fire III Save State Editor";
    private const string FileFilter = "DuckStation save states (*.sav)|*.sav|All files (*.*)|*.*";
    private const string DefaultSaveFolder = @"D:\Emulation\PS1\duckstation-windows-x64-release\Saves";

    private static readonly string[] StatNames = ["Pwr", "Def", "Agl", "Int"];
    private static readonly string[] SlotNames = ["Weapon", "Shield", "Helmet", "Armor", "Accessory", "Accessory"];
    private static readonly string[] BagNames = ["Items", "Weapons", "Armor", "Options"];

    private Bof3Save? _save;
    private Bof3Character? _current;
    private bool _dirty;
    private bool _binding;
    private Bag _bag = Bag.Items;

    // ------------------------------------------------------------- header
    private readonly Bof3Frame _shotFrame = new();
    private readonly ScreenshotView _shot = new();
    private readonly Bof3Frame _infoFrame = new();
    private readonly Bof3InfoList _info = new() { LineHeight = 26 };
    private readonly Bof3Frame _zennyFrame = new();
    private readonly Bof3NumberField _zenny = new()
        { Caption = "ZENNY", CaptionWidth = 104, BoxWidth = 120, Maximum = Bof3Inventory.MaxZenny };

    private readonly Bof3TabStrip _tabs = new() { Tabs = ["Party", "Inventory"] };
    private readonly WoodPanel _partyPage = new();
    private readonly WoodPanel _inventoryPage = new();

    // ----------------------------------------------------------- party page
    private readonly Bof3Frame _partyFrame = new();
    private readonly Bof3PartyList _party = new();

    private readonly Bof3Frame _statusFrame = new() { Title = "Status" };
    private readonly PortraitView _portrait = new();
    private readonly Bof3TextField _name = new() { Caption = "NAME", CaptionWidth = 74, BoxWidth = 130, MaxLength = 5 };
    private readonly Bof3NumberField _level = new() { Caption = "LV", CaptionWidth = 74, BoxWidth = 64, Minimum = 1, Maximum = Bof3Character.MaxLevel };
    private readonly Bof3NumberField _exp = new() { Caption = "EXP", CaptionWidth = 74, BoxWidth = 130, Maximum = Bof3Character.MaxExp };
    private readonly Bof3NumberField _hp = new() { Caption = "HP", CaptionWidth = 74, BoxWidth = 64, Paired = true, SecondMinimum = 1 };
    private readonly Bof3NumberField _ap = new() { Caption = "AP", CaptionWidth = 74, BoxWidth = 64, Paired = true };

    private readonly Bof3Frame _attrFrame = new() { Title = "Attributes" };
    private readonly Bof3NumberField[] _stats = new Bof3NumberField[4];
    private readonly Bof3InfoList _hidden = new() { Columns = 2, LineHeight = 21 };

    private readonly Bof3Frame _equipFrame = new() { Title = "Equipment" };
    private readonly Bof3ComboField[] _equip = new Bof3ComboField[6];
    private readonly Bof3InfoList _equipNote = new() { LineHeight = 20 };

    private readonly Bof3Frame _abilityFrame = new() { Title = "Resistances & Abilities" };
    private readonly Bof3InfoList _resist = new() { Columns = 3, LineHeight = 23 };
    private readonly Bof3InfoList _abilities = new() { Columns = 4, LineHeight = 18 };

    // ------------------------------------------------------- inventory page
    private readonly Bof3Frame _bagFrame = new() { Title = "Inventory" };
    private readonly Bof3TabStrip _bagTabs = new() { Tabs = BagNames, TabWidth = 130 };
    private readonly DataGridView _grid = new();
    private readonly Bof3Button _add = new() { Text = "Add" };
    private readonly Bof3Button _remove = new() { Text = "Remove" };
    private readonly Bof3Button _maxAll = new() { Text = "All to 99", Width = 120 };
    private readonly Bof3Frame _keyFrame = new() { Title = "Key Items" };
    private readonly Bof3InfoList _keyItems = new() { LineHeight = 24 };
    private List<BagEntry> _bagEntries = [];

    private ToolStripMenuItem _saveItem = null!;
    private ToolStripMenuItem _saveAsItem = null!;
    private ToolStripMenuItem _reloadItem = null!;
    private MenuStrip _menu = null!;

    public MainForm()
    {
        Text = AppTitle;
        BackColor = Bof3Theme.Wood;
        ForeColor = Bof3Theme.TextSilver;
        Font = Bof3Theme.Text(12f);
        ClientSize = new Size(1300, 940);
        MinimumSize = new Size(1180, 860);
        StartPosition = FormStartPosition.CenterScreen;
        DoubleBuffered = true;

        BuildMenu();
        BuildHeader();
        BuildPartyPage();
        BuildInventoryPage();

        _tabs.SelectedIndexChanged += (_, _) => ShowPage();
        Controls.Add(_tabs);
        Controls.Add(_partyPage);
        Controls.Add(_inventoryPage);
        ShowPage();

        Layout += (_, _) => ArrangeForm();
        Shown += (_, _) => TryReopenLastFile();
        FormClosing += OnFormClosing;

        Bind();
    }

    protected override void OnPaintBackground(PaintEventArgs e) =>
        Bof3Theme.PaintWood(this, e.Graphics, ClientRectangle);

    /// <summary>Dark title bar to sit with the theme.</summary>
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        int enabled = 1;
        try
        {
            DwmSetWindowAttribute(Handle, 20, ref enabled, sizeof(int));
        }
        catch (DllNotFoundException)
        {
        }
        catch (EntryPointNotFoundException)
        {
        }
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    // ================================================================ layout

    private void BuildMenu()
    {
        var open = new ToolStripMenuItem("&Open...", null, (_, _) => OpenFile()) { ShortcutKeys = Keys.Control | Keys.O };
        _saveItem = new ToolStripMenuItem("&Save", null, (_, _) => SaveFile(null)) { ShortcutKeys = Keys.Control | Keys.S };
        _saveAsItem = new ToolStripMenuItem("Save &As...", null, (_, _) => SaveAs());
        _reloadItem = new ToolStripMenuItem("&Reload from disk", null, (_, _) => Reload()) { ShortcutKeys = Keys.F5 };
        var exit = new ToolStripMenuItem("E&xit", null, (_, _) => Close());

        var file = new ToolStripMenuItem("&File");
        file.DropDownItems.AddRange(new ToolStripItem[]
        {
            open, _saveItem, _saveAsItem, new ToolStripSeparator(), _reloadItem, new ToolStripSeparator(), exit,
        });

        var help = new ToolStripMenuItem("&Help");
        help.DropDownItems.Add(new ToolStripMenuItem("&About", null, (_, _) => ShowAbout()));

        _menu = new MenuStrip
        {
            BackColor = Bof3Theme.InsetFill,
            ForeColor = Bof3Theme.TextSilver,
            Renderer = new ToolStripProfessionalRenderer(new Bof3MenuColours()),
            Font = Bof3Theme.Text(11.5f),
        };
        _menu.Items.AddRange(new ToolStripItem[] { file, help });
        foreach (ToolStripMenuItem top in _menu.Items)
        {
            foreach (ToolStripItem item in top.DropDownItems)
            {
                item.ForeColor = Bof3Theme.TextSilver;
            }
        }

        MainMenuStrip = _menu;
        Controls.Add(_menu);
    }

    private void BuildHeader()
    {
        _shotFrame.Controls.Add(_shot);
        _shotFrame.Layout += (_, _) => _shot.Bounds = InnerRect(_shotFrame);

        new RowStack(_infoFrame, 0).AddFill(_info);

        _zennyFrame.Layout += (_, _) =>
        {
            var r = InnerRect(_zennyFrame);
            _zenny.Bounds = new Rectangle(r.X, r.Y + (r.Height - 36) / 2, r.Width, 36);
        };
        _zennyFrame.Controls.Add(_zenny);
        _zenny.UserEdited += (_, _) =>
        {
            if (Editing(out var save))
            {
                save.Inventory.Zenny = (uint)_zenny.Value;
                MarkDirty();
            }
        };

        Controls.Add(_shotFrame);
        Controls.Add(_infoFrame);
        Controls.Add(_zennyFrame);
    }

    private void BuildPartyPage()
    {
        _partyFrame.Controls.Add(_party);
        _partyFrame.Layout += (_, _) => _party.Bounds = InnerRect(_partyFrame);
        _party.SelectedIndexChanged += (_, _) => BindCharacter();

        // Status: portrait on the left, fields to its right.
        _statusFrame.Controls.Add(_portrait);
        foreach (var c in new Control[] { _name, _level, _exp, _hp, _ap })
        {
            _statusFrame.Controls.Add(c);
        }

        _statusFrame.Layout += (_, _) =>
        {
            var r = InnerRect(_statusFrame);
            _portrait.Bounds = new Rectangle(r.X, r.Y + 2, 112, 142);
            int x = r.X + 128;
            int w = r.Right - x;
            int y = r.Y;
            foreach (var c in new Control[] { _name, _level, _exp })
            {
                c.Bounds = new Rectangle(x, y, w, 36);
                y += 40;
            }

            y = Math.Max(y, _portrait.Bottom + 10);
            _hp.Bounds = new Rectangle(r.X, y, r.Width, 36);
            _ap.Bounds = new Rectangle(r.X, y + 40, r.Width, 36);
        };

        _name.KeyFilter = c => Bof3Text.CanEncode(c, out _);
        _name.UserEdited += (_, _) => Edit(c => c.Name = _name.Text, refreshParty: true);
        _level.UserEdited += (_, _) => Edit(c => c.Level = (int)_level.Value, refreshParty: true);
        _exp.UserEdited += (_, _) => Edit(c => c.Exp = (uint)_exp.Value);
        _hp.UserEdited += (_, _) => Edit(c =>
        {
            c.MaxHp = (int)_hp.SecondValue;
            c.Hp = (int)Math.Min(_hp.Value, _hp.SecondValue);
        }, refreshParty: true);
        _ap.UserEdited += (_, _) => Edit(c =>
        {
            c.MaxAp = (int)_ap.SecondValue;
            c.Ap = (int)Math.Min(_ap.Value, _ap.SecondValue);
        }, refreshParty: true);

        // Attributes: the four base stats, then the hidden ones.
        var attrStack = new RowStack(_attrFrame);
        for (int i = 0; i < 4; i++)
        {
            int stat = i;
            _stats[i] = new Bof3NumberField { Caption = StatNames[i].ToUpperInvariant(), CaptionWidth = 74, BoxWidth = 64 };
            _stats[i].UserEdited += (_, _) => Edit(c => c.SetBase(stat, (int)_stats[stat].Value), refreshNotes: true);
            attrStack.Add(_stats[i], 34);
        }

        attrStack.AddFill(_hidden);

        // Equipment: one drop down per slot.
        var equipStack = new RowStack(_equipFrame);
        for (int i = 0; i < 6; i++)
        {
            int slot = i;
            _equip[i] = new Bof3ComboField { Caption = SlotNames[i], CaptionWidth = 96, ComboWidth = 196 };
            _equip[i].Combo.SelectionChangeCommitted += (_, _) =>
                Edit(c => c.SetEquipment(slot, _equip[slot].Combo.SelectedId), refreshNotes: true);
            equipStack.Add(_equip[i], 36);
        }

        _equipNote.Font = Bof3Theme.Text(11f);
        _abilities.Font = Bof3Theme.Text(11.5f);
        equipStack.AddFill(_equipNote);

        var abilityStack = new RowStack(_abilityFrame, 6);
        abilityStack.Add(_resist, 3 * 23);
        abilityStack.AddFill(_abilities);

        _partyPage.Controls.AddRange(new Control[] { _partyFrame, _statusFrame, _attrFrame, _equipFrame, _abilityFrame });
        _partyPage.Layout += (_, _) => ArrangePartyPage();
    }

    private void BuildInventoryPage()
    {
        StyleGrid();

        var buttons = new Panel { BackColor = Bof3Theme.PanelFill, Height = 36 };
        _add.Location = new Point(0, 2);
        _remove.Location = new Point(_add.Right + 10, 2);
        _maxAll.Location = new Point(_remove.Right + 10, 2);
        buttons.Controls.AddRange(new Control[] { _add, _remove, _maxAll });

        _bagTabs.BackColor = Bof3Theme.PanelFill;
        _bagTabs.PaintBackdrop = false;
        var stack = new RowStack(_bagFrame, 8);
        stack.Add(_bagTabs, 44);
        stack.AddFill(_grid);
        stack.Add(buttons, 36);

        _bagTabs.SelectedIndexChanged += (_, _) =>
        {
            _bag = (Bag)_bagTabs.SelectedIndex;
            BindBag();
        };

        _add.Click += (_, _) => AddBagEntry();
        _remove.Click += (_, _) => RemoveBagEntry();
        _maxAll.Click += (_, _) =>
        {
            foreach (var entry in _bagEntries)
            {
                entry.Count = Bof3Inventory.MaxCount;
            }

            WriteBag();
            BindBag();
        };

        new RowStack(_keyFrame).AddFill(_keyItems);

        _inventoryPage.Controls.AddRange(new Control[] { _bagFrame, _keyFrame });
        _inventoryPage.Layout += (_, _) =>
        {
            var r = _inventoryPage.ClientRectangle;
            int keyWidth = 300;
            _bagFrame.Bounds = new Rectangle(0, 0, r.Width - keyWidth - 12, r.Height);
            _keyFrame.Bounds = new Rectangle(r.Width - keyWidth, 0, keyWidth, r.Height);
        };
    }

    private void ArrangeForm()
    {
        const int margin = 14;
        int top = _menu.Bottom + 8;
        int width = ClientSize.Width - 2 * margin;

        const int headerHeight = 132;
        int shotWidth = 160 + 2 * (Bof3Theme.FrameBand + 8);
        int zennyWidth = 300;
        _shotFrame.Bounds = new Rectangle(margin, top, shotWidth, headerHeight);
        _zennyFrame.Bounds = new Rectangle(margin + width - zennyWidth, top, zennyWidth, 76);
        _infoFrame.Bounds = new Rectangle(_shotFrame.Right + 12, top, _zennyFrame.Left - _shotFrame.Right - 24, headerHeight);

        int tabsTop = top + headerHeight + 10;
        _tabs.Bounds = new Rectangle(margin, tabsTop, width, 44);

        var page = new Rectangle(margin, _tabs.Bottom + 8, width, ClientSize.Height - _tabs.Bottom - 8 - margin);
        _partyPage.Bounds = page;
        _inventoryPage.Bounds = page;

        // Planks are aligned to the form, so repaint everything that shows them.
        Invalidate(true);
    }

    private void ArrangePartyPage()
    {
        var r = _partyPage.ClientRectangle;
        const int gap = 12;

        int listWidth = 340;
        _partyFrame.Bounds = new Rectangle(0, 0, listWidth, r.Height);

        int x = listWidth + gap;
        int rest = r.Width - x;
        int leftWidth = Math.Max(400, rest * 46 / 100);
        int rightWidth = rest - leftWidth - gap;

        int statusHeight = 290;
        _statusFrame.Bounds = new Rectangle(x, 0, leftWidth, statusHeight);
        _attrFrame.Bounds = new Rectangle(x, statusHeight + gap, leftWidth, r.Height - statusHeight - gap);

        int equipHeight = 340;
        int rx = x + leftWidth + gap;
        _equipFrame.Bounds = new Rectangle(rx, 0, rightWidth, equipHeight);
        _abilityFrame.Bounds = new Rectangle(rx, equipHeight + gap, rightWidth, r.Height - equipHeight - gap);
    }

    private static Rectangle InnerRect(Control c) => new(
        c.Padding.Left, c.Padding.Top, Math.Max(0, c.ClientSize.Width - c.Padding.Horizontal),
        Math.Max(0, c.ClientSize.Height - c.Padding.Vertical));

    private void ShowPage()
    {
        _partyPage.Visible = _tabs.SelectedIndex == 0;
        _inventoryPage.Visible = _tabs.SelectedIndex == 1;
    }

    // ============================================================ grid

    private void StyleGrid()
    {
        var g = _grid;
        g.BackgroundColor = Bof3Theme.InsetFill;
        g.BorderStyle = BorderStyle.None;
        g.GridColor = Bof3Theme.PanelFill;
        g.EnableHeadersVisualStyles = false;
        g.RowHeadersVisible = false;
        g.AllowUserToAddRows = false;
        g.AllowUserToDeleteRows = false;
        g.AllowUserToResizeRows = false;
        g.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        g.MultiSelect = false;
        g.AutoGenerateColumns = false;
        g.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        g.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
        g.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        g.ColumnHeadersHeight = 32;
        g.RowTemplate.Height = 30;
        g.EditMode = DataGridViewEditMode.EditOnEnter;

        g.DefaultCellStyle.BackColor = Bof3Theme.InsetFill;
        g.DefaultCellStyle.ForeColor = Bof3Theme.TextWhite;
        g.DefaultCellStyle.SelectionBackColor = Bof3Theme.SelectFill;
        g.DefaultCellStyle.SelectionForeColor = Bof3Theme.TextWhite;
        g.DefaultCellStyle.Font = Bof3Theme.Text(13f);
        g.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(0x2B, 0x2B, 0x2A);
        g.ColumnHeadersDefaultCellStyle.BackColor = Bof3Theme.PanelFill;
        g.ColumnHeadersDefaultCellStyle.ForeColor = Bof3Theme.LabelMid;
        g.ColumnHeadersDefaultCellStyle.SelectionBackColor = Bof3Theme.PanelFill;
        g.ColumnHeadersDefaultCellStyle.Font = Bof3Theme.Text(12.5f, FontStyle.Bold);

        g.Columns.Add(new DataGridViewTextBoxColumn
            { Name = "Slot", HeaderText = "#", ReadOnly = true, Width = 48, SortMode = DataGridViewColumnSortMode.NotSortable });
        g.Columns.Add(new DataGridViewComboBoxColumn
        {
            Name = "Item",
            HeaderText = "Item",
            Width = 240,
            FlatStyle = FlatStyle.Flat,
            DisplayStyle = DataGridViewComboBoxDisplayStyle.Nothing,
            ValueMember = nameof(ComboEntry.Id),
            DisplayMember = nameof(ComboEntry.Text),
            SortMode = DataGridViewColumnSortMode.NotSortable,
            MaxDropDownItems = 20,
        });
        g.Columns.Add(new DataGridViewTextBoxColumn
            { Name = "Count", HeaderText = "Qty", Width = 70, SortMode = DataGridViewColumnSortMode.NotSortable });
        g.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Info",
            HeaderText = "Details",
            ReadOnly = true,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            SortMode = DataGridViewColumnSortMode.NotSortable,
            DefaultCellStyle = { ForeColor = Bof3Theme.TextDim },
        });

        g.CurrentCellDirtyStateChanged += (_, _) =>
        {
            if (g.IsCurrentCellDirty && g.CurrentCell is DataGridViewComboBoxCell)
            {
                g.CommitEdit(DataGridViewDataErrorContexts.Commit);
            }
        };
        g.CellValueChanged += OnGridValueChanged;
        g.DataError += (_, e) => e.ThrowException = false;
        g.EditingControlShowing += (_, e) =>
        {
            if (e.Control is ComboBox combo)
            {
                combo.BackColor = Bof3Theme.InsetFill;
                combo.ForeColor = Bof3Theme.TextWhite;
                combo.FlatStyle = FlatStyle.Flat;
            }
            else if (e.Control is TextBox box)
            {
                box.BackColor = Bof3Theme.InsetFill;
                box.ForeColor = Bof3Theme.TextWhite;
                box.KeyPress -= DigitsOnly;
                box.KeyPress += DigitsOnly;
            }
        };
    }

    private static void DigitsOnly(object? sender, KeyPressEventArgs e)
    {
        if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar))
        {
            e.Handled = true;
        }
    }

    private void OnGridValueChanged(object? sender, DataGridViewCellEventArgs e)
    {
        if (_binding || e.RowIndex < 0 || e.RowIndex >= _bagEntries.Count)
        {
            return;
        }

        var row = _grid.Rows[e.RowIndex];
        var entry = _bagEntries[e.RowIndex];

        if (e.ColumnIndex == 1 && row.Cells[1].Value is int id)
        {
            entry.Id = id;
            if (entry.Count <= 0)
            {
                entry.Count = 1;
            }
        }
        else if (e.ColumnIndex == 2)
        {
            int.TryParse(Convert.ToString(row.Cells[2].Value), out int count);
            entry.Count = Math.Clamp(count, 1, Bof3Inventory.MaxCount);
        }

        WriteBag();

        // Show the tidied values (count clamped, details for the new item).
        _binding = true;
        row.Cells[2].Value = entry.Count.ToString();
        row.Cells[3].Value = ItemDetail(_bag, entry.Id);
        _binding = false;
    }

    private void AddBagEntry()
    {
        if (_save is null)
        {
            return;
        }

        if (_bagEntries.Count >= Bof3Inventory.SlotsPerBag)
        {
            Warn("That bag is full (128 entries).");
            return;
        }

        _bagEntries.Add(new BagEntry { Id = 1, Count = 1 });
        WriteBag();
        BindBag();
        if (_grid.Rows.Count > 0)
        {
            _grid.CurrentCell = _grid.Rows[_grid.Rows.Count - 1].Cells[1];
            _grid.BeginEdit(true);
        }
    }

    private void RemoveBagEntry()
    {
        if (_save is null || _grid.CurrentRow is null)
        {
            return;
        }

        int index = _grid.CurrentRow.Index;
        if (index < 0 || index >= _bagEntries.Count)
        {
            return;
        }

        _bagEntries.RemoveAt(index);
        WriteBag();
        BindBag();
    }

    private void WriteBag()
    {
        if (_save is null)
        {
            return;
        }

        _save.Inventory.WriteBag(_bag, _bagEntries);
        MarkDirty();
    }

    // ============================================================ binding

    private bool Editing(out Bof3Save save)
    {
        save = _save!;
        return !_binding && _save is not null;
    }

    /// <summary>Applies a change to the selected character and refreshes what depends on it.</summary>
    private void Edit(Action<Bof3Character> change, bool refreshParty = false, bool refreshNotes = false)
    {
        if (_binding || _current is null)
        {
            return;
        }

        change(_current);
        MarkDirty();

        if (refreshNotes)
        {
            BindDerived();
        }

        if (refreshParty || refreshNotes)
        {
            RefreshPartyCards();
        }
    }

    private void Bind()
    {
        bool loaded = _save is not null;
        _saveItem.Enabled = loaded;
        _saveAsItem.Enabled = loaded;
        _reloadItem.Enabled = loaded;
        foreach (var c in new Control[] { _tabs, _partyPage, _inventoryPage, _zennyFrame })
        {
            c.Enabled = loaded;
        }

        UpdateInfo();

        if (_save is null)
        {
            _shot.SetImage(null, 0, 0);
            _party.Cards = [];
            _current = null;
            return;
        }

        _binding = true;
        _zenny.Value = _save.Inventory.Zenny;
        _binding = false;

        _shot.SetImage(_save.State.ScreenshotRgba, _save.State.ScreenshotWidth, _save.State.ScreenshotHeight);
        RefreshPartyCards();
        if (_party.SelectedIndex < 0)
        {
            _party.SelectedIndex = 0;
        }

        BindCharacter();
        BindBag();
        BindKeyItems();
    }

    private void RefreshPartyCards()
    {
        if (_save is null)
        {
            return;
        }

        int selected = _party.SelectedIndex;
        _party.Cards = _save.Characters
            .Select(c => new PartyCard(c.Name, c.ClassName, c.Level, c.Hp, c.CurrentMaxHp, c.Ap, c.CurrentMaxAp))
            .ToList();
        if (selected >= 0)
        {
            _party.SelectedIndex = selected;
        }
    }

    private void BindCharacter()
    {
        if (_save is null || _party.SelectedIndex < 0)
        {
            return;
        }

        var c = _save.Characters[_party.SelectedIndex];
        _current = c;
        _binding = true;
        try
        {
            _statusFrame.Title = c.Name == c.ClassName ? c.ClassName : $"{c.Name}   ({c.ClassName})";
            _portrait.ClassName = c.ClassName;
            _name.Text = c.Name;
            _level.Value = Math.Max(1, c.Level);
            _exp.Value = c.Exp;
            _hp.Value = c.Hp;
            _hp.SecondValue = Math.Max(1, c.MaxHp);
            _ap.Value = c.Ap;
            _ap.SecondValue = c.MaxAp;

            for (int i = 0; i < 4; i++)
            {
                _stats[i].Value = c.Base(i);
            }

            BindEquipmentLists(c);
            BindDerived();
            BindAbilities(c);
        }
        finally
        {
            _binding = false;
        }
    }

    /// <summary>Everything computed from stats and gear: totals, notes, hidden stats.</summary>
    private void BindDerived()
    {
        var c = _current;
        if (c is null)
        {
            return;
        }

        for (int i = 0; i < 4; i++)
        {
            int total = c.Total(i);
            int diff = total - c.Base(i);
            string sign = diff > 0 ? "+" : string.Empty;
            _stats[i].Note = diff == 0 ? $"total {total}" : $"total {total}  ({sign}{diff} gear)";
            _stats[i].NoteColour = diff > 0 ? Bof3Theme.TextGood : diff < 0 ? Bof3Theme.TextBad : Bof3Theme.TextDim;
        }

        _hp.Note = c.CurrentMaxHp < c.MaxHp ? $"max now {c.CurrentMaxHp}" : string.Empty;
        _ap.Note = c.CurrentMaxAp < c.MaxAp ? $"max now {c.CurrentMaxAp}" : string.Empty;

        for (int slot = 0; slot < 6; slot++)
        {
            _equip[slot].Note = EquipDetail(slot, c.Equipment(slot));
        }

        var note = c.HasAccessoryChanges
            ? ("Accessory bonus applies after the next in-game equip change.", string.Empty, (Color?)Bof3Theme.LabelMid)
            : ("Gear changed here is not taken from or put back in the bag.", string.Empty, (Color?)Bof3Theme.TextDim);
        _equipNote.Lines = [note];

        static string Signed(int v) => v > 0 ? $"+{v}" : v.ToString();
        _hidden.Lines =
        [
            ("Master", Bof3Names.MasterName(c.Master), null),
            ("Apprentice LV", c.ApprenticeLevel.ToString(), null),
            ("Fatigue", c.Fatigue.ToString(), null),
            ("Willpower", c.Willpower.ToString(), null),
            ("Surprise", c.Surprise.ToString(), null),
            ("Reprisal", c.Reprisal.ToString(), null),
            ("Critical", c.Critical.ToString(), null),
            ("Dodge", c.Dodge.ToString(), null),
            ("Accuracy", c.Accuracy.ToString(), null),
            ("Growth HP", Signed(c.Growth(0)), null),
            ("Growth AP", Signed(c.Growth(1)), null),
            ("Growth PWR", Signed(c.Growth(2)), null),
            ("Growth DEF", Signed(c.Growth(3)), null),
            ("Growth AGL", Signed(c.Growth(4)), null),
            ("Growth INT", Signed(c.Growth(5)), null),
        ];
    }

    private void BindEquipmentLists(Bof3Character c)
    {
        int bit = c.EquipBit;

        var weapons = Bof3Data.Weapons
            .Select((w, id) => (w, id))
            .Where(x => x.id == 0 || (x.w.EquipMask & bit) != 0 || x.id == c.Equipment(0))
            .Select(x => new ComboEntry(x.id, x.w.Name, x.id == 0 ? string.Empty : $"ATK {x.w.Attack}"));
        _equip[0].Combo.SetEntries(weapons, c.Equipment(0));

        int[] armorSlots = [Bof3Names.SlotShield, Bof3Names.SlotHelm, Bof3Names.SlotBody];
        for (int s = 0; s < 3; s++)
        {
            int slot = s + 1;
            int kind = armorSlots[s];
            var armor = Bof3Data.Armor
                .Select((a, id) => (a, id))
                .Where(x => x.id == 0 || (x.a.Slot == kind && (x.a.EquipMask & bit) != 0) || x.id == c.Equipment(slot))
                .Select(x => new ComboEntry(x.id, x.a.Name, x.id == 0 ? string.Empty : $"DEF {x.a.Defense}"));
            _equip[slot].Combo.SetEntries(armor, c.Equipment(slot));
        }

        for (int slot = 4; slot <= 5; slot++)
        {
            int current = c.Equipment(slot);
            var accessories = Bof3Data.Accessories
                .Select((a, id) => (a, id))
                .Where(x => x.id == 0 || (x.a.Kind == 5 && (x.a.EquipMask & bit) != 0) || x.id == current)
                .Select(x => new ComboEntry(x.id, x.a.Name));
            _equip[slot].Combo.SetEntries(accessories, current);
        }
    }

    private static string EquipDetail(int slot, int id)
    {
        if (id == 0)
        {
            return string.Empty;
        }

        if (slot == 0 && id < Bof3Data.Weapons.Length)
        {
            var w = Bof3Data.Weapons[id];
            string element = Bof3Names.ElementText(w.Element);
            return $"ATK +{w.Attack}" + (w.Weight > 0 ? $"   AGL -{w.Weight}" : string.Empty) +
                   (element.Length > 0 ? $"   {element}" : string.Empty);
        }

        if (slot is >= 1 and <= 3 && id < Bof3Data.Armor.Length)
        {
            var a = Bof3Data.Armor[id];
            return $"DEF +{a.Defense}" + (a.Weight > 0 ? $"   AGL -{a.Weight}" : string.Empty);
        }

        return string.Empty;
    }

    private void BindAbilities(Bof3Character c)
    {
        var resist = new List<(string, string, Color?)>();
        for (int i = 0; i < Bof3Names.Resistances.Length; i++)
        {
            resist.Add((Bof3Names.Resistances[i], c.Resistance(i).ToString(), null));
        }

        _resist.Lines = resist;

        // One column per group: each is padded to the same length so the
        // list's even split puts every group at the top of its own column.
        var groups = Enumerable.Range(0, 4)
            .Select(g => c.Abilities(g).Where(id => id != 0).Select(id => Bof3Names.NameOrId(Bof3Data.Abilities, id)).ToList())
            .ToList();
        int rows = Math.Max(1, groups.Max(g => g.Count)) + 1;

        var lines = new List<(string, string, Color?)>();
        for (int group = 0; group < 4; group++)
        {
            lines.Add((string.Empty, Bof3Names.AbilityGroups[group], null));
            List<string> names = groups[group].Count == 0 ? ["-"] : groups[group];
            foreach (string name in names)
            {
                lines.Add((name, string.Empty, Bof3Theme.TextSilver));
            }

            for (int pad = names.Count + 1; pad < rows; pad++)
            {
                lines.Add((string.Empty, string.Empty, null));
            }
        }

        _abilities.Lines = lines;
    }

    private void BindBag()
    {
        if (_save is null)
        {
            return;
        }

        _bagEntries = _save.Inventory.ReadBag(_bag);

        var column = (DataGridViewComboBoxColumn)_grid.Columns["Item"]!;
        int length = Bof3Inventory.TableLength(_bag);
        var choices = Enumerable.Range(1, length - 1)
            .Select(id => new ComboEntry(id, Bof3Inventory.ItemName(_bag, id)))
            .ToList();

        _binding = true;
        try
        {
            _grid.Rows.Clear();
            column.DataSource = choices;

            for (int i = 0; i < _bagEntries.Count; i++)
            {
                var entry = _bagEntries[i];
                int row = _grid.Rows.Add();
                var cells = _grid.Rows[row].Cells;
                cells[0].Value = (i + 1).ToString();
                cells[1].Value = entry.Id;
                cells[2].Value = entry.Count.ToString();
                cells[3].Value = ItemDetail(_bag, entry.Id);
            }
        }
        finally
        {
            _binding = false;
        }

        _bagFrame.Title = $"Inventory  -  {BagNames[(int)_bag]}: {_bagEntries.Count} / {Bof3Inventory.SlotsPerBag}";
    }

    private static string ItemDetail(Bag bag, int id)
    {
        switch (bag)
        {
            case Bag.Weapons when id < Bof3Data.Weapons.Length:
            {
                var w = Bof3Data.Weapons[id];
                return $"ATK {w.Attack}   {WhoCanEquip(w.EquipMask)}   {w.Price}Z";
            }
            case Bag.Armor when id < Bof3Data.Armor.Length:
            {
                var a = Bof3Data.Armor[id];
                string kind = a.Slot switch { 2 => "Shield", 3 => "Helmet", _ => "Armor" };
                return $"{kind}  DEF {a.Defense}   {WhoCanEquip(a.EquipMask)}   {a.Price}Z";
            }
            case Bag.Accessories when id < Bof3Data.Accessories.Length:
            {
                var a = Bof3Data.Accessories[id];
                string kind = a.Kind switch { 10 => "Bait", 11 => "Fishing rod", _ => WhoCanEquip(a.EquipMask) };
                return $"{kind}   {a.Price}Z";
            }
            case Bag.Items when id < Bof3Data.Items.Length:
                return $"{Bof3Data.Items[id].Price}Z";
            default:
                return string.Empty;
        }
    }

    private static string WhoCanEquip(int mask)
    {
        if ((mask & 0x7F) == 0x7F)
        {
            return "Everyone";
        }

        var names = Enumerable.Range(0, 7).Where(i => (mask & (1 << i)) != 0).Select(i => Bof3Names.Characters[i]);
        return string.Join(", ", names);
    }

    private void BindKeyItems()
    {
        if (_save is null)
        {
            return;
        }

        var lines = _save.Inventory.VitalItems()
            .Select(id => (Bof3Names.NameOrId(Bof3Data.VitalItems, id), string.Empty, (Color?)null))
            .ToList();
        if (lines.Count == 0)
        {
            lines.Add(("(none yet)", string.Empty, null));
        }

        _keyItems.Lines = lines;
    }

    private void UpdateInfo()
    {
        if (_save is null)
        {
            _info.Lines =
            [
                (string.Empty, "No save state loaded", null),
                ("Use File > Open to pick a DuckStation .sav file.", string.Empty, null),
            ];
            Text = AppTitle;
            return;
        }

        var s = _save.State;
        string file = Path.GetFileName(_save.Path);
        _info.Lines =
        [
            (string.Empty, file, null),
            ("Game", $"{s.Title}  {s.Serial}".Trim(), null),
            ("DuckStation state", $"version {s.Version}", null),
            (_dirty ? "Unsaved changes" : "Saved", _dirty ? "Ctrl+S to write the state" : "Load the state in DuckStation to see edits",
                _dirty ? Bof3Theme.LabelMid : Bof3Theme.TextDim),
        ];
        Text = $"{AppTitle} - {file}{(_dirty ? " *" : string.Empty)}";
    }

    private void MarkDirty()
    {
        if (!_dirty)
        {
            _dirty = true;
            UpdateInfo();
        }
    }

    // ============================================================== files

    private void TryReopenLastFile()
    {
        string? last = Settings.LastFile;
        if (!string.IsNullOrWhiteSpace(last) && File.Exists(last))
        {
            LoadFile(last, quiet: true);
        }
    }

    private void OpenFile()
    {
        if (!ConfirmDiscard())
        {
            return;
        }

        using var dialog = new OpenFileDialog
        {
            Title = "Open a DuckStation save state",
            Filter = FileFilter,
            InitialDirectory = InitialDirectory(),
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            LoadFile(dialog.FileName, quiet: false);
        }
    }

    private string InitialDirectory()
    {
        string? last = _save?.Path ?? Settings.LastFile;
        string? dir = string.IsNullOrWhiteSpace(last) ? null : Path.GetDirectoryName(last);
        if (Directory.Exists(dir))
        {
            return dir!;
        }

        return Directory.Exists(DefaultSaveFolder)
            ? DefaultSaveFolder
            : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
    }

    private void LoadFile(string path, bool quiet)
    {
        try
        {
            var save = Bof3Save.Load(path);
            _save = save;
            _dirty = false;
            Settings.LastFile = path;
            Bind();
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException
                                       or OverflowException or ZstdSharp.ZstdException)
        {
            if (!quiet)
            {
                Warn($"Could not read that save state.\n\n{ex.Message}");
            }
        }
    }

    private void SaveFile(string? path)
    {
        if (_save is null)
        {
            return;
        }

        // Push anything still being typed.
        Validate();
        _grid.EndEdit();
        foreach (var field in new[] { _level, _exp, _hp, _ap, _zenny }.Concat(_stats))
        {
            field.CommitPending();
        }

        try
        {
            _save.Save(path);
            _dirty = false;
            Settings.LastFile = _save.Path;
            UpdateInfo();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Warn($"Could not write the save state.\n\n{ex.Message}");
        }
    }

    private void SaveAs()
    {
        if (_save is null)
        {
            return;
        }

        using var dialog = new SaveFileDialog
        {
            Title = "Save state as",
            Filter = FileFilter,
            InitialDirectory = InitialDirectory(),
            FileName = Path.GetFileName(_save.Path),
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            SaveFile(dialog.FileName);
        }
    }

    private void Reload()
    {
        if (_save is null || !ConfirmDiscard())
        {
            return;
        }

        int selected = _party.SelectedIndex;
        LoadFile(_save.Path, quiet: false);
        if (selected >= 0)
        {
            _party.SelectedIndex = selected;
        }
    }

    private bool ConfirmDiscard()
    {
        if (_save is null || !_dirty)
        {
            return true;
        }

        return MessageBox.Show(this, "There are unsaved changes. Discard them?", AppTitle,
            MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) == DialogResult.Yes;
    }

    private void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        if (!ConfirmDiscard())
        {
            e.Cancel = true;
        }
    }

    private void Warn(string message) =>
        MessageBox.Show(this, message, AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning);

    private void ShowAbout() =>
        MessageBox.Show(this,
            AppTitle + "\n\n" +
            "Edits Breath of Fire III (USA) inside DuckStation save states (*.sav).\n" +
            "Party records are read from RAM at 0x80144968, the inventory from 0x80144F50.\n\n" +
            "Every save first copies the old file to <name>.sav.editor.bak.\n" +
            "After saving, load the state in DuckStation to see the changes.",
            "About", MessageBoxButtons.OK, MessageBoxIcon.Information);
}

/// <summary>The large portrait on the Status window.</summary>
internal sealed class PortraitView : Control
{
    private string _className = string.Empty;

    public PortraitView()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
        BackColor = Bof3Theme.PanelFill;
    }

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public string ClassName
    {
        get => _className;
        set
        {
            _className = value;
            Invalidate();
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(BackColor);
        if (_className.Length > 0)
        {
            Portraits.Draw(e.Graphics, _className, Rectangle.Inflate(ClientRectangle, -3, -3));
        }
    }
}

/// <summary>Dark colours for the menu strip, which does not follow BackColor on its own.</summary>
internal sealed class Bof3MenuColours : ProfessionalColorTable
{
    public override Color MenuItemSelected => Bof3Theme.SelectFill;
    public override Color MenuItemSelectedGradientBegin => Bof3Theme.SelectFill;
    public override Color MenuItemSelectedGradientEnd => Bof3Theme.SelectFill;
    public override Color MenuItemBorder => Bof3Theme.Khaki;
    public override Color MenuBorder => Bof3Theme.Khaki;
    public override Color ToolStripDropDownBackground => Bof3Theme.InsetFill;
    public override Color ImageMarginGradientBegin => Bof3Theme.InsetFill;
    public override Color ImageMarginGradientMiddle => Bof3Theme.InsetFill;
    public override Color ImageMarginGradientEnd => Bof3Theme.InsetFill;
    public override Color MenuItemPressedGradientBegin => Bof3Theme.PanelFill;
    public override Color MenuItemPressedGradientEnd => Bof3Theme.PanelFill;
    public override Color SeparatorDark => Bof3Theme.KhakiShadow;
    public override Color SeparatorLight => Bof3Theme.InsetFill;
    public override Color MenuStripGradientBegin => Bof3Theme.InsetFill;
    public override Color MenuStripGradientEnd => Bof3Theme.InsetFill;
}
