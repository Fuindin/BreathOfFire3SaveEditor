using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace BreathOfFire3SaveEditor.Ui;

/// <summary>
/// Character portraits. Pictures are loaded from a "portraits" folder next to the
/// exe, named after the character ("Ryu.png", "Nina.png", ...), so anyone can
/// drop in their own. Without one, a coloured crest with the initial is drawn.
/// </summary>
public static class Portraits
{
    private static readonly Dictionary<string, Image?> Cache = new(StringComparer.OrdinalIgnoreCase);

    private static readonly Dictionary<string, Color> Colours = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Ryu"] = Color.FromArgb(0x3A, 0xA5, 0xC8),
        ["Nina"] = Color.FromArgb(0xD8, 0x5E, 0x8E),
        ["Garr"] = Color.FromArgb(0x9A, 0x8A, 0x3C),
        ["Teepo"] = Color.FromArgb(0x8C, 0x6E, 0xD8),
        ["Rei"] = Color.FromArgb(0xD8, 0xB4, 0x2A),
        ["Momo"] = Color.FromArgb(0xC8, 0x7A, 0x3A),
        ["Peco"] = Color.FromArgb(0x5E, 0xB0, 0x52),
        ["Whelp"] = Color.FromArgb(0x5A, 0x8E, 0xD0),
    };

    public static Image? For(string className)
    {
        if (Cache.TryGetValue(className, out var cached))
        {
            return cached;
        }

        Image? image = null;
        try
        {
            string path = Path.Combine(AppContext.BaseDirectory, "portraits", className + ".png");
            if (File.Exists(path))
            {
                // Copy into memory so the file is not kept locked.
                using var stream = new MemoryStream(File.ReadAllBytes(path));
                using var loaded = Image.FromStream(stream);
                image = new Bitmap(loaded);
            }
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or UnauthorizedAccessException)
        {
            image = null;
        }

        Cache[className] = image;
        return image;
    }

    /// <summary>Draws a portrait (or the crest) filling the rectangle, pixel-sharp.</summary>
    public static void Draw(Graphics g, string className, Rectangle r)
    {
        Bof3Theme.PaintInset(g, Rectangle.Inflate(r, 2, 2), Bof3Theme.Outline);
        var image = For(className);
        if (image is not null)
        {
            var oldMode = g.InterpolationMode;
            var oldOffset = g.PixelOffsetMode;
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.Half;

            // Fit by height, crop the sides if needed, keep the aspect ratio.
            float scale = Math.Max(r.Width / (float)image.Width, r.Height / (float)image.Height);
            int srcW = (int)(r.Width / scale);
            int srcH = (int)(r.Height / scale);
            var src = new Rectangle((image.Width - srcW) / 2, (image.Height - srcH) / 2, srcW, srcH);
            g.DrawImage(image, r, src, GraphicsUnit.Pixel);

            g.InterpolationMode = oldMode;
            g.PixelOffsetMode = oldOffset;
            return;
        }

        var colour = Colours.TryGetValue(className, out var c) ? c : Bof3Theme.Khaki;
        using (var brush = new LinearGradientBrush(r, ControlPaint.Light(colour), ControlPaint.Dark(colour), 60f))
            g.FillRectangle(brush, r);

        g.SmoothingMode = SmoothingMode.AntiAlias;
        int d = Math.Min(r.Width, r.Height) - 14;
        var circle = new Rectangle(r.X + (r.Width - d) / 2, r.Y + (r.Height - d) / 2, d, d);
        using (var ring = new Pen(Color.FromArgb(150, Color.White), 2))
            g.DrawEllipse(ring, circle);

        string initial = className.Length > 0 ? className[..1] : "?";
        Bof3Theme.DrawLabel(g, initial, r, Math.Max(12f, d * 0.6f), StringAlignment.Center);
    }
}

/// <summary>What the party list needs to draw one card.</summary>
public sealed record PartyCard(string Name, string ClassName, int Level, int Hp, int MaxHp, int Ap, int MaxAp);

/// <summary>
/// The column of character cards from the game's menu: number tag, name plate,
/// LV/HP/AP and the portrait. Click a card to select it.
/// </summary>
public sealed class Bof3PartyList : Control
{
    private IReadOnlyList<PartyCard> _cards = [];
    private int _selected = -1;
    private int _hot = -1;

    public Bof3PartyList()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.Selectable, true);
        BackColor = Bof3Theme.PanelFill;
        Font = Bof3Theme.Text(15f);
        Cursor = Cursors.Hand;
    }

    public const int CardHeight = 76;
    private const int Gap = 6;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IReadOnlyList<PartyCard> Cards
    {
        get => _cards;
        set
        {
            _cards = value ?? [];
            if (_selected >= _cards.Count)
            {
                _selected = _cards.Count - 1;
            }

            Invalidate();
        }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int SelectedIndex
    {
        get => _selected;
        set
        {
            int v = _cards.Count == 0 ? -1 : Math.Clamp(value, 0, _cards.Count - 1);
            if (v == _selected)
            {
                return;
            }

            _selected = v;
            Invalidate();
            SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public event EventHandler? SelectedIndexChanged;

    private Rectangle CardBounds(int i) => new(0, i * (CardHeight + Gap), Width, CardHeight);

    private int HitTest(Point p)
    {
        for (int i = 0; i < _cards.Count; i++)
        {
            if (CardBounds(i).Contains(p))
            {
                return i;
            }
        }

        return -1;
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        Focus();
        int hit = HitTest(e.Location);
        if (hit >= 0)
        {
            SelectedIndex = hit;
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        int hit = HitTest(e.Location);
        if (hit != _hot)
        {
            _hot = hit;
            Invalidate();
        }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hot = -1;
        Invalidate();
    }

    protected override bool IsInputKey(Keys keyData) => keyData is Keys.Up or Keys.Down || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode == Keys.Up)
        {
            SelectedIndex = Math.Max(0, _selected - 1);
        }
        else if (e.KeyCode == Keys.Down)
        {
            SelectedIndex = Math.Min(_cards.Count - 1, _selected + 1);
        }
    }

    private static readonly Font NameFont = Bof3Theme.Text(17f);
    private static readonly Font NumberFont = Bof3Theme.Text(13f);
    private static readonly Font TagFont = Bof3Theme.Label(10f);

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(BackColor);

        for (int i = 0; i < _cards.Count; i++)
        {
            var card = _cards[i];
            var r = CardBounds(i);
            bool selected = i == _selected;

            // Card body: a sub-frame, lit up when selected.
            using (var fill = new SolidBrush(selected ? Bof3Theme.SelectFill : i == _hot ? Color.FromArgb(0x42, 0x42, 0x3E) : Bof3Theme.PanelFill))
                g.FillRectangle(fill, r);
            using (var pen = new Pen(selected ? Bof3Theme.KhakiLight : Bof3Theme.KhakiShadow, selected ? 3 : 2))
                g.DrawRectangle(pen, new Rectangle(r.X + 1, r.Y + 1, r.Width - 3, r.Height - 3));

            int portraitW = 56;
            var portrait = new Rectangle(r.Right - portraitW - 8, r.Y + 7, portraitW, r.Height - 14);
            Portraits.Draw(g, card.ClassName, portrait);

            // Number tag.
            var tag = new Rectangle(r.X + 8, r.Y + 8, 22, 24);
            Bof3Theme.PaintInset(g, tag, Bof3Theme.Outline);
            Bof3Theme.DrawText(g, (i + 1).ToString(), TagFont, tag, Bof3Theme.TagCyan,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

            // Name plate.
            var plate = new Rectangle(tag.Right + 4, r.Y + 6, portrait.X - tag.Right - 14, 28);
            Bof3Theme.PaintInset(g, plate);
            string name = card.Name == card.ClassName || card.Name.Length == 0
                ? card.ClassName
                : $"{card.Name}";
            Bof3Theme.DrawText(g, name, NameFont, Rectangle.Inflate(plate, -6, 0), Bof3Theme.TextSilver);
            if (!string.Equals(card.Name, card.ClassName, StringComparison.Ordinal))
            {
                Bof3Theme.DrawText(g, card.ClassName, NumberFont, Rectangle.Inflate(plate, -6, 0), Bof3Theme.TextDim,
                    TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
            }

            // LV / HP / AP line.
            int y = plate.Bottom + 6;
            int x = r.X + 10;
            Bof3Theme.DrawLabel(g, "LV", new Rectangle(x, y, 30, 22), 14f);
            Bof3Theme.DrawText(g, card.Level.ToString(), NumberFont, new Rectangle(x + 30, y, 30, 22), Bof3Theme.TextWhite);

            int barLeft = x + 66;
            int barWidth = Math.Max(30, (portrait.X - 10 - barLeft) / 2 - 6);
            DrawBar(g, "HP", card.Hp, card.MaxHp, new Rectangle(barLeft, y, barWidth, 22), Bof3Theme.HpBar);
            DrawBar(g, "AP", card.Ap, card.MaxAp, new Rectangle(barLeft + barWidth + 8, y, barWidth, 22), Bof3Theme.ApBar);
        }
    }

    private static void DrawBar(Graphics g, string label, int value, int max, Rectangle r, Color colour)
    {
        Bof3Theme.DrawLabel(g, label, new Rectangle(r.X, r.Y, 30, r.Height), 14f);
        var track = new Rectangle(r.X + 30, r.Y + r.Height / 2 + 3, r.Width - 32, 5);
        Bof3Theme.PaintInset(g, Rectangle.Inflate(track, 1, 1), Bof3Theme.Outline);
        if (max > 0 && value > 0)
        {
            int w = (int)Math.Round(track.Width * Math.Min(1.0, value / (double)max));
            using var b = new SolidBrush(colour);
            g.FillRectangle(b, track.X, track.Y, w, track.Height);
        }

        using var font = Bof3Theme.Text(11f);
        Bof3Theme.DrawText(g, value.ToString(), font, new Rectangle(r.X + 30, r.Y - 4, r.Width - 32, 16),
            Bof3Theme.TextSilver, TextFormatFlags.Right | TextFormatFlags.Top);
    }
}

/// <summary>
/// The row of tab plates from the menus. The selected tab is lit green like the
/// game's highlighted icon; pages are swapped by the owner.
/// </summary>
public sealed class Bof3TabStrip : Control
{
    private string[] _tabs = [];
    private int _selected;
    private int _hot = -1;

    public Bof3TabStrip()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
        Font = Bof3Theme.Text(15f);
        Height = 44;
        Cursor = Cursors.Hand;
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int TabWidth { get; set; } = 150;

    /// <summary>False to fill with BackColor instead of planks, for tabs inside a frame.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool PaintBackdrop { get; set; } = true;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string[] Tabs
    {
        get => _tabs;
        set
        {
            _tabs = value ?? [];
            _selected = Math.Clamp(_selected, 0, Math.Max(0, _tabs.Length - 1));
            Invalidate();
        }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int SelectedIndex
    {
        get => _selected;
        set
        {
            int v = _tabs.Length == 0 ? 0 : Math.Clamp(value, 0, _tabs.Length - 1);
            if (v == _selected)
            {
                return;
            }

            _selected = v;
            Invalidate();
            SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public event EventHandler? SelectedIndexChanged;

    private Rectangle TabBounds(int i) => new(i * (TabWidth + 8), 2, TabWidth, Height - 4);

    private int HitTest(Point p)
    {
        for (int i = 0; i < _tabs.Length; i++)
        {
            if (TabBounds(i).Contains(p))
            {
                return i;
            }
        }

        return -1;
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        int hit = HitTest(e.Location);
        if (hit >= 0)
        {
            SelectedIndex = hit;
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        int hit = HitTest(e.Location);
        if (hit != _hot)
        {
            _hot = hit;
            Invalidate();
        }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hot = -1;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        if (PaintBackdrop)
        {
            Bof3Theme.PaintWood(this, g, ClientRectangle);
        }
        else
        {
            g.Clear(BackColor);
        }

        for (int i = 0; i < _tabs.Length; i++)
        {
            var r = TabBounds(i);
            bool selected = i == _selected;

            using (var path = Bof3Theme.Chamfer(new Rectangle(r.X, r.Y, r.Width - 1, r.Height - 1), 4))
            {
                using var band = new SolidBrush(selected ? Bof3Theme.KhakiLight : Bof3Theme.Khaki);
                g.FillPath(band, path);
                using var line = new Pen(Bof3Theme.Outline, 1);
                g.DrawPath(line, path);
            }

            var inner = Rectangle.Inflate(r, -5, -5);
            Color fill = selected ? Bof3Theme.TabGreen : i == _hot ? Bof3Theme.SelectFill : Bof3Theme.InsetFill;
            Bof3Theme.PaintInset(g, inner, fill);
            Bof3Theme.DrawText(g, _tabs[i], Font, inner, selected ? Bof3Theme.TextWhite : Bof3Theme.TextSilver,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
    }
}
