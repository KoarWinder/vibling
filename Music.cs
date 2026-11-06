using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace Vibling;

class Biquad
{
    double b0, b1, b2, a1, a2, x1, x2, y1, y2;

    public Biquad(char type, double f, double sr)
    {
        double q = type == 'b' ? 1 : 0.707;
        double w = 2 * Math.PI * Math.Min(f, sr * 0.45) / sr, cs = Math.Cos(w), al = Math.Sin(w) / (2 * q);
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
    public long Start, End;
    public int Wave;
    public double Freq, FreqMul = 1, Gain, AttackMul = 1, DecayMul = 1, Send, Pan;
    public long AttackEnd, SweepEnd;
    public Biquad Filter;
    double phase;

    public double Next(long n, float[] noise, ref int ni)
    {
        if (n < Start) return 0;
        double s;
        if (Wave == 4) { s = noise[ni]; ni = (ni + 1) % noise.Length; }
        else
        {
            phase += Freq / LofiSynth.SR;
            if (phase >= 1) phase -= 1;
            s = Wave switch
            {
                0 => Math.Sin(phase * 2 * Math.PI),
                1 => 1 - 4 * Math.Abs(phase - 0.5),
                2 => 2 * phase - 1,
                _ => phase < 0.5 ? 1 : -1,
            };
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
    public string Name, Key, Mode, Style;
    public int Bpm, Len, Stab;
    public int[] Kick, Snare, Bass;
    public double[] Hat = new double[16];
    public double HatLen, Swing, PadVol, PadCut, LeadVol, LeadLen, MelP, BassVol, BassCut, BassLen, Cut, Dly;
    public int PadT, LeadT, BassT, LeadBase, RootM;
    public bool Brk, Arp;
    public int[] ArpPat;
    public (int b, int[] c)[] Chords;
    public int[][] Motifs;
    public int[] Scale;
    public Func<double> R;

    public int Deg(int d) => RootM + Scale[((d % 7) + 7) % 7] + 12 * (int)Math.Floor(d / 7.0);

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
    static readonly string[] AdjRu = { "Неоновый", "Лунный", "Тёплый", "Ночной", "Ленивый", "Стеклянный", "Медленный", "Пыльный", "Полуночный", "Хмурый", "Сонный", "Электрический", "Дальний", "Розовый", "Забытый", "Бархатный", "Пиксельный", "Дождливый", "Мятный", "Тихий", "Ламповый", "Кофейный", "Осенний", "Звёздный" };
    static readonly string[] NounRu = { "рейс", "дождь", "туман", "закат", "маршрут", "ветер", "сон", "город", "вайб", "шоссе", "рассвет", "проспект", "автобус", "вечер", "полёт", "бит", "коммит", "кофе", "плейлист", "чердак", "трамвай", "балкон", "компилятор", "понедельник" };
    static readonly string[] AdjEn = { "Neon", "Lunar", "Warm", "Night", "Lazy", "Glass", "Slow", "Dusty", "Midnight", "Cloudy", "Sleepy", "Electric", "Distant", "Pink", "Forgotten", "Velvet", "Pixel", "Rainy", "Mint", "Quiet", "Tube", "Coffee", "Autumn", "Starry" };
    static readonly string[] NounEn = { "ride", "rain", "fog", "sunset", "route", "wind", "dream", "city", "vibe", "highway", "dawn", "avenue", "bus", "evening", "flight", "beat", "commit", "coffee", "playlist", "attic", "tram", "balcony", "compiler", "monday" };
    static readonly int[][] Arps = { new[] { 0, 1, 2, 3, 2, 1, 2, 1 }, new[] { 0, 2, 1, 3, 0, 2, 1, 3 }, new[] { 3, 2, 1, 0, 1, 2, 3, 2 } };

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

    public static LofiTrack Make(uint seed, bool ru)
    {
        var r = Mulberry(seed);
        T Pick<T>(T[] a) => a[(int)(r() * a.Length)];
        int Ri(int a, int b) => a + (int)(r() * (b - a + 1));

        var t = new LofiTrack { R = Mulberry(seed + 999) };
        var sc = Pick(Scales);
        int key = Ri(0, 11);
        t.Scale = sc.s;
        t.Mode = ru ? sc.ru : sc.en;
        t.Key = Keys[key];
        t.RootM = 45 + (key > 5 ? key - 12 : key);
        t.Style = Pick(new[] { "lofi", "lofi", "lofi", "jazzhop", "chill", "rainy", "house", "broken", "drive" });
        t.Bpm = t.Style is "house" or "drive" ? Ri(100, 124) : t.Style is "chill" or "rainy" ? Ri(62, 78) : Ri(72, 96);

        var prog = (int[])Pick(Progs).Clone();
        if (r() < 0.3) prog[3] = Ri(0, 6);
        t.Chords = prog.Select(d => (t.Deg(d), new[] { 0, 2, 4, 6 }.Select(k => t.Deg(d + k) + 12).ToArray())).ToArray();

        switch (t.Style)
        {
            case "lofi":
            case "jazzhop":
                t.Kick = r() < 0.5 ? new[] { 0, 7, 10 } : new[] { 0, 10 };
                t.Snare = new[] { 4, 12 }; t.Bass = new[] { 0, 6, 10 }; t.Swing = t.Style == "jazzhop" ? 0.2 : 0.14;
                for (int i = 0; i < 16; i++) t.Hat[i] = i % 4 == 0 ? 0.16 : i % 2 == 0 ? 0.1 : 0.05;
                t.HatLen = 0.04;
                break;
            case "house":
                t.Kick = new[] { 0, 4, 8, 12 }; t.Snare = new[] { 4, 12 }; t.Bass = new[] { 2, 6, 10, 14 }; t.Swing = 0.02;
                for (int i = 0; i < 16; i++) t.Hat[i] = i % 4 == 2 ? 0.15 : 0;
                t.HatLen = 0.12;
                break;
            case "drive":
                t.Kick = new[] { 0, 4, 8, 12 }; t.Snare = new[] { 4, 12 }; t.Bass = new[] { 0, 2, 4, 6, 8, 10, 12, 14 }; t.Swing = 0;
                for (int i = 0; i < 16; i++) t.Hat[i] = i % 2 == 0 ? 0.12 : 0.07;
                t.HatLen = 0.04;
                break;
            case "chill":
            case "rainy":
                t.Kick = new[] { 0, r() < 0.5 ? 9 : 10 }; t.Snare = new[] { 8 }; t.Bass = new[] { 0, 8 }; t.Swing = 0.1;
                for (int i = 0; i < 16; i++) t.Hat[i] = i % 2 == 0 ? 0.1 : 0;
                t.HatLen = 0.04;
                break;
            default:
                var k = new HashSet<int> { 0 };
                for (int i = 0; i < 3; i++) k.Add(Pick(new[] { 3, 6, 7, 9, 10, 13, 14 }));
                t.Kick = k.ToArray(); t.Snare = new[] { 4, Pick(new[] { 11, 12, 13 }) };
                t.Bass = new[] { 0, Pick(new[] { 3, 6, 7 }), Pick(new[] { 10, 11, 14 }) }; t.Swing = 0.06;
                for (int i = 0; i < 16; i++) t.Hat[i] = r() < 0.6 ? 0.05 + r() * 0.1 : 0;
                t.HatLen = 0.04;
                break;
        }

        t.PadT = Pick(new[] { 1, 1, 0, 2, 3 });
        t.LeadT = Pick(new[] { 0, 1, 3, 2 });
        t.BassT = Pick(new[] { 0, 0, 1, 2 });
        double dens = t.Style is "chill" or "rainy" ? 0.6 : t.Style is "lofi" or "jazzhop" ? 0.8 : 1;
        int[] Motif()
        {
            int i = Ri(2, 5);
            return Enumerable.Range(0, 16).Select(s =>
            {
                if (r() < (s % 4 == 0 ? 0.7 : s % 2 == 0 ? 0.4 : 0.2) * dens) { i = Math.Clamp(i + Ri(-2, 2), 0, 8); return i; }
                return -1;
            }).ToArray();
        }
        t.Name = ru ? Pick(AdjRu) + " " + Pick(NounRu) : Pick(AdjEn) + " " + Pick(NounEn);
        t.Len = Pick(new[] { 16, 24, 32 });
        t.Brk = r() < 0.6;
        t.Stab = r() < 0.6 ? Pick(new[] { 6, 10, 11 }) : -1;
        t.PadVol = new[] { 0.1, 0.07, 0.035, 0.028 }[t.PadT];
        t.PadCut = new[] { 1800.0, 1400, 1000, 800 }[t.PadT];
        t.LeadVol = new[] { 0.13, 0.13, 0.05, 0.05 }[t.LeadT];
        t.LeadLen = Pick(new[] { 0.25, 0.45, 0.8 });
        t.MelP = 0.35 + r() * 0.4;
        t.BassVol = t.BassT == 2 ? 0.28 : 0.5;
        t.BassCut = t.BassT == 2 ? 380 : 600;
        t.BassLen = t.Style == "drive" ? 2 : 4;
        t.Arp = r() < 0.4;
        t.ArpPat = Pick(Arps);
        t.Motifs = new[] { Motif(), Motif() };
        t.LeadBase = t.RootM < 45 ? 24 : 12;
        t.Cut = Ri(2300, 5200);
        if (t.Style == "rainy") t.Cut = Math.Min(t.Cut, 2600);
        t.Dly = 0.15 + r() * 0.35;
        return t;
    }
}

class LofiSynth : ISampleProvider
{
    public const int SR = 44100;
    public WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(SR, 2);

    public volatile bool ForceNext;
    public volatile float Target;
    public bool Russian = true;
    public LofiTrack Track { get; private set; }
    public double KickTime = -10, Now, Anchor;
    public double Bpm => Track?.Bpm ?? 84;

    readonly List<Voice> voices = new();
    readonly float[] noise = new float[SR];
    readonly float[] delay = new float[SR * 2];
    readonly Random rnd = new();
    Biquad master, crackle;
    double masterCut = 4000, rain;
    long n, nextStep;
    int ts, di, ni, ci;
    float gain;

    public LofiSynth()
    {
        for (int i = 0; i < noise.Length; i++) noise[i] = (float)(rnd.NextDouble() * 2 - 1);
        master = new Biquad('l', masterCut, SR);
        crackle = new Biquad('h', 3000, SR);
        nextStep = SR / 10;
    }

    double Sd => 60.0 / Bpm / 4;
    long S(double sec) => (long)(sec * SR);

    void NewTrack()
    {
        Track = LofiTrack.Make((uint)rnd.Next(), Russian);
        ts = 0;
        Anchor = nextStep / (double)SR;
        masterCut = Track.Cut;
        master = new Biquad('l', masterCut, SR);
    }

    Voice Tone(long at, double f, double dur, int wave, double vol, double cut, bool send, double det = 0, double pan = 0)
    {
        var v = new Voice
        {
            Start = at, End = at + S(dur + 0.05), Wave = wave, Freq = f * Math.Pow(2, det / 1200), Pan = pan,
            Filter = new Biquad('l', cut, SR), Gain = 0.0001, Send = send ? 1 : 0,
            AttackEnd = at + S(0.02), AttackMul = Math.Pow(vol / 0.0001, 1.0 / S(0.02)),
            DecayMul = Math.Pow(0.0001 / vol, 1.0 / Math.Max(1, S(dur - 0.02))),
        };
        voices.Add(v);
        return v;
    }

    void Noise(long at, double dur, char type, double f, double vol)
    {
        voices.Add(new Voice
        {
            Start = at, End = at + S(dur + 0.02), Wave = 4, Filter = new Biquad(type, f, SR), Gain = vol,
            AttackEnd = at, DecayMul = Math.Pow(0.0001 / vol, 1.0 / Math.Max(1, S(dur))),
        });
    }

    void Kick(long at)
    {
        voices.Add(new Voice
        {
            Start = at, End = at + S(0.4), Wave = 0, Freq = 130, FreqMul = Math.Pow(42.0 / 130, 1.0 / S(0.13)), SweepEnd = at + S(0.13),
            Gain = 0.95, AttackEnd = at, DecayMul = Math.Pow(0.001 / 0.95, 1.0 / S(0.38)),
        });
        KickTime = at / (double)SR;
    }

    void Snare(long at, double v = 1)
    {
        Noise(at, 0.2, 'b', 1800, 0.4 * v);
        Tone(at, 185, 0.1, 1, 0.25 * v, 1500, false);
    }

    static double Mf(int m) => 440 * Math.Pow(2, (m - 69) / 12.0);

    void Play(int s, long at)
    {
        var T = Track;
        var r = T.R;
        int bar = s / 16, st = s % 16;
        var ch = T.Chords[bar % 4];
        bool intro = bar < 2, outro = bar >= T.Len - 2, brk = T.Brk && bar >= T.Len / 2 - 2 && bar < T.Len / 2;
        bool drums = !intro && !outro && !brk, fill = bar % 8 == 7 && st >= 12 && bar < T.Len - 2;
        if (drums && T.Kick.Contains(st)) Kick(at);
        if (fill) Snare(at, 0.3 + (st - 12) * 0.15);
        else if (drums && T.Snare.Contains(st)) Snare(at);
        double hv = T.Hat[st];
        if (hv > 0 && !brk && (st % 2 == 0 || r() < 0.5)) Noise(at, T.HatLen, 'h', 7500, hv * (intro ? 0.6 : 1));
        if (!intro && !brk && T.Bass.Contains(st))
            Tone(at, Mf(ch.b + (st >= 8 && st % 4 == 2 && r() < 0.4 ? 12 : 0)), Sd * T.BassLen, T.BassT, T.BassVol, T.BassCut, false);
        if (st == 0)
            foreach (int note in ch.c)
            {
                Tone(at, Mf(note), Sd * 15, T.PadT, T.PadVol, T.PadCut, true, -7, -0.4);
                Tone(at, Mf(note), Sd * 15, T.PadT, T.PadVol, T.PadCut, false, 7, 0.4);
            }
        if (st == T.Stab && !intro && !brk)
            foreach (int note in ch.c) Tone(at, Mf(note), Sd * 3, 1, 0.05, 1800, true);
        if (T.Arp && !intro && st % 2 == 0) Tone(at, Mf(ch.c[T.ArpPat[st / 2]]), Sd * 2, 1, 0.06, 2600, true, 0, 0.25);
        if (!intro && !brk)
        {
            int m = T.Motifs[new[] { 0, 0, 1, 0 }[bar % 4]][st];
            if (m >= 0 && r() < T.MelP * (T.Arp ? 0.7 : 1))
            {
                if (r() < 0.15) m = Math.Clamp(m + (r() < 0.5 ? -1 : 1), 0, 8);
                Tone(at, Mf(T.Deg(m) + T.LeadBase), T.LeadLen, T.LeadT, T.LeadVol, 3200, true, 0, -0.15);
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
                if (Track == null || ts >= Track.Len * 16 || ForceNext) { NewTrack(); ForceNext = false; }
                Play(ts, nextStep + (ts % 2 == 1 ? S(Sd * Track.Swing) : 0));
                nextStep += S(Sd);
                ts++;
            }

            double l = 0, r = 0, send = 0;
            for (int v = voices.Count - 1; v >= 0; v--)
            {
                var vo = voices[v];
                if (n >= vo.End) { voices.RemoveAt(v); continue; }
                double s = vo.Next(n, noise, ref ni);
                l += s * (1 - Math.Max(0, vo.Pan));
                r += s * (1 + Math.Min(0, vo.Pan));
                send += s * vo.Send;
            }

            double echoed = delay[di];
            int len = Math.Clamp((int)(60.0 / Bpm * 0.75 * SR), 1, delay.Length - 1);
            delay[(di + len) % delay.Length] = (float)(send * (Track?.Dly ?? 0.3) + echoed * 0.35);
            delay[di] = 0;
            di = (di + 1) % delay.Length;

            double crk = crackle.Run(noise[ci]) * 0.012;
            ci = (ci + 7) % noise.Length;
            if (Track?.Style == "rainy") rain += (noise[(ci * 3) % noise.Length] - rain) * 0.02;

            gain += (target - gain) * 0.00005f;
            double mono = master.Run(((l + r) / 2 + echoed + crk + rain * 0.25) * 0.8);
            double side = (r - l) / 2 * 0.8 * 0.6;
            buffer[offset + i] = (float)Math.Clamp((mono - side) * gain, -1, 1);
            buffer[offset + i + 1] = (float)Math.Clamp((mono + side) * gain, -1, 1);
            n++;
        }
        Now = n / (double)SR;
        return count;
    }

    public float Gain => gain;
}

class LofiPlayer
{
    WaveOutEvent output;
    public LofiSynth Synth { get; private set; }
    public bool On { get; private set; }

    public void Play(float volume, bool russian)
    {
        try
        {
            if (output == null)
            {
                Synth = new LofiSynth { Russian = russian };
                output = new WaveOutEvent { DesiredLatency = 150 };
                output.Init(Synth);
            }
            Synth.Russian = russian;
            Synth.Target = volume;
            output.Play();
            On = true;
        }
        catch { On = false; }
    }

    public void SetVolume(float volume) { if (On && Synth != null) Synth.Target = volume; }

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
