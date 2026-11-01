using System.Globalization;

namespace Vibling;

/// Тексты интерфейса на русском и английском.
static class L
{
    public static readonly string[] Languages = { "auto", "ru", "en" };
    static bool ru;

    public static void Set(string lang)
    {
        ru = lang switch
        {
            "ru" => true,
            "en" => false,
            _ => IsRu(CultureInfo.CurrentUICulture.Name) || IsRu(FirstUserLanguage()),
        };
    }

    static bool IsRu(string tag) => tag != null && (tag.StartsWith("ru") || tag.StartsWith("uk") || tag.StartsWith("be") || tag.StartsWith("kk"));

    /// Первый язык из списка «Язык» в параметрах Windows: бывает русским даже при английском интерфейсе.
    static string FirstUserLanguage()
    {
        try
        {
            using var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Control Panel\International\User Profile");
            return (k?.GetValue("Languages") as string[])?.FirstOrDefault();
        }
        catch { return null; }
    }

    public static string T(string key) => Table.TryGetValue(key, out var v) ? (ru ? v.ru : v.en) : key;

    static readonly Dictionary<string, (string ru, string en)> Table = new()
    {
        ["app"] = ("Вайблинг", "Vibling"),
        ["tagline"] = ("Питомец для рабочего стола", "A tiny desktop pet"),

        ["pet"] = ("Погладить", "Pet"),
        ["feed"] = ("Покормить", "Feed"),
        ["ball"] = ("Мячик", "Ball"),
        ["ball.remove"] = ("Убрать мячик", "Remove ball"),
        ["flip"] = ("Сальто", "Flip"),
        ["flip.menu"] = ("Сделать сальто", "Do a flip"),
        ["sleep"] = ("Уложить спать", "Put to sleep"),
        ["wake"] = ("Разбудить", "Wake up"),
        ["settings"] = ("Настройки…", "Settings…"),
        ["hide"] = ("Спрятать", "Hide"),
        ["show"] = ("Показать", "Show"),
        ["quit"] = ("Выход", "Quit"),
        ["quit.button"] = ("Выйти", "Quit"),
        ["summon"] = ("Позвать на экран", "Call to screen"),
        ["autostart"] = ("Запуск вместе с Windows", "Start with Windows"),
        ["stats"] = ("Погладили: {0}\nНажатий клавиш: {1}\nСъедено токенов: {2}", "Pets: {0}\nKeys pressed: {1}\nTokens eaten: {2}"),

        ["sec.skin"] = ("Скин", "Skin"),
        ["sec.accessory"] = ("Аксессуар", "Accessory"),
        ["sec.size"] = ("Размер", "Size"),
        ["sec.mood"] = ("Характер", "Personality"),
        ["sec.behavior"] = ("Поведение", "Behavior"),
        ["sec.language"] = ("Язык", "Language"),

        ["size.0"] = ("Маленький", "Small"),
        ["size.1"] = ("Средний", "Medium"),
        ["size.2"] = ("Большой", "Large"),
        ["size.3"] = ("Огромный", "Huge"),

        ["mood.0"] = ("Спокойный", "Calm"),
        ["mood.1"] = ("Обычный", "Normal"),
        ["mood.2"] = ("Энергичный", "Energetic"),

        ["lang.auto"] = ("Как в системе", "System"),
        ["lang.ru"] = ("Русский", "Русский"),
        ["lang.en"] = ("English", "English"),

        ["opt.bongo"] = ("Стучит по клавишам вместе с вами", "Types along with you"),
        ["opt.bongo.sub"] = ("Садится за ноутбук, когда вы печатаете", "Sits down at a laptop while you type"),
        ["opt.windows"] = ("Гуляет по окнам", "Walks on windows"),
        ["opt.windows.sub"] = ("Приземляется на окна и запрыгивает на них", "Lands on window tops and jumps onto them"),
        ["opt.follow"] = ("Ходит за курсором", "Follows the cursor"),
        ["opt.follow.sub"] = ("Всё время идёт туда, где мышка", "Always walks toward the mouse"),
        ["opt.sleep"] = ("Засыпает без дела", "Naps when bored"),
        ["opt.sleep.sub"] = ("Через 5 минут без вас и без печати", "After 5 minutes with no clicks or typing"),

        ["skin.classic"] = ("Классический", "Classic"),
        ["skin.mint"] = ("Мятный", "Mint"),
        ["skin.blueberry"] = ("Черничный", "Blueberry"),
        ["skin.sakura"] = ("Сакура", "Sakura"),
        ["skin.lemon"] = ("Лимонный", "Lemon"),
        ["skin.night"] = ("Ночной", "Night"),
        ["skin.ghost"] = ("Призрак", "Ghost"),
        ["skin.gold"] = ("Золотой", "Gold"),
        ["skin.granite"] = ("Гранит", "Granite"),

        ["acc.none"] = ("Без всего", "None"),
        ["acc.headphones"] = ("Наушники", "Headphones"),
        ["acc.cap"] = ("Кепка", "Cap"),
        ["acc.beanie"] = ("Шапка", "Beanie"),
        ["acc.crown"] = ("Корона", "Crown"),
        ["acc.shades"] = ("Очки", "Shades"),
        ["acc.bow"] = ("Бантик", "Bow"),
        ["acc.halo"] = ("Нимб", "Halo"),
    };
}
