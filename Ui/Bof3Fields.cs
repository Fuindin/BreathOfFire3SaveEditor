using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

namespace BreathOfFire3SaveEditor.Ui;

/// <summary>
/// A gradient caption on the left ("HP"), one or two numeric boxes, and an
/// optional note after them (for example the total a base stat produces).
/// Values are clamped to [Minimum, Maximum] once the user leaves a box.
/// </summary>
public sealed class Bof3NumberField : Control
{
    private readonly TextBox _first = NewBox();
    private readonly TextBox _second = NewBox();
    private bool _suppress;
    private long _value;
    private long _secondValue;
    private string _note = string.Empty;
    private Color _noteColour = Bof3Theme.TextDim;

    public Bof3NumberField()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
        BackColor = Bof3Theme.PanelFill;
        Font = Bof3Theme.Text(14f);
        Height = 34;

        Wire(_first, second: false);
        Wire(_second, second: true);
        Controls.Add(_first);
        Controls.Add(_second);
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string Caption { get; set; } = string.Empty;

    /// <summary>Pixels reserved for the caption.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int CaptionWidth { get; set; } = 70;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int BoxWidth { get; set; } = 70;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool Paired { get; set; }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public long Minimum { get; set; }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public long Maximum { get; set; } = 999;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public long SecondMinimum { get; set; }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string Note
    {
        get => _note;
        set
        {
            _note = value ?? string.Empty;
            Invalidate();
        }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color NoteColour
    {
        get => _noteColour;
        set
        {
            _noteColour = value;
            Invalidate();
        }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public long Value
    {
        get => _value;
        set
        {
            _value = Math.Clamp(value, Minimum, Maximum);
            Show(_first, _value);
        }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public long SecondValue
    {
        get => _secondValue;
        set
        {
            _secondValue = Math.Clamp(value, SecondMinimum, Maximum);
            Show(_second, _secondValue);
        }
    }

    /// <summary>Raised when the user changes a box, never when code assigns a value.</summary>
    public event EventHandler? UserEdited;

    private static TextBox NewBox() => new()
    {
        BorderStyle = BorderStyle.None,
        TextAlign = HorizontalAlignment.Right,
        BackColor = Bof3Theme.InsetFill,
        ForeColor = Bof3Theme.TextWhite,
        Font = Bof3Theme.Text(14f),
    };

    private void Wire(TextBox box, bool second)
    {
        box.KeyPress += (_, e) =>
        {
            if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar))
            {
                e.Handled = true;
            }
        };

        box.TextChanged += (_, _) =>
        {
            if (_suppress || !long.TryParse(box.Text, out long parsed))
            {
                return;
            }

            if (second)
            {
                _secondValue = Math.Clamp(parsed, SecondMinimum, Maximum);
            }
            else
            {
                _value = Math.Clamp(parsed, Minimum, Maximum);
            }

            UserEdited?.Invoke(this, EventArgs.Empty);
        };

        // Select the whole number when the box is entered, so typing replaces
        // it. Deferred so the click that focused the box does not undo it.
        box.Enter += (_, _) => box.BeginInvoke(new Action(box.SelectAll));

        // Rewrite the text only once the user is done, so clamping never fights
        // someone part way through typing a longer number.
        box.Leave += (_, _) => Show(box, second ? _secondValue : _value);
        box.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                Show(box, second ? _secondValue : _value);
                e.SuppressKeyPress = true;
            }
        };
    }

    private void Show(TextBox box, long value)
    {
        _suppress = true;
        box.Text = value.ToString();
        box.SelectionStart = box.TextLength;
        _suppress = false;
    }

    /// <summary>Normalises whatever is still being typed, before saving.</summary>
    public void CommitPending()
    {
        Show(_first, _value);
        if (Paired)
        {
            Show(_second, _secondValue);
        }
    }

    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        _second.Visible = Paired;
        foreach (var (box, rect) in Boxes())
        {
            box.Bounds = new Rectangle(rect.X + 6, rect.Y + (rect.Height - box.PreferredHeight) / 2,
                rect.Width - 12, box.PreferredHeight);
        }
    }

    private const int SlashWidth = 18;

    private IEnumerable<(TextBox Box, Rectangle Rect)> Boxes()
    {
        int h = Math.Min(Height - 4, 30);
        int y = (Height - h) / 2;
        int x = CaptionWidth;
        yield return (_first, new Rectangle(x, y, BoxWidth, h));
        if (Paired)
        {
            yield return (_second, new Rectangle(x + BoxWidth + SlashWidth, y, BoxWidth, h));
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(BackColor);

        Bof3Theme.DrawLabel(g, Caption, new Rectangle(0, 0, CaptionWidth - 6, Height), 17f);

        var rects = Boxes().Select(b => b.Rect).ToList();
        foreach (var r in rects)
        {
            Bof3Theme.PaintInset(g, r);
        }

        if (Paired && rects.Count == 2)
        {
            var slash = new Rectangle(rects[0].Right, 0, SlashWidth, Height);
            Bof3Theme.DrawText(g, "/", Font, slash, Bof3Theme.TextSilver,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        if (_note.Length > 0)
        {
            int left = rects[^1].Right + 10;
            using var noteFont = Bof3Theme.Text(12f);
            Bof3Theme.DrawText(g, _note, noteFont, new Rectangle(left, 0, Math.Max(0, Width - left), Height), _noteColour);
        }
    }
}

/// <summary>A caption and a free text box, for the character name.</summary>
public sealed class Bof3TextField : Control
{
    private readonly TextBox _box = new()
    {
        BorderStyle = BorderStyle.None,
        BackColor = Bof3Theme.InsetFill,
        ForeColor = Bof3Theme.TextWhite,
        Font = Bof3Theme.Text(15f),
    };

    private bool _suppress;

    public Bof3TextField()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
        BackColor = Bof3Theme.PanelFill;
        Height = 36;
        _box.TextChanged += (_, _) =>
        {
            if (!_suppress)
            {
                UserEdited?.Invoke(this, EventArgs.Empty);
            }
        };
        _box.KeyPress += (_, e) =>
        {
            if (!char.IsControl(e.KeyChar) && !(KeyFilter?.Invoke(e.KeyChar) ?? true))
            {
                e.Handled = true;
            }
        };
        Controls.Add(_box);
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string Caption { get; set; } = string.Empty;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int CaptionWidth { get; set; } = 70;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int BoxWidth { get; set; } = 150;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int MaxLength
    {
        get => _box.MaxLength;
        set => _box.MaxLength = value;
    }

    /// <summary>Return false to reject a typed character.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Func<char, bool>? KeyFilter { get; set; }

    [AllowNull]
    public override string Text
    {
        get => _box.Text;
        set
        {
            _suppress = true;
            _box.Text = value ?? string.Empty;
            _suppress = false;
        }
    }

    public event EventHandler? UserEdited;

    private Rectangle BoxRect => new(CaptionWidth, (Height - 30) / 2, BoxWidth, 30);

    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        var r = BoxRect;
        _box.Bounds = new Rectangle(r.X + 8, r.Y + (r.Height - _box.PreferredHeight) / 2, r.Width - 16, _box.PreferredHeight);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(BackColor);
        Bof3Theme.DrawLabel(e.Graphics, Caption, new Rectangle(0, 0, CaptionWidth - 6, Height), 17f);
        Bof3Theme.PaintInset(e.Graphics, BoxRect);
    }
}

/// <summary>An entry for <see cref="Bof3Combo"/>: an id and the text shown for it.</summary>
public sealed record ComboEntry(int Id, string Text, string Detail = "")
{
    public override string ToString() => Text;
}

/// <summary>A drop down list painted in the menu colours.</summary>
public sealed class Bof3Combo : ComboBox
{
    public Bof3Combo()
    {
        DrawMode = DrawMode.OwnerDrawFixed;
        DropDownStyle = ComboBoxStyle.DropDownList;
        FlatStyle = FlatStyle.Flat;
        BackColor = Bof3Theme.InsetFill;
        ForeColor = Bof3Theme.TextWhite;
        Font = Bof3Theme.Text(13f);
        ItemHeight = 24;
        MaxDropDownItems = 18;
        IntegralHeight = false;
    }

    public void SetEntries(IEnumerable<ComboEntry> entries, int selectedId)
    {
        BeginUpdate();
        Items.Clear();
        foreach (var entry in entries)
        {
            Items.Add(entry);
        }

        SelectId(selectedId);
        EndUpdate();
    }

    public void SelectId(int id)
    {
        for (int i = 0; i < Items.Count; i++)
        {
            if (Items[i] is ComboEntry entry && entry.Id == id)
            {
                SelectedIndex = i;
                return;
            }
        }

        SelectedIndex = Items.Count > 0 ? 0 : -1;
    }

    public int SelectedId => SelectedItem is ComboEntry entry ? entry.Id : 0;

    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= Items.Count)
        {
            return;
        }

        bool selected = (e.State & DrawItemState.Selected) != 0 && (e.State & DrawItemState.ComboBoxEdit) == 0;
        using (var b = new SolidBrush(selected ? Bof3Theme.SelectFill : Bof3Theme.InsetFill))
            e.Graphics.FillRectangle(b, e.Bounds);

        var entry = Items[e.Index] as ComboEntry;
        string text = entry?.Text ?? Items[e.Index]?.ToString() ?? string.Empty;
        var r = Rectangle.Inflate(e.Bounds, -6, 0);
        TextRenderer.DrawText(e.Graphics, text, Font, r, Bof3Theme.TextWhite,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);

        if (entry is { Detail.Length: > 0 })
        {
            TextRenderer.DrawText(e.Graphics, entry.Detail, Font, r, Bof3Theme.TextDim,
                TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        }
    }
}

/// <summary>A caption, a <see cref="Bof3Combo"/> and a note to the right of it.</summary>
public sealed class Bof3ComboField : Control
{
    private string _note = string.Empty;

    public Bof3ComboField()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
        BackColor = Bof3Theme.PanelFill;
        Height = 36;
        Controls.Add(Combo);
    }

    public Bof3Combo Combo { get; } = new();

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string Caption { get; set; } = string.Empty;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int CaptionWidth { get; set; } = 90;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int ComboWidth { get; set; } = 200;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string Note
    {
        get => _note;
        set
        {
            _note = value ?? string.Empty;
            Invalidate();
        }
    }

    private static readonly Font CaptionFont = Bof3Theme.Text(13f);
    private static readonly Font NoteFont = Bof3Theme.Text(12f);

    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        Combo.Bounds = new Rectangle(CaptionWidth, (Height - Combo.Height) / 2, ComboWidth, Combo.Height);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(BackColor);
        Bof3Theme.DrawText(g, Caption, CaptionFont, new Rectangle(0, 0, CaptionWidth - 6, Height), Bof3Theme.TextDim);
        int left = CaptionWidth + ComboWidth + 10;
        Bof3Theme.DrawText(g, _note, NoteFont, new Rectangle(left, 0, Math.Max(0, Width - left), Height), Bof3Theme.TextSilver);
    }
}
