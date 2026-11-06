using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Runtime.InteropServices;

namespace Vibling;

class Particle
{
    public float X, Y, VX, VY, Life, Max, G, Size;
    public int Kind;
    public string Text;
    public Color C;
}

enum St { Idle, Walk, Run, Code, Sleep, Drag, Air, Land, Splat, Dizzy, Slide, Happy, Eat, Crouch, Teeter, Stretch, Look, Dance, Sit, Wave, Yawn, Float, Party }
enum AirKind { Drop, Jump, Flip }

class PetForm : LayeredForm
{
    const float G = 2200;

    public readonly Settings S;
    readonly float U;
    float P, LW, LH, FX, FY;
    Bitmap bmp;

    readonly Random rnd = new();
    readonly Stopwatch sw = Stopwatch.StartNew();
    readonly System.Windows.Forms.Timer timer = new();
    readonly System.Windows.Forms.Timer updates = new();
    readonly List<Particle> parts = new();
    readonly Pose pose = new();
    readonly NotifyIcon tray = new();
    readonly HashSet<int> keysDown = new();
    readonly Native.HookProc hookProc;
    IntPtr hook;
    SettingsWindow settingsWindow;
    BallForm ball;

    static readonly string[] CodeGlyphs = { "{ }", "</>", "=>", ";", "fn()", "++", "git", "0x1F", "[ ]", "//" };
    static readonly Color Neon = Color.FromArgb(127, 233, 255);

    float x, y, vx, vy, angle, spin, time, stT, stDur, targetX, speed, airT;
    float sqX = 1, sqY = 1, blinkT = 3, pawT, codeT, snoreT, kickCd, dustT, occT, nearT, saveT, lastT;
    float lastInteract, lastKeyT = -999;
    int face = 1, paw;
    float aspin, bonkT, fHappyT, fEatT, fDizzyT, hicT, squintT, fxT, faT, faDur, curSpeed;
    FA fa;
    int faDir = 1;
    PointF faPt;
    Point lastCur;
    bool fSleep;

    readonly LofiPlayer music = new();
    readonly ScriptRunner scripts = new();
    bool afk, afkMusic, party, beatHigh;
    float topT, clockT, hideUntil = -1, nod;
    int beatCount, beatIdx;
    double beatPos, lastBeatT, beatGap = 0.7;

    public LofiPlayer Music => music;

    enum FA
    {
        Hover, Confused, Dance, Swim, SlowSpin, Stretch, Sit, LieBack, UpsideDown, Wave, ToCursor, Dash, WallPlay,
        PeekTop, Code, Snack, Sparkles, Moonwalk, Hiccup, Zoomies, Orbit, TopSpin, Superhero, Nap, Startled, Ball,
    }
    bool goOff, teeterOff, umbrellaWanted, umbrellaOpen, waved, hidden;
    St st = St.Air;
    AirKind air = AirKind.Drop;
    float jumpVX, jumpVY;
    IntPtr onWin;
    Rectangle winR;

    bool mouseDown, dragging;
    Point downPt;
    PointF grabOff;
    readonly Queue<(float t, Point p)> trail = new();
    float dragVX;

    public PetForm(Settings settings)
    {
        S = settings;
        using (var g0 = Graphics.FromHwnd(IntPtr.Zero)) U = g0.DpiX / 96f;
        Text = L.T("app");
        ApplySize();

        var wa = Screen.PrimaryScreen.WorkingArea;
        x = wa.Left + wa.Width * 0.7f;
        y = wa.Top + 60 * U;
        umbrellaWanted = true;

        tray.Icon = AppIcon.Get();
        tray.Text = L.T("app");
        tray.Visible = true;
        tray.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left) ShowSettings();
            else if (e.Button == MouseButtons.Right) ShowMenu(Cursor.Position);
        };

        hookProc = HookCb;
        using (var cur = Process.GetCurrentProcess())
            hook = Native.SetWindowsHookEx(13, hookProc, Native.GetModuleHandle(cur.MainModule.ModuleName), 0);

        timer.Interval = 30;
        timer.Tick += (_, _) => Tick();
        timer.Start();

        updates.Interval = 3000;
        updates.Tick += async (_, _) =>
        {
            updates.Stop();
            await Updater.Check(this, false);
        };
        updates.Start();
        Shown += (_, _) => scripts.Fire(S.Scripts, "start");
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Desk.Own.Add(Handle);
    }

    public void ApplySize()
    {
        P = S.Pixel;
        LW = 36 * P; LH = 40 * P; FX = 18 * P; FY = 27 * P;
        bmp?.Dispose();
        bmp = new Bitmap((int)Math.Ceiling(LW * U), (int)Math.Ceiling(LH * U), PixelFormat.Format32bppArgb);
    }

    float Px => P * U;

    void ShowMenu(Point at)
    {
        var skin = Looks.SkinById(S.Skin);
        var icon = new Bitmap(19, 19);
        using (var g = Graphics.FromImage(icon))
        {
            var p = new Pose();
            p.Reset(0);
            g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
            Sprite.Draw(g, p, skin, "none", 9.5f, 16.5f, 1);
        }
        PopupMenu.Entry Item(string key, Action act, bool check = false) => new() { Text = L.T(key), Act = act, Check = check };
        var sep = new PopupMenu.Entry { Separator = true };
        var items = new List<PopupMenu.Entry>
        {
            new() { Text = L.T("app"), Header = true, Icon = icon },
            sep,
            Item("pet", PetMe),
            Item("feed", Feed),
            Item("ball", ToggleBall, ball != null),
            Item("flip.menu", Flip),
            Item("opt.zerog", () => S.ZeroG = !S.ZeroG, S.ZeroG),
            Item("menu.music", ToggleMusic, music.On),
            Item(st == St.Sleep ? "wake" : "sleep", () =>
            {
                if (st == St.Float) fSleep = !fSleep;
                else if (st == St.Sleep) WakeUp();
                else if (Grounded && !Busy) Set(St.Yawn, 1.3f);
            }),
            sep,
            Item("settings", ShowSettings),
            Item(hidden ? "show" : "hide", () => SetHidden(!hidden)),
            sep,
            Item("quit", Quit),
        };
        if (music.On) items.Insert(items.FindIndex(e => e.Text == L.T("menu.music")) + 1, Item("music.next", NextTrack));
        PopupMenu.Open(items, at, U);
    }

    public void ShowSettings()
    {
        if (settingsWindow == null)
        {
            settingsWindow = new SettingsWindow(this);
            settingsWindow.Closed += (_, _) => settingsWindow = null;
            System.Windows.Forms.Integration.ElementHost.EnableModelessKeyboardInterop(settingsWindow);
            settingsWindow.Show();
        }
        if (settingsWindow.WindowState == System.Windows.WindowState.Minimized) settingsWindow.WindowState = System.Windows.WindowState.Normal;
        settingsWindow.Topmost = true;
        settingsWindow.Topmost = false;
        settingsWindow.Activate();
        if (!S.FirstRunDone) { S.FirstRunDone = true; S.Save(); }
    }

    public void ReloadSettings()
    {
        var old = settingsWindow;
        double? left = old?.Left, top = old?.Top;
        old?.Close();
        settingsWindow = null;
        tray.Text = L.T("app");
        Text = L.T("app");
        ShowSettings();
        if (left.HasValue)
        {
            settingsWindow.WindowStartupLocation = System.Windows.WindowStartupLocation.Manual;
            settingsWindow.Left = left.Value;
            settingsWindow.Top = top.Value;
        }
    }

    public void OnUpdateFound(bool manual)
    {
        if (settingsWindow != null) settingsWindow.RefreshBanner();
        else if (manual || (!fsHidden && Desk.Fullscreen == null)) ShowSettings();
    }

    public bool IsHidden => hidden;
    public bool HasBall => ball != null;

    bool fsHidden;
    float fsT;

    public void SetHidden(bool h)
    {
        hidden = h;
        ApplyVisibility();
    }

    void ApplyVisibility()
    {
        bool show = !hidden && !fsHidden;
        if (show == Visible) return;
        if (show) { Show(); ball?.Show(); KeepOnTop(); }
        else { Hide(); ball?.Hide(); }
    }

    public void Quit()
    {
        S.Save();
        timer.Stop();
        if (hook != IntPtr.Zero) { Native.UnhookWindowsHookEx(hook); hook = IntPtr.Zero; }
        tray.Visible = false;
        tray.Dispose();
        ball?.Close();
        music.Stop();
        music.Dispose();
        settingsWindow?.Close();
        Application.Exit();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; return; }
        S.Save();
        if (hook != IntPtr.Zero) { Native.UnhookWindowsHookEx(hook); hook = IntPtr.Zero; }
        tray.Visible = false;
        base.OnFormClosing(e);
    }

    IntPtr HookCb(int n, IntPtr w, IntPtr l)
    {
        if (n >= 0)
        {
            int msg = (int)w, vk = Marshal.ReadInt32(l);
            if (msg == 0x100 || msg == 0x104) { if (keysDown.Add(vk)) OnKey(); }
            else if (msg == 0x101 || msg == 0x105) keysDown.Remove(vk);
        }
        return Native.CallNextHookEx(hook, n, w, l);
    }

    void OnKey()
    {
        S.Keys++;
        scripts.Fire(S.Scripts, "keys", sc => sc.When.Num >= 1 && S.Keys % (long)sc.When.Num == 0);
        lastKeyT = time;
        if (!S.Bongo || hidden) return;
        paw ^= 1;
        pawT = 0.12f;
        if (st == St.Sleep)
        {
            StartAir(AirKind.Jump, 0, -520 * U);
            return;
        }
        if (st == St.Float)
        {
            fSleep = false;
            if (rnd.NextDouble() < 0.3) SpawnGlyph();
            return;
        }
        if (st is St.Idle or St.Walk or St.Code or St.Look or St.Sit or St.Stretch or St.Dance or St.Wave)
        {
            if (st != St.Code) Set(St.Code, 3);
            if (rnd.NextDouble() < 0.3) SpawnGlyph();
        }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        lastInteract = time;
        if (e.Button == MouseButtons.Left)
        {
            mouseDown = true;
            dragging = false;
            downPt = Cursor.Position;
            grabOff = new PointF(downPt.X - x, downPt.Y - y);
            trail.Clear();
        }
        else if (e.Button == MouseButtons.Right)
        {
            Native.SetForegroundWindow(Handle);
            ShowMenu(Cursor.Position);
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (!mouseDown || dragging) return;
        var c = Cursor.Position;
        if (Math.Abs(c.X - downPt.X) + Math.Abs(c.Y - downPt.Y) > 5 * U)
        {
            dragging = true;
            dragVX = 0;
            onWin = IntPtr.Zero;
            Set(St.Drag);
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left || !mouseDown) return;
        mouseDown = false;
        if (dragging)
        {
            dragging = false;
            float tvx = 0, tvy = 0;
            if (trail.Count >= 2)
            {
                var a = trail.Peek();
                var b = trail.Last();
                float dt = Math.Max(0.016f, b.t - a.t);
                tvx = Math.Clamp((b.p.X - a.p.X) / dt, -2600 * U, 2600 * U);
                tvy = Math.Clamp((b.p.Y - a.p.Y) / dt, -2600 * U, 2600 * U);
            }
            if (S.ZeroG) StartFloat(tvx, tvy);
            else StartAir(AirKind.Drop, tvx, tvy);
        }
        else PetMe();
    }

    protected override void OnMouseDoubleClick(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left) { scripts.Fire(S.Scripts, "dclick"); Flip(); }
    }

    bool Grounded => st is not (St.Drag or St.Air or St.Float);
    bool Busy => st is St.Splat or St.Dizzy or St.Land or St.Slide or St.Crouch or St.Teeter;

    void Set(St s, float dur = 0) { st = s; stT = 0; stDur = dur; }
    float Rand(float a, float b) => a + (float)rnd.NextDouble() * (b - a);
    Rectangle WA => Desk.AreaAt(new Point((int)x, (int)y - 10));

    (float L, float R) Span()
    {
        if (onWin != IntPtr.Zero) return (winR.Left + 5 * U, winR.Right - 5 * U);
        var wa = WA;
        return (wa.Left + 9 * Px, wa.Right - 9 * Px);
    }

    public void PetMe()
    {
        lastInteract = time;
        scripts.Fire(S.Scripts, "click");
        if (st != St.Float && (!Grounded || Busy)) return;
        S.Pets++;
        if (st == St.Sleep) { WakeUp(); return; }
        if (st == St.Float) { fSleep = false; fHappyT = 1.2f; }
        else { Set(St.Happy, 1.2f); sqX = 1.15f; sqY = 0.85f; }
        for (int i = 0; i < 5; i++)
            parts.Add(new Particle { Kind = 0, X = FX + Rand(-5, 5) * P, Y = FY - 13 * P, VX = Rand(-5, 5) * P, VY = Rand(-15, -8) * P, Life = 1.3f, Max = 1.3f, C = Color.FromArgb(255, 111, 168) });
    }

    public void Feed()
    {
        lastInteract = time;
        parts.Add(new Particle { Kind = 2, X = FX, Y = 0, VY = 40, G = 500, Life = 3, Max = 3 });
    }

    public void Flip()
    {
        lastInteract = time;
        if (st == St.Float) { S.Flips++; aspin += -face * 520; fHappyT = 1f; return; }
        if (!Grounded || Busy) return;
        S.Flips++;
        StartAir(AirKind.Flip, 0, -780 * U);
    }

    public void Summon()
    {
        var wa = Screen.PrimaryScreen.WorkingArea;
        if (hidden) SetHidden(false);
        x = wa.Left + wa.Width / 2f;
        y = wa.Top + 40 * U;
        StartAir(AirKind.Drop, 0, 0);
        umbrellaWanted = true;
    }

    public void ToggleBall()
    {
        if (ball != null) { ball.Close(); ball = null; return; }
        ball = new BallForm(U, x + face * 15 * Px, y - 22 * Px);
        ball.VX = face * 300 * U;
        ball.Removed += () => ball = null;
        ball.Thrown += () => { if (Grounded && !Busy && st != St.Sleep) Set(St.Run); };
        ball.Show();
        if (Grounded && !Busy) Set(St.Run);
    }

    void WakeUp() => StartAir(AirKind.Jump, 0, -520 * U);

    void SpawnGlyph()
    {
        parts.Add(new Particle
        {
            Kind = 1, Text = CodeGlyphs[rnd.Next(CodeGlyphs.Length)], X = FX + Rand(-7, 5) * P, Y = FY - 12 * P,
            VX = Rand(-2, 2) * P, VY = Rand(-8, -5) * P, Life = 1.6f, Max = 1.6f,
            C = rnd.Next(2) == 0 ? Neon : Looks.SkinById(S.Skin).Spark
        });
    }

    void Dust(int n, float dir = 0)
    {
        for (int i = 0; i < n; i++)
        {
            float side = dir != 0 ? dir : (i % 2 == 0 ? -1 : 1);
            parts.Add(new Particle
            {
                Kind = 4, X = FX + side * Rand(2, 6) * P, Y = FY - Rand(0, 1) * P,
                VX = side * Rand(8, 22) * P, VY = -Rand(2, 9) * P, G = 30 * P,
                Life = Rand(0.3f, 0.6f), Max = 0.6f, Size = Rand(0.6f, 1.1f) * P, C = Color.FromArgb(225, 225, 235)
            });
        }
    }

    void StartAir(AirKind kind, float vx0, float vy0)
    {
        st = St.Air;
        stT = 0;
        air = kind;
        vx = vx0;
        vy = vy0;
        airT = 0;
        onWin = IntPtr.Zero;
        umbrellaOpen = false;
        umbrellaWanted = kind == AirKind.Drop && Math.Abs(vx0) < 700 * U && rnd.NextDouble() < 0.3;
        float speed0 = (float)Math.Sqrt(vx0 * vx0 + vy0 * vy0);
        if (kind == AirKind.Flip) spin = -face * 360f / (2 * -vy0 / (G * U));
        else spin = 0;
        if (kind == AirKind.Jump && vx0 != 0) face = Math.Sign(vx0);
        Desk.Scan(true);
    }

    bool TryJumpTo(float tx, float top)
    {
        float g = G * U;
        float dyUp = y - top;
        float H = Math.Max(dyUp, 0) + 60 * U;
        float t1 = (float)Math.Sqrt(2 * H / g);
        float t2 = (float)Math.Sqrt(2 * (H - dyUp) / g);
        jumpVY = -(float)Math.Sqrt(2 * g * H);
        jumpVX = (tx - x) / (t1 + t2);
        face = tx < x ? -1 : 1;
        Set(St.Crouch, 0.22f);
        return true;
    }

    bool JumpSomewhere()
    {
        if (!S.Windows) return false;
        Desk.Scan(true);
        var targets = Desk.JumpTargets(x, y, 40 * U, 380 * U, 450 * U, 20 * U)
            .Where(t => t.h != onWin).ToList();
        if (targets.Count == 0) return false;
        var t = targets[rnd.Next(targets.Count)];
        return TryJumpTo(t.x, t.top);
    }

    void Decide()
    {
        float act = new[] { 0.55f, 1f, 1.6f }[Math.Clamp(S.Mood, 0, 2)];
        var cur = Cursor.Position;

        if (S.Follow) { targetX = cur.X; speed = 110 * U; goOff = false; Set(St.Walk); return; }
        if (S.SleepWhenIdle && time - lastInteract > 300 && time - lastKeyT > 300) { Set(St.Yawn, 1.4f); return; }
        if (ball != null && rnd.NextDouble() < 0.6) { Set(St.Run); return; }

        var (L, R) = Span();
        var options = new (float w, Action a)[]
        {
            (3f * act, () =>
            {
                goOff = onWin != IntPtr.Zero && rnd.NextDouble() < 0.18 * act;
                targetX = goOff ? (rnd.Next(2) == 0 ? L - 50 * U : R + 50 * U) : Rand(L, R);
                speed = Rand(60, 85) * U * (act > 1 ? 1.3f : 1);
                Set(St.Walk);
            }),
            (S.Windows ? 1.6f * act : 0, () => { if (!JumpSomewhere()) Set(St.Look, 2.4f); }),
            (2f, () => Set(St.Code, Rand(8, 15))),
            (1.2f, () => Set(St.Look, 2.4f)),
            (0.6f, () => Set(St.Stretch, 1.6f)),
            (1.4f / act, () => Set(St.Sit, Rand(3, 7))),
            (0.35f * act, () => Set(St.Dance, 3f)),
            (1.5f / act, () => Set(St.Idle, Rand(2, 5))),
            (0.8f * act, () => { targetX = Math.Clamp(cur.X, L, R); speed = 130 * U; goOff = false; Set(St.Walk); }),
            (0.25f * act, Flip),
        };
        float sum = options.Sum(o => o.w), pick = Rand(0, sum);
        foreach (var o in options)
        {
            pick -= o.w;
            if (pick <= 0) { o.a(); return; }
        }
        Set(St.Idle, 2);
    }

    void Tick()
    {
        float now = (float)sw.Elapsed.TotalSeconds;
        float dt = Math.Min(0.05f, now - lastT);
        lastT = now;
        time += dt;
        if (bmp.Width != (int)Math.Ceiling(36 * S.Pixel * U)) ApplySize();

        fsT -= dt;
        if (fsT <= 0)
        {
            fsT = 0.5f;
            Desk.CheckFullscreen();
            bool fs = S.HideInFullscreen && Desk.Fullscreen == Screen.FromPoint(new Point((int)x, (int)y - 10)).Bounds;
            if (fs != fsHidden) { fsHidden = fs; ApplyVisibility(); }
        }
        music.Tick();
        SysAudio.Tick(time, dt);
        AfkTick(dt);
        scripts.Tick(time, RunAction);
        topT -= dt;
        if (topT <= 0) { topT = 1.5f; KeepOnTop(); }
        if (hideUntil > 0 && time >= hideUntil) { hideUntil = -1; SetHidden(false); }
        if (fsHidden) return;

        Step(dt);
        ball?.Step(dt);
        if (!hidden) Render();

        saveT += dt;
        if (saveT > 60) { saveT = 0; S.Save(); }
        timer.Interval = st == St.Sleep ? 70 : 30;
    }

    void Step(float dt)
    {
        stT += dt;
        pawT -= dt;
        kickCd -= dt;
        blinkT -= dt;
        if (blinkT < -0.12f) blinkT = Rand(2, 5);
        glanceT -= dt;
        if (st == St.Idle && glanceT <= 0 && rnd.NextDouble() < dt * 0.4) Glance();
        var cur = Cursor.Position;

        curSpeed = dt > 0 ? MathF.Sqrt((cur.X - lastCur.X) * (cur.X - lastCur.X) + (cur.Y - lastCur.Y) * (cur.Y - lastCur.Y)) / dt : 0;
        lastCur = cur;
        if (Grounded) UpdateSurface(dt);
        if (S.ZeroG && st != St.Drag && st != St.Float) StartFloat(vx, vy);
        else if (!S.ZeroG && st == St.Float) { fSleep = false; StartAir(AirKind.Drop, vx, vy); }
        BallForm.ZeroG = S.ZeroG;
        party = music.On || (S.DanceToMusic && SysAudio.Playing);
        if (music.On) beatPos = music.BeatPos();
        else
        {
            // своя музыка: доли ловим по ударам в звуке, промежуток между ними сглаживаем
            float hit = SysAudio.Beat;
            if (hit > 0.8f && !beatHigh)
            {
                beatHigh = true;
                double gap = time - lastBeatT;
                if (gap > 0.3 && gap < 1.5) beatGap += (gap - beatGap) * 0.3;
                lastBeatT = time;
                beatIdx++;
            }
            else if (hit < 0.4f) beatHigh = false;
            beatPos = beatIdx + Math.Min(0.999, (time - lastBeatT) / beatGap);
        }
        beatCount = (int)beatPos;
        double frac = beatPos - Math.Floor(beatPos);
        nod = (float)((1 - frac) * (1 - frac));
        if (party && st is St.Idle or St.Walk or St.Look or St.Sit or St.Stretch or St.Wave) Set(St.Party);

        float tsx = 1, tsy = 1;

        switch (st)
        {
            case St.Drag:
            {
                float nx = cur.X - grabOff.X;
                dragVX += ((nx - x) / Math.Max(dt, 0.001f) - dragVX) * Math.Min(1, dt * 10);
                x = nx;
                y = cur.Y - grabOff.Y;
                trail.Enqueue((time, cur));
                while (trail.Count > 0 && time - trail.Peek().t > 0.08f) trail.Dequeue();
                if ((MouseButtons & MouseButtons.Left) == 0 && mouseDown) OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 0, 0, 0));
                break;
            }

            case St.Air:
                StepAir(dt);
                break;

            case St.Party:
                if (!party) { Set(St.Idle, 1); break; }
                tsy = 1 - 0.05f * nod;
                tsx = 1 + 0.03f * nod;
                angle = (float)Math.Sin(beatPos * Math.PI) * 4;
                break;

            case St.Float:
                (tsx, tsy) = StepFloat(dt, cur);
                break;

            case St.Idle:
            {
                var lt = LookTarget();
                if (Math.Abs(lt.X - x) > 30 * U) face = lt.X < x ? -1 : 1;
            }
                CheckWave(dt, cur);
                if (stT > stDur) Decide();
                break;

            case St.Walk:
            {
                var (L, R) = Span();
                if (S.Follow) targetX = Math.Clamp(cur.X, L, R);
                float dx = targetX - x;
                if (Math.Abs(dx) < 3 * U) { Set(St.Idle, S.Follow ? 0.6f : Rand(1, 3)); break; }
                face = Math.Sign(dx);
                float nx = x + face * Math.Min(Math.Abs(dx), speed * dt);
                if (onWin != IntPtr.Zero && ((face < 0 && nx <= L) || (face > 0 && nx >= R)))
                {
                    x = face < 0 ? L : R;
                    if (goOff) { teeterOff = rnd.NextDouble() < 0.65; Set(St.Teeter, 1.1f); }
                    else Set(St.Idle, Rand(1, 2));
                    break;
                }
                x = nx;
                if (stT > 25) Set(St.Idle, 1);
                break;
            }

            case St.Run:
                StepChase(dt);
                break;

            case St.Teeter:
                if (stT > stDur)
                {
                    angle = 0;
                    if (teeterOff) StartAir(AirKind.Jump, face * 150 * U, -420 * U);
                    else { face = -face; var (L, R) = Span(); targetX = Rand(L, R); goOff = false; speed = 70 * U; Set(St.Walk); }
                }
                break;

            case St.Crouch:
                tsx = 1.2f; tsy = 0.75f;
                if (stT > stDur) { StartAir(AirKind.Jump, jumpVX, jumpVY); sqX = 0.85f; sqY = 1.25f; }
                break;

            case St.Slide:
            {
                x += vx * dt;
                vx *= (float)Math.Exp(-4 * dt);
                dustT -= dt;
                if (Math.Abs(vx) > 1) face = Math.Sign(vx);
                if (dustT <= 0) { dustT = 0.05f; Dust(1, face); }
                var (L, R) = Span();
                if (onWin != IntPtr.Zero && (x < L || x > R)) { StartAir(AirKind.Drop, vx, 0); break; }
                if (onWin == IntPtr.Zero && (x <= L || x >= R)) { x = Math.Clamp(x, L, R); vx = -vx * 0.3f; sqX = 0.75f; }
                if (Math.Abs(vx) < 60 * U) { vx = 0; Set(St.Idle, Rand(1, 2)); }
                break;
            }

            case St.Code:
                codeT -= dt;
                if (codeT <= 0) { codeT = 0.4f; SpawnGlyph(); }
                if (time - lastKeyT > 0.5f && pawT < -0.03f && rnd.NextDouble() < 0.15) { paw ^= 1; pawT = 0.1f; }
                if (stT > stDur && time - lastKeyT > 3) Set(St.Idle, Rand(1, 3));
                break;

            case St.Sleep:
                snoreT -= dt;
                if (snoreT <= 0)
                {
                    snoreT = 1.3f;
                    parts.Add(new Particle { Kind = 3, Text = rnd.Next(2) == 0 ? "z" : "Z", X = FX + 4 * P, Y = FY - 11 * P, VX = 2.5f * P, VY = -4 * P, Life = 2.2f, Max = 2.2f, C = Color.FromArgb(220, 225, 255) });
                }
                tsy = 1 + (float)Math.Sin(time * 2) * 0.04f;
                break;

            case St.Splat:
                tsx = 1.3f; tsy = 0.65f;
                if (stT > stDur) { Set(St.Dizzy, 1.6f); sqX = 0.8f; sqY = 1.3f; }
                break;

            case St.Dizzy:
                if (stT > stDur) { angle = 0; Set(St.Idle, Rand(1, 2)); }
                break;

            case St.Stretch:
                tsy = 1 + 0.16f * (float)Math.Sin(Math.Clamp(stT / stDur, 0, 1) * Math.PI);
                tsx = 2 - tsy;
                if (stT > stDur) Set(St.Idle, Rand(1, 2));
                break;

            case St.Look:
                if ((int)(stT / 0.6f) % 2 == 1 && (int)((stT - dt) / 0.6f) % 2 == 0) face = -face;
                CheckWave(dt, cur);
                if (stT > stDur) Set(St.Idle, Rand(1, 2));
                break;

            case St.Dance:
                face = (int)(stT / 0.35f) % 2 == 0 ? 1 : -1;
                tsy = 1 - 0.08f * Math.Abs((float)Math.Sin(stT * 9));
                if (stT > stDur) Set(St.Idle, Rand(1, 2));
                break;

            case St.Yawn:
                if (stT > stDur) Set(St.Sleep);
                break;

            case St.Sit:
                if (glanceT <= 0 && rnd.NextDouble() < dt * 0.3) Glance();
                if (stT > stDur) Set(St.Idle, Rand(1, 2.5f));
                break;

            case St.Wave:
            case St.Happy:
            case St.Eat:
            case St.Land:
                if (st == St.Land) { tsx = 1.15f; tsy = 0.85f; }
                if (stT > stDur) Set(St.Idle, Rand(1, 2.5f));
                break;
        }

        if (st is not (St.Float or St.Party) && (st != St.Air || air != AirKind.Flip)) angle *= (float)Math.Exp(-dt * 14);

        sqX += (tsx - sqX) * Math.Min(1, dt * 12);
        sqY += (tsy - sqY) * Math.Min(1, dt * 12);

        for (int i = parts.Count - 1; i >= 0; i--)
        {
            var p = parts[i];
            p.VY += p.G * dt;
            p.X += p.VX * dt;
            p.Y += p.VY * dt;
            p.Life -= dt;
            if (p.Kind == 2 && p.Y > FY - 10 * P)
            {
                parts.RemoveAt(i);
                S.Tokens += 1000;
                if (st == St.Float) fEatT = 1.4f;
                else if (Grounded && !Busy && st != St.Sleep) { Set(St.Eat, 1.4f); sqX = 1.1f; sqY = 0.9f; }
                continue;
            }
            if (p.Life <= 0) parts.RemoveAt(i);
        }
    }

    /// Иногда окна или проводник сбрасывают «поверх всех» — возвращаем его каждые полторы секунды.
    void KeepOnTop()
    {
        const uint flags = 0x0001 | 0x0002 | 0x0010;
        if (Visible) Native.SetWindowPos(Handle, (IntPtr)(-1), 0, 0, 0, 0, flags);
        if (ball != null && !ball.IsDisposed && ball.Visible) Native.SetWindowPos(ball.Handle, (IntPtr)(-1), 0, 0, 0, 0, flags);
    }

    public void ToggleMusic()
    {
        if (music.On) { music.Stop(); afkMusic = false; }
        else StartMusic();
    }

    public void NextTrack()
    {
        if (music.On) music.Next();
        else StartMusic();
    }

    void StartMusic()
    {
        music.Play(S.MusicVolume, L.Russian);
        if (music.On) scripts.Fire(S.Scripts, "music");
    }

    /// Раз в секунду: сколько человек не трогал мышь и клавиатуру, лоу-фай и сценарии по времени.
    void AfkTick(float dt)
    {
        clockT -= dt;
        if (clockT > 0) return;
        clockT = 1;
        double idle = Native.IdleSeconds();
        scripts.Clock(S.Scripts, time, idle / 60);
        bool away = idle >= Math.Max(1, S.AfkMinutes) * 60;
        if (away && !afk)
        {
            afk = true;
            if (S.AfkMusic && Desk.Fullscreen == null && !SysAudio.Playing && !music.On) { StartMusic(); afkMusic = music.On; }
        }
        else if (afk && idle < 2)
        {
            afk = false;
            scripts.Back();
            scripts.Fire(S.Scripts, "back");
            if (afkMusic) { afkMusic = false; music.Stop(); }
        }
    }

    void Hearts()
    {
        for (int i = 0; i < 5; i++)
            parts.Add(new Particle { Kind = 0, X = FX + Rand(-5, 5) * P, Y = FY - 13 * P, VX = Rand(-5, 5) * P, VY = Rand(-15, -8) * P, Life = 1.3f, Max = 1.3f, C = Color.FromArgb(255, 111, 168) });
    }

    void Cheer()
    {
        if (st == St.Float) fHappyT = 1.2f;
        else if (Grounded && !Busy && st != St.Sleep) { Set(St.Happy, 1.2f); sqX = 1.15f; sqY = 0.85f; }
        Hearts();
    }

    void Emote(string id)
    {
        switch (id)
        {
            case "heart": Hearts(); break;
            case "star":
                for (int i = 0; i < 7; i++)
                    parts.Add(new Particle { Kind = 5, X = FX + Rand(-10, 10) * P, Y = FY - Rand(12, 20) * P, VX = Rand(-3, 3) * P, VY = Rand(-6, -2) * P, Life = 1.2f, Max = 1.2f, Size = Rand(1.2f, 2f) * P, C = Color.FromArgb(255, 225, 110) });
                break;
            case "note": Mark("♪", Color.FromArgb(127, 233, 255)); break;
            case "zzz": Mark("zZ", Color.FromArgb(220, 225, 255)); break;
            default: Mark(id ?? "!", Color.White); break;
        }
    }

    /// Выполняет один блок сценария.
    void RunAction(ScriptBlock b)
    {
        bool fl = st == St.Float;
        float num = (float)Math.Max(0, b.Num);
        switch (b.Id)
        {
            case "jump": if (fl) vy -= 450 * U; else if (Grounded && !Busy) StartAir(AirKind.Jump, 0, -650 * U); break;
            case "flip": Flip(); break;
            case "tocursor":
                if (fl) StartAct(FA.ToCursor, WA);
                else if (Grounded && !Busy)
                {
                    var (L, R) = Span();
                    targetX = Math.Clamp(Cursor.Position.X, L, R);
                    speed = 130 * U;
                    goOff = false;
                    Set(St.Walk);
                }
                break;
            case "summon": Summon(); break;
            case "zerog": S.ZeroG = true; S.Save(); break;
            case "gravity": S.ZeroG = false; S.Save(); break;
            case "dance": if (fl) { StartAct(FA.Dance, WA); faDur = num; } else if (Grounded && !Busy) Set(St.Dance, num); break;
            case "code": if (fl) { StartAct(FA.Code, WA); faDur = num; } else if (Grounded && !Busy) Set(St.Code, num); break;
            case "wave": if (fl) StartAct(FA.Wave, WA); else if (Grounded && !Busy) Set(St.Wave, 1.5f); break;
            case "happy": Cheer(); break;
            case "eat": Feed(); break;
            case "sleep": if (fl) fSleep = true; else if (Grounded && !Busy && st != St.Sleep) Set(St.Yawn, 1.3f); break;
            case "wake": fSleep = false; if (st == St.Sleep) WakeUp(); break;
            case "ballon": if (ball == null) ToggleBall(); break;
            case "balloff": if (ball != null) ToggleBall(); break;
            case "skin": if (!string.IsNullOrEmpty(b.Str)) { S.Skin = b.Str; S.Save(); } break;
            case "wear": if (!string.IsNullOrEmpty(b.Str) && !S.Wear.Contains(b.Str)) { S.Wear.Add(b.Str); S.Save(); } break;
            case "takeoff": if (S.Wear.Remove(b.Str)) S.Save(); break;
            case "undress": S.Wear.Clear(); S.Save(); break;
            case "emote": Emote(b.Str); break;
            case "hide": SetHidden(true); hideUntil = time + Math.Max(1, num); break;
            case "musicon": if (!music.On) StartMusic(); break;
            case "musicoff": music.Stop(); afkMusic = false; break;
            case "nexttrack": NextTrack(); break;
        }
    }

    void StartFloat(float vx0, float vy0)
    {
        st = St.Float;
        stT = 0;
        onWin = IntPtr.Zero;
        umbrellaOpen = false;
        vx = vx0;
        vy = vx0 == 0 && vy0 == 0 ? -120 * U : vy0;
        aspin = Math.Clamp(vx0 / U * 0.25f, -500, 500) + Rand(-40, 40);
        StartAct(FA.Hover, WA);
    }

    void Mark(string text, Color c) =>
        parts.Add(new Particle { Kind = 6, Text = text, X = FX + 3 * P, Y = FY - 17 * P, VY = -3 * P, Life = 1.2f, Max = 1.2f, C = c });

    void StartAct(FA a, Rectangle wa)
    {
        fa = a;
        faT = 0;
        fxT = 0;
        hicT = 0.3f;
        faDir = rnd.Next(2) == 0 ? -1 : 1;
        if (a is not (FA.Hover or FA.Nap) && rnd.NextDouble() < 0.4) aspin += Rand(-100, 100);
        faDur = a switch
        {
            FA.Hover => Rand(2, 5), FA.Confused => 2.6f, FA.Dance => Rand(3, 5), FA.Swim => Rand(3, 5),
            FA.SlowSpin => 3f, FA.Stretch => 1.8f, FA.Sit => Rand(3, 6), FA.LieBack => Rand(4, 7),
            FA.UpsideDown => Rand(2.5f, 4), FA.Wave => 1.6f, FA.ToCursor => 5f, FA.Dash => 1.6f,
            FA.WallPlay => 4.5f, FA.PeekTop => 4.5f, FA.Code => Rand(4, 7), FA.Snack => 2.2f,
            FA.Sparkles => 3f, FA.Moonwalk => 3f, FA.Hiccup => 2.4f, FA.Zoomies => 3f, FA.Orbit => 4f,
            FA.TopSpin => 1.6f, FA.Superhero => 4f, FA.Nap => Rand(5, 8), FA.Startled => 0.8f, _ => 99f,
        };
        float mid = (wa.Left + wa.Right) / 2f;
        switch (a)
        {
            case FA.Confused: Mark("?", Color.White); break;
            case FA.Superhero:
                face = x < mid ? 1 : -1;
                faPt = new PointF(face > 0 ? wa.Right - 12 * Px : wa.Left + 12 * Px, Rand(wa.Top + 60 * U, wa.Bottom - 40 * U));
                break;
            case FA.WallPlay: faPt = new PointF(x < mid ? wa.Left : wa.Right, y); break;
            case FA.PeekTop: faPt = new PointF(x, wa.Top + 15 * Px); break;
            case FA.Zoomies: faPt = new PointF(Math.Clamp(x, wa.Left + 160 * U, wa.Right - 160 * U), Math.Clamp(y, wa.Top + 120 * U, wa.Bottom - 40 * U)); break;
        }
    }

    void PickAct(Rectangle wa)
    {
        float m = new[] { 0.55f, 1f, 1.6f }[Math.Clamp(S.Mood, 0, 2)];
        var options = new (FA a, float w)[]
        {
            (FA.Hover, 2f / m), (FA.Confused, 1f), (FA.Dance, 1.2f * m), (FA.Swim, 1.2f), (FA.SlowSpin, 0.8f),
            (FA.Stretch, 0.6f), (FA.Sit, 1.2f / m), (FA.LieBack, 1.2f / m), (FA.UpsideDown, 0.6f), (FA.Wave, 0.7f),
            (FA.ToCursor, 0.8f), (FA.Dash, 0.8f * m), (FA.WallPlay, 0.7f * m), (FA.PeekTop, 0.5f), (FA.Code, 0.8f),
            (FA.Snack, 0.5f), (FA.Sparkles, 0.7f), (FA.Moonwalk, 0.6f), (FA.Hiccup, 0.4f), (FA.Zoomies, 0.6f * m),
            (FA.Orbit, 0.6f), (FA.TopSpin, 0.5f * m), (FA.Superhero, 0.7f * m), (FA.Nap, 0.5f / m),
        };
        float sum = options.Where(o => o.a != fa).Sum(o => o.w), pick = Rand(0, sum);
        foreach (var o in options)
        {
            if (o.a == fa) continue;
            pick -= o.w;
            if (pick <= 0) { StartAct(o.a, wa); return; }
        }
        StartAct(FA.Hover, wa);
    }

    (float sx, float sy) StepFloat(float dt, Point cur)
    {
        var wa = WA;
        float left = wa.Left + 9 * Px, right = wa.Right - 9 * Px, top = wa.Top + 15 * Px, bottom = wa.Bottom - Px;
        float tsx = 1, tsy = 1;
        if (party && !fSleep && fa is not (FA.Startled or FA.Ball or FA.Dance)) StartAct(FA.Dance, wa);
        if (party && fa == FA.Dance) faT = 0;

        float cdx = cur.X - x, cdy = cur.Y - (y - 6 * Px), cd = Math.Max(1, MathF.Sqrt(cdx * cdx + cdy * cdy));
        if (!fSleep && fa != FA.Startled && curSpeed > 2500 * U && cd < 130 * U)
        {
            StartAct(FA.Startled, wa);
            vx -= cdx / cd * 500 * U;
            vy -= cdy / cd * 500 * U;
            aspin += Rand(-220, 220);
            Mark("!", Color.FromArgb(255, 220, 90));
        }
        bool hasBall = ball != null && !ball.IsDisposed;
        if (hasBall && !fSleep && fa is not (FA.Ball or FA.Startled)) StartAct(FA.Ball, wa);
        if (!hasBall && fa == FA.Ball) StartAct(FA.Hover, wa);
        if (S.Follow && !fSleep && fa is not (FA.ToCursor or FA.Startled or FA.Ball)) StartAct(FA.ToCursor, wa);

        faT += dt;
        if (!fSleep && faT > faDur && fa != FA.Ball && !(S.Follow && fa == FA.ToCursor))
        {
            if (fa == FA.TopSpin) fDizzyT = 0.9f;
            PickAct(wa);
        }

        float ax = 0, ay = MathF.Sin(time * 1.7f) * 18 * U, damping = 0.45f;
        bool faceByMotion = true;
        void Toward(PointF t, float force)
        {
            float dx = t.X - x, dy = t.Y - y, d = Math.Max(1, MathF.Sqrt(dx * dx + dy * dy));
            if (d > 15 * U) { ax += dx / d * force; ay += dy / d * force; }
        }
        void FaceCursor() { faceByMotion = false; if (Math.Abs(cdx) > 20 * U) face = cdx < 0 ? -1 : 1; }

        if (fSleep || fa == FA.Nap)
        {
            damping = 1.2f;
            ay = MathF.Sin(time) * 8 * U;
            faceByMotion = false;
        }
        else switch (fa)
        {
            case FA.Hover: damping = 1.5f; FaceCursor(); break;
            case FA.Confused:
                damping = 2f;
                faceByMotion = false;
                face = (int)(faT / 0.55f) % 2 == 0 ? 1 : -1;
                break;
            case FA.Dance: damping = 1.2f; if (party) { tsy = 1 - 0.05f * nod; tsx = 1 + 0.03f * nod; } break;
            case FA.Swim: ax += face * 110 * U; damping = 0.9f; break;
            case FA.SlowSpin: damping = 1.5f; break;
            case FA.Stretch:
                damping = 2f;
                tsy = 1 + 0.15f * MathF.Sin(Math.Clamp(faT / faDur, 0, 1) * MathF.PI);
                tsx = 2 - tsy;
                break;
            case FA.Sit: damping = 2f; FaceCursor(); break;
            case FA.LieBack: damping = 1.5f; faceByMotion = false; break;
            case FA.UpsideDown: damping = 1.5f; FaceCursor(); break;
            case FA.Wave: damping = 2f; FaceCursor(); break;
            case FA.ToCursor: Toward(new PointF(cur.X, cur.Y + 20 * Px), 220 * U); FaceCursor(); break;
            case FA.Dash:
                if (faT < 0.35f) { damping = 6f; tsx = 1.25f; tsy = 0.75f; }
                else
                {
                    if (faT - dt < 0.35f)
                    {
                        float a = Rand(0, MathF.PI * 2);
                        vx = MathF.Cos(a) * 900 * U;
                        vy = MathF.Sin(a) * 600 * U;
                        aspin += Rand(-300, 300);
                    }
                    damping = 0.3f;
                }
                break;
            case FA.WallPlay: Toward(faPt, 260 * U); break;
            case FA.PeekTop: Toward(faPt, 160 * U); break;
            case FA.Code:
                damping = 2f;
                faceByMotion = false;
                fxT -= dt;
                if (fxT <= 0) { fxT = 0.45f; SpawnGlyph(); }
                if (pawT < -0.03f && rnd.NextDouble() < 0.3) { paw ^= 1; pawT = 0.1f; }
                break;
            case FA.Snack:
                damping = 2f;
                fxT -= dt;
                if (fxT <= 0)
                {
                    fxT = 0.3f;
                    parts.Add(new Particle { Kind = 4, X = FX + face * 2 * P, Y = FY - 4 * P, VX = Rand(-4, 4) * P, VY = Rand(-3, 3) * P, Life = 0.9f, Max = 0.9f, Size = 0.6f * P, C = Color.FromArgb(200, 150, 90) });
                }
                break;
            case FA.Sparkles:
                damping = 1.5f;
                fxT -= dt;
                if (fxT <= 0)
                {
                    fxT = 0.25f;
                    parts.Add(new Particle { Kind = 5, X = FX + Rand(-12, 12) * P, Y = FY - Rand(12, 22) * P, VX = Rand(-2, 2) * P, VY = Rand(-2, 2) * P, Life = 1.1f, Max = 1.1f, Size = Rand(1.2f, 2f) * P, C = Color.FromArgb(255, 225, 110) });
                }
                break;
            case FA.Moonwalk: ax += -face * 70 * U; damping = 1.5f; faceByMotion = false; break;
            case FA.Hiccup:
                damping = 1.5f;
                hicT -= dt;
                if (hicT <= 0) { hicT = 0.6f; vy -= 160 * U; sqX = 1.25f; sqY = 0.8f; squintT = 0.15f; }
                break;
            case FA.Zoomies:
                Toward(new PointF(faPt.X + MathF.Cos(faT * 4) * 110 * U, faPt.Y + MathF.Sin(faT * 4) * 70 * U), 600 * U);
                damping = 1.2f;
                break;
            case FA.Orbit:
                Toward(new PointF(cur.X + MathF.Cos(faT * 2.2f) * 130 * U, cur.Y + MathF.Sin(faT * 2.2f) * 90 * U), 400 * U);
                damping = 1.2f;
                break;
            case FA.TopSpin:
                damping = 2.5f;
                faceByMotion = false;
                face = (int)(faT / 0.1f) % 2 == 0 ? 1 : -1;
                break;
            case FA.Superhero:
                Toward(faPt, 500 * U);
                damping = 0.8f;
                if (Math.Abs(faPt.X - x) < 30 * U) faT = faDur;
                break;
            case FA.Startled: damping = 3f; faceByMotion = false; break;
            case FA.Ball:
                if (!ball.Held) Toward(new PointF(ball.X, ball.Y + 7 * Px), 160 * U);
                else FaceCursor();
                break;
        }

        vx += ax * dt;
        vy += ay * dt;
        float damp = MathF.Exp(-damping * dt);
        vx *= damp;
        vy *= damp;
        x += vx * dt;
        y += vy * dt;

        float hit = 0;
        if (x < left) { x = left; hit = Math.Abs(vx); vx = Math.Abs(vx) * 0.75f; sqX = 0.8f; sqY = 1.15f; }
        if (x > right) { x = right; hit = Math.Abs(vx); vx = -Math.Abs(vx) * 0.75f; sqX = 0.8f; sqY = 1.15f; }
        if (y < top) { y = top; hit = Math.Max(hit, Math.Abs(vy)); vy = Math.Abs(vy) * 0.75f; sqX = 1.15f; sqY = 0.8f; }
        if (y > bottom) { y = bottom; hit = Math.Max(hit, Math.Abs(vy)); vy = -Math.Abs(vy) * 0.75f; sqX = 1.15f; sqY = 0.8f; }
        if (hit > 90 * U && bonkT <= 0.1f)
        {
            bonkT = Math.Min(0.8f, 0.3f + hit / (3000 * U));
            scripts.Fire(S.Scripts, "bonk");
            aspin += (rnd.Next(2) == 0 ? -1 : 1) * Math.Min(320, hit / U * 0.25f);
            if (fa == FA.WallPlay)
            {
                fHappyT = 0.6f;
                faPt = new PointF(x < (left + right) / 2 ? wa.Right : wa.Left, y);
            }
        }
        if (faceByMotion && Math.Abs(vx) > 25 * U) face = Math.Sign(vx);

        if (hasBall && !ball.Held && kickCd <= 0)
        {
            float bx = ball.X - x, by = ball.Y - (y - 6 * Px), bd = MathF.Sqrt(bx * bx + by * by);
            if (bd < 12 * Px)
            {
                bd = Math.Max(1, bd);
                ball.VX = bx / bd * Rand(400, 700) * U;
                ball.VY = by / bd * Rand(400, 700) * U;
                kickCd = 0.6f;
                scripts.Fire(S.Scripts, "kick");
                fHappyT = 0.4f;
            }
        }

        bonkT -= dt;
        fHappyT -= dt;
        fEatT -= dt;
        fDizzyT -= dt;
        squintT -= dt;
        if (fa == FA.SlowSpin) aspin += (faDir * 140 - aspin) * Math.Min(1, dt * 1.5f);
        if (fa == FA.Hover && !fSleep) aspin *= MathF.Exp(-1.1f * dt);
        aspin *= MathF.Exp(-0.06f * dt);
        angle = (angle + aspin * dt) % 360;

        if (S.SleepWhenIdle && !fSleep && time - lastInteract > 300 && time - lastKeyT > 300) fSleep = true;
        if (fSleep || fa == FA.Nap)
        {
            snoreT -= dt;
            if (snoreT <= 0)
            {
                snoreT = 1.3f;
                parts.Add(new Particle { Kind = 3, Text = rnd.Next(2) == 0 ? "z" : "Z", X = FX + 4 * P, Y = FY - 11 * P, VX = 2.5f * P, VY = -4 * P, Life = 2.2f, Max = 2.2f, C = Color.FromArgb(220, 225, 255) });
            }
        }
        return (tsx, tsy);
    }

    bool UpdateSurface(float dt)
    {
        if (onWin != IntPtr.Zero)
        {
            if (!S.Windows || !Native.IsWindow(onWin) || !Native.IsWindowVisible(onWin) || Native.IsIconic(onWin) ||
                Native.IsZoomed(onWin) || !Desk.GetRect(onWin, out var r))
            {
                StartAir(AirKind.Drop, 0, 0);
                return false;
            }
            if (r != winR)
            {
                x += r.Left - winR.Left;
                y = r.Top;
                winR = r;
            }
            occT -= dt;
            if (occT <= 0)
            {
                occT = 0.3f;
                Desk.Scan();
                if (!Desk.Standable(onWin, x)) { StartAir(AirKind.Drop, 0, 0); return false; }
            }
            var (L, R) = Span();
            if ((x < L - 1 || x > R + 1) && st is not (St.Teeter or St.Slide))
            {
                StartAir(AirKind.Drop, face * 90 * U, -100 * U);
                return false;
            }
        }
        else
        {
            var wa = WA;
            y = wa.Bottom;
            var (L, R) = Span();
            x = Math.Clamp(x, L, R);
        }
        return true;
    }

    void StepAir(float dt)
    {
        airT += dt;
        float prevY = y;
        var wa = WA;

        if (umbrellaWanted && !umbrellaOpen && airT > 0.35f && vy > 200 * U) { umbrellaOpen = true; spin = 0; }
        if (umbrellaOpen)
        {
            vy += (130 * U - vy) * Math.Min(1, dt * 5);
            vx *= (float)Math.Exp(-2 * dt);
            x += (float)Math.Sin(airT * 3) * 40 * U * dt;
        }
        else
        {
            vy += G * U * dt;
            angle += spin * dt;
        }
        x += vx * dt;
        y += vy * dt;

        float side = 9 * Px;
        if (x < wa.Left + side) { x = wa.Left + side; if (vx < 0) { vx = -vx * 0.55f; spin = -spin * 0.6f; sqX = 0.7f; sqY = 1.2f; } }
        if (x > wa.Right - side) { x = wa.Right - side; if (vx > 0) { vx = -vx * 0.55f; spin = -spin * 0.6f; sqX = 0.7f; sqY = 1.2f; } }
        float headroom = 14 * Px;
        if (y - headroom < wa.Top && vy < 0) { y = wa.Top + headroom; vy = -vy * 0.35f; sqX = 1.2f; sqY = 0.8f; }

        if (vy <= 0) return;

        Desk.Scan();
        float floor = wa.Bottom;
        float landY = float.MaxValue;
        IntPtr landW = IntPtr.Zero;
        if (S.Windows && Desk.TryLand(x, prevY, y, 5 * U, out var t, out var h)) { landY = t; landW = h; }
        if (y >= floor && floor < landY) { landY = floor; landW = IntPtr.Zero; }
        if (landY != float.MaxValue) Land(landY, landW);
    }

    void Land(float top, IntPtr h)
    {
        scripts.Fire(S.Scripts, "land");
        y = top;
        onWin = h;
        if (h != IntPtr.Zero) Desk.GetRect(h, out winR);
        float impact = vy, hv = Math.Abs(vx);
        float a = ((angle % 360) + 540) % 360 - 180;
        angle = a;
        spin = 0;
        vy = 0;
        bool tumbled = Math.Abs(a) > 60;

        if (umbrellaOpen)
        {
            umbrellaOpen = false;
            sqX = 1.15f; sqY = 0.85f; vx = 0;
            Dust(4);
            Set(St.Idle, Rand(1, 2));
        }
        else if (air == AirKind.Flip)
        {
            sqX = 1.25f; sqY = 0.8f; vx = 0;
            Dust(5);
            Set(St.Happy, 0.6f);
        }
        else if (impact > 1900 * U || (tumbled && impact > 900 * U))
        {
            sqX = 1.3f; sqY = 0.65f; vx = 0;
            Dust(14);
            Set(St.Splat, 1f);
            scripts.Fire(S.Scripts, "bonk");
        }
        else if (hv > 650 * U)
        {
            vx *= 0.8f;
            face = Math.Sign(vx);
            Dust(8, face);
            Set(St.Slide);
        }
        else if (impact > 1000 * U)
        {
            sqX = 1.4f; sqY = 0.6f; vx = 0;
            Dust(9);
            Set(St.Land, 0.45f);
        }
        else
        {
            sqX = 1.2f; sqY = 0.82f; vx = 0;
            Dust(4);
            Set(St.Idle, Rand(0.8f, 2));
        }
    }

    void StepChase(float dt)
    {
        if (ball == null || ball.IsDisposed) { Set(St.Idle, 1); return; }
        if (ball.Held) { face = ball.X < x ? -1 : 1; return; }
        float dx = ball.X - x;
        face = dx < 0 ? -1 : 1;
        float ballBottom = ball.Y + ball.Rad;
        bool sameLevel = Math.Abs(ballBottom - y) < 45 * U;

        if (!sameLevel && ball.Grounded && ballBottom < y - 45 * U && Math.Abs(dx) < 450 * U && S.Windows)
        {
            TryJumpTo(ball.X - face * 20 * U, ballBottom);
            return;
        }
        var (sl, sr) = Span();
        bool cornered = onWin == IntPtr.Zero && (ball.X < sl || ball.X > sr) &&
                        Math.Abs(Math.Clamp(ball.X, sl, sr) - x) < 2 * U && Math.Abs(dx) < 14 * Px;
        if (Math.Abs(dx) > 4 * Px && !cornered)
        {
            float nx = x + face * Math.Min(Math.Abs(dx), 190 * U * dt);
            if (onWin != IntPtr.Zero && (nx < sl || nx > sr)) { x = nx; StartAir(AirKind.Jump, face * 160 * U, -380 * U); return; }
            x = nx;
        }
        else if (kickCd <= 0 && ballBottom > y - 12 * Px)
        {
            int dir = cornered ? (ball.X < sl ? 1 : -1) : face;
            ball.VX = dir * Rand(350, 750) * U;
            ball.VY = -Rand(cornered ? 700 : 450, 900) * U;
            kickCd = 0.7f;
            scripts.Fire(S.Scripts, "kick");
            sqX = 0.85f; sqY = 1.2f;
        }
        if (stT > 25) Set(St.Idle, 3);
    }

    PointF glance;
    float glanceT;

    void Glance()
    {
        glance = new PointF(x + Rand(-300, 300) * U, y - Rand(-20, 260) * U);
        glanceT = Rand(1.2f, 2.5f);
    }

    PointF LookTarget()
    {
        if (ball != null && !ball.IsDisposed) return new PointF(ball.X, ball.Y);
        if (st == St.Walk) return new PointF(targetX, y - 7 * Px);
        if (st == St.Air && air == AirKind.Jump) return new PointF(x + vx, y);
        if (glanceT > 0) return glance;
        return Cursor.Position;
    }

    void CheckWave(float dt, Point cur)
    {
        float dx = cur.X - x, dy = cur.Y - (y - 7 * Px);
        if (dx * dx + dy * dy < (110 * U) * (110 * U)) nearT += dt;
        else { nearT = 0; waved = false; }
        if (nearT > 1.2f && !waved) { waved = true; face = dx < 0 ? -1 : 1; Set(St.Wave, 1.5f); }
    }

    void BuildPose()
    {
        pose.Reset(time);
        var t = LookTarget();
        float eyeY = y - 7 * Px;
        int lx = t.X > x + 30 * U ? 1 : t.X < x - 30 * U ? -1 : 0;
        pose.LookX = lx * face;
        pose.LookY = t.Y < eyeY - 60 * U ? -1 : t.Y > eyeY + 60 * U ? 1 : 0;
        if (blinkT < 0) pose.Eyes = Eyes.Blink;

        switch (st)
        {
            case St.Walk: pose.Legs = Legs.Walk; pose.Arms = Arms.Swing; break;
            case St.Run:
                if (ball != null && ball.Held) break;
                pose.Legs = Legs.Run; pose.Arms = Arms.Swing; break;
            case St.Code:
                pose.Sit = true; pose.Laptop = true; pose.Arms = Arms.Typing;
                pose.LaptopLit = pawT > 0 || time - lastKeyT < 0.3f;
                pose.Paw = pawT > 0 ? paw : -1;
                pose.LookX = 0; pose.LookY = 1;
                break;
            case St.Sleep: pose.Sit = true; pose.Eyes = Eyes.Closed; break;
            case St.Drag: pose.Legs = Legs.Dangle; pose.Arms = Arms.Flail; pose.Eyes = Eyes.Wide; pose.Mouth = Mouth.O; break;
            case St.Air:
                if (air == AirKind.Flip) { pose.Legs = Legs.Tuck; pose.Arms = Arms.Up; pose.Eyes = Eyes.Happy; }
                else if (air == AirKind.Jump) { pose.Legs = Legs.Tuck; pose.Arms = Arms.Up; if (vy > 0) pose.Eyes = Eyes.Wide; }
                else if (umbrellaOpen) { pose.Arms = Arms.Umbrella; pose.Umbrella = true; pose.Eyes = Eyes.Happy; pose.Legs = Legs.Stand; }
                else { pose.Legs = Legs.Dangle; pose.Arms = Arms.Flail; pose.Eyes = Eyes.Wide; pose.Mouth = Mouth.Open; }
                break;
            case St.Land: pose.Legs = Legs.Brace; pose.Eyes = Eyes.Closed; pose.Mouth = Mouth.O; break;
            case St.Splat: pose.Legs = Legs.Hidden; pose.Arms = Arms.Out; pose.Eyes = Eyes.Squint; break;
            case St.Dizzy: pose.Eyes = Eyes.Closed; break;
            case St.Slide: pose.Legs = Legs.Skid; pose.Arms = Arms.Out; pose.Eyes = Eyes.Wide; pose.Mouth = Mouth.O; break;
            case St.Happy: pose.Arms = Arms.Up; pose.Eyes = Eyes.Happy; pose.Blush = true; pose.Mouth = Mouth.Smile; break;
            case St.Eat: pose.Eyes = Eyes.Happy; pose.Blush = true; pose.Mouth = Mouth.Chew; break;
            case St.Crouch: pose.Legs = Legs.Brace; break;
            case St.Teeter: pose.Legs = Legs.Brace; pose.Arms = Arms.Out; pose.Eyes = Eyes.Wide; pose.Mouth = Mouth.O; break;
            case St.Stretch:
                pose.Arms = Arms.Up; pose.Eyes = Eyes.Closed;
                if (stT > 0.35f && stT < 1.2f) pose.Mouth = Mouth.Yawn;
                break;
            case St.Yawn: pose.Eyes = Eyes.Closed; pose.Mouth = Mouth.Yawn; break;
            case St.Look: pose.LookX = 1; if (pose.Eyes != Eyes.Blink) pose.Eyes = Eyes.Open; break;
            case St.Sit: pose.Sit = true; if ((int)stT % 5 == 4) pose.Eyes = Eyes.Happy; break;
            case St.Dance:
                pose.Arms = (int)(stT * 3) % 2 == 0 ? Arms.Up : Arms.Swing;
                pose.Legs = Legs.Run; pose.Eyes = Eyes.Happy; pose.Mouth = Mouth.Smile;
                break;
            case St.Wave: pose.Arms = Arms.Wave; pose.Eyes = Eyes.Happy; pose.Mouth = Mouth.Smile; break;
            case St.Party:
                // спокойный лоу-фай вайб: кивает с закрытыми глазами, иногда улыбается, каждые 16 долей садится
                pose.Sit = beatCount / 16 % 2 == 1;
                pose.Eyes = beatCount % 8 == 7 ? Eyes.Happy : Eyes.Closed;
                if (beatCount % 32 == 31) pose.Arms = Arms.Up;
                break;
            case St.Float:
                pose.Legs = Legs.Dangle;
                if (bonkT > 0 || squintT > 0) { pose.Eyes = Eyes.Squint; pose.Arms = Arms.Out; }
                else if (fSleep || fa == FA.Nap) { pose.Eyes = Eyes.Closed; pose.Legs = Legs.Tuck; }
                else if ((S.Bongo && time - lastKeyT < 2.5f) || fa == FA.Code)
                {
                    pose.Sit = true; pose.Laptop = true; pose.Arms = Arms.Typing;
                    pose.LaptopLit = pawT > 0 || time - lastKeyT < 0.3f;
                    pose.Paw = pawT > 0 ? paw : -1;
                    pose.LookX = 0; pose.LookY = 1;
                }
                else if (fEatT > 0 || fa == FA.Snack) { pose.Eyes = Eyes.Happy; pose.Blush = true; pose.Mouth = Mouth.Chew; }
                else if (fHappyT > 0) { pose.Arms = Arms.Up; pose.Eyes = Eyes.Happy; pose.Blush = true; pose.Legs = Legs.Tuck; }
                else if (fDizzyT > 0) { pose.Eyes = Eyes.Closed; pose.Mouth = Mouth.O; }
                else switch (fa)
                {
                    case FA.Hover: if (Math.Abs(aspin) > 25) { pose.Arms = Arms.Out; pose.Mouth = Mouth.O; } break;
                    case FA.Confused: pose.Mouth = Mouth.O; pose.LookX = 1; pose.Eyes = blinkT < 0 ? Eyes.Blink : Eyes.Open; break;
                    case FA.Dance:
                        if (party) { pose.Eyes = beatCount % 8 == 7 ? Eyes.Happy : Eyes.Closed; break; }
                        pose.Arms = ((int)(time * 2.5f) % 4) switch { 0 => Arms.Up, 1 => Arms.Wave, 2 => Arms.Up, _ => Arms.Swing };
                        if ((int)(time * 0.5f) % 3 == 0) pose.Eyes = Eyes.Happy;
                        break;
                    case FA.Swim: pose.Arms = (int)(faT * 2.4f) % 2 == 0 ? Arms.Up : Arms.Out; pose.Legs = Legs.Walk; break;
                    case FA.SlowSpin: pose.Arms = Arms.Out; pose.Legs = Legs.Tuck; pose.Eyes = Eyes.Happy; break;
                    case FA.Stretch:
                        pose.Arms = Arms.Up; pose.Eyes = Eyes.Closed;
                        if (faT > 0.4f && faT < 1.3f) pose.Mouth = Mouth.Yawn;
                        break;
                    case FA.Sit: pose.Sit = true; break;
                    case FA.LieBack: pose.Arms = Arms.Up; pose.Legs = Legs.Tuck; pose.Eyes = (int)faT % 3 == 2 ? Eyes.Happy : Eyes.Closed; break;
                    case FA.UpsideDown: pose.Legs = Legs.Stand; pose.Mouth = Mouth.O; break;
                    case FA.Wave: pose.Arms = Arms.Wave; pose.Eyes = Eyes.Happy; break;
                    case FA.ToCursor: pose.Arms = Arms.Up; pose.Legs = Legs.Tuck; pose.Eyes = Eyes.Happy; break;
                    case FA.Dash:
                        if (faT < 0.35f) { pose.Legs = Legs.Brace; pose.Eyes = Eyes.Squint; }
                        else { pose.Arms = Arms.Up; pose.Legs = Legs.Tuck; pose.Eyes = Eyes.Wide; }
                        break;
                    case FA.WallPlay: pose.Arms = Arms.Out; pose.Legs = Legs.Tuck; pose.Eyes = Eyes.Happy; break;
                    case FA.PeekTop:
                        bool atTop = Math.Abs(y - faPt.Y) < 30 * U;
                        pose.LookY = atTop ? 1 : -1;
                        if (atTop) pose.Arms = Arms.Up;
                        break;
                    case FA.Sparkles:
                        pose.Arms = (int)(faT * 3) % 2 == 0 ? Arms.Up : Arms.Wave;
                        pose.Eyes = Eyes.Happy; pose.LookY = -1;
                        break;
                    case FA.Moonwalk: pose.Legs = Legs.Walk; pose.Arms = Arms.Swing; pose.LookX = -1; break;
                    case FA.Hiccup: pose.Mouth = Mouth.O; break;
                    case FA.Zoomies: pose.Arms = Arms.Up; pose.Legs = Legs.Tuck; pose.Eyes = Eyes.Happy; break;
                    case FA.Orbit: pose.Arms = Arms.Out; pose.Eyes = Eyes.Happy; break;
                    case FA.TopSpin: pose.Arms = Arms.Out; pose.Eyes = Eyes.Closed; break;
                    case FA.Superhero: pose.Arms = Arms.Up; pose.Legs = Legs.Tuck; pose.LookX = 1; pose.LookY = 0; break;
                    case FA.Startled: pose.Eyes = Eyes.Wide; pose.Arms = Arms.Flail; pose.Mouth = Mouth.O; break;
                    case FA.Ball: pose.Arms = Arms.Out; break;
                }
                break;
        }
    }

    readonly Dictionary<int, Font> fonts = new();
    static readonly string[] Heart = { ".X.X.", "XXXXX", "XXXXX", ".XXX.", "..X.." };

    void Render()
    {
        var skin = Looks.SkinById(S.Skin);
        using var g = Graphics.FromImage(bmp);
        g.Clear(Color.Transparent);
        g.ScaleTransform(U, U);
        g.SmoothingMode = SmoothingMode.None;
        g.PixelOffsetMode = PixelOffsetMode.Half;
        g.TextRenderingHint = TextRenderingHint.AntiAlias;

        float hop = st == St.Happy ? -Math.Abs((float)Math.Sin(stT * 10)) * 2 * P : 0;

        if (Grounded)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using var sh = new SolidBrush(Color.FromArgb(45, 0, 0, 0));
            g.FillEllipse(sh, FX - 8 * P * sqX, FY - 0.5f * P, 16 * P * sqX, P);
            g.SmoothingMode = SmoothingMode.None;
        }

        BuildPose();
        Sprite.Draw(g, pose, skin, S.Wear, FX, FY + hop, P, face, sqX, sqY, angle);

        if (st == St.Dizzy || st == St.Splat)
        {
            using var yb = new SolidBrush(Color.FromArgb(255, 220, 90));
            float baseY = st == St.Splat ? FY - 5 * P : FY - 14 * P;
            for (int i = 0; i < 3; i++)
            {
                double a = time * 5 + i * Math.PI * 2 / 3;
                float sx = FX + (float)Math.Cos(a) * 7 * P, sy = baseY + (float)Math.Sin(a) * 1.3f * P;
                g.FillRectangle(yb, sx - 0.3f * P, sy - P, 0.6f * P, 2 * P);
                g.FillRectangle(yb, sx - P, sy - 0.3f * P, 2 * P, 0.6f * P);
            }
        }

        foreach (var p in parts) DrawParticle(g, p, skin);

        Present(bmp, (int)Math.Round(x - FX * U), (int)Math.Round(y - FY * U));
    }

    void DrawParticle(Graphics g, Particle p, Skin skin)
    {
        int alpha = (int)(255 * Math.Clamp(p.Life / p.Max * 1.6f, 0, 1));
        switch (p.Kind)
        {
            case 0:
            {
                float c3 = P * 0.6f;
                using var hb = new SolidBrush(Color.FromArgb(alpha, p.C));
                using var ob = new SolidBrush(Color.FromArgb(alpha, skin.Outline));
                for (int r = 0; r < 5; r++) for (int c = 0; c < 5; c++)
                    if (Heart[r][c] == 'X') g.FillRectangle(ob, p.X + c * c3 - c3 / 3, p.Y + r * c3 - c3 / 3, c3 * 5 / 3, c3 * 5 / 3);
                for (int r = 0; r < 5; r++) for (int c = 0; c < 5; c++)
                    if (Heart[r][c] == 'X') g.FillRectangle(hb, p.X + c * c3, p.Y + r * c3, c3, c3);
                break;
            }
            case 1:
            case 3:
            {
                float size = (p.Kind == 3 ? 1.5f + (1 - p.Life / p.Max) : 1.5f) * P;
                int key = (int)Math.Round(size);
                if (!fonts.TryGetValue(key, out var f)) fonts[key] = f = new Font("Consolas", key, FontStyle.Bold, GraphicsUnit.Pixel);
                using var shb = new SolidBrush(Color.FromArgb(alpha, skin.Outline));
                using var tb = new SolidBrush(Color.FromArgb(alpha, p.C));
                g.DrawString(p.Text, f, shb, p.X + 1, p.Y + 1);
                g.DrawString(p.Text, f, tb, p.X, p.Y);
                break;
            }
            case 2:
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using var cb = new SolidBrush(Color.FromArgb(255, 205, 60));
                using var cp = new Pen(skin.Outline, 2);
                float w = 2.4f * P * Math.Abs((float)Math.Cos(time * 8)) + 3;
                g.FillEllipse(cb, p.X - w / 2, p.Y - 1.2f * P, w, 2.4f * P);
                g.DrawEllipse(cp, p.X - w / 2, p.Y - 1.2f * P, w, 2.4f * P);
                g.SmoothingMode = SmoothingMode.None;
                break;
            }
            case 4:
            {
                using var db = new SolidBrush(Color.FromArgb((int)(alpha * 0.8f), p.C));
                g.FillRectangle(db, p.X - p.Size / 2, p.Y - p.Size / 2, p.Size, p.Size);
                break;
            }
            case 5:
            {
                using var sb = new SolidBrush(Color.FromArgb(alpha, p.C));
                float a = p.Size, b = p.Size * 0.3f;
                g.FillRectangle(sb, p.X - b / 2, p.Y - a / 2, b, a);
                g.FillRectangle(sb, p.X - a / 2, p.Y - b / 2, a, b);
                break;
            }
            case 6:
            {
                using var f = new Font("Segoe UI Symbol", 2.4f * P, FontStyle.Bold, GraphicsUnit.Pixel);
                using var shb = new SolidBrush(Color.FromArgb(alpha, skin.Outline));
                using var tb = new SolidBrush(Color.FromArgb(alpha, p.C));
                g.DrawString(p.Text, f, shb, p.X + 1.5f, p.Y + 1.5f);
                g.DrawString(p.Text, f, tb, p.X, p.Y);
                break;
            }
        }
    }
}

class BallForm : LayeredForm
{
    public float X, Y, VX, VY;
    public bool Held { get; private set; }
    public bool Grounded { get; private set; }
    public static bool ZeroG;
    public float Rad => R * U;
    public event Action Removed, Thrown;

    readonly float U, R = 13;
    readonly Bitmap bmp;
    readonly Stopwatch sw = Stopwatch.StartNew();
    readonly Queue<(float t, Point p)> trail = new();
    float rot;
    PointF off;

    public BallForm(float u, float x, float y)
    {
        U = u; X = x; Y = y;
        bmp = new Bitmap((int)Math.Ceiling(32 * U), (int)Math.Ceiling(32 * U), PixelFormat.Format32bppArgb);
        Text = "Мячик";
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Desk.Own.Add(Handle);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            Held = true;
            var c = Cursor.Position;
            off = new PointF(c.X - X, c.Y - Y);
            trail.Clear();
        }
        else if (e.Button == MouseButtons.Right) Close();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left || !Held) return;
        Held = false;
        VX = VY = 0;
        if (trail.Count >= 2)
        {
            var a = trail.Peek();
            var b = trail.Last();
            float dt = Math.Max(0.016f, b.t - a.t);
            VX = Math.Clamp((b.p.X - a.p.X) / dt, -3000 * U, 3000 * U);
            VY = Math.Clamp((b.p.Y - a.p.Y) / dt, -3000 * U, 3000 * U);
        }
        Thrown?.Invoke();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        Desk.Own.Remove(Handle);
        Removed?.Invoke();
        base.OnFormClosed(e);
    }

    public void Step(float dt)
    {
        if (IsDisposed) return;
        float rad = Rad;
        var wa = Desk.AreaAt(new Point((int)X, (int)Y));
        if (Held)
        {
            var c = Cursor.Position;
            X = c.X - off.X;
            Y = c.Y - off.Y;
            float t = (float)sw.Elapsed.TotalSeconds;
            trail.Enqueue((t, c));
            while (trail.Count > 0 && t - trail.Peek().t > 0.08f) trail.Dequeue();
            if ((MouseButtons & MouseButtons.Left) == 0) OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 0, 0, 0));
            Grounded = false;
        }
        else if (ZeroG)
        {
            X += VX * dt;
            Y += VY * dt;
            float damp = MathF.Exp(-0.25f * dt);
            VX *= damp;
            VY *= damp;
            if (Y + rad > wa.Bottom) { Y = wa.Bottom - rad; VY = -Math.Abs(VY) * 0.8f; }
            if (Y - rad < wa.Top) { Y = wa.Top + rad; VY = Math.Abs(VY) * 0.8f; }
            if (X - rad < wa.Left) { X = wa.Left + rad; VX = Math.Abs(VX) * 0.8f; }
            if (X + rad > wa.Right) { X = wa.Right - rad; VX = -Math.Abs(VX) * 0.8f; }
            Grounded = false;
            rot += VX * dt / rad;
        }
        else
        {
            float prevBottom = Y + rad;
            VY += 2000 * U * dt;
            X += VX * dt;
            Y += VY * dt;
            Grounded = false;

            float landTop = float.MaxValue;
            if (VY >= 0)
            {
                Desk.Scan();
                if (Desk.TryLand(X, prevBottom, Y + rad, 2 * U, out var top, out _)) landTop = top;
                if (Y + rad >= wa.Bottom && wa.Bottom < landTop) landTop = wa.Bottom;
            }
            if (landTop != float.MaxValue)
            {
                Y = landTop - rad;
                if (VY > 0) VY = -VY * 0.55f;
                if (Math.Abs(VY) < 140 * U) VY = 0;
                Grounded = true;
            }
            if (Y - rad < wa.Top) { Y = wa.Top + rad; VY = Math.Abs(VY) * 0.5f; }
            if (X - rad < wa.Left) { X = wa.Left + rad; VX = Math.Abs(VX) * 0.7f; }
            if (X + rad > wa.Right) { X = wa.Right - rad; VX = -Math.Abs(VX) * 0.7f; }
            if (Grounded)
            {
                VX *= 1 - Math.Min(1, 1.6f * dt);
                if (Math.Abs(VX) < 5 * U) VX = 0;
            }
            rot += VX * dt / rad;
        }
        if (Visible) Render();
    }

    void Render()
    {
        using var g = Graphics.FromImage(bmp);
        g.Clear(Color.Transparent);
        g.ScaleTransform(U, U);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var rect = new RectangleF(16 - R, 16 - R, R * 2, R * 2);
        using (var w = new SolidBrush(Color.FromArgb(255, 250, 244))) g.FillEllipse(w, rect);
        Color[] cols = { Color.FromArgb(217, 119, 87), Color.FromArgb(127, 233, 255), Color.FromArgb(255, 205, 60) };
        float deg = rot * 180 / (float)Math.PI;
        for (int i = 0; i < 3; i++)
        {
            using var b = new SolidBrush(cols[i]);
            g.FillPie(b, rect.X, rect.Y, rect.Width, rect.Height, deg + i * 120, 60);
        }
        using (var p = new Pen(Color.FromArgb(42, 24, 18), 2)) g.DrawEllipse(p, rect);
        using (var hl = new SolidBrush(Color.FromArgb(120, 255, 255, 255))) g.FillEllipse(hl, 16 - R * 0.55f, 16 - R * 0.7f, R * 0.6f, R * 0.4f);
        Present(bmp, (int)Math.Round(X - 16 * U), (int)Math.Round(Y - 16 * U));
    }
}
