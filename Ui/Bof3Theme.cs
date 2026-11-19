using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;

namespace BreathOfFire3SaveEditor.Ui;

/// <summary>
/// Colours and painting helpers taken from Breath of Fire III's menus: wooden
/// planks behind everything, khaki bevelled frames, charcoal panels set into
/// them, silver text, and the orange-on-red "EXP / LV / HP / AP" captions.
/// </summary>
public static class Bof3Theme
{
    // Wood behind the menus.
    public static readonly Color WoodDeep  = Color.FromArgb(0x2E, 0x1C, 0x10);
    public static readonly Color Wood      = Color.FromArgb(0x4A, 0x31, 0x19);
    public static readonly Color WoodLight = Color.FromArgb(0x5E, 0x40, 0x24);
    public static readonly Color WoodGap   = Color.FromArgb(0x1E, 0x12, 0x0C);

    // Frames.
    public static readonly Color Outline     = Color.FromArgb(0x21, 0x19, 0x21);
    public static readonly Color Khaki       = Color.FromArgb(0x8C, 0x8C, 0x63);
    public static readonly Color KhakiLight  = Color.FromArgb(0xB5, 0xB5, 0x8C);
    public static readonly Color KhakiShadow = Color.FromArgb(0x5A, 0x5A, 0x3A);
    public static readonly Color FrameInner  = Color.FromArgb(0x4A, 0x3A, 0x31);

    // Panels inside frames.
    public static readonly Color PanelFill   = Color.FromArgb(0x37, 0x37, 0x37);
    public static readonly Color InsetFill   = Color.FromArgb(0x24, 0x24, 0x24);
    public static readonly Color InsetEdge   = Color.FromArgb(0x16, 0x14, 0x16);
    public static readonly Color InsetLip    = Color.FromArgb(0x5E, 0x5E, 0x52);
    public static readonly Color SelectFill  = Color.FromArgb(0x4E, 0x57, 0x47);

    // Text.
    public static readonly Color TextSilver = Color.FromArgb(0xDE, 0xDE, 0xDE);
    public static readonly Color TextWhite  = Color.FromArgb(0xFF, 0xFF, 0xFF);
    public static readonly Color TextDim    = Color.FromArgb(0xA6, 0xA6, 0x96);
    public static readonly Color TextShadow = Color.FromArgb(0x10, 0x10, 0x10);
    public static readonly Color TextGood   = Color.FromArgb(0x8C, 0xE6, 0x7B);
    public static readonly Color TextBad    = Color.FromArgb(0xFF, 0x84, 0x7B);

    // Accents.
    public static readonly Color LabelTop    = Color.FromArgb(0xFF, 0xEF, 0x8C);
    public static readonly Color LabelMid    = Color.FromArgb(0xFF, 0xA5, 0x31);
    public static readonly Color LabelBottom = Color.FromArgb(0xF7, 0x5A, 0x8C);
    public static readonly Color TagCyan     = Color.FromArgb(0x21, 0xB5, 0xE6);
    public static readonly Color TabGreen    = Color.FromArgb(0x6B, 0xA4, 0x73);
    public static readonly Color HpBar       = Color.FromArgb(0x4E, 0xC2, 0x5A);
    public static readonly Color ApBar       = Color.FromArgb(0x4A, 0x9C, 0xE6);
    public static readonly Color ExpBar      = Color.FromArgb(0xC8, 0x2E, 0x2E);

    public const int FrameBand = 7;

    // ----------------------------------------------------------------- fonts

    private static readonly string[] TextFamilies =
        ["Bahnschrift SemiCondensed", "Bahnschrift", "Arial Narrow", "Segoe UI", "Tahoma"];

    private static readonly string[] LabelFamilies = ["Arial Black", "Segoe UI Black", "Impact", "Tahoma"];

    private static string? _textFamily;
    private static string? _labelFamily;

    private static string Pick(string[] preference)
    {
        using var installed = new InstalledFontCollection();
        var names = installed.Families.Select(f => f.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return preference.FirstOrDefault(names.Contains) ?? FontFamily.GenericSansSerif.Name;
    }

    public static string TextFamily => _textFamily ??= Pick(TextFamilies);
    public static string LabelFamily => _labelFamily ??= Pick(LabelFamilies);

    /// <summary>Body text: names, numbers, item lists.</summary>
    public static Font Text(float size, FontStyle style = FontStyle.Regular) => new(TextFamily, size, style);

    /// <summary>The heavy face used for the gradient captions.</summary>
    public static Font Label(float size) => new(LabelFamily, size, FontStyle.Regular);

    // ------------------------------------------------------------------ wood

    private static Bitmap? _wood;

    /// <summary>A tile of vertical planks, generated once.</summary>
    public static Bitmap WoodTile => _wood ??= MakeWood();

    private static Bitmap MakeWood()
    {
        const int plank = 74;
        const int planks = 6;
        const int height = 512;
        var bmp = new Bitmap(plank * planks, height, PixelFormat.Format32bppArgb);
        var rng = new Random(1998);

        using var g = Graphics.FromImage(bmp);
        g.Clear(Wood);

        for (int p = 0; p < planks; p++)
        {
            int x0 = p * plank;
            int shade = rng.Next(-10, 11);
            var baseColour = Shift(Wood, shade);
            using (var b = new SolidBrush(baseColour))
                g.FillRectangle(b, x0, 0, plank, height);

            // Grain: thin wavering streaks running down the plank.
            for (int s = 0; s < 22; s++)
            {
                int x = x0 + 3 + rng.Next(plank - 6);
                var c = rng.Next(3) == 0 ? Shift(WoodLight, rng.Next(-8, 8)) : Shift(WoodDeep, rng.Next(0, 18));
                using var pen = new Pen(Color.FromArgb(rng.Next(60, 150), c), rng.Next(1, 3));
                var pts = new List<Point>();
                int wobble = 0;
                for (int y = 0; y <= height; y += 16)
                {
                    wobble = Math.Clamp(wobble + rng.Next(-1, 2), -3, 3);
                    pts.Add(new Point(x + wobble, y));
                }

                g.DrawLines(pen, pts.ToArray());
            }

            // A knot or two.
            for (int k = 0; k < 2; k++)
            {
                if (rng.Next(3) != 0)
                {
                    continue;
                }

                int kx = x0 + 12 + rng.Next(plank - 24);
                int ky = rng.Next(height - 30);
                using var knot = new SolidBrush(Color.FromArgb(150, WoodDeep));
                g.FillEllipse(knot, kx, ky, 10, 22);
            }

            // Plank seams: dark gap, a lit edge on the left of the next plank.
            using (var gap = new Pen(WoodGap, 3))
                g.DrawLine(gap, x0 + plank - 2, 0, x0 + plank - 2, height);
            using (var lit = new Pen(Color.FromArgb(90, WoodLight), 1))
                g.DrawLine(lit, x0 + 1, 0, x0 + 1, height);

            // Short cross joints at staggered heights, with nail heads.
            int joint = rng.Next(60, height - 60);
            using (var jointPen = new Pen(WoodGap, 2))
                g.DrawLine(jointPen, x0, joint, x0 + plank - 3, joint);
            using var nail = new SolidBrush(Color.FromArgb(0x24, 0x1A, 0x14));
            g.FillRectangle(nail, x0 + 8, joint + 6, 3, 3);
            g.FillRectangle(nail, x0 + plank - 14, joint + 6, 3, 3);
        }

        return bmp;
    }

    private static Color Shift(Color c, int amount) => Color.FromArgb(
        Math.Clamp(c.R + amount, 0, 255), Math.Clamp(c.G + amount, 0, 255), Math.Clamp(c.B + amount, 0, 255));

    /// <summary>
    /// Fills a control with planks aligned to its form, so neighbouring
    /// controls line up into one continuous wall.
    /// </summary>
    public static void PaintWood(Control control, Graphics g, Rectangle area)
    {
        var form = control.FindForm();
        Point origin = form is null ? Point.Empty : form.PointToClient(control.PointToScreen(Point.Empty));

        using var brush = new TextureBrush(WoodTile, WrapMode.Tile);
        brush.TranslateTransform(-origin.X, -origin.Y);
        g.FillRectangle(brush, area);
    }

    // ---------------------------------------------------------------- frames

    /// <summary>
    /// The khaki bevelled frame with a charcoal panel inside. Corners are
    /// clipped at 45 degrees like the game's windows.
    /// </summary>
    public static void PaintFrame(Graphics g, Rectangle r)
    {
        if (r.Width < 20 || r.Height < 20)
        {
            return;
        }

        g.SmoothingMode = SmoothingMode.None;
        var outer = new Rectangle(r.X, r.Y, r.Width - 1, r.Height - 1);

        using (var path = Chamfer(outer, 5))
        {
            using var band = new LinearGradientBrush(outer, KhakiLight, Khaki, LinearGradientMode.ForwardDiagonal);
            g.FillPath(band, path);
            using var line = new Pen(Outline, 1);
            g.DrawPath(line, path);
        }

        // Light along the top and left of the band, shade on the bottom and right.
        using (var hi = new Pen(Color.FromArgb(180, KhakiLight), 1))
        {
            g.DrawLine(hi, outer.X + 5, outer.Y + 1, outer.Right - 5, outer.Y + 1);
            g.DrawLine(hi, outer.X + 1, outer.Y + 5, outer.X + 1, outer.Bottom - 5);
        }

        using (var lo = new Pen(KhakiShadow, 1))
        {
            g.DrawLine(lo, outer.X + 5, outer.Bottom - 1, outer.Right - 5, outer.Bottom - 1);
            g.DrawLine(lo, outer.Right - 1, outer.Y + 5, outer.Right - 1, outer.Bottom - 5);
        }

        // A little stipple so the band reads as brushed metal rather than flat.
        using (var speck = new SolidBrush(Color.FromArgb(40, KhakiShadow)))
        {
            for (int x = outer.X + 3; x < outer.Right - 2; x += 3)
            {
                g.FillRectangle(speck, x, outer.Y + 3 + (x % 2), 1, 1);
                g.FillRectangle(speck, x, outer.Bottom - 4 - (x % 2), 1, 1);
            }
        }

        var panel = Rectangle.Inflate(outer, -FrameBand, -FrameBand);
        if (panel.Width <= 0 || panel.Height <= 0)
        {
            return;
        }

        using (var fill = new SolidBrush(PanelFill))
            g.FillRectangle(fill, panel);

        using (var edge = new Pen(FrameInner, 1))
            g.DrawRectangle(edge, Rectangle.Inflate(panel, 1, 1));
        using (var deep = new Pen(Outline, 1))
        {
            g.DrawLine(deep, panel.X, panel.Y, panel.Right - 1, panel.Y);
            g.DrawLine(deep, panel.X, panel.Y, panel.X, panel.Bottom - 1);
        }
    }

    /// <summary>The darker recessed boxes that hold names and numbers.</summary>
    public static void PaintInset(Graphics g, Rectangle r, Color? fill = null)
    {
        if (r.Width <= 2 || r.Height <= 2)
        {
            return;
        }

        g.SmoothingMode = SmoothingMode.None;
        using (var b = new SolidBrush(fill ?? InsetFill))
            g.FillRectangle(b, r);

        using var dark = new Pen(InsetEdge, 1);
        using var lip = new Pen(InsetLip, 1);
        g.DrawLine(dark, r.X, r.Y, r.Right - 1, r.Y);
        g.DrawLine(dark, r.X, r.Y, r.X, r.Bottom - 1);
        g.DrawLine(lip, r.X + 1, r.Bottom - 1, r.Right - 1, r.Bottom - 1);
        g.DrawLine(lip, r.Right - 1, r.Y + 1, r.Right - 1, r.Bottom - 1);
    }

    public static GraphicsPath Chamfer(Rectangle r, int cut)
    {
        var path = new GraphicsPath();
        path.AddPolygon(new[]
        {
            new Point(r.X + cut, r.Y), new Point(r.Right - cut, r.Y), new Point(r.Right, r.Y + cut),
            new Point(r.Right, r.Bottom - cut), new Point(r.Right - cut, r.Bottom), new Point(r.X + cut, r.Bottom),
            new Point(r.X, r.Bottom - cut), new Point(r.X, r.Y + cut),
        });
        return path;
    }

    // ------------------------------------------------------------------ text

    /// <summary>Silver text with the one pixel drop shadow the game uses.</summary>
    public static void DrawText(Graphics g, string text, Font font, Rectangle bounds, Color colour,
        TextFormatFlags flags = TextFormatFlags.Left | TextFormatFlags.VerticalCenter)
    {
        flags |= TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding;
        var shadow = bounds;
        shadow.Offset(1, 1);
        TextRenderer.DrawText(g, text, font, shadow, TextShadow, flags);
        TextRenderer.DrawText(g, text, font, bounds, colour, flags);
    }

    /// <summary>
    /// A caption in the game's style: yellow fading through orange to pink,
    /// with a dark outline.
    /// </summary>
    public static void DrawLabel(Graphics g, string text, Rectangle bounds, float emSize,
        StringAlignment align = StringAlignment.Near)
    {
        if (string.IsNullOrEmpty(text) || bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var family = new FontFamily(LabelFamily);
        using var format = new StringFormat
        {
            Alignment = align,
            LineAlignment = StringAlignment.Center,
            FormatFlags = StringFormatFlags.NoWrap,
        };

        using var path = new GraphicsPath();
        path.AddString(text, family, (int)FontStyle.Regular, emSize, bounds, format);

        var box = path.GetBounds();
        if (box.Height < 1)
        {
            return;
        }

        using (var outline = new Pen(Outline, 3.2f) { LineJoin = LineJoin.Round })
            g.DrawPath(outline, path);

        var gradientBox = RectangleF.Inflate(box, 0, 1);
        using var brush = new LinearGradientBrush(gradientBox, LabelTop, LabelBottom, LinearGradientMode.Vertical)
        {
            InterpolationColors = new ColorBlend
            {
                Colors = [LabelTop, LabelMid, LabelBottom],
                Positions = [0f, 0.5f, 1f],
            },
        };
        g.FillPath(brush, path);
    }
}
