using UnityEngine;

// ---------------------------------------------------------------------------------------------------------------------
// Modelos 3D de los edificios de felicidad y de las maravillas añadidas después (Pirámides ... Biosfera).
// ---------------------------------------------------------------------------------------------------------------------
public static partial class CivViews
{
    static Material M(float r, float g, float b) { return LitMaterial(new Color(r, g, b)); }

    // Hileras de columnas (hh = media altura)
    static void Columns(Transform t, Material m, float x0, float x1, float y, float z, int n, float rad, float hh)
    {
        for (int i = 0; i < n; i++)
        {
            float x = n == 1 ? (x0 + x1) * 0.5f : Mathf.Lerp(x0, x1, i / (float)(n - 1));
            Piece(t, Cylinder, m, new Vector3(x, y, z), new Vector3(rad, hh, rad));
        }
    }

    // ---------------------------------------------------------------- Edificios de felicidad

    // Los 11 edificios de felicidad se colocan en un anillo exterior alrededor de la ciudad, mirando al centro
    static Transform HappyAnchor(Transform root, int slot, float size, string name)
    {
        float a = slot / 11f * Mathf.PI * 2f + 0.3f;
        Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));

        GameObject g = new GameObject(name);
        g.transform.SetParent(root, false);
        g.transform.localPosition = dir * size * 2.45f;
        g.transform.localRotation = Quaternion.LookRotation(-dir, Vector3.up);
        return g.transform;
    }

    static void BuildHappinessBuildings(City city, Transform root, float size, Material civMat)
    {
        float s = size;

        if (city.buildings.Contains(BuildingKind.Temple))
        {
            Transform t = HappyAnchor(root, 0, size, "Templo");
            Material stone = M(0.88f, 0.86f, 0.8f), dark = M(0.35f, 0.33f, 0.3f);
            Piece(t, Cube, stone, new Vector3(0f, s * 0.03f, 0f), new Vector3(s * 0.7f, s * 0.06f, s * 0.5f));
            Piece(t, Cube, stone, new Vector3(0f, s * 0.08f, 0f), new Vector3(s * 0.62f, s * 0.04f, s * 0.44f));
            Piece(t, Cube, dark, new Vector3(0f, s * 0.2f, 0f), new Vector3(s * 0.46f, s * 0.22f, s * 0.28f));
            Columns(t, stone, -s * 0.27f, s * 0.27f, s * 0.2f, s * 0.18f, 6, s * 0.04f, s * 0.12f);
            Columns(t, stone, -s * 0.27f, s * 0.27f, s * 0.2f, -s * 0.18f, 6, s * 0.04f, s * 0.12f);
            Piece(t, Cube, stone, new Vector3(0f, s * 0.34f, 0f), new Vector3(s * 0.64f, s * 0.04f, s * 0.44f));
            for (int side = -1; side <= 1; side += 2)
            {
                GameObject roof = Piece(t, Cube, civMat, new Vector3(0f, s * 0.42f, side * s * 0.1f), new Vector3(s * 0.66f, s * 0.02f, s * 0.26f));
                roof.transform.localRotation = Quaternion.Euler(-side * 22f, 0f, 0f);
            }
        }

        if (city.buildings.Contains(BuildingKind.Amphitheater))
        {
            Transform t = HappyAnchor(root, 1, size, "Anfiteatro");
            Material stone = M(0.8f, 0.76f, 0.66f), sand = M(0.86f, 0.78f, 0.55f), dark = M(0.2f, 0.18f, 0.16f);
            Piece(t, Cylinder, sand, new Vector3(0f, s * 0.02f, 0f), new Vector3(s * 0.56f, s * 0.02f, s * 0.56f));
            for (int ring = 0; ring < 2; ring++)
                for (int i = 0; i < 14; i++)
                {
                    float a = i / 14f * Mathf.PI * 2f;
                    Vector3 r = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    float rad = s * (ring == 0 ? 0.42f : 0.33f);
                    GameObject b = Piece(t, Cube, stone, r * rad + new Vector3(0f, s * (ring == 0 ? 0.13f : 0.08f), 0f),
                        new Vector3(s * 0.17f, s * (ring == 0 ? 0.26f : 0.16f), s * 0.09f));
                    b.transform.localRotation = Quaternion.LookRotation(r, Vector3.up);
                    if (ring == 0 && i % 2 == 0)
                    {
                        GameObject arch = Piece(t, Cube, dark, r * (rad + s * 0.046f) + new Vector3(0f, s * 0.08f, 0f), new Vector3(s * 0.06f, s * 0.1f, s * 0.01f));
                        arch.transform.localRotation = Quaternion.LookRotation(r, Vector3.up);
                    }
                }
            Piece(t, Cylinder, civMat, new Vector3(0f, s * 0.045f, 0f), new Vector3(s * 0.2f, s * 0.01f, s * 0.2f));
        }

        if (city.buildings.Contains(BuildingKind.Cathedral))
        {
            Transform t = HappyAnchor(root, 2, size, "Catedral");
            Material stone = M(0.7f, 0.68f, 0.64f), roof = M(0.3f, 0.32f, 0.4f), glass = FutGlow(new Color(1f, 0.7f, 0.3f), 1.4f);
            Piece(t, Cube, stone, new Vector3(0f, s * 0.15f, -s * 0.05f), new Vector3(s * 0.3f, s * 0.3f, s * 0.6f));
            GameObject r1 = Piece(t, Cube, roof, new Vector3(-s * 0.08f, s * 0.35f, -s * 0.05f), new Vector3(s * 0.2f, s * 0.02f, s * 0.62f));
            r1.transform.localRotation = Quaternion.Euler(0f, 0f, 35f);
            GameObject r2 = Piece(t, Cube, roof, new Vector3(s * 0.08f, s * 0.35f, -s * 0.05f), new Vector3(s * 0.2f, s * 0.02f, s * 0.62f));
            r2.transform.localRotation = Quaternion.Euler(0f, 0f, -35f);
            for (int side = -1; side <= 1; side += 2)
            {
                Piece(t, Cylinder, stone, new Vector3(side * s * 0.15f, s * 0.28f, s * 0.26f), new Vector3(s * 0.1f, s * 0.28f, s * 0.1f));
                Piece(t, Sphere, roof, new Vector3(side * s * 0.15f, s * 0.6f, s * 0.26f), new Vector3(s * 0.11f, s * 0.2f, s * 0.11f));
            }
            Piece(t, Sphere, glass, new Vector3(0f, s * 0.2f, s * 0.26f), new Vector3(s * 0.1f, s * 0.1f, s * 0.02f));   // rosetón
            Piece(t, Cube, civMat, new Vector3(0f, s * 0.43f, s * 0.04f), new Vector3(s * 0.02f, s * 0.12f, s * 0.02f));
            Piece(t, Cube, civMat, new Vector3(0f, s * 0.45f, s * 0.04f), new Vector3(s * 0.08f, s * 0.02f, s * 0.02f));
        }

        if (city.buildings.Contains(BuildingKind.Theater))
        {
            Transform t = HappyAnchor(root, 3, size, "Teatro");
            Material wall = M(0.92f, 0.88f, 0.8f), red = M(0.65f, 0.12f, 0.15f), marquee = FutGlow(new Color(1f, 0.85f, 0.4f), 1.8f);
            Piece(t, Cube, wall, new Vector3(0f, s * 0.15f, 0f), new Vector3(s * 0.5f, s * 0.3f, s * 0.4f));
            Piece(t, Sphere, civMat, new Vector3(0f, s * 0.33f, -s * 0.05f), new Vector3(s * 0.34f, s * 0.2f, s * 0.34f));
            Columns(t, wall, -s * 0.2f, s * 0.2f, s * 0.15f, s * 0.22f, 4, s * 0.04f, s * 0.15f);
            Piece(t, Cube, marquee, new Vector3(0f, s * 0.3f, s * 0.23f), new Vector3(s * 0.34f, s * 0.04f, s * 0.02f));
            for (int side = -1; side <= 1; side += 2)
                Piece(t, Cube, red, new Vector3(side * s * 0.12f, s * 0.12f, s * 0.205f), new Vector3(s * 0.06f, s * 0.18f, s * 0.01f));
        }

        if (city.buildings.Contains(BuildingKind.Hospital))
        {
            Transform t = HappyAnchor(root, 4, size, "Hospital");
            Material wall = M(0.95f, 0.95f, 0.97f), red = M(0.85f, 0.15f, 0.15f), win = FutGlow(new Color(0.6f, 0.85f, 1f), 1.2f);
            Piece(t, Cube, wall, new Vector3(0f, s * 0.18f, 0f), new Vector3(s * 0.5f, s * 0.36f, s * 0.34f));
            Piece(t, Cube, red, new Vector3(0f, s * 0.27f, s * 0.175f), new Vector3(s * 0.16f, s * 0.04f, s * 0.01f));
            Piece(t, Cube, red, new Vector3(0f, s * 0.27f, s * 0.175f), new Vector3(s * 0.04f, s * 0.16f, s * 0.01f));
            for (int i = 0; i < 4; i++)
                for (int row = 0; row < 2; row++)
                    Piece(t, Cube, win, new Vector3((i - 1.5f) * s * 0.11f, s * (0.1f + row * 0.1f), s * 0.175f), new Vector3(s * 0.05f, s * 0.05f, s * 0.01f));
            Piece(t, Cylinder, civMat, new Vector3(0f, s * 0.365f, 0f), new Vector3(s * 0.22f, s * 0.006f, s * 0.22f));   // helipuerto
            Piece(t, Cube, wall, new Vector3(0f, s * 0.372f, 0f), new Vector3(s * 0.1f, s * 0.004f, s * 0.02f));
        }

        if (city.buildings.Contains(BuildingKind.Stadium))
        {
            Transform t = HappyAnchor(root, 5, size, "Estadio");
            Material grass = M(0.25f, 0.6f, 0.28f), stand = M(0.8f, 0.8f, 0.82f), lamp = FutGlow(new Color(1f, 1f, 0.85f), 2f), dark = M(0.2f, 0.2f, 0.24f);
            Piece(t, Cylinder, grass, new Vector3(0f, s * 0.02f, 0f), new Vector3(s * 0.6f, s * 0.02f, s * 0.42f));
            for (int i = 0; i < 20; i++)
            {
                float a = i / 20f * Mathf.PI * 2f;
                Vector3 r = new Vector3(Mathf.Cos(a) * 0.34f, 0f, Mathf.Sin(a) * 0.24f);
                GameObject b = Piece(t, Cube, i % 5 == 0 ? civMat : stand, r * s + new Vector3(0f, s * 0.09f, 0f), new Vector3(s * 0.13f, s * 0.16f, s * 0.07f));
                b.transform.localRotation = Quaternion.LookRotation(new Vector3(Mathf.Cos(a) * 0.24f, 0f, Mathf.Sin(a) * 0.34f), Vector3.up);
            }
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                {
                    Piece(t, Cylinder, dark, new Vector3(sx * s * 0.32f, s * 0.2f, sz * s * 0.22f), new Vector3(s * 0.016f, s * 0.2f, s * 0.016f));
                    Piece(t, Cube, lamp, new Vector3(sx * s * 0.32f, s * 0.41f, sz * s * 0.22f), new Vector3(s * 0.07f, s * 0.04f, s * 0.07f));
                }
        }

        if (city.buildings.Contains(BuildingKind.Park))
        {
            Transform t = HappyAnchor(root, 6, size, "Parque");
            Material grass = M(0.3f, 0.65f, 0.3f), water = M(0.25f, 0.5f, 0.8f), trunk = M(0.4f, 0.28f, 0.15f), leaf = M(0.18f, 0.5f, 0.2f), path = M(0.85f, 0.8f, 0.65f);
            Piece(t, Cylinder, grass, new Vector3(0f, s * 0.015f, 0f), new Vector3(s * 0.7f, s * 0.015f, s * 0.7f));
            Piece(t, Cylinder, water, new Vector3(s * 0.1f, s * 0.03f, -s * 0.06f), new Vector3(s * 0.22f, s * 0.01f, s * 0.16f));
            Piece(t, Cube, path, new Vector3(0f, s * 0.032f, s * 0.2f), new Vector3(s * 0.08f, s * 0.004f, s * 0.3f));
            Vector3[] trees = { new Vector3(-0.22f, 0f, -0.15f), new Vector3(-0.15f, 0f, 0.1f), new Vector3(0.26f, 0f, 0.12f), new Vector3(0.2f, 0f, -0.24f), new Vector3(-0.28f, 0f, 0.22f), new Vector3(0.02f, 0f, -0.27f) };
            for (int i = 0; i < trees.Length; i++)
            {
                Vector3 p = trees[i] * s;
                Piece(t, Cylinder, trunk, p + new Vector3(0f, s * 0.07f, 0f), new Vector3(s * 0.025f, s * 0.07f, s * 0.025f));
                Piece(t, Sphere, leaf, p + new Vector3(0f, s * 0.18f, 0f), Vector3.one * s * (0.13f + (i % 3) * 0.02f));
            }
            Piece(t, Cube, civMat, new Vector3(-s * 0.05f, s * 0.045f, s * 0.12f), new Vector3(s * 0.1f, s * 0.03f, s * 0.03f));   // banco
        }

        if (city.buildings.Contains(BuildingKind.Mall))
        {
            Transform t = HappyAnchor(root, 7, size, "Centro comercial");
            Material glass = M(0.2f, 0.32f, 0.5f), wall = M(0.85f, 0.85f, 0.88f), sign = FutGlow(new Color(1f, 0.3f, 0.8f), 2f), asphalt = M(0.2f, 0.2f, 0.22f);
            Piece(t, Cube, wall, new Vector3(0f, s * 0.03f, 0f), new Vector3(s * 0.74f, s * 0.06f, s * 0.5f));
            Piece(t, Cube, glass, new Vector3(0f, s * 0.14f, -s * 0.04f), new Vector3(s * 0.68f, s * 0.18f, s * 0.34f));
            Piece(t, Cube, civMat, new Vector3(0f, s * 0.245f, -s * 0.04f), new Vector3(s * 0.7f, s * 0.03f, s * 0.36f));
            Piece(t, Cube, sign, new Vector3(0f, s * 0.2f, s * 0.14f), new Vector3(s * 0.4f, s * 0.05f, s * 0.01f));
            Piece(t, Cube, asphalt, new Vector3(0f, s * 0.065f, s * 0.2f), new Vector3(s * 0.7f, s * 0.006f, s * 0.12f));
            for (int i = 0; i < 6; i++)
                Piece(t, Cube, i % 2 == 0 ? civMat : wall, new Vector3((i - 2.5f) * s * 0.11f, s * 0.085f, s * 0.2f), new Vector3(s * 0.07f, s * 0.04f, s * 0.04f));
        }

        if (city.buildings.Contains(BuildingKind.Cinema))
        {
            Transform t = HappyAnchor(root, 8, size, "Cine");
            Material wall = M(0.18f, 0.16f, 0.22f), screen = FutGlow(new Color(0.4f, 0.9f, 1f), 1.8f), beam = FutGlow(new Color(1f, 0.95f, 0.6f), 1.5f);
            Piece(t, Cube, wall, new Vector3(0f, s * 0.14f, 0f), new Vector3(s * 0.42f, s * 0.28f, s * 0.36f));
            Piece(t, Cube, civMat, new Vector3(0f, s * 0.295f, 0f), new Vector3(s * 0.44f, s * 0.03f, s * 0.38f));
            Piece(t, Cube, screen, new Vector3(0f, s * 0.18f, s * 0.185f), new Vector3(s * 0.32f, s * 0.14f, s * 0.01f));
            for (int side = -1; side <= 1; side += 2)
            {
                GameObject b = Piece(t, Cylinder, beam, new Vector3(side * s * 0.2f, s * 0.5f, s * 0.2f), new Vector3(s * 0.014f, s * 0.26f, s * 0.014f));
                b.transform.localRotation = Quaternion.Euler(0f, 0f, side * 14f);
            }
        }

        if (city.buildings.Contains(BuildingKind.VRPark))
        {
            Transform t = HappyAnchor(root, 9, size, "Parque de realidad virtual");
            Material pad = M(0.15f, 0.16f, 0.22f), dome = M(0.6f, 0.82f, 0.95f), cyan = FutGlow(new Color(0.3f, 0.95f, 1f), 2f), mag = FutGlow(new Color(1f, 0.3f, 0.9f), 2f);
            Piece(t, Cylinder, pad, new Vector3(0f, s * 0.02f, 0f), new Vector3(s * 0.72f, s * 0.02f, s * 0.72f));
            Piece(t, Sphere, dome, new Vector3(0f, s * 0.12f, 0f), new Vector3(s * 0.52f, s * 0.36f, s * 0.52f));
            Piece(t, Sphere, mag, new Vector3(0f, s * 0.2f, 0f), Vector3.one * s * 0.14f);
            for (int ring = 0; ring < 3; ring++)
                for (int i = 0; i < 14; i++)
                {
                    float a = i / 14f * Mathf.PI * 2f;
                    Piece(t, Sphere, ring % 2 == 0 ? cyan : mag, new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * s * 0.3f + new Vector3(0f, s * (0.06f + ring * 0.1f), 0f), Vector3.one * s * 0.03f);
                }
            Piece(t, Cylinder, civMat, new Vector3(0f, s * 0.04f, 0f), new Vector3(s * 0.74f, s * 0.008f, s * 0.74f));
        }

        if (city.buildings.Contains(BuildingKind.OrbitalGarden))
        {
            Transform t = HappyAnchor(root, 10, size, "Jardín orbital");
            Material pad = M(0.55f, 0.58f, 0.62f), cable = FutGlow(new Color(0.6f, 1f, 0.7f), 1.6f), dome = M(0.55f, 0.85f, 0.65f), green = FutGlow(new Color(0.3f, 1f, 0.45f), 1.6f);
            Piece(t, Cylinder, pad, new Vector3(0f, s * 0.02f, 0f), new Vector3(s * 0.5f, s * 0.02f, s * 0.5f));
            Piece(t, Cylinder, civMat, new Vector3(0f, s * 0.045f, 0f), new Vector3(s * 0.52f, s * 0.008f, s * 0.52f));
            Piece(t, Cylinder, cable, new Vector3(0f, s * 0.6f, 0f), new Vector3(s * 0.015f, s * 0.6f, s * 0.015f));
            Vector3 c = new Vector3(0f, s * 1.25f, 0f);
            for (int i = 0; i < 20; i++)
            {
                float a = i / 20f * Mathf.PI * 2f;
                Piece(t, Sphere, green, c + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * s * 0.32f, Vector3.one * s * 0.04f);
            }
            for (int i = 0; i < 4; i++)
            {
                float a = i / 4f * Mathf.PI * 2f;
                Piece(t, Sphere, dome, c + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * s * 0.32f, new Vector3(s * 0.14f, s * 0.1f, s * 0.14f));
            }
            Piece(t, Sphere, dome, c, new Vector3(s * 0.2f, s * 0.14f, s * 0.2f));
        }
    }

    // ---------------------------------------------------------------- Maravillas

    // Se llama desde BuildCity: dibuja las 12 maravillas nuevas que tenga la ciudad, cada una en una casilla libre de su territorio
    static void BuildExtraWonders(City city, Transform root, WorldGrid grid, Transform earth, float size, Material civMat)
    {
        BuildingKind[] kinds =
        {
            BuildingKind.Pyramids, BuildingKind.LighthouseAlex, BuildingKind.GreatWall, BuildingKind.Colosseum,
            BuildingKind.SistineChapel, BuildingKind.MarcoPolo, BuildingKind.EiffelTower, BuildingKind.StatueOfLiberty,
            BuildingKind.Hollywood, BuildingKind.ApolloProgram, BuildingKind.UnitedNations, BuildingKind.Biosphere
        };

        for (int i = 0; i < kinds.Length; i++)
        {
            if (!city.buildings.Contains(kinds[i])) continue;

            // Si no hay celdilla libre, se dibuja fuera del disco de la ciudad (una posición distinta para cada maravilla)
            float a = i / 12f * Mathf.PI * 2f + 0.5f;
            Vector3 at = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * size * 2.9f;
            Transform site = WonderSite(city, kinds[i], root, grid, earth, ref at);
            ExtraWonder(kinds[i], site, at, size, civMat);
        }
    }

    static void ExtraWonder(BuildingKind kind, Transform t, Vector3 at, float s, Material civ)
    {
        switch (kind)
        {
            case BuildingKind.Pyramids:
            {
                Material sand = M(0.85f, 0.72f, 0.45f), sand2 = M(0.78f, 0.64f, 0.38f);
                for (int i = 0; i < 6; i++)
                {
                    float w = s * (0.74f - i * 0.12f);
                    Piece(t, Cube, i % 2 == 0 ? sand : sand2, at + new Vector3(0f, s * (0.04f + i * 0.07f), 0f), new Vector3(w, s * 0.07f, w));
                }
                Piece(t, Cube, civ, at + new Vector3(0f, s * 0.46f, 0f), new Vector3(s * 0.08f, s * 0.07f, s * 0.08f));
                break;
            }
            case BuildingKind.LighthouseAlex:
            {
                Material stone = M(0.9f, 0.88f, 0.82f), glow = FutGlow(new Color(1f, 0.9f, 0.45f), 2.4f);
                Piece(t, Cube, stone, at + new Vector3(0f, s * 0.1f, 0f), new Vector3(s * 0.34f, s * 0.2f, s * 0.34f));
                Piece(t, Cylinder, stone, at + new Vector3(0f, s * 0.36f, 0f), new Vector3(s * 0.22f, s * 0.22f, s * 0.22f));
                Piece(t, Cylinder, civ, at + new Vector3(0f, s * 0.5f, 0f), new Vector3(s * 0.225f, s * 0.02f, s * 0.225f));
                Piece(t, Cylinder, stone, at + new Vector3(0f, s * 0.68f, 0f), new Vector3(s * 0.14f, s * 0.14f, s * 0.14f));
                Piece(t, Sphere, glow, at + new Vector3(0f, s * 0.88f, 0f), Vector3.one * s * 0.14f);
                break;
            }
            case BuildingKind.GreatWall:
            {
                Material stone = M(0.62f, 0.58f, 0.52f);
                for (int i = 0; i < 9; i++)
                {
                    float x = (i - 4f) * s * 0.12f, z = Mathf.Sin(i * 0.8f) * s * 0.12f;
                    GameObject b = Piece(t, Cube, stone, at + new Vector3(x, s * 0.09f, z), new Vector3(s * 0.15f, s * 0.18f, s * 0.07f));
                    b.transform.localRotation = Quaternion.Euler(0f, -Mathf.Cos(i * 0.8f) * 25f, 0f);
                    if (i % 3 == 0)
                    {
                        Piece(t, Cube, stone, at + new Vector3(x, s * 0.22f, z), new Vector3(s * 0.1f, s * 0.12f, s * 0.1f));
                        Piece(t, Cube, civ, at + new Vector3(x, s * 0.29f, z), new Vector3(s * 0.11f, s * 0.02f, s * 0.11f));
                    }
                }
                break;
            }
            case BuildingKind.Colosseum:
            {
                Material stone = M(0.82f, 0.74f, 0.58f), sand = M(0.86f, 0.78f, 0.55f), dark = M(0.2f, 0.17f, 0.14f);
                Piece(t, Cylinder, sand, at + new Vector3(0f, s * 0.02f, 0f), new Vector3(s * 0.62f, s * 0.02f, s * 0.62f));
                for (int tier = 0; tier < 3; tier++)
                    for (int i = 0; i < 18; i++)
                    {
                        float a = i / 18f * Mathf.PI * 2f;
                        Vector3 r = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                        float rad = s * (0.47f - tier * 0.015f);
                        GameObject b = Piece(t, Cube, stone, at + r * rad + new Vector3(0f, s * (0.08f + tier * 0.15f), 0f), new Vector3(s * 0.17f, s * 0.15f, s * 0.07f));
                        b.transform.localRotation = Quaternion.LookRotation(r, Vector3.up);
                        GameObject arch = Piece(t, Cube, dark, at + r * (rad + s * 0.037f) + new Vector3(0f, s * (0.07f + tier * 0.15f), 0f), new Vector3(s * 0.06f, s * 0.09f, s * 0.01f));
                        arch.transform.localRotation = Quaternion.LookRotation(r, Vector3.up);
                    }
                Piece(t, Cylinder, civ, at + new Vector3(0f, s * 0.045f, 0f), new Vector3(s * 0.22f, s * 0.01f, s * 0.22f));
                break;
            }
            case BuildingKind.SistineChapel:
            {
                Material wall = M(0.9f, 0.82f, 0.7f), dark = M(0.25f, 0.2f, 0.18f), dome = M(0.7f, 0.5f, 0.4f);
                Piece(t, Cube, wall, at + new Vector3(0f, s * 0.14f, 0f), new Vector3(s * 0.5f, s * 0.28f, s * 0.34f));
                for (int side = -1; side <= 1; side += 2)
                {
                    GameObject r = Piece(t, Cube, civ, at + new Vector3(0f, s * 0.34f, side * s * 0.09f), new Vector3(s * 0.52f, s * 0.02f, s * 0.2f));
                    r.transform.localRotation = Quaternion.Euler(-side * 28f, 0f, 0f);
                }
                Piece(t, Sphere, dome, at + new Vector3(0f, s * 0.36f, 0f), new Vector3(s * 0.2f, s * 0.14f, s * 0.2f));
                for (int i = 0; i < 4; i++)
                    Piece(t, Cube, dark, at + new Vector3((i - 1.5f) * s * 0.11f, s * 0.15f, s * 0.175f), new Vector3(s * 0.04f, s * 0.14f, s * 0.01f));
                break;
            }
            case BuildingKind.MarcoPolo:
            {
                Material awning1 = M(0.8f, 0.2f, 0.18f), awning2 = M(0.95f, 0.8f, 0.3f), wood = M(0.45f, 0.3f, 0.15f), camel = M(0.7f, 0.55f, 0.35f), cloth = M(0.9f, 0.88f, 0.8f);
                for (int i = 0; i < 3; i++)
                {
                    float x = (i - 1) * s * 0.26f;
                    Piece(t, Cube, wood, at + new Vector3(x, s * 0.06f, 0f), new Vector3(s * 0.18f, s * 0.12f, s * 0.12f));
                    GameObject aw = Piece(t, Cube, i == 1 ? civ : (i == 0 ? awning1 : awning2), at + new Vector3(x, s * 0.2f, s * 0.04f), new Vector3(s * 0.22f, s * 0.015f, s * 0.2f));
                    aw.transform.localRotation = Quaternion.Euler(-15f, 0f, 0f);
                    Piece(t, Cube, cloth, at + new Vector3(x, s * 0.14f, -s * 0.05f), new Vector3(s * 0.16f, s * 0.1f, s * 0.01f));
                }
                for (int k = 0; k < 2; k++)
                {
                    Vector3 p = at + new Vector3(-s * 0.2f + k * s * 0.4f, 0f, s * 0.3f);
                    GameObject body = Piece(t, Capsule, camel, p + new Vector3(0f, s * 0.14f, 0f), new Vector3(s * 0.1f, s * 0.12f, s * 0.1f));
                    body.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                    Piece(t, Sphere, camel, p + new Vector3(0f, s * 0.2f, 0f), new Vector3(s * 0.07f, s * 0.07f, s * 0.07f));
                    for (int lx = -1; lx <= 1; lx += 2)
                        for (int lz = -1; lz <= 1; lz += 2)
                            Piece(t, Cylinder, camel, p + new Vector3(lx * s * 0.06f, s * 0.05f, lz * s * 0.03f), new Vector3(s * 0.02f, s * 0.05f, s * 0.02f));
                    Piece(t, Cylinder, camel, p + new Vector3(s * 0.12f, s * 0.2f, 0f), new Vector3(s * 0.025f, s * 0.07f, s * 0.025f));
                    Piece(t, Sphere, civ, p + new Vector3(s * 0.12f, s * 0.29f, 0f), Vector3.one * s * 0.045f);
                }
                break;
            }
            case BuildingKind.EiffelTower:
            {
                Material iron = M(0.35f, 0.3f, 0.28f), glow = FutGlow(new Color(1f, 0.8f, 0.4f), 1.6f);
                for (int sx = -1; sx <= 1; sx += 2)
                    for (int sz = -1; sz <= 1; sz += 2)
                    {
                        Vector3 b0 = at + new Vector3(sx * s * 0.22f, 0f, sz * s * 0.22f);
                        Vector3 b1 = at + new Vector3(sx * s * 0.1f, s * 0.35f, sz * s * 0.1f);
                        Vector3 b2 = at + new Vector3(sx * s * 0.03f, s * 0.95f, sz * s * 0.03f);
                        ModTube(t, iron, b0, b1, s * 0.03f);
                        ModTube(t, iron, b1, b2, s * 0.025f);
                    }
                Piece(t, Cube, iron, at + new Vector3(0f, s * 0.3f, 0f), new Vector3(s * 0.3f, s * 0.025f, s * 0.3f));
                Piece(t, Cube, iron, at + new Vector3(0f, s * 0.6f, 0f), new Vector3(s * 0.18f, s * 0.02f, s * 0.18f));
                Piece(t, Cube, civ, at + new Vector3(0f, s * 0.31f, 0f), new Vector3(s * 0.31f, s * 0.01f, s * 0.31f));
                Piece(t, Cylinder, iron, at + new Vector3(0f, s * 1.08f, 0f), new Vector3(s * 0.012f, s * 0.14f, s * 0.012f));
                Piece(t, Sphere, glow, at + new Vector3(0f, s * 1.24f, 0f), Vector3.one * s * 0.05f);
                break;
            }
            case BuildingKind.StatueOfLiberty:
            {
                Material stone = M(0.7f, 0.7f, 0.68f), copper = M(0.35f, 0.68f, 0.6f), glow = FutGlow(new Color(1f, 0.85f, 0.3f), 2.2f);
                Piece(t, Cube, stone, at + new Vector3(0f, s * 0.05f, 0f), new Vector3(s * 0.4f, s * 0.1f, s * 0.4f));
                Piece(t, Cube, stone, at + new Vector3(0f, s * 0.18f, 0f), new Vector3(s * 0.26f, s * 0.16f, s * 0.26f));
                Piece(t, Capsule, copper, at + new Vector3(0f, s * 0.46f, 0f), new Vector3(s * 0.16f, s * 0.2f, s * 0.12f));
                Piece(t, Sphere, copper, at + new Vector3(0f, s * 0.7f, 0f), Vector3.one * s * 0.1f);
                for (int i = 0; i < 7; i++)
                {
                    float a = (i - 3f) * 0.38f;
                    GameObject spike = Piece(t, Cube, copper, at + new Vector3(Mathf.Sin(a) * s * 0.07f, s * 0.78f, Mathf.Cos(a) * s * 0.01f), new Vector3(s * 0.015f, s * 0.07f, s * 0.015f));
                    spike.transform.localRotation = Quaternion.Euler(0f, 0f, -a * 55f);
                }
                GameObject arm = Piece(t, Cylinder, copper, at + new Vector3(s * 0.12f, s * 0.78f, 0f), new Vector3(s * 0.04f, s * 0.14f, s * 0.04f));
                arm.transform.localRotation = Quaternion.Euler(0f, 0f, -12f);
                Piece(t, Sphere, glow, at + new Vector3(s * 0.15f, s * 0.97f, 0f), new Vector3(s * 0.07f, s * 0.09f, s * 0.07f));
                Piece(t, Cube, civ, at + new Vector3(-s * 0.1f, s * 0.46f, s * 0.07f), new Vector3(s * 0.08f, s * 0.11f, s * 0.02f));
                break;
            }
            case BuildingKind.Hollywood:
            {
                Material white = M(0.96f, 0.96f, 0.96f), post = M(0.4f, 0.4f, 0.42f), beam = FutGlow(new Color(1f, 0.95f, 0.7f), 1.6f), gold = FutGlow(new Color(1f, 0.8f, 0.25f), 1.8f), pave = M(0.3f, 0.3f, 0.34f);
                Piece(t, Cube, pave, at + new Vector3(0f, s * 0.02f, 0f), new Vector3(s * 0.9f, s * 0.04f, s * 0.5f));
                for (int i = 0; i < 9; i++)
                {
                    float x = (i - 4f) * s * 0.095f;
                    Piece(t, Cylinder, post, at + new Vector3(x, s * 0.12f, 0f), new Vector3(s * 0.012f, s * 0.1f, s * 0.012f));
                    Piece(t, Cube, i % 3 == 1 ? civ : white, at + new Vector3(x, s * 0.3f, 0f), new Vector3(s * 0.07f, s * 0.14f, s * 0.02f));
                }
                for (int side = -1; side <= 1; side += 2)
                {
                    GameObject b = Piece(t, Cylinder, beam, at + new Vector3(side * s * 0.36f, s * 0.42f, s * 0.12f), new Vector3(s * 0.014f, s * 0.38f, s * 0.014f));
                    b.transform.localRotation = Quaternion.Euler(0f, 0f, -side * 28f);
                }
                Piece(t, Sphere, gold, at + new Vector3(0f, s * 0.05f, s * 0.18f), new Vector3(s * 0.12f, s * 0.02f, s * 0.12f));
                break;
            }
            case BuildingKind.ApolloProgram:
            {
                Material white = M(0.95f, 0.95f, 0.95f), black = M(0.1f, 0.1f, 0.12f), iron = M(0.5f, 0.5f, 0.55f), fire = FutGlow(new Color(1f, 0.5f, 0.15f), 2.4f), pad = M(0.5f, 0.5f, 0.54f);
                Piece(t, Cylinder, pad, at + new Vector3(0f, s * 0.02f, 0f), new Vector3(s * 0.6f, s * 0.02f, s * 0.6f));
                Piece(t, Cylinder, white, at + new Vector3(0f, s * 0.55f, 0f), new Vector3(s * 0.12f, s * 0.5f, s * 0.12f));
                for (int i = 0; i < 3; i++)
                    Piece(t, Cylinder, black, at + new Vector3(0f, s * (0.25f + i * 0.35f), 0f), new Vector3(s * 0.124f, s * 0.03f, s * 0.124f));
                Piece(t, Cylinder, civ, at + new Vector3(0f, s * 0.42f, 0f), new Vector3(s * 0.126f, s * 0.015f, s * 0.126f));
                Piece(t, Sphere, white, at + new Vector3(0f, s * 1.12f, 0f), new Vector3(s * 0.12f, s * 0.2f, s * 0.12f));
                for (int i = 0; i < 4; i++)
                {
                    float a = i * Mathf.PI * 0.5f + Mathf.PI * 0.25f;
                    GameObject fin = Piece(t, Cube, black, at + new Vector3(Mathf.Cos(a) * s * 0.1f, s * 0.12f, Mathf.Sin(a) * s * 0.1f), new Vector3(s * 0.02f, s * 0.2f, s * 0.08f));
                    fin.transform.localRotation = Quaternion.Euler(0f, -a * Mathf.Rad2Deg, 0f);
                }
                for (int side = -1; side <= 1; side += 2)
                {
                    Piece(t, Cube, iron, at + new Vector3(side * s * 0.2f, s * 0.5f, 0f), new Vector3(s * 0.025f, s * 1f, s * 0.025f));
                    for (int k = 1; k < 5; k++)
                        Piece(t, Cube, iron, at + new Vector3(side * s * 0.15f, s * k * 0.2f, 0f), new Vector3(s * 0.1f, s * 0.015f, s * 0.015f));
                }
                Piece(t, Sphere, fire, at + new Vector3(0f, s * 0.05f, 0f), new Vector3(s * 0.2f, s * 0.06f, s * 0.2f));
                break;
            }
            case BuildingKind.UnitedNations:
            {
                Material glass = M(0.3f, 0.5f, 0.75f), wall = M(0.85f, 0.88f, 0.92f), pole = M(0.7f, 0.7f, 0.72f);
                Piece(t, Cube, glass, at + new Vector3(0f, s * 0.38f, -s * 0.1f), new Vector3(s * 0.14f, s * 0.76f, s * 0.42f));
                Piece(t, Cube, wall, at + new Vector3(s * 0.14f, s * 0.08f, -s * 0.1f), new Vector3(s * 0.36f, s * 0.16f, s * 0.3f));
                Piece(t, Cube, civ, at + new Vector3(0f, s * 0.77f, -s * 0.1f), new Vector3(s * 0.15f, s * 0.02f, s * 0.43f));
                Color[] flags = { new Color(0.9f, 0.2f, 0.2f), new Color(0.2f, 0.4f, 0.9f), new Color(0.2f, 0.7f, 0.3f), new Color(0.95f, 0.8f, 0.2f), new Color(0.9f, 0.5f, 0.1f), new Color(0.6f, 0.3f, 0.8f), new Color(0.95f, 0.95f, 0.95f), new Color(0.1f, 0.6f, 0.7f) };
                for (int i = 0; i < flags.Length; i++)
                {
                    float x = (i - 3.5f) * s * 0.08f;
                    Piece(t, Cylinder, pole, at + new Vector3(x, s * 0.14f, s * 0.2f), new Vector3(s * 0.01f, s * 0.14f, s * 0.01f));
                    Piece(t, Cube, LitMaterial(flags[i]), at + new Vector3(x + s * 0.03f, s * 0.25f, s * 0.2f), new Vector3(s * 0.05f, s * 0.035f, s * 0.008f));
                }
                break;
            }
            case BuildingKind.Biosphere:
            {
                Material shell = M(0.65f, 0.88f, 0.75f), frame = M(0.9f, 0.95f, 0.92f), green = M(0.2f, 0.55f, 0.25f), trunk = M(0.4f, 0.28f, 0.15f), pad = M(0.55f, 0.58f, 0.6f);
                Piece(t, Cylinder, pad, at + new Vector3(0f, s * 0.02f, 0f), new Vector3(s * 0.8f, s * 0.02f, s * 0.8f));
                Piece(t, Sphere, shell, at + new Vector3(0f, s * 0.25f, 0f), new Vector3(s * 0.72f, s * 0.6f, s * 0.72f));
                for (int ring = 0; ring < 3; ring++)
                    for (int i = 0; i < 12; i++)
                    {
                        float a = i / 12f * Mathf.PI * 2f;
                        float lat = (ring - 0.5f) * 0.55f;
                        Vector3 p = new Vector3(Mathf.Cos(a) * Mathf.Cos(lat), Mathf.Sin(lat), Mathf.Sin(a) * Mathf.Cos(lat)) * s * 0.35f;
                        Piece(t, Sphere, frame, at + p + new Vector3(0f, s * 0.25f, 0f), Vector3.one * s * 0.028f);
                    }
                Vector3[] trees = { new Vector3(-0.1f, 0f, 0.05f), new Vector3(0.12f, 0f, -0.04f), new Vector3(0f, 0f, 0.14f), new Vector3(-0.04f, 0f, -0.14f) };
                foreach (Vector3 tr in trees)
                {
                    Piece(t, Cylinder, trunk, at + tr * s + new Vector3(0f, s * 0.08f, 0f), new Vector3(s * 0.02f, s * 0.07f, s * 0.02f));
                    Piece(t, Sphere, green, at + tr * s + new Vector3(0f, s * 0.2f, 0f), Vector3.one * s * 0.12f);
                }
                Piece(t, Cylinder, civ, at + new Vector3(0f, s * 0.045f, 0f), new Vector3(s * 0.82f, s * 0.008f, s * 0.82f));
                break;
            }
        }
    }
}
