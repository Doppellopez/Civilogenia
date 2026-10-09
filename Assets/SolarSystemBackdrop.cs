using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Fondo espacial: cielo estrellado con una banda de Vía Láctea y el sistema solar visible desde la Tierra
/// (Sol con halo, Luna, Mercurio, Venus, Marte, Júpiter, Saturno con anillos, Urano y Neptuno).
/// Las distancias y tamaños son artísticos, no reales, pero relativos al radio de la Tierra.
/// Se crea solo al pulsar Play.
/// </summary>
public class SolarSystemBackdrop : MonoBehaviour
{
    [Header("Escala (en radios de la Tierra)")]
    [Tooltip("Distancia Tierra-Sol")]
    public float sunDistance = 150f;
    public float sunRadius = 5f;
    public float moonDistance = 18f;
    public float moonRadius = 0.3f;

    [Header("Movimiento")]
    [Tooltip("Velocidad de las órbitas (0 = todo quieto)")]
    public float orbitTimeScale = 1f;

    [Header("Cielo")]
    public int starTextureWidth = 2048;
    public int starCount = 2600;

    // Nombre, radio (radios terrestres), órbita (x distancia Tierra-Sol), periodo (s), color A, color B, bandas, anillos
    struct PlanetDef
    {
        public string name;
        public float radius, orbit, period;
        public Color a, b;
        public float bands;
        public bool rings;
    }

    float earthRadius = 500f;
    Camera cam;
    Vector3 sunPos;
    Vector3 planeU, planeV;
    readonly List<Orbiter> orbiters = new List<Orbiter>();

    // Valor común para los planos de recorte de la cámara
    public static float FarClipFor(float earthRadius)
    {
        return earthRadius * 1500f;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Create()
    {
        if (FindFirstObjectByType<SolarSystemBackdrop>() != null) return;
        new GameObject("Sistema solar (fondo)").AddComponent<SolarSystemBackdrop>();
    }

    void Start()
    {
        cam = Camera.main;

        EarthTopography earth = FindFirstObjectByType<EarthTopography>();
        if (earth != null) earthRadius = earth.FinalRadius;
        Vector3 earthPos = earth != null ? earth.transform.position : Vector3.zero;

        float far = FarClipFor(earthRadius);
        if (cam != null && cam.farClipPlane < far) cam.farClipPlane = far;

        // El Sol está en la dirección opuesta a la que viaja la luz direccional
        Vector3 toSun = -FindSunLightForward();
        float earthOrbit = sunDistance * earthRadius;
        sunPos = earthPos + toSun * earthOrbit;

        // Plano de las órbitas: contiene al Sol y a la Tierra
        planeU = -toSun;
        planeV = Vector3.Cross(Vector3.up, planeU);
        if (planeV.sqrMagnitude < 1e-4f) planeV = Vector3.Cross(Vector3.right, planeU);
        planeV.Normalize();

        BuildSky(far * 0.8f);
        BuildSun();
        BuildMoon(earthPos);
        BuildPlanets(earthOrbit);
    }

    void Update()
    {
        foreach (Orbiter o in orbiters) o.Step(Time.deltaTime * orbitTimeScale);
    }

    Vector3 FindSunLightForward()
    {
        Light sun = RenderSettings.sun;

        if (sun == null)
        {
            foreach (Light l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Directional) { sun = l; break; }
        }

        return sun != null ? sun.transform.forward : Quaternion.Euler(35f, -40f, 0f) * Vector3.forward;
    }

    // ---------------------------------------------------------------- Cielo

    void BuildSky(float radius)
    {
        Mesh source = SphereMesh();

        Mesh inverted = new Mesh { name = "Cielo" };
        inverted.vertices = source.vertices;
        inverted.uv = source.uv;

        Vector3[] normals = source.normals;
        for (int i = 0; i < normals.Length; i++) normals[i] = -normals[i];
        inverted.normals = normals;

        int[] tris = source.triangles;
        for (int i = 0; i < tris.Length; i += 3)
        {
            int t = tris[i]; tris[i] = tris[i + 1]; tris[i + 1] = t;
        }
        inverted.triangles = tris;

        GameObject sky = new GameObject("Cielo estrellado");
        sky.transform.SetParent(transform, false);
        sky.transform.localScale = Vector3.one * radius * 2f;
        sky.AddComponent<MeshFilter>().sharedMesh = inverted;

        MeshRenderer r = sky.AddComponent<MeshRenderer>();
        r.sharedMaterial = UnlitMaterial(BuildStarTexture());
        r.sharedMaterial.renderQueue = 1000; // al fondo
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;

        sky.AddComponent<FollowMainCamera>();
    }

    Texture2D BuildStarTexture()
    {
        int w = starTextureWidth, h = w / 2;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        tex.wrapModeU = TextureWrapMode.Repeat;
        tex.wrapModeV = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        Color32[] px = new Color32[w * h];

        // Vía Láctea: banda tenue y con grumos alrededor de un plano inclinado
        Vector3 galactic = new Vector3(0.3f, 1f, 0.2f).normalized;
        for (int y = 0; y < h; y++)
        {
            float lat = ((float)y / (h - 1) - 0.5f) * Mathf.PI;
            for (int x = 0; x < w; x++)
            {
                float lon = (float)x / w * Mathf.PI * 2f;
                Vector3 d = new Vector3(Mathf.Cos(lat) * Mathf.Cos(lon), Mathf.Sin(lat), Mathf.Cos(lat) * Mathf.Sin(lon));

                float b = Vector3.Dot(d, galactic) / 0.16f;
                float band = Mathf.Exp(-b * b);
                if (band < 0.01f) continue;

                float cloud = 0.4f + 0.6f * Noise3(d * 5f + new Vector3(11f, 7f, 3f));
                float v = band * cloud * 0.32f;
                px[y * w + x] = new Color(v * 0.85f, v * 0.9f, v);
            }
        }

        // Estrellas sueltas: tamaño y brillo variables
        System.Random rng = new System.Random(20240611);
        for (int i = 0; i < starCount; i++)
        {
            int x = rng.Next(2, w - 2), y = rng.Next(2, h - 2);
            float b = (float)rng.NextDouble();
            b = 0.3f + b * b * b * 0.7f;

            Color c = Color.Lerp(new Color(0.7f, 0.8f, 1f), new Color(1f, 0.9f, 0.7f), (float)rng.NextDouble()) * b;
            px[y * w + x] = c;

            if (b > 0.65f)
            {
                Color halo = c * 0.4f;
                px[y * w + x + 1] = halo; px[y * w + x - 1] = halo;
                px[(y + 1) * w + x] = halo; px[(y - 1) * w + x] = halo;
            }
        }

        tex.SetPixels32(px);
        tex.Apply();
        return tex;
    }

    // ---------------------------------------------------------------- Sol, Luna y planetas

    void BuildSun()
    {
        float r = sunRadius * earthRadius;

        GameObject sun = CreateBody("Sol", sunPos, r,
            UnlitMaterial(PlanetTexture(1, new Color(1f, 0.95f, 0.55f), new Color(1f, 0.55f, 0.1f), 0f)));

        // Halo: sprite con degradado radial que mira siempre a la cámara
        GameObject glow = new GameObject("Halo del Sol");
        glow.transform.SetParent(sun.transform, false);
        glow.transform.localScale = Vector3.one * 2f * 3.2f; // relativo al diámetro (la esfera mide 1 de radio 0.5)
        MeshFilter mf = glow.AddComponent<MeshFilter>();
        mf.sharedMesh = QuadMesh();
        MeshRenderer mr = glow.AddComponent<MeshRenderer>();
        mr.sharedMaterial = SpriteMaterial(GlowTexture());
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        glow.AddComponent<Billboard>();
    }

    void BuildMoon(Vector3 earthPos)
    {
        GameObject moon = CreateBody("Luna", earthPos, moonRadius * earthRadius, LitOrUnlit(
            PlanetTexture(5, new Color(0.78f, 0.78f, 0.76f), new Color(0.3f, 0.3f, 0.3f), 0f)));

        Orbiter o = moon.AddComponent<Orbiter>();
        o.Setup(earthPos, planeU, planeV, moonDistance * earthRadius, 40f, Random.value * 360f);
        orbiters.Add(o);
    }

    void BuildPlanets(float earthOrbit)
    {
        PlanetDef[] defs =
        {
            Def("Mercurio", 0.35f, 0.40f, 60f,   new Color(0.62f, 0.6f, 0.58f),  new Color(0.25f, 0.24f, 0.23f), 0f),
            Def("Venus",    0.9f,  0.70f, 100f,  new Color(0.95f, 0.82f, 0.55f), new Color(0.75f, 0.55f, 0.3f),   3f),
            Def("Marte",    0.5f,  1.50f, 190f,  new Color(0.75f, 0.35f, 0.17f), new Color(0.4f, 0.17f, 0.09f),   0f),
            Def("Júpiter",  3.2f,  2.40f, 400f,  new Color(0.92f, 0.82f, 0.68f), new Color(0.6f, 0.36f, 0.22f),   7f),
            Def("Saturno",  2.6f,  3.40f, 650f,  new Color(0.93f, 0.85f, 0.62f), new Color(0.72f, 0.6f, 0.4f),    6f, true),
            Def("Urano",    2.0f,  4.40f, 950f,  new Color(0.6f, 0.85f, 0.9f),   new Color(0.45f, 0.7f, 0.8f),    2f),
            Def("Neptuno",  2.0f,  5.40f, 1300f, new Color(0.25f, 0.45f, 0.95f), new Color(0.1f, 0.2f, 0.6f),     3f),
        };

        int seed = 100;
        foreach (PlanetDef d in defs)
        {
            float r = d.radius * earthRadius;
            GameObject body = CreateBody(d.name, sunPos, r, UnlitMaterial(PlanetTexture(seed++, d.a, d.b, d.bands)));

            if (d.rings) AddRings(body.transform, d);

            Orbiter o = body.AddComponent<Orbiter>();
            o.Setup(sunPos, planeU, planeV, d.orbit * earthOrbit, d.period, Random.value * 360f);
            orbiters.Add(o);
        }
    }

    static PlanetDef Def(string name, float radius, float orbit, float period, Color a, Color b, float bands, bool rings = false)
    {
        return new PlanetDef { name = name, radius = radius, orbit = orbit, period = period, a = a, b = b, bands = bands, rings = rings };
    }

    GameObject CreateBody(string bodyName, Vector3 position, float radius, Material material)
    {
        GameObject go = new GameObject(bodyName);
        go.transform.SetParent(transform, false);
        go.transform.position = position;
        go.transform.localScale = Vector3.one * radius * 2f;

        go.AddComponent<MeshFilter>().sharedMesh = SphereMesh();
        MeshRenderer r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = material;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        return go;
    }

    void AddRings(Transform planet, PlanetDef d)
    {
        // El planeta tiene escala = diámetro, así que el anillo se define en unidades de diámetro
        const int segments = 96;
        float inner = 0.5f * 1.35f, outer = 0.5f * 2.4f;

        Vector3[] v = new Vector3[(segments + 1) * 2];
        Vector2[] uv = new Vector2[v.Length];
        int[] t = new int[segments * 6];

        for (int i = 0; i <= segments; i++)
        {
            float a = (float)i / segments * Mathf.PI * 2f;
            Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            v[i * 2] = dir * inner;
            v[i * 2 + 1] = dir * outer;
            uv[i * 2] = new Vector2(0f, 0f);
            uv[i * 2 + 1] = new Vector2(1f, 1f);
        }

        for (int i = 0; i < segments; i++)
        {
            int a = i * 2, b = a + 1, c = a + 2, e = a + 3;
            t[i * 6] = a; t[i * 6 + 1] = b; t[i * 6 + 2] = c;
            t[i * 6 + 3] = c; t[i * 6 + 4] = b; t[i * 6 + 5] = e;
        }

        Mesh mesh = new Mesh { name = "Anillos", vertices = v, uv = uv, triangles = t };
        mesh.RecalculateNormals();

        GameObject rings = new GameObject("Anillos");
        rings.transform.SetParent(planet, false);
        rings.transform.localRotation = Quaternion.Euler(27f, 0f, 0f);
        rings.AddComponent<MeshFilter>().sharedMesh = mesh;

        MeshRenderer r = rings.AddComponent<MeshRenderer>();
        r.sharedMaterial = SpriteMaterial(RingTexture(d));
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    // ---------------------------------------------------------------- Materiales y texturas

    static Material UnlitMaterial(Texture tex)
    {
        Shader s = Shader.Find("Unlit/Texture");
        if (s == null) s = Shader.Find("Sprites/Default");

        Material m = new Material(s);
        m.mainTexture = tex;
        return m;
    }

    // La Luna usa el shader iluminado para mostrar sus fases con la luz del Sol
    static Material LitOrUnlit(Texture tex)
    {
        Shader s = Shader.Find("Standard");
        if (s == null) return UnlitMaterial(tex);

        Material m = new Material(s);
        m.mainTexture = tex;
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.05f);
        return m;
    }

    static Material SpriteMaterial(Texture tex)
    {
        Material m = new Material(Shader.Find("Sprites/Default"));
        m.mainTexture = tex;
        m.renderQueue = 3000;
        return m;
    }

    static Texture2D PlanetTexture(int seed, Color a, Color b, float bands)
    {
        const int w = 256, h = 128;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGB24, true);
        tex.wrapModeU = TextureWrapMode.Repeat;
        tex.wrapModeV = TextureWrapMode.Clamp;

        Vector3 off = new Vector3(seed * 13.7f % 90f + 20f, seed * 7.3f % 90f + 20f, seed * 3.1f % 90f + 20f);
        Color32[] px = new Color32[w * h];

        for (int y = 0; y < h; y++)
        {
            float lat = ((float)y / (h - 1) - 0.5f) * Mathf.PI;
            for (int x = 0; x < w; x++)
            {
                float lon = (float)x / w * Mathf.PI * 2f;
                Vector3 d = new Vector3(Mathf.Cos(lat) * Mathf.Cos(lon), Mathf.Sin(lat), Mathf.Cos(lat) * Mathf.Sin(lon));

                float n = Noise3(d * 3f + off) * 0.65f + Noise3(d * 9f + off) * 0.35f;
                float t = bands > 0f
                    ? 0.5f + 0.5f * Mathf.Sin(d.y * bands * Mathf.PI + (n - 0.5f) * 3f)
                    : n;

                px[y * w + x] = Color.Lerp(b, a, Mathf.Clamp01(t));
            }
        }

        tex.SetPixels32(px);
        tex.Apply();
        return tex;
    }

    static Texture2D GlowTexture()
    {
        const int size = 128;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;

        Color32[] px = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(size / 2f, size / 2f)) / (size / 2f);
                float a = Mathf.Clamp01(1f - d);
                a = a * a * a;
                px[y * size + x] = new Color(1f, 0.75f, 0.35f, a * 0.85f);
            }
        }

        tex.SetPixels32(px);
        tex.Apply();
        return tex;
    }

    static Texture2D RingTexture(PlanetDef d)
    {
        const int w = 256;
        Texture2D tex = new Texture2D(w, 1, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;

        Color32[] px = new Color32[w];
        for (int i = 0; i < w; i++)
        {
            float t = (float)i / (w - 1);
            float bands = Mathf.PerlinNoise(t * 40f + 3f, 0.5f) * 0.6f + Mathf.PerlinNoise(t * 110f + 3f, 3.3f) * 0.4f;

            float alpha = Mathf.Clamp01((bands - 0.25f) / 0.45f);
            alpha *= Mathf.Clamp01(t / 0.05f) * Mathf.Clamp01((1f - t) / 0.08f);
            if (t > 0.58f && t < 0.64f) alpha *= 0.08f;

            Color c = Color.Lerp(d.b, d.a, bands);
            px[i] = new Color(c.r, c.g, c.b, alpha * 0.9f);
        }

        tex.SetPixels32(px);
        tex.Apply();
        return tex;
    }

    // Ruido continuo sobre la esfera (aprox. 3D con planos de Perlin), 0..1
    static float Noise3(Vector3 p)
    {
        return (Mathf.PerlinNoise(p.x, p.y) + Mathf.PerlinNoise(p.y, p.z) + Mathf.PerlinNoise(p.z, p.x)) / 3f;
    }

    // ---------------------------------------------------------------- Mallas

    static Mesh sphereMesh, quadMesh;

    // Malla de esfera copiada de una primitiva (funciona igual en el editor y en un build)
    static Mesh SphereMesh()
    {
        if (sphereMesh != null) return sphereMesh;

        GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphereMesh = temp.GetComponent<MeshFilter>().sharedMesh;
        Destroy(temp);
        return sphereMesh;
    }

    static Mesh QuadMesh()
    {
        if (quadMesh != null) return quadMesh;

        GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quadMesh = temp.GetComponent<MeshFilter>().sharedMesh;
        Destroy(temp);
        return quadMesh;
    }
}

/// <summary>Órbita circular alrededor de un punto, dentro del plano definido por u y v.</summary>
public class Orbiter : MonoBehaviour
{
    Vector3 center, u, v;
    float radius, period, angle;

    public void Setup(Vector3 center, Vector3 u, Vector3 v, float radius, float period, float startAngle)
    {
        this.center = center;
        this.u = u;
        this.v = v;
        this.radius = radius;
        this.period = period;
        angle = startAngle;
        Place();
    }

    public void Step(float dt)
    {
        angle = Mathf.Repeat(angle + 360f / period * dt, 360f);
        Place();
    }

    void Place()
    {
        float a = angle * Mathf.Deg2Rad;
        transform.position = center + (u * Mathf.Cos(a) + v * Mathf.Sin(a)) * radius;
    }
}

/// <summary>El objeto siempre mira a la cámara principal.</summary>
public class Billboard : MonoBehaviour
{
    void LateUpdate()
    {
        Camera cam = Camera.main;
        if (cam != null) transform.rotation = cam.transform.rotation;
    }
}

/// <summary>El cielo se mueve con la cámara para que nunca se pueda alcanzar.</summary>
public class FollowMainCamera : MonoBehaviour
{
    void LateUpdate()
    {
        Camera cam = Camera.main;
        if (cam != null) transform.position = cam.transform.position;
    }
}
