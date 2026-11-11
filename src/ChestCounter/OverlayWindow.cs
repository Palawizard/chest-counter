using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace ChestCounter;

// A small panel over the game: the estimated total and the last match. It never takes the focus
// and lets clicks through, except while the player moves it.
internal sealed class OverlayWindow : Form
{
    private static readonly Color Ground = Color.FromArgb(36, 22, 10);
    private static readonly Color Gold = Color.FromArgb(255, 213, 92);
    private static readonly Color Cream = Color.FromArgb(251, 236, 200);
    private static readonly PrivateFontCollection Figures = LoadFigures();

    private string _total = "";
    private string _last = "";
    private bool _moving;

    public OverlayWindow()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        BackColor = Ground;
        Opacity = 0.9;
        DoubleBuffered = true;
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96F, 96F);
        ClientSize = new Size(230, 84);
        MouseDown += (_, e) =>
        {
            if (_moving && e.Button == MouseButtons.Left)
            {
                NativeMethods.StartDrag(Handle);
                Moved?.Invoke(Location);
            }
        };
    }

    public event Action<Point>? Moved;

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool Moving
    {
        get => _moving;
        set
        {
            _moving = value;
            Cursor = value ? Cursors.SizeAll : Cursors.Default;
            if (IsHandleCreated)
            {
                NativeMethods.SetClickThrough(Handle, !value);
            }

            Invalidate();
        }
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var parameters = base.CreateParams;
            parameters.ExStyle |= NativeMethods.WsExToolWindow | NativeMethods.WsExNoActivate | NativeMethods.WsExLayered | NativeMethods.WsExTransparent;
            return parameters;
        }
    }

    public void Show(string total, string last)
    {
        if (total == _total && last == _last)
        {
            return;
        }

        _total = total;
        _last = last;
        Invalidate();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        NativeMethods.SetClickThrough(Handle, !_moving);
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        using var shape = Rounded(new Rectangle(Point.Empty, ClientSize), Scale(12));
        Region = new Region(shape);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        if (_moving)
        {
            using var rim = new Pen(Gold, Scale(2));
            using var shape = Rounded(Rectangle.Inflate(new Rectangle(Point.Empty, ClientSize), -Scale(1), -Scale(1)), Scale(11));
            g.DrawPath(rim, shape);
        }

        using var label = new Font("Segoe UI", 9f, FontStyle.Bold);
        using var detail = new Font("Segoe UI", 9f);
        using var figure = new Font(Figures.Families[0], 26f);
        using var cream = new SolidBrush(Cream);
        using var gold = new SolidBrush(Gold);
        var left = Scale(14);
        g.DrawString(Text, label, cream, left, Scale(8));
        g.DrawString(_total, figure, gold, left - Scale(2), Scale(24));
        g.DrawString(_last, detail, cream, left, Scale(62));
    }

    private int Scale(int pixels) => pixels * DeviceDpi / 96;

    private static GraphicsPath Rounded(Rectangle bounds, int radius)
    {
        var path = new GraphicsPath();
        var diameter = radius * 2;
        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    private static PrivateFontCollection LoadFigures()
    {
        var fonts = new PrivateFontCollection();
        fonts.AddFontFile(Path.Combine(AppContext.BaseDirectory, "wwwroot", "fonts", "LilitaOne.ttf"));
        return fonts;
    }
}
