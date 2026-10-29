using ScreenDarkFilter.Services;

namespace ScreenDarkFilter.Color;

internal sealed record ColorThemeInfo(
    string Id,
    string Name,
    string Hint,
    int Background = 16,
    int Text = 82,
    int Contrast = 52,
    int Saturation = 38,
    int Strength = 100,
    int Warmth = 50,
    int Midtones = 50);

internal static class ThemeEngine
{
    public static IReadOnlyList<ColorThemeInfo> Themes { get; } =
    [
        new("dark1c", "Dark 1C",
            "Белый фон → уголь, текст → светлый. Акценты 1С почти не уезжают в чужой цвет.",
            Background: 16, Text: 82, Contrast: 52, Saturation: 38, Warmth: 48, Midtones: 50),
        new("graphite", "Графит",
            "Почти монохром. Спокойнее для больших таблиц и журналов.",
            Background: 18, Text: 78, Contrast: 48, Saturation: 12, Warmth: 46, Midtones: 48),
        new("oled", "OLED",
            "Почти чёрный фон, текст чуть приглушён, меньше засветки на тёмных матрицах.",
            Background: 4, Text: 74, Contrast: 58, Saturation: 34, Warmth: 50, Midtones: 42),
        new("nord", "Nord",
            "Полярная ночь: холодный сине-серый, как в северных редакторских темах.",
            Background: 14, Text: 80, Contrast: 50, Saturation: 28, Warmth: 18, Midtones: 52),
        new("nightblue", "Ночная синь",
            "Тёмная тема с явным холодным сдвигом. Меньше рыжины на сером.",
            Background: 14, Text: 84, Contrast: 54, Saturation: 36, Warmth: 22, Midtones: 50),
        new("slate", "Сланец",
            "Сине-стальной, ближе к IDE, чем к «ночному небу».",
            Background: 20, Text: 80, Contrast: 46, Saturation: 24, Warmth: 36, Midtones: 54),
        new("forest", "Лес",
            "Тёмно-зелёный уклон. Мягче для глаз, чем чистый серый.",
            Background: 15, Text: 80, Contrast: 50, Saturation: 32, Warmth: 42, Midtones: 52),
        new("warm", "Тёплый вечер",
            "Как Dark 1C, но желтее и спокойнее по синеве.",
            Background: 18, Text: 80, Contrast: 50, Saturation: 34, Warmth: 78, Midtones: 54),
        new("amber", "Янтарь",
            "Тёплый жёлто-оранжевый свет, ближе к лампе, чем к монитору.",
            Background: 16, Text: 78, Contrast: 48, Saturation: 40, Warmth: 88, Midtones: 56),
        new("copper", "Медь",
            "Рыже-коричневый металл. Акценты 1С остаются читаемыми.",
            Background: 14, Text: 82, Contrast: 54, Saturation: 36, Warmth: 72, Midtones: 48),
        new("wine", "Вино",
            "Бордово-фиолетовый тон. Не инверсия в красный, а лёгкий сдвиг теней.",
            Background: 12, Text: 84, Contrast: 56, Saturation: 34, Warmth: 58, Midtones: 46),
        new("midnight", "Полночь",
            "Глубокий сине-фиолетовый, темнее Nord и спокойнее инверсии.",
            Background: 10, Text: 82, Contrast: 54, Saturation: 30, Warmth: 32, Midtones: 44),
        new("sepia", "Крафт / сепия",
            "Тёмная бумага: коричневые тени, кремовый текст, мало «цифрового» серого.",
            Background: 22, Text: 76, Contrast: 44, Saturation: 26, Warmth: 82, Midtones: 58),
        new("paper", "Чернила на бумаге",
            "Негатив слабее: больше серой бумаги, меньше угля. Для длинного чтения.",
            Background: 34, Text: 70, Contrast: 40, Saturation: 22, Warmth: 64, Midtones: 62, Strength: 82),
        new("solar", "Solarized dark",
            "Приглушённый сине-зелёный, как в solarized: меньше контраста, меньше рези в глазах.",
            Background: 24, Text: 74, Contrast: 38, Saturation: 28, Warmth: 40, Midtones: 58),
        new("highcontrast", "Высокий контраст",
            "Уголь и почти белый текст. Для зала с ярким светом, не для ночи.",
            Background: 6, Text: 94, Contrast: 86, Saturation: 30, Warmth: 50, Midtones: 40),
        new("mono", "Монохром",
            "Только яркость, без цвета. Таблицы и проводки без цветного шума.",
            Background: 16, Text: 80, Contrast: 50, Saturation: 0, Warmth: 50, Midtones: 50),
        new("dim", "Приглушение",
            "Без инверсии: просто гасим яркость, цвета 1С остаются на месте.",
            Background: 36, Text: 48, Contrast: 42, Saturation: 70, Warmth: 50, Midtones: 50, Strength: 100),
        new("invert", "Инверсия RGB",
            "Классический негатив. Голубой станет рыжим — только если нужен «как лупа».",
            Background: 50, Text: 50, Contrast: 50, Saturation: 50, Warmth: 50, Midtones: 50)
    ];

    public static ColorThemeInfo Resolve(string? themeId) =>
        Themes.FirstOrDefault(t => t.Id == themeId) ?? Themes[0];

    public static void ApplyDefaults(AppSettings settings, ColorThemeInfo theme)
    {
        settings.ThemeId = theme.Id;
        settings.Background = theme.Background;
        settings.Text = theme.Text;
        settings.Contrast = theme.Contrast;
        settings.Saturation = theme.Saturation;
        settings.Strength = theme.Strength;
        settings.Warmth = theme.Warmth;
        settings.Midtones = theme.Midtones;
    }

    public static ColorMatrix5 Build(AppSettings settings)
    {
        float contrast = 0.62f + settings.Contrast * 0.0076f;
        var contrastMatrix = ColorMatrix5.Contrast(contrast);
        var warmth = ColorMatrix5.Warmth((settings.Warmth - 50) / 50f);
        var midtones = ColorMatrix5.Midtones((settings.Midtones - 50) / 50f);

        ColorMatrix5 matrix = settings.ThemeId switch
        {
            "graphite" => BuildDarkUi(settings, chromaScale: 0.10f),
            "oled" => BuildDarkUi(settings, chromaScale: 0.95f)
                .Then(ColorMatrix5.TintOffset(-0.01f, -0.01f, 0.00f)),
            "nord" => BuildDarkUi(settings, chromaScale: 0.72f)
                .Then(ColorMatrix5.TintOffset(-0.04f, 0.01f, 0.07f)),
            "nightblue" => BuildDarkUi(settings, chromaScale: 1f)
                .Then(ColorMatrix5.TintOffset(-0.03f, 0.00f, 0.06f)),
            "slate" => BuildDarkUi(settings, chromaScale: 0.55f)
                .Then(ColorMatrix5.TintOffset(-0.02f, 0.01f, 0.04f)),
            "forest" => BuildDarkUi(settings, chromaScale: 0.85f)
                .Then(ColorMatrix5.TintOffset(-0.05f, 0.06f, -0.03f)),
            "warm" => BuildDarkUi(settings, chromaScale: 1f)
                .Then(ColorMatrix5.TintOffset(0.05f, 0.02f, -0.04f)),
            "amber" => BuildDarkUi(settings, chromaScale: 0.9f)
                .Then(ColorMatrix5.TintOffset(0.08f, 0.04f, -0.07f)),
            "copper" => BuildDarkUi(settings, chromaScale: 0.88f)
                .Then(ColorMatrix5.TintOffset(0.07f, 0.01f, -0.05f)),
            "wine" => BuildDarkUi(settings, chromaScale: 0.82f)
                .Then(ColorMatrix5.TintOffset(0.06f, -0.04f, 0.04f)),
            "midnight" => BuildDarkUi(settings, chromaScale: 0.78f)
                .Then(ColorMatrix5.TintOffset(-0.02f, -0.03f, 0.08f)),
            "sepia" => BuildDarkUi(settings, chromaScale: 0.42f)
                .Then(ColorMatrix5.TintOffset(0.07f, 0.03f, -0.05f)),
            "paper" => BuildDarkUi(settings, chromaScale: 0.35f)
                .Then(ColorMatrix5.TintOffset(0.05f, 0.03f, -0.02f))
                .Then(ColorMatrix5.Brightness(0.04f)),
            "solar" => BuildDarkUi(settings, chromaScale: 0.62f)
                .Then(ColorMatrix5.TintOffset(-0.03f, 0.02f, 0.01f)),
            "highcontrast" => BuildDarkUi(settings, chromaScale: 0.7f),
            "mono" => BuildDarkUi(settings, chromaScale: 0f),
            "dim" => BuildDim(settings),
            "invert" => ColorMatrix5.InvertRgb()
                .Then(ColorMatrix5.Saturation(settings.Saturation / 50f))
                .Then(ColorMatrix5.Brightness((settings.Background - 50) / 220f)),
            _ => BuildDarkUi(settings, chromaScale: 1f)
        };

        matrix = matrix.Then(contrastMatrix).Then(warmth).Then(midtones);
        if (settings.SoftenOneCText)
        {
            matrix = matrix.Then(ColorMatrix5.SoftenGlyphs());
        }

        return matrix.BlendWithIdentity(settings.Strength / 100f);
    }

    private static ColorMatrix5 BuildDarkUi(AppSettings settings, float chromaScale)
    {
        float offset = 0.58f + settings.Text * 0.0034f;
        float targetBg = 0.045f + settings.Background * 0.0028f;
        float scale = targetBg - offset;
        float chroma = Math.Clamp(settings.Saturation / 100f * chromaScale, 0f, 0.85f);
        return ColorMatrix5.DarkUi(scale, offset, chroma);
    }

    private static ColorMatrix5 BuildDim(AppSettings settings)
    {
        float dim = 0.42f + settings.Background * 0.0045f;
        float sat = settings.Saturation / 80f;
        return ColorMatrix5.ScaleRgb(dim, dim, dim)
            .Then(ColorMatrix5.Saturation(sat))
            .Then(ColorMatrix5.Brightness((settings.Text - 50) / 400f));
    }
}
