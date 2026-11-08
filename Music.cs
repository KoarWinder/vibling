using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace Vibling;

class Biquad
{
    double b0, b1, b2, a1, a2, x1, x2, y1, y2;

    public Biquad(char type, double f, double sr) => Set(type, f, sr);

    public void Set(char type, double f, double sr)
    {
        double q = type == 'b' ? 1 : 0.707;
        double w = 2 * Math.PI * Math.Clamp(f, 20, sr * 0.45) / sr, cs = Math.Cos(w), al = Math.Sin(w) / (2 * q);
        double B0, B1, B2;
        switch (type)
        {
            case 'l': B0 = (1 - cs) / 2; B1 = 1 - cs; B2 = (1 - cs) / 2; break;
            case 'h': B0 = (1 + cs) / 2; B1 = -(1 + cs); B2 = (1 + cs) / 2; break;
            default: B0 = al; B1 = 0; B2 = -al; break;
        }
        double A0 = 1 + al;
        b0 = B0 / A0; b1 = B1 / A0; b2 = B2 / A0; a1 = -2 * cs / A0; a2 = (1 - al) / A0;
    }

    public double Run(double x)
    {
        double y = b0 * x + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2;
        x2 = x1; x1 = x; y2 = y1; y1 = y;
        return y;
    }
}

class Voice
{
    public long Start, End, AttackEnd, SweepEnd;
    public int Wave;
    public double Freq, FreqMul = 1, Gain, AttackMul = 1, DecayMul = 1, Send, Pan;
    public double ModRatio = 1, ModIndex, ModMul = 1;
    public bool Wobble, Duck;
    public Biquad Filter;
    double phase, mphase;

    public double Next(long n, double wob, float[] noise, ref int ni)
    {
        if (n < Start) return 0;
        double s;
        if (Wave == 4) { s = noise[ni]; ni = (ni + 1) % noise.Length; }
        else
        {
            double f = Wobble ? Freq * wob : Freq;
            phase += f / LofiSynth.SR;
            if (phase >= 1) phase -= 1;
            switch (Wave)
            {
                case 0: s = Math.Sin(phase * 2 * Math.PI); break;
                case 1: s = 1 - 4 * Math.Abs(phase - 0.5); break;
                default:
                    mphase += f * ModRatio / LofiSynth.SR;
                    if (mphase >= 1) mphase -= 1;
                    s = Math.Sin(phase * 2 * Math.PI + ModIndex * Math.Sin(mphase * 2 * Math.PI));
                    ModIndex *= ModMul;
                    break;
            }
            if (n < SweepEnd) Freq *= FreqMul;
        }
        if (Filter != null) s = Filter.Run(s);
        s *= Gain;
        Gain *= n < AttackEnd ? AttackMul : DecayMul;
        return s;
    }
}

class LofiTrack
{
    public static readonly string[] Styles = { "calm", "classic", "jazz", "rainy", "night", "sunny" };

    public string Name, Key, Mode, Style;
    public int Bpm, RootM, KeysKind, LeadKind, BassKind;
    public int[] Scale;
    public double Swing, Cut, Dly, MelDensity, HatLevel;
    public bool Ninths, Rain;
    public (int bass, int[] notes)[][] Progs;
    public int[] HarmRhythm;
    public (int[] kick, int[] snare, int[] rim)[] Grooves;
    public double[][] Hats;
    public int[][] Motifs;
    public List<Section> Sections = new();
    public int TotalBars;
    public Func<double> R;

    public class Section
    {
        public string Kind;
        public int Bars;
        public int Drums;
        public bool Bass, Comp = true, Half, Final;
        public int Lead = -1, Prog, Groove, Hat, CompPat, Fill;
        public double Filter = 1, FilterFrom = -1, Level = 1, LevelFrom = -1, LeadVol = 0.16;
        public Section(string kind, int bars) { Kind = kind; Bars = bars; }
    }

    public static readonly (int at, int len)[][] CompPats =
    {
        new[] { (0, 15) },
        new[] { (0, 9), (10, 6) },
        new[] { (0, 3), (6, 3), (11, 3) },
        new[] { (0, 7), (8, 7) },
        new[] { (0, 5), (6, 4), (10, 6) },
        new[] { (0, 2), (3, 2), (8, 3), (14, 2) },
        new[] { (2, 6), (10, 6) },
        new[] { (0, 30) },
    };

    public int Deg(int d) => RootM + Scale[((d % 7) + 7) % 7] + 12 * (int)Math.Floor(d / 7.0);

    public (Section sec, int bar) SectionAt(int bar)
    {
        int at = 0;
        foreach (var sec in Sections)
        {
            if (bar < at + sec.Bars) return (sec, bar - at);
            at += sec.Bars;
        }
        return (new Section("tail", 1) { Comp = false }, 0);
    }

    public static string StyleName(string style, bool ru) => style switch
    {
        "calm" => ru ? "спокойный" : "calm",
        "classic" => ru ? "классический" : "classic",
        "jazz" => ru ? "джазовый" : "jazzy",
        "rainy" => ru ? "дождливый" : "rainy",
        "night" => ru ? "ночной" : "night",
        "sunny" => ru ? "солнечный" : "sunny",
        _ => ru ? "любой" : "any",
    };

    static readonly (string ru, string en, int[] s)[] Scales =
    {
        ("минор", "minor", new[] { 0, 2, 3, 5, 7, 8, 10 }), ("дорийский", "dorian", new[] { 0, 2, 3, 5, 7, 9, 10 }),
        ("мажор", "major", new[] { 0, 2, 4, 5, 7, 9, 11 }), ("миксолидийский", "mixolydian", new[] { 0, 2, 4, 5, 7, 9, 10 }),
        ("фригийский", "phrygian", new[] { 0, 1, 3, 5, 7, 8, 10 }), ("лидийский", "lydian", new[] { 0, 2, 4, 6, 7, 9, 11 }),
        ("гармонический минор", "harmonic minor", new[] { 0, 2, 3, 5, 7, 8, 11 }), ("мелодический минор", "melodic minor", new[] { 0, 2, 3, 5, 7, 9, 11 }),
    };
    static readonly string[] Keys = { "C", "C#", "D", "Eb", "E", "F", "F#", "G", "Ab", "A", "Bb", "B" };
    static readonly int[][] ProgPool =
    {
        new[] { 0, 5, 3, 4 }, new[] { 0, 3, 4, 3 }, new[] { 0, 4, 5, 3 }, new[] { 1, 4, 0, 0 }, new[] { 0, 2, 3, 4 },
        new[] { 5, 3, 0, 4 }, new[] { 0, 5, 1, 4 }, new[] { 0, 3, 5, 4 }, new[] { 0, 0, 3, 4 }, new[] { 0, 6, 5, 4 },
        new[] { 1, 4, 2, 5 }, new[] { 3, 4, 2, 5 }, new[] { 0, 2, 5, 4 }, new[] { 5, 4, 3, 4 }, new[] { 3, 2, 1, 4 }, new[] { 0, 1, 3, 4 },
    };
    static readonly string[] AdjRu = { "Неоновый", "Лунный", "Тёплый", "Ночной", "Ленивый", "Стеклянный", "Медленный", "Пыльный", "Полуночный", "Хмурый", "Сонный", "Дальний", "Розовый", "Забытый", "Бархатный", "Пиксельный", "Дождливый", "Мятный", "Тихий", "Ламповый", "Кофейный", "Осенний", "Звёздный", "Уютный" };
    static readonly string[] NounRu = { "рейс", "дождь", "туман", "закат", "маршрут", "ветер", "сон", "город", "вайб", "рассвет", "проспект", "вечер", "полёт", "бит", "коммит", "кофе", "плейлист", "чердак", "трамвай", "балкон", "подоконник", "понедельник", "плед", "чай" };
    static readonly string[] AdjEn = { "Neon", "Lunar", "Warm", "Night", "Lazy", "Glass", "Slow", "Dusty", "Midnight", "Cloudy", "Sleepy", "Distant", "Pink", "Forgotten", "Velvet", "Pixel", "Rainy", "Mint", "Quiet", "Tube", "Coffee", "Autumn", "Starry", "Cozy" };
    static readonly string[] NounEn = { "ride", "rain", "fog", "sunset", "route", "wind", "dream", "city", "vibe", "dawn", "avenue", "evening", "flight", "beat", "commit", "coffee", "playlist", "attic", "tram", "balcony", "windowsill", "monday", "blanket", "tea" };

    public static Func<double> Mulberry(uint a) => () =>
    {
        unchecked
        {
            a += 0x6D2B79F5;
            uint t = a;
            t = (t ^ (t >> 15)) * (1 | t);
            t ^= t + (t ^ (t >> 7)) * (61 | t);
            return ((t ^ (t >> 14)) >> 0) / 4294967296.0;
        }
    };

    public static LofiTrack Make(uint seed, bool ru, string style)
    {
        var r = Mulberry(seed);
        T Pick<T>(T[] a) => a[(int)(r() * a.Length)];
        int Ri(int a, int b) => a + (int)(r() * (b - a + 1));

        var t = new LofiTrack { R = Mulberry(seed + 999) };
        t.Style = Array.IndexOf(Styles, style) >= 0 ? style : Pick(Styles);

        int[] scales = t.Style switch
        {
            "calm" => new[] { 2, 5, 1, 3 },
            "classic" => new[] { 0, 1, 1, 7 },
            "jazz" => new[] { 1, 3, 7, 2 },
            "rainy" => new[] { 0, 1, 7 },
            "night" => new[] { 0, 4, 6, 1 },
            _ => new[] { 2, 5, 3 },
        };
        var sc = Scales[Pick(scales)];
        int key = Ri(0, 11);
        t.Scale = sc.s;
        t.Mode = ru ? sc.ru : sc.en;
        t.Key = Keys[key];
        t.RootM = 45 + (key > 5 ? key - 12 : key);

        (t.Bpm, t.Swing, t.Cut) = t.Style switch
        {
            "calm" => (Ri(64, 74), 0.12, Ri(2600, 3400)),
            "classic" => (Ri(74, 88), 0.16, Ri(2800, 3800)),
            "jazz" => (Ri(80, 92), 0.22, Ri(3200, 4200)),
            "rainy" => (Ri(62, 72), 0.12, Ri(2000, 2700)),
            "night" => (Ri(68, 80), 0.14, Ri(1800, 2500)),
            _ => (Ri(78, 90), 0.1, Ri(3800, 5000)),
        };
        t.Ninths = t.Style is "jazz" or "calm" or "sunny" || r() < 0.4;
        t.Rain = t.Style == "rainy";
        t.Dly = 0.2 + r() * 0.25;

        int[] tones = t.Ninths ? new[] { 0, 2, 4, 6, 8 } : new[] { 0, 2, 4, 6 };
        (int, int[]) Chord(int d)
        {
            var notes = tones.Select(k => t.Deg(d + k) + 12).ToArray();
            if (r() < 0.35) notes[0] += 12;
            Array.Sort(notes);
            return (t.Deg(d), notes);
        }
        (int, int[])[] Prog()
        {
            var pr = (int[])Pick(ProgPool).Clone();
            if (r() < 0.3) pr[3] = Ri(0, 6);
            if (r() < 0.25) pr = pr.Concat(new[] { pr[0], pr[1], Ri(0, 6), pr[3] }).ToArray();
            if (r() < 0.15) pr = new[] { pr[0], pr[1] };
            return pr.Select(Chord).ToArray();
        }
        t.Progs = new[] { Prog(), Prog() };
        t.HarmRhythm = new[] { r() < 0.2 ? 2 : 1, r() < 0.35 ? 2 : 1 };

        (t.KeysKind, t.LeadKind, t.BassKind) = t.Style switch
        {
            "calm" => (Pick(new[] { 1, 0, 2 }), Pick(new[] { 4, 3, 2 }), Pick(new[] { 0, 3 })),
            "classic" => (Pick(new[] { 0, 2 }), Pick(new[] { 2, 0, 3 }), Pick(new[] { 1, 0 })),
            "jazz" => (Pick(new[] { 0, 2 }), Pick(new[] { 0, 2, 4 }), 2),
            "rainy" => (Pick(new[] { 1, 2 }), Pick(new[] { 3, 4, 2 }), Pick(new[] { 0, 3 })),
            "night" => (Pick(new[] { 1, 0 }), Pick(new[] { 4, 0 }), Pick(new[] { 3, 1 })),
            _ => (Pick(new[] { 0, 2 }), Pick(new[] { 3, 2 }), Pick(new[] { 1, 0 })),
        };
        int[] compPool = t.Style switch
        {
            "calm" => new[] { 0, 7, 3, 6 },
            "classic" => new[] { 1, 3, 4, 6 },
            "jazz" => new[] { 2, 4, 6 },
            "rainy" => new[] { 0, 7, 3 },
            "night" => new[] { 0, 3, 7, 6 },
            _ => new[] { 5, 3, 4, 1 },
        };

        (int[], int[], int[])[] pool = t.Style switch
        {
            "calm" => new[] { (new[] { 0, 9 }, new[] { 8 }, new int[0]), (new[] { 0, 10 }, new[] { 8 }, new int[0]), (new[] { 0 }, new int[0], new[] { 8 }), (new[] { 0, 7 }, new[] { 8 }, new int[0]), (new[] { 0, 6, 9 }, new int[0], new[] { 8 }) },
            "classic" => new[] { (new[] { 0, 7, 10 }, new[] { 4, 12 }, new int[0]), (new[] { 0, 10 }, new[] { 4, 12 }, new int[0]), (new[] { 0, 3, 10 }, new[] { 4, 12 }, new int[0]), (new[] { 0, 8, 11 }, new[] { 4, 12 }, new int[0]), (new[] { 0, 6, 10 }, new[] { 4, 12 }, new[] { 15 }) },
            "jazz" => new[] { (new[] { 0, 10 }, new int[0], new[] { 4, 12 }), (new[] { 0, 7 }, new int[0], new[] { 4, 12 }), (new[] { 0, 11 }, new[] { 12 }, new[] { 4 }), (new[] { 0, 6, 10 }, new[] { 4, 12 }, new int[0]) },
            "rainy" => new[] { (new[] { 0 }, new int[0], new[] { 8 }), (new[] { 0, 10 }, new int[0], new[] { 8 }), (new[] { 0, 9 }, new[] { 8 }, new int[0]), (new[] { 0, 7 }, new int[0], new[] { 12 }) },
            "night" => new[] { (new[] { 0, 7, 10 }, new[] { 8 }, new int[0]), (new[] { 0, 10 }, new[] { 8 }, new int[0]), (new[] { 0, 3, 7 }, new[] { 8 }, new[] { 14 }), (new[] { 0, 9 }, new int[0], new[] { 4, 12 }) },
            _ => new[] { (new[] { 0, 8, 10 }, new[] { 4, 12 }, new int[0]), (new[] { 0, 6, 10 }, new[] { 4, 12 }, new int[0]), (new[] { 0, 7, 8 }, new[] { 4, 12 }, new[] { 2 }), (new[] { 0, 10 }, new[] { 4, 12 }, new[] { 7 }) },
        };
        int g0 = (int)(r() * pool.Length), g1 = (g0 + 1 + (int)(r() * (pool.Length - 1))) % pool.Length;
        t.Grooves = new[] { pool[g0], pool[g1] };
        t.HatLevel = t.Style switch { "calm" => 0.032, "rainy" => 0.02, "night" => 0.03, "jazz" => 0.045, "sunny" => 0.042, _ => 0.05 };
        double[] HatPat()
        {
            var h = new double[16];
            switch (Ri(0, 5))
            {
                case 0: for (int i = 0; i < 16; i += 2) h[i] = 1; break;
                case 1: for (int i = 0; i < 16; i++) h[i] = i % 2 == 0 ? 1 : r() < 0.4 ? 0.4 : 0; break;
                case 2: for (int i = 0; i < 16; i += 4) h[i] = 1; break;
                case 3: for (int i = 0; i < 16; i++) h[i] = i % 4 == 0 ? 1 : i % 2 == 0 ? 0.6 : 0.35; break;
                case 4: for (int i = 2; i < 16; i += 4) h[i] = 1; break;
                default: for (int i = 0; i < 16; i++) h[i] = i % 4 == 0 ? 1 : i % 4 == 3 ? 0.7 : 0; break;
            }
            return h;
        }
        t.Hats = new[] { HatPat(), HatPat() };

        t.MelDensity = t.Style switch { "calm" => 0.45, "rainy" => 0.4, "night" => 0.4, "jazz" => 0.65, "sunny" => 0.7, _ => 0.55 };
        int[] Motif()
        {
            int i = Ri(3, 6);
            int shift = Ri(0, 3);
            return Enumerable.Range(0, 32).Select(s =>
            {
                int q = (s + shift) % 32;
                double p = q % 8 == 0 ? 0.75 : q % 4 == 0 ? 0.45 : q % 2 == 0 ? 0.25 : 0.06;
                if (r() < p * t.MelDensity) { i = Math.Clamp(i + Ri(-2, 2), 0, 9); return i; }
                return -1;
            }).ToArray();
        }
        t.Motifs = new[] { Motif(), Motif(), Motif() };

        var S = t.Sections;
        int Len(params int[] a) => Pick(a);
        int fills = 0;
        int Fill() => r() < 0.75 ? Ri(1, 4) : 0;

        switch (Pick(new[] { "keys", "keys", "drums", "lead", "bass", "vinyl", "cold", "filtered" }))
        {
            case "keys":
                S.Add(new Section("intro", Len(2, 4, 4, 8)) { FilterFrom = 0.18, Filter = 0.8, CompPat = Pick(compPool) });
                break;
            case "drums":
                S.Add(new Section("intro", Len(1, 2, 2)) { Comp = false, Drums = 1, Filter = 0.35, Groove = 0, Hat = 0 });
                S.Add(new Section("intro", Len(2, 4)) { Drums = 1, FilterFrom = 0.35, Filter = 0.75, CompPat = Pick(compPool), Groove = 0, Hat = 0 });
                break;
            case "lead":
                S.Add(new Section("intro", Len(4, 4, 2)) { CompPat = 7, Lead = Ri(0, 2), LeadVol = 0.13, FilterFrom = 0.3, Filter = 0.7 });
                break;
            case "bass":
                S.Add(new Section("intro", Len(2, 4)) { Bass = true, CompPat = Pick(compPool), FilterFrom = 0.25, Filter = 0.6 });
                break;
            case "vinyl":
                S.Add(new Section("intro", 1) { Comp = false, Filter = 0.2 });
                S.Add(new Section("intro", Len(2, 4)) { CompPat = Pick(compPool), FilterFrom = 0.2, Filter = 0.7 });
                break;
            case "filtered":
                S.Add(new Section("intro", Len(2, 4)) { Drums = 2, Bass = true, CompPat = Pick(compPool), Filter = 0.12, FilterFrom = 0.08 });
                break;
            default: break;
        }
        if (S.Count > 0) S[^1].Fill = Fill();

        string[] form = Pick(new[]
        {
            new[] { "A", "B", "A", "B" }, new[] { "A", "A", "B", "A" }, new[] { "A", "B", "C", "A" }, new[] { "A", "B", "brk", "B" },
            new[] { "A", "brk", "A", "B" }, new[] { "A", "B", "A", "brk", "B" }, new[] { "A", "C", "B", "A" }, new[] { "A", "B", "B", "brk", "A" },
            new[] { "A", "A", "brk", "B", "B" }, new[] { "A", "B", "C" },
        });
        int leadA = r() < 0.4 ? 0 : -1;
        int compA = Pick(compPool), compB = Pick(compPool), compC = Pick(compPool);
        bool firstA = true;
        foreach (var f in form)
        {
            Section sec = f switch
            {
                "A" => new Section("a", Len(8, 8, 4, 16)) { Drums = 2, Bass = true, CompPat = compA, Lead = firstA ? leadA : (r() < 0.6 ? 0 : 2), Groove = 0, Hat = 0 },
                "B" => new Section("b", Len(8, 8, 4)) { Drums = 2, Bass = true, CompPat = compB, Lead = 1, Prog = 1, Groove = r() < 0.6 ? 1 : 0, Hat = 1 },
                "C" => new Section("c", Len(4, 8)) { Drums = r() < 0.5 ? 2 : 1, Half = r() < 0.6, Bass = true, CompPat = compC, Lead = 2, Prog = r() < 0.5 ? 1 : 0, Groove = 1, Hat = 1, Filter = 0.8 },
                _ => new Section("break", Len(2, 4, 4)) { Drums = r() < 0.4 ? 1 : 0, Bass = r() < 0.5, CompPat = Pick(new[] { 0, 7, compB }), Lead = r() < 0.5 ? Ri(0, 2) : -1, LeadVol = 0.12, Filter = 0.5 },
            };
            if (f == "A") firstA = false;
            sec.Fill = Fill();
            fills++;
            S.Add(sec);
        }

        var last = S[^1];
        switch (Pick(new[] { "fade", "strip", "stop", "lead", "slow" }))
        {
            case "fade":
                S.Add(new Section("outro", Len(4, 8)) { Drums = 2, Bass = true, CompPat = last.CompPat, Lead = last.Lead, Prog = last.Prog, Groove = last.Groove, Hat = last.Hat, FilterFrom = 1, Filter = 0.15, LevelFrom = 1, Level = 0.05 });
                break;
            case "strip":
                S.Add(new Section("outro", 2) { Bass = true, CompPat = last.CompPat, Drums = 1, Prog = last.Prog });
                S.Add(new Section("outro", 2) { CompPat = last.CompPat, FilterFrom = 0.8, Filter = 0.45 });
                S.Add(new Section("outro", 2) { Final = true, Filter = 0.45 });
                break;
            case "stop":
                last.Fill = 4;
                S.Add(new Section("outro", 2) { Final = true, Filter = 0.75 });
                break;
            case "lead":
                S.Add(new Section("outro", 4) { CompPat = 7, Lead = Ri(0, 2), LeadVol = 0.13, FilterFrom = 0.8, Filter = 0.35 });
                S.Add(new Section("outro", 2) { Final = true, Filter = 0.35 });
                break;
            default:
                S.Add(new Section("outro", 4) { Bass = true, CompPat = 0, FilterFrom = 0.8, Filter = 0.3, LevelFrom = 1, Level = 0.4 });
                S.Add(new Section("outro", 2) { Final = true, Filter = 0.3, Level = 0.4 });
                break;
        }
        S.Add(new Section("tail", 1) { Comp = false, Filter = 0.1 });
        t.TotalBars = S.Sum(x => x.Bars);

        t.Name = ru ? Pick(AdjRu) + " " + Pick(NounRu) : Pick(AdjEn) + " " + Pick(NounEn);
        return t;
    }
}

class LofiSynth : ISampleProvider
{
    public const int SR = 44100;
    public WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(SR, 2);

    public volatile bool ForceNext;
    public volatile float Target;
    public volatile string Style = "any";
    public bool Russian = true;
    public LofiTrack Track { get; private set; }
    public double Now, Anchor;
    public double Bpm => Track?.Bpm ?? 80;
    public float Gain => gain;

    readonly List<Voice> voices = new();
    readonly float[] noise = new float[SR];
    readonly float[] delay = new float[SR * 2];
    readonly Random rnd = new();
    readonly Biquad master, rainF, rainF2;
    ulong rng = 0x9E3779B97F4A7C15;
    double cut = 600, cutTarget = 600, duck, rainLevel, level = 1, levelTarget = 1;
    long n, nextStep;
    int ts, di, ni;
    float gain;

    public LofiSynth()
    {
        for (int i = 0; i < noise.Length; i++) noise[i] = (float)(rnd.NextDouble() * 2 - 1);
        master = new Biquad('l', cut, SR);
        rainF = new Biquad('l', 650, SR);
        rainF2 = new Biquad('l', 650, SR);
        nextStep = SR / 10;
    }

    double Sd => 60.0 / Bpm / 4;
    long S(double sec) => (long)(sec * SR);
    static double Mf(int m) => 440 * Math.Pow(2, (m - 69) / 12.0);

    void NewTrack()
    {
        Track = LofiTrack.Make((uint)rnd.Next(), Russian, Style);
        ts = 0;
        levelTarget = 1;
        Anchor = nextStep / (double)SR;
    }

    Voice Add(long at, double dur, double vol, double attack)
    {
        attack = Math.Max(0.002, attack);
        var v = new Voice
        {
            Start = at, End = at + S(dur + 0.08), Gain = 0.0001,
            AttackEnd = at + S(attack), AttackMul = Math.Pow(vol / 0.0001, 1.0 / S(attack)),
            DecayMul = Math.Pow(0.0001 / vol, 1.0 / Math.Max(1, S(dur - attack))),
        };
        voices.Add(v);
        return v;
    }

    void Keys(long at, double f, double dur, double vol, int kind, double pan, double send)
    {
        Voice v;
        switch (kind)
        {
            case 1:
                v = Add(at, dur, vol * 1.3, 0.25);
                v.Wave = 1; v.Filter = new Biquad('l', 1100, SR);
                break;
            case 2:
                v = Add(at, dur, vol, 0.004);
                v.Wave = 5; v.ModRatio = 2; v.ModIndex = 0.9; v.ModMul = Math.Pow(0.05, 1.0 / S(0.35));
                v.Filter = new Biquad('l', 2600, SR);
                break;
            case 3:
                v = Add(at, Math.Min(dur, 0.9), vol * 0.9, 0.003);
                v.Wave = 5; v.ModRatio = 3.5; v.ModIndex = 1.6; v.ModMul = Math.Pow(0.02, 1.0 / S(0.12));
                v.Filter = new Biquad('l', 4500, SR);
                break;
            case 4:
                v = Add(at, dur, vol * 0.8, 0.07);
                v.Wave = 0; v.Filter = new Biquad('l', 2200, SR);
                break;
            default:
                v = Add(at, dur, vol, 0.005);
                v.Wave = 5; v.ModRatio = 1; v.ModIndex = 1.6; v.ModMul = Math.Pow(0.12, 1.0 / S(0.6));
                v.Filter = new Biquad('l', 3200, SR);
                break;
        }
        v.Freq = f; v.Pan = pan; v.Send = send; v.Wobble = true; v.Duck = true;
    }

    void Bass(long at, double f, double dur, double vol)
    {
        var v = Add(at, dur, vol, 0.012);
        v.Wave = 0; v.Freq = f; v.Filter = new Biquad('l', 420, SR); v.Wobble = true;
    }

    void Noise(long at, double dur, char type, double f, double vol, double send = 0)
    {
        voices.Add(new Voice
        {
            Start = at, End = at + S(dur + 0.02), Wave = 4, Filter = new Biquad(type, f, SR), Gain = vol, Send = send,
            AttackEnd = at, DecayMul = Math.Pow(0.0001 / vol, 1.0 / Math.Max(1, S(dur))),
        });
    }

    void Kick(long at, double vol = 0.8)
    {
        voices.Add(new Voice
        {
            Start = at, End = at + S(0.5), Wave = 0, Freq = 110, FreqMul = Math.Pow(45.0 / 110, 1.0 / S(0.1)), SweepEnd = at + S(0.1),
            Filter = new Biquad('l', 900, SR), Gain = vol, AttackEnd = at, DecayMul = Math.Pow(0.001 / vol, 1.0 / S(0.45)),
        });
        duckAt = at;
    }
    long duckAt = -1;

    void Snare(long at, double v = 1)
    {
        Noise(at, 0.22, 'b', 1500, 0.24 * v, 0.25);
        var body = Add(at, 0.09, 0.16 * v, 0.002);
        body.Wave = 1; body.Freq = 180; body.Filter = new Biquad('l', 1200, SR);
    }

    void Rim(long at, double v = 1) => Noise(at, 0.05, 'b', 2400, 0.22 * v, 0.2);

    void Play(int s, long at)
    {
        var T = Track;
        var r = T.R;
        int bar = s / 16, st = s % 16;
        var (sec, inBar) = T.SectionAt(bar);
        var prog = T.Progs[sec.Prog];
        int hr = T.HarmRhythm[sec.Prog];
        var ch = prog[(inBar * hr + (hr == 2 && st >= 8 ? 1 : 0)) % prog.Length];
        double hum() => 0.85 + r() * 0.15;

        if (st == 0)
        {
            double k = sec.Bars <= 1 ? 1 : inBar / (double)(sec.Bars - 1);
            double f = sec.FilterFrom < 0 ? sec.Filter : sec.FilterFrom + (sec.Filter - sec.FilterFrom) * k;
            cutTarget = 350 + (T.Cut - 350) * f;
            levelTarget = sec.LevelFrom < 0 ? sec.Level : sec.LevelFrom + (sec.Level - sec.LevelFrom) * k;
        }
        if (sec.Kind == "tail") return;

        if (sec.Final)
        {
            if (inBar == 0 && st == 0)
            {
                var c0 = T.Progs[0][0];
                Kick(at, 0.45);
                Bass(at, Mf(c0.bass), Sd * 24, 0.32);
                for (int i = 0; i < c0.notes.Length; i++)
                    Keys(at + S(i * 0.03), Mf(c0.notes[i]), Sd * 30, 0.19, T.KeysKind, i % 2 == 0 ? -0.3 : 0.3, 0.45);
            }
            return;
        }

        bool lastBar = inBar == sec.Bars - 1;
        int fill = lastBar ? sec.Fill : 0;
        bool drumsHere = sec.Drums > 0 && !(fill == 2 && st >= 12) && !(fill == 4 && st >= 8);

        if (drumsHere)
        {
            var (kick, snare, rim) = T.Grooves[sec.Groove];
            if (sec.Half) { kick = new[] { 0, 10 }; snare = new[] { 8 }; rim = Array.Empty<int>(); }
            if (kick.Contains(st)) Kick(at, 0.75 * hum());
            if (sec.Drums == 2 && snare.Contains(st)) Snare(at, hum());
            if (sec.Drums == 2 && rim.Contains(st)) Rim(at, hum());
            double hv = T.Hats[sec.Hat][st] * T.HatLevel;
            if (sec.Half && st % 4 != 0) hv = 0;
            if (hv > 0) Noise(at, 0.03, 'h', 7000, hv * hum());
            if (fill == 1 && st >= 12 && sec.Drums == 2) Snare(at, 0.22 + (st - 12) * 0.09);
            if (fill == 3 && (st == 13 || st == 14)) Kick(at, 0.5);
        }
        if (fill == 4 && st == 8) { Kick(at, 0.8); Noise(at, 0.5, 'h', 5000, 0.05, 0.3); }

        if (sec.Bass && !(fill == 4 && st >= 8))
        {
            double bf = Mf(ch.bass);
            bool chordStart = st == 0 || (hr == 2 && st == 8);
            switch (T.BassKind)
            {
                case 1: if (st == 0 || st == 10 || (st == 7 && r() < 0.4)) Bass(at, bf, Sd * 5, 0.42); break;
                case 2:
                    if (st % 4 == 0)
                    {
                        int[] walk = { 0, 4, 7, 5 };
                        Bass(at, Mf(ch.bass + walk[st / 4] - (walk[st / 4] > 5 ? 12 : 0)), Sd * 3.5, 0.38);
                    }
                    break;
                case 3: if (st == 0 || st == 8) Bass(at, bf / 2, Sd * 7, 0.5); break;
                default: if (chordStart) Bass(at, bf, Sd * (hr == 2 ? 7 : 14), 0.4); break;
            }
        }

        if (sec.Comp && !(fill == 4 && st >= 8))
        {
            var pat = LofiTrack.CompPats[sec.CompPat];
            foreach (var (pos, plen) in pat)
            {
                if (pos != st || (sec.CompPat == 7 && inBar % 2 == 1)) continue;
                double vol = (sec.Kind == "break" ? 0.17 : 0.19) * hum();
                for (int k = 0; k < ch.notes.Length; k++)
                {
                    if (k == 2 && ch.notes.Length > 4 && r() < 0.5) continue;
                    Keys(at + S(k * (0.008 + r() * 0.012)), Mf(ch.notes[k]), Sd * plen, vol, T.KeysKind, k % 2 == 0 ? -0.3 : 0.3, 0.35);
                }
            }
        }

        if (sec.Lead >= 0 && !(fill == 4 && st >= 8))
        {
            int m = T.Motifs[sec.Lead][(inBar % 2) * 16 + st];
            if (m >= 0)
            {
                if (r() < 0.12) m = Math.Clamp(m + (r() < 0.5 ? -1 : 1), 0, 9);
                Keys(at, Mf(T.Deg(m) + 24), Sd * (T.LeadKind == 4 ? 3 : 6), sec.LeadVol * hum(), T.LeadKind, -0.12, 0.5);
            }
        }
    }

    public int Read(float[] buffer, int offset, int count)
    {
        float target = Target;
        for (int i = 0; i < count; i += 2)
        {
            while (n >= nextStep)
            {
                if (Track == null || ts >= Track.TotalBars * 16 || ForceNext) { NewTrack(); ForceNext = false; }
                Play(ts, nextStep + (ts % 2 == 1 ? S(Sd * Track.Swing) : 0));
                nextStep += S(Sd);
                ts++;
            }

            double t = n / (double)SR;
            double wob = 1 + 0.0022 * Math.Sin(2 * Math.PI * 0.4 * t) + 0.0007 * Math.Sin(2 * Math.PI * 2.7 * t);
            if (duckAt >= 0 && n >= duckAt) { duck = 1; duckAt = -1; }
            duck *= 0.99985;
            double duckGain = 1 - 0.32 * duck;

            double l = 0, r = 0, send = 0;
            for (int v = voices.Count - 1; v >= 0; v--)
            {
                var vo = voices[v];
                if (n >= vo.End) { voices.RemoveAt(v); continue; }
                double s = vo.Next(n, wob, noise, ref ni);
                if (vo.Duck) s *= duckGain;
                l += s * (1 - Math.Max(0, vo.Pan));
                r += s * (1 + Math.Min(0, vo.Pan));
                send += s * vo.Send;
            }

            double echoed = delay[di];
            int len = Math.Clamp((int)(60.0 / Bpm * 0.75 * SR), 1, delay.Length - 1);
            delay[(di + len) % delay.Length] = (float)(send * (Track?.Dly ?? 0.3) + echoed * 0.38);
            delay[di] = 0;
            di = (di + 1) % delay.Length;

            if (Track?.Rain == true) rainLevel += (1 - rainLevel) * 0.00002; else rainLevel *= 0.99998;
            double rain = 0;
            if (rainLevel > 0.001)
            {
                rng ^= rng << 13; rng ^= rng >> 7; rng ^= rng << 17;
                double white = (rng >> 11) * (2.0 / (1UL << 53)) - 1;
                double swell = 0.75 + 0.25 * Math.Sin(2 * Math.PI * 0.07 * t);
                rain = rainF2.Run(rainF.Run(white)) * 0.03 * rainLevel * swell;
            }

            if ((n & 511) == 0)
            {
                cut += (cutTarget - cut) * 0.08;
                level += (levelTarget - level) * 0.03;
                master.Set('l', cut, SR);
            }

            double mono = master.Run(((l + r) / 2 + echoed * 0.8) * 0.9) * level + rain;
            mono = Math.Tanh(mono * 1.3) / 1.3;
            double side = (r - l) / 2 * 0.5;
            gain += (target - gain) * 0.00005f;
            buffer[offset + i] = (float)Math.Clamp((mono - side) * gain, -1, 1);
            buffer[offset + i + 1] = (float)Math.Clamp((mono + side) * gain, -1, 1);
            n++;
        }
        Now = n / (double)SR;
        return count;
    }
}

class LofiPlayer
{
    WaveOutEvent output;
    public LofiSynth Synth { get; private set; }
    public bool On { get; private set; }

    public void Play(float volume, bool russian, string style)
    {
        try
        {
            if (output == null)
            {
                Synth = new LofiSynth();
                output = new WaveOutEvent { DesiredLatency = 150 };
                output.Init(Synth);
            }
            Synth.Russian = russian;
            Synth.Style = style;
            Synth.Target = volume;
            output.Play();
            On = true;
        }
        catch { On = false; }
    }

    public void SetVolume(float volume) { if (On && Synth != null) Synth.Target = volume; }

    public void SetStyle(string style)
    {
        if (Synth == null) return;
        Synth.Style = style;
        if (On) Synth.ForceNext = true;
    }

    public void Stop() { if (Synth != null) Synth.Target = 0; On = false; }

    public void Next() { if (Synth != null) Synth.ForceNext = true; }

    public void Tick()
    {
        if (!On && output != null && Synth.Gain < 0.0005f && output.PlaybackState == PlaybackState.Playing) output.Pause();
    }

    public double BeatPos() => Synth == null ? 0 : Math.Max(0, Synth.Now - 0.15 - Synth.Anchor) * Synth.Bpm / 60;

    public void Dispose() { try { output?.Dispose(); } catch { } }
}

static class SysAudio
{
    static MMDeviceEnumerator enumerator;
    static MMDevice device;
    static double lastDevice;
    static float avg, slow;
    static double lastBeat, loudSince = -1, quietSince;

    public static bool Playing { get; private set; }
    public static float Beat { get; private set; }

    public static void Tick(double time, float dt)
    {
        float peak = 0;
        try
        {
            if (device == null || time - lastDevice > 10)
            {
                enumerator ??= new MMDeviceEnumerator();
                device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                lastDevice = time;
            }
            peak = device.AudioMeterInformation.MasterPeakValue;
        }
        catch { device = null; }

        avg += (peak - avg) * Math.Min(1, dt * 8);
        slow += (peak - slow) * Math.Min(1, dt * 1.2f);
        if (peak > 0.03f && peak > slow * 1.35f && time - lastBeat > 0.28) { lastBeat = time; Beat = 1; }
        Beat *= MathF.Exp(-dt * 7);

        if (slow > 0.02f) { if (loudSince < 0) loudSince = time; quietSince = time; }
        else if (time - quietSince > 2.5) loudSince = -1;
        Playing = loudSince >= 0 && time - loudSince > 2;
    }
}
