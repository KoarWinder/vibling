using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;

namespace Vibling;

/// Меню в духе Discord: тёмное, со скруглёнными углами и мягкой тенью.
/// Стандартное меню Windows так не умеет, поэтому рисуем его сами в прозрачном окне.
class PopupMenu : LayeredForm
{
    public class Entry
    {
        public string Text;
        public Action Act;
        public bool Check, Separator, Header;
        public Image Icon;
    }

    const float Shadow = 12, Pad = 4, ItemH = 26, SepH = 7, Radius = 7, TextX = 32;

    static readonly Color Bg = Color.FromArgb(33, 33, 36);
    static readonly Color Border = Color.FromArgb(60, 60, 66);
    static readonly Color HoverBg = Color.FromArgb(56, 56, 62);
    static readonly Color TextC = Color.FromArgb(232, 232, 236);
    static readonly Color Muted = Color.FromArgb(146, 146, 154);
    static readonly Font ItemFont = new("Segoe UI", 12f, GraphicsUnit.Pixel);
    static readonly Font HeaderFont = new("Segoe UI Semibold", 12f, GraphicsUnit.Pixel);
    static PopupMenu current;

    readonly List<Entry> items;
    readonly List<RectangleF> rects = new();
    readonly float U, W, H;
    readonly Bitmap bmp;
    readonly System.Windows.Forms.Timer fade = new() { Interval = 15 };
    int hot = -1, px, py;
    byte alpha;

    PopupMenu(List<Entry> items, float u)
    {
        this.items = items;
        U = u;
        KeyPreview = true;

        float w = 0, y = Pad;
        using (var tmp = new Bitmap(1, 1))
        using (var g = Graphics.FromImage(tmp))
        {
            foreach (var it in items)
            {
                float h = it.Separator ? SepH : ItemH;
                rects.Add(new RectangleF(Pad, y, 0, h));
                y += h;
                if (!it.Separator) w = Math.Max(w, g.MeasureString(it.Text, it.Header ? HeaderFont : ItemFont).Width);
            }
        }
        W = Math.Max(160, TextX + w + 22);
        H = y + Pad;
        for (int i = 0; i < rects.Count; i++) rects[i] = new RectangleF(Pad, rects[i].Y, W - Pad * 2, rects[i].Height);
        bmp = new Bitmap((int)Math.Ceiling((W + Shadow * 2) * U), (int)Math.Ceiling((H + Shadow * 2) * U), PixelFormat.Format32bppArgb);

        fade.Tick += (_, _) =>
        {
            alpha = (byte)Math.Min(255, alpha + 64);
            Render();
            if (alpha == 255) fade.Stop();
        };
    }

    /// Открывает меню у точки на экране; если не помещается — разворачивает вверх или влево.
    public static void Open(List<Entry> items, Point at, float u)
    {
        current?.Close();
        var m = new PopupMenu(items, u);
        var wa = Screen.FromPoint(at).WorkingArea;
        float w = m.W * u, h = m.H * u;
        int x = at.X + w > wa.Right ? at.X - (int)w : at.X;
        int y = at.Y + h > wa.Bottom ? at.Y - (int)h : at.Y;
        m.px = (int)(x - Shadow * u);
        m.py = (int)(y - Shadow * u);
        current = m;
        m.Show();
        m.Activate();
        Native.SetForegroundWindow(m.Handle);
        m.Render();
        m.fade.Start();
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle &= ~0x08000000; // меню должно получать фокус, чтобы закрываться по клику мимо
            return cp;
        }
    }

    protected override bool ShowWithoutActivation => false;

    protected override void OnDeactivate(EventArgs e)
    {
        base.OnDeactivate(e);
        Close();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        fade.Dispose();
        bmp.Dispose();
        if (current == this) current = null;
        base.OnFormClosed(e);
    }

    int HitTest(Point client)
    {
        float lx = client.X / U - Shadow, ly = client.Y / U - Shadow;
        for (int i = 0; i < rects.Count; i++)
            if (!items[i].Separator && !items[i].Header && rects[i].Contains(lx, ly)) return i;
        return -1;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        int h = HitTest(e.Location);
        if (h != hot) { hot = h; Render(); }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        if (hot != -1) { hot = -1; Render(); }
        base.OnMouseLeave(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;
        int i = HitTest(e.Location);
        if (i < 0) return;
        Invoke(i);
    }

    void Invoke(int i)
    {
        var act = items[i].Act;
        Close();
        act?.Invoke();
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys key)
    {
        switch (key)
        {
            case Keys.Escape: Close(); return true;
            case Keys.Down: StepHot(1); return true;
            case Keys.Up: StepHot(-1); return true;
            case Keys.Enter: if (hot >= 0) Invoke(hot); return true;
        }
        return base.ProcessCmdKey(ref msg, key);
    }

    void StepHot(int dir)
    {
        int i = hot;
        for (int n = 0; n < items.Count; n++)
        {
            i = (i + dir + items.Count) % items.Count;
            if (!items[i].Separator && !items[i].Header) { hot = i; Render(); return; }
        }
    }

    static GraphicsPath Round(RectangleF r, float rad)
    {
        var p = new GraphicsPath();
        float d = rad * 2;
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    void Render()
    {
        if (IsDisposed) return;
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.Transparent);
            g.ScaleTransform(U, U);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            // мягкая тень: несколько полупрозрачных слоёв
            for (int i = 0; i < 10; i++)
            {
                float grow = Shadow - 2 - i * 1.2f;
                using var sp = Round(new RectangleF(Shadow - grow, Shadow - grow + 3, W + grow * 2, H + grow * 2), Radius + grow);
                using var sb = new SolidBrush(Color.FromArgb(7, 0, 0, 0));
                g.FillPath(sb, sp);
            }

            g.TranslateTransform(Shadow, Shadow);
            using (var body = Round(new RectangleF(0, 0, W, H), Radius))
            {
                using var bb = new SolidBrush(Bg);
                g.FillPath(bb, body);
                using var pen = new Pen(Border, 1);
                g.DrawPath(pen, body);
            }

            for (int i = 0; i < items.Count; i++)
            {
                var it = items[i];
                var r = rects[i];
                if (it.Separator)
                {
                    using var sp = new Pen(Border, 1);
                    float sy = r.Y + r.Height / 2;
                    g.DrawLine(sp, 8, sy, W - 8, sy);
                    continue;
                }
                if (i == hot)
                {
                    using var hp = Round(r, 4);
                    using var hb = new SolidBrush(HoverBg);
                    g.FillPath(hb, hp);
                }
                if (it.Icon != null)
                {
                    var ps = g.PixelOffsetMode;
                    g.InterpolationMode = InterpolationMode.NearestNeighbor;
                    g.PixelOffsetMode = PixelOffsetMode.Half;
                    g.DrawImage(it.Icon, 11, r.Y + (r.Height - 16) / 2, 16, 16);
                    g.PixelOffsetMode = ps;
                }
                if (it.Check)
                {
                    using var cp = new Pen(TextC, 1.6f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
                    float cy = r.Y + r.Height / 2;
                    g.DrawLines(cp, new[] { new PointF(13, cy), new PointF(16.5f, cy + 3.5f), new PointF(22.5f, cy - 3.5f) });
                }
                var font = it.Header ? HeaderFont : ItemFont;
                var size = g.MeasureString(it.Text, font);
                using var tb = new SolidBrush(it.Header ? Muted : TextC);
                g.DrawString(it.Text, font, tb, TextX, r.Y + (r.Height - size.Height) / 2);
            }
        }
        Present(bmp, px, py, alpha);
    }
}
