using UnityEngine;

/// <summary>
/// Iconos de los recursos (comida, producción, oro, ciencia) dibujados por código en texturas pequeñas:
/// una manzana, un martillo, una moneda y un matraz.
/// </summary>
public static class ResourceIcons
{
    const int N = 32;

    static Texture2D food, production, gold, science;

    public static Texture2D Food => food != null ? food : (food = Build(PaintApple));
    public static Texture2D Production => production != null ? production : (production = Build(PaintHammer));
    public static Texture2D Gold => gold != null ? gold : (gold = Build(PaintCoin));
    public static Texture2D Science => science != null ? science : (science = Build(PaintFlask));

    delegate Color Painter(float x, float y);

    static Texture2D Build(Painter paint)
    {
        Texture2D t = new Texture2D(N, N, TextureFormat.RGBA32, false);
        t.filterMode = FilterMode.Bilinear;
        t.wrapMode = TextureWrapMode.Clamp;
        t.hideFlags = HideFlags.HideAndDontSave;

        Color[] px = new Color[N * N];
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
                px[y * N + x] = paint(x + 0.5f, y + 0.5f);

        t.SetPixels(px);
        t.Apply();
        return t;
    }

    // ---------------------------------------------------------------- Pintura con bordes suaves

    static float Cov(float signedDistance) { return Mathf.Clamp01(0.5f - signedDistance); }

    static float Circle(float x, float y, float cx, float cy, float r)
    {
        return Cov(Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) - r);
    }

    static float Ellipse(float x, float y, float cx, float cy, float rx, float ry)
    {
        float dx = (x - cx) / rx, dy = (y - cy) / ry;
        return Cov((Mathf.Sqrt(dx * dx + dy * dy) - 1f) * Mathf.Min(rx, ry));
    }

    static float Box(float x, float y, float cx, float cy, float hw, float hh)
    {
        float dx = Mathf.Abs(x - cx) - hw, dy = Mathf.Abs(y - cy) - hh;
        return Cov(Mathf.Max(dx, dy));
    }

    // Rectángulo girado "deg" grados alrededor de su centro
    static float RotBox(float x, float y, float cx, float cy, float hw, float hh, float deg)
    {
        float a = -deg * Mathf.Deg2Rad;
        float px = x - cx, py = y - cy;
        float rx = px * Mathf.Cos(a) - py * Mathf.Sin(a);
        float ry = px * Mathf.Sin(a) + py * Mathf.Cos(a);
        return Cov(Mathf.Max(Mathf.Abs(rx) - hw, Mathf.Abs(ry) - hh));
    }

    static float Segment(float x, float y, float ax, float ay, float bx, float by, float halfWidth)
    {
        float abx = bx - ax, aby = by - ay;
        float t = Mathf.Clamp01(((x - ax) * abx + (y - ay) * aby) / (abx * abx + aby * aby));
        float dx = x - (ax + abx * t), dy = y - (ay + aby * t);
        return Cov(Mathf.Sqrt(dx * dx + dy * dy) - halfWidth);
    }

    // Compone una capa de color encima de lo ya pintado
    static Color Over(Color under, Color c, float coverage)
    {
        float a = c.a * coverage;
        float outA = a + under.a * (1f - a);
        if (outA <= 0f) return new Color(0f, 0f, 0f, 0f);

        Color rgb = (c * a + under * under.a * (1f - a)) / outA;
        return new Color(rgb.r, rgb.g, rgb.b, outA);
    }

    // ---------------------------------------------------------------- Iconos

    // Manzana roja con hoja y rabito
    static Color PaintApple(float x, float y)
    {
        Color p = new Color(0, 0, 0, 0);
        Color dark = new Color(0.55f, 0.08f, 0.08f);
        Color red = new Color(0.88f, 0.16f, 0.13f);

        float body = Mathf.Max(Circle(x, y, 11.5f, 14f, 9.5f), Circle(x, y, 20.5f, 14f, 9.5f));
        body = Mathf.Max(body, Box(x, y, 16f, 14f, 6f, 8f));

        p = Over(p, dark, body);
        float inner = Mathf.Max(Circle(x, y, 11.5f, 14f, 8.2f), Circle(x, y, 20.5f, 14f, 8.2f));
        inner = Mathf.Max(inner, Box(x, y, 16f, 14f, 6f, 7f));
        p = Over(p, red, inner);

        p = Over(p, new Color(1f, 0.6f, 0.55f), Ellipse(x, y, 10f, 17f, 3f, 4.5f) * 0.9f);
        p = Over(p, new Color(0.35f, 0.2f, 0.08f), Segment(x, y, 16f, 21f, 17f, 27f, 1.1f));
        p = Over(p, new Color(0.3f, 0.7f, 0.2f), RotBox(x, y, 21.5f, 25f, 4.5f, 2.2f, 25f));
        return p;
    }

    // Martillo: mango de madera en diagonal y cabeza de acero
    static Color PaintHammer(float x, float y)
    {
        Color p = new Color(0, 0, 0, 0);

        p = Over(p, new Color(0.25f, 0.15f, 0.07f), Segment(x, y, 7f, 5f, 21f, 19f, 3.1f));
        p = Over(p, new Color(0.6f, 0.4f, 0.2f), Segment(x, y, 7f, 5f, 21f, 19f, 2.1f));

        p = Over(p, new Color(0.3f, 0.32f, 0.38f), RotBox(x, y, 22f, 22f, 9.2f, 5.2f, 45f));
        p = Over(p, new Color(0.72f, 0.75f, 0.82f), RotBox(x, y, 22f, 22f, 8.2f, 4.2f, 45f));
        p = Over(p, new Color(0.95f, 0.97f, 1f), RotBox(x, y, 21f, 23.5f, 6f, 1.1f, 45f) * 0.8f);
        return p;
    }

    // Moneda dorada con reborde y marca central
    static Color PaintCoin(float x, float y)
    {
        Color p = new Color(0, 0, 0, 0);

        p = Over(p, new Color(0.62f, 0.42f, 0.05f), Circle(x, y, 16f, 16f, 14f));
        p = Over(p, new Color(1f, 0.82f, 0.22f), Circle(x, y, 16f, 16f, 12.2f));
        p = Over(p, new Color(0.85f, 0.62f, 0.1f), Circle(x, y, 16f, 16f, 9.2f));
        p = Over(p, new Color(1f, 0.9f, 0.4f), Circle(x, y, 16f, 16f, 8f));
        p = Over(p, new Color(0.85f, 0.62f, 0.1f), Box(x, y, 16f, 16f, 1.6f, 5.5f));
        p = Over(p, new Color(1f, 1f, 0.8f), Ellipse(x, y, 10.5f, 21f, 2.2f, 3.2f) * 0.7f);
        return p;
    }

    // Matraz de laboratorio con líquido azul
    static Color PaintFlask(float x, float y)
    {
        Color p = new Color(0, 0, 0, 0);

        // Cuerpo trapezoidal ancho abajo y cuello estrecho arriba
        float t = Mathf.Clamp01((y - 4f) / 15f);
        float halfWidth = Mathf.Lerp(12f, 4.2f, t);
        float inY = Cov(Mathf.Max(3.5f - y, y - 19f));
        float body = Cov(Mathf.Abs(x - 16f) - halfWidth) * inY;
        float neck = Box(x, y, 16f, 23.5f, 4.2f, 5f);
        float shape = Mathf.Max(body, neck);

        p = Over(p, new Color(0.28f, 0.34f, 0.4f), shape);

        // Cristal por dentro
        float t2 = Mathf.Clamp01((y - 5f) / 14f);
        float hw2 = Mathf.Lerp(10.3f, 3f, t2);
        float inner = Mathf.Max(Cov(Mathf.Abs(x - 16f) - hw2) * Cov(Mathf.Max(4.8f - y, y - 19f)), Box(x, y, 16f, 23.5f, 3f, 4.4f));
        p = Over(p, new Color(0.82f, 0.92f, 0.97f), inner);

        // Líquido azul en la parte baja, con burbuja
        float liquid = Cov(Mathf.Abs(x - 16f) - Mathf.Lerp(10.3f, 3f, t2)) * Cov(Mathf.Max(4.8f - y, y - 12.5f));
        p = Over(p, new Color(0.2f, 0.62f, 0.95f), liquid);
        p = Over(p, new Color(0.75f, 0.92f, 1f), Circle(x, y, 18f, 9f, 1.5f) * 0.9f);

        // Borde del cuello
        p = Over(p, new Color(0.28f, 0.34f, 0.4f), Box(x, y, 16f, 28.2f, 6.2f, 1.4f));
        return p;
    }
}
