using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace Vibling;

static class Theme
{
    static Color C(int r, int g, int b) => Color.FromArgb(r, g, b);
    public static readonly Color Bg = C(22, 19, 30);
    public static readonly Color Card = C(32, 28, 43);
    public static readonly Color Raised = C(44, 39, 58);
    public static readonly Color Hover = C(56, 50, 73);
    public static readonly Color Line = C(62, 56, 80);
    public static readonly Color Fg = C(238, 233, 246);
    public static readonly Color Muted = C(156, 148, 176);

    public static Color OnAccent(Color a) => a.R * 0.299 + a.G * 0.587 + a.B * 0.114 > 150 ? C(24, 18, 22) : C(250, 248, 255);

    public static GraphicsPath Round(RectangleF r, float rad)
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
}

/// Общая база для нарисованных вручную контролов: двойная буферизация и масштаб под DPI.
class Painted : Control
{
    public Color Accent = Color.FromArgb(217, 119, 87);
    protected bool hover;

    public Painted()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        BackColor = Theme.Bg;
        ForeColor = Theme.Fg;
        Cursor = Cursors.Hand;
    }

    protected float K => DeviceDpi / 96f;

    protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }

    protected static void Prep(Graphics g)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
    }
}

/// Набор «таблеток»: одна выбранная. Equal = все одной ширины в один ряд.
class Chips : Painted
{
    public string[] Items = Array.Empty<string>();
    public int Selected;
    public bool Equal;
    public event Action<int> Changed;
    readonly List<RectangleF> rects = new();
    int hot = -1;

    void DoLayout()
    {
        rects.Clear();
        float h = 34 * K, gap = 8 * K, x = 0, yy = 0;
        if (Equal)
        {
            float w = (Width - gap * (Items.Length - 1)) / Items.Length;
            for (int i = 0; i < Items.Length; i++) rects.Add(new RectangleF(i * (w + gap), 0, w, h));
        }
        else
        {
            foreach (var it in Items)
            {
                float w = TextRenderer.MeasureText(it, Font).Width + 26 * K;
                if (x + w > Width && x > 0) { x = 0; yy += h + gap; }
                rects.Add(new RectangleF(x, yy, w, h));
                x += w + gap;
            }
        }
        int need = (int)Math.Ceiling(rects.Count == 0 ? h : rects.Max(r => r.Bottom)) + 1;
        if (Height != need) Height = need;
    }

    protected override void OnSizeChanged(EventArgs e) { base.OnSizeChanged(e); DoLayout(); }
    protected override void OnFontChanged(EventArgs e) { base.OnFontChanged(e); DoLayout(); }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        int h = rects.FindIndex(r => r.Contains(e.Location));
        if (h != hot) { hot = h; Invalidate(); }
    }

    protected override void OnMouseLeave(EventArgs e) { hot = -1; base.OnMouseLeave(e); }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        int i = rects.FindIndex(r => r.Contains(e.Location));
        if (i < 0 || i == Selected) return;
        Selected = i;
        Invalidate();
        Changed?.Invoke(i);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        if (rects.Count != Items.Length) DoLayout();
        var g = e.Graphics;
        Prep(g);
        for (int i = 0; i < rects.Count; i++)
        {
            var r = rects[i];
            bool sel = i == Selected;
            using var path = Theme.Round(r, r.Height / 2);
            using var b = new SolidBrush(sel ? Accent : i == hot ? Theme.Hover : Theme.Raised);
            g.FillPath(b, path);
            TextRenderer.DrawText(g, Items[i], Font, Rectangle.Round(r), sel ? Theme.OnAccent(Accent) : Theme.Fg,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
    }
}

/// Строка с переключателем: заголовок, пояснение и тумблер справа.
class Toggle : Painted
{
    public bool Checked;
    public string Title = "", Sub = "";
    public event Action<bool> Changed;

    public Toggle() { Height = 52; }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        Checked = !Checked;
        Invalidate();
        Changed?.Invoke(Checked);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        Prep(g);
        float tw = 42 * K, th = 24 * K;
        var track = new RectangleF(Width - tw - 2, (Height - th) / 2, tw, th);
        using (var path = Theme.Round(track, th / 2))
        using (var b = new SolidBrush(Checked ? Accent : hover ? Theme.Hover : Theme.Line))
            g.FillPath(b, path);
        float kd = th - 6 * K;
        float kx = Checked ? track.Right - kd - 3 * K : track.X + 3 * K;
        using (var kb = new SolidBrush(Checked ? Theme.OnAccent(Accent) : Theme.Fg))
            g.FillEllipse(kb, kx, track.Y + 3 * K, kd, kd);

        int textW = (int)(Width - tw - 16 * K);
        using var small = new Font(Font.FontFamily, Font.Size * 0.86f);
        int titleH = TextRenderer.MeasureText(Title, Font).Height;
        int subH = string.IsNullOrEmpty(Sub) ? 0 : TextRenderer.MeasureText(Sub, small).Height;
        int top = (Height - titleH - subH) / 2;
        TextRenderer.DrawText(g, Title, Font, new Rectangle(0, top, textW, titleH), Theme.Fg, TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
        if (subH > 0)
            TextRenderer.DrawText(g, Sub, small, new Rectangle(0, top + titleH, textW, subH), Theme.Muted, TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
    }
}

class FlatButton : Painted
{
    public bool Primary;

    public FlatButton() { Height = 38; }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        Prep(g);
        var r = new RectangleF(0.5f, 0.5f, Width - 1, Height - 1);
        using var path = Theme.Round(r, 10 * K);
        Color bg = Primary ? (hover ? ControlPaint.Light(Accent, 0.15f) : Accent) : hover ? Theme.Hover : Theme.Raised;
        using (var b = new SolidBrush(bg)) g.FillPath(b, path);
        TextRenderer.DrawText(g, Text, Font, ClientRectangle, Primary ? Theme.OnAccent(Accent) : Theme.Fg,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }

    protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); Invalidate(); }
}

/// Сетка скинов: в каждой клетке мини-портрет питомца.
class SkinPicker : Painted
{
    public int Selected;
    public Func<string> AccessoryId = () => "none";
    public event Action<int> Changed;
    readonly List<RectangleF> rects = new();
    readonly Pose pose = new();
    int hot = -1;

    void DoLayout()
    {
        rects.Clear();
        float w = 76 * K, h = 88 * K, gap = 8 * K;
        int cols = Math.Max(1, (int)((Width + gap) / (w + gap)));
        w = (Width - gap * (cols - 1)) / cols;
        for (int i = 0; i < Looks.Skins.Length; i++)
            rects.Add(new RectangleF(i % cols * (w + gap), i / cols * (h + gap), w, h));
        int need = (int)Math.Ceiling(rects.Max(r => r.Bottom)) + 1;
        if (Height != need) Height = need;
    }

    protected override void OnSizeChanged(EventArgs e) { base.OnSizeChanged(e); DoLayout(); }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        int h = rects.FindIndex(r => r.Contains(e.Location));
        if (h != hot) { hot = h; Invalidate(); }
    }

    protected override void OnMouseLeave(EventArgs e) { hot = -1; base.OnMouseLeave(e); }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        int i = rects.FindIndex(r => r.Contains(e.Location));
        if (i < 0 || i == Selected) return;
        Selected = i;
        Invalidate();
        Changed?.Invoke(i);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        if (rects.Count == 0) DoLayout();
        var g = e.Graphics;
        Prep(g);
        using var small = new Font(Font.FontFamily, Font.Size * 0.85f);
        for (int i = 0; i < rects.Count; i++)
        {
            var r = rects[i];
            var skin = Looks.Skins[i];
            using (var path = Theme.Round(r, 12 * K))
            {
                using var b = new SolidBrush(i == hot ? Theme.Hover : Theme.Raised);
                g.FillPath(b, path);
                if (i == Selected)
                {
                    using var pen = new Pen(skin.Accent, 2.5f * K);
                    g.DrawPath(pen, path);
                }
            }
            pose.Reset(0);
            if (i == Selected) { pose.Eyes = Eyes.Happy; pose.Blush = true; }
            g.SmoothingMode = SmoothingMode.None;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            Sprite.Draw(g, pose, skin, AccessoryId(), r.X + r.Width / 2, r.Y + 58 * K, 3.2f * K);
            g.PixelOffsetMode = PixelOffsetMode.Default;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            TextRenderer.DrawText(g, skin.Name, small, new Rectangle((int)r.X, (int)(r.Y + 64 * K), (int)r.Width, (int)(20 * K)),
                i == Selected ? Theme.Fg : Theme.Muted, TextFormatFlags.HorizontalCenter | TextFormatFlags.EndEllipsis);
        }
    }
}

/// Живое превью: питомец дышит, моргает, смотрит на курсор и радуется клику.
class PreviewBox : Painted
{
    readonly Settings S;
    readonly Pose pose = new();
    readonly System.Windows.Forms.Timer timer = new() { Interval = 33 };
    readonly Random rnd = new();
    float time, blinkT = 2, happyT, waveT = 4;
    int face = 1;

    public PreviewBox(Settings s)
    {
        S = s;
        timer.Tick += (_, _) => { Step(0.033f); Invalidate(); };
        timer.Start();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) timer.Dispose();
        base.Dispose(disposing);
    }

    protected override void OnMouseClick(MouseEventArgs e) { happyT = 1.2f; }

    void Step(float dt)
    {
        time += dt;
        blinkT -= dt;
        if (blinkT < -0.12f) blinkT = 2 + (float)rnd.NextDouble() * 3;
        happyT -= dt;
        waveT -= dt;
        if (waveT < -1.5f) { waveT = 5 + (float)rnd.NextDouble() * 4; face = rnd.Next(2) == 0 ? 1 : -1; }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        Prep(g);
        var skin = Looks.SkinById(S.Skin);
        var r = new RectangleF(0, 0, Width - 1, Height - 1);
        using (var path = Theme.Round(r, 18 * K))
        {
            using var bg = new LinearGradientBrush(r, Theme.Card, Color.FromArgb(40, 34, 54), 90);
            g.FillPath(bg, path);
            g.SetClip(path);
            using var glow = new SolidBrush(Color.FromArgb(40, skin.Accent));
            g.FillEllipse(glow, Width * 0.15f, Height * 0.2f, Width * 0.7f, Height * 0.6f);
        }
        float floor = Height * 0.8f;
        using (var floorPen = new Pen(Theme.Line, 2 * K)) g.DrawLine(floorPen, 20 * K, floor, Width - 20 * K, floor);

        float P = Math.Min(Width, Height) / 26f;
        pose.Reset(time);
        var c = PointToClient(Cursor.Position);
        pose.LookX = (c.X > Width * 0.62f ? 1 : c.X < Width * 0.38f ? -1 : 0) * face;
        pose.LookY = c.Y < Height * 0.25f ? -1 : 0;
        float sqY = 1 + (float)Math.Sin(time * 2.2) * 0.025f, hop = 0;
        if (blinkT < 0) pose.Eyes = Eyes.Blink;
        if (happyT > 0)
        {
            pose.Arms = Arms.Up; pose.Eyes = Eyes.Happy; pose.Blush = true; pose.Mouth = Mouth.Smile;
            hop = -Math.Abs((float)Math.Sin(happyT * 10)) * 2 * P;
        }
        else if (waveT < 0) { pose.Arms = Arms.Wave; pose.Eyes = Eyes.Happy; pose.Mouth = Mouth.Smile; }

        using (var sh = new SolidBrush(Color.FromArgb(60, 0, 0, 0)))
            g.FillEllipse(sh, Width / 2f - 8 * P, floor - 0.5f * P, 16 * P, P);
        g.SmoothingMode = SmoothingMode.None;
        g.PixelOffsetMode = PixelOffsetMode.Half;
        Sprite.Draw(g, pose, skin, S.Accessory, Width / 2f, floor + hop, P, face, 1, sqY);
        g.ResetClip();
    }
}

class SettingsForm : Form
{
    readonly PetForm pet;
    readonly Settings S;
    readonly List<Painted> accented = new();
    readonly Label stats;
    readonly FlatButton ballBtn, hideBtn;
    readonly SkinPicker skins;
    readonly PreviewBox preview;
    readonly System.Windows.Forms.Timer refresh = new() { Interval = 1000 };

    public SettingsForm(PetForm pet)
    {
        this.pet = pet;
        S = pet.S;

        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96, 96);
        Text = L.T("app");
        Icon = AppIcon.Get();
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Theme.Bg;
        ForeColor = Theme.Fg;
        Font = new Font("Segoe UI", 10f);
        ClientSize = new Size(860, 700);

        // левая колонка: превью и действия
        const int LX = 28, LWd = 280;
        var title = new Label { Text = L.T("app"), Font = new Font("Segoe UI", 20f, FontStyle.Bold), ForeColor = Theme.Fg, AutoSize = true, Location = new Point(LX - 2, 20) };
        var subtitle = new Label { Text = L.T("tagline"), ForeColor = Theme.Muted, AutoSize = true, Location = new Point(LX, 62) };
        preview = new PreviewBox(S) { Location = new Point(LX, 96), Size = new Size(LWd, 250) };
        stats = new Label { ForeColor = Theme.Muted, Location = new Point(LX, 358), Size = new Size(LWd, 64), Font = new Font("Segoe UI", 9f) };

        var feed = Button(L.T("feed"), LX, 432, 134, () => pet.Feed());
        ballBtn = Button(L.T("ball"), LX + 146, 432, 134, () => { pet.ToggleBall(); UpdateButtons(); });
        var summon = Button(L.T("summon"), LX, 478, LWd, pet.Summon);
        summon.Primary = true;
        var flip = Button(L.T("flip"), LX, 524, 134, pet.Flip);
        var pat = Button(L.T("pet"), LX + 146, 524, 134, pet.PetMe);

        var autostart = new Toggle { Title = L.T("autostart"), Checked = Autostart.On, Location = new Point(LX, 576), Size = new Size(LWd, 46) };
        autostart.Changed += v => Autostart.On = v;
        accented.Add(autostart);
        hideBtn = Button(L.T("hide"), LX, 636, 134, () => { pet.SetHidden(!pet.IsHidden); UpdateButtons(); });
        var quit = Button(L.T("quit.button"), LX + 146, 636, 134, () => pet.Quit());

        Controls.AddRange(new Control[] { title, subtitle, preview, stats, feed, ballBtn, summon, flip, pat, autostart, hideBtn, quit });

        // правая колонка: внешний вид и поведение
        const int RX = 344, RW = 488;
        int y = 22;

        Section(L.T("sec.skin"), RX, ref y);
        skins = new SkinPicker { Location = new Point(RX, y), Width = RW, Selected = Array.FindIndex(Looks.Skins, s => s.Id == S.Skin), AccessoryId = () => S.Accessory };
        if (skins.Selected < 0) skins.Selected = 0;
        skins.Changed += i => { S.Skin = Looks.Skins[i].Id; S.Save(); ApplyAccent(); };
        Controls.Add(skins);
        accented.Add(skins);
        y += skins.Height + 18;

        Section(L.T("sec.accessory"), RX, ref y);
        var acc = new Chips { Items = Looks.Accessories.Select(a => a.Name).ToArray(), Location = new Point(RX, y), Width = RW };
        acc.Selected = Math.Max(0, Array.FindIndex(Looks.Accessories, a => a.Id == S.Accessory));
        acc.Changed += i => { S.Accessory = Looks.Accessories[i].Id; S.Save(); skins.Invalidate(); };
        Controls.Add(acc);
        accented.Add(acc);
        y += acc.Height + 18;

        Section(L.T("sec.size"), RX, ref y);
        var size = new Chips { Items = Enumerable.Range(0, 4).Select(i => L.T("size." + i)).ToArray(), Equal = true, Selected = S.Size, Location = new Point(RX, y), Width = RW };
        size.Changed += i => { S.Size = i; S.Save(); };
        Controls.Add(size);
        accented.Add(size);
        y += size.Height + 18;

        Section(L.T("sec.mood"), RX, ref y);
        var mood = new Chips { Items = Enumerable.Range(0, 3).Select(i => L.T("mood." + i)).ToArray(), Equal = true, Selected = S.Mood, Location = new Point(RX, y), Width = RW };
        mood.Changed += i => { S.Mood = i; S.Save(); };
        Controls.Add(mood);
        accented.Add(mood);
        y += mood.Height + 18;

        Section(L.T("sec.behavior"), RX, ref y);
        AddToggle(L.T("opt.bongo"), L.T("opt.bongo.sub"), S.Bongo, v => S.Bongo = v, RX, RW, ref y);
        AddToggle(L.T("opt.windows"), L.T("opt.windows.sub"), S.Windows, v => S.Windows = v, RX, RW, ref y);
        AddToggle(L.T("opt.follow"), L.T("opt.follow.sub"), S.Follow, v => S.Follow = v, RX, RW, ref y);
        AddToggle(L.T("opt.sleep"), L.T("opt.sleep.sub"), S.SleepWhenIdle, v => S.SleepWhenIdle = v, RX, RW, ref y);
        y += 8;
        Section(L.T("sec.language"), RX, ref y);
        var lang = new Chips { Items = L.Languages.Select(l => L.T("lang." + l)).ToArray(), Equal = true, Location = new Point(RX, y), Width = RW };
        lang.Selected = Math.Max(0, Array.IndexOf(L.Languages, S.Language));
        lang.Changed += i =>
        {
            S.Language = L.Languages[i];
            S.Save();
            L.Set(S.Language);
            pet.BeginInvoke(pet.ReloadSettings);
        };
        Controls.Add(lang);
        accented.Add(lang);
        y += lang.Height + 18;

        ClientSize = new Size(860, Math.Max(700, y + 4));

        ApplyAccent();
        UpdateButtons();
        UpdateStats();
        refresh.Tick += (_, _) => { UpdateStats(); UpdateButtons(); };
        refresh.Start();
    }

    FlatButton Button(string text, int x, int y, int w, Action click)
    {
        var b = new FlatButton { Text = text, Location = new Point(x, y), Size = new Size(w, 38) };
        b.Click += (_, _) => click();
        accented.Add(b);
        return b;
    }

    void Section(string text, int x, ref int y)
    {
        Controls.Add(new Label
        {
            Text = text.ToUpperInvariant(), ForeColor = Theme.Muted, AutoSize = true, Location = new Point(x, y),
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold)
        });
        y += 26;
    }

    void AddToggle(string title, string sub, bool value, Action<bool> set, int x, int w, ref int y)
    {
        var t = new Toggle { Title = title, Sub = sub, Checked = value, Location = new Point(x, y), Size = new Size(w, 52) };
        t.Changed += v => { set(v); S.Save(); };
        Controls.Add(t);
        accented.Add(t);
        y += 54;
    }

    void ApplyAccent()
    {
        var a = Looks.SkinById(S.Skin).Accent;
        foreach (var c in accented) { c.Accent = a; c.Invalidate(); }
        preview.Invalidate();
    }

    void UpdateButtons()
    {
        ballBtn.Text = L.T(pet.HasBall ? "ball.remove" : "ball");
        hideBtn.Text = L.T(pet.IsHidden ? "show" : "hide");
    }

    void UpdateStats()
    {
        stats.Text = string.Format(L.T("stats"), S.Pets.ToString("N0"), S.Keys.ToString("N0"), S.Tokens.ToString("N0"));
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        int on = 1;
        Native.DwmSetWindowAttribute(Handle, 20, ref on, 4); // тёмный заголовок окна
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        refresh.Dispose();
        S.Save();
        base.OnFormClosed(e);
    }
}
