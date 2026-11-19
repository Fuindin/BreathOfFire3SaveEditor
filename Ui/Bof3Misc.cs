using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace BreathOfFire3SaveEditor.Ui;

/// <summary>Shows the emulator's thumbnail of the state, scaled to fit.</summary>
public sealed class ScreenshotView : Control
{
    private Bitmap? _image;

    public ScreenshotView()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
        BackColor = Bof3Theme.PanelFill;
    }

    public void SetImage(byte[]? rgba, int width, int height)
    {
        _image?.Dispose();
        _image = rgba is null ? null : FromRgba(rgba, width, height);
        Invalidate();
    }

    private static Bitmap FromRgba(byte[] rgba, int width, int height)
    {
        // GDI+ wants BGRA; the state stores RGBA. Alpha is forced opaque.
        var bgra = new byte[width * height * 4];
        for (int i = 0; i < bgra.Length; i += 4)
        {
            bgra[i] = rgba[i + 2];
            bgra[i + 1] = rgba[i + 1];
            bgra[i + 2] = rgba[i];
            bgra[i + 3] = 0xFF;
        }

        var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        var data = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        try
        {
            for (int y = 0; y < height; y++)
            {
                Marshal.Copy(bgra, y * width * 4, data.Scan0 + y * data.Stride, width * 4);
            }
        }
        finally
        {
            bmp.UnlockBits(data);
        }

        return bmp;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(BackColor);
        var frame = new Rectangle(0, 0, Width, Height);
        Bof3Theme.PaintInset(g, frame, Bof3Theme.Outline);

        if (_image is null)
        {
            using var font = Bof3Theme.Text(11f);
            Bof3Theme.DrawText(g, "no picture", font, frame, Bof3Theme.TextDim,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            return;
        }

        var inner = Rectangle.Inflate(frame, -2, -2);
        float scale = Math.Min(inner.Width / (float)_image.Width, inner.Height / (float)_image.Height);
        int w = (int)(_image.Width * scale);
        int h = (int)(_image.Height * scale);
        var dest = new Rectangle(inner.X + (inner.Width - w) / 2, inner.Y + (inner.Height - h) / 2, w, h);
        g.InterpolationMode = InterpolationMode.HighQualityBilinear;
        g.DrawImage(_image, dest);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _image?.Dispose();
        }

        base.Dispose(disposing);
    }
}

/// <summary>
/// Stacks a frame's children top to bottom inside its padding. Each row keeps
/// its own height; a row whose height is 0 takes whatever is left.
/// </summary>
public sealed class RowStack
{
    private readonly Control _host;
    private readonly List<Control> _rows = [];

    public RowStack(Control host, int spacing = 4)
    {
        _host = host;
        Spacing = spacing;
        host.Layout += (_, _) => Arrange();
    }

    public int Spacing { get; }

    public RowStack Add(Control row, int height = -1)
    {
        if (height >= 0)
        {
            row.Height = height;
        }

        _rows.Add(row);
        _host.Controls.Add(row);
        return this;
    }

    /// <summary>Adds a row that fills the space left below the others.</summary>
    public RowStack AddFill(Control row)
    {
        row.Tag = FillTag;
        _rows.Add(row);
        _host.Controls.Add(row);
        return this;
    }

    private static readonly object FillTag = new();

    public void Arrange()
    {
        var client = _host.ClientRectangle;
        var area = new Rectangle(client.X + _host.Padding.Left, client.Y + _host.Padding.Top,
            client.Width - _host.Padding.Horizontal, client.Height - _host.Padding.Vertical);

        // Every row is always laid out: Visible reads false for all children
        // while the host page is hidden, so it cannot be used to skip rows.
        var shown = _rows;
        int fixedHeight = shown.Where(r => r.Tag != FillTag).Sum(r => r.Height + Spacing);
        int y = area.Y;
        foreach (var row in shown)
        {
            int h = row.Tag == FillTag ? Math.Max(20, area.Height - fixedHeight) : row.Height;
            row.Bounds = new Rectangle(area.X, y, area.Width, h);
            y += h + Spacing;
        }
    }
}
