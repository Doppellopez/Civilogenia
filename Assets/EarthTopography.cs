using System.Collections.Generic;
using UnityEngine;

public enum Biome
{
    Sea,
    Plains,
    Forest,
    Desert,
    Tundra,
    Hills,
    Mountain
}

/// <summary>
/// Topografía pseudoaleatoria parecida a la Tierra:
/// continentes con costas fractales (ruido deformado), un porcentaje fijo de tierra,
/// cordilleras alineadas, plataformas oceánicas y biomas según latitud, altura y humedad.
/// </summary>
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class EarthTopography : MonoBehaviour
{
    [Header("Planeta")]
    public float radius = 5f;
    [Tooltip("Multiplica el radio. El tamaño final es Radius x Size Multiplier")]
    public float sizeMultiplier = 1000f;
    public int longitudeSegments = 256;
    public int latitudeSegments = 128;

    [Header("Semilla")]
    public bool randomSeed = true;
    public int seed = 12345;

    [Header("Continentes")]
    [Tooltip("Fracción de la superficie que es tierra (la Tierra real ≈ 0.29)")]
    [Range(0.1f, 0.6f)]
    public float landFraction = 0.3f;
    [Tooltip("Menos = continentes grandes, más = muchas islas")]
    public float continentScale = 1.1f;
    [Tooltip("Cuánto se retuercen las costas")]
    [Range(0f, 1.5f)]
    public float coastWarp = 0.7f;
    public int octaves = 6;

    [Header("Relieve")]
    [Tooltip("Quita la topografía: la superficie queda lisa (los biomas y colores se mantienen)")]
    public bool flatSurface = true;
    [Tooltip("Añade alturas en los vértices de las celdillas (malla poligonal): cada vértice toma su altura del terreno y las caras planas unen los vértices. Tiene prioridad sobre Flat Surface")]
    public bool heightsFromVertices = true;
    [Tooltip("Altura máxima de las montañas, como fracción del radio")]
    [Range(0f, 0.15f)]
    public float mountainHeight = 0.05f;
    [Tooltip("Exagera el relieve para que se lea mejor en el modelo poligonal")]
    [Range(0.5f, 3f)]
    public float reliefScale = 1.5f;

    [Tooltip("Suaviza el relieve (promedia alturas vecinas) y el sombreado de las caras. 0 = sin suavizar")]
    [Range(0f, 3f)]
    public float reliefSmoothing = 1.5f;

    [Header("Forma")]
    [Tooltip("Achatamiento polar del esferoide: 0 = esfera perfecta. La Tierra real es 0,003; aquí se exagera para que se vea")]
    [Range(0f, 0.25f)]
    public float flattening = 0.08f;
    [Tooltip("Esferoide poligonal (caras planas, estilo low-poly). Desmarcado = esfera suave con textura")]
    public bool lowPoly = false;
    [Tooltip("Subdivisiones del icosaedro: 20 x 4^n caras (5 = 20 mil, 6 = 82 mil, 7 = 328 mil)")]
    [Range(2, 7)]
    public int subdivisions = 6;

    [Header("Textura (solo esfera suave)")]
    public int textureWidth = 1024;

    [Header("Rotación")]
    public float rotationSpeed = 5f;

    // Permutación del ruido y desplazamientos por semilla
    int[] perm;
    Vector3 oBase, oWarpX, oWarpY, oWarpZ, oDetail, oBelt, oRidge, oMoist;

    // Resultado del muestreo del terreno en una dirección
    struct Terrain
    {
        public float elevation;
        public float land;      // 0 = costa, 1 = punto más alto (solo tierra)
        public float depth;     // 0 = costa, 1 = fondo más profundo (solo mar)
        public float mountain;  // 0..1
        public bool isLand;
    }

    /// <summary>Radio real del planeta en unidades de Unity.</summary>
    public float FinalRadius => radius * sizeMultiplier;

    float seaElevation;
    float maxElevation;
    float minElevation;

    void Start()
    {
        Generate();
    }

    void Update()
    {
        // Signo negativo: gira al revés (un valor negativo en el inspector vuelve al sentido anterior)
        transform.Rotate(Vector3.up, -rotationSpeed * Time.deltaTime, Space.World);
    }

    [ContextMenu("Generar")]
    public void Generate()
    {
        if (randomSeed) seed = Random.Range(0, 1000000);

        InitNoise(seed);
        CalibrateSeaLevel();

        if (lowPoly || heightsFromVertices)
        {
            GetComponent<MeshFilter>().sharedMesh = BuildPolygonalMesh(out Texture2D palette);
            GetComponent<MeshRenderer>().sharedMaterial = BuildMaterial(palette);
        }
        else
        {
            GetComponent<MeshFilter>().sharedMesh = BuildMesh();
            GetComponent<MeshRenderer>().sharedMaterial = BuildMaterial(BuildTexture());
        }

        SphereCollider sphere = GetComponent<SphereCollider>();
        if (sphere != null) sphere.radius = FinalRadius;
    }

    // ---------------------------------------------------------------- Terreno

    float Elevation(Vector3 dir)
    {
        Vector3 q = dir * continentScale;

        // Deformación de dominio: retuerce el ruido para obtener costas naturales
        Vector3 warp = new Vector3(
            Fbm(q * 0.9f + oWarpX, 3),
            Fbm(q * 0.9f + oWarpY, 3),
            Fbm(q * 0.9f + oWarpZ, 3)
        ) * coastWarp;

        float continents = Fbm(q + warp + oBase, octaves);
        float detail = Fbm(q * 4f + oDetail, 4) * 0.12f;

        return continents + detail;
    }

    // El nivel del mar se calcula para que la tierra ocupe exactamente landFraction
    void CalibrateSeaLevel()
    {
        const int samples = 12000;
        List<float> values = new List<float>(samples);
        float golden = Mathf.PI * (3f - Mathf.Sqrt(5f));

        for (int i = 0; i < samples; i++)
        {
            float y = 1f - 2f * (i + 0.5f) / samples;
            float r = Mathf.Sqrt(1f - y * y);
            float a = golden * i;

            values.Add(Elevation(new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r)));
        }

        values.Sort();

        seaElevation = values[Mathf.Clamp((int)((1f - landFraction) * samples), 0, samples - 1)];
        minElevation = values[0];
        maxElevation = values[samples - 1];
    }

    Terrain Sample(Vector3 dir, bool withMountains)
    {
        Terrain t = new Terrain();
        t.elevation = Elevation(dir);
        t.isLand = t.elevation > seaElevation;

        if (!t.isLand)
        {
            t.depth = Mathf.Clamp01(Mathf.InverseLerp(seaElevation, minElevation, t.elevation));
            return t;
        }

        t.land = Mathf.Clamp01(Mathf.InverseLerp(seaElevation, maxElevation, t.elevation));

        if (withMountains)
        {
            Vector3 q = dir * continentScale;

            // Cinturones montañosos: líneas donde un ruido de baja frecuencia cruza el cero
            float belt = 1f - Mathf.Abs(Fbm(q * 0.9f + oBelt, 3)) * 2.2f;
            belt = Smooth(0.35f, 0.9f, belt);

            // Crestas detalladas dentro de los cinturones
            float ridge = 1f - Mathf.Abs(Fbm(q * 3.5f + oRidge, 4)) * 1.6f;
            ridge = Mathf.Clamp01(ridge);

            float inland = Smooth(0.02f, 0.25f, t.land);
            t.mountain = Mathf.Clamp01(belt * (0.35f + 0.65f * ridge * ridge)) * inland;
        }

        return t;
    }

    float RelativeRadius(Vector3 dir)
    {
        if (flatSurface && !heightsFromVertices) return 1f;
        if (reliefSmoothing <= 0f) return RawRelativeRadius(dir);

        // Desenfoque: promedia la altura del punto con 6 vecinos en un anillo (suaviza crestas y picos)
        Vector3 t1 = Vector3.Cross(Vector3.up, dir);
        if (t1.sqrMagnitude < 1e-6f) t1 = Vector3.Cross(Vector3.right, dir);
        t1.Normalize();
        Vector3 t2 = Vector3.Cross(dir, t1);

        float spread = reliefSmoothing * 0.05f;
        float sum = RawRelativeRadius(dir) * 2f;
        for (int i = 0; i < 6; i++)
        {
            float a = i / 6f * Mathf.PI * 2f;
            sum += RawRelativeRadius((dir + (t1 * Mathf.Cos(a) + t2 * Mathf.Sin(a)) * spread).normalized);
        }
        return sum / 8f;
    }

    float RawRelativeRadius(Vector3 dir)
    {
        Terrain t = Sample(dir, true);
        if (!t.isLand) return 1f;

        // Llanuras suaves + montañas en los cinturones
        return 1f + (t.land * 0.25f + t.mountain * 0.75f) * mountainHeight;
    }

    Vector3 SurfacePoint(Vector3 dir)
    {
        float relative = 1f + (RelativeRadius(dir) - 1f) * reliefScale;
        Vector3 p = dir * (relative * FinalRadius);

        // Esferoide achatado por los polos: el eje vertical se acorta
        p.y *= 1f - flattening;
        return p;
    }

    // ---------------------------------------------------------------- Esferoide poligonal

    // Icosaedro subdividido: caras triangulares casi iguales, sin polos deformados
    static void BuildIcosphere(int subdiv, out List<Vector3> dirs, out List<int> tris)
    {
        float t = (1f + Mathf.Sqrt(5f)) / 2f;

        dirs = new List<Vector3>
        {
            new Vector3(-1,  t, 0), new Vector3( 1,  t, 0), new Vector3(-1, -t, 0), new Vector3( 1, -t, 0),
            new Vector3(0, -1,  t), new Vector3(0,  1,  t), new Vector3(0, -1, -t), new Vector3(0,  1, -t),
            new Vector3( t, 0, -1), new Vector3( t, 0,  1), new Vector3(-t, 0, -1), new Vector3(-t, 0,  1)
        };
        for (int i = 0; i < dirs.Count; i++) dirs[i] = dirs[i].normalized;

        tris = new List<int>
        {
            0, 11, 5,  0, 5, 1,  0, 1, 7,  0, 7, 10,  0, 10, 11,
            1, 5, 9,  5, 11, 4,  11, 10, 2,  10, 7, 6,  7, 1, 8,
            3, 9, 4,  3, 4, 2,  3, 2, 6,  3, 6, 8,  3, 8, 9,
            4, 9, 5,  2, 4, 11,  6, 2, 10,  8, 6, 7,  9, 8, 1
        };

        for (int s = 0; s < subdiv; s++)
        {
            Dictionary<long, int> middle = new Dictionary<long, int>();
            List<int> next = new List<int>(tris.Count * 4);

            for (int f = 0; f < tris.Count; f += 3)
            {
                int a = tris[f], b = tris[f + 1], c = tris[f + 2];
                int ab = Midpoint(dirs, middle, a, b);
                int bc = Midpoint(dirs, middle, b, c);
                int ca = Midpoint(dirs, middle, c, a);

                next.AddRange(new[] { a, ab, ca,  b, bc, ab,  c, ca, bc,  ab, bc, ca });
            }

            tris = next;
        }
    }

    static int Midpoint(List<Vector3> dirs, Dictionary<long, int> cache, int a, int b)
    {
        long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
        if (cache.TryGetValue(key, out int index)) return index;

        dirs.Add(((dirs[a] + dirs[b]) * 0.5f).normalized);
        index = dirs.Count - 1;
        cache[key] = index;
        return index;
    }

    // Malla low-poly: cada triángulo con sus propios vértices (sombreado plano) y un color único por cara,
    // guardado en una pequeña paleta de un píxel por cara.
    Mesh BuildPolygonalMesh(out Texture2D palette)
    {
        transform.localScale = Vector3.one;

        BuildIcosphere(subdivisions, out List<Vector3> dirs, out List<int> tris);

        // Altura de cada vértice del icosaedro (se calcula una sola vez por vértice)
        Vector3[] points = new Vector3[dirs.Count];
        for (int i = 0; i < dirs.Count; i++)
        {
            points[i] = SurfacePoint(dirs[i]);
        }

        int faces = tris.Count / 3;
        const int paletteWidth = 512;
        int paletteHeight = Mathf.CeilToInt(faces / (float)paletteWidth);

        Color32[] colors = new Color32[paletteWidth * paletteHeight];
        Vector3[] vertices = new Vector3[faces * 3];
        Vector3[] normals = new Vector3[faces * 3];
        Vector2[] uv = new Vector2[faces * 3];
        int[] indices = new int[faces * 3];

        for (int f = 0; f < faces; f++)
        {
            int ia = tris[f * 3], ib = tris[f * 3 + 1], ic = tris[f * 3 + 2];

            Vector3 pa = points[ia];
            Vector3 pb = points[ib];
            Vector3 pc = points[ic];

            // Sentido horario visto desde fuera: la cara exterior es la visible
            Vector3 normal = Vector3.Cross(pb - pa, pc - pa);
            if (Vector3.Dot(normal, pa + pb + pc) < 0f)
            {
                Vector3 swap = pb; pb = pc; pc = swap;
                normal = -normal;
            }
            normal.Normalize();

            // Color de la cara según el terreno en su centro, con una ligera variación para marcar las facetas
            Vector3 center = (dirs[ia] + dirs[ib] + dirs[ic]).normalized;
            Color c = Shade(center, Mathf.Asin(Mathf.Clamp(center.y, -1f, 1f)));
            float jitter = 0.95f + 0.1f * Hash01(f);
            colors[f] = new Color(c.r * jitter, c.g * jitter, c.b * jitter);

            Vector2 pixel = new Vector2((f % paletteWidth + 0.5f) / paletteWidth, (f / paletteWidth + 0.5f) / paletteHeight);

            int v = f * 3;
            vertices[v] = pa; vertices[v + 1] = pb; vertices[v + 2] = pc;
            // Sombreado suave: mezcla la normal plana con la radial de cada vértice
            float soft = Mathf.Clamp01(reliefSmoothing / 3f) * 0.85f;
            normals[v] = Vector3.Slerp(normal, pa.normalized, soft);
            normals[v + 1] = Vector3.Slerp(normal, pb.normalized, soft);
            normals[v + 2] = Vector3.Slerp(normal, pc.normalized, soft);
            uv[v] = uv[v + 1] = uv[v + 2] = pixel;
            indices[v] = v; indices[v + 1] = v + 1; indices[v + 2] = v + 2;
        }

        palette = new Texture2D(paletteWidth, paletteHeight, TextureFormat.RGB24, false);
        palette.filterMode = FilterMode.Point;
        palette.wrapMode = TextureWrapMode.Clamp;
        palette.SetPixels32(colors);
        palette.Apply();

        Mesh mesh = new Mesh { name = "Tierra poligonal" };
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.vertices = vertices;
        mesh.normals = normals;
        mesh.uv = uv;
        mesh.triangles = indices;
        mesh.RecalculateBounds();

        return mesh;
    }

    static float Hash01(int n)
    {
        uint h = (uint)n * 2654435761u;
        h ^= h >> 15;
        h *= 2246822519u;
        h ^= h >> 13;
        return (h & 0xFFFF) / 65535f;
    }

    // ---------------------------------------------------------------- Malla

    Mesh BuildMesh()
    {
        int cols = longitudeSegments + 1;
        int rows = latitudeSegments + 1;

        Vector3[] vertices = new Vector3[cols * rows];
        Vector3[] normals = new Vector3[vertices.Length];
        Vector2[] uv = new Vector2[vertices.Length];
        int[] triangles = new int[longitudeSegments * latitudeSegments * 6];

        // La malla ya tiene el tamaño final, así que la escala del transform vuelve a 1
        transform.localScale = Vector3.one;

        const float eps = 0.002f;

        for (int y = 0; y < rows; y++)
        {
            float v = (float)y / latitudeSegments;
            float lat = (v - 0.5f) * Mathf.PI;

            for (int x = 0; x < cols; x++)
            {
                float u = (float)x / longitudeSegments;
                float lon = u * Mathf.PI * 2f;

                Vector3 dir = new Vector3(
                    Mathf.Cos(lat) * Mathf.Cos(lon),
                    Mathf.Sin(lat),
                    Mathf.Cos(lat) * Mathf.Sin(lon)
                );

                int i = y * cols + x;
                vertices[i] = SurfacePoint(dir);
                uv[i] = new Vector2(u, v);

                // Normal por diferencias finitas sobre la superficie, sin costura
                Vector3 t1 = Vector3.Cross(Vector3.up, dir);
                if (t1.sqrMagnitude < 1e-6f) t1 = Vector3.Cross(Vector3.right, dir);
                t1.Normalize();
                Vector3 t2 = Vector3.Cross(dir, t1);

                Vector3 pA = SurfacePoint((dir + t1 * eps).normalized);
                Vector3 pB = SurfacePoint((dir + t2 * eps).normalized);
                Vector3 n = Vector3.Cross(pA - vertices[i], pB - vertices[i]).normalized;
                if (Vector3.Dot(n, dir) < 0f) n = -n;

                normals[i] = n;
            }
        }

        int tri = 0;
        for (int y = 0; y < latitudeSegments; y++)
        {
            for (int x = 0; x < longitudeSegments; x++)
            {
                int a = y * cols + x;
                int b = a + 1;
                int c = a + cols;
                int d = c + 1;

                // Sentido horario visto desde fuera (así Unity dibuja la cara exterior)
                triangles[tri++] = a; triangles[tri++] = c; triangles[tri++] = b;
                triangles[tri++] = b; triangles[tri++] = c; triangles[tri++] = d;
            }
        }

        Mesh mesh = new Mesh { name = "Tierra procedural" };
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.vertices = vertices;
        mesh.normals = normals;
        mesh.uv = uv;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();

        return mesh;
    }

    // ---------------------------------------------------------------- Textura y biomas

    Texture2D BuildTexture()
    {
        int width = textureWidth;
        int height = width / 2;

        Texture2D tex = new Texture2D(width, height, TextureFormat.RGB24, true);
        tex.wrapModeU = TextureWrapMode.Repeat;
        tex.wrapModeV = TextureWrapMode.Clamp;

        Color32[] pixels = new Color32[width * height];

        for (int y = 0; y < height; y++)
        {
            float lat = ((float)y / (height - 1) - 0.5f) * Mathf.PI;

            for (int x = 0; x < width; x++)
            {
                float lon = (float)x / width * Mathf.PI * 2f;

                Vector3 dir = new Vector3(
                    Mathf.Cos(lat) * Mathf.Cos(lon),
                    Mathf.Sin(lat),
                    Mathf.Cos(lat) * Mathf.Sin(lon)
                );

                pixels[y * width + x] = Shade(dir, lat);
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply();
        return tex;
    }

    Color Shade(Vector3 dir, float latitude)
    {
        Terrain t = Sample(dir, true);
        float absLat = Mathf.Abs(latitude);
        float polar = Mathf.Abs(dir.y);

        if (!t.isLand)
        {
            // Plataforma continental clara junto a la costa, abismo oscuro mar adentro
            Color sea = Color.Lerp(
                new Color(0.05f, 0.42f, 0.7f),
                new Color(0.01f, 0.07f, 0.26f),
                Smooth(0f, 0.55f, t.depth)
            );

            float pack = Smooth(0.86f, 0.94f, polar + Fbm(dir * 6f + oDetail, 3) * 0.05f);
            return Color.Lerp(sea, new Color(0.9f, 0.94f, 0.97f), pack);
        }

        Climate(dir, absLat, t, out float temp, out float moisture);

        Color tundra = new Color(0.55f, 0.58f, 0.5f);
        Color taiga = new Color(0.12f, 0.28f, 0.2f);
        Color cold = Color.Lerp(tundra, taiga, Smooth(0.35f, 0.6f, moisture));

        Color temperate = Row(moisture,
            new Color(0.62f, 0.6f, 0.35f),
            new Color(0.3f, 0.5f, 0.18f),
            new Color(0.11f, 0.32f, 0.12f));

        Color hot = Row(moisture,
            new Color(0.82f, 0.7f, 0.44f),
            new Color(0.66f, 0.6f, 0.27f),
            new Color(0.05f, 0.3f, 0.08f));

        Color c = Color.Lerp(cold, temperate, Smooth(0.25f, 0.42f, temp));
        c = Color.Lerp(c, hot, Smooth(0.58f, 0.75f, temp));

        // Costa arenosa
        c = Color.Lerp(new Color(0.78f, 0.72f, 0.52f), c, Smooth(0f, 0.025f, t.land));

        // Roca en montañas y nieve en las cumbres o donde hace frío
        c = Color.Lerp(c, new Color(0.45f, 0.4f, 0.36f), Smooth(0.25f, 0.6f, t.mountain));
        float snow = Mathf.Max(Smooth(0.2f, 0.08f, temp), Smooth(0.6f, 0.85f, t.mountain));
        c = Color.Lerp(c, new Color(0.94f, 0.95f, 0.97f), snow);

        return c;
    }

    // Temperatura: calor en el ecuador, frío en los polos y con la altitud.
    // Humedad: ruido + bandas climáticas (húmedo en el ecuador y a ~50°, seco a ~25°)
    void Climate(Vector3 dir, float absLat, Terrain t, out float temp, out float moisture)
    {
        temp = Mathf.Cos(absLat) - t.land * 0.45f - t.mountain * 0.2f
             + Fbm(dir * 3f + oMoist, 3) * 0.06f;

        float bands = 0.5f + 0.5f * Mathf.Cos(absLat * 7f);
        moisture = Mathf.Clamp01(0.5f + Fbm(dir * continentScale * 1.6f + oMoist, 4) * 0.7f) * 0.55f
                 + bands * 0.45f;
    }

    // ---------------------------------------------------------------- Consulta del terreno (para el juego)

    public bool IsReady => perm != null;

    /// <summary>
    /// Altura relativa para trazar ríos (vector unitario en espacio local): negativa en el mar (más cuanto más profundo),
    /// y en tierra de 0 en la costa a 1 en las cumbres. Es la misma que da relieve a la superficie.
    /// </summary>
    public float RelativeHeight(Vector3 dir)
    {
        Terrain t = Sample(dir, true);
        return t.isLand ? t.land * 0.25f + t.mountain * 0.75f : -t.depth - 0.01f;
    }

    /// <summary>Bioma en esta dirección (vector unitario en espacio local).</summary>
    public Biome BiomeAt(Vector3 dir)
    {
        Terrain t = Sample(dir, true);
        if (!t.isLand) return Biome.Sea;

        if (t.mountain > 0.6f || t.land > 0.9f) return Biome.Mountain;
        if (t.mountain > 0.3f) return Biome.Hills;

        Climate(dir, Mathf.Asin(Mathf.Clamp(Mathf.Abs(dir.y), 0f, 1f)), t, out float temp, out float moisture);

        if (temp < 0.22f) return Biome.Tundra;
        if (temp > 0.55f && moisture < 0.38f) return Biome.Desert;
        if (moisture > 0.6f) return Biome.Forest;
        return Biome.Plains;
    }

    /// <summary>Punto de la superficie (espacio local) en esta dirección.</summary>
    public Vector3 LocalSurfacePoint(Vector3 dir)
    {
        // Ligeramente por encima: las caras planas quedan algo por debajo de la superficie ideal
        return SurfacePoint(dir) + dir * (FinalRadius * 0.0012f);
    }

    static Color Row(float moisture, Color dry, Color mid, Color wet)
    {
        return moisture < 0.5f
            ? Color.Lerp(dry, mid, Smooth(0.2f, 0.5f, moisture))
            : Color.Lerp(mid, wet, Smooth(0.5f, 0.8f, moisture));
    }

    Material BuildMaterial(Texture2D tex)
    {
        Shader shader = Shader.Find("Standard");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Sprites/Default");

        Material m = new Material(shader);
        m.mainTexture = tex;
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.3f);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.3f);
        return m;
    }

    // ---------------------------------------------------------------- Ruido

    // smoothstep estilo shader (Mathf.SmoothStep tiene otra firma: interpola entre from y to)
    static float Smooth(float edge0, float edge1, float x)
    {
        float t = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
        return t * t * (3f - 2f * t);
    }

    void InitNoise(int s)
    {
        System.Random rng = new System.Random(s);

        int[] p = new int[256];
        for (int i = 0; i < 256; i++) p[i] = i;
        for (int i = 255; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            int tmp = p[i]; p[i] = p[j]; p[j] = tmp;
        }

        perm = new int[512];
        for (int i = 0; i < 512; i++) perm[i] = p[i & 255];

        oBase = RandomOffset(rng);
        oWarpX = RandomOffset(rng);
        oWarpY = RandomOffset(rng);
        oWarpZ = RandomOffset(rng);
        oDetail = RandomOffset(rng);
        oBelt = RandomOffset(rng);
        oRidge = RandomOffset(rng);
        oMoist = RandomOffset(rng);
    }

    static Vector3 RandomOffset(System.Random rng)
    {
        return new Vector3(
            (float)rng.NextDouble() * 200f - 100f,
            (float)rng.NextDouble() * 200f - 100f,
            (float)rng.NextDouble() * 200f - 100f
        );
    }

    // Ruido de Perlin mejorado en 3D, devuelve aproximadamente -1..1
    float Noise(float x, float y, float z)
    {
        int X = Mathf.FloorToInt(x) & 255;
        int Y = Mathf.FloorToInt(y) & 255;
        int Z = Mathf.FloorToInt(z) & 255;

        x -= Mathf.Floor(x);
        y -= Mathf.Floor(y);
        z -= Mathf.Floor(z);

        float u = Fade(x), v = Fade(y), w = Fade(z);

        int A = perm[X] + Y, AA = perm[A] + Z, AB = perm[A + 1] + Z;
        int B = perm[X + 1] + Y, BA = perm[B] + Z, BB = perm[B + 1] + Z;

        return Mathf.Lerp(
            Mathf.Lerp(
                Mathf.Lerp(Grad(perm[AA], x, y, z), Grad(perm[BA], x - 1, y, z), u),
                Mathf.Lerp(Grad(perm[AB], x, y - 1, z), Grad(perm[BB], x - 1, y - 1, z), u), v),
            Mathf.Lerp(
                Mathf.Lerp(Grad(perm[AA + 1], x, y, z - 1), Grad(perm[BA + 1], x - 1, y, z - 1), u),
                Mathf.Lerp(Grad(perm[AB + 1], x, y - 1, z - 1), Grad(perm[BB + 1], x - 1, y - 1, z - 1), u), v),
            w);
    }

    static float Fade(float t)
    {
        return t * t * t * (t * (t * 6f - 15f) + 10f);
    }

    static float Grad(int hash, float x, float y, float z)
    {
        int h = hash & 15;
        float u = h < 8 ? x : y;
        float v = h < 4 ? y : (h == 12 || h == 14 ? x : z);
        return ((h & 1) == 0 ? u : -u) + ((h & 2) == 0 ? v : -v);
    }

    // fBm normalizado a aproximadamente -1..1
    float Fbm(Vector3 p, int oct)
    {
        float sum = 0f, amplitude = 1f, norm = 0f;

        for (int i = 0; i < oct; i++)
        {
            sum += Noise(p.x, p.y, p.z) * amplitude;
            norm += amplitude;
            amplitude *= 0.5f;
            p *= 2.03f;
        }

        return Mathf.Clamp(sum / norm * 1.5f, -1f, 1f);
    }
}
