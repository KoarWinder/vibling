using System.Diagnostics;
using SD = System.Drawing;
using W = System.Windows;
using WC = System.Windows.Controls;
using WM = System.Windows.Media;
using WMI = System.Windows.Media.Imaging;
using WT = System.Windows.Threading;
using UI = Wpf.Ui.Controls;

namespace Vibling;

static class Fluent
{
    static bool ready;

    public static void Init()
    {
        if (ready) return;
        ready = true;
        var app = W.Application.Current ?? new W.Application { ShutdownMode = W.ShutdownMode.OnExplicitShutdown };
        app.Resources.MergedDictionaries.Add(new Wpf.Ui.Markup.ThemesDictionary { Theme = Wpf.Ui.Appearance.ApplicationTheme.Dark });
        app.Resources.MergedDictionaries.Add(new Wpf.Ui.Markup.ControlsDictionary());
    }

    public static void Accent(SD.Color c) =>
        Wpf.Ui.Appearance.ApplicationAccentColorManager.Apply(WM.Color.FromRgb(c.R, c.G, c.B), Wpf.Ui.Appearance.ApplicationTheme.Dark, false);

    public static WM.SolidColorBrush Brush(int r, int g, int b)
    {
        var x = new WM.SolidColorBrush(WM.Color.FromRgb((byte)r, (byte)g, (byte)b));
        x.Freeze();
        return x;
    }

    public static WM.SolidColorBrush Brush(SD.Color c) => Brush(c.R, c.G, c.B);

    public static WMI.BitmapSource Render(int w, int h, Action<SD.Graphics> draw)
    {
        using var bmp = new SD.Bitmap(w, h, SD.Imaging.PixelFormat.Format32bppArgb);
        using (var g = SD.Graphics.FromImage(bmp))
        {
            g.Clear(SD.Color.Transparent);
            g.PixelOffsetMode = SD.Drawing2D.PixelOffsetMode.Half;
            draw(g);
        }
        var data = bmp.LockBits(new SD.Rectangle(0, 0, w, h), SD.Imaging.ImageLockMode.ReadOnly, SD.Imaging.PixelFormat.Format32bppArgb);
        try
        {
            var src = WMI.BitmapSource.Create(w, h, 96, 96, WM.PixelFormats.Bgra32, null, data.Scan0, data.Stride * h, data.Stride);
            src.Freeze();
            return src;
        }
        finally { bmp.UnlockBits(data); }
    }

    public static WMI.BitmapSource Pet(Skin skin, IEnumerable<string> wear, int w, int h, float P, Pose pose = null)
    {
        if (pose == null) { pose = new Pose(); pose.Reset(0); }
        return Render(w, h, g => Sprite.Draw(g, pose, skin, wear, w / 2f, h - P * 1.5f, P));
    }

    public static WMI.BitmapSource Pet(Skin skin, string acc, int w, int h, float P) => Pet(skin, new[] { acc }, w, h, P);
}

class SettingsWindow : UI.FluentWindow
{
    static int lastPage;

    static readonly WM.Brush BgB = Fluent.Brush(32, 32, 32);
    static readonly WM.Brush PanelB = Fluent.Brush(43, 43, 43);
    static readonly WM.Brush PanelStroke = Fluent.Brush(52, 52, 52);
    static readonly WM.Brush CardB = Fluent.Brush(52, 52, 52);
    static readonly WM.Brush CardStroke = Fluent.Brush(60, 60, 60);
    static readonly WM.Brush TileB = Fluent.Brush(62, 62, 62);
    static readonly WM.Brush TileHover = Fluent.Brush(74, 74, 74);
    static readonly WM.Brush NavHover = Fluent.Brush(42, 42, 42);
    static readonly WM.Brush NavSelected = Fluent.Brush(50, 50, 50);
    static readonly WM.Brush Fg = Fluent.Brush(245, 245, 245);
    static readonly WM.Brush Muted = Fluent.Brush(170, 170, 170);

    readonly PetForm pet;
    readonly Settings S;
    readonly WC.ContentControl page = new();
    readonly WC.ScrollViewer scroll = new() { VerticalScrollBarVisibility = WC.ScrollBarVisibility.Auto, Padding = new W.Thickness(24, 20, 24, 8) };
    readonly List<(WC.Border item, WC.Border bar)> nav = new();
    readonly WT.DispatcherTimer tick = new() { Interval = TimeSpan.FromSeconds(1) };
    readonly WC.Image logo = new() { Width = 22, Height = 22 };
    readonly WC.ContentControl banner = new() { Margin = new W.Thickness(236, 0, 16, 12), Visibility = W.Visibility.Collapsed };
    Action onTick;

    Skin Skin => Looks.SkinById(S.Skin);
    WM.Brush AccentB => Fluent.Brush(Skin.Accent);

    public SettingsWindow(PetForm pet)
    {
        this.pet = pet;
        S = pet.S;
        Fluent.Accent(Skin.Accent);

        Title = L.T("app");
        Width = 1040;
        Height = 700;
        MinWidth = 900;
        MinHeight = 560;
        WindowStartupLocation = W.WindowStartupLocation.CenterScreen;
        ExtendsContentIntoTitleBar = true;
        WindowBackdropType = UI.WindowBackdropType.None;
        Background = BgB;
        Foreground = Fg;
        UseLayoutRounding = true;
        WM.RenderOptions.SetBitmapScalingMode(this, WM.BitmapScalingMode.NearestNeighbor);
        Icon = Fluent.Pet(Skin, "none", 32, 32, 1.6f);

        var root = new WC.Grid();
        root.RowDefinitions.Add(new WC.RowDefinition { Height = W.GridLength.Auto });
        root.RowDefinitions.Add(new WC.RowDefinition { Height = W.GridLength.Auto });
        root.RowDefinitions.Add(new WC.RowDefinition { Height = W.GridLength.Auto });
        root.RowDefinitions.Add(new WC.RowDefinition { Height = new W.GridLength(1, W.GridUnitType.Star) });

        var titleBar = new UI.TitleBar
        {
            Title = L.T("app"),
            Icon = new UI.ImageIcon { Source = Fluent.Pet(Skin, "none", 32, 32, 1.6f), Width = 16, Height = 16 },
            ShowMaximize = false,
        };
        root.Children.Add(titleBar);

        var head = new WC.Grid { Margin = new W.Thickness(24, 6, 16, 12) };
        head.ColumnDefinitions.Add(new WC.ColumnDefinition { Width = new W.GridLength(1, W.GridUnitType.Star) });
        head.ColumnDefinitions.Add(new WC.ColumnDefinition { Width = W.GridLength.Auto });
        var brand = new WC.StackPanel { Orientation = WC.Orientation.Horizontal, VerticalAlignment = W.VerticalAlignment.Center };
        logo.Source = Fluent.Pet(Skin, S.Wear, 44, 44, 2.2f);
        brand.Children.Add(logo);
        brand.Children.Add(new WC.TextBlock { Text = L.T("app"), FontSize = 17, FontWeight = W.FontWeights.SemiBold, Margin = new W.Thickness(10, 0, 0, 0), VerticalAlignment = W.VerticalAlignment.Center, Foreground = Fg });
        head.Children.Add(brand);
        var summon = new UI.Button { Content = L.T("summon"), Appearance = UI.ControlAppearance.Primary, Padding = new W.Thickness(28, 8, 28, 8), MinWidth = 110 };
        summon.Click += (_, _) => pet.Summon();
        WC.Grid.SetColumn(summon, 1);
        head.Children.Add(summon);
        WC.Grid.SetRow(head, 1);
        root.Children.Add(head);

        var body = new WC.Grid();
        body.ColumnDefinitions.Add(new WC.ColumnDefinition { Width = new W.GridLength(236) });
        body.ColumnDefinitions.Add(new WC.ColumnDefinition { Width = new W.GridLength(1, W.GridUnitType.Star) });
        WC.Grid.SetRow(banner, 2);
        root.Children.Add(banner);
        WC.Grid.SetRow(body, 3);
        root.Children.Add(body);

        var side = new WC.DockPanel { Margin = new W.Thickness(16, 0, 12, 16), LastChildFill = false };
        string[] pages = { "nav.look", "nav.behavior", "nav.system", "nav.about" };
        UI.SymbolRegular[] icons = { UI.SymbolRegular.PaintBrush24, UI.SymbolRegular.AnimalCat24, UI.SymbolRegular.Settings24, UI.SymbolRegular.Info24 };
        for (int i = 0; i < pages.Length; i++)
        {
            int idx = i;
            var item = NavItem(L.T(pages[i]), icons[i], () => ShowPage(idx));
            WC.DockPanel.SetDock(item, i == pages.Length - 1 ? WC.Dock.Bottom : WC.Dock.Top);
            side.Children.Add(item);
        }
        body.Children.Add(side);

        var panel = new WC.Border
        {
            Background = PanelB,
            BorderBrush = PanelStroke,
            BorderThickness = new W.Thickness(1),
            CornerRadius = new W.CornerRadius(8),
            Margin = new W.Thickness(0, 0, 16, 16),
            Child = scroll,
        };
        WC.Grid.SetColumn(panel, 1);
        body.Children.Add(panel);

        scroll.Content = page;
        scroll.RequestBringIntoView += (_, e) => e.Handled = true;
        Content = root;

        tick.Tick += (_, _) => onTick?.Invoke();
        tick.Start();
        Closed += (_, _) => { tick.Stop(); S.Save(); };

        ShowPage(lastPage);
        RefreshBanner();
    }

    public void RefreshBanner(string info = null)
    {
        var r = Updater.Pending;
        if (r == null && info == null)
        {
            banner.Content = null;
            banner.Visibility = W.Visibility.Collapsed;
            return;
        }

        var grid = new WC.Grid();
        grid.ColumnDefinitions.Add(new WC.ColumnDefinition { Width = new W.GridLength(1, W.GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new WC.ColumnDefinition { Width = W.GridLength.Auto });
        var text = new WC.StackPanel { VerticalAlignment = W.VerticalAlignment.Center };
        grid.Children.Add(text);
        var buttons = new WC.StackPanel { Orientation = WC.Orientation.Horizontal, VerticalAlignment = W.VerticalAlignment.Center, Margin = new W.Thickness(16, 0, 0, 0) };
        WC.Grid.SetColumn(buttons, 1);
        grid.Children.Add(buttons);

        if (r == null)
        {
            text.Children.Add(new WC.TextBlock { Text = info, FontSize = 14, Foreground = Fg, TextWrapping = W.TextWrapping.Wrap });
            var ok = new UI.Button { Content = "OK", MinWidth = 80 };
            ok.Click += (_, _) => RefreshBanner();
            buttons.Children.Add(ok);
        }
        else
        {
            text.Children.Add(new WC.TextBlock { Text = string.Format(L.T("upd.title"), Updater.Show(r.Version)), FontSize = 15, FontWeight = W.FontWeights.SemiBold, Foreground = Fg });
            var sub = new WC.TextBlock { FontSize = 13, Foreground = Muted, Margin = new W.Thickness(0, 2, 0, 0) };
            sub.Inlines.Add(new W.Documents.Run(string.Format(L.T("upd.text"), Updater.Show(Updater.Current)) + "  "));
            var link = new W.Documents.Hyperlink(new W.Documents.Run(L.T("upd.notes"))) { Foreground = AccentB };
            link.Click += (_, _) => { try { Process.Start(new ProcessStartInfo(r.Page) { UseShellExecute = true }); } catch { } };
            sub.Inlines.Add(link);
            text.Children.Add(sub);
            var bar = new WC.ProgressBar { Height = 4, Maximum = 1, Margin = new W.Thickness(0, 8, 0, 0), Visibility = W.Visibility.Collapsed };
            var status = new WC.TextBlock { FontSize = 12, Foreground = Muted, Margin = new W.Thickness(0, 4, 0, 0), Visibility = W.Visibility.Collapsed, TextWrapping = W.TextWrapping.Wrap };
            text.Children.Add(bar);
            text.Children.Add(status);

            var later = new UI.Button { Content = L.T("upd.later"), MinWidth = 90, Margin = new W.Thickness(0, 0, 8, 0) };
            later.Click += (_, _) => { Updater.Dismiss(); RefreshBanner(); };
            var update = new UI.Button { Content = L.T("upd.update"), Appearance = UI.ControlAppearance.Primary, MinWidth = 100 };
            update.Click += async (_, _) =>
            {
                update.IsEnabled = later.IsEnabled = false;
                bar.Visibility = status.Visibility = W.Visibility.Visible;
                status.Text = L.T("upd.downloading");
                try
                {
                    await Updater.Install(r, new Progress<double>(p => bar.Value = p));
                    pet.Quit();
                }
                catch (Exception e)
                {
                    status.Text = L.T("upd.error") + ": " + e.Message;
                    update.IsEnabled = later.IsEnabled = true;
                }
            };
            buttons.Children.Add(later);
            buttons.Children.Add(update);
        }

        banner.Content = new WC.Border
        {
            Background = CardB,
            BorderBrush = AccentB,
            BorderThickness = new W.Thickness(1),
            CornerRadius = new W.CornerRadius(8),
            Padding = new W.Thickness(18, 12, 12, 12),
            Child = grid,
        };
        banner.Visibility = W.Visibility.Visible;
    }

    WC.Border NavItem(string text, UI.SymbolRegular icon, Action click)
    {
        var bar = new WC.Border { Width = 3, Height = 16, CornerRadius = new W.CornerRadius(1.5), HorizontalAlignment = W.HorizontalAlignment.Left, Visibility = W.Visibility.Hidden };
        var row = new WC.StackPanel { Orientation = WC.Orientation.Horizontal, VerticalAlignment = W.VerticalAlignment.Center, Margin = new W.Thickness(14, 0, 0, 0) };
        row.Children.Add(new UI.SymbolIcon { Symbol = icon, FontSize = 16, Foreground = Fg });
        row.Children.Add(new WC.TextBlock { Text = text, FontSize = 14, Margin = new W.Thickness(12, 0, 0, 0), VerticalAlignment = W.VerticalAlignment.Center, Foreground = Fg });
        var grid = new WC.Grid();
        grid.Children.Add(bar);
        grid.Children.Add(row);
        var item = new WC.Border
        {
            Height = 38,
            CornerRadius = new W.CornerRadius(5),
            Margin = new W.Thickness(0, 2, 0, 2),
            Background = WM.Brushes.Transparent,
            Cursor = System.Windows.Input.Cursors.Hand,
            Child = grid,
        };
        item.MouseEnter += (_, _) => { if (bar.Visibility != W.Visibility.Visible) item.Background = NavHover; };
        item.MouseLeave += (_, _) => { if (bar.Visibility != W.Visibility.Visible) item.Background = WM.Brushes.Transparent; };
        item.MouseLeftButtonUp += (_, _) => click();
        nav.Add((item, bar));
        return item;
    }

    void ShowPage(int i)
    {
        lastPage = i;
        onTick = null;
        for (int n = 0; n < nav.Count; n++)
        {
            bool sel = n == i;
            nav[n].item.Background = sel ? NavSelected : WM.Brushes.Transparent;
            nav[n].bar.Visibility = sel ? W.Visibility.Visible : W.Visibility.Hidden;
            nav[n].bar.Background = AccentB;
        }

        string[] titles = { "nav.look", "nav.behavior", "nav.system", "nav.about" };
        var stack = new WC.StackPanel();
        stack.Children.Add(new WC.TextBlock { Text = L.T(titles[i]), FontSize = 28, FontWeight = W.FontWeights.SemiBold, Foreground = Fg, Margin = new W.Thickness(0, 0, 0, 20) });

        var cols = new WC.Grid();
        cols.ColumnDefinitions.Add(new WC.ColumnDefinition { Width = new W.GridLength(1, W.GridUnitType.Star) });
        cols.ColumnDefinitions.Add(new WC.ColumnDefinition { Width = new W.GridLength(16) });
        cols.ColumnDefinitions.Add(new WC.ColumnDefinition { Width = new W.GridLength(1, W.GridUnitType.Star) });
        var left = new WC.StackPanel();
        var right = new WC.StackPanel();
        WC.Grid.SetColumn(right, 2);
        cols.Children.Add(left);
        cols.Children.Add(right);
        stack.Children.Add(cols);

        switch (i)
        {
            case 0: PageLook(left, right); break;
            case 1: PageBehavior(left, right); break;
            case 2: PageSystem(left, right); break;
            default: PageAbout(left, right); break;
        }
        page.Content = stack;
        scroll.ScrollToTop();
    }

    WC.Border Card(string title, params W.UIElement[] rows)
    {
        var st = new WC.StackPanel();
        st.Children.Add(new WC.TextBlock { Text = title, FontSize = 18, FontWeight = W.FontWeights.SemiBold, Foreground = Fg, Margin = new W.Thickness(0, 0, 0, 10) });
        foreach (var r in rows) st.Children.Add(r);
        return new WC.Border
        {
            Background = CardB,
            BorderBrush = CardStroke,
            BorderThickness = new W.Thickness(1),
            CornerRadius = new W.CornerRadius(6),
            Padding = new W.Thickness(18, 16, 18, 12),
            Margin = new W.Thickness(0, 0, 0, 16),
            Child = st,
        };
    }

    W.UIElement Row(string label, W.FrameworkElement control, string sub = null)
    {
        var g = new WC.Grid { Margin = new W.Thickness(0, 5, 0, 5), MinHeight = 34 };
        g.ColumnDefinitions.Add(new WC.ColumnDefinition { Width = new W.GridLength(1, W.GridUnitType.Star) });
        g.ColumnDefinitions.Add(new WC.ColumnDefinition { Width = W.GridLength.Auto });
        var text = new WC.StackPanel { VerticalAlignment = W.VerticalAlignment.Center, Margin = new W.Thickness(0, 0, 16, 0) };
        text.Children.Add(new WC.TextBlock { Text = label, FontSize = 14, FontWeight = W.FontWeights.SemiBold, Foreground = Fg, TextWrapping = W.TextWrapping.Wrap });
        if (sub != null)
            text.Children.Add(new WC.TextBlock { Text = sub, FontSize = 12, Foreground = Muted, TextWrapping = W.TextWrapping.Wrap, Margin = new W.Thickness(0, 2, 0, 0) });
        g.Children.Add(text);
        control.VerticalAlignment = W.VerticalAlignment.Center;
        WC.Grid.SetColumn(control, 1);
        g.Children.Add(control);
        return g;
    }

    UI.ToggleSwitch Toggle(bool value, Action<bool> set)
    {
        var t = new UI.ToggleSwitch { IsChecked = value, OnContent = L.T("on"), OffContent = L.T("off") };
        t.Checked += (_, _) => { set(true); S.Save(); };
        t.Unchecked += (_, _) => { set(false); S.Save(); };
        return t;
    }

    WC.ComboBox Combo(string[] items, int selected, Action<int> set, double width = 210)
    {
        var c = new WC.ComboBox { Width = width, ItemsSource = items, SelectedIndex = Math.Max(0, selected) };
        c.SelectionChanged += (_, _) => { if (c.SelectedIndex >= 0) { set(c.SelectedIndex); S.Save(); } };
        return c;
    }

    UI.Button Btn(string text, Action act)
    {
        var b = new UI.Button { Content = text, HorizontalAlignment = W.HorizontalAlignment.Stretch, Margin = new W.Thickness(0, 0, 0, 0), Padding = new W.Thickness(12, 7, 12, 7) };
        b.Click += (_, _) => act();
        return b;
    }

    void PageLook(WC.StackPanel left, WC.StackPanel right)
    {
        var preview = new WC.Image { Height = 220, Stretch = WM.Stretch.None };
        var previewBox = new WC.Border { Background = PanelB, CornerRadius = new W.CornerRadius(6), Height = 230, Child = preview, ClipToBounds = true };
        var pose = new Pose();
        var rnd = new Random();
        float t = 0, blink = 2, happy = 0, wave = 4;
        int face = 1;
        previewBox.MouseLeftButtonUp += (_, _) => happy = 1.2f;
        var anim = new WT.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
        anim.Tick += (_, _) =>
        {
            if (!previewBox.IsVisible) return;
            t += 0.033f; blink -= 0.033f; happy -= 0.033f; wave -= 0.033f;
            if (blink < -0.12f) blink = 2 + (float)rnd.NextDouble() * 3;
            if (wave < -1.5f) { wave = 5 + (float)rnd.NextDouble() * 4; face = rnd.Next(2) == 0 ? 1 : -1; }
            int w = Math.Max(1, (int)previewBox.ActualWidth), h = 220;
            float P = Math.Min(9f, S.Pixel * 1.5f);
            pose.Reset(t);
            var c = previewBox.PointFromScreen(new W.Point(System.Windows.Forms.Cursor.Position.X, System.Windows.Forms.Cursor.Position.Y));
            pose.LookX = (c.X > w * 0.62 ? 1 : c.X < w * 0.38 ? -1 : 0) * face;
            pose.LookY = c.Y < h * 0.25 ? -1 : 0;
            if (blink < 0) pose.Eyes = Eyes.Blink;
            float hop = 0;
            if (happy > 0) { pose.Arms = Arms.Up; pose.Eyes = Eyes.Happy; pose.Blush = true; hop = -Math.Abs((float)Math.Sin(happy * 10)) * 2 * P; }
            else if (wave < 0) { pose.Arms = Arms.Wave; pose.Eyes = Eyes.Happy; }
            float sq = 1 + (float)Math.Sin(t * 2.2) * 0.025f;
            preview.Source = Fluent.Render(w, h, g =>
            {
                g.SmoothingMode = SD.Drawing2D.SmoothingMode.AntiAlias;
                using (var glow = new SD.SolidBrush(SD.Color.FromArgb(34, Skin.Accent)))
                    g.FillEllipse(glow, w / 2f - 110, 20, 220, 190);
                using (var sh = new SD.SolidBrush(SD.Color.FromArgb(70, 0, 0, 0)))
                    g.FillEllipse(sh, w / 2f - 8 * P, h - 30 - 0.5f * P, 16 * P, P);
                g.SmoothingMode = SD.Drawing2D.SmoothingMode.None;
                Sprite.Draw(g, pose, Skin, S.Wear, w / 2f, h - 30 + hop, P, face, 1, sq);
            });
        };
        anim.Start();
        Closed += (_, _) => anim.Stop();
        left.Children.Add(Card(L.T("card.preview"), previewBox));

        left.Children.Add(Card(L.T("card.look"),
            Row(L.T("sec.size"), Combo(Enumerable.Range(0, 4).Select(n => L.T("size." + n)).ToArray(), S.Size, i => S.Size = i))));

        WC.Border Tile(W.FrameworkElement img, string name, Action click)
        {
            var st = new WC.StackPanel { HorizontalAlignment = W.HorizontalAlignment.Center, VerticalAlignment = W.VerticalAlignment.Center };
            st.Children.Add(img);
            st.Children.Add(new WC.TextBlock { Text = name, FontSize = 12, Foreground = Muted, HorizontalAlignment = W.HorizontalAlignment.Center, TextTrimming = W.TextTrimming.CharacterEllipsis, Margin = new W.Thickness(4, 2, 4, 0) });
            var tile = new WC.Border
            {
                Width = 96, Height = 96, Margin = new W.Thickness(0, 0, 8, 8),
                CornerRadius = new W.CornerRadius(6), Background = TileB,
                BorderThickness = new W.Thickness(2), Cursor = System.Windows.Input.Cursors.Hand, Child = st,
            };
            tile.MouseEnter += (_, _) => tile.Background = TileHover;
            tile.MouseLeave += (_, _) => tile.Background = TileB;
            tile.MouseLeftButtonUp += (_, _) => click();
            return tile;
        }

        var skinTiles = new List<(WC.Border tile, Skin skin, WC.Image img)>();
        WC.Border customTile = null;
        W.UIElement customCard = null;
        var accTiles = new List<(WC.Border tile, string id, WC.Image img)>();
        void Refresh()
        {
            foreach (var (tile, skin, img) in skinTiles)
            {
                tile.BorderBrush = skin.Id == S.Skin ? AccentB : WM.Brushes.Transparent;
                img.Source = Fluent.Pet(skin, S.Wear, 64, 60, 3f);
            }
            foreach (var (tile, id, img) in accTiles)
            {
                tile.BorderBrush = S.Wear.Contains(id) ? AccentB : WM.Brushes.Transparent;
                img.Source = Fluent.Pet(Skin, id, 64, 60, 3f);
            }
            logo.Source = Fluent.Pet(Skin, S.Wear, 44, 44, 2.2f);
            foreach (var (_, bar) in nav) bar.Background = AccentB;
            if (customTile != null) customTile.BorderBrush = S.Skin == "custom" ? AccentB : WM.Brushes.Transparent;
            if (customCard != null) customCard.Visibility = S.Skin == "custom" ? W.Visibility.Visible : W.Visibility.Collapsed;
        }

        var skins = new WC.WrapPanel();
        foreach (var skin in Looks.Skins)
        {
            var img = new WC.Image { Width = 64, Height = 60 };
            var sk = skin;
            var tile = Tile(img, skin.Name, () =>
            {
                S.Skin = sk.Id;
                S.Save();
                Fluent.Accent(sk.Accent);
                Refresh();
            });
            skinTiles.Add((tile, skin, img));
            skins.Children.Add(tile);
        }

        var plus = new WC.Grid { Width = 64, Height = 60 };
        plus.Children.Add(new UI.SymbolIcon { Symbol = UI.SymbolRegular.Add24, FontSize = 30, Foreground = Fg, HorizontalAlignment = W.HorizontalAlignment.Center, VerticalAlignment = W.VerticalAlignment.Center });
        customTile = Tile(plus, L.T("skin.custom"), () =>
        {
            S.Skin = "custom";
            S.Save();
            Fluent.Accent(Skin.Accent);
            Refresh();
        });
        skins.Children.Add(customTile);
        right.Children.Add(Card(L.T("card.skin"), skins));

        customCard = CustomColorCard(() => { Fluent.Accent(Skin.Accent); Refresh(); });
        left.Children.Insert(1, customCard);

        var accs = new WC.WrapPanel();
        foreach (var a in Looks.Accessories.Where(a => a.Id != "none"))
        {
            var img = new WC.Image { Width = 64, Height = 60 };
            string id = a.Id;
            var tile = Tile(img, a.Name, () =>
            {
                if (!S.Wear.Remove(id)) S.Wear.Add(id);
                S.Save();
                Refresh();
            });
            accTiles.Add((tile, id, img));
            accs.Children.Add(tile);
        }
        var clear = new UI.Button { Content = L.T("acc.clear"), Padding = new W.Thickness(14, 6, 14, 6), Margin = new W.Thickness(0, 4, 0, 4) };
        clear.Click += (_, _) => { S.Wear.Clear(); S.Save(); Refresh(); };
        right.Children.Add(Card(L.T("card.accessories"), accs, clear));
        Refresh();
    }

    static readonly string[] Palette =
    {
        "#D97757", "#E8484F", "#FF6EAA", "#F59AB8", "#B47CFF", "#787CE8", "#4682E6", "#3DB5E6",
        "#6FD3B5", "#60C46E", "#B6E05A", "#F2CF4A", "#FFB03C", "#8B5A3C", "#8B8D98", "#ECEEF8",
        "#30324A", "#181216", "#2A1812", "#0A0A12", "#FFFFFF", "#7FE9FF", "#FFD048", "#5C3A28",
    };

    static (double h, double s, double v) ToHsv(SD.Color c)
    {
        double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0;
        double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b)), d = max - min;
        double h = d == 0 ? 0 : max == r ? 60 * (((g - b) / d) % 6) : max == g ? 60 * ((b - r) / d + 2) : 60 * ((r - g) / d + 4);
        if (h < 0) h += 360;
        return (h, max == 0 ? 0 : d / max, max);
    }

    static SD.Color FromHsv(double h, double s, double v)
    {
        double c = v * s, x = c * (1 - Math.Abs(h / 60 % 2 - 1)), m = v - c;
        (double r, double g, double b) = h < 60 ? (c, x, 0d) : h < 120 ? (x, c, 0d) : h < 180 ? (0d, c, x) : h < 240 ? (0d, x, c) : h < 300 ? (x, 0d, c) : (c, 0d, x);
        return SD.Color.FromArgb((int)Math.Round((r + m) * 255), (int)Math.Round((g + m) * 255), (int)Math.Round((b + m) * 255));
    }

    W.UIElement CustomColorCard(Action changed)
    {
        string[] keys = { "color.body", "color.eyes", "color.outline" };
        Func<string>[] get = { () => S.CustomBody, () => S.CustomEye, () => S.CustomOutline };
        Action<string>[] put = { v => S.CustomBody = v, v => S.CustomEye = v, v => S.CustomOutline = v };
        int target = 0;
        bool syncing = false;

        var swatches = new WC.Border[3];
        var holders = new WC.Border[3];
        var targets = new WC.StackPanel { Orientation = WC.Orientation.Horizontal, Margin = new W.Thickness(0, 0, 0, 12) };
        var palette = new WC.WrapPanel { Margin = new W.Thickness(0, 0, 0, 6) };
        var hue = new WC.Slider { Minimum = 0, Maximum = 360, Width = 200 };
        var sat = new WC.Slider { Minimum = 0, Maximum = 1, Width = 200 };
        var val = new WC.Slider { Minimum = 0, Maximum = 1, Width = 200 };
        var hex = new WC.TextBox { Width = 110, FontFamily = new WM.FontFamily("Consolas") };

        SD.Color Current() => Settings.Hex(get[target](), SD.Color.Gray);

        void Sync()
        {
            syncing = true;
            var (h, s, v) = ToHsv(Current());
            hue.Value = h; sat.Value = s; val.Value = v;
            hex.Text = Settings.Hex(Current());
            for (int i = 0; i < 3; i++)
            {
                swatches[i].Background = Fluent.Brush(Settings.Hex(get[i](), SD.Color.Gray));
                holders[i].BorderBrush = i == target ? AccentB : WM.Brushes.Transparent;
            }
            syncing = false;
        }

        void Apply(SD.Color c, bool fromSliders)
        {
            put[target](Settings.Hex(c));
            S.ApplyCustom();
            swatches[target].Background = Fluent.Brush(c);
            if (!fromSliders) Sync();
            else if (!hex.IsKeyboardFocused) { syncing = true; hex.Text = Settings.Hex(c); syncing = false; }
            changed();
        }

        for (int i = 0; i < 3; i++)
        {
            int idx = i;
            swatches[i] = new WC.Border { Width = 56, Height = 26, CornerRadius = new W.CornerRadius(4), BorderBrush = Fluent.Brush(90, 90, 90), BorderThickness = new W.Thickness(1) };
            var st = new WC.StackPanel();
            st.Children.Add(swatches[i]);
            st.Children.Add(new WC.TextBlock { Text = L.T(keys[i]), FontSize = 12, Foreground = Fg, HorizontalAlignment = W.HorizontalAlignment.Center, Margin = new W.Thickness(0, 4, 0, 0) });
            holders[i] = new WC.Border
            {
                Padding = new W.Thickness(8, 8, 8, 6), Margin = new W.Thickness(0, 0, 8, 0), CornerRadius = new W.CornerRadius(6),
                Background = TileB, BorderThickness = new W.Thickness(2), Cursor = System.Windows.Input.Cursors.Hand, Child = st,
            };
            holders[i].MouseLeftButtonUp += (_, _) => { target = idx; Sync(); };
            targets.Children.Add(holders[i]);
        }

        foreach (var code in Palette)
        {
            var c = Settings.Hex(code, SD.Color.Gray);
            var dot = new WC.Border
            {
                Width = 24, Height = 24, Margin = new W.Thickness(0, 0, 6, 6), CornerRadius = new W.CornerRadius(4),
                Background = Fluent.Brush(c), BorderBrush = Fluent.Brush(90, 90, 90), BorderThickness = new W.Thickness(1),
                Cursor = System.Windows.Input.Cursors.Hand, ToolTip = code,
            };
            dot.MouseLeftButtonUp += (_, _) => { Apply(c, false); S.Save(); };
            palette.Children.Add(dot);
        }

        void FromSliders() { if (!syncing) Apply(FromHsv(hue.Value, sat.Value, val.Value), true); }
        hue.ValueChanged += (_, _) => FromSliders();
        sat.ValueChanged += (_, _) => FromSliders();
        val.ValueChanged += (_, _) => FromSliders();
        foreach (var sl in new[] { hue, sat, val }) sl.LostMouseCapture += (_, _) => S.Save();

        void FromHex()
        {
            if (syncing) return;
            var t = hex.Text.Trim();
            if (!t.StartsWith("#")) t = "#" + t;
            if (t.Length == 7 && int.TryParse(t[1..], System.Globalization.NumberStyles.HexNumber, null, out _))
            {
                Apply(Settings.Hex(t, SD.Color.Gray), false);
                S.Save();
            }
        }
        hex.KeyUp += (_, e) => { if (e.Key == System.Windows.Input.Key.Enter) FromHex(); };
        hex.LostFocus += (_, _) => FromHex();
        hex.TextChanged += (_, _) => { if (hex.IsKeyboardFocused && hex.Text.Trim().TrimStart('#').Length == 6) FromHex(); };

        var card = Card(L.T("card.custom"), targets, palette,
            Row(L.T("color.hue"), hue), Row(L.T("color.sat"), sat), Row(L.T("color.val"), val), Row("HEX", hex));
        Sync();
        return card;
    }

    void PageBehavior(WC.StackPanel left, WC.StackPanel right)
    {
        left.Children.Add(Card(L.T("card.mood"),
            Row(L.T("sec.mood"), Combo(Enumerable.Range(0, 3).Select(n => L.T("mood." + n)).ToArray(), S.Mood, i => S.Mood = i)),
            Row(L.T("opt.sleep"), Toggle(S.SleepWhenIdle, v => S.SleepWhenIdle = v), L.T("opt.sleep.sub"))));

        left.Children.Add(Card(L.T("card.keyboard"),
            Row(L.T("opt.bongo"), Toggle(S.Bongo, v => S.Bongo = v), L.T("opt.bongo.sub"))));

        right.Children.Add(Card(L.T("card.screen"),
            Row(L.T("opt.windows"), Toggle(S.Windows, v => S.Windows = v), L.T("opt.windows.sub")),
            Row(L.T("opt.follow"), Toggle(S.Follow, v => S.Follow = v), L.T("opt.follow.sub"))));

        right.Children.Add(Card(L.T("card.fullscreen"),
            Row(L.T("opt.fsmode"), Combo(new[] { L.T("fs.hide"), L.T("fs.walk") }, S.HideInFullscreen ? 0 : 1, i => S.HideInFullscreen = i == 0, 190), L.T("opt.fsmode.sub"))));
    }

    void PageSystem(WC.StackPanel left, WC.StackPanel right)
    {
        left.Children.Add(Card(L.T("card.general"),
            Row(L.T("sec.language"), Combo(L.Languages.Select(l => L.T("lang." + l)).ToArray(), Array.IndexOf(L.Languages, S.Language), i =>
            {
                S.Language = L.Languages[i];
                L.Set(S.Language);
                Dispatcher.BeginInvoke(pet.ReloadSettings);
            })),
            Row(L.T("autostart"), Toggle(Autostart.On, v => Autostart.On = v))));

        var check = new UI.Button { Content = L.T("upd.check"), Padding = new W.Thickness(14, 6, 14, 6) };
        check.Click += async (_, _) =>
        {
            check.IsEnabled = false;
            var info = await Updater.Check(pet, true);
            if (info != null) RefreshBanner(info);
            check.IsEnabled = true;
        };
        left.Children.Add(Card(L.T("card.updates"),
            Row(string.Format(L.T("upd.yours"), Updater.Show(Updater.Current)), check)));

        var ballBtn = Btn(L.T(pet.HasBall ? "ball.remove" : "ball"), () => { });
        ballBtn.Click += (_, _) => { pet.ToggleBall(); ballBtn.Content = L.T(pet.HasBall ? "ball.remove" : "ball"); };
        var hideBtn = Btn(L.T(pet.IsHidden ? "show" : "hide"), () => { });
        hideBtn.Click += (_, _) => { pet.SetHidden(!pet.IsHidden); hideBtn.Content = L.T(pet.IsHidden ? "show" : "hide"); };
        var grid = new WC.Primitives.UniformGrid { Columns = 2 };
        foreach (var b in new[] { Btn(L.T("feed"), pet.Feed), Btn(L.T("pet"), pet.PetMe), Btn(L.T("flip"), pet.Flip), ballBtn, hideBtn, Btn(L.T("quit.button"), pet.Quit) })
        {
            b.Margin = new W.Thickness(0, 0, 8, 8);
            grid.Children.Add(b);
        }
        left.Children.Add(Card(L.T("card.actions"), grid));

        WC.TextBlock Val() => new() { FontSize = 14, Foreground = Fg, MinWidth = 80, TextAlignment = W.TextAlignment.Right };
        var pets = Val(); var keys = Val(); var tokens = Val();
        right.Children.Add(Card(L.T("card.stats"),
            Row(L.T("stat.pets"), pets), Row(L.T("stat.keys"), keys), Row(L.T("stat.tokens"), tokens)));
        onTick = () =>
        {
            pets.Text = S.Pets.ToString("N0");
            keys.Text = S.Keys.ToString("N0");
            tokens.Text = S.Tokens.ToString("N0");
            ballBtn.Content = L.T(pet.HasBall ? "ball.remove" : "ball");
            hideBtn.Content = L.T(pet.IsHidden ? "show" : "hide");
        };
        onTick();
    }

    void PageAbout(WC.StackPanel left, WC.StackPanel right)
    {
        var version = typeof(Program).Assembly.GetName().Version;
        WC.TextBlock Val(string text) => new() { Text = text, FontSize = 14, Foreground = Fg };
        var link = new WC.TextBlock { FontSize = 14 };
        var hl = new W.Documents.Hyperlink(new W.Documents.Run("github.com/KoarWinder/vibling")) { Foreground = AccentB };
        hl.Click += (_, _) =>
        {
            try { Process.Start(new ProcessStartInfo("https://github.com/KoarWinder/vibling") { UseShellExecute = true }); } catch { }
        };
        link.Inlines.Add(hl);
        left.Children.Add(Card(L.T("app"),
            Row(L.T("about.version"), Val($"{version.Major}.{version.Minor}.{version.Build}")),
            Row(L.T("about.license"), Val("MIT")),
            Row(L.T("about.source"), link)));
    }
}
