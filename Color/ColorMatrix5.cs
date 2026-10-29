using System.Windows.Media;
using ScreenDarkFilter.Native;

namespace ScreenDarkFilter.Color;

/// <summary>
/// Матрица 5x5 в том же порядке, что MAGCOLOREFFECT / GDI+ ColorMatrix:
/// строка 0 = вклад R в R'G'B'A', строка 4 = сдвиг.
/// </summary>
internal sealed class ColorMatrix5
{
    private readonly float[] _m = new float[25];

    public float this[int row, int col]
    {
        get => _m[row * 5 + col];
        set => _m[row * 5 + col] = value;
    }

    public static ColorMatrix5 Identity()
    {
        var m = new ColorMatrix5();
        m[0, 0] = 1f;
        m[1, 1] = 1f;
        m[2, 2] = 1f;
        m[3, 3] = 1f;
        m[4, 4] = 1f;
        return m;
    }

    public ColorMatrix5 BlendWithIdentity(float amount)
    {
        amount = Math.Clamp(amount, 0f, 1f);
        var result = new ColorMatrix5();
        var identity = Identity();
        for (int i = 0; i < 25; i++)
        {
            result._m[i] = identity._m[i] + (_m[i] - identity._m[i]) * amount;
        }

        return result;
    }

    public ColorMatrix5 Then(ColorMatrix5 next)
    {
        var result = new ColorMatrix5();
        for (int r = 0; r < 5; r++)
        {
            for (int c = 0; c < 5; c++)
            {
                float sum = 0f;
                for (int k = 0; k < 5; k++)
                {
                    sum += this[r, k] * next[k, c];
                }

                result[r, c] = sum;
            }
        }

        return result;
    }

    public MagColorEffect ToNative() => MagColorEffect.FromRowMajor(_m);

    public System.Windows.Media.Color Transform(byte r, byte g, byte b)
    {
        float rf = r / 255f;
        float gf = g / 255f;
        float bf = b / 255f;

        float nr = rf * this[0, 0] + gf * this[1, 0] + bf * this[2, 0] + this[4, 0];
        float ng = rf * this[0, 1] + gf * this[1, 1] + bf * this[2, 1] + this[4, 1];
        float nb = rf * this[0, 2] + gf * this[1, 2] + bf * this[2, 2] + this[4, 2];

        return System.Windows.Media.Color.FromRgb(ToByte(nr), ToByte(ng), ToByte(nb));
    }

    public SolidColorBrush TransformBrush(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Transform(r, g, b));
        brush.Freeze();
        return brush;
    }

    public static ColorMatrix5 InvertRgb()
    {
        var m = Identity();
        m[0, 0] = -1f;
        m[1, 1] = -1f;
        m[2, 2] = -1f;
        m[4, 0] = 1f;
        m[4, 1] = 1f;
        m[4, 2] = 1f;
        return m;
    }

    /// <summary>
    /// Инверсия яркости с сохранением оттенка: белый становится тёмным,
    /// чёрный текст светлым, голубые и зелёные акценты не уезжают в красный.
    /// </summary>
    public static ColorMatrix5 DarkUi(float scale, float offset, float chroma)
    {
        const float lr = 0.2126f;
        const float lg = 0.7152f;
        const float lb = 0.0722f;
        float k = scale - chroma;

        var m = Identity();
        m[0, 0] = k * lr + chroma;
        m[0, 1] = k * lr;
        m[0, 2] = k * lr;
        m[1, 0] = k * lg;
        m[1, 1] = k * lg + chroma;
        m[1, 2] = k * lg;
        m[2, 0] = k * lb;
        m[2, 1] = k * lb;
        m[2, 2] = k * lb + chroma;
        m[4, 0] = offset;
        m[4, 1] = offset;
        m[4, 2] = offset;
        return m;
    }

    public static ColorMatrix5 Saturation(float amount)
    {
        const float lr = 0.2126f;
        const float lg = 0.7152f;
        const float lb = 0.0722f;
        float inv = 1f - amount;

        var m = Identity();
        m[0, 0] = inv * lr + amount;
        m[0, 1] = inv * lr;
        m[0, 2] = inv * lr;
        m[1, 0] = inv * lg;
        m[1, 1] = inv * lg + amount;
        m[1, 2] = inv * lg;
        m[2, 0] = inv * lb;
        m[2, 1] = inv * lb;
        m[2, 2] = inv * lb + amount;
        return m;
    }

    public static ColorMatrix5 Contrast(float amount)
    {
        float t = (1f - amount) * 0.5f;
        var m = Identity();
        m[0, 0] = amount;
        m[1, 1] = amount;
        m[2, 2] = amount;
        m[4, 0] = t;
        m[4, 1] = t;
        m[4, 2] = t;
        return m;
    }

    public static ColorMatrix5 Brightness(float offset)
    {
        var m = Identity();
        m[4, 0] = offset;
        m[4, 1] = offset;
        m[4, 2] = offset;
        return m;
    }

    public static ColorMatrix5 ScaleRgb(float r, float g, float b)
    {
        var m = Identity();
        m[0, 0] = r;
        m[1, 1] = g;
        m[2, 2] = b;
        return m;
    }

    public static ColorMatrix5 TintOffset(float r, float g, float b)
    {
        var m = Identity();
        m[4, 0] = r;
        m[4, 1] = g;
        m[4, 2] = b;
        return m;
    }

    /// <summary>
    /// Теплота: отрицательное — холод/синева, положительное — желтизна.
    /// </summary>
    public static ColorMatrix5 Warmth(float amount)
    {
        amount = Math.Clamp(amount, -1f, 1f);
        return TintOffset(amount * 0.055f, amount * 0.018f, -amount * 0.07f);
    }

    /// <summary>
    /// Грубая имитация средних тонов линейной матрицей (настоящую гамму Magnifier не умеет).
    /// </summary>
    public static ColorMatrix5 Midtones(float amount)
    {
        amount = Math.Clamp(amount, -1f, 1f);
        return Contrast(1f - amount * 0.14f)
            .Then(Brightness(amount * 0.07f));
    }

    /// <summary>
    /// Приглушает цветную кайму ClearType и чуть снижает «неон» светлого текста.
    /// </summary>
    public static ColorMatrix5 SoftenGlyphs()
    {
        return Saturation(0.72f)
            .Then(Contrast(0.86f))
            .Then(Brightness(-0.02f));
    }

    private static byte ToByte(float value)
    {
        int n = (int)Math.Round(Math.Clamp(value, 0f, 1f) * 255f);
        return (byte)n;
    }
}
