using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Microsoft.Win32;

namespace Vibling;

static class Program
{
    const string MutexName = "Vibling.SingleInstance";
    const string OpenSettingsEvent = "Vibling.OpenSettings";
    static EventWaitHandle openSettings;

    [STAThread]
    static void Main(string[] args)
    {
        // после обновления новая версия ждёт, пока закроется старая
        var mutex = new Mutex(false, MutexName);
        bool first;
        try { first = mutex.WaitOne(args.Contains("--updated") ? 15000 : 0); }
        catch (AbandonedMutexException) { first = true; }
        if (!first)
        {
            // Питомец уже на экране: повторный запуск открывает его настройки.
            try { using var ev = EventWaitHandle.OpenExisting(OpenSettingsEvent); ev.Set(); } catch { }
            return;
        }
        openSettings = new EventWaitHandle(false, EventResetMode.AutoReset, OpenSettingsEvent);

        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        Updater.Cleanup();
        Fluent.Init();
        var settings = Settings.Load();
        L.Set(settings.Language);
        Autostart.Refresh();
        var pet = new PetForm(settings);

        new Thread(() =>
        {
            while (openSettings.WaitOne())
            {
                try { pet.BeginInvoke(pet.ShowSettings); } catch { break; }
            }
        }) { IsBackground = true }.Start();

        if (args.Contains("--settings") || !settings.FirstRunDone)
            pet.Shown += (_, _) => pet.BeginInvoke(pet.ShowSettings);

        Application.Run(pet);
        GC.KeepAlive(mutex);
    }
}

static class Native
{
    [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; public POINT(int x, int y) { X = x; Y = y; } }
    [StructLayout(LayoutKind.Sequential)] public struct SIZE { public int CX, CY; public SIZE(int x, int y) { CX = x; CY = y; } }
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }
    [StructLayout(LayoutKind.Sequential, Pack = 1)] public struct BLENDFUNCTION { public byte BlendOp, BlendFlags, SourceConstantAlpha, AlphaFormat; }

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst, ref POINT pptDst, ref SIZE psize, IntPtr hdcSrc, ref POINT pprSrc, int crKey, ref BLENDFUNCTION pblend, int dwFlags);
    [DllImport("user32.dll")] public static extern IntPtr GetDC(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("gdi32.dll")] public static extern IntPtr CreateCompatibleDC(IntPtr hDC);
    [DllImport("gdi32.dll")] public static extern bool DeleteDC(IntPtr hdc);
    [DllImport("gdi32.dll")] public static extern IntPtr SelectObject(IntPtr hDC, IntPtr hObject);
    [DllImport("gdi32.dll")] public static extern bool DeleteObject(IntPtr hObject);

    public delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll", SetLastError = true)] public static extern IntPtr SetWindowsHookEx(int idHook, HookProc fn, IntPtr hMod, uint threadId);
    [DllImport("user32.dll")] public static extern bool UnhookWindowsHookEx(IntPtr hhk);
    [DllImport("user32.dll")] public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr GetModuleHandle(string name);

    public delegate bool EnumWindowsProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc cb, IntPtr l);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr h);
    [DllImport("user32.dll")] public static extern bool IsZoomed(IntPtr h);
    [DllImport("user32.dll")] public static extern bool IsWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern int GetWindowLong(IntPtr h, int idx);
    [DllImport("user32.dll")] public static extern int GetWindowTextLength(IntPtr h);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassName(IntPtr h, StringBuilder sb, int max);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr h, int attr, out RECT r, int size);
    [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr h, int attr, out int v, int size);
    [DllImport("dwmapi.dll")] public static extern int DwmSetWindowAttribute(IntPtr h, int attr, ref int v, int size);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
}

/// Окна на рабочем столе, по верхней кромке которых можно ходить.
static class Desk
{
    public struct Win { public IntPtr H; public Rectangle R; public bool Platform; }

    public static readonly List<Win> Wins = new();
    public static readonly HashSet<IntPtr> Own = new();
    static readonly Stopwatch sw = Stopwatch.StartNew();
    static readonly Native.EnumWindowsProc cb = OnWindow;
    static readonly StringBuilder cls = new(64);
    static double last = -1;

    public static void Scan(bool force = false)
    {
        double now = sw.Elapsed.TotalSeconds;
        if (!force && now - last < 0.025) return;
        last = now;
        Wins.Clear();
        Native.EnumWindows(cb, IntPtr.Zero);
    }

    static bool OnWindow(IntPtr h, IntPtr _)
    {
        if (Own.Contains(h) || !Native.IsWindowVisible(h) || Native.IsIconic(h)) return true;
        int ex = Native.GetWindowLong(h, -20);
        if ((ex & (0x80 | 0x20 | 0x08000000)) != 0) return true; // TOOLWINDOW | TRANSPARENT | NOACTIVATE
        if (Native.DwmGetWindowAttribute(h, 14, out int cloaked, 4) == 0 && cloaked != 0) return true;
        cls.Clear();
        Native.GetClassName(h, cls, 64);
        string c = cls.ToString();
        if (c is "Progman" or "WorkerW" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd" or "#32768") return true;
        if (!GetRect(h, out var r) || r.Width < 60 || r.Height < 30) return true;

        var mon = Screen.FromHandle(h).Bounds;
        bool full = r.Left <= mon.Left && r.Top <= mon.Top && r.Right >= mon.Right && r.Bottom >= mon.Bottom;
        bool platform = !full && !Native.IsZoomed(h) && Native.GetWindowTextLength(h) > 0 && r.Width >= 150 && r.Height >= 80;
        Wins.Add(new Win { H = h, R = r, Platform = platform });
        return true;
    }

    /// Монитор, на котором сейчас полноэкранное окно (игра, видео, F11), иначе null.
    public static Rectangle? Fullscreen;

    public static void CheckFullscreen()
    {
        Fullscreen = null;
        var fg = Native.GetForegroundWindow();
        if (fg == IntPtr.Zero || Own.Contains(fg)) return;
        cls.Clear();
        Native.GetClassName(fg, cls, 64);
        if (cls.ToString() is "Progman" or "WorkerW" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd") return;
        if (!Native.GetWindowRect(fg, out var rc)) return;
        var mon = Screen.FromHandle(fg).Bounds;
        if (rc.L <= mon.Left && rc.T <= mon.Top && rc.R >= mon.Right && rc.B >= mon.Bottom) Fullscreen = mon;
    }

    /// Где можно ходить: рабочая область, а поверх полноэкранного окна — весь монитор (панели задач там не видно).
    public static Rectangle AreaAt(Point p)
    {
        var scr = Screen.FromPoint(p);
        return Fullscreen == scr.Bounds ? scr.Bounds : scr.WorkingArea;
    }

    public static bool GetRect(IntPtr h, out Rectangle r)
    {
        if (Native.DwmGetWindowAttribute(h, 9, out Native.RECT rc, Marshal.SizeOf<Native.RECT>()) != 0 &&
            !Native.GetWindowRect(h, out rc))
        {
            r = default;
            return false;
        }
        r = Rectangle.FromLTRB(rc.L, rc.T, rc.R, rc.B);
        return r.Width > 0 && r.Height > 0;
    }

    static bool Visible(int idx, int px, int py)
    {
        for (int j = 0; j < idx; j++)
            if (Wins[j].R.Contains(px, py)) return false;
        return true;
    }

    /// Пересекает ли падение с prevY до newY верхнюю кромку какого-нибудь окна в точке x.
    public static bool TryLand(float x, float prevY, float newY, float margin, out float top, out IntPtr h)
    {
        top = float.MaxValue;
        h = IntPtr.Zero;
        for (int i = 0; i < Wins.Count; i++)
        {
            var w = Wins[i];
            if (!w.Platform || x < w.R.Left + margin || x > w.R.Right - margin) continue;
            float t = w.R.Top;
            if (prevY <= t + 0.5f && newY >= t && t < top && Visible(i, (int)x, w.R.Top + 3))
            {
                top = t;
                h = w.H;
            }
        }
        return h != IntPtr.Zero;
    }

    public static bool Standable(IntPtr h, float x)
    {
        for (int i = 0; i < Wins.Count; i++)
            if (Wins[i].H == h) return Wins[i].Platform && Visible(i, (int)x, Wins[i].R.Top + 3);
        return false;
    }

    /// Точки на кромках окон, куда можно запрыгнуть из (x, y).
    public static List<(IntPtr h, float x, float top)> JumpTargets(float x, float y, float minDy, float maxDy, float maxDx, float margin)
    {
        var res = new List<(IntPtr, float, float)>();
        for (int i = 0; i < Wins.Count; i++)
        {
            var w = Wins[i];
            if (!w.Platform) continue;
            float dy = Math.Abs(y - w.R.Top);
            if (dy < minDy || dy > maxDy) continue;
            float tx = Math.Clamp(x, w.R.Left + margin, w.R.Right - margin);
            if (w.R.Right - w.R.Left < margin * 2 + 10) continue;
            float dx = Math.Abs(tx - x);
            if (dx > maxDx) continue;
            if (dx < 20) tx = Math.Clamp(x + (x - w.R.Left < w.R.Right - x ? 60 : -60), w.R.Left + margin, w.R.Right - margin);
            if (Visible(i, (int)tx, w.R.Top + 3) && Screen.FromPoint(new Point((int)tx, w.R.Top)).WorkingArea.Top < w.R.Top - 80)
                res.Add((w.H, tx, w.R.Top));
        }
        return res;
    }
}

class Settings
{
    public string Skin { get; set; } = "classic";
    public List<string> Wear { get; set; } = new();
    public string CustomBody { get; set; } = "#D97757";
    public string CustomEye { get; set; } = "#181216";
    public string CustomOutline { get; set; } = "#2A1812";

    public static Color Hex(string s, Color fallback)
    {
        try { return ColorTranslator.FromHtml(s); } catch { return fallback; }
    }

    public static string Hex(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";

    /// Переносит свои цвета из настроек в скин «custom».
    public void ApplyCustom() => Looks.SetCustom(
        Hex(CustomBody, Color.FromArgb(217, 119, 87)), Hex(CustomEye, Color.FromArgb(24, 18, 22)), Hex(CustomOutline, Color.FromArgb(42, 24, 18)));

    // аксессуар из прошлых версий, где можно было надеть только один
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string Accessory { get; set; }
    public int Size { get; set; } = 2;
    public int Mood { get; set; } = 1;
    public bool Bongo { get; set; } = true;
    public bool Windows { get; set; } = true;
    public bool Follow { get; set; }
    public bool SleepWhenIdle { get; set; } = true;
    public bool HideInFullscreen { get; set; } = true;
    public string Language { get; set; } = "auto";
    public bool FirstRunDone { get; set; }
    public long Keys { get; set; }
    public int Pets { get; set; }
    public int Tokens { get; set; }
    public int Flips { get; set; }

    public static readonly int[] PixelSizes = { 4, 5, 6, 8 };

    static string AppData => Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
    static string Dir => Path.Combine(AppData, "Vibling");
    static string OldDir => Path.Combine(AppData, "Vibobus");
    static string FilePath => Path.Combine(Dir, "settings.json");

    [System.Text.Json.Serialization.JsonIgnore]
    public float Pixel => PixelSizes[Math.Clamp(Size, 0, PixelSizes.Length - 1)];

    public static Settings Load()
    {
        var s = LoadRaw();
        s.Wear ??= new();
        if (!string.IsNullOrEmpty(s.Accessory) && s.Accessory != "none" && !s.Wear.Contains(s.Accessory)) s.Wear.Add(s.Accessory);
        s.Accessory = null;
        s.ApplyCustom();
        return s;
    }

    static Settings LoadRaw()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath)) ?? new Settings();
            // переезд со старого названия
            var oldJson = Path.Combine(OldDir, "settings.json");
            if (File.Exists(oldJson))
                return JsonSerializer.Deserialize<Settings>(File.ReadAllText(oldJson)) ?? new Settings();
            var s = new Settings();
            var old = Path.Combine(OldDir, "state.txt");
            if (File.Exists(old))
            {
                foreach (var line in File.ReadAllLines(old))
                {
                    var kv = line.Split('=', 2);
                    if (kv.Length != 2) continue;
                    if (kv[0] == "keys" && long.TryParse(kv[1], out var k)) s.Keys = k;
                    if (kv[0] == "pets" && int.TryParse(kv[1], out var p)) s.Pets = p;
                    if (kv[0] == "tokens" && int.TryParse(kv[1], out var t)) s.Tokens = t;
                }
            }
            return s;
        }
        catch { return new Settings(); }
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Dir);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }
}

static class Autostart
{
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string Name = "Vibling", OldName = "Vibobus";
    static string Cmd => $"\"{Application.ExecutablePath}\"";

    public static bool On
    {
        get
        {
            using var k = Registry.CurrentUser.OpenSubKey(RunKey);
            return k?.GetValue(Name) != null;
        }
        set
        {
            using var k = Registry.CurrentUser.OpenSubKey(RunKey, true);
            if (k == null) return;
            if (value) k.SetValue(Name, Cmd);
            else if (k.GetValue(Name) != null) k.DeleteValue(Name);
        }
    }

    /// Если exe переехал, автозапуск должен смотреть на новое место.
    public static void Refresh()
    {
        try
        {
            using var k = Registry.CurrentUser.OpenSubKey(RunKey, true);
            if (k == null) return;
            if (k.GetValue(OldName) != null) { k.DeleteValue(OldName); k.SetValue(Name, Cmd); }
            if (k.GetValue(Name) is string v && v != Cmd) k.SetValue(Name, Cmd);
        }
        catch { }
    }
}

static class AppIcon
{
    static Icon icon;
    public static Icon Get()
    {
        if (icon != null) return icon;
        try { icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
        return icon ??= SystemIcons.Application;
    }
}

/// Окно без рамки с попиксельной прозрачностью: прозрачные места пропускают клики насквозь.
class LayeredForm : Form
{
    public LayeredForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= 0x80000 | 0x80 | 0x8 | 0x08000000; // LAYERED | TOOLWINDOW | TOPMOST | NOACTIVATE
            return cp;
        }
    }

    protected override bool ShowWithoutActivation => true;

    public void Present(Bitmap bmp, int x, int y, byte alpha = 255)
    {
        IntPtr screenDc = Native.GetDC(IntPtr.Zero);
        IntPtr memDc = Native.CreateCompatibleDC(screenDc);
        IntPtr hBmp = bmp.GetHbitmap(Color.FromArgb(0));
        IntPtr old = Native.SelectObject(memDc, hBmp);
        var size = new Native.SIZE(bmp.Width, bmp.Height);
        var src = new Native.POINT(0, 0);
        var dst = new Native.POINT(x, y);
        var blend = new Native.BLENDFUNCTION { BlendOp = 0, BlendFlags = 0, SourceConstantAlpha = alpha, AlphaFormat = 1 };
        Native.UpdateLayeredWindow(Handle, screenDc, ref dst, ref size, memDc, ref src, 0, ref blend, 2);
        Native.SelectObject(memDc, old);
        Native.DeleteObject(hBmp);
        Native.DeleteDC(memDc);
        Native.ReleaseDC(IntPtr.Zero, screenDc);
    }
}
