using System.ComponentModel;

namespace BreathOfFire3SaveEditor.Ui;

/// <summary>A plain wall of planks, used behind pages.</summary>
public class WoodPanel : Panel
{
    public WoodPanel()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
        BackColor = Bof3Theme.Wood;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Bof3Theme.PaintWood(this, e.Graphics, ClientRectangle);
        base.OnPaint(e);
    }

    protected override void OnLocationChanged(EventArgs e)
    {
        base.OnLocationChanged(e);
        Invalidate();
    }
}

/// <summary>
/// One of the game's menu windows: planks showing round the clipped corners, a
/// khaki band, and a charcoal panel. An optional title sits in a dark name plate
/// along the top, like "Items" or a character's name.
/// </summary>
public class Bof3Frame : Panel
{
    public const int TitleHeight = 34;

    private string _title = string.Empty;

    public Bof3Frame()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
        BackColor = Bof3Theme.PanelFill;
        ForeColor = Bof3Theme.TextSilver;
        UpdatePadding();
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string Title
    {
        get => _title;
        set
        {
            _title = value ?? string.Empty;
            UpdatePadding();
            Invalidate();
        }
    }

    /// <summary>Extra space between the frame band and the content.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int InnerMargin { get; set; } = 10;

    private void UpdatePadding()
    {
        int m = Bof3Theme.FrameBand + 8;
        Padding = new Padding(m, m + (string.IsNullOrEmpty(_title) ? 0 : TitleHeight + 4), m, m);
    }

    private static readonly Font TitleFont = Bof3Theme.Text(15f);

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        Bof3Theme.PaintWood(this, g, ClientRectangle);
        Bof3Theme.PaintFrame(g, ClientRectangle);

        if (!string.IsNullOrEmpty(_title))
        {
            var plate = new Rectangle(Bof3Theme.FrameBand + 6, Bof3Theme.FrameBand + 6,
                Width - 2 * (Bof3Theme.FrameBand + 6), TitleHeight);
            Bof3Theme.PaintInset(g, plate);
            var text = Rectangle.Inflate(plate, -10, 0);
            Bof3Theme.DrawText(g, _title, TitleFont, text, Bof3Theme.TextSilver);
        }

        base.OnPaint(e);
    }

    protected override void OnLocationChanged(EventArgs e)
    {
        base.OnLocationChanged(e);
        Invalidate();
    }
}

/// <summary>
/// A block of read-only text lines inside a frame. Each line is a caption on the
/// left and a value on the right; a line with no caption is a heading.
/// </summary>
public sealed class Bof3InfoList : Control
{
    private IReadOnlyList<(string Caption, string Value, Color? Colour)> _lines = [];

    public Bof3InfoList()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
        BackColor = Bof3Theme.PanelFill;
        Font = Bof3Theme.Text(12.5f);
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int LineHeight { get; set; } = 24;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Columns { get; set; } = 1;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IReadOnlyList<(string Caption, string Value, Color? Colour)> Lines
    {
        get => _lines;
        set
        {
            _lines = value ?? [];
            Invalidate();
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Bof3Theme.PanelFill);

        int columns = Math.Max(1, Columns);
        int perColumn = (int)Math.Ceiling(_lines.Count / (double)columns);
        int colWidth = Width / columns;

        for (int i = 0; i < _lines.Count; i++)
        {
            int col = perColumn == 0 ? 0 : i / perColumn;
            int row = perColumn == 0 ? 0 : i % perColumn;
            var r = new Rectangle(col * colWidth + 4, row * LineHeight, colWidth - 12, LineHeight);
            var (caption, value, colour) = _lines[i];

            if (string.IsNullOrEmpty(caption))
            {
                Bof3Theme.DrawLabel(g, value, r, 15f);
                continue;
            }

            // A line with only a caption is a note: it takes the line's colour.
            Color captionColour = value.Length == 0 && colour is { } c ? c : Bof3Theme.TextDim;
            Bof3Theme.DrawText(g, caption, Font, r, captionColour);
            Bof3Theme.DrawText(g, value, Font, r, colour ?? Bof3Theme.TextSilver,
                TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
        }
    }
}

/// <summary>A flat button drawn as a small khaki-framed plate.</summary>
public sealed class Bof3Button : Control
{
    private bool _hot;
    private bool _down;

    public Bof3Button()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.Selectable, true);
        BackColor = Bof3Theme.PanelFill;
        Font = Bof3Theme.Text(12.5f);
        Size = new Size(110, 32);
        Cursor = Cursors.Hand;
    }

    protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); _hot = true; Invalidate(); }
    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _hot = false; _down = false; Invalidate(); }
    protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); _down = true; Invalidate(); }
    protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); _down = false; Invalidate(); }
    protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); Invalidate(); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(BackColor);
        var r = new Rectangle(0, 0, Width - 1, Height - 1);

        Color fill = !Enabled ? Bof3Theme.InsetFill : _down ? Bof3Theme.TabGreen : _hot ? Bof3Theme.SelectFill : Bof3Theme.InsetFill;
        using (var b = new SolidBrush(fill))
            g.FillRectangle(b, r);
        using (var pen = new Pen(Enabled ? Bof3Theme.Khaki : Bof3Theme.KhakiShadow, 2))
            g.DrawRectangle(pen, Rectangle.Inflate(r, -1, -1));

        Bof3Theme.DrawText(g, Text, Font, r, Enabled ? Bof3Theme.TextSilver : Bof3Theme.TextDim,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }
}
