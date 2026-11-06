using System.Globalization;

namespace Vibling;

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
        ["app"] = ("Vibling", "Vibling"),

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

        ["sec.size"] = ("Размер", "Size"),
        ["sec.mood"] = ("Характер", "Personality"),
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
        ["opt.zerog"] = ("Невесомость", "Zero gravity"),
        ["opt.zerog.sub"] = ("Летает, танцует в воздухе и отскакивает от краёв экрана", "Floats, dances in the air and bounces off screen edges"),
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
        ["skin.custom"] = ("Свой", "Custom"),
        ["card.custom"] = ("Свой цвет", "Custom color"),
        ["color.body"] = ("Тело", "Body"),
        ["color.eyes"] = ("Глаза", "Eyes"),
        ["color.outline"] = ("Обводка", "Outline"),
        ["color.hue"] = ("Оттенок", "Hue"),
        ["color.sat"] = ("Насыщенность", "Saturation"),
        ["color.val"] = ("Яркость", "Brightness"),

        ["acc.none"] = ("Без всего", "None"),
        ["acc.headphones"] = ("Наушники", "Headphones"),
        ["acc.cap"] = ("Кепка", "Cap"),
        ["acc.beanie"] = ("Шапка", "Beanie"),
        ["acc.crown"] = ("Корона", "Crown"),
        ["acc.shades"] = ("Очки", "Shades"),
        ["acc.bow"] = ("Бантик", "Bow"),
        ["acc.halo"] = ("Нимб", "Halo"),
        ["acc.tophat"] = ("Цилиндр", "Top hat"),
        ["acc.santa"] = ("Шапка Санты", "Santa hat"),
        ["acc.partyhat"] = ("Колпак", "Party hat"),
        ["acc.propeller"] = ("Пропеллер", "Propeller cap"),
        ["acc.catears"] = ("Ушки", "Cat ears"),
        ["acc.horns"] = ("Рожки", "Horns"),
        ["acc.flower"] = ("Цветок", "Flower"),
        ["acc.bowtie"] = ("Бабочка", "Bow tie"),
        ["acc.scarf"] = ("Шарф", "Scarf"),
        ["acc.bell"] = ("Колокольчик", "Bell"),
        ["nav.look"] = ("Внешний вид", "Appearance"),
        ["nav.behavior"] = ("Поведение", "Behavior"),
        ["nav.system"] = ("Система", "System"),
        ["nav.about"] = ("О программе", "About"),
        ["card.preview"] = ("Превью", "Preview"),
        ["card.look"] = ("Вид", "Look"),
        ["card.accessories"] = ("Аксессуары", "Accessories"),
        ["card.updates"] = ("Обновления", "Updates"),
        ["upd.check"] = ("Проверить сейчас", "Check now"),
        ["upd.yours"] = ("У вас версия {0}", "You have version {0}"),
        ["upd.title"] = ("Вышло обновление {0}", "Update {0} is out"),
        ["upd.text"] = ("У вас {0}, программа перезапустится сама.", "You have {0}, the app will restart by itself."),
        ["upd.notes"] = ("Что нового", "What's new"),
        ["upd.update"] = ("Обновить", "Update"),
        ["upd.later"] = ("Позже", "Later"),
        ["upd.downloading"] = ("Скачиваю…", "Downloading…"),
        ["upd.latest"] = ("У вас последняя версия ({0})", "You're up to date ({0})"),
        ["upd.error"] = ("Не получилось проверить обновления", "Couldn't check for updates"),
        ["acc.clear"] = ("Снять всё", "Remove all"),
        ["card.skin"] = ("Скин", "Skin"),
        ["card.mood"] = ("Характер", "Personality"),
        ["card.keyboard"] = ("Клавиатура", "Keyboard"),
        ["card.screen"] = ("Окна и курсор", "Windows and cursor"),
        ["card.fullscreen"] = ("Полноэкранный режим", "Fullscreen"),
        ["opt.fsmode"] = ("При F11 и в играх", "In F11 and games"),
        ["opt.fsmode.sub"] = ("Окно на весь экран", "Window covers the screen"),
        ["fs.hide"] = ("Пропадать", "Disappear"),
        ["fs.walk"] = ("Ходить внизу экрана", "Walk at screen bottom"),
        ["card.general"] = ("Общие", "General"),
        ["card.actions"] = ("Действия", "Actions"),
        ["card.stats"] = ("Статистика", "Stats"),
        ["stat.pets"] = ("Погладили", "Pets"),
        ["stat.keys"] = ("Нажатий клавиш", "Keys pressed"),
        ["stat.tokens"] = ("Съедено токенов", "Tokens eaten"),
        ["about.version"] = ("Версия", "Version"),
        ["about.license"] = ("Лицензия", "License"),
        ["about.source"] = ("Исходный код", "Source code"),
        ["on"] = ("Вкл", "On"),
        ["off"] = ("Выкл", "Off"),
    };
}
