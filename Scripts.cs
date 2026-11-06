namespace Vibling;

enum ArgKind { None, Number, Skin, Accessory, Emote, Time, Condition }

/// Описание блока: id, категория (цвет), тип аргумента и подписи. {0} — место для аргумента.
record BlockDef(string Id, string Cat, ArgKind Arg, double Default, string Ru, string En)
{
    public string Text => L.Russian ? Ru : En;
}

class ScriptBlock
{
    public string Id { get; set; }
    public double Num { get; set; }
    public string Str { get; set; }
}

class PetScript
{
    public bool On { get; set; } = true;
    public ScriptBlock When { get; set; }
    public List<ScriptBlock> Do { get; set; } = new();
}

static class Blocks
{
    public static readonly BlockDef[] Events =
    {
        new("start", "event", ArgKind.None, 0, "Когда Vibling запускается", "When Vibling starts"),
        new("click", "event", ArgKind.None, 0, "Когда меня гладят", "When I'm petted"),
        new("dclick", "event", ArgKind.None, 0, "Когда двойной клик", "When double-clicked"),
        new("keys", "event", ArgKind.Number, 100, "Каждые {0} нажатий клавиш", "Every {0} key presses"),
        new("afk", "event", ArgKind.Number, 3, "Когда никого нет {0} мин", "When you're away for {0} min"),
        new("back", "event", ArgKind.None, 0, "Когда человек вернулся", "When you come back"),
        new("every", "event", ArgKind.Number, 30, "Каждые {0} мин", "Every {0} min"),
        new("time", "event", ArgKind.Time, 0, "Каждый день в {0}", "Every day at {0}"),
        new("land", "event", ArgKind.None, 0, "Когда приземлился", "When I land"),
        new("bonk", "event", ArgKind.None, 0, "Когда ударился", "When I bump into something"),
        new("kick", "event", ArgKind.None, 0, "Когда пнул мячик", "When I kick the ball"),
        new("music", "event", ArgKind.None, 0, "Когда заиграла музыка", "When music starts"),
    };

    public static readonly BlockDef[] Actions =
    {
        new("jump", "motion", ArgKind.None, 0, "Подпрыгнуть", "Jump"),
        new("flip", "motion", ArgKind.None, 0, "Сделать сальто", "Do a flip"),
        new("tocursor", "motion", ArgKind.None, 0, "Подойти к курсору", "Go to the cursor"),
        new("summon", "motion", ArgKind.None, 0, "Появиться в центре экрана", "Appear in the middle"),
        new("zerog", "motion", ArgKind.None, 0, "Включить невесомость", "Turn on zero gravity"),
        new("gravity", "motion", ArgKind.None, 0, "Включить гравитацию", "Turn on gravity"),

        new("dance", "behavior", ArgKind.Number, 5, "Танцевать {0} сек", "Dance for {0} s"),
        new("code", "behavior", ArgKind.Number, 5, "Кодить {0} сек", "Code for {0} s"),
        new("wave", "behavior", ArgKind.None, 0, "Помахать", "Wave"),
        new("happy", "behavior", ArgKind.None, 0, "Обрадоваться", "Be happy"),
        new("eat", "behavior", ArgKind.None, 0, "Покушать", "Eat"),
        new("sleep", "behavior", ArgKind.None, 0, "Уснуть", "Fall asleep"),
        new("wake", "behavior", ArgKind.None, 0, "Проснуться", "Wake up"),
        new("ballon", "behavior", ArgKind.None, 0, "Достать мячик", "Get the ball"),
        new("balloff", "behavior", ArgKind.None, 0, "Убрать мячик", "Put the ball away"),

        new("skin", "looks", ArgKind.Skin, 0, "Сменить скин на {0}", "Switch skin to {0}"),
        new("wear", "looks", ArgKind.Accessory, 0, "Надеть {0}", "Put on {0}"),
        new("takeoff", "looks", ArgKind.Accessory, 0, "Снять {0}", "Take off {0}"),
        new("undress", "looks", ArgKind.None, 0, "Снять все аксессуары", "Remove all accessories"),
        new("emote", "looks", ArgKind.Emote, 0, "Показать {0}", "Show {0}"),
        new("hide", "looks", ArgKind.Number, 10, "Спрятаться на {0} сек", "Hide for {0} s"),

        new("musicon", "sound", ArgKind.None, 0, "Включить лоу-фай", "Play lo-fi"),
        new("musicoff", "sound", ArgKind.None, 0, "Выключить музыку", "Stop the music"),
        new("nexttrack", "sound", ArgKind.None, 0, "Следующий трек", "Next track"),

        new("peek", "behavior", ArgKind.None, 0, "Спрятаться и выглянуть", "Hide and peek"),
        new("fart", "behavior", ArgKind.None, 0, "Пукнуть", "Fart"),

        new("wait", "control", ArgKind.Number, 1, "Подождать {0} сек", "Wait {0} s"),
        new("repeat", "control", ArgKind.Number, 3, "Повторить {0} раз", "Repeat {0} times"),
        new("forever", "control", ArgKind.None, 0, "Повторять всегда", "Forever"),
        new("if", "control", ArgKind.Condition, 0, "Если {0}", "If {0}"),
        new("end", "control", ArgKind.None, 0, "Конец", "End"),
        new("chance", "control", ArgKind.Number, 50, "Дальше с шансом {0}%", "Go on with {0}% chance"),
    };

    public static readonly string[] Categories = { "motion", "behavior", "looks", "sound", "control" };
    public static readonly string[] Emotes = { "heart", "star", "note", "!", "?", "zzz" };
    public static readonly string[] Conditions = { "zerog", "gravity", "music", "nomusic", "ball", "noball", "night", "day", "away", "here", "sleeping", "awake" };

    public static bool Opens(string id) => id is "repeat" or "forever" or "if";

    /// Где закрывается блок «повторить/всегда/если», открытый на позиции open.
    public static int MatchEnd(List<ScriptBlock> list, int open)
    {
        int depth = 0;
        for (int i = open + 1; i < list.Count; i++)
        {
            if (Opens(list[i].Id)) depth++;
            else if (list[i].Id == "end") { if (depth == 0) return i; depth--; }
        }
        return list.Count;
    }

    static int MatchOpen(List<ScriptBlock> list, int end)
    {
        int depth = 0;
        for (int i = end - 1; i >= 0; i--)
        {
            if (list[i].Id == "end") depth++;
            else if (Opens(list[i].Id)) { if (depth == 0) return i; depth--; }
        }
        return -1;
    }

    public static int OpenerOf(List<ScriptBlock> list, int end) => MatchOpen(list, end);

    public static BlockDef Def(string id) => Events.Concat(Actions).FirstOrDefault(b => b.Id == id);

    public static ScriptBlock New(BlockDef d) => new()
    {
        Id = d.Id, Num = d.Default,
        Str = d.Arg switch { ArgKind.Skin => "mint", ArgKind.Accessory => "crown", ArgKind.Emote => "heart", ArgKind.Time => "23:00", ArgKind.Condition => "music", _ => null },
    };

    /// Готовые примеры, чтобы было с чего начать.
    public static List<PetScript> Templates() => new()
    {
        new()
        {
            When = new() { Id = "afk", Num = 3 },
            Do = { new() { Id = "musicon" }, new() { Id = "dance", Num = 30 } },
        },
        new()
        {
            When = new() { Id = "every", Num = 50 },
            Do = { new() { Id = "tocursor" }, new() { Id = "wait", Num = 2 }, new() { Id = "wave" }, new() { Id = "emote", Str = "heart" } },
        },
        new()
        {
            When = new() { Id = "keys", Num = 1000 },
            Do = { new() { Id = "flip" }, new() { Id = "emote", Str = "star" } },
        },
        new()
        {
            When = new() { Id = "time", Str = "23:30" },
            Do = { new() { Id = "emote", Str = "zzz" }, new() { Id = "sleep" } },
        },
    };
}

/// Исполняет сценарии: события запускают цепочки блоков, «Подождать» ставит паузу.
class ScriptRunner
{
    class Run { public PetScript Script; public int Pc; public double WaitUntil; public Dictionary<int, int> Loops = new(); }

    readonly List<Run> runs = new();
    readonly Random rnd = new();
    readonly Dictionary<PetScript, double> everyNext = new();
    readonly HashSet<PetScript> afkFired = new();
    readonly Dictionary<PetScript, DateTime> timeFired = new();

    public void Fire(IEnumerable<PetScript> scripts, string ev, Func<PetScript, bool> filter = null)
    {
        foreach (var s in scripts)
            if (s.On && s.When?.Id == ev && (filter == null || filter(s)) && runs.Count < 30)
                runs.Add(new Run { Script = s });
    }

    /// События, которые зависят от времени: «каждые N минут», «в HH:MM», «никого нет N минут».
    public void Clock(IEnumerable<PetScript> scripts, double time, double idleMinutes)
    {
        var now = DateTime.Now;
        foreach (var s in scripts)
        {
            if (!s.On || s.When == null) continue;
            switch (s.When.Id)
            {
                case "every":
                    double period = Math.Max(0.1, s.When.Num) * 60;
                    if (!everyNext.TryGetValue(s, out var next)) everyNext[s] = next = time + period;
                    if (time >= next) { everyNext[s] = time + period; runs.Add(new Run { Script = s }); }
                    break;
                case "time":
                    if (TimeSpan.TryParse(s.When.Str, out var at) && now.Hour == at.Hours && now.Minute == at.Minutes &&
                        (!timeFired.TryGetValue(s, out var last) || last.Date != now.Date))
                    {
                        timeFired[s] = now;
                        runs.Add(new Run { Script = s });
                    }
                    break;
                case "afk":
                    if (idleMinutes >= s.When.Num && afkFired.Add(s)) runs.Add(new Run { Script = s });
                    break;
            }
        }
    }

    /// Человек вернулся: сценарии «никого нет» снова готовы сработать.
    public void Back() => afkFired.Clear();

    public void Tick(double time, Action<ScriptBlock> act, Func<string, bool> check)
    {
        for (int i = runs.Count - 1; i >= 0; i--)
        {
            var r = runs[i];
            var list = r.Script.Do;
            int guard = 0;
            while (r.Pc < list.Count && time >= r.WaitUntil && guard++ < 50)
            {
                var b = list[r.Pc];
                switch (b.Id)
                {
                    case "wait": r.WaitUntil = time + Math.Max(0, b.Num); r.Pc++; break;
                    case "chance": r.Pc = rnd.NextDouble() * 100 >= b.Num ? list.Count : r.Pc + 1; break;
                    case "repeat":
                        int times = (int)Math.Round(Math.Max(0, b.Num));
                        if (times <= 0) r.Pc = Blocks.MatchEnd(list, r.Pc) + 1;
                        else { r.Loops[r.Pc] = times; r.Pc++; }
                        break;
                    case "forever": r.Loops[r.Pc] = int.MaxValue; r.Pc++; break;
                    case "if": r.Pc = check(b.Str) ? r.Pc + 1 : Blocks.MatchEnd(list, r.Pc) + 1; break;
                    case "end":
                        int open = Blocks.OpenerOf(list, r.Pc);
                        if (open >= 0 && r.Loops.TryGetValue(open, out int left))
                        {
                            if (left == int.MaxValue || --left > 0) { if (left != int.MaxValue) r.Loops[open] = left; r.Pc = open + 1; }
                            else { r.Loops.Remove(open); r.Pc++; }
                        }
                        else r.Pc++;
                        break;
                    default: act(b); r.Pc++; break;
                }
            }
            if (r.Pc >= list.Count) runs.RemoveAt(i);
        }
    }
}
