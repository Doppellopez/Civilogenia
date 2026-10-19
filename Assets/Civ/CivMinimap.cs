using UnityEngine;

// ---------------------------------------------------------------------------------------------------------------------
// Minimapa: el planeta desplegado en proyección equirrectangular (longitud/latitud). Muestra el terreno, el territorio de
// cada civilización, las ciudades y las unidades, respetando la niebla de guerra, y marca hacia dónde mira la cámara.
// Clic o arrastre sobre él para llevar la cámara a ese punto.
// ---------------------------------------------------------------------------------------------------------------------
public class CivMinimap
{
    public const int W = 240;
    public const int H = 120;

    const int PrecomputePerFrame = 700;   // casillas más cercanas que se calculan cada fotograma al preparar el mapa
    const float RefreshSeconds = 0.4f;

    Texture2D tex;
    Color32[] pixels;
    int[] cellOf;          // casilla que corresponde a cada píxel
    int mapped;            // píxeles con casilla ya calculada
    WorldGrid builtFor;
    float refreshTimer;

    public bool Ready { get { return cellOf != null && mapped >= W * H; } }
    public Texture2D Texture { get { return tex; } }

    static Vector3 DirOfPixel(float u, float v)
    {
        // u, v en 0..1 (v hacia abajo): longitud de -π a π y latitud de π/2 a -π/2
        float lon = u * Mathf.PI * 2f - Mathf.PI;
        float lat = Mathf.PI * 0.5f - v * Mathf.PI;
        float c = Mathf.Cos(lat);
        return new Vector3(c * Mathf.Sin(lon), Mathf.Sin(lat), c * Mathf.Cos(lon));
    }

    static Vector2 UvOfDir(Vector3 d)
    {
        d.Normalize();
        float lon = Mathf.Atan2(d.x, d.z);
        float lat = Mathf.Asin(Mathf.Clamp(d.y, -1f, 1f));
        return new Vector2((lon + Mathf.PI) / (Mathf.PI * 2f), (Mathf.PI * 0.5f - lat) / Mathf.PI);
    }

    /// <summary>Dirección (en coordenadas locales de la Tierra) de un punto del minimapa: u, v en 0..1, v hacia abajo.</summary>
    public static Vector3 DirAt(float u, float v) { return DirOfPixel(Mathf.Clamp01(u), Mathf.Clamp01(v)); }

    /// <summary>Posición normalizada (0..1, v hacia abajo) de una dirección en el minimapa.</summary>
    public static Vector2 UvAt(Vector3 dir) { return UvOfDir(dir); }

    /// <summary>Prepara el mapa de píxeles a casillas (poco a poco) y refresca la imagen cada pocas décimas de segundo.</summary>
    public void Tick(CivGame game)
    {
        if (game == null || game.grid == null) return;

        if (builtFor != game.grid || cellOf == null)
        {
            builtFor = game.grid;
            cellOf = new int[W * H];
            mapped = 0;
            refreshTimer = 0f;

            if (tex == null)
            {
                tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                pixels = new Color32[W * H];
            }
        }

        // La búsqueda de la casilla más cercana es lineal: se reparte entre varios fotogramas para no congelar el juego
        if (mapped < W * H)
        {
            int end = Mathf.Min(W * H, mapped + PrecomputePerFrame);
            for (int p = mapped; p < end; p++)
            {
                int x = p % W, y = p / W;
                cellOf[p] = game.grid.Nearest(DirOfPixel((x + 0.5f) / W, (y + 0.5f) / H));
            }
            mapped = end;
            if (mapped < W * H) return;
        }

        refreshTimer -= Time.unscaledDeltaTime;
        if (refreshTimer > 0f) return;
        refreshTimer = RefreshSeconds;

        Refresh(game);
    }

    static Color32 BiomeColor(Biome b)
    {
        switch (b)
        {
            case Biome.Sea:      return new Color32(24, 52, 100, 255);
            case Biome.Plains:   return new Color32(112, 158, 76, 255);
            case Biome.Forest:   return new Color32(48, 108, 56, 255);
            case Biome.Desert:   return new Color32(204, 184, 114, 255);
            case Biome.Tundra:   return new Color32(198, 208, 214, 255);
            case Biome.Hills:    return new Color32(128, 114, 82, 255);
            default:             return new Color32(140, 140, 146, 255);   // montaña
        }
    }

    static Color32 Mix(Color32 a, Color32 b, float k)
    {
        return new Color32(
            (byte)Mathf.Lerp(a.r, b.r, k),
            (byte)Mathf.Lerp(a.g, b.g, k),
            (byte)Mathf.Lerp(a.b, b.b, k),
            255);
    }

    // Píxel (índice en el arreglo, con v hacia abajo) de una casilla
    int IndexOfCell(CivGame game, int cell)
    {
        Vector2 uv = UvOfDir(game.grid.dir[cell]);
        int x = Mathf.Clamp((int)(uv.x * W), 0, W - 1);
        int y = Mathf.Clamp((int)(uv.y * H), 0, H - 1);
        return y * W + x;
    }

    void Dot(int center, int radius, Color32 fill, Color32 border)
    {
        int cx = center % W, cy = center / W;
        for (int dy = -radius - 1; dy <= radius + 1; dy++)
            for (int dx = -radius - 1; dx <= radius + 1; dx++)
            {
                int x = cx + dx, y = cy + dy;
                if (x < 0 || x >= W || y < 0 || y >= H) continue;
                bool inner = Mathf.Abs(dx) <= radius && Mathf.Abs(dy) <= radius;
                pixels[y * W + x] = inner ? fill : border;
            }
    }

    void Refresh(CivGame game)
    {
        WorldGrid grid = game.grid;
        Color32 black = new Color32(0, 0, 0, 255);

        // Terreno, territorio y niebla
        for (int p = 0; p < W * H; p++)
        {
            int cell = cellOf[p];

            if (!game.IsExplored(cell)) { pixels[p] = new Color32(6, 6, 10, 255); continue; }

            Color32 c = BiomeColor(grid.biome[cell]);

            City owner = grid.tileCity[cell];
            if (owner != null && owner.alive && owner.owner != null)
            {
                Color oc = owner.owner.color;
                c = Mix(c, new Color32((byte)(oc.r * 255f), (byte)(oc.g * 255f), (byte)(oc.b * 255f), 255), 0.5f);
            }

            if (!game.IsVisible(cell)) c = Mix(c, black, 0.5f);

            pixels[p] = c;
        }

        // Ciudades (cuadrados grandes con borde negro)
        foreach (City city in game.allCities)
        {
            if (!city.alive) continue;
            if (city.owner != game.human && !game.IsExplored(city.cell)) continue;

            Color cc = city.owner.color;
            Dot(IndexOfCell(game, city.cell), 2, new Color32((byte)(cc.r * 255f), (byte)(cc.g * 255f), (byte)(cc.b * 255f), 255), black);
        }

        // Unidades: las propias siempre; las enemigas solo si están a la vista
        foreach (Unit u in game.allUnits)
        {
            if (!u.alive || u.carriedBy != null) continue;
            if (u.owner != game.human && !game.IsVisible(u.cell)) continue;

            Color uc = u.owner.color;
            Dot(IndexOfCell(game, u.cell), 1, new Color32((byte)(uc.r * 255f), (byte)(uc.g * 255f), (byte)(uc.b * 255f), 255),
                u.owner == game.human ? new Color32(255, 255, 255, 255) : black);
        }

        tex.SetPixels32(pixels);
        tex.Apply(false);
    }

    /// <summary>Punto del minimapa (0..1, v hacia abajo) hacia el que mira la cámara, o null si no hay cámara.</summary>
    public static Vector2? CameraUv(CivGame game)
    {
        Camera cam = Camera.main;
        if (cam == null || game == null || game.EarthT == null) return null;

        Vector3 world = (cam.transform.position - game.EarthT.position).normalized;
        return UvOfDir(game.EarthT.InverseTransformDirection(world));
    }
}
