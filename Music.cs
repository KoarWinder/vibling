using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace Vibling;

/// Простой биквадратный фильтр (как BiquadFilter в браузере).
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

/// Один звук: синус, треугольник, шум или FM (электропиано, калимба), фильтр и огибающая.
class Voice
{
    public long Start, End, AttackEnd, SweepEnd;
    public int Wave; // 0 синус, 1 треугольник, 4 шум, 5 FM
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

/// Трек лоу-фая, собранный из случайного зерна: лад, гармония, тембры, ритм и форма трека.
class LofiTrack
{
    public static readonly string[] Styles = { "calm", "classic", "jazz", "rainy", "night", "sunny" };

    public string Name, Key, Mode, Style;
    public int Bpm, RootM, KeysKind, LeadKind, CompKind, BassKind;
    public int[] Scale, Kick, Snare, Rim;
    public double[] Hat = new double[16];
    public double Swing, Cut, Dly, MelDensity, OpenHat = -1;
    public bool Ninths, Rain;
    public (int bass, int[] notes)[] Chords;
    public int[][] Motifs;
    public List<(string kind, int bars)> Sections = new();
    public int TotalBars;
    public Func<double> R;

    public int Deg(int d) => RootM + Scale[((d % 7) + 7) % 7] + 12 * (int)Math.Floor(d / 7.0);

    /// Какая часть трека идёт в такте bar: вид части, номер такта внутри неё и её длина.
    public (string kind, int bar, int len, int index) SectionAt(int bar)
    {
        int at = 0;
        for (int i = 0; i < Sections.Count; i++)
        {
            var (kind, bars) = Sections[i];
            if (bar < at + bars) return (kind, bar - at, bars, i);
            at += bars;
        }
        return ("tail", 0, 1, Sections.Count);
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
    static readonly int[][] Progs =
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

        var prog = (int[])Pick(Progs).Clone();
        if (r() < 0.3) prog[3] = Ri(0, 6);
        int[] tones = t.Ninths ? new[] { 0, 2, 4, 6, 8 } : new[] { 0, 2, 4, 6 };
        t.Chords = prog.Select(d => (t.Deg(d), tones.Select(k => t.Deg(d + k) + 12).ToArray())).ToArray();

        // тембры: 0 электропиано, 1 мягкий пэд, 2 мягкое пианино; мелодия: 0 электропиано, 2 пианино, 3 калимба, 4 флейта
        (t.KeysKind, t.LeadKind, t.CompKind, t.BassKind) = t.Style switch
        {
            "calm" => (Pick(new[] { 1, 0, 2 }), Pick(new[] { 4, 3 }), 0, 0),
            "classic" => (Pick(new[] { 0, 2 }), Pick(new[] { 2, 0, 3 }), 1, 1),
            "jazz" => (0, Pick(new[] { 0, 2 }), 2, 2),
            "rainy" => (Pick(new[] { 1, 2 }), Pick(new[] { 3, 4 }), 0, 0),
            "night" => (Pick(new[] { 1, 0 }), 4, 0, 3),
            _ => (Pick(new[] { 0, 2 }), 3, 3, 1),
        };

        int[] kick, snare, rim = Array.Empty<int>();
        switch (t.Style)
        {
            case "calm":
                kick = new[] { 0, 9 }; snare = new[] { 8 };
                for (int i = 0; i < 16; i += 4) t.Hat[i] = 0.035;
                break;
            case "classic":
                kick = r() < 0.5 ? new[] { 0, 7, 10 } : new[] { 0, 10 }; snare = new[] { 4, 12 };
                for (int i = 0; i < 16; i++) t.Hat[i] = i % 2 == 0 ? 0.055 : r() < 0.35 ? 0.02 : 0;
                break;
            case "jazz":
                kick = new[] { 0, 10 }; snare = Array.Empty<int>(); rim = new[] { 4, 12 };
                for (int i = 0; i < 16; i++) t.Hat[i] = i % 4 == 0 ? 0.05 : i % 4 == 3 ? 0.035 : 0;
                break;
            case "rainy":
                kick = new[] { 0 }; snare = Array.Empty<int>(); rim = new[] { 8 };
                for (int i = 0; i < 16; i++) t.Hat[i] = 0.015;
                break;
            case "night":
                kick = new[] { 0, 7, 10 }; snare = new[] { 8 };
                for (int i = 0; i < 16; i += 2) t.Hat[i] = 0.03;
                break;
            default:
                kick = new[] { 0, 8, 10 }; snare = new[] { 4, 12 };
                for (int i = 0; i < 16; i++) t.Hat[i] = i % 2 == 0 ? 0.04 : 0.02;
                t.OpenHat = 14;
                break;
        }
        t.Kick = kick; t.Snare = snare; t.Rim = rim;

        // мелодия: два мотива по два такта, сильные доли чаще
        t.MelDensity = t.Style switch { "calm" => 0.45, "rainy" => 0.4, "night" => 0.4, "jazz" => 0.65, "sunny" => 0.7, _ => 0.55 };
        int[] Motif()
        {
            int i = Ri(3, 6);
            return Enumerable.Range(0, 32).Select(s =>
            {
                double p = s % 8 == 0 ? 0.75 : s % 4 == 0 ? 0.45 : s % 2 == 0 ? 0.25 : 0.06;
                if (r() < p * t.MelDensity) { i = Math.Clamp(i + Ri(-2, 2), 0, 9); return i; }
                return -1;
            }).ToArray();
        }
        t.Motifs = new[] { Motif(), Motif(), Motif() };

        // форма: вступление, части, брейк, концовка и немного тишины с винилом
        t.Sections.Add(("intro", 4));
        t.Sections.Add(("a", 8));
        t.Sections.Add(("b", 8));
        if (r() < 0.7) t.Sections.Add(("break", 4));
        t.Sections.Add(("a2", 8));
        if (r() < 0.6) t.Sections.Add(("b", 8));
        t.Sections.Add(("outro", 4));
        t.Sections.Add(("tail", 1));
        t.TotalBars = t.Sections.Sum(x => x.bars);

        t.Name = ru ? Pick(AdjRu) + " " + Pick(NounRu) : Pick(AdjEn) + " " + Pick(NounEn);
        return t;
    }
}

/// Генератор лоу-фая в реальном времени: каждый трек — новое случайное зерно.
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
    readonly Biquad master, hiss, rainF;
    double cut = 600, cutTarget = 600, duck, pop, rainLevel;
    long n, nextStep;
    int ts, di, ni, ci;
    float gain;

    public LofiSynth()
    {
        for (int i = 0; i < noise.Length; i++) noise[i] = (float)(rnd.NextDouble() * 2 - 1);
        master = new Biquad('l', cut, SR);
        hiss = new Biquad('h', 4000, SR);
        rainF = new Biquad('l', 1800, SR);
        nextStep = SR / 10;
    }

    double Sd => 60.0 / Bpm / 4;
    long S(double sec) => (long)(sec * SR);
    static double Mf(int m) => 440 * Math.Pow(2, (m - 69) / 12.0);

    void NewTrack()
    {
        Track = LofiTrack.Make((uint)rnd.Next(), Russian, Style);
        ts = 0;
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

    /// Аккордовые и мелодические тембры.
    void Keys(long at, double f, double dur, double vol, int kind, double pan, double send)
    {
        Voice v;
        switch (kind)
        {
            case 1: // мягкий пэд
                v = Add(at, dur, vol * 1.3, 0.25);
                v.Wave = 1; v.Filter = new Biquad('l', 1100, SR);
                break;
            case 2: // мягкое пианино
                v = Add(at, dur, vol, 0.004);
                v.Wave = 5; v.ModRatio = 2; v.ModIndex = 0.9; v.ModMul = Math.Pow(0.05, 1.0 / S(0.35));
                v.Filter = new Biquad('l', 2600, SR);
                break;
            case 3: // калимба
                v = Add(at, Math.Min(dur, 0.9), vol * 0.9, 0.003);
                v.Wave = 5; v.ModRatio = 3.5; v.ModIndex = 1.6; v.ModMul = Math.Pow(0.02, 1.0 / S(0.12));
                v.Filter = new Biquad('l', 4500, SR);
                break;
            case 4: // флейта
                v = Add(at, dur, vol * 0.8, 0.07);
                v.Wave = 0; v.Filter = new Biquad('l', 2200, SR);
                break;
            default: // электропиано
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
        var (kind, inBar, len, _) = T.SectionAt(bar);
        var ch = T.Chords[bar % 4];
        double hum() => 0.85 + r() * 0.15;

        if (st == 0)
        {
            double full = T.Cut;
            cutTarget = kind switch
            {
                "intro" => 600 + (full - 600) * (inBar + 1) / len,
                "break" => full * 0.55,
                "outro" => full - (full - 450) * (inBar + 1) / len,
                "tail" => 400,
                _ => full,
            };
        }
        if (kind == "tail") return;

        bool lastOutroBar = kind == "outro" && inBar == len - 1;
        bool drums = kind is "a" or "b" or "a2" || (kind == "outro" && inBar < 2);
        bool fullDrums = kind is "a" or "b" or "a2";
        bool bass = kind is "a" or "b" or "a2" || (kind == "outro" && inBar < 2);

        // барабаны
        if (drums)
        {
            if (T.Kick.Contains(st)) Kick(at, 0.75 * hum());
            if (fullDrums && T.Snare.Contains(st)) Snare(at, hum());
            if (fullDrums && T.Rim.Contains(st)) Rim(at, hum());
            double hv = T.Hat[st];
            if (hv > 0) Noise(at, 0.03, 'h', 7000, hv * hum());
            if (fullDrums && st == T.OpenHat) Noise(at, 0.14, 'h', 6500, 0.04);
            if (fullDrums && inBar == len - 1 && st >= 13 && T.Style == "classic") Snare(at, 0.25 + (st - 13) * 0.08);
        }

        // бас
        if (bass)
        {
            double bf = Mf(ch.bass);
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
                default: if (st == 0) Bass(at, bf, Sd * 14, 0.4); break;
            }
        }

        // аккорды: в последнем такте концовки — один долгий звенящий аккорд
        if (lastOutroBar)
        {
            if (st == 0)
                for (int k = 0; k < ch.notes.Length; k++)
                    Keys(at + S(k * 0.018), Mf(ch.notes[k]), Sd * 40, 0.19, T.KeysKind, k % 2 == 0 ? -0.3 : 0.3, 0.4);
            return;
        }
        bool comp = T.CompKind switch
        {
            1 => st == 0 || st == 10,
            2 => st == 0 || st == 6 || (st == 11 && r() < 0.5),
            3 => st == 0 || st == 8,
            _ => st == 0,
        };
        if (comp)
        {
            double dur = T.CompKind switch { 1 => st == 0 ? Sd * 9 : Sd * 6, 2 => Sd * 3, 3 => Sd * 7, _ => Sd * 15 };
            double vol = (kind == "break" ? 0.17 : 0.19) * hum();
            for (int k = 0; k < ch.notes.Length; k++)
            {
                if (k == 2 && ch.notes.Length > 4 && r() < 0.5) continue;
                Keys(at + S(k * (0.008 + r() * 0.012)), Mf(ch.notes[k]), dur, vol, T.KeysKind, k % 2 == 0 ? -0.3 : 0.3, 0.35);
            }
        }

        // мелодия: в частях b, во второй части a и тихо в брейке
        bool melody = kind is "b" or "a2" || (kind == "break" && inBar >= 2);
        if (melody)
        {
            int motif = kind == "b" ? (inBar / 2 % 2 == 0 ? 0 : 1) : kind == "a2" ? 2 : 1;
            int m = T.Motifs[motif][(inBar % 2) * 16 + st];
            if (m >= 0)
            {
                if (r() < 0.12) m = Math.Clamp(m + (r() < 0.5 ? -1 : 1), 0, 9);
                double vol = (kind == "break" ? 0.13 : 0.16) * hum();
                Keys(at, Mf(T.Deg(m) + 24), Sd * (T.LeadKind == 4 ? 3 : 6), vol, T.LeadKind, -0.12, 0.5);
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

            // винил: тихое шипение и редкие щелчки
            double vinyl = hiss.Run(noise[ci]) * 0.006;
            ci = (ci + 7) % noise.Length;
            if (rnd.NextDouble() < 3.0 / SR) pop = (rnd.NextDouble() < 0.5 ? -1 : 1) * (0.05 + rnd.NextDouble() * 0.08);
            vinyl += pop;
            pop *= 0.6;
            if (Track?.Rain == true) rainLevel += (0.05 - rainLevel) * 0.00002; else rainLevel *= 0.99998;
            double rain = rainF.Run(noise[(ci * 3) % noise.Length]) * rainLevel;

            if ((n & 511) == 0)
            {
                cut += (cutTarget - cut) * 0.08;
                master.Set('l', cut, SR);
            }

            double mono = master.Run(((l + r) / 2 + echoed * 0.8) * 0.9) + vinyl + rain;
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

/// Проигрыватель: включить, выключить с плавным затуханием, следующий трек.
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

    /// Сменить стиль: если музыка играет, сразу начинается трек в новом стиле.
    public void SetStyle(string style)
    {
        if (Synth == null) return;
        Synth.Style = style;
        if (On) Synth.ForceNext = true;
    }

    public void Stop() { if (Synth != null) Synth.Target = 0; On = false; }

    public void Next() { if (Synth != null) Synth.ForceNext = true; }

    /// Когда звук плавно затих — ставим вывод на паузу, чтобы не тратить процессор.
    public void Tick()
    {
        if (!On && output != null && Synth.Gain < 0.0005f && output.PlaybackState == PlaybackState.Playing) output.Pause();
    }

    /// Сколько долей прошло с начала трека (с поправкой на задержку вывода): дробная часть — где мы внутри доли.
    public double BeatPos() => Synth == null ? 0 : Math.Max(0, Synth.Now - 0.15 - Synth.Anchor) * Synth.Bpm / 60;

    public void Dispose() { try { output?.Dispose(); } catch { } }
}

/// Слушает общий уровень звука в колонках, чтобы танцевать под музыку пользователя.
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
