namespace Vibling;

record Skin(string Id, Color Body, Color Shade, Color Spark, Color Eye, Color Outline)
{
    public Color Accent => Id == "night" ? Spark : Id == "custom" && IsDark ? Eye : Body;
    public string Name => L.T("skin." + Id);
    public bool IsDark => Body.R * 0.299 + Body.G * 0.587 + Body.B * 0.114 < 110;
}

record Accessory(string Id)
{
    public string Name => L.T("acc." + Id);
}

static class Looks
{
    static Color C(int r, int g, int b) => Color.FromArgb(r, g, b);

    public static readonly Skin[] Skins =
    {
        new("classic", C(217, 119, 87),  C(178, 88, 60),  C(255, 196, 160), C(24, 18, 22),    C(42, 24, 18)),
        new("mint", C(111, 211, 181), C(67, 168, 140), C(216, 255, 241), C(20, 32, 30),    C(18, 48, 40)),
        new("blueberry", C(120, 124, 232), C(84, 87, 190),  C(214, 215, 255), C(18, 16, 40),    C(22, 20, 58)),
        new("sakura", C(245, 154, 184), C(209, 110, 145), C(255, 224, 234), C(40, 16, 26),   C(70, 24, 44)),
        new("lemon", C(242, 207, 74),  C(201, 164, 42), C(255, 243, 184), C(40, 30, 10),    C(64, 48, 10)),
        new("night", C(48, 50, 74),    C(32, 33, 52),   C(127, 233, 255), C(127, 233, 255), C(10, 10, 18)),
        new("ghost", C(236, 238, 248), C(200, 204, 224), C(255, 255, 255), C(40, 40, 60),   C(90, 94, 120)),
        new("gold", C(232, 182, 76),  C(185, 138, 44), C(255, 240, 176), C(40, 26, 8),     C(70, 44, 8)),
        new("granite", C(139, 141, 152), C(106, 108, 118), C(208, 210, 218), C(20, 20, 26),   C(36, 36, 44)),
    };

    public static readonly Accessory[] Accessories =
    {
        new("none"), new("headphones"), new("cap"), new("beanie"), new("crown"), new("tophat"),
        new("santa"), new("partyhat"), new("propeller"), new("catears"), new("horns"), new("bow"),
        new("flower"), new("halo"), new("shades"), new("bowtie"), new("scarf"), new("bell"),
    };

    public static Skin Custom { get; private set; } = MakeCustom(C(217, 119, 87), C(24, 18, 22), C(42, 24, 18));

    public static void SetCustom(Color body, Color eye, Color outline) => Custom = MakeCustom(body, eye, outline);

    static Skin MakeCustom(Color body, Color eye, Color outline) =>
        new("custom", body, Mix(body, Color.Black, 0.2f), Mix(body, Color.White, 0.5f), eye, outline);

    static Color Mix(Color a, Color b, float t) =>
        C((int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));

    public static Skin SkinById(string id) => id == "custom" ? Custom : Skins.FirstOrDefault(s => s.Id == id) ?? Skins[0];

    static readonly string[] DrawOrder =
    {
        "bowtie", "scarf", "bell", "shades", "headphones", "catears", "horns", "beanie", "cap", "tophat",
        "santa", "partyhat", "propeller", "crown", "halo", "flower", "bow",
    };

    public static IEnumerable<string> Ordered(IEnumerable<string> wear) => DrawOrder.Where(wear.Contains);
}

enum Legs { Stand, Walk, Run, Dangle, Drift, Tuck, Brace, Skid, Hidden }
enum Arms { Side, Swing, Up, Flail, Wave, Typing, Out, Umbrella, Struggle }
enum Eyes { Open, Blink, Happy, Closed, Wide, Squint, Angry }
enum Mouth { None, Smile, O, Open, Chew, Yawn, Frown }

class Pose
{
    public bool Sit, Laptop, LaptopLit, Blush, Umbrella, Flower;
    public Legs Legs;
    public Arms Arms;
    public Eyes Eyes;
    public Mouth Mouth;
    public int LookX, LookY, Paw = -1;
    public float Phase;

    public void Reset(float phase)
    {
        Sit = Laptop = LaptopLit = Blush = Umbrella = Flower = false;
        Legs = Legs.Stand; Arms = Arms.Side; Eyes = Eyes.Open; Mouth = Mouth.None;
        LookX = LookY = 0; Paw = -1; Phase = phase;
    }
}

static class Sprite
{
    const int OX = 4, OY = 10, GW = 30, GH = 30;
    static readonly Color?[,] grid = new Color?[GW, GH];
    static readonly Dictionary<Color, SolidBrush> brushes = new();

    static readonly Color Metal = Color.FromArgb(70, 74, 92);
    static readonly Color MetalLight = Color.FromArgb(150, 156, 178);
    static readonly Color Neon = Color.FromArgb(127, 233, 255);
    static readonly Color BlushC = Color.FromArgb(255, 140, 150);
    static readonly Color Dark = Color.FromArgb(40, 36, 52);
    static readonly Color Red = Color.FromArgb(232, 72, 85);
    static readonly Color RedDark = Color.FromArgb(176, 42, 58);
    static readonly Color Knit = Color.FromArgb(92, 140, 230);
    static readonly Color KnitDark = Color.FromArgb(62, 102, 186);
    static readonly Color Gold = Color.FromArgb(255, 208, 72);
    static readonly Color Pink = Color.FromArgb(255, 110, 170);
    static readonly Color PinkDark = Color.FromArgb(214, 70, 130);
    static readonly Color White = Color.FromArgb(250, 250, 255);
    static readonly Color Black = Color.FromArgb(16, 14, 20);
    static readonly Color Green = Color.FromArgb(96, 196, 110);
    static readonly Color Blue = Color.FromArgb(70, 130, 230);
    static readonly Color Silver = Color.FromArgb(196, 200, 214);
    static readonly Color SilverLight = Color.FromArgb(232, 234, 242);
    static readonly Color UmbA = Color.FromArgb(232, 72, 85);
    static readonly Color UmbB = Color.FromArgb(255, 245, 235);

    static void Cell(int c, int r, Color col)
    {
        int ci = c + OX, ri = r + OY;
        if (ci >= 0 && ci < GW && ri >= 0 && ri < GH) grid[ci, ri] = col;
    }

    static void Rect(int c0, int r0, int c1, int r1, Color col)
    {
        for (int c = c0; c <= c1; c++)
            for (int r = r0; r <= r1; r++) Cell(c, r, col);
    }

    static void Build(Pose p, Skin s, IEnumerable<string> wear)
    {
        Array.Clear(grid);
        int by = p.Sit ? 1 : 0;
        int ph2(float speed) => (int)(p.Phase * speed) % 2;

        Rect(3, 3 + by, 14, 9 + by, s.Body);
        Rect(3, 9 + by, 14, 9 + by, s.Shade);

        if (!p.Sit && p.Legs == Legs.Skid)
        {
            Rect(4, 10, 4, 11, s.Shade); Rect(6, 10, 6, 11, s.Shade);
            Cell(12, 10, s.Shade); Cell(13, 11, s.Shade);
            Cell(14, 10, s.Shade); Cell(15, 11, s.Shade);
        }
        else if (!p.Sit && p.Legs != Legs.Hidden)
        {
            int[] cols = p.Legs == Legs.Brace ? new[] { 3, 5, 12, 14 } : new[] { 4, 6, 11, 13 };
            for (int i = 0; i < 4; i++)
            {
                int len = 2;
                switch (p.Legs)
                {
                    case Legs.Walk:
                    case Legs.Run:
                        len = i % 2 == ph2(p.Legs == Legs.Run ? 14 : 9) ? 2 : 1;
                        break;
                    case Legs.Dangle: len = 2 + ((int)(p.Phase * 5) + i) % 2; break;
                    case Legs.Drift:
                        len = i is 1 or 2 ? 3 : 2;
                        if (p.Phase % 5f < 0.4f && (int)(p.Phase / 5f) % 4 == i) len++;
                        break;
                    case Legs.Tuck: len = 1; break;
                }
                Rect(cols[i], 10, cols[i], 9 + len, s.Shade);
            }
        }

        switch (p.Arms)
        {
            case Arms.Typing:
            {
                int l = p.Paw == 0 ? 9 : 10, r = p.Paw == 1 ? 9 : 10;
                Rect(1, l, 2, l, s.Body); Rect(15, r, 16, r, s.Body);
                break;
            }
            case Arms.Up:
                ArmUp(s.Body, by, 0);
                break;
            case Arms.Flail:
                ArmUp(s.Body, by, ph2(5));
                break;
            case Arms.Wave:
            {
                Rect(1, 6 + by, 2, 7 + by, s.Body);
                int f = ph2(6);
                Rect(15, 2 + f + by, 16, 3 + f + by, s.Body);
                break;
            }
            case Arms.Out:
            {
                int f = ph2(5);
                Rect(0, 5 + f + by, 2, 5 + f + by, s.Body); Rect(15, 6 - f + by, 17, 6 - f + by, s.Body);
                break;
            }
            case Arms.Umbrella:
                Rect(1, 6 + by, 2, 7 + by, s.Body); Rect(15, 2, 16, 4, s.Body);
                break;
            case Arms.Struggle:
            {
                int f = ph2(7);
                Rect(1, 6 + by, 2, 7 + by, s.Body); Rect(f, 8 + by, f + 1, 9 + by, s.Body);
                Rect(15, 6 + by, 16, 7 + by, s.Body); Rect(16 - f, 8 + by, 17 - f, 9 + by, s.Body);
                break;
            }
            case Arms.Swing:
            {
                int d = ph2(9);
                Rect(1, 6 + by + d, 2, 7 + by + d, s.Body); Rect(15, 7 + by - d, 16, 8 + by - d, s.Body);
                break;
            }
            default:
                Rect(1, 6 + by, 2, 7 + by, s.Body); Rect(15, 6 + by, 16, 7 + by, s.Body);
                break;
        }

        int lc = p.LookX, e0 = 5 + by + p.LookY;
        switch (p.Eyes)
        {
            case Eyes.Open:
                Rect(6 + lc, e0, 6 + lc, e0 + 1, s.Eye); Rect(11 + lc, e0, 11 + lc, e0 + 1, s.Eye);
                break;
            case Eyes.Blink:
                Cell(6 + lc, e0 + 1, s.Eye); Cell(11 + lc, e0 + 1, s.Eye);
                break;
            case Eyes.Happy:
                Cell(5, 6 + by, s.Eye); Cell(6, 5 + by, s.Eye); Cell(7, 6 + by, s.Eye);
                Cell(10, 6 + by, s.Eye); Cell(11, 5 + by, s.Eye); Cell(12, 6 + by, s.Eye);
                break;
            case Eyes.Closed:
                Rect(5, 6 + by, 6, 6 + by, s.Eye); Rect(11, 6 + by, 12, 6 + by, s.Eye);
                break;
            case Eyes.Wide:
                Rect(6 + lc, 4 + by, 6 + lc, 5 + by, s.Eye); Rect(11 + lc, 4 + by, 11 + lc, 5 + by, s.Eye);
                break;
            case Eyes.Squint:
                Cell(5, 4 + by, s.Eye); Cell(6, 5 + by, s.Eye); Cell(5, 6 + by, s.Eye);
                Cell(12, 4 + by, s.Eye); Cell(11, 5 + by, s.Eye); Cell(12, 6 + by, s.Eye);
                break;
            case Eyes.Angry:
                Cell(5, 3 + by, s.Eye); Cell(6, 4 + by, s.Eye); Cell(12, 3 + by, s.Eye); Cell(11, 4 + by, s.Eye);
                Rect(6 + lc, 5 + by, 6 + lc, 6 + by, s.Eye); Rect(11 + lc, 5 + by, 11 + lc, 6 + by, s.Eye);
                break;
        }

        Color m = s.Outline;
        switch (p.Mouth)
        {
            case Mouth.O: Rect(8, 8 + by, 9, 8 + by, m); break;
            case Mouth.Open: Rect(8, 8 + by, 9, 8 + by, m); break;
            case Mouth.Chew: if (ph2(8) == 0) Rect(8, 7 + by, 9, 8 + by, m); else Rect(8, 8 + by, 9, 8 + by, m); break;
            case Mouth.Yawn: Rect(7, 7 + by, 10, 8 + by, m); break;
            case Mouth.Frown: Rect(8, 7 + by, 9, 7 + by, m); Cell(7, 8 + by, m); Cell(10, 8 + by, m); break;
        }
        if (p.Blush) { Cell(4, 7 + by, BlushC); Cell(13, 7 + by, BlushC); }

        foreach (var acc in Looks.Ordered(wear))
        switch (acc)
        {
            case "headphones":
                Rect(3, 2 + by, 14, 2 + by, Dark);
                Cell(2, 3 + by, Dark); Cell(15, 3 + by, Dark);
                Rect(2, 4 + by, 2, 5 + by, Pink); Rect(15, 4 + by, 15, 5 + by, Pink);
                break;
            case "cap":
                Rect(3, 1 + by, 14, 2 + by, Red);
                Rect(13, 2 + by, 17, 2 + by, RedDark);
                Rect(8, 0 + by, 9, 0 + by, Red);
                break;
            case "beanie":
                Rect(3, 1 + by, 14, 2 + by, Knit);
                Rect(5, 0 + by, 12, 0 + by, Knit);
                Rect(3, 2 + by, 14, 2 + by, KnitDark);
                Rect(8, -1 + by, 9, -1 + by, White);
                break;
            case "crown":
                Rect(4, 2 + by, 13, 2 + by, Gold);
                Cell(4, 1 + by, Gold); Cell(13, 1 + by, Gold); Rect(8, 0 + by, 9, 1 + by, Gold);
                Cell(6, 2 + by, Red); Cell(11, 2 + by, Red);
                break;
            case "shades":
                Rect(4, 5 + by, 7, 6 + by, Black); Rect(10, 5 + by, 13, 6 + by, Black);
                Rect(8, 5 + by, 9, 5 + by, Black);
                Cell(5, 5 + by, MetalLight); Cell(11, 5 + by, MetalLight);
                break;
            case "bow":
                Bow(0 + by, Pink, PinkDark);
                break;
            case "bowtie":
                Bow(8 + by, Red, RedDark, 3);
                break;
            case "tophat":
                Rect(5, -2 + by, 12, 1 + by, Black);
                Rect(5, 0 + by, 12, 0 + by, Red);
                Rect(3, 2 + by, 14, 2 + by, Black);
                break;
            case "santa":
                Rect(5, 0 + by, 12, 1 + by, Red);
                Rect(9, -1 + by, 14, -1 + by, Red);
                Rect(15, -1 + by, 15, 0 + by, White);
                Rect(3, 2 + by, 14, 2 + by, White);
                break;
            case "partyhat":
                Rect(8, -3 + by, 9, -3 + by, White);
                Rect(8, -2 + by, 9, -2 + by, Pink);
                Rect(7, -1 + by, 10, -1 + by, Gold);
                Rect(7, 0 + by, 10, 0 + by, Pink);
                Rect(6, 1 + by, 11, 1 + by, Gold);
                Rect(6, 2 + by, 11, 2 + by, Pink);
                break;
            case "propeller":
                Rect(4, 2 + by, 8, 2 + by, Red); Rect(9, 2 + by, 13, 2 + by, Blue);
                Rect(6, 1 + by, 8, 1 + by, Red); Rect(9, 1 + by, 11, 1 + by, Blue);
                Rect(8, 0 + by, 9, 0 + by, Dark);
                if ((int)(p.Phase * 10) % 2 == 0) Rect(5, -1 + by, 12, -1 + by, Gold);
                else Rect(7, -1 + by, 10, -1 + by, Gold);
                break;
            case "catears":
                Cell(4, 1 + by, s.Body); Rect(3, 2 + by, 5, 2 + by, s.Body); Cell(4, 2 + by, BlushC);
                Cell(13, 1 + by, s.Body); Rect(12, 2 + by, 14, 2 + by, s.Body); Cell(13, 2 + by, BlushC);
                break;
            case "horns":
                Cell(3, 0 + by, Red); Rect(4, 1 + by, 4, 2 + by, Red); Cell(5, 2 + by, Red);
                Cell(14, 0 + by, Red); Rect(13, 1 + by, 13, 2 + by, Red); Cell(12, 2 + by, Red);
                break;
            case "flower":
                Rect(8, 1 + by, 9, 2 + by, Green);
                Cell(10, 2 + by, Green);
                Rect(8, -3 + by, 9, -2 + by, Pink);
                Rect(6, -1 + by, 7, 0 + by, Pink);
                Rect(10, -1 + by, 11, 0 + by, Pink);
                Rect(8, -1 + by, 9, 0 + by, Gold);
                break;
            case "scarf":
                Rect(3, 9 + by, 14, 9 + by, Red);
                for (int c = 4; c <= 14; c += 3) Cell(c, 9 + by, White);
                Rect(12, 10 + by, 12, 11 + by, Red);
                break;
            case "bell":
                Rect(3, 9 + by, 14, 9 + by, Red);
                Rect(8, 9 + by, 9, 10 + by, Gold);
                break;
            case "halo":
                Rect(5, -2 + by, 12, -2 + by, Gold);
                break;
        }

        if (p.Flower)
        {
            Rect(15, 6 + by, 17, 7 + by, s.Body);
            Rect(18, 3 + by, 18, 6 + by, Green); Cell(19, 4 + by, Green);
            Rect(17, -1 + by, 19, 1 + by, Pink); Cell(16, 0 + by, Pink); Cell(20, 0 + by, Pink); Cell(18, -2 + by, Pink); Cell(18, 2 + by, Pink);
            Cell(18, 0 + by, Gold);
        }
        if (p.Laptop)
        {
            Color lid = s.IsDark ? Silver : Metal, deck = s.IsDark ? SilverLight : MetalLight;
            Rect(5, 8, 12, 10, lid);
            Rect(8, 9, 9, 9, p.LaptopLit ? Neon : deck);
            Rect(2, 11, 15, 11, deck);
        }

        if (p.Umbrella)
        {
            Rect(16, -4, 16, 1, Dark);
            Cell(16, -8, UmbA);
            Rect(13, -7, 19, -7, UmbA);
            Rect(11, -6, 21, -6, UmbA);
            Rect(10, -5, 22, -5, UmbA);
            for (int c = 10; c <= 22; c++)
                if ((c / 3) % 2 == 0) { Cell(c, -5, UmbB); if (c >= 11 && c <= 21) Cell(c, -6, UmbB); }
        }
    }

    static void ArmUp(Color c, int by, int lower)
    {
        Rect(1, 4 + by, 2, 5 + by, c); Rect(0, 2 + by + lower, 1, 3 + by + lower, c);
        Rect(15, 4 + by, 16, 5 + by, c); Rect(16, 2 + by + lower, 17, 3 + by + lower, c);
    }

    static void Bow(int top, Color wing, Color knot, int h = 4)
    {
        int mid = top + (h - 1) / 2;
        Rect(5, top, 6, top + h - 1, wing);
        Rect(11, top, 12, top + h - 1, wing);
        Rect(7, mid, 7, top + h / 2, wing);
        Rect(10, mid, 10, top + h / 2, wing);
        Rect(8, mid, 9, top + h / 2, knot);
    }

    static SolidBrush Brush(Color c)
    {
        if (!brushes.TryGetValue(c, out var b)) brushes[c] = b = new SolidBrush(c);
        return b;
    }

    public static void Draw(Graphics g, Pose p, Skin s, string acc, float cx, float feetY, float P,
                            int face = 1, float sqX = 1, float sqY = 1, float angle = 0) =>
        Draw(g, p, s, new[] { acc }, cx, feetY, P, face, sqX, sqY, angle);

    public static void Draw(Graphics g, Pose p, Skin s, IEnumerable<string> wear, float cx, float feetY, float P,
                            int face = 1, float sqX = 1, float sqY = 1, float angle = 0)
    {
        Build(p, s, wear);
        var state = g.Save();
        g.TranslateTransform(cx, feetY);
        if (Math.Abs(angle) > 0.01f)
        {
            float pivot = 6.5f * P * sqY;
            g.TranslateTransform(0, -pivot);
            g.RotateTransform(angle);
            g.TranslateTransform(0, pivot);
        }
        g.ScaleTransform(face * sqX, sqY);

        var ob = Brush(s.Outline);
        for (int ci = 0; ci < GW; ci++)
            for (int ri = 0; ri < GH; ri++)
            {
                if (grid[ci, ri] == null) continue;
                float px = (ci - OX - 9) * P, py = (ri - OY - 12) * P;
                g.FillRectangle(ob, px - P, py, P * 3, P);
                g.FillRectangle(ob, px, py - P, P, P * 3);
            }
        for (int ci = 0; ci < GW; ci++)
            for (int ri = 0; ri < GH; ri++)
            {
                if (grid[ci, ri] is not Color col) continue;
                g.FillRectangle(Brush(col), (ci - OX - 9) * P, (ri - OY - 12) * P, P, P);
            }
        g.Restore(state);
    }
}
