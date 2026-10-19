using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Creación de los objetos visibles del juego: ciudades, unidades, marcadores y territorio.
/// Todo cuelga de la Tierra para que gire con ella.
/// </summary>
public static partial class CivViews
{
    static Mesh sphere, cube, cylinder, capsule;
    static Material litTemplate;

    // ---------------------------------------------------------------- Mallas y materiales

    static Mesh PrimitiveMesh(PrimitiveType type)
    {
        GameObject temp = GameObject.CreatePrimitive(type);
        Mesh mesh = temp.GetComponent<MeshFilter>().sharedMesh;
        Object.Destroy(temp);
        return mesh;
    }

    static Mesh Sphere => sphere != null ? sphere : (sphere = PrimitiveMesh(PrimitiveType.Sphere));
    static Mesh Cube => cube != null ? cube : (cube = PrimitiveMesh(PrimitiveType.Cube));
    static Mesh Cylinder => cylinder != null ? cylinder : (cylinder = PrimitiveMesh(PrimitiveType.Cylinder));
    static Mesh Capsule => capsule != null ? capsule : (capsule = PrimitiveMesh(PrimitiveType.Capsule));

    public static Material LitMaterial(Color color)
    {
        Shader s = Shader.Find("Standard");
        if (s == null) s = Shader.Find("Sprites/Default");

        Material m = new Material(s);
        m.color = color;
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.15f);
        return m;
    }

    public static Material FlatMaterial(Color color)
    {
        Material m = new Material(Shader.Find("Sprites/Default"));
        m.color = color;
        m.renderQueue = 3000;
        return m;
    }

    static GameObject Piece(Transform parent, Mesh mesh, Material material, Vector3 localPos, Vector3 scale, float yaw = 0f)
    {
        GameObject go = new GameObject("Pieza");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        go.transform.localScale = scale;

        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = material;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return go;
    }

    // Raíz orientada según la normal de la casilla (el "arriba" local apunta fuera del planeta)
    static GameObject Root(string name, Transform earth, Vector3 localPos, Vector3 dir)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(earth, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, dir);
        return go;
    }

    static GameObject Ring(Transform parent, float diameter)
    {
        GameObject ring = Piece(parent, Cylinder, FlatMaterial(new Color(1f, 0.95f, 0.3f, 0.9f)),
            new Vector3(0f, 0.02f * diameter, 0f), new Vector3(diameter, 0.01f * diameter, diameter));
        ring.name = "Selección";
        ring.SetActive(false);
        return ring;
    }

    // ---------------------------------------------------------------- Ciudades

    // Donde se dibuja una maravilla: su propia celdilla libre (centrada en ella) o, si no hubo sitio, junto a la ciudad en 'at'
    static Transform WonderSite(City city, BuildingKind kind, Transform cityRoot, WorldGrid grid, Transform earth, ref Vector3 at)
    {
        int cell;
        if (!city.wonderCells.TryGetValue(kind, out cell)) return cityRoot;

        GameObject site = new GameObject("Maravilla " + kind);
        site.transform.SetParent(cityRoot, false);
        site.transform.position = earth.TransformPoint(grid.pos[cell]);
        site.transform.rotation = earth.rotation * Quaternion.FromToRotation(Vector3.up, grid.dir[cell]);
        at = Vector3.zero;
        return site.transform;
    }

    public static void BuildCity(City city, WorldGrid grid, Transform earth)
    {
        if (city.view != null) Object.Destroy(city.view);

        float size = grid.spacing * 0.5f;
        Color civColor = city.owner.color;

        GameObject root = Root("Ciudad " + city.name, earth, grid.pos[city.cell], grid.dir[city.cell]);
        city.view = root;

        Material civMat = LitMaterial(civColor);
        Material hide = LitMaterial(new Color(0.63f, 0.47f, 0.3f));       // pieles curtidas
        Material hideDark = LitMaterial(new Color(0.42f, 0.3f, 0.19f));   // remiendos y borde
        Material bone = LitMaterial(new Color(0.86f, 0.82f, 0.7f));       // huesos y astas
        Material dark = LitMaterial(new Color(0.08f, 0.06f, 0.05f));      // interior

        // Tras el Cambio de era (Edad Media) las chozas de pieles se sustituyen por casas de piedra
        bool medieval = city.owner.Has(TechKind.EraChange);
        Material houseStone = null, houseStoneDark = null, houseRoof = null, houseWood = null;
        if (medieval)
        {
            houseStone = LitMaterial(new Color(0.62f, 0.6f, 0.56f));      // muros de piedra
            houseStoneDark = LitMaterial(new Color(0.46f, 0.44f, 0.41f)); // zócalo, esquinas y chimenea
            houseRoof = LitMaterial(new Color(0.55f, 0.24f, 0.15f));      // tejas de barro
            houseWood = LitMaterial(new Color(0.34f, 0.22f, 0.11f));      // puerta y marcos
        }

        // Tras el cuarto Cambio de era (Edad Futura) los rascacielos se sustituyen por torres de cristal con neones
        bool futuristic = city.owner.Has(TechKind.EraChange4);
        Material futGlass = null, futCivGlow = null, futRing = null;
        Material[] futNeons = null;
        if (futuristic)
        {
            futGlass = LitMaterial(new Color(0.07f, 0.1f, 0.2f));
            futGlass.SetFloat("_Glossiness", 0.85f);
            futNeons = new[]
            {
                FutGlow(new Color(0.2f, 0.95f, 1f), 2.2f),   // cian
                FutGlow(new Color(1f, 0.2f, 0.9f), 2.2f),    // magenta
                FutGlow(new Color(0.6f, 0.3f, 1f), 2.2f)     // violeta
            };
            futCivGlow = FutGlow(civColor, 2.2f);
            futRing = FutGlow(civColor, 1.6f);
        }

        // Tras el tercer Cambio de era (Edad Contemporánea) las casas se sustituyen por rascacielos
        bool contemporary = city.owner.Has(TechKind.EraChange3);
        Material towerGlass = null, towerGlassLight = null, towerConcrete = null, towerMetal = null;
        if (contemporary)
        {
            towerGlass = LitMaterial(new Color(0.16f, 0.28f, 0.42f));        // cristal azul oscuro
            towerGlassLight = LitMaterial(new Color(0.55f, 0.75f, 0.9f));    // franjas de ventanas
            towerConcrete = LitMaterial(new Color(0.72f, 0.73f, 0.76f));     // hormigón
            towerMetal = LitMaterial(new Color(0.3f, 0.31f, 0.34f));         // antenas y remates
            towerGlass.SetFloat("_Glossiness", 0.7f);
        }

        // Un barrio por cada celda ocupada (la central y una más por cada 10 de población)
        int cellCount = Mathf.Max(1, city.cells.Count);
        for (int k = 0; k < cellCount; k++)
        {
            Transform district = root.transform;
            if (k > 0)
            {
                int cell = city.cells[k];
                GameObject d = new GameObject("Barrio " + k);
                d.transform.SetParent(root.transform, false);
                d.transform.position = earth.TransformPoint(grid.pos[cell]);
                d.transform.rotation = earth.rotation * Quaternion.FromToRotation(Vector3.up, grid.dir[cell]);
                district = d.transform;
            }

            // Habitantes que corresponden a este barrio
            int pop = Mathf.Clamp(city.pop - k * CivGame.PopPerCell, 1, CivGame.PopPerCell);

            // Base con el color de la civilización
            Piece(district, Cylinder, civMat, new Vector3(0f, size * 0.05f, 0f), new Vector3(size * 1.7f, size * 0.05f, size * 1.7f));
            if (futuristic) Piece(district, Cylinder, futRing, new Vector3(0f, size * 0.062f, 0f), new Vector3(size * 1.74f, size * 0.012f, size * 1.74f)); // aro de neón

            // Edificios: más cuantos más habitantes
            System.Random rng = new System.Random(city.cell * 7 + 1 + k * 101);
            int buildings = Mathf.Clamp(2 + pop, 3, 10);
            for (int i = 0; i < buildings; i++)
            {
                // Chozas repartidas en espiral alrededor del estandarte, dejando libres las esquinas para los edificios
                float angle = i * 2.4f + (float)rng.NextDouble() * 0.4f;
                float dist = size * (0.3f + 0.1f * Mathf.Sqrt(i) + (float)rng.NextDouble() * 0.02f);
                float radius = size * (0.16f + (float)rng.NextDouble() * 0.05f);
                Vector3 p = new Vector3(Mathf.Cos(angle) * dist, 0f, Mathf.Sin(angle) * dist);

                if (futuristic)
                    BuildFuturisticTower(district, p, radius, futGlass, futNeons, futCivGlow, -angle * Mathf.Rad2Deg + 90f, rng);
                else if (contemporary)
                    BuildSkyscraper(district, p, radius, towerGlass, towerGlassLight, towerConcrete, towerMetal, civMat, -angle * Mathf.Rad2Deg + 90f, rng);
                else if (medieval)
                    BuildStoneHouse(district, p, radius, houseStone, houseStoneDark, houseRoof, houseWood, dark, -angle * Mathf.Rad2Deg + 90f, rng);
                else
                    BuildHut(district, p, radius, hide, hideDark, bone, dark, -angle * Mathf.Rad2Deg + 90f, rng);
            }
        }

        // Estandarte central con el color del jugador
        Piece(root.transform, Cube, civMat, new Vector3(0f, size * 0.95f, 0f), new Vector3(size * 0.08f, size * 1.9f, size * 0.08f));
        Piece(root.transform, Cube, civMat, new Vector3(size * 0.2f, size * 1.7f, 0f), new Vector3(size * 0.4f, size * 0.25f, size * 0.03f));

        if (city.owner.Has(TechKind.Fire))
        {
            // Hoguera: aro de piedras, troncos cruzados y llamas brillantes junto al estandarte
            Material stoneMat = LitMaterial(new Color(0.5f, 0.48f, 0.45f));
            Material logMat = LitMaterial(new Color(0.3f, 0.19f, 0.1f));
            Material flameOuter = LitMaterial(new Color(1f, 0.45f, 0.1f));
            Material flameInner = LitMaterial(new Color(1f, 0.85f, 0.3f));
            foreach (Material m in new[] { flameOuter, flameInner })
            {
                m.EnableKeyword("_EMISSION");
                if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", m.color * 1.5f);
            }

            Vector3 at = new Vector3(-size * 0.55f, 0f, size * 0.45f);
            for (int i = 0; i < 7; i++)
            {
                float a = i / 7f * Mathf.PI * 2f;
                Piece(root.transform, Sphere, stoneMat, at + new Vector3(Mathf.Cos(a), 0.03f, Mathf.Sin(a)) * size * 0.14f, Vector3.one * size * 0.07f);
            }
            GameObject log1 = Piece(root.transform, Cylinder, logMat, at + new Vector3(0f, size * 0.05f, 0f), new Vector3(size * 0.04f, size * 0.12f, size * 0.04f));
            log1.transform.localRotation = Quaternion.Euler(0f, 0f, 70f);
            GameObject log2 = Piece(root.transform, Cylinder, logMat, at + new Vector3(0f, size * 0.05f, 0f), new Vector3(size * 0.04f, size * 0.12f, size * 0.04f));
            log2.transform.localRotation = Quaternion.Euler(70f, 0f, 0f);
            Piece(root.transform, Sphere, flameOuter, at + new Vector3(0f, size * 0.16f, 0f), new Vector3(size * 0.14f, size * 0.26f, size * 0.14f));
            Piece(root.transform, Sphere, flameInner, at + new Vector3(0f, size * 0.14f, 0f), new Vector3(size * 0.08f, size * 0.16f, size * 0.08f));
        }

        if (city.buildings.Contains(BuildingKind.Granary))
        {
            // Granero: silo de madera sobre pilotes con tejado a dos aguas, puerta y sacos de grano
            Material woodMat = LitMaterial(new Color(0.6f, 0.42f, 0.22f));
            Material plankMat = LitMaterial(new Color(0.48f, 0.32f, 0.17f));
            Material roofMat = LitMaterial(new Color(0.72f, 0.5f, 0.2f));
            Material sackMat = LitMaterial(new Color(0.88f, 0.8f, 0.55f));
            Material grainMat = LitMaterial(new Color(0.95f, 0.8f, 0.25f));

            Vector3 at = new Vector3(size * 0.6f, 0f, size * 0.5f);

            // Pilotes
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                    Piece(root.transform, Cylinder, plankMat, at + new Vector3(sx * size * 0.17f, size * 0.06f, sz * size * 0.14f),
                        new Vector3(size * 0.05f, size * 0.06f, size * 0.05f));

            // Cuerpo
            Piece(root.transform, Cube, woodMat, at + new Vector3(0f, size * 0.25f, 0f), new Vector3(size * 0.44f, size * 0.26f, size * 0.36f));

            // Tablones verticales en la fachada
            for (int i = -2; i <= 2; i++)
                Piece(root.transform, Cube, plankMat, at + new Vector3(i * size * 0.085f, size * 0.25f, size * 0.181f),
                    new Vector3(size * 0.012f, size * 0.26f, size * 0.01f));

            // Tejado a dos aguas (dos planchas inclinadas) y remate
            GameObject roofL = Piece(root.transform, Cube, roofMat, at + new Vector3(-size * 0.12f, size * 0.46f, 0f), new Vector3(size * 0.3f, size * 0.03f, size * 0.42f));
            roofL.transform.localRotation = Quaternion.Euler(0f, 0f, 32f);
            GameObject roofR = Piece(root.transform, Cube, roofMat, at + new Vector3(size * 0.12f, size * 0.46f, 0f), new Vector3(size * 0.3f, size * 0.03f, size * 0.42f));
            roofR.transform.localRotation = Quaternion.Euler(0f, 0f, -32f);
            Piece(root.transform, Cube, plankMat, at + new Vector3(0f, size * 0.54f, 0f), new Vector3(size * 0.04f, size * 0.03f, size * 0.44f));

            // Puerta y escalera
            Piece(root.transform, Cube, plankMat, at + new Vector3(0f, size * 0.2f, size * 0.186f), new Vector3(size * 0.12f, size * 0.17f, size * 0.012f));
            GameObject ramp = Piece(root.transform, Cube, woodMat, at + new Vector3(0f, size * 0.07f, size * 0.26f), new Vector3(size * 0.12f, size * 0.015f, size * 0.17f));
            ramp.transform.localRotation = Quaternion.Euler(-28f, 0f, 0f);

            // Sacos de grano y montón de cereal junto al granero
            Piece(root.transform, Sphere, sackMat, at + new Vector3(-size * 0.3f, size * 0.05f, size * 0.12f), new Vector3(size * 0.11f, size * 0.12f, size * 0.09f));
            Piece(root.transform, Sphere, sackMat, at + new Vector3(-size * 0.3f, size * 0.05f, size * 0.26f), new Vector3(size * 0.11f, size * 0.12f, size * 0.09f));
            Piece(root.transform, Sphere, sackMat, at + new Vector3(-size * 0.3f, size * 0.15f, size * 0.19f), new Vector3(size * 0.1f, size * 0.11f, size * 0.09f));
            Piece(root.transform, Sphere, grainMat, at + new Vector3(size * 0.3f, size * 0.03f, size * 0.22f), new Vector3(size * 0.16f, size * 0.09f, size * 0.16f));
        }

        if (city.buildings.Contains(BuildingKind.Library))
        {
            // Biblioteca: edificio de piedra con escalinata, columnas, frontón, ventanas y pergaminos
            Material marble = LitMaterial(new Color(0.9f, 0.88f, 0.82f));
            Material stoneDark = LitMaterial(new Color(0.62f, 0.6f, 0.56f));
            Material roofTile = LitMaterial(Color.Lerp(civColor, new Color(0.45f, 0.2f, 0.15f), 0.5f));
            Material doorMat = LitMaterial(new Color(0.3f, 0.2f, 0.12f));
            Material glass = LitMaterial(new Color(0.45f, 0.65f, 0.85f));
            Material scroll = LitMaterial(new Color(0.94f, 0.88f, 0.7f));
            Material bookA = LitMaterial(new Color(0.6f, 0.15f, 0.15f));
            Material bookB = LitMaterial(new Color(0.15f, 0.35f, 0.6f));

            Vector3 at = new Vector3(-size * 0.6f, 0f, -size * 0.5f);

            // Escalinata y cuerpo
            Piece(root.transform, Cube, stoneDark, at + new Vector3(0f, size * 0.03f, 0f), new Vector3(size * 0.6f, size * 0.06f, size * 0.5f));
            Piece(root.transform, Cube, stoneDark, at + new Vector3(0f, size * 0.08f, size * 0.03f), new Vector3(size * 0.54f, size * 0.05f, size * 0.42f));
            Piece(root.transform, Cube, marble, at + new Vector3(0f, size * 0.25f, -size * 0.02f), new Vector3(size * 0.46f, size * 0.3f, size * 0.32f));

            // Cuatro columnas en la fachada
            for (int i = 0; i < 4; i++)
            {
                float x = (i - 1.5f) * size * 0.135f;
                Piece(root.transform, Cylinder, marble, at + new Vector3(x, size * 0.26f, size * 0.17f), new Vector3(size * 0.045f, size * 0.17f, size * 0.045f));
                Piece(root.transform, Cube, stoneDark, at + new Vector3(x, size * 0.44f, size * 0.17f), new Vector3(size * 0.07f, size * 0.025f, size * 0.07f));
            }

            // Arquitrabe, frontón triangular (dos planchas) y tejado
            Piece(root.transform, Cube, marble, at + new Vector3(0f, size * 0.47f, size * 0.12f), new Vector3(size * 0.56f, size * 0.04f, size * 0.22f));
            GameObject pedL = Piece(root.transform, Cube, roofTile, at + new Vector3(-size * 0.13f, size * 0.54f, size * 0.12f), new Vector3(size * 0.3f, size * 0.03f, size * 0.24f));
            pedL.transform.localRotation = Quaternion.Euler(0f, 0f, 20f);
            GameObject pedR = Piece(root.transform, Cube, roofTile, at + new Vector3(size * 0.13f, size * 0.54f, size * 0.12f), new Vector3(size * 0.3f, size * 0.03f, size * 0.24f));
            pedR.transform.localRotation = Quaternion.Euler(0f, 0f, -20f);
            Piece(root.transform, Cube, roofTile, at + new Vector3(0f, size * 0.47f, -size * 0.06f), new Vector3(size * 0.5f, size * 0.03f, size * 0.36f));

            // Puerta, ventanas a los lados y cúpula pequeña con pergamino
            Piece(root.transform, Cube, doorMat, at + new Vector3(0f, size * 0.2f, size * 0.145f), new Vector3(size * 0.09f, size * 0.17f, size * 0.012f));
            for (int s = -1; s <= 1; s += 2)
                Piece(root.transform, Cube, glass, at + new Vector3(s * size * 0.2f, size * 0.28f, size * 0.145f), new Vector3(size * 0.06f, size * 0.12f, size * 0.012f));
            Piece(root.transform, Sphere, roofTile, at + new Vector3(0f, size * 0.5f, -size * 0.08f), new Vector3(size * 0.2f, size * 0.12f, size * 0.2f));
            GameObject rolled = Piece(root.transform, Cylinder, scroll, at + new Vector3(0f, size * 0.6f, -size * 0.08f), new Vector3(size * 0.03f, size * 0.07f, size * 0.03f));
            rolled.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);

            // Libros apilados junto a la escalinata
            Piece(root.transform, Cube, bookA, at + new Vector3(size * 0.4f, size * 0.025f, size * 0.22f), new Vector3(size * 0.12f, size * 0.03f, size * 0.09f), 15f);
            Piece(root.transform, Cube, bookB, at + new Vector3(size * 0.4f, size * 0.055f, size * 0.22f), new Vector3(size * 0.11f, size * 0.03f, size * 0.08f), -10f);
            Piece(root.transform, Cube, scroll, at + new Vector3(size * 0.4f, size * 0.085f, size * 0.22f), new Vector3(size * 0.1f, size * 0.025f, size * 0.075f), 30f);
        }

        if (city.buildings.Contains(BuildingKind.Market))
        {
            // Mercado: dos puestos con toldo a rayas, mostradores con fruta, cajas, barril y un cartel con moneda
            Material poleMat = LitMaterial(new Color(0.4f, 0.27f, 0.14f));
            Material counterMat = LitMaterial(new Color(0.62f, 0.45f, 0.25f));
            Material awningA = LitMaterial(civColor);
            Material awningB = LitMaterial(new Color(0.95f, 0.92f, 0.85f));
            Material crateMat = LitMaterial(new Color(0.55f, 0.4f, 0.22f));
            Material appleMat = LitMaterial(new Color(0.85f, 0.2f, 0.15f));
            Material orangeMat = LitMaterial(new Color(0.95f, 0.6f, 0.1f));
            Material greenMat = LitMaterial(new Color(0.35f, 0.65f, 0.2f));
            Material coinMat = LitMaterial(new Color(0.95f, 0.8f, 0.3f));
            coinMat.SetFloat("_Glossiness", 0.6f);

            Vector3 at = new Vector3(size * 0.6f, 0f, -size * 0.5f);

            for (int stall = 0; stall < 2; stall++)
            {
                Vector3 s0 = at + new Vector3((stall - 0.5f) * size * 0.42f, 0f, 0f);

                // Cuatro postes, mostrador y mercancía
                for (int sx = -1; sx <= 1; sx += 2)
                    for (int sz = -1; sz <= 1; sz += 2)
                        Piece(root.transform, Cylinder, poleMat, s0 + new Vector3(sx * size * 0.16f, size * 0.17f, sz * size * 0.11f),
                            new Vector3(size * 0.022f, size * 0.17f, size * 0.022f));

                Piece(root.transform, Cube, counterMat, s0 + new Vector3(0f, size * 0.13f, size * 0.09f), new Vector3(size * 0.34f, size * 0.05f, size * 0.1f));
                Piece(root.transform, Cube, poleMat, s0 + new Vector3(0f, size * 0.065f, size * 0.09f), new Vector3(size * 0.32f, size * 0.12f, size * 0.08f));

                Material goods = stall == 0 ? appleMat : orangeMat;
                for (int i = -2; i <= 2; i++)
                    Piece(root.transform, Sphere, (i & 1) == 0 ? goods : greenMat,
                        s0 + new Vector3(i * size * 0.06f, size * 0.185f, size * 0.09f), Vector3.one * size * 0.05f);

                // Toldo inclinado a rayas alternas
                for (int i = 0; i < 4; i++)
                {
                    GameObject stripe = Piece(root.transform, Cube, (i & 1) == 0 ? awningA : awningB,
                        s0 + new Vector3((i - 1.5f) * size * 0.085f, size * 0.385f, size * 0.01f),
                        new Vector3(size * 0.085f, size * 0.015f, size * 0.3f));
                    stripe.transform.localRotation = Quaternion.Euler(-14f, 0f, 0f);
                }
            }

            // Cajas apiladas, barril y cartel con una moneda
            Piece(root.transform, Cube, crateMat, at + new Vector3(-size * 0.4f, size * 0.05f, -size * 0.05f), new Vector3(size * 0.12f, size * 0.1f, size * 0.12f), 20f);
            Piece(root.transform, Cube, crateMat, at + new Vector3(-size * 0.4f, size * 0.15f, -size * 0.05f), new Vector3(size * 0.09f, size * 0.09f, size * 0.09f), -15f);
            Piece(root.transform, Cylinder, poleMat, at + new Vector3(size * 0.4f, size * 0.07f, -size * 0.02f), new Vector3(size * 0.1f, size * 0.07f, size * 0.1f));

            Piece(root.transform, Cylinder, poleMat, at + new Vector3(0f, size * 0.3f, -size * 0.17f), new Vector3(size * 0.02f, size * 0.3f, size * 0.02f));
            GameObject coin = Piece(root.transform, Cylinder, coinMat, at + new Vector3(0f, size * 0.64f, -size * 0.17f), new Vector3(size * 0.14f, size * 0.012f, size * 0.14f));
            coin.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        }

        if (city.buildings.Contains(BuildingKind.Observatory))
        {
            // Observatorio: torre de piedra con cúpula blanca y una ranura oscura con un telescopio asomando
            Material obsStone = LitMaterial(new Color(0.62f, 0.6f, 0.56f));
            Material obsDome = LitMaterial(new Color(0.92f, 0.92f, 0.95f));
            Material obsDark = LitMaterial(new Color(0.08f, 0.08f, 0.12f));
            Material obsBrass = LitMaterial(new Color(0.75f, 0.58f, 0.25f));

            Vector3 at = new Vector3(-size * 0.95f, 0f, 0f);

            Piece(root.transform, Cylinder, obsStone, at + new Vector3(0f, size * 0.25f, 0f), new Vector3(size * 0.34f, size * 0.25f, size * 0.34f));
            Piece(root.transform, Sphere, obsDome, at + new Vector3(0f, size * 0.52f, 0f), new Vector3(size * 0.38f, size * 0.3f, size * 0.38f));
            Piece(root.transform, Cube, obsDark, at + new Vector3(0f, size * 0.56f, size * 0.15f), new Vector3(size * 0.08f, size * 0.22f, size * 0.08f));
            GameObject scope = Piece(root.transform, Cylinder, obsBrass, at + new Vector3(0f, size * 0.62f, size * 0.24f), new Vector3(size * 0.04f, size * 0.14f, size * 0.04f));
            scope.transform.localRotation = Quaternion.Euler(55f, 0f, 0f);
        }

        if (city.buildings.Contains(BuildingKind.GreatLibrary))
        {
            // Gran Biblioteca: templo de mármol con escalinata, cuatro columnas, frontón y el color de la civilización
            Material marble = LitMaterial(new Color(0.9f, 0.88f, 0.82f));
            Material marbleDark = LitMaterial(new Color(0.75f, 0.73f, 0.68f));

            Vector3 at = new Vector3(0f, 0f, -size * 1.5f); // si no hay celdilla libre, fuera del disco de la ciudad
            Transform site = WonderSite(city, BuildingKind.GreatLibrary, root.transform, grid, earth, ref at);

            Piece(site, Cube, marbleDark, at + new Vector3(0f, size * 0.03f, 0f), new Vector3(size * 0.8f, size * 0.06f, size * 0.5f));
            Piece(site, Cube, marble, at + new Vector3(0f, size * 0.08f, 0f), new Vector3(size * 0.7f, size * 0.04f, size * 0.42f));

            for (int i = -1; i <= 1; i += 2)
                for (int j = -1; j <= 1; j += 2)
                    Piece(site, Cylinder, marble, at + new Vector3(i * size * 0.25f, size * 0.26f, j * size * 0.13f),
                        new Vector3(size * 0.07f, size * 0.17f, size * 0.07f));

            Piece(site, Cube, marbleDark, at + new Vector3(0f, size * 0.46f, 0f), new Vector3(size * 0.7f, size * 0.06f, size * 0.42f));
            Piece(site, Cube, civMat, at + new Vector3(0f, size * 0.54f, 0f), new Vector3(size * 0.5f, size * 0.1f, size * 0.3f));
        }

        if (city.buildings.Contains(BuildingKind.AlexandriaLibrary))
        {
            // Gran Biblioteca de Alejandría: gran sala de mármol con columnata, estanterías de rollos y un faro dorado en el centro
            Material alMarble = LitMaterial(new Color(0.93f, 0.91f, 0.85f));
            Material alDark = LitMaterial(new Color(0.72f, 0.69f, 0.62f));
            Material alScroll = LitMaterial(new Color(0.85f, 0.72f, 0.45f));
            Material alGold = LitMaterial(new Color(0.95f, 0.78f, 0.2f));

            Vector3 at = new Vector3(-size * 1.4f, 0f, size * 1.2f); // si no hay celdilla libre, fuera del disco de la ciudad
            Transform site = WonderSite(city, BuildingKind.AlexandriaLibrary, root.transform, grid, earth, ref at);

            // Plataforma de dos escalones y sala central
            Piece(site, Cube, alDark, at + new Vector3(0f, size * 0.03f, 0f), new Vector3(size * 0.9f, size * 0.06f, size * 0.7f));
            Piece(site, Cube, alMarble, at + new Vector3(0f, size * 0.08f, 0f), new Vector3(size * 0.8f, size * 0.04f, size * 0.6f));
            Piece(site, Cube, alMarble, at + new Vector3(0f, size * 0.24f, 0f), new Vector3(size * 0.5f, size * 0.28f, size * 0.4f));
            Piece(site, Cube, alDark, at + new Vector3(0f, size * 0.4f, 0f), new Vector3(size * 0.58f, size * 0.05f, size * 0.48f));
            Piece(site, Cube, civMat, at + new Vector3(0f, size * 0.45f, 0f), new Vector3(size * 0.4f, size * 0.05f, size * 0.3f));

            // Columnata: seis columnas por cada lado largo
            for (int side = -1; side <= 1; side += 2)
                for (int i = 0; i < 6; i++)
                    Piece(site, Cylinder, alMarble, at + new Vector3((i - 2.5f) * size * 0.13f, size * 0.22f, side * size * 0.27f),
                        new Vector3(size * 0.05f, size * 0.14f, size * 0.05f));

            // Estantes de rollos en la fachada
            for (int row = 0; row < 3; row++)
                for (int col = -2; col <= 2; col++)
                    Piece(site, Cylinder, alScroll, at + new Vector3(col * size * 0.08f, size * (0.16f + row * 0.07f), size * 0.205f),
                        new Vector3(size * 0.035f, size * 0.03f, size * 0.035f));

            // Faro dorado sobre la sala
            Piece(site, Cylinder, alMarble, at + new Vector3(0f, size * 0.58f, 0f), new Vector3(size * 0.1f, size * 0.1f, size * 0.1f));
            Piece(site, Sphere, alGold, at + new Vector3(0f, size * 0.72f, 0f), new Vector3(size * 0.1f, size * 0.1f, size * 0.1f));
        }

        if (city.buildings.Contains(BuildingKind.Workshop))
        {
            // Taller: cobertizo de madera con tejado inclinado, chimenea humeante, yunque y montón de leña
            Material wsWood = LitMaterial(new Color(0.5f, 0.34f, 0.18f));
            Material wsRoof = LitMaterial(new Color(0.35f, 0.25f, 0.2f));
            Material wsStone = LitMaterial(new Color(0.5f, 0.48f, 0.45f));
            Material wsIron = LitMaterial(new Color(0.22f, 0.22f, 0.25f));
            Material wsEmber = LitMaterial(new Color(1f, 0.5f, 0.15f));

            Vector3 at = new Vector3(size * 0.9f, 0f, -size * 0.9f);

            Piece(root.transform, Cube, wsWood, at + new Vector3(0f, size * 0.12f, 0f), new Vector3(size * 0.4f, size * 0.24f, size * 0.32f));
            GameObject roof = Piece(root.transform, Cube, wsRoof, at + new Vector3(0f, size * 0.27f, 0f), new Vector3(size * 0.46f, size * 0.04f, size * 0.38f));
            roof.transform.localRotation = Quaternion.Euler(0f, 0f, 8f);
            Piece(root.transform, Cube, wsStone, at + new Vector3(size * 0.13f, size * 0.36f, size * 0.08f), new Vector3(size * 0.07f, size * 0.2f, size * 0.07f));
            Piece(root.transform, Cube, wsEmber, at + new Vector3(size * 0.13f, size * 0.47f, size * 0.08f), new Vector3(size * 0.05f, size * 0.025f, size * 0.05f));

            // Yunque y leña delante
            Piece(root.transform, Cube, wsIron, at + new Vector3(-size * 0.1f, size * 0.05f, -size * 0.26f), new Vector3(size * 0.12f, size * 0.06f, size * 0.06f));
            Piece(root.transform, Cube, wsIron, at + new Vector3(-size * 0.1f, size * 0.02f, -size * 0.26f), new Vector3(size * 0.06f, size * 0.04f, size * 0.04f));
            for (int i = 0; i < 3; i++)
                Piece(root.transform, Cylinder, wsWood, at + new Vector3(size * (0.1f + i * 0.045f), size * 0.025f, -size * 0.26f), new Vector3(size * 0.03f, size * 0.07f, size * 0.03f))
                    .transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        }

        if (city.buildings.Contains(BuildingKind.PublicBaths))
        {
            // Baños públicos: estanque rectangular de agua azul rodeado de borde de mármol, con columnas en dos lados
            Material pbMarble = LitMaterial(new Color(0.9f, 0.88f, 0.82f));
            Material pbWater = LitMaterial(new Color(0.35f, 0.65f, 0.85f));

            Vector3 at = new Vector3(0f, 0f, -size * 0.95f);

            Piece(root.transform, Cube, pbMarble, at + new Vector3(0f, size * 0.025f, 0f), new Vector3(size * 0.7f, size * 0.05f, size * 0.42f));
            Piece(root.transform, Cube, pbWater, at + new Vector3(0f, size * 0.055f, 0f), new Vector3(size * 0.56f, size * 0.02f, size * 0.28f));
            for (int i = -1; i <= 1; i++)
                for (int side = -1; side <= 1; side += 2)
                    Piece(root.transform, Cylinder, pbMarble, at + new Vector3(i * size * 0.27f, size * 0.14f, side * size * 0.19f), new Vector3(size * 0.035f, size * 0.09f, size * 0.035f));
            Piece(root.transform, Cube, pbMarble, at + new Vector3(0f, size * 0.24f, -size * 0.19f), new Vector3(size * 0.62f, size * 0.03f, size * 0.07f));
        }

        if (city.buildings.Contains(BuildingKind.FullingMill))
        {
            // Batán: casa de madera junto a un canal de agua con una gran rueda hidráulica de palas
            Material fmWood = LitMaterial(new Color(0.5f, 0.34f, 0.18f));
            Material fmDark = LitMaterial(new Color(0.34f, 0.22f, 0.11f));
            Material fmStone = LitMaterial(new Color(0.6f, 0.58f, 0.54f));
            Material fmRoof = LitMaterial(new Color(0.55f, 0.24f, 0.15f));
            Material fmWater = LitMaterial(new Color(0.35f, 0.65f, 0.85f));

            Vector3 at = new Vector3(0f, 0f, size * 0.95f);

            Piece(root.transform, Cube, fmStone, at + new Vector3(0f, size * 0.03f, 0f), new Vector3(size * 0.7f, size * 0.06f, size * 0.42f));
            Piece(root.transform, Cube, fmWood, at + new Vector3(-size * 0.1f, size * 0.17f, 0f), new Vector3(size * 0.34f, size * 0.22f, size * 0.3f));
            Piece(root.transform, Cube, fmRoof, at + new Vector3(-size * 0.1f, size * 0.3f, 0f), new Vector3(size * 0.4f, size * 0.05f, size * 0.36f));
            Piece(root.transform, Cube, fmWater, at + new Vector3(size * 0.2f, size * 0.07f, 0f), new Vector3(size * 0.2f, size * 0.02f, size * 0.38f));

            // Rueda: eje, aro de cilindro plano y ocho palas
            Vector3 hub = at + new Vector3(size * 0.2f, size * 0.2f, 0f);
            Piece(root.transform, Cylinder, fmDark, hub, new Vector3(size * 0.025f, size * 0.17f, size * 0.025f)).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            for (int i = 0; i < 8; i++)
            {
                float a = i / 8f * Mathf.PI * 2f;
                GameObject blade = Piece(root.transform, Cube, fmDark, hub + new Vector3(0f, Mathf.Sin(a), 0f) * size * 0.14f + new Vector3(Mathf.Cos(a), 0f, 0f) * size * 0.14f,
                    new Vector3(size * 0.03f, size * 0.07f, size * 0.26f));
                blade.transform.localRotation = Quaternion.Euler(0f, 0f, -a * Mathf.Rad2Deg);
            }
        }

        if (city.buildings.Contains(BuildingKind.University))
        {
            // Universidad: edificio gótico de piedra con salón central de tejado azul, dos torres con chapitel,
            // portón de madera, ventana redonda sobre la entrada y un pendón del color de la civilización
            Material unStone = LitMaterial(new Color(0.74f, 0.71f, 0.65f));
            Material unDark = LitMaterial(new Color(0.52f, 0.5f, 0.46f));
            Material unRoof = LitMaterial(new Color(0.25f, 0.33f, 0.52f));
            Material unWood = LitMaterial(new Color(0.3f, 0.19f, 0.1f));
            Material unGlass = LitMaterial(new Color(0.55f, 0.75f, 0.9f));

            Vector3 at = new Vector3(-size * 0.9f, 0f, -size * 0.9f);

            // Zócalo y salón central (fachada hacia -Z)
            Piece(root.transform, Cube, unDark, at + new Vector3(0f, size * 0.02f, 0f), new Vector3(size * 0.7f, size * 0.04f, size * 0.4f));
            Piece(root.transform, Cube, unStone, at + new Vector3(0f, size * 0.15f, 0f), new Vector3(size * 0.34f, size * 0.22f, size * 0.32f));
            Piece(root.transform, Cube, unRoof, at + new Vector3(0f, size * 0.29f, 0f), new Vector3(size * 0.38f, size * 0.05f, size * 0.36f));
            Piece(root.transform, Cube, unRoof, at + new Vector3(0f, size * 0.335f, 0f), new Vector3(size * 0.28f, size * 0.05f, size * 0.3f));
            Piece(root.transform, Cube, unRoof, at + new Vector3(0f, size * 0.38f, 0f), new Vector3(size * 0.18f, size * 0.05f, size * 0.24f));

            // Dos torres con chapitel escalonado
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 tower = at + new Vector3(side * size * 0.25f, 0f, 0f);
                Piece(root.transform, Cylinder, unStone, tower + new Vector3(0f, size * 0.22f, 0f), new Vector3(size * 0.12f, size * 0.22f, size * 0.12f));
                Piece(root.transform, Cylinder, unDark, tower + new Vector3(0f, size * 0.45f, 0f), new Vector3(size * 0.14f, size * 0.025f, size * 0.14f));
                Piece(root.transform, Cylinder, unRoof, tower + new Vector3(0f, size * 0.5f, 0f), new Vector3(size * 0.1f, size * 0.04f, size * 0.1f));
                Piece(root.transform, Cylinder, unRoof, tower + new Vector3(0f, size * 0.57f, 0f), new Vector3(size * 0.06f, size * 0.05f, size * 0.06f));
                Piece(root.transform, Cylinder, unRoof, tower + new Vector3(0f, size * 0.65f, 0f), new Vector3(size * 0.025f, size * 0.05f, size * 0.025f));
                Piece(root.transform, Cube, unGlass, tower + new Vector3(0f, size * 0.34f, -size * 0.115f), new Vector3(size * 0.03f, size * 0.08f, size * 0.012f));
            }

            // Portón con arco, ventana redonda y pendón
            Piece(root.transform, Cube, unWood, at + new Vector3(0f, size * 0.09f, -size * 0.165f), new Vector3(size * 0.1f, size * 0.14f, size * 0.012f));
            Piece(root.transform, Cylinder, unWood, at + new Vector3(0f, size * 0.165f, -size * 0.165f), new Vector3(size * 0.05f, size * 0.006f, size * 0.05f))
                .transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Piece(root.transform, Cylinder, unGlass, at + new Vector3(0f, size * 0.23f, -size * 0.165f), new Vector3(size * 0.06f, size * 0.006f, size * 0.06f))
                .transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Piece(root.transform, Cube, civMat, at + new Vector3(size * 0.1f, size * 0.15f, -size * 0.17f), new Vector3(size * 0.04f, size * 0.12f, size * 0.01f));
        }

        if (city.buildings.Contains(BuildingKind.Academy))
        {
            // Academia: edificio de ladrillo con dos alas bajas, cuerpo central con tejado a escalones, cúpula de bronce
            // con una bandera del color de la civilización, puerta de madera y ventanas
            Material acBrick = LitMaterial(new Color(0.66f, 0.42f, 0.32f));
            Material acStone = LitMaterial(new Color(0.82f, 0.8f, 0.74f));
            Material acRoof = LitMaterial(new Color(0.36f, 0.4f, 0.46f));
            Material acBronze = LitMaterial(new Color(0.55f, 0.62f, 0.5f));
            Material acWood = LitMaterial(new Color(0.3f, 0.19f, 0.1f));
            Material acWindow = LitMaterial(new Color(0.2f, 0.28f, 0.4f));

            Vector3 at = new Vector3(size * 0.9f, 0f, size * 0.9f);

            // Zócalo y alas laterales
            Piece(root.transform, Cube, acStone, at + new Vector3(0f, size * 0.02f, 0f), new Vector3(size * 0.6f, size * 0.04f, size * 0.36f));
            for (int side = -1; side <= 1; side += 2)
            {
                Piece(root.transform, Cube, acBrick, at + new Vector3(side * size * 0.19f, size * 0.12f, 0f), new Vector3(size * 0.2f, size * 0.16f, size * 0.28f));
                Piece(root.transform, Cube, acRoof, at + new Vector3(side * size * 0.19f, size * 0.22f, 0f), new Vector3(size * 0.23f, size * 0.04f, size * 0.31f));
            }

            // Cuerpo central, más alto, con tejado a escalones
            Piece(root.transform, Cube, acBrick, at + new Vector3(0f, size * 0.2f, 0f), new Vector3(size * 0.22f, size * 0.32f, size * 0.3f));
            Piece(root.transform, Cube, acStone, at + new Vector3(0f, size * 0.37f, 0f), new Vector3(size * 0.26f, size * 0.03f, size * 0.34f));
            Piece(root.transform, Cube, acRoof, at + new Vector3(0f, size * 0.405f, 0f), new Vector3(size * 0.2f, size * 0.04f, size * 0.26f));

            // Cúpula con farol y bandera
            Piece(root.transform, Sphere, acBronze, at + new Vector3(0f, size * 0.44f, 0f), new Vector3(size * 0.16f, size * 0.14f, size * 0.16f));
            Piece(root.transform, Cube, acStone, at + new Vector3(0f, size * 0.54f, 0f), new Vector3(size * 0.03f, size * 0.06f, size * 0.03f));
            Piece(root.transform, Cube, civMat, at + new Vector3(0f, size * 0.64f, 0f), new Vector3(size * 0.012f, size * 0.16f, size * 0.012f));
            Piece(root.transform, Cube, civMat, at + new Vector3(size * 0.05f, size * 0.69f, 0f), new Vector3(size * 0.1f, size * 0.06f, size * 0.01f));

            // Puerta (fachada hacia +Z) y ventanas en las alas y sobre la puerta
            Piece(root.transform, Cube, acWood, at + new Vector3(0f, size * 0.1f, size * 0.155f), new Vector3(size * 0.08f, size * 0.16f, size * 0.01f));
            for (int side = -1; side <= 1; side += 2)
            {
                Piece(root.transform, Cube, acWindow, at + new Vector3(side * size * 0.19f, size * 0.13f, size * 0.145f), new Vector3(size * 0.06f, size * 0.08f, size * 0.01f));
                Piece(root.transform, Cube, acWindow, at + new Vector3(side * size * 0.06f, size * 0.27f, size * 0.155f), new Vector3(size * 0.04f, size * 0.07f, size * 0.01f));
            }
        }

        if (city.buildings.Contains(BuildingKind.Bank))
        {
            // Banco: edificio de piedra clara con escalones, cuatro columnas en la fachada, techo plano con cornisa,
            // puerta de madera y una moneda dorada sobre la entrada
            Material bankStone = LitMaterial(new Color(0.78f, 0.75f, 0.68f));
            Material bankDark = LitMaterial(new Color(0.55f, 0.52f, 0.47f));
            Material bankWood = LitMaterial(new Color(0.3f, 0.19f, 0.1f));
            Material bankGold = LitMaterial(new Color(0.95f, 0.78f, 0.2f));

            Vector3 at = new Vector3(size * 0.95f, 0f, 0f);

            // Escalones y cuerpo
            Piece(root.transform, Cube, bankDark, at + new Vector3(0f, size * 0.025f, 0f), new Vector3(size * 0.5f, size * 0.05f, size * 0.7f));
            Piece(root.transform, Cube, bankDark, at + new Vector3(-size * 0.02f, size * 0.07f, 0f), new Vector3(size * 0.44f, size * 0.04f, size * 0.62f));
            Piece(root.transform, Cube, bankStone, at + new Vector3(size * 0.05f, size * 0.24f, 0f), new Vector3(size * 0.3f, size * 0.3f, size * 0.54f));

            // Fachada (mira a -X, hacia la ciudad): cuatro columnas, cornisa y techo
            for (int j = 0; j < 4; j++)
                Piece(root.transform, Cylinder, bankStone, at + new Vector3(-size * 0.14f, size * 0.24f, (j - 1.5f) * size * 0.16f),
                    new Vector3(size * 0.05f, size * 0.15f, size * 0.05f));
            Piece(root.transform, Cube, bankDark, at + new Vector3(-size * 0.02f, size * 0.42f, 0f), new Vector3(size * 0.42f, size * 0.05f, size * 0.62f));
            Piece(root.transform, Cube, bankStone, at + new Vector3(0f, size * 0.46f, 0f), new Vector3(size * 0.34f, size * 0.04f, size * 0.56f));

            // Puerta y moneda dorada
            Piece(root.transform, Cube, bankWood, at + new Vector3(-size * 0.095f, size * 0.17f, 0f), new Vector3(size * 0.02f, size * 0.2f, size * 0.14f));
            GameObject bankCoin = Piece(root.transform, Cylinder, bankGold, at + new Vector3(-size * 0.17f, size * 0.54f, 0f),
                new Vector3(size * 0.16f, size * 0.012f, size * 0.16f));
            bankCoin.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);

            // Símbolo del euro (€) en la cara de la moneda: arco abierto hacia la derecha y dos barras horizontales.
            // Visto de frente (desde -X) la derecha es -Z.
            Material euroMat = LitMaterial(new Color(0.45f, 0.3f, 0.05f));
            Vector3 coinAt = at + new Vector3(-size * 0.17f - size * 0.018f, size * 0.54f, 0f);
            float euroR = size * 0.04f;
            for (int i = 0; i <= 10; i++)
            {
                float a = Mathf.Deg2Rad * (45f + i * 27f); // de 45° a 315°, la abertura queda a la derecha
                Piece(root.transform, Cube, euroMat, coinAt + new Vector3(0f, Mathf.Sin(a) * euroR, -Mathf.Cos(a) * euroR),
                    new Vector3(size * 0.012f, size * 0.016f, size * 0.016f));
            }
            for (int bar = -1; bar <= 1; bar += 2)
                Piece(root.transform, Cube, euroMat, coinAt + new Vector3(0f, bar * size * 0.014f, size * 0.01f),
                    new Vector3(size * 0.012f, size * 0.008f, size * 0.07f));
        }

        if (city.buildings.Contains(BuildingKind.PrintingHouse))
        {
            // Prensa: taller de madera con tejado a dos aguas, una prensa de tornillo con su barra y pliegos de papel
            Material prWood = LitMaterial(new Color(0.45f, 0.3f, 0.16f));
            Material prDark = LitMaterial(new Color(0.28f, 0.18f, 0.1f));
            Material prRoof = LitMaterial(new Color(0.55f, 0.25f, 0.18f));
            Material prPaper = LitMaterial(new Color(0.93f, 0.9f, 0.8f));
            Material prIron = LitMaterial(new Color(0.3f, 0.3f, 0.34f));

            Vector3 at = new Vector3(-size * 0.9f, 0f, size * 0.9f);

            // Cuerpo y tejado
            Piece(root.transform, Cube, prWood, at + new Vector3(0f, size * 0.15f, 0f), new Vector3(size * 0.4f, size * 0.3f, size * 0.4f));
            GameObject prRoofPiece = Piece(root.transform, Cylinder, prRoof, at + new Vector3(0f, size * 0.34f, 0f), new Vector3(size * 0.3f, size * 0.22f, size * 0.3f));
            prRoofPiece.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            Piece(root.transform, Cube, prDark, at + new Vector3(size * 0.2f, size * 0.12f, 0f), new Vector3(size * 0.02f, size * 0.18f, size * 0.12f)); // puerta

            // Prensa de tornillo a la entrada: dos montantes, tornillo central y barra giratoria
            Vector3 press = at + new Vector3(size * 0.34f, 0f, 0f);
            Piece(root.transform, Cube, prDark, press + new Vector3(0f, size * 0.04f, 0f), new Vector3(size * 0.16f, size * 0.08f, size * 0.2f));
            for (int side = -1; side <= 1; side += 2)
                Piece(root.transform, Cube, prWood, press + new Vector3(0f, size * 0.17f, side * size * 0.08f), new Vector3(size * 0.03f, size * 0.26f, size * 0.03f));
            Piece(root.transform, Cube, prWood, press + new Vector3(0f, size * 0.3f, 0f), new Vector3(size * 0.04f, size * 0.03f, size * 0.2f));
            Piece(root.transform, Cylinder, prIron, press + new Vector3(0f, size * 0.22f, 0f), new Vector3(size * 0.025f, size * 0.12f, size * 0.025f));
            Piece(root.transform, Cube, prDark, press + new Vector3(0f, size * 0.36f, 0f), new Vector3(size * 0.03f, size * 0.025f, size * 0.26f));
            Piece(root.transform, Cube, prPaper, press + new Vector3(0f, size * 0.1f, 0f), new Vector3(size * 0.1f, size * 0.02f, size * 0.12f));
        }

        if (city.buildings.Contains(BuildingKind.Newspaper))
        {
            // Periódico: redacción de ladrillo con rótulo, una rotativa a la entrada y un fajo de ejemplares
            Material npBrick = LitMaterial(new Color(0.62f, 0.5f, 0.4f));
            Material npRoof = LitMaterial(new Color(0.3f, 0.3f, 0.34f));
            Material npSign = LitMaterial(new Color(0.95f, 0.92f, 0.82f));
            Material npInk = LitMaterial(new Color(0.12f, 0.12f, 0.14f));
            Material npWindow = LitMaterial(new Color(0.9f, 0.8f, 0.4f));

            Vector3 at = new Vector3(-size * 1.45f, 0f, -size * 0.5f);

            Piece(root.transform, Cube, npBrick, at + new Vector3(0f, size * 0.13f, 0f), new Vector3(size * 0.36f, size * 0.26f, size * 0.46f));
            Piece(root.transform, Cube, npRoof, at + new Vector3(0f, size * 0.275f, 0f), new Vector3(size * 0.4f, size * 0.03f, size * 0.5f));

            // Rótulo en la fachada (mira a +X, hacia la ciudad) con renglones de texto
            Piece(root.transform, Cube, npSign, at + new Vector3(size * 0.185f, size * 0.2f, 0f), new Vector3(size * 0.01f, size * 0.07f, size * 0.3f));
            for (int i = -2; i <= 2; i++)
                Piece(root.transform, Cube, npInk, at + new Vector3(size * 0.192f, size * 0.2f, i * size * 0.05f), new Vector3(size * 0.005f, size * 0.04f, size * 0.025f));

            for (int side = -1; side <= 1; side += 2)
                Piece(root.transform, Cube, npWindow, at + new Vector3(size * 0.185f, size * 0.1f, side * size * 0.13f), new Vector3(size * 0.01f, size * 0.07f, size * 0.07f));

            // Rotativa y fajo de periódicos junto a la puerta
            GameObject drum = Piece(root.transform, Cylinder, npInk, at + new Vector3(size * 0.26f, size * 0.07f, -size * 0.1f), new Vector3(size * 0.08f, size * 0.08f, size * 0.08f));
            drum.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            for (int i = 0; i < 4; i++)
                Piece(root.transform, Cube, npSign, at + new Vector3(size * 0.27f, size * (0.015f + i * 0.02f), size * 0.1f), new Vector3(size * 0.07f, size * 0.015f, size * 0.09f));
        }

        if (city.buildings.Contains(BuildingKind.GutenbergPrint))
        {
            // Prensa de Gutenberg: gran imprenta moderna con una rotativa de dos cilindros, una torre con un rótulo de periódico
            // y un enorme rollo de papel
            Material gpStone = LitMaterial(new Color(0.72f, 0.7f, 0.66f));
            Material gpBrick = LitMaterial(new Color(0.58f, 0.34f, 0.26f));
            Material gpIron = LitMaterial(new Color(0.28f, 0.28f, 0.32f));
            Material gpPaper = LitMaterial(new Color(0.94f, 0.91f, 0.82f));
            Material gpInk = LitMaterial(new Color(0.1f, 0.1f, 0.12f));
            Vector3 at = new Vector3(-size * 1.5f, 0f, -size * 1.3f); // si no hay celdilla libre, fuera del disco de la ciudad
            Transform site = WonderSite(city, BuildingKind.GutenbergPrint, root.transform, grid, earth, ref at);

            Piece(site, Cube, gpStone, at + new Vector3(0f, size * 0.03f, 0f), new Vector3(size * 0.8f, size * 0.06f, size * 0.7f));
            Piece(site, Cube, gpBrick, at + new Vector3(0f, size * 0.22f, 0f), new Vector3(size * 0.6f, size * 0.32f, size * 0.5f));
            Piece(site, Cube, gpIron, at + new Vector3(0f, size * 0.4f, 0f), new Vector3(size * 0.64f, size * 0.04f, size * 0.54f));

            // Torre con reloj y rótulo
            Piece(site, Cube, gpBrick, at + new Vector3(-size * 0.22f, size * 0.55f, 0f), new Vector3(size * 0.16f, size * 0.3f, size * 0.16f));
            Piece(site, Cube, gpPaper, at + new Vector3(-size * 0.22f, size * 0.62f, size * 0.09f), new Vector3(size * 0.1f, size * 0.1f, size * 0.01f));

            // Rotativa: dos cilindros horizontales con el papel entre ellos
            for (int i = 0; i < 2; i++)
            {
                GameObject roll = Piece(site, Cylinder, gpIron, at + new Vector3(size * 0.1f, size * (0.28f + i * 0.1f), size * 0.27f), new Vector3(size * 0.09f, size * 0.2f, size * 0.09f));
                roll.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            }
            Piece(site, Cube, gpPaper, at + new Vector3(size * 0.1f, size * 0.33f, size * 0.27f), new Vector3(size * 0.34f, size * 0.01f, size * 0.12f));

            // Rollo de papel gigante a un lado y pila de periódicos delante
            GameObject paperRoll = Piece(site, Cylinder, gpPaper, at + new Vector3(size * 0.22f, size * 0.12f, -size * 0.3f), new Vector3(size * 0.2f, size * 0.12f, size * 0.2f));
            paperRoll.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            for (int i = 0; i < 5; i++)
            {
                Piece(site, Cube, gpPaper, at + new Vector3(size * 0.34f, size * (0.075f + i * 0.02f), size * 0.1f), new Vector3(size * 0.14f, size * 0.018f, size * 0.18f));
                Piece(site, Cube, gpInk, at + new Vector3(size * 0.34f, size * (0.075f + i * 0.02f), size * 0.1f), new Vector3(size * 0.1f, size * 0.019f, size * 0.01f));
            }
        }

        if (city.buildings.Contains(BuildingKind.HooverDam))
        {
            // Presa Hoover: gran muro curvo de hormigón entre dos paredes de roca, vertedero, agua detrás y dos torres de toma
            Material hvConcrete = LitMaterial(new Color(0.78f, 0.77f, 0.73f));
            Material hvDark = LitMaterial(new Color(0.45f, 0.44f, 0.46f));
            Material hvRock = LitMaterial(new Color(0.5f, 0.38f, 0.28f));
            Material hvWater = LitMaterial(new Color(0.2f, 0.45f, 0.72f));
            Material hvGlow = LitMaterial(new Color(0.95f, 0.85f, 0.35f));
            Vector3 at = new Vector3(size * 1.5f, 0f, size * 1.4f); // si no hay celdilla libre, fuera del disco de la ciudad
            Transform site = WonderSite(city, BuildingKind.HooverDam, root.transform, grid, earth, ref at);

            // Paredes del cañón y embalse
            for (int side = -1; side <= 1; side += 2)
                Piece(site, Cube, hvRock, at + new Vector3(0f, size * 0.2f, side * size * 0.42f), new Vector3(size * 0.5f, size * 0.4f, size * 0.22f));
            Piece(site, Cube, hvWater, at + new Vector3(size * 0.22f, size * 0.03f, 0f), new Vector3(size * 0.34f, size * 0.05f, size * 0.62f));

            // Muro curvo: tramos escalonados a lo largo del río
            for (int i = -2; i <= 2; i++)
            {
                float off = Mathf.Abs(i) * size * 0.015f;
                Piece(site, Cube, hvConcrete, at + new Vector3(-off, size * 0.2f, i * size * 0.12f), new Vector3(size * 0.12f, size * 0.4f, size * 0.13f));
            }
            Piece(site, Cube, hvDark, at + new Vector3(0f, size * 0.41f, 0f), new Vector3(size * 0.16f, size * 0.025f, size * 0.7f));

            // Dos torres de toma y la central al pie con luces
            for (int side = -1; side <= 1; side += 2)
            {
                Piece(site, Cylinder, hvConcrete, at + new Vector3(size * 0.04f, size * 0.55f, side * size * 0.22f), new Vector3(size * 0.1f, size * 0.15f, size * 0.1f));
                Piece(site, Cube, hvDark, at + new Vector3(size * 0.04f, size * 0.71f, side * size * 0.22f), new Vector3(size * 0.12f, size * 0.025f, size * 0.12f));
            }
            Piece(site, Cube, hvConcrete, at + new Vector3(-size * 0.17f, size * 0.07f, 0f), new Vector3(size * 0.16f, size * 0.14f, size * 0.4f));
            Piece(site, Cube, hvGlow, at + new Vector3(-size * 0.255f, size * 0.08f, 0f), new Vector3(size * 0.01f, size * 0.05f, size * 0.3f));
        }

        if (city.buildings.Contains(BuildingKind.HydroDam))
        {
            // Presa hidroeléctrica: muro curvo de hormigón con agua detrás, casa de turbinas y una torre de cables
            Material dmConcrete = LitMaterial(new Color(0.68f, 0.68f, 0.66f));
            Material dmDark = LitMaterial(new Color(0.4f, 0.4f, 0.43f));
            Material dmWater = LitMaterial(new Color(0.2f, 0.45f, 0.7f));
            Material dmGlow = LitMaterial(new Color(0.95f, 0.85f, 0.35f));

            Vector3 at = new Vector3(size * 1.45f, 0f, -size * 0.55f);

            // Embalse (agua) y muro de la presa
            Piece(root.transform, Cube, dmWater, at + new Vector3(size * 0.12f, size * 0.03f, 0f), new Vector3(size * 0.22f, size * 0.05f, size * 0.5f));
            Piece(root.transform, Cube, dmConcrete, at + new Vector3(-size * 0.02f, size * 0.12f, 0f), new Vector3(size * 0.1f, size * 0.24f, size * 0.52f));
            Piece(root.transform, Cube, dmDark, at + new Vector3(-size * 0.02f, size * 0.25f, 0f), new Vector3(size * 0.12f, size * 0.025f, size * 0.54f));

            // Casa de turbinas al pie y torre de transmisión
            Piece(root.transform, Cube, dmConcrete, at + new Vector3(-size * 0.14f, size * 0.07f, 0f), new Vector3(size * 0.14f, size * 0.14f, size * 0.24f));
            Piece(root.transform, Cube, dmGlow, at + new Vector3(-size * 0.215f, size * 0.08f, 0f), new Vector3(size * 0.01f, size * 0.05f, size * 0.12f));
            Piece(root.transform, Cylinder, dmDark, at + new Vector3(-size * 0.1f, size * 0.24f, size * 0.22f), new Vector3(size * 0.025f, size * 0.22f, size * 0.025f));
            Piece(root.transform, Cube, dmDark, at + new Vector3(-size * 0.1f, size * 0.42f, size * 0.22f), new Vector3(size * 0.14f, size * 0.015f, size * 0.015f));
        }

        if (city.buildings.Contains(BuildingKind.Factory))
        {
            // Fábrica: nave de ladrillo con tejado en dientes de sierra, una alta chimenea con humo y una puerta grande
            Material fcBrick = LitMaterial(new Color(0.55f, 0.27f, 0.2f));
            Material fcDark = LitMaterial(new Color(0.25f, 0.25f, 0.28f));
            Material fcRoof = LitMaterial(new Color(0.35f, 0.38f, 0.42f));
            Material fcWindow = LitMaterial(new Color(0.9f, 0.8f, 0.4f));
            Material fcSmoke = LitMaterial(new Color(0.6f, 0.6f, 0.62f));

            Vector3 at = new Vector3(size * 1.45f, 0f, size * 0.5f);

            // Nave y tejado en dientes de sierra
            Piece(root.transform, Cube, fcBrick, at + new Vector3(0f, size * 0.12f, 0f), new Vector3(size * 0.4f, size * 0.24f, size * 0.5f));
            for (int i = 0; i < 3; i++)
            {
                GameObject tooth = Piece(root.transform, Cube, fcRoof, at + new Vector3(0f, size * 0.29f, (i - 1) * size * 0.16f), new Vector3(size * 0.42f, size * 0.02f, size * 0.17f));
                tooth.transform.localRotation = Quaternion.Euler(18f, 0f, 0f);
            }

            // Puerta y ventanas en la fachada (mira a -X, hacia la ciudad)
            Piece(root.transform, Cube, fcDark, at + new Vector3(-size * 0.205f, size * 0.09f, 0f), new Vector3(size * 0.01f, size * 0.16f, size * 0.16f));
            for (int side = -1; side <= 1; side += 2)
                Piece(root.transform, Cube, fcWindow, at + new Vector3(-size * 0.205f, size * 0.15f, side * size * 0.17f), new Vector3(size * 0.01f, size * 0.07f, size * 0.07f));

            // Chimenea alta con franja y humo
            Piece(root.transform, Cylinder, fcBrick, at + new Vector3(size * 0.12f, size * 0.42f, -size * 0.3f), new Vector3(size * 0.09f, size * 0.3f, size * 0.09f));
            Piece(root.transform, Cylinder, fcDark, at + new Vector3(size * 0.12f, size * 0.71f, -size * 0.3f), new Vector3(size * 0.1f, size * 0.025f, size * 0.1f));
            Piece(root.transform, Sphere, fcSmoke, at + new Vector3(size * 0.12f, size * 0.85f, -size * 0.3f), new Vector3(size * 0.16f, size * 0.12f, size * 0.16f));
        }

        if (city.buildings.Contains(BuildingKind.ComputerLab))
        {
            // Laboratorio informático: edificio bajo de cristal y hormigón con filas de servidores iluminados y una antena parabólica
            Material clConcrete = LitMaterial(new Color(0.8f, 0.8f, 0.82f));
            Material clGlass = LitMaterial(new Color(0.2f, 0.35f, 0.5f));
            Material clDark = LitMaterial(new Color(0.12f, 0.12f, 0.14f));
            Material clLed = LitMaterial(new Color(0.3f, 1f, 0.5f));
            Material clCiv = LitMaterial(civColor);
            clLed.EnableKeyword("_EMISSION");
            if (clLed.HasProperty("_EmissionColor")) clLed.SetColor("_EmissionColor", new Color(0.2f, 1f, 0.4f) * 1.5f);

            Vector3 at = new Vector3(-size * 1.9f, 0f, size * 0.5f);

            Piece(root.transform, Cube, clConcrete, at + new Vector3(0f, size * 0.03f, 0f), new Vector3(size * 0.55f, size * 0.06f, size * 0.45f));
            Piece(root.transform, Cube, clGlass, at + new Vector3(0f, size * 0.16f, 0f), new Vector3(size * 0.5f, size * 0.2f, size * 0.4f));
            Piece(root.transform, Cube, clCiv, at + new Vector3(0f, size * 0.27f, 0f), new Vector3(size * 0.52f, size * 0.025f, size * 0.42f));

            // Racks de servidores visibles tras el cristal de la fachada (+X, hacia la ciudad)
            for (int i = 0; i < 4; i++)
            {
                float z = (i - 1.5f) * size * 0.09f;
                Piece(root.transform, Cube, clDark, at + new Vector3(size * 0.2f, size * 0.15f, z), new Vector3(size * 0.05f, size * 0.15f, size * 0.06f));
                Piece(root.transform, Cube, clLed, at + new Vector3(size * 0.232f, size * 0.18f, z), new Vector3(size * 0.008f, size * 0.02f, size * 0.04f));
            }

            // Antena parabólica en el tejado
            Piece(root.transform, Cylinder, clDark, at + new Vector3(-size * 0.1f, size * 0.33f, 0f), new Vector3(size * 0.02f, size * 0.06f, size * 0.02f));
            GameObject dish = Piece(root.transform, Sphere, clConcrete, at + new Vector3(-size * 0.1f, size * 0.41f, 0f), new Vector3(size * 0.2f, size * 0.04f, size * 0.2f));
            dish.transform.localRotation = Quaternion.Euler(0f, 0f, -50f);
        }

        // Edificios de felicidad y maravillas nuevas (ver CivViews.Extra.cs)
        BuildHappinessBuildings(city, root.transform, size, civMat);
        BuildExtraWonders(city, root.transform, grid, earth, size, civMat);

        if (city.buildings.Contains(BuildingKind.FusionReactor))
        {
            // Reactor de fusión: anillo toroidal de esferas de plasma brillantes (tokamak) alrededor de un núcleo, sobre una
            // base de hormigón con cuatro pilares
            Material frBase = LitMaterial(new Color(0.55f, 0.56f, 0.6f));
            Material frDark = LitMaterial(FutGraphite);
            Material frPlasma = FutGlow(new Color(1f, 0.45f, 0.9f), 2.2f);
            Material frCore = FutGlow(new Color(1f, 0.95f, 0.6f), 2.5f);
            Material frCiv = LitMaterial(civColor);
            Vector3 at = new Vector3(size * 2.1f, 0f, -size * 0.9f);

            Piece(root.transform, Cylinder, frBase, at + new Vector3(0f, size * 0.03f, 0f), new Vector3(size * 0.6f, size * 0.03f, size * 0.6f));
            Piece(root.transform, Cylinder, frCiv, at + new Vector3(0f, size * 0.065f, 0f), new Vector3(size * 0.62f, size * 0.01f, size * 0.62f));
            for (int i = 0; i < 4; i++)
            {
                float a = i * Mathf.PI * 0.5f + Mathf.PI * 0.25f;
                Piece(root.transform, Cylinder, frDark, at + new Vector3(Mathf.Cos(a) * size * 0.2f, size * 0.12f, Mathf.Sin(a) * size * 0.2f), new Vector3(size * 0.04f, size * 0.07f, size * 0.04f));
            }

            Vector3 center = at + new Vector3(0f, size * 0.3f, 0f);
            Piece(root.transform, Sphere, frCore, center, Vector3.one * size * 0.14f);
            for (int i = 0; i < 20; i++)
            {
                float a = i / 20f * Mathf.PI * 2f;
                Vector3 p = center + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * size * 0.24f;
                Piece(root.transform, Sphere, i % 2 == 0 ? frPlasma : frDark, p, Vector3.one * size * (i % 2 == 0 ? 0.075f : 0.06f));
            }
        }

        if (city.buildings.Contains(BuildingKind.QuantumComputer))
        {
            // Computadora cuántica: cubo oscuro de cristal sobre un pedestal con tres anillos luminosos que flotan en órbita
            Material qcCase = LitMaterial(new Color(0.08f, 0.1f, 0.18f));
            qcCase.SetFloat("_Glossiness", 0.9f);
            Material qcBase = LitMaterial(new Color(0.75f, 0.76f, 0.8f));
            Material qcGlow = FutGlow(new Color(0.6f, 0.3f, 1f), 2.2f);
            Material qcCyan = FutGlow(FutCyan, 2.2f);
            Material qcCiv = LitMaterial(civColor);
            Vector3 at = new Vector3(-size * 2.1f, 0f, size * 1.5f);

            Piece(root.transform, Cube, qcBase, at + new Vector3(0f, size * 0.04f, 0f), new Vector3(size * 0.5f, size * 0.08f, size * 0.5f));
            Piece(root.transform, Cube, qcCiv, at + new Vector3(0f, size * 0.085f, 0f), new Vector3(size * 0.52f, size * 0.01f, size * 0.52f));
            Piece(root.transform, Cube, qcCase, at + new Vector3(0f, size * 0.24f, 0f), new Vector3(size * 0.3f, size * 0.3f, size * 0.3f));
            Piece(root.transform, Cube, qcGlow, at + new Vector3(0f, size * 0.24f, size * 0.155f), new Vector3(size * 0.18f, size * 0.18f, size * 0.01f));

            // Anillos de partículas inclinados en distintos planos
            for (int ring = 0; ring < 3; ring++)
            {
                Quaternion rot = Quaternion.Euler(ring * 60f, ring * 50f, ring * 25f);
                for (int i = 0; i < 14; i++)
                {
                    float a = i / 14f * Mathf.PI * 2f;
                    Vector3 p = at + new Vector3(0f, size * 0.52f, 0f) + rot * new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * size * 0.26f;
                    Piece(root.transform, Sphere, i % 3 == 0 ? qcCyan : qcGlow, p, Vector3.one * size * (i % 3 == 0 ? 0.05f : 0.028f));
                }
            }
        }

        if (city.buildings.Contains(BuildingKind.LunarBase))
        {
            // Base lunar: tres cúpulas blancas de distinto tamaño unidas por túneles, con ventanas luminosas, una antena y
            // un cráter de polvo gris a su alrededor
            Material lbDust = LitMaterial(new Color(0.62f, 0.62f, 0.64f));
            Material lbWhite = LitMaterial(FutWhite);
            Material lbGlass = LitMaterial(new Color(0.3f, 0.45f, 0.6f));
            Material lbGlow = FutGlow(new Color(1f, 0.9f, 0.6f), 1.8f);
            Material lbDark = LitMaterial(FutGraphite);
            Material lbCiv = LitMaterial(civColor);
            Vector3 at = new Vector3(size * 0.4f, 0f, size * 2.1f);

            Piece(root.transform, Cylinder, lbDust, at + new Vector3(0f, size * 0.015f, 0f), new Vector3(size * 0.85f, size * 0.015f, size * 0.85f));
            Vector3[] domes = { new Vector3(0f, 0f, 0f), new Vector3(size * 0.28f, 0f, size * 0.12f), new Vector3(-size * 0.22f, 0f, -size * 0.2f) };
            float[] radius = { 0.2f, 0.13f, 0.1f };
            for (int i = 0; i < domes.Length; i++)
            {
                Vector3 c = at + domes[i];
                float r2 = size * radius[i];
                Piece(root.transform, Sphere, lbWhite, c + new Vector3(0f, r2 * 0.05f, 0f), new Vector3(r2 * 2f, r2 * 1.3f, r2 * 2f));
                Piece(root.transform, Cylinder, lbCiv, c + new Vector3(0f, r2 * 0.12f, 0f), new Vector3(r2 * 2.02f, r2 * 0.05f, r2 * 2.02f));
                Piece(root.transform, Cube, lbGlow, c + new Vector3(0f, r2 * 0.45f, r2 * 0.95f), new Vector3(r2 * 0.5f, r2 * 0.22f, r2 * 0.05f)); // ventana luminosa
            }

            // Túneles entre las cúpulas y antena
            ModTube(root.transform, lbGlass, at + new Vector3(size * 0.06f, size * 0.04f, size * 0.02f), at + new Vector3(size * 0.28f, size * 0.04f, size * 0.12f), size * 0.045f);
            ModTube(root.transform, lbGlass, at + new Vector3(-size * 0.06f, size * 0.04f, -size * 0.04f), at + new Vector3(-size * 0.22f, size * 0.04f, -size * 0.2f), size * 0.045f);
            Piece(root.transform, Cylinder, lbDark, at + new Vector3(size * 0.05f, size * 0.32f, -size * 0.05f), new Vector3(size * 0.012f, size * 0.12f, size * 0.012f));
            Piece(root.transform, Sphere, lbGlow, at + new Vector3(size * 0.05f, size * 0.45f, -size * 0.05f), Vector3.one * size * 0.035f);
        }

        if (city.buildings.Contains(BuildingKind.GeneBank))
        {
            // Banco genético: cúpula de cristal sobre una base de la civilización con una doble hélice de ADN giratoria en su
            // interior (dos hileras de esferas luminosas unidas por travesaños)
            Material gbBase = LitMaterial(new Color(0.8f, 0.82f, 0.85f));
            Material gbGlass = LitMaterial(new Color(0.55f, 0.8f, 0.85f));
            Material gbA = FutGlow(new Color(0.2f, 1f, 0.5f), 2f);
            Material gbB = FutGlow(new Color(0.2f, 0.7f, 1f), 2f);
            Material gbRung = LitMaterial(new Color(0.95f, 0.95f, 0.98f));
            Material gbCiv = LitMaterial(civColor);
            Vector3 at = new Vector3(-size * 1.9f, 0f, size * 0.2f); // si no hay celdilla libre, fuera del disco de la ciudad
            Transform site = WonderSite(city, BuildingKind.GeneBank, root.transform, grid, earth, ref at);

            Piece(site, Cylinder, gbBase, at + new Vector3(0f, size * 0.03f, 0f), new Vector3(size * 0.62f, size * 0.03f, size * 0.62f));
            Piece(site, Cylinder, gbCiv, at + new Vector3(0f, size * 0.065f, 0f), new Vector3(size * 0.64f, size * 0.01f, size * 0.64f));
            Piece(site, Sphere, gbGlass, at + new Vector3(0f, size * 0.07f, 0f), new Vector3(size * 0.56f, size * 0.7f, size * 0.56f));

            int steps = 16;
            for (int i = 0; i < steps; i++)
            {
                float k = i / (float)(steps - 1);
                float a = k * Mathf.PI * 4f;
                float y = size * (0.1f + k * 0.5f);
                Vector3 p1 = at + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * size * 0.14f + new Vector3(0f, y, 0f);
                Vector3 p2 = at + new Vector3(-Mathf.Cos(a), 0f, -Mathf.Sin(a)) * size * 0.14f + new Vector3(0f, y, 0f);
                Piece(site, Sphere, gbA, p1, Vector3.one * size * 0.05f);
                Piece(site, Sphere, gbB, p2, Vector3.one * size * 0.05f);
                if (i % 2 == 0) ModTube(site, gbRung, p1, p2, size * 0.012f);
            }
        }

        if (city.buildings.Contains(BuildingKind.SpaceElevator))
        {
            // Ascensor espacial: base de anclaje, un cable luminoso casi vertical hasta una estación orbital en lo alto
            // (disco con paneles solares) y una cabina que sube por el cable
            Material seBase = LitMaterial(new Color(0.6f, 0.62f, 0.66f));
            Material seCable = FutGlow(new Color(0.5f, 0.9f, 1f), 1.8f);
            Material seStation = LitMaterial(FutWhite);
            Material sePanel = LitMaterial(new Color(0.1f, 0.2f, 0.5f));
            Material seDark = LitMaterial(FutGraphite);
            Material seCiv = LitMaterial(civColor);
            Vector3 at = new Vector3(size * 1.9f, 0f, -size * 1.9f); // si no hay celdilla libre, fuera del disco de la ciudad
            Transform site = WonderSite(city, BuildingKind.SpaceElevator, root.transform, grid, earth, ref at);

            Piece(site, Cylinder, seBase, at + new Vector3(0f, size * 0.03f, 0f), new Vector3(size * 0.5f, size * 0.03f, size * 0.5f));
            Piece(site, Cylinder, seCiv, at + new Vector3(0f, size * 0.065f, 0f), new Vector3(size * 0.52f, size * 0.01f, size * 0.52f));
            Piece(site, Cylinder, seDark, at + new Vector3(0f, size * 0.15f, 0f), new Vector3(size * 0.14f, size * 0.09f, size * 0.14f));

            float top = size * 3.2f;
            Piece(site, Cylinder, seCable, at + new Vector3(0f, top * 0.5f, 0f), new Vector3(size * 0.02f, top * 0.5f, size * 0.02f));

            // Cabina subiendo por el cable
            Piece(site, Cube, seStation, at + new Vector3(0f, top * 0.35f, 0f), new Vector3(size * 0.1f, size * 0.12f, size * 0.1f));
            Piece(site, Cube, seCiv, at + new Vector3(0f, top * 0.35f, size * 0.052f), new Vector3(size * 0.06f, size * 0.04f, size * 0.01f));

            // Estación orbital: disco con anillo luminoso y paneles solares
            Vector3 st = at + new Vector3(0f, top, 0f);
            Piece(site, Cylinder, seStation, st, new Vector3(size * 0.34f, size * 0.03f, size * 0.34f));
            Piece(site, Cylinder, seCable, st + new Vector3(0f, size * 0.03f, 0f), new Vector3(size * 0.2f, size * 0.01f, size * 0.2f));
            Piece(site, Sphere, seStation, st + new Vector3(0f, size * 0.06f, 0f), new Vector3(size * 0.14f, size * 0.1f, size * 0.14f));
            for (int side = -1; side <= 1; side += 2)
            {
                Piece(site, Cube, sePanel, st + new Vector3(side * size * 0.4f, 0f, 0f), new Vector3(size * 0.4f, size * 0.01f, size * 0.16f));
                Piece(site, Cube, seDark, st + new Vector3(side * size * 0.2f, 0f, 0f), new Vector3(size * 0.06f, size * 0.02f, size * 0.02f));
            }
        }

        if (city.buildings.Contains(BuildingKind.Satellite))
        {
            // Satélite: plataforma de lanzamiento con un cohete blanco junto a una torre de servicio, y el satélite
            // (cuerpo dorado, paneles solares azules y antena) sobre un pedestal luminoso
            Material stPad = LitMaterial(new Color(0.55f, 0.56f, 0.6f));
            Material stRocket = LitMaterial(FutWhite);
            Material stDark = LitMaterial(FutGraphite);
            Material stGold = LitMaterial(new Color(0.95f, 0.78f, 0.3f));
            Material stPanel = LitMaterial(new Color(0.1f, 0.2f, 0.5f));
            Material stCiv = LitMaterial(civColor);
            Material stGlow = FutGlow(FutCyan);
            Vector3 at = new Vector3(-size * 1.9f, 0f, -size * 1.2f);

            // Plataforma, cohete (cuerpo, cono, aletas y franja de la civilización) y torre de servicio
            Piece(root.transform, Cylinder, stPad, at + new Vector3(0f, size * 0.02f, 0f), new Vector3(size * 0.5f, size * 0.02f, size * 0.5f));
            Piece(root.transform, Cylinder, stRocket, at + new Vector3(-size * 0.1f, size * 0.3f, 0f), new Vector3(size * 0.1f, size * 0.26f, size * 0.1f));
            Piece(root.transform, Sphere, stRocket, at + new Vector3(-size * 0.1f, size * 0.58f, 0f), new Vector3(size * 0.1f, size * 0.14f, size * 0.1f));
            Piece(root.transform, Cylinder, stCiv, at + new Vector3(-size * 0.1f, size * 0.4f, 0f), new Vector3(size * 0.102f, size * 0.03f, size * 0.102f));
            for (int i = 0; i < 4; i++)
            {
                float a = i * Mathf.PI * 0.5f;
                GameObject fin = Piece(root.transform, Cube, stDark, at + new Vector3(-size * 0.1f + Mathf.Cos(a) * size * 0.08f, size * 0.07f, Mathf.Sin(a) * size * 0.08f), new Vector3(size * 0.02f, size * 0.12f, size * 0.07f));
                fin.transform.localRotation = Quaternion.Euler(0f, -a * Mathf.Rad2Deg, 0f);
            }
            for (int sz = -1; sz <= 1; sz += 2)
                Piece(root.transform, Cube, stDark, at + new Vector3(size * 0.06f, size * 0.3f, sz * size * 0.06f), new Vector3(size * 0.02f, size * 0.6f, size * 0.02f));
            Piece(root.transform, Cube, stDark, at + new Vector3(size * 0.06f, size * 0.45f, 0f), new Vector3(size * 0.02f, size * 0.02f, size * 0.12f));

            // Satélite sobre su pedestal
            Vector3 sat = at + new Vector3(size * 0.28f, 0f, size * 0.05f);
            Piece(root.transform, Cylinder, stGlow, sat + new Vector3(0f, size * 0.03f, 0f), new Vector3(size * 0.14f, size * 0.02f, size * 0.14f));
            Piece(root.transform, Cube, stGold, sat + new Vector3(0f, size * 0.15f, 0f), new Vector3(size * 0.12f, size * 0.12f, size * 0.12f));
            for (int side = -1; side <= 1; side += 2)
            {
                Piece(root.transform, Cube, stPanel, sat + new Vector3(side * size * 0.17f, size * 0.15f, 0f), new Vector3(size * 0.2f, size * 0.01f, size * 0.12f));
                Piece(root.transform, Cube, stGlow, sat + new Vector3(side * size * 0.17f, size * 0.156f, 0f), new Vector3(size * 0.2f, size * 0.003f, size * 0.01f));
            }
            Piece(root.transform, Cylinder, stDark, sat + new Vector3(0f, size * 0.26f, 0f), new Vector3(size * 0.012f, size * 0.05f, size * 0.012f));
            GameObject dish = Piece(root.transform, Sphere, stRocket, sat + new Vector3(0f, size * 0.33f, 0f), new Vector3(size * 0.12f, size * 0.025f, size * 0.12f));
            dish.transform.localRotation = Quaternion.Euler(30f, 0f, 0f);
        }

        if (city.buildings.Contains(BuildingKind.NuclearPlant))
        {
            // Central nuclear: dos torres de refrigeración con vapor, cúpula del reactor y una chimenea con franjas
            Material npConcrete = LitMaterial(new Color(0.82f, 0.82f, 0.8f));
            Material npDark = LitMaterial(new Color(0.45f, 0.46f, 0.5f));
            Material npSteam = LitMaterial(new Color(0.95f, 0.95f, 0.97f));
            Material npRed = LitMaterial(new Color(0.8f, 0.2f, 0.15f));
            Material npGlow = LitMaterial(new Color(0.4f, 1f, 0.4f));
            Material npCiv = LitMaterial(civColor);
            npGlow.EnableKeyword("_EMISSION");
            if (npGlow.HasProperty("_EmissionColor")) npGlow.SetColor("_EmissionColor", new Color(0.3f, 1f, 0.3f) * 1.5f);

            Vector3 at = new Vector3(size * 1.9f, 0f, 0f);

            // Torres de refrigeración: perfil de reloj de arena apilando cilindros de distinto ancho
            float[] ringW = { 0.3f, 0.25f, 0.21f, 0.24f };
            for (int t = 0; t < 2; t++)
            {
                Vector3 c = at + new Vector3(size * 0.05f, 0f, (t == 0 ? -1f : 1f) * size * 0.3f);
                for (int i = 0; i < ringW.Length; i++)
                    Piece(root.transform, Cylinder, npConcrete, c + new Vector3(0f, size * (0.06f + i * 0.12f), 0f), new Vector3(size * ringW[i], size * 0.065f, size * ringW[i]));
                Piece(root.transform, Cylinder, npCiv, c + new Vector3(0f, size * 0.52f, 0f), new Vector3(size * 0.245f, size * 0.01f, size * 0.245f));
                Piece(root.transform, Sphere, npSteam, c + new Vector3(0f, size * 0.68f, 0f), new Vector3(size * 0.32f, size * 0.2f, size * 0.32f));
            }

            // Edificio y cúpula del reactor con un anillo verde brillante
            Piece(root.transform, Cube, npDark, at + new Vector3(-size * 0.25f, size * 0.07f, 0f), new Vector3(size * 0.2f, size * 0.14f, size * 0.5f));
            Piece(root.transform, Sphere, npConcrete, at + new Vector3(-size * 0.25f, size * 0.14f, 0f), new Vector3(size * 0.26f, size * 0.22f, size * 0.26f));
            Piece(root.transform, Cylinder, npGlow, at + new Vector3(-size * 0.25f, size * 0.2f, 0f), new Vector3(size * 0.27f, size * 0.01f, size * 0.27f));

            // Chimenea con franjas rojas
            Piece(root.transform, Cylinder, npConcrete, at + new Vector3(-size * 0.28f, size * 0.3f, size * 0.32f), new Vector3(size * 0.04f, size * 0.3f, size * 0.04f));
            Piece(root.transform, Cylinder, npRed, at + new Vector3(-size * 0.28f, size * 0.52f, size * 0.32f), new Vector3(size * 0.042f, size * 0.04f, size * 0.042f));
        }

        if (city.buildings.Contains(BuildingKind.GutenbergPress))
        {
            // Imprenta de Gutenberg: plataforma de piedra con una gran prensa de tornillo de madera y un libro abierto
            Material gbStone = LitMaterial(new Color(0.7f, 0.68f, 0.62f));
            Material gbWood = LitMaterial(new Color(0.45f, 0.3f, 0.16f));
            Material gbDark = LitMaterial(new Color(0.28f, 0.18f, 0.1f));
            Material gbIron = LitMaterial(new Color(0.3f, 0.3f, 0.34f));
            Material gbPaper = LitMaterial(new Color(0.94f, 0.91f, 0.82f));
            Vector3 at = new Vector3(size * 1.4f, 0f, -size * 1.2f); // si no hay celdilla libre, fuera del disco de la ciudad
            Transform site = WonderSite(city, BuildingKind.GutenbergPress, root.transform, grid, earth, ref at);

            Piece(site, Cube, gbStone, at + new Vector3(0f, size * 0.03f, 0f), new Vector3(size * 0.7f, size * 0.06f, size * 0.6f));

            // Bastidor: dos montantes y una viga superior; tornillo central con su barra
            for (int side = -1; side <= 1; side += 2)
                Piece(site, Cube, gbWood, at + new Vector3(0f, size * 0.3f, side * size * 0.17f), new Vector3(size * 0.07f, size * 0.48f, size * 0.07f));
            Piece(site, Cube, gbWood, at + new Vector3(0f, size * 0.56f, 0f), new Vector3(size * 0.09f, size * 0.07f, size * 0.42f));
            Piece(site, Cylinder, gbIron, at + new Vector3(0f, size * 0.42f, 0f), new Vector3(size * 0.05f, size * 0.17f, size * 0.05f));
            Piece(site, Cube, gbDark, at + new Vector3(0f, size * 0.5f, 0f), new Vector3(size * 0.05f, size * 0.04f, size * 0.55f));
            Piece(site, Cube, gbDark, at + new Vector3(0f, size * 0.2f, 0f), new Vector3(size * 0.28f, size * 0.03f, size * 0.26f)); // plato

            // Libro abierto con tapas del color de la civilización y una pila de pliegos
            Piece(site, Cube, civMat, at + new Vector3(-size * 0.25f, size * 0.09f, 0f), new Vector3(size * 0.18f, size * 0.05f, size * 0.26f));
            Piece(site, Cube, gbPaper, at + new Vector3(-size * 0.25f, size * 0.125f, 0f), new Vector3(size * 0.16f, size * 0.03f, size * 0.24f));
            Piece(site, Cube, gbPaper, at + new Vector3(size * 0.22f, size * 0.11f, size * 0.1f), new Vector3(size * 0.14f, size * 0.07f, size * 0.12f));
        }

        if (city.buildings.Contains(BuildingKind.GalileoObservatory))
        {
            // Observatorio de Galileo: base de piedra, torre cilíndrica con cúpula del color de la civilización y un telescopio largo apuntando al cielo
            Material glStone = LitMaterial(new Color(0.78f, 0.76f, 0.7f));
            Material glBrass = LitMaterial(new Color(0.85f, 0.65f, 0.25f));
            Material glDark = LitMaterial(new Color(0.15f, 0.15f, 0.18f));
            Vector3 at = new Vector3(-size * 1.4f, 0f, -size * 1.2f); // si no hay celdilla libre, fuera del disco de la ciudad
            Transform site = WonderSite(city, BuildingKind.GalileoObservatory, root.transform, grid, earth, ref at);

            Piece(site, Cube, glStone, at + new Vector3(0f, size * 0.03f, 0f), new Vector3(size * 0.7f, size * 0.06f, size * 0.7f));
            Piece(site, Cylinder, glStone, at + new Vector3(0f, size * 0.3f, 0f), new Vector3(size * 0.4f, size * 0.24f, size * 0.4f));
            Piece(site, Sphere, civMat, at + new Vector3(0f, size * 0.55f, 0f), new Vector3(size * 0.42f, size * 0.26f, size * 0.42f));
            Piece(site, Cube, glDark, at + new Vector3(0f, size * 0.32f, size * 0.2f), new Vector3(size * 0.1f, size * 0.16f, size * 0.01f)); // puerta

            // Telescopio de latón inclinado sobre un trípode
            for (int i = 0; i < 3; i++)
            {
                float a = i / 3f * Mathf.PI * 2f;
                Piece(site, Cube, glDark, at + new Vector3(Mathf.Cos(a) * size * 0.4f + size * 0.45f, size * 0.12f, Mathf.Sin(a) * size * 0.1f), new Vector3(size * 0.03f, size * 0.24f, size * 0.03f));
            }
            GameObject tube = Piece(site, Cylinder, glBrass, at + new Vector3(size * 0.45f, size * 0.42f, 0f), new Vector3(size * 0.06f, size * 0.3f, size * 0.06f));
            tube.transform.localRotation = Quaternion.Euler(0f, 0f, 40f);
        }

        if (city.buildings.Contains(BuildingKind.FahrenheitStudy))
        {
            // Estudio de Fahrenheit: casita de piedra con tejado del color de la civilización y un gran termómetro de mercurio
            Material fhStone = LitMaterial(new Color(0.8f, 0.76f, 0.68f));
            Material fhGlass = LitMaterial(new Color(0.85f, 0.92f, 0.95f));
            Material fhMercury = LitMaterial(new Color(0.85f, 0.15f, 0.12f));
            Material fhDark = LitMaterial(new Color(0.25f, 0.18f, 0.12f));
            Vector3 at = new Vector3(-size * 0.2f, 0f, -size * 1.6f); // si no hay celdilla libre, fuera del disco de la ciudad
            Transform site = WonderSite(city, BuildingKind.FahrenheitStudy, root.transform, grid, earth, ref at);

            Piece(site, Cube, fhStone, at + new Vector3(0f, size * 0.15f, 0f), new Vector3(size * 0.55f, size * 0.3f, size * 0.45f));
            Piece(site, Cube, civMat, at + new Vector3(0f, size * 0.34f, 0f), new Vector3(size * 0.62f, size * 0.1f, size * 0.52f));
            Piece(site, Cube, fhDark, at + new Vector3(0f, size * 0.12f, size * 0.23f), new Vector3(size * 0.1f, size * 0.2f, size * 0.01f)); // puerta

            // Termómetro gigante junto a la casa: tubo de cristal, columna de mercurio y bulbo
            Piece(site, Cylinder, fhGlass, at + new Vector3(size * 0.5f, size * 0.38f, 0f), new Vector3(size * 0.06f, size * 0.38f, size * 0.06f));
            Piece(site, Cylinder, fhMercury, at + new Vector3(size * 0.5f, size * 0.28f, 0f), new Vector3(size * 0.035f, size * 0.26f, size * 0.035f));
            Piece(site, Sphere, fhMercury, at + new Vector3(size * 0.5f, size * 0.06f, 0f), new Vector3(size * 0.14f, size * 0.14f, size * 0.14f));
        }

        if (city.buildings.Contains(BuildingKind.RadioTower))
        {
            // Torre de radio de Marconi: torre de celosía con franjas rojas y blancas, travesaños, baliza roja y una caseta de control
            Material rtSteel = LitMaterial(new Color(0.85f, 0.85f, 0.88f));
            Material rtRed = LitMaterial(new Color(0.85f, 0.2f, 0.15f));
            Material rtHut = LitMaterial(new Color(0.75f, 0.75f, 0.72f));
            Material rtBeacon = LitMaterial(new Color(1f, 0.2f, 0.2f));
            rtBeacon.EnableKeyword("_EMISSION");
            if (rtBeacon.HasProperty("_EmissionColor")) rtBeacon.SetColor("_EmissionColor", new Color(1f, 0.1f, 0.1f) * 2f);
            Vector3 at = new Vector3(size * 1.4f, 0f, size * 1.2f); // si no hay celdilla libre, fuera del disco de la ciudad
            Transform site = WonderSite(city, BuildingKind.RadioTower, root.transform, grid, earth, ref at);

            Piece(site, Cube, rtHut, at + new Vector3(size * 0.28f, size * 0.06f, 0f), new Vector3(size * 0.22f, size * 0.12f, size * 0.22f));
            Piece(site, Cube, civMat, at + new Vector3(size * 0.28f, size * 0.13f, 0f), new Vector3(size * 0.24f, size * 0.02f, size * 0.24f));

            // Cuatro patas inclinadas que se juntan en lo alto, con tramos alternos rojos y blancos
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                    for (int seg = 0; seg < 5; seg++)
                    {
                        float k0 = seg / 5f, k1 = (seg + 1) / 5f;
                        float w0 = Mathf.Lerp(0.2f, 0.02f, k0), w1 = Mathf.Lerp(0.2f, 0.02f, k1);
                        Vector3 a = at + new Vector3(sx * size * w0 * 0.5f, size * 1.4f * k0, sz * size * w0 * 0.5f);
                        Vector3 b = at + new Vector3(sx * size * w1 * 0.5f, size * 1.4f * k1, sz * size * w1 * 0.5f);
                        ModTube(site, seg % 2 == 0 ? rtRed : rtSteel, a, b, size * 0.018f);
                    }

            // Travesaños horizontales
            for (int i = 1; i < 6; i++)
            {
                float k = i / 6f;
                float w = Mathf.Lerp(0.2f, 0.02f, k) * size;
                Piece(site, Cube, rtSteel, at + new Vector3(0f, size * 1.4f * k, 0f), new Vector3(w, size * 0.012f, size * 0.012f));
                Piece(site, Cube, rtSteel, at + new Vector3(0f, size * 1.4f * k, 0f), new Vector3(size * 0.012f, size * 0.012f, w));
            }
            Piece(site, Cylinder, rtSteel, at + new Vector3(0f, size * 1.55f, 0f), new Vector3(size * 0.012f, size * 0.15f, size * 0.012f));
            Piece(site, Sphere, rtBeacon, at + new Vector3(0f, size * 1.72f, 0f), Vector3.one * size * 0.05f);
        }

        if (city.buildings.Contains(BuildingKind.FordAssembly))
        {
            // Cadena de montaje de Ford: nave larga con tejado en dientes de sierra, cinta transportadora con coches
            // en distintas fases de montaje y una chimenea
            Material fdWall = LitMaterial(new Color(0.7f, 0.7f, 0.68f));
            Material fdRoof = LitMaterial(new Color(0.35f, 0.38f, 0.42f));
            Material fdBelt = LitMaterial(new Color(0.15f, 0.15f, 0.17f));
            Material fdTyre = LitMaterial(new Color(0.06f, 0.06f, 0.06f));
            Material fdChrome = LitMaterial(new Color(0.8f, 0.82f, 0.85f));
            Vector3 at = new Vector3(-size * 1.5f, 0f, size * 1.4f); // si no hay celdilla libre, fuera del disco de la ciudad
            Transform site = WonderSite(city, BuildingKind.FordAssembly, root.transform, grid, earth, ref at);

            Piece(site, Cube, fdWall, at + new Vector3(0f, size * 0.12f, -size * 0.12f), new Vector3(size * 0.9f, size * 0.24f, size * 0.36f));
            for (int i = 0; i < 4; i++)
            {
                GameObject tooth = Piece(site, Cube, fdRoof, at + new Vector3((i - 1.5f) * size * 0.22f, size * 0.29f, -size * 0.12f), new Vector3(size * 0.23f, size * 0.02f, size * 0.38f));
                tooth.transform.localRotation = Quaternion.Euler(0f, 0f, 16f);
            }
            Piece(site, Cube, civMat, at + new Vector3(0f, size * 0.22f, size * 0.07f), new Vector3(size * 0.9f, size * 0.04f, size * 0.01f)); // rótulo

            // Cinta transportadora delante de la nave con coches (más completos hacia la derecha)
            Piece(site, Cube, fdBelt, at + new Vector3(0f, size * 0.03f, size * 0.22f), new Vector3(size * 0.95f, size * 0.04f, size * 0.12f));
            for (int i = 0; i < 4; i++)
            {
                float x = (i - 1.5f) * size * 0.22f;
                Piece(site, Cube, civMat, at + new Vector3(x, size * 0.09f, size * 0.22f), new Vector3(size * 0.14f, size * 0.05f, size * 0.08f));
                if (i >= 1) Piece(site, Cube, fdChrome, at + new Vector3(x, size * 0.125f, size * 0.22f), new Vector3(size * 0.08f, size * 0.035f, size * 0.07f));
                if (i >= 2)
                    for (int w = -1; w <= 1; w += 2)
                        Piece(site, Sphere, fdTyre, at + new Vector3(x + w * size * 0.045f, size * 0.07f, size * 0.265f), Vector3.one * size * 0.035f);
            }

            Piece(site, Cylinder, fdRoof, at + new Vector3(size * 0.38f, size * 0.38f, -size * 0.25f), new Vector3(size * 0.06f, size * 0.2f, size * 0.06f));
        }

        if (city.buildings.Contains(BuildingKind.FlemingLab))
        {
            // Laboratorio de Fleming: pabellón blanco con tejado de la civilización, un gran matraz con líquido verde brillante
            // y una enorme placa de Petri con moho
            Material flWall = LitMaterial(new Color(0.9f, 0.9f, 0.92f));
            Material flGlass = LitMaterial(new Color(0.75f, 0.9f, 0.95f));
            Material flLiquid = LitMaterial(new Color(0.3f, 0.9f, 0.4f));
            Material flMould = LitMaterial(new Color(0.45f, 0.65f, 0.35f));
            Material flDark = LitMaterial(new Color(0.2f, 0.2f, 0.24f));
            flLiquid.EnableKeyword("_EMISSION");
            if (flLiquid.HasProperty("_EmissionColor")) flLiquid.SetColor("_EmissionColor", new Color(0.2f, 0.9f, 0.3f) * 1.2f);
            Vector3 at = new Vector3(size * 0.6f, 0f, size * 1.7f); // si no hay celdilla libre, fuera del disco de la ciudad
            Transform site = WonderSite(city, BuildingKind.FlemingLab, root.transform, grid, earth, ref at);

            Piece(site, Cube, flWall, at + new Vector3(0f, size * 0.13f, 0f), new Vector3(size * 0.55f, size * 0.26f, size * 0.4f));
            Piece(site, Cube, civMat, at + new Vector3(0f, size * 0.29f, 0f), new Vector3(size * 0.6f, size * 0.06f, size * 0.45f));
            Piece(site, Cube, flDark, at + new Vector3(0f, size * 0.1f, size * 0.205f), new Vector3(size * 0.1f, size * 0.18f, size * 0.01f)); // puerta
            for (int side = -1; side <= 1; side += 2)
                Piece(site, Cube, flGlass, at + new Vector3(side * size * 0.18f, size * 0.15f, size * 0.205f), new Vector3(size * 0.1f, size * 0.1f, size * 0.01f));

            // Matraz Erlenmeyer: cuerpo cónico apilando cilindros, cuello y líquido
            float[] flask = { 0.2f, 0.17f, 0.14f, 0.11f, 0.08f };
            for (int i = 0; i < flask.Length; i++)
            {
                Piece(site, Cylinder, flGlass, at + new Vector3(-size * 0.5f, size * (0.05f + i * 0.045f), 0f), new Vector3(size * flask[i], size * 0.025f, size * flask[i]));
                if (i < 3) Piece(site, Cylinder, flLiquid, at + new Vector3(-size * 0.5f, size * (0.05f + i * 0.045f), 0f), new Vector3(size * flask[i] * 0.85f, size * 0.026f, size * flask[i] * 0.85f));
            }
            Piece(site, Cylinder, flGlass, at + new Vector3(-size * 0.5f, size * 0.33f, 0f), new Vector3(size * 0.05f, size * 0.12f, size * 0.05f));

            // Placa de Petri con moho
            Piece(site, Cylinder, flGlass, at + new Vector3(size * 0.5f, size * 0.025f, 0f), new Vector3(size * 0.3f, size * 0.015f, size * 0.3f));
            Piece(site, Sphere, flMould, at + new Vector3(size * 0.5f, size * 0.045f, 0f), new Vector3(size * 0.1f, size * 0.025f, size * 0.1f));
            Piece(site, Sphere, flMould, at + new Vector3(size * 0.56f, size * 0.04f, size * 0.05f), new Vector3(size * 0.05f, size * 0.02f, size * 0.05f));
        }

        if (city.buildings.Contains(BuildingKind.ManhattanProject))
        {
            // Proyecto Manhattan: gran edificio de hormigón con cúpula, un átomo gigante con tres órbitas de electrones brillantes
            // sobre el tejado y torres de vigilancia
            Material mhConcrete = LitMaterial(new Color(0.65f, 0.66f, 0.68f));
            Material mhDark = LitMaterial(new Color(0.25f, 0.26f, 0.3f));
            Material mhCore = LitMaterial(new Color(1f, 0.85f, 0.3f));
            Material mhElectron = LitMaterial(new Color(0.4f, 0.9f, 1f));
            foreach (Material m in new[] { mhCore, mhElectron })
            {
                m.EnableKeyword("_EMISSION");
                if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", m.color * 1.6f);
            }
            Vector3 at = new Vector3(-size * 0.6f, 0f, -size * 1.7f); // si no hay celdilla libre, fuera del disco de la ciudad
            Transform site = WonderSite(city, BuildingKind.ManhattanProject, root.transform, grid, earth, ref at);

            Piece(site, Cube, mhConcrete, at + new Vector3(0f, size * 0.06f, 0f), new Vector3(size * 0.9f, size * 0.12f, size * 0.7f));
            Piece(site, Cube, civMat, at + new Vector3(0f, size * 0.13f, 0f), new Vector3(size * 0.92f, size * 0.02f, size * 0.72f));
            Piece(site, Cylinder, mhConcrete, at + new Vector3(0f, size * 0.22f, 0f), new Vector3(size * 0.45f, size * 0.1f, size * 0.45f));
            Piece(site, Sphere, mhDark, at + new Vector3(0f, size * 0.32f, 0f), new Vector3(size * 0.42f, size * 0.2f, size * 0.42f));

            // Átomo: núcleo y tres órbitas inclinadas con cuatro electrones cada una
            Vector3 center = at + new Vector3(0f, size * 0.72f, 0f);
            Piece(site, Sphere, mhCore, center, Vector3.one * size * 0.14f);
            for (int orbit = 0; orbit < 3; orbit++)
            {
                Quaternion rot = Quaternion.Euler(orbit * 60f, orbit * 60f, 0f);
                for (int i = 0; i < 16; i++)
                {
                    float a = i / 16f * Mathf.PI * 2f;
                    Vector3 p = center + rot * new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * size * 0.3f;
                    Piece(site, Sphere, i % 4 == 0 ? mhElectron : mhDark, p, Vector3.one * size * (i % 4 == 0 ? 0.06f : 0.025f));
                }
            }

            // Torres de vigilancia en las esquinas
            for (int sx = -1; sx <= 1; sx += 2)
            {
                Vector3 t = at + new Vector3(sx * size * 0.5f, 0f, size * 0.4f);
                Piece(site, Cylinder, mhDark, t + new Vector3(0f, size * 0.2f, 0f), new Vector3(size * 0.03f, size * 0.2f, size * 0.03f));
                Piece(site, Cube, mhConcrete, t + new Vector3(0f, size * 0.42f, 0f), new Vector3(size * 0.1f, size * 0.06f, size * 0.1f));
            }
        }

        if (city.buildings.Contains(BuildingKind.Internet))
        {
            // Internet: centro de datos con filas de servidores iluminados, un globo terráqueo con nodos conectados
            // y una antena de satélite
            Material inWall = LitMaterial(new Color(0.85f, 0.86f, 0.9f));
            Material inGlass = LitMaterial(new Color(0.2f, 0.35f, 0.5f));
            Material inDark = LitMaterial(new Color(0.12f, 0.12f, 0.15f));
            Material inGlobe = LitMaterial(new Color(0.15f, 0.4f, 0.75f));
            Material inNode = LitMaterial(new Color(0.9f, 1f, 0.4f));
            Material inLed = LitMaterial(new Color(0.3f, 1f, 0.5f));
            foreach (Material m in new[] { inNode, inLed })
            {
                m.EnableKeyword("_EMISSION");
                if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", m.color * 1.6f);
            }
            Vector3 at = new Vector3(size * 1.4f, 0f, -size * 1.2f); // si no hay celdilla libre, fuera del disco de la ciudad
            Transform site = WonderSite(city, BuildingKind.Internet, root.transform, grid, earth, ref at);

            // Centro de datos con racks luminosos
            Piece(site, Cube, inWall, at + new Vector3(-size * 0.3f, size * 0.1f, 0f), new Vector3(size * 0.45f, size * 0.2f, size * 0.55f));
            Piece(site, Cube, civMat, at + new Vector3(-size * 0.3f, size * 0.215f, 0f), new Vector3(size * 0.47f, size * 0.03f, size * 0.57f));
            for (int i = 0; i < 5; i++)
            {
                float z = (i - 2) * size * 0.1f;
                Piece(site, Cube, inGlass, at + new Vector3(-size * 0.075f, size * 0.1f, z), new Vector3(size * 0.012f, size * 0.14f, size * 0.07f));
                Piece(site, Cube, inLed, at + new Vector3(-size * 0.066f, size * 0.12f, z), new Vector3(size * 0.006f, size * 0.02f, size * 0.04f));
            }

            // Globo terráqueo sobre un pedestal, con un anillo ecuatorial de nodos y varios nodos en la superficie
            Vector3 g = at + new Vector3(size * 0.28f, size * 0.38f, 0f);
            Piece(site, Cylinder, inDark, at + new Vector3(size * 0.28f, size * 0.1f, 0f), new Vector3(size * 0.06f, size * 0.1f, size * 0.06f));
            Piece(site, Sphere, inGlobe, g, Vector3.one * size * 0.36f);
            for (int ring = 0; ring < 2; ring++)
            {
                Quaternion rot = Quaternion.Euler(ring * 70f, ring * 40f, 0f);
                for (int i = 0; i < 14; i++)
                {
                    float a = i / 14f * Mathf.PI * 2f;
                    Vector3 p = g + rot * new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * size * 0.2f;
                    Piece(site, Sphere, i % 3 == 0 ? inNode : inDark, p, Vector3.one * size * (i % 3 == 0 ? 0.045f : 0.02f));
                }
            }

            // Antena de satélite
            Piece(site, Cylinder, inDark, at + new Vector3(-size * 0.3f, size * 0.32f, size * 0.18f), new Vector3(size * 0.015f, size * 0.1f, size * 0.015f));
            GameObject dish = Piece(site, Sphere, inWall, at + new Vector3(-size * 0.3f, size * 0.44f, size * 0.18f), new Vector3(size * 0.18f, size * 0.035f, size * 0.18f));
            dish.transform.localRotation = Quaternion.Euler(0f, 0f, -45f);
        }

        if (city.buildings.Contains(BuildingKind.FibonacciLab))
        {
            // Laboratorio de Fibonacci: base de piedra y una espiral de cilindros cada vez más grandes (proporción áurea)
            Material labBase = LitMaterial(new Color(0.7f, 0.68f, 0.62f));
            Vector3 at = new Vector3(0f, 0f, size * 1.5f); // si no hay celdilla libre, fuera del disco de la ciudad
            Transform site = WonderSite(city, BuildingKind.FibonacciLab, root.transform, grid, earth, ref at);

            Piece(site, Cube, labBase, at + new Vector3(0f, size * 0.03f, 0f), new Vector3(size * 0.8f, size * 0.06f, size * 0.5f));

            float[] fib = { 0.06f, 0.06f, 0.12f, 0.18f, 0.3f };
            float x = -size * 0.3f;
            for (int i = 0; i < fib.Length; i++)
            {
                float s = size * fib[i];
                x += s * 0.5f;
                Piece(site, Cylinder, i % 2 == 0 ? civMat : labBase, at + new Vector3(x, size * 0.06f + s * 0.5f, 0f), new Vector3(s, s * 0.5f, s));
                x += s * 0.5f;
            }
        }

        if (city.buildings.Contains(BuildingKind.Walls))
        {
            Material stone =LitMaterial(new Color(0.55f, 0.52f, 0.48f));
            for (int i = 0; i < 8; i++)
            {
                float a = i / 8f * Mathf.PI * 2f;
                Piece(root.transform, Cube, stone,
                    new Vector3(Mathf.Cos(a), 0.12f, Mathf.Sin(a)) * size * 0.95f,
                    new Vector3(size * 0.4f, size * 0.25f, size * 0.15f),
                    -a * Mathf.Rad2Deg + 90f);
            }
        }

        city.selectionRing = Ring(root.transform, size * 3f);
    }

    // Casa de piedra medieval: muros de piedra con zócalo y esquinas, tejado de tejas a dos aguas, puerta de madera,
    // una ventana y a veces una chimenea. La puerta mira a +Z local.
    static void BuildStoneHouse(Transform parent, Vector3 pos, float r, Material stone, Material stoneDark, Material roof,
                                Material wood, Material dark, float yaw, System.Random rng)
    {
        GameObject house = new GameObject("Casa de piedra");
        house.transform.SetParent(parent, false);
        house.transform.localPosition = pos;
        house.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        Transform t = house.transform;

        float w = r * 2.0f;                                       // ancho (eje X)
        float d = r * 1.6f;                                       // fondo (eje Z)
        float wh = r * (0.95f + (float)rng.NextDouble() * 0.3f);  // altura de los muros

        // Zócalo y muros
        Piece(t, Cube, stoneDark, new Vector3(0f, wh * 0.08f, 0f), new Vector3(w * 1.06f, wh * 0.16f, d * 1.06f));
        Piece(t, Cube, stone, new Vector3(0f, wh * 0.5f, 0f), new Vector3(w, wh, d));

        // Esquinas de sillares más oscuros
        for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2)
                Piece(t, Cube, stoneDark, new Vector3(sx * w * 0.5f, wh * 0.5f, sz * d * 0.5f), new Vector3(w * 0.1f, wh * 1.02f, d * 0.1f));

        // Hiladas de piedra: franjas ligeramente salientes que dan aspecto de muro
        for (int row = 1; row <= 2; row++)
        {
            float y = wh * (0.2f + row * 0.25f);
            Piece(t, Cube, stoneDark, new Vector3(0f, y, 0f), new Vector3(w * 1.015f, wh * 0.025f, d * 1.015f));
        }

        // Tejado a dos aguas: dos losas inclinadas que se juntan en la cumbre (cumbrera a lo largo de X)
        float angle = 35f;
        float rise = (d * 0.25f) * Mathf.Tan(angle * Mathf.Deg2Rad);
        float slab = (d * 0.5f) / Mathf.Cos(angle * Mathf.Deg2Rad) + r * 0.12f;
        for (int side = -1; side <= 1; side += 2)
        {
            GameObject s = Piece(t, Cube, roof, new Vector3(0f, wh + rise + r * 0.02f, side * d * 0.25f), new Vector3(w * 1.2f, r * 0.07f, slab));
            s.transform.localRotation = Quaternion.Euler(side * angle, 0f, 0f);
        }
        Piece(t, Cube, stoneDark, new Vector3(0f, wh + rise * 2f + r * 0.02f, 0f), new Vector3(w * 1.22f, r * 0.06f, r * 0.12f)); // cumbrera

        // Frontones de piedra bajo las aguas del tejado
        for (int sx = -1; sx <= 1; sx += 2)
            Piece(t, Cube, stone, new Vector3(sx * w * 0.47f, wh + rise * 0.5f, 0f), new Vector3(w * 0.06f, rise, d * 0.5f));

        // Puerta de madera con marco, en la fachada +Z
        Piece(t, Cube, wood, new Vector3(0f, wh * 0.36f, d * 0.5f), new Vector3(w * 0.26f, wh * 0.72f, r * 0.08f));
        Piece(t, Cube, dark, new Vector3(0f, wh * 0.34f, d * 0.5f + r * 0.02f), new Vector3(w * 0.18f, wh * 0.62f, r * 0.06f));

        // Ventana con marco de madera
        Piece(t, Cube, wood, new Vector3(w * 0.3f, wh * 0.62f, d * 0.5f), new Vector3(w * 0.2f, wh * 0.26f, r * 0.07f));
        Piece(t, Cube, dark, new Vector3(w * 0.3f, wh * 0.62f, d * 0.5f + r * 0.02f), new Vector3(w * 0.14f, wh * 0.2f, r * 0.06f));

        // Chimenea de piedra en la mitad de las casas
        if (rng.NextDouble() < 0.5)
            Piece(t, Cube, stoneDark, new Vector3(-w * 0.3f, wh + rise * 1.4f, 0f), new Vector3(r * 0.2f, rise * 1.3f + r * 0.2f, r * 0.2f));
    }

    // Rascacielos actual: torre de cristal sobre un zócalo de hormigón, franjas de ventanas que rodean las cuatro fachadas,
    // un escalón superior en algunos, remate con el color de la civilización y antena. La entrada mira a +Z local.
    static void BuildSkyscraper(Transform parent, Vector3 pos, float r, Material glass, Material glassLight, Material concrete,
                                Material metal, Material civ, float yaw, System.Random rng)
    {
        GameObject tower = new GameObject("Rascacielos");
        tower.transform.SetParent(parent, false);
        tower.transform.localPosition = pos;
        tower.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        Transform t = tower.transform;

        float w = r * (1.2f + (float)rng.NextDouble() * 0.3f);   // ancho (eje X)
        float d = r * (1.1f + (float)rng.NextDouble() * 0.25f);  // fondo (eje Z)
        float h = r * (3.2f + (float)rng.NextDouble() * 3.8f);   // altura total

        // Zócalo de hormigón, puerta de cristal y cuerpo de la torre
        float baseH = r * 0.35f;
        Piece(t, Cube, concrete, new Vector3(0f, baseH * 0.5f, 0f), new Vector3(w * 1.15f, baseH, d * 1.15f));
        Piece(t, Cube, glassLight, new Vector3(0f, baseH * 0.45f, d * 0.58f), new Vector3(w * 0.4f, baseH * 0.7f, r * 0.04f));
        float bodyH = h - baseH;
        Piece(t, Cube, glass, new Vector3(0f, baseH + bodyH * 0.5f, 0f), new Vector3(w, bodyH, d));

        // Franjas de ventanas claras que dan la vuelta a toda la torre
        int rows = Mathf.Clamp(Mathf.RoundToInt(bodyH / (r * 0.45f)), 4, 14);
        for (int i = 0; i < rows; i++)
        {
            float y = baseH + bodyH * (i + 0.6f) / (rows + 0.2f);
            Piece(t, Cube, glassLight, new Vector3(0f, y, 0f), new Vector3(w * 1.02f, bodyH / (rows * 2.6f), d * 1.02f));
        }

        // Cuatro pilares de hormigón en las esquinas
        for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2)
                Piece(t, Cube, concrete, new Vector3(sx * w * 0.5f, baseH + bodyH * 0.5f, sz * d * 0.5f), new Vector3(w * 0.07f, bodyH, d * 0.07f));

        // Algunos tienen un escalón superior más estrecho
        float topY = h;
        if (rng.NextDouble() < 0.5)
        {
            float h2 = r * (0.8f + (float)rng.NextDouble() * 0.8f);
            Piece(t, Cube, glass, new Vector3(0f, topY + h2 * 0.5f, 0f), new Vector3(w * 0.65f, h2, d * 0.65f));
            Piece(t, Cube, glassLight, new Vector3(0f, topY + h2 * 0.55f, 0f), new Vector3(w * 0.67f, h2 * 0.12f, d * 0.67f));
            topY += h2;
        }

        // Remate con el color de la civilización y antena
        Piece(t, Cube, civ, new Vector3(0f, topY + r * 0.04f, 0f), new Vector3(w * 1.04f, r * 0.08f, d * 1.04f));
        Piece(t, Cube, metal, new Vector3(w * 0.2f, topY + r * 0.15f, -d * 0.15f), new Vector3(w * 0.22f, r * 0.12f, d * 0.22f)); // maquinaria
        if (rng.NextDouble() < 0.7)
        {
            Piece(t, Cylinder, metal, new Vector3(0f, topY + r * 0.5f, 0f), new Vector3(r * 0.025f, r * 0.45f, r * 0.025f));
            Piece(t, Sphere, civ, new Vector3(0f, topY + r * 0.98f, 0f), Vector3.one * r * 0.06f);
        }
    }

    // Torre futurista: cristal oscuro recorrido por neones. Tipo 0 = obelisco escalonado con bandas de neón en cada nivel;
    // tipo 1 = torre cilíndrica con anillos luminosos. Algunas llevan un anillo flotante; todas, una aguja de luz y un remate
    // del color de la civilización.
    static void BuildFuturisticTower(Transform parent, Vector3 pos, float r, Material glass, Material[] neons, Material civGlow,
                                     float yaw, System.Random rng)
    {
        GameObject tower = new GameObject("Torre futurista");
        tower.transform.SetParent(parent, false);
        tower.transform.localPosition = pos;
        tower.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        Transform t = tower.transform;

        Material edge = neons[rng.Next(neons.Length)];
        Material band = neons[rng.Next(neons.Length)];
        float w = r * (1.0f + (float)rng.NextDouble() * 0.4f);
        float h = r * (3.5f + (float)rng.NextDouble() * 4f);
        float top;

        if (rng.NextDouble() < 0.5)
        {
            // Obelisco escalonado de tres niveles
            float[] hf = { 0.55f, 0.3f, 0.15f };
            float[] wf = { 1f, 0.72f, 0.46f };
            float y = 0f;
            for (int i = 0; i < 3; i++)
            {
                float th = h * hf[i], tw = w * wf[i];
                Piece(t, Cube, glass, new Vector3(0f, y + th * 0.5f, 0f), new Vector3(tw, th, tw));
                for (int sx = -1; sx <= 1; sx += 2)
                    for (int sz = -1; sz <= 1; sz += 2)
                        Piece(t, Cube, edge, new Vector3(sx * tw * 0.5f, y + th * 0.5f, sz * tw * 0.5f), new Vector3(tw * 0.05f, th, tw * 0.05f));
                y += th;
                Piece(t, Cube, band, new Vector3(0f, y, 0f), new Vector3(tw * 1.08f, r * 0.04f, tw * 1.08f));
            }
            top = y;
        }
        else
        {
            // Torre cilíndrica con anillos luminosos
            Piece(t, Cylinder, glass, new Vector3(0f, h * 0.5f, 0f), new Vector3(w, h * 0.5f, w));
            int rings = Mathf.Clamp(Mathf.RoundToInt(h / (r * 0.7f)), 4, 10);
            for (int i = 1; i <= rings; i++)
                Piece(t, Cylinder, i % 2 == 0 ? band : edge, new Vector3(0f, h * i / (rings + 0.5f), 0f), new Vector3(w * 1.04f, r * 0.02f, w * 1.04f));
            Piece(t, Sphere, band, new Vector3(0f, h, 0f), new Vector3(w * 0.7f, w * 0.35f, w * 0.7f));
            top = h;
        }

        // Aguja de luz con remate del color de la civilización, y a veces un anillo flotante
        Piece(t, Cylinder, edge, new Vector3(0f, top + r * 0.5f, 0f), new Vector3(r * 0.03f, r * 0.5f, r * 0.03f));
        Piece(t, Sphere, civGlow, new Vector3(0f, top + r * 1.02f, 0f), Vector3.one * r * 0.1f);
        if (rng.NextDouble() < 0.5)
            Piece(t, Cylinder, band, new Vector3(0f, top + r * 0.35f, 0f), new Vector3(w * 1.25f, r * 0.012f, w * 1.25f));
    }

    // Choza paleolítica: cúpula de pieles sobre un armazón de ramas, con remiendos, entrada oscura
    // y huesos de mamut cruzados en lo alto
    static void BuildHut(Transform parent, Vector3 pos, float r, Material hide, Material hideDark, Material bone, Material dark,
                         float yaw, System.Random rng)
    {
        GameObject hut = new GameObject("Choza");
        hut.transform.SetParent(parent, false);
        hut.transform.localPosition = pos;
        hut.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        Transform t = hut.transform;

        float h = r * (1.05f + (float)rng.NextDouble() * 0.35f);

        // Cúpula de pieles (la mitad inferior queda bajo el suelo) y aro de base
        Piece(t, Sphere, hide, new Vector3(0f, 0f, 0f), new Vector3(r * 2f, h * 2f, r * 2f));
        Piece(t, Cylinder, hideDark, new Vector3(0f, r * 0.03f, 0f), new Vector3(r * 2.05f, r * 0.03f, r * 2.05f));

        // Remiendos de piel más oscura
        for (int i = 0; i < 3; i++)
        {
            float a = (float)rng.NextDouble() * Mathf.PI * 2f;
            float elev = 0.3f + (float)rng.NextDouble() * 0.5f;
            Vector3 n = new Vector3(Mathf.Cos(a) * Mathf.Cos(elev), Mathf.Sin(elev), Mathf.Sin(a) * Mathf.Cos(elev));
            Piece(t, Sphere, hideDark, new Vector3(n.x * r * 0.96f, n.y * h * 0.96f, n.z * r * 0.96f), new Vector3(r * 0.4f, h * 0.22f, r * 0.4f));
        }

        // Entrada baja y oscura, mirando a +Z local
        Piece(t, Sphere, dark, new Vector3(0f, h * 0.22f, r * 0.9f), new Vector3(r * 0.5f, h * 0.45f, r * 0.3f));

        // Dos huesos cruzados en la cumbre
        for (int s = -1; s <= 1; s += 2)
        {
            GameObject b = Piece(t, Cylinder, bone, new Vector3(0f, h * 1.02f, 0f), new Vector3(r * 0.07f, h * 0.45f, r * 0.07f));
            b.transform.localRotation = Quaternion.Euler(0f, 0f, s * 22f);
        }
    }

    // ---------------------------------------------------------------- Unidades

    public static void BuildUnit(Unit unit, WorldGrid grid, Transform earth)
    {
        if (unit.view != null) Object.Destroy(unit.view);

        float size = grid.spacing * 0.35f;
        Material civMat = LitMaterial(unit.owner.color);
        Material dark = LitMaterial(new Color(0.2f, 0.2f, 0.22f));
        Material metal = LitMaterial(new Color(0.75f, 0.78f, 0.82f));

        GameObject root = Root(unit.def.name, earth, grid.pos[unit.cell], grid.dir[unit.cell]);
        unit.view = root;

        // Tras el Cambio de era (Edad Media) las unidades usan su modelo medieval
        // y tras el tercero (Edad Contemporánea) su modelo actual
        // y tras el cuarto (Edad Futura) su modelo futurista
        if (unit.owner.Has(TechKind.EraChange4))
            BuildFuturisticUnit(root.transform, size, unit);
        else if (unit.owner.Has(TechKind.EraChange3))
            BuildContemporaryUnit(root.transform, size, unit);
        else if (unit.owner.Has(TechKind.EraChange))
            BuildMedievalUnit(root.transform, size, unit);
        else
        switch (unit.kind)
        {
            case UnitKind.Settler:
                BuildSettlerModel(root.transform, size, unit.owner.color);
                break;

            case UnitKind.Worker:
                BuildWorkerModel(root.transform, size, unit.owner.color);
                break;

            case UnitKind.Spearman:
                BuildSoldierModel(root.transform, size, unit.owner.color, true);
                break;

            case UnitKind.Catapult:
            {
                // Catapulta: bastidor de madera sobre dos ruedas, brazo con cuchara cargada de piedra y bandera de la civilización
                Material catWood = LitMaterial(new Color(0.5f, 0.34f, 0.18f));
                Material catDark = LitMaterial(new Color(0.3f, 0.2f, 0.1f));
                Material catStone = LitMaterial(new Color(0.5f, 0.5f, 0.52f));

                Piece(root.transform, Cube, catWood, new Vector3(0f, size * 0.25f, 0f), new Vector3(size * 0.5f, size * 0.07f, size * 0.9f));
                Piece(root.transform, Cube, catDark, new Vector3(-size * 0.2f, size * 0.42f, 0f), new Vector3(size * 0.06f, size * 0.35f, size * 0.08f));
                Piece(root.transform, Cube, catDark, new Vector3(size * 0.2f, size * 0.42f, 0f), new Vector3(size * 0.06f, size * 0.35f, size * 0.08f));
                for (int sx = -1; sx <= 1; sx += 2)
                {
                    GameObject wheel = Piece(root.transform, Cylinder, catDark, new Vector3(sx * size * 0.3f, size * 0.2f, 0f), new Vector3(size * 0.4f, size * 0.025f, size * 0.4f));
                    wheel.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                }

                GameObject arm = Piece(root.transform, Cylinder, catWood, new Vector3(0f, size * 0.62f, -size * 0.18f), new Vector3(size * 0.05f, size * 0.42f, size * 0.05f));
                arm.transform.localRotation = Quaternion.Euler(-40f, 0f, 0f);
                Piece(root.transform, Sphere, catDark, new Vector3(0f, size * 0.85f, -size * 0.38f), new Vector3(size * 0.17f, size * 0.08f, size * 0.17f));
                Piece(root.transform, Sphere, catStone, new Vector3(0f, size * 0.92f, -size * 0.38f), new Vector3(size * 0.12f, size * 0.12f, size * 0.12f));
                Piece(root.transform, Cube, civMat, new Vector3(0f, size * 0.62f, size * 0.38f), new Vector3(size * 0.02f, size * 0.2f, size * 0.14f));
                break;
            }

            case UnitKind.Legion:
            {
                // Legionario: soldado con espada, gran escudo rectangular rojo con el color de la civilización y cimera en el casco
                BuildSoldierModel(root.transform, size, unit.owner.color, false);
                Material scutum = LitMaterial(new Color(0.65f, 0.12f, 0.1f));
                Material ironMat = LitMaterial(new Color(0.55f, 0.57f, 0.6f));
                Piece(root.transform, Cube, scutum, new Vector3(-size * 0.2f, size * 0.45f, size * 0.15f), new Vector3(size * 0.3f, size * 0.5f, size * 0.04f));
                Piece(root.transform, Cube, civMat, new Vector3(-size * 0.2f, size * 0.45f, size * 0.175f), new Vector3(size * 0.12f, size * 0.2f, size * 0.02f));
                Piece(root.transform, Cube, ironMat, new Vector3(size * 0.22f, size * 0.4f, size * 0.1f), new Vector3(size * 0.04f, size * 0.04f, size * 0.38f));
                Piece(root.transform, Cube, scutum, new Vector3(0f, size * 0.98f, 0f), new Vector3(size * 0.04f, size * 0.12f, size * 0.26f));
                break;
            }

            case UnitKind.Phalanx:
            {
                // Soldado con lanza y un gran escudo redondo de bronce con el color de la civilización en el centro
                BuildSoldierModel(root.transform, size, unit.owner.color, true);
                Material bronzeShield = LitMaterial(new Color(0.72f, 0.5f, 0.2f));
                GameObject shield = Piece(root.transform, Cylinder, bronzeShield, new Vector3(-size * 0.2f, size * 0.5f, size * 0.15f), new Vector3(size * 0.4f, size * 0.025f, size * 0.4f));
                shield.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                GameObject boss = Piece(root.transform, Cylinder, civMat, new Vector3(-size * 0.2f, size * 0.5f, size * 0.17f), new Vector3(size * 0.14f, size * 0.02f, size * 0.14f));
                boss.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                break;
            }

            case UnitKind.Crossbowman:
                BuildCrossbowman(root.transform, size, unit.owner.color);
                break;

            case UnitKind.Ballista:
                BuildBallista(root.transform, size, unit.owner.color);
                break;

            case UnitKind.Sailboat:
                BuildSailboatModel(root.transform, size, unit.owner.color);
                break;

            case UnitKind.Horseman:
            {
                // Caballo con un soldado a lomos
                BuildHorseModel(root.transform, size, 0f);

                GameObject rider = new GameObject("Jinete");
                rider.transform.SetParent(root.transform, false);
                rider.transform.localPosition = new Vector3(0f, size * 0.5f, -size * 0.05f);
                rider.transform.localScale = Vector3.one * 0.75f;
                BuildSoldierModel(rider.transform, size, unit.owner.color, false);
                break;
            }

            case UnitKind.Chariot:
            {
                // Carro de guerra: caballo delante, plataforma con dos ruedas y un soldado en pie
                Material wood = LitMaterial(new Color(0.5f, 0.34f, 0.18f));
                Material woodDark = LitMaterial(new Color(0.3f, 0.2f, 0.1f));
                BuildHorseModel(root.transform, size, size * 0.75f);

                Piece(root.transform, Cube, wood, new Vector3(0f, size * 0.3f, -size * 0.3f), new Vector3(size * 0.55f, size * 0.06f, size * 0.6f));
                Piece(root.transform, Cube, woodDark, new Vector3(0f, size * 0.42f, -size * 0.56f), new Vector3(size * 0.55f, size * 0.2f, size * 0.05f));
                Piece(root.transform, Cube, woodDark, new Vector3(0f, size * 0.38f, size * 0.25f), new Vector3(size * 0.06f, size * 0.05f, size * 0.9f)); // lanza de tiro
                for (int sx = -1; sx <= 1; sx += 2)
                {
                    GameObject wheel = Piece(root.transform, Cylinder, woodDark, new Vector3(sx * size * 0.32f, size * 0.22f, -size * 0.3f), new Vector3(size * 0.44f, size * 0.025f, size * 0.44f));
                    wheel.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                }

                GameObject driver = new GameObject("Auriga");
                driver.transform.SetParent(root.transform, false);
                driver.transform.localPosition = new Vector3(0f, size * 0.33f, -size * 0.3f);
                driver.transform.localScale = Vector3.one * 0.7f;
                BuildSoldierModel(driver.transform, size, unit.owner.color, false);
                break;
            }

            case UnitKind.Archer:
                BuildSoldierModel(root.transform, size, unit.owner.color, false);
                // Arco de madera en la mano y cuerda tensada
                Piece(root.transform, Cylinder, LitMaterial(new Color(0.42f, 0.28f, 0.15f)), new Vector3(size * 0.28f, size * 0.5f, size * 0.12f), new Vector3(size * 0.03f, size * 0.3f, size * 0.03f));
                Piece(root.transform, Cube, LitMaterial(new Color(0.9f, 0.88f, 0.8f)), new Vector3(size * 0.28f, size * 0.5f, size * 0.07f), new Vector3(size * 0.01f, size * 0.55f, size * 0.01f));
                break;

            case UnitKind.Caravan:
            case UnitKind.Merchant:
                BuildCaravanModel(root.transform, size, unit.owner.color);
                break;

            default:
                BuildSoldierModel(root.transform, size, unit.owner.color, false);
                break;
        }

        unit.selectionRing = Ring(root.transform, size * 1.9f);
    }

    // Caravana: carro de madera con toldo del color de la civilización, dos ruedas y la lanza de tiro. Mira hacia +Z.
    static void BuildCaravanModel(Transform root, float size, Color civColor)
    {
        Material wood = LitMaterial(new Color(0.42f, 0.28f, 0.15f));
        Material woodLight = LitMaterial(new Color(0.62f, 0.47f, 0.28f));
        Material canvas = LitMaterial(civColor);
        Material dark = LitMaterial(new Color(0.2f, 0.2f, 0.22f));

        Piece(root, Cube, woodLight, new Vector3(0f, size * 0.3f, 0f), new Vector3(size * 0.55f, size * 0.12f, size * 0.9f));      // plataforma
        Piece(root, Cube, canvas, new Vector3(0f, size * 0.58f, size * -0.05f), new Vector3(size * 0.5f, size * 0.42f, size * 0.7f)); // toldo
        Piece(root, Cube, wood, new Vector3(0f, size * 0.3f, size * 0.85f), new Vector3(size * 0.06f, size * 0.06f, size * 0.8f));  // lanza de tiro

        foreach (float s in new[] { -1f, 1f })
        {
            GameObject wheel = Piece(root, Cylinder, dark, new Vector3(s * size * 0.32f, size * 0.17f, size * -0.05f), new Vector3(size * 0.34f, size * 0.03f, size * 0.34f));
            wheel.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        }
    }

    // Barco de vela: casco con quilla y proa afilada, mástil, botavara, bauprés, vela mayor de lona,
    // foque y bandera del color de la civilización, cabina y timón. Mira hacia +Z.
    static Mesh shipHull, shipMainSail, shipJib;

    // Cuadrilátero visible por las dos caras (cada cara con vértices propios para que la luz sea plana)
    static void AddQuad(List<Vector3> v, List<int> t, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
    {
        int i = v.Count;
        v.Add(a); v.Add(b); v.Add(c); v.Add(d);
        t.Add(i); t.Add(i + 1); t.Add(i + 2); t.Add(i); t.Add(i + 2); t.Add(i + 3);

        int j = v.Count;
        v.Add(a); v.Add(b); v.Add(c); v.Add(d);
        t.Add(j); t.Add(j + 2); t.Add(j + 1); t.Add(j); t.Add(j + 3); t.Add(j + 2);
    }

    static Mesh FinishMesh(List<Vector3> v, List<int> t)
    {
        Mesh m = new Mesh();
        m.SetVertices(v);
        m.SetTriangles(t, 0);
        m.RecalculateNormals();
        m.RecalculateBounds();
        return m;
    }

    // Casco en unidades de "size": eslora de -1 (popa) a 1,2 (proa), borda a 0,3 y quilla en V
    static Mesh ShipHull()
    {
        if (shipHull != null) return shipHull;

        float[] z    = { -1.0f, -0.4f, 0.3f, 0.8f, 1.2f };
        float[] half = { 0.30f, 0.38f, 0.34f, 0.18f, 0.0f };   // semimanga a la altura de la borda
        float[] top  = { 0.34f, 0.30f, 0.30f, 0.36f, 0.48f };  // la proa se eleva
        float[] bot  = { 0.04f, 0.0f, 0.0f, 0.05f, 0.30f };    // la quilla sube hacia la proa

        int n = z.Length;
        Vector3[] tl = new Vector3[n], tr = new Vector3[n], bl = new Vector3[n], br = new Vector3[n];
        for (int i = 0; i < n; i++)
        {
            float keel = half[i] * 0.35f;
            tl[i] = new Vector3(-half[i], top[i], z[i]);
            tr[i] = new Vector3(half[i], top[i], z[i]);
            bl[i] = new Vector3(-keel, bot[i], z[i]);
            br[i] = new Vector3(keel, bot[i], z[i]);
        }

        List<Vector3> v = new List<Vector3>();
        List<int> t = new List<int>();

        AddQuad(v, t, tl[0], tr[0], br[0], bl[0]); // espejo de popa
        for (int i = 0; i < n - 1; i++)
        {
            int k = i + 1;
            AddQuad(v, t, tl[i], tl[k], bl[k], bl[i]); // costado de babor
            AddQuad(v, t, tr[i], tr[k], br[k], br[i]); // costado de estribor
            AddQuad(v, t, bl[i], bl[k], br[k], br[i]); // fondo
            AddQuad(v, t, tl[i], tl[k], tr[k], tr[i]); // cubierta
        }

        return shipHull = FinishMesh(v, t);
    }

    // Vela triangular en el plano YZ: p0 = pie delantero, p1 = puño de escota, p2 = tope del palo
    static Mesh ShipSail(Vector3 p0, Vector3 p1, Vector3 p2, ref Mesh cache)
    {
        if (cache != null) return cache;

        List<Vector3> v = new List<Vector3>();
        List<int> t = new List<int>();
        AddQuad(v, t, p0, p1, p2, p2); // triángulo: el cuarto vértice repite el tercero
        return cache = FinishMesh(v, t);
    }

    // Caballo marrón (cuerpo, cuello, cabeza, crin, cuatro patas y cola), centrado en z = zOffset. Mira hacia +Z.
    static void BuildHorseModel(Transform root, float size, float zOffset)
    {
        Material horse = LitMaterial(new Color(0.45f, 0.28f, 0.14f));
        Material mane = LitMaterial(new Color(0.15f, 0.1f, 0.06f));
        float z = zOffset;

        Piece(root, Sphere, horse, new Vector3(0f, size * 0.38f, z), new Vector3(size * 0.3f, size * 0.3f, size * 0.75f));
        GameObject neck = Piece(root, Capsule, horse, new Vector3(0f, size * 0.6f, z + size * 0.38f), new Vector3(size * 0.14f, size * 0.2f, size * 0.14f));
        neck.transform.localRotation = Quaternion.Euler(35f, 0f, 0f);
        Piece(root, Sphere, horse, new Vector3(0f, size * 0.72f, z + size * 0.55f), new Vector3(size * 0.16f, size * 0.18f, size * 0.3f));
        Piece(root, Cube, mane, new Vector3(0f, size * 0.7f, z + size * 0.32f), new Vector3(size * 0.03f, size * 0.22f, size * 0.1f));
        Piece(root, Cube, mane, new Vector3(0f, size * 0.4f, z - size * 0.42f), new Vector3(size * 0.05f, size * 0.25f, size * 0.05f));
        for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2)
                Piece(root, Cylinder, horse, new Vector3(sx * size * 0.11f, size * 0.15f, z + sz * size * 0.27f), new Vector3(size * 0.06f, size * 0.15f, size * 0.06f));
    }

    static void BuildSailboatModel(Transform root, float size, Color civColor)
    {
        Material wood = LitMaterial(new Color(0.42f, 0.28f, 0.15f));
        Material woodLight = LitMaterial(new Color(0.62f, 0.47f, 0.28f));
        Material canvas = LitMaterial(new Color(0.93f, 0.9f, 0.8f));
        Material civ = LitMaterial(civColor);

        Vector3 one = Vector3.one * size;

        // Casco y cubierta
        Piece(root, ShipHull(), wood, Vector3.zero, one);
        Piece(root, Cube, woodLight, new Vector3(0f, size * 0.38f, size * -0.65f), new Vector3(size * 0.38f, size * 0.16f, size * 0.4f)); // cabina
        Piece(root, Cube, wood, new Vector3(0f, size * 0.2f, size * -1.04f), new Vector3(size * 0.04f, size * 0.32f, size * 0.16f));      // timón

        // Mástil, botavara y bauprés
        Piece(root, Cylinder, wood, new Vector3(0f, size * 1.02f, size * 0.1f), new Vector3(size * 0.05f, size * 0.72f, size * 0.05f));
        GameObject boom = Piece(root, Cylinder, wood, new Vector3(0f, size * 0.52f, size * -0.35f), new Vector3(size * 0.035f, size * 0.45f, size * 0.035f));
        boom.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        GameObject sprit = Piece(root, Cylinder, wood, new Vector3(0f, size * 0.52f, size * 1.2f), new Vector3(size * 0.03f, size * 0.35f, size * 0.03f));
        sprit.transform.localRotation = Quaternion.Euler(75f, 0f, 0f);

        // Vela mayor (lona) detrás del mástil y foque (color de la civilización) delante
        Piece(root, ShipSail(new Vector3(0f, 0.52f, 0.1f), new Vector3(0f, 0.52f, -0.8f), new Vector3(0f, 1.7f, 0.1f), ref shipMainSail), canvas, Vector3.zero, one);
        Piece(root, ShipSail(new Vector3(0f, 0.5f, 1.15f), new Vector3(0f, 0.5f, 0.15f), new Vector3(0f, 1.6f, 0.15f), ref shipJib), civ, Vector3.zero, one);

        // Bandera en lo alto del mástil
        Piece(root, Cube, civ, new Vector3(0f, size * 1.7f, size * -0.08f), new Vector3(size * 0.02f, size * 0.12f, size * 0.3f));
    }

    // Colono: viajero con sombrero de ala ancha, capa del color de la civilización,
    // mochila con el petate, bastón con farol y un saco de semillas. Mira hacia +Z.
    static void BuildSettlerModel(Transform root, float size, Color civColor)
    {
        Material cloth = LitMaterial(civColor);
        Material clothDark = LitMaterial(Color.Lerp(civColor, Color.black, 0.35f));
        Material skin = LitMaterial(new Color(0.92f, 0.74f, 0.58f));
        Material trousers = LitMaterial(new Color(0.36f, 0.27f, 0.2f));
        Material boots = LitMaterial(new Color(0.16f, 0.11f, 0.08f));
        Material leather = LitMaterial(new Color(0.55f, 0.38f, 0.22f));
        Material straw = LitMaterial(new Color(0.86f, 0.74f, 0.42f));
        Material wood = LitMaterial(new Color(0.42f, 0.28f, 0.15f));
        Material bedroll = LitMaterial(new Color(0.78f, 0.7f, 0.55f));
        Material glow = LitMaterial(new Color(1f, 0.85f, 0.35f));
        glow.EnableKeyword("_EMISSION");
        if (glow.HasProperty("_EmissionColor")) glow.SetColor("_EmissionColor", new Color(1f, 0.7f, 0.2f));

        float s = size;

        // Botas y piernas
        for (int side = -1; side <= 1; side += 2)
        {
            float x = side * s * 0.09f;
            Piece(root, Sphere, boots, new Vector3(x, s * 0.05f, s * 0.04f), new Vector3(s * 0.13f, s * 0.1f, s * 0.2f));
            Piece(root, Capsule, trousers, new Vector3(x, s * 0.24f, 0f), new Vector3(s * 0.11f, s * 0.15f, s * 0.11f));
        }

        // Túnica (cuerpo) y cinturón
        Piece(root, Capsule, cloth, new Vector3(0f, s * 0.5f, 0f), new Vector3(s * 0.34f, s * 0.2f, s * 0.26f));
        Piece(root, Cylinder, leather, new Vector3(0f, s * 0.45f, 0f), new Vector3(s * 0.36f, s * 0.02f, s * 0.28f));
        Piece(root, Cube, straw, new Vector3(0f, s * 0.45f, s * 0.14f), new Vector3(s * 0.06f, s * 0.05f, s * 0.02f));

        // Capa en forma de campana por la espalda y esclavina sobre los hombros
        Piece(root, Cube, clothDark, new Vector3(0f, s * 0.44f, -s * 0.15f), new Vector3(s * 0.36f, s * 0.42f, s * 0.05f));
        Piece(root, Cylinder, clothDark, new Vector3(0f, s * 0.64f, 0f), new Vector3(s * 0.4f, s * 0.03f, s * 0.32f));

        // Brazos
        Piece(root, Capsule, cloth, new Vector3(-s * 0.21f, s * 0.5f, s * 0.02f), new Vector3(s * 0.09f, s * 0.14f, s * 0.09f));
        GameObject rightArm = Piece(root, Capsule, cloth, new Vector3(s * 0.22f, s * 0.53f, s * 0.05f), new Vector3(s * 0.09f, s * 0.14f, s * 0.09f));
        rightArm.transform.localRotation = Quaternion.Euler(20f, 0f, -12f);
        Piece(root, Sphere, skin, new Vector3(s * 0.25f, s * 0.4f, s * 0.1f), Vector3.one * s * 0.08f);
        Piece(root, Sphere, skin, new Vector3(-s * 0.23f, s * 0.36f, s * 0.02f), Vector3.one * s * 0.08f);

        // Cabeza, nariz y sombrero de ala ancha con cinta
        Piece(root, Sphere, skin, new Vector3(0f, s * 0.78f, 0f), Vector3.one * s * 0.24f);
        Piece(root, Sphere, skin, new Vector3(0f, s * 0.76f, s * 0.12f), Vector3.one * s * 0.05f);
        Piece(root, Cylinder, straw, new Vector3(0f, s * 0.87f, 0f), new Vector3(s * 0.52f, s * 0.008f, s * 0.52f));
        Piece(root, Cylinder, straw, new Vector3(0f, s * 0.93f, 0f), new Vector3(s * 0.26f, s * 0.05f, s * 0.26f));
        Piece(root, Cylinder, cloth, new Vector3(0f, s * 0.89f, 0f), new Vector3(s * 0.27f, s * 0.015f, s * 0.27f));

        // Mochila con petate encima (rollo horizontal)
        Piece(root, Cube, leather, new Vector3(0f, s * 0.5f, -s * 0.24f), new Vector3(s * 0.28f, s * 0.32f, s * 0.14f));
        GameObject roll = Piece(root, Cylinder, bedroll, new Vector3(0f, s * 0.7f, -s * 0.24f), new Vector3(s * 0.11f, s * 0.17f, s * 0.11f));
        roll.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        Piece(root, Cube, wood, new Vector3(0f, s * 0.36f, -s * 0.24f), new Vector3(s * 0.12f, s * 0.05f, s * 0.05f));

        // Bastón de caminante con farol colgado
        GameObject staff = Piece(root, Cylinder, wood, new Vector3(s * 0.3f, s * 0.5f, s * 0.12f), new Vector3(s * 0.035f, s * 0.5f, s * 0.035f));
        staff.transform.localRotation = Quaternion.Euler(0f, 0f, -4f);
        Piece(root, Cube, wood, new Vector3(s * 0.34f, s * 0.9f, s * 0.12f), new Vector3(s * 0.12f, s * 0.025f, s * 0.025f));
        Piece(root, Cube, glow, new Vector3(s * 0.4f, s * 0.8f, s * 0.12f), new Vector3(s * 0.07f, s * 0.09f, s * 0.07f));

        // Saco de semillas al costado
        Piece(root, Sphere, bedroll, new Vector3(-s * 0.26f, s * 0.24f, -s * 0.02f), new Vector3(s * 0.16f, s * 0.2f, s * 0.16f));
        Piece(root, Cylinder, leather, new Vector3(-s * 0.26f, s * 0.35f, -s * 0.02f), new Vector3(s * 0.06f, s * 0.015f, s * 0.06f));
    }

    // Trabajador: túnica de piel con cinta del color de la civilización en la frente, cesta de mimbre a la
    // espalda y hacha de piedra al hombro. Mira hacia +Z.
    static void BuildWorkerModel(Transform root, float size, Color civColor)
    {
        Material cloth = LitMaterial(civColor);
        Material hide = LitMaterial(new Color(0.6f, 0.44f, 0.27f));
        Material hideDark = LitMaterial(new Color(0.4f, 0.28f, 0.17f));
        Material skin = LitMaterial(new Color(0.9f, 0.7f, 0.54f));
        Material hair = LitMaterial(new Color(0.18f, 0.12f, 0.08f));
        Material wood = LitMaterial(new Color(0.45f, 0.3f, 0.16f));
        Material stone = LitMaterial(new Color(0.55f, 0.55f, 0.58f));
        Material wicker = LitMaterial(new Color(0.78f, 0.6f, 0.33f));
        Material rope = LitMaterial(new Color(0.7f, 0.62f, 0.45f));

        float s = size;

        // Piernas descalzas y pies
        for (int side = -1; side <= 1; side += 2)
        {
            float x = side * s * 0.09f;
            Piece(root, Sphere, skin, new Vector3(x, s * 0.04f, s * 0.04f), new Vector3(s * 0.12f, s * 0.08f, s * 0.19f));
            Piece(root, Capsule, skin, new Vector3(x, s * 0.22f, 0f), new Vector3(s * 0.1f, s * 0.15f, s * 0.1f));
        }

        // Túnica de piel con borde irregular, y cinturón de cuerda
        Piece(root, Capsule, hide, new Vector3(0f, s * 0.5f, 0f), new Vector3(s * 0.34f, s * 0.2f, s * 0.26f));
        Piece(root, Cylinder, hideDark, new Vector3(0f, s * 0.36f, 0f), new Vector3(s * 0.36f, s * 0.02f, s * 0.28f));
        Piece(root, Cylinder, rope, new Vector3(0f, s * 0.46f, 0f), new Vector3(s * 0.35f, s * 0.015f, s * 0.27f));

        // Un solo hombro cubierto: el otro al descubierto
        Piece(root, Sphere, hideDark, new Vector3(-s * 0.19f, s * 0.68f, 0f), new Vector3(s * 0.16f, s * 0.1f, s * 0.16f));

        // Brazos
        Piece(root, Capsule, skin, new Vector3(-s * 0.21f, s * 0.5f, s * 0.02f), new Vector3(s * 0.08f, s * 0.14f, s * 0.08f));
        GameObject rightArm = Piece(root, Capsule, skin, new Vector3(s * 0.22f, s * 0.58f, s * 0.04f), new Vector3(s * 0.08f, s * 0.14f, s * 0.08f));
        rightArm.transform.localRotation = Quaternion.Euler(0f, 0f, -35f);

        // Cabeza, pelo largo y cinta
        Piece(root, Sphere, skin, new Vector3(0f, s * 0.8f, 0f), Vector3.one * s * 0.24f);
        Piece(root, Sphere, hair, new Vector3(0f, s * 0.84f, -s * 0.03f), new Vector3(s * 0.27f, s * 0.2f, s * 0.27f));
        Piece(root, Sphere, hair, new Vector3(0f, s * 0.7f, -s * 0.09f), new Vector3(s * 0.2f, s * 0.2f, s * 0.12f));
        Piece(root, Cylinder, cloth, new Vector3(0f, s * 0.83f, 0f), new Vector3(s * 0.25f, s * 0.015f, s * 0.25f));
        Piece(root, Sphere, skin, new Vector3(0f, s * 0.78f, s * 0.12f), Vector3.one * s * 0.045f);

        // Cesta de mimbre a la espalda con fardo de ramas
        Piece(root, Cylinder, wicker, new Vector3(0f, s * 0.5f, -s * 0.22f), new Vector3(s * 0.24f, s * 0.14f, s * 0.2f));
        for (int i = -1; i <= 1; i++)
        {
            GameObject branch = Piece(root, Cylinder, wood, new Vector3(i * s * 0.07f, s * 0.72f, -s * 0.22f), new Vector3(s * 0.025f, s * 0.14f, s * 0.025f));
            branch.transform.localRotation = Quaternion.Euler(0f, 0f, i * 14f);
        }
        Piece(root, Cylinder, rope, new Vector3(0f, s * 0.6f, -s * 0.15f), new Vector3(s * 0.26f, s * 0.01f, s * 0.2f));

        // Hacha de piedra al hombro: mango de madera, cabeza de piedra atada con cuerda
        GameObject handle = Piece(root, Cylinder, wood, new Vector3(s * 0.3f, s * 0.7f, s * 0.04f), new Vector3(s * 0.04f, s * 0.32f, s * 0.04f));
        handle.transform.localRotation = Quaternion.Euler(0f, 0f, -18f);
        GameObject head = Piece(root, Cube, stone, new Vector3(s * 0.38f, s * 1.0f, s * 0.04f), new Vector3(s * 0.05f, s * 0.14f, s * 0.2f));
        head.transform.localRotation = Quaternion.Euler(0f, 0f, -18f);
        GameObject tie = Piece(root, Cylinder, rope, new Vector3(s * 0.375f, s * 0.97f, s * 0.04f), new Vector3(s * 0.06f, s * 0.015f, s * 0.06f));
        tie.transform.localRotation = Quaternion.Euler(0f, 0f, -18f);
    }

    // Soldado: casco con cresta y penacho del color de la civilización, coraza, hombreras, faldilla de
    // tiras de cuero, escudo redondo con emblema y arma (espada o lanza). Mira hacia +Z.
    static void BuildSoldierModel(Transform root, float size, Color civColor, bool spear)
    {
        Material cloth = LitMaterial(civColor);
        Material clothDark = LitMaterial(Color.Lerp(civColor, Color.black, 0.4f));
        Material skin = LitMaterial(new Color(0.88f, 0.68f, 0.52f));
        Material bronze = LitMaterial(new Color(0.72f, 0.5f, 0.2f));
        Material steel = LitMaterial(new Color(0.78f, 0.8f, 0.85f));
        Material leather = LitMaterial(new Color(0.42f, 0.27f, 0.15f));
        Material boots = LitMaterial(new Color(0.18f, 0.12f, 0.09f));
        Material wood = LitMaterial(new Color(0.45f, 0.3f, 0.16f));
        Material gold = LitMaterial(new Color(0.95f, 0.8f, 0.3f));
        bronze.SetFloat("_Glossiness", 0.55f);
        steel.SetFloat("_Glossiness", 0.7f);
        gold.SetFloat("_Glossiness", 0.6f);

        float s = size;

        // Piernas con grebas de bronce y botas
        for (int side = -1; side <= 1; side += 2)
        {
            float x = side * s * 0.1f;
            Piece(root, Sphere, boots, new Vector3(x, s * 0.05f, s * 0.04f), new Vector3(s * 0.15f, s * 0.11f, s * 0.22f));
            Piece(root, Cylinder, bronze, new Vector3(x, s * 0.2f, s * 0.02f), new Vector3(s * 0.13f, s * 0.11f, s * 0.13f));
            Piece(root, Capsule, skin, new Vector3(x, s * 0.34f, 0f), new Vector3(s * 0.11f, s * 0.07f, s * 0.11f));
        }

        // Faldilla de tiras de cuero alrededor de la cintura
        for (int i = 0; i < 8; i++)
        {
            float a = i / 8f * Mathf.PI * 2f;
            Vector3 p = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * s * 0.17f;
            Piece(root, Cube, leather, new Vector3(p.x, s * 0.4f, p.z), new Vector3(s * 0.09f, s * 0.14f, s * 0.03f), a * Mathf.Rad2Deg);
        }

        // Coraza de bronce, cinturón y túnica del color de la civilización asomando por debajo
        Piece(root, Cylinder, clothDark, new Vector3(0f, s * 0.46f, 0f), new Vector3(s * 0.36f, s * 0.03f, s * 0.29f));
        Piece(root, Capsule, bronze, new Vector3(0f, s * 0.6f, 0f), new Vector3(s * 0.36f, s * 0.15f, s * 0.28f));
        Piece(root, Sphere, gold, new Vector3(0f, s * 0.62f, s * 0.14f), new Vector3(s * 0.08f, s * 0.08f, s * 0.03f));

        // Hombreras y brazos
        for (int side = -1; side <= 1; side += 2)
        {
            Piece(root, Sphere, bronze, new Vector3(side * s * 0.22f, s * 0.72f, 0f), new Vector3(s * 0.17f, s * 0.11f, s * 0.17f));
            Piece(root, Capsule, cloth, new Vector3(side * s * 0.25f, s * 0.58f, s * 0.02f), new Vector3(s * 0.09f, s * 0.13f, s * 0.09f));
        }

        // Cabeza y casco con protector de nariz, cresta y penacho
        Piece(root, Sphere, skin, new Vector3(0f, s * 0.86f, 0f), Vector3.one * s * 0.2f);
        Piece(root, Sphere, bronze, new Vector3(0f, s * 0.91f, 0f), new Vector3(s * 0.25f, s * 0.2f, s * 0.25f));
        Piece(root, Cube, bronze, new Vector3(0f, s * 0.87f, s * 0.115f), new Vector3(s * 0.035f, s * 0.11f, s * 0.02f));
        Piece(root, Cube, clothDark, new Vector3(0f, s * 1.03f, 0f), new Vector3(s * 0.03f, s * 0.07f, s * 0.24f));
        Piece(root, Capsule, cloth, new Vector3(0f, s * 1.08f, -s * 0.06f), new Vector3(s * 0.06f, s * 0.09f, s * 0.14f));

        // Escudo redondo en el brazo izquierdo, con borde, emblema y umbo central
        GameObject shield = Piece(root, Cylinder, cloth, new Vector3(-s * 0.36f, s * 0.58f, s * 0.05f), new Vector3(s * 0.4f, s * 0.02f, s * 0.4f));
        shield.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        GameObject rim = Piece(root, Cylinder, bronze, new Vector3(-s * 0.375f, s * 0.58f, s * 0.05f), new Vector3(s * 0.43f, s * 0.008f, s * 0.43f));
        rim.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        GameObject emblem = Piece(root, Cylinder, gold, new Vector3(-s * 0.385f, s * 0.58f, s * 0.05f), new Vector3(s * 0.2f, s * 0.008f, s * 0.2f));
        emblem.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        Piece(root, Sphere, steel, new Vector3(-s * 0.4f, s * 0.58f, s * 0.05f), Vector3.one * s * 0.09f);

        // Mano derecha y arma
        Piece(root, Sphere, skin, new Vector3(s * 0.3f, s * 0.46f, s * 0.1f), Vector3.one * s * 0.08f);

        if (spear)
        {
            // Lanza larga con punta de acero y banderín
            Piece(root, Cylinder, wood, new Vector3(s * 0.3f, s * 0.75f, s * 0.1f), new Vector3(s * 0.035f, s * 0.78f, s * 0.035f));
            GameObject tip = Piece(root, Cube, steel, new Vector3(s * 0.3f, s * 1.56f, s * 0.1f), new Vector3(s * 0.07f, s * 0.2f, s * 0.02f));
            tip.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
            Piece(root, Cube, cloth, new Vector3(s * 0.3f, s * 1.42f, s * 0.02f), new Vector3(s * 0.02f, s * 0.12f, s * 0.14f));
        }
        else
        {
            // Espada corta con empuñadura dorada, en guardia
            GameObject sword = Piece(root, Cube, steel, new Vector3(s * 0.33f, s * 0.75f, s * 0.2f), new Vector3(s * 0.05f, s * 0.5f, s * 0.015f));
            sword.transform.localRotation = Quaternion.Euler(25f, 0f, -8f);
            Piece(root, Cube, gold, new Vector3(s * 0.31f, s * 0.5f, s * 0.12f), new Vector3(s * 0.16f, s * 0.03f, s * 0.03f));
            Piece(root, Sphere, gold, new Vector3(s * 0.3f, s * 0.4f, s * 0.09f), Vector3.one * s * 0.05f);
        }
    }

    // ---------------------------------------------------------------- Unidades medievales
    // Tras el Cambio de era (Edad Media) cada unidad se dibuja con su versión medieval. Todas miran hacia +Z.

    // Piernas con calzas y botas (mismas proporciones que el soldado)
    static void MedLegs(Transform r, float s, Material hose, Material boots)
    {
        for (int side = -1; side <= 1; side += 2)
        {
            float x = side * s * 0.1f;
            Piece(r, Sphere, boots, new Vector3(x, s * 0.05f, s * 0.04f), new Vector3(s * 0.15f, s * 0.11f, s * 0.22f));
            Piece(r, Capsule, hose, new Vector3(x, s * 0.23f, 0f), new Vector3(s * 0.11f, s * 0.15f, s * 0.11f));
        }
    }

    // Torso con túnica (y falda opcional), cinturón y brazos
    static void MedTorso(Transform r, float s, Material tunic, Material belt, bool skirt)
    {
        Piece(r, Capsule, tunic, new Vector3(0f, s * 0.6f, 0f), new Vector3(s * 0.36f, s * 0.15f, s * 0.28f));
        if (skirt) Piece(r, Cylinder, tunic, new Vector3(0f, s * 0.38f, 0f), new Vector3(s * 0.4f, s * 0.1f, s * 0.32f));
        Piece(r, Cylinder, belt, new Vector3(0f, s * 0.47f, 0f), new Vector3(s * 0.38f, s * 0.02f, s * 0.3f));
        for (int side = -1; side <= 1; side += 2)
            Piece(r, Capsule, tunic, new Vector3(side * s * 0.25f, s * 0.58f, s * 0.02f), new Vector3(s * 0.09f, s * 0.13f, s * 0.09f));
    }

    static void MedHead(Transform r, float s, Material skin)
    {
        Piece(r, Sphere, skin, new Vector3(0f, s * 0.86f, 0f), Vector3.one * s * 0.2f);
        Piece(r, Sphere, skin, new Vector3(0f, s * 0.85f, s * 0.1f), Vector3.one * s * 0.04f);
    }

    static void MedHand(Transform r, float s, Material skin, float x, float y, float z)
    {
        Piece(r, Sphere, skin, new Vector3(x, y, z), Vector3.one * s * 0.08f);
    }

    // Escudo de cometa en el brazo izquierdo, con borde y cruz del color de la civilización
    static void MedKiteShield(Transform r, float s, Material face, Material rim, Material cross, float scale)
    {
        float x = -s * 0.37f, y = s * 0.55f, z = s * 0.06f;
        Piece(r, Cube, rim, new Vector3(x - s * 0.008f, y, z), new Vector3(s * 0.03f, s * 0.46f * scale, s * 0.34f * scale));
        GameObject tip = Piece(r, Cube, rim, new Vector3(x - s * 0.008f, y - s * 0.26f * scale, z), new Vector3(s * 0.03f, s * 0.24f * scale, s * 0.24f * scale));
        tip.transform.localRotation = Quaternion.Euler(45f, 0f, 0f);
        Piece(r, Cube, face, new Vector3(x, y, z), new Vector3(s * 0.03f, s * 0.42f * scale, s * 0.3f * scale));
        GameObject tipFace = Piece(r, Cube, face, new Vector3(x, y - s * 0.25f * scale, z), new Vector3(s * 0.03f, s * 0.2f * scale, s * 0.2f * scale));
        tipFace.transform.localRotation = Quaternion.Euler(45f, 0f, 0f);
        Piece(r, Cube, cross, new Vector3(x - s * 0.02f, y, z), new Vector3(s * 0.012f, s * 0.36f * scale, s * 0.05f));
        Piece(r, Cube, cross, new Vector3(x - s * 0.02f, y + s * 0.05f * scale, z), new Vector3(s * 0.012f, s * 0.05f, s * 0.24f * scale));
    }

    // Colono medieval: viajero con túnica y capucha, mochila con manta, bolsa de monedas y bastón con farol
    static void BuildMedievalSettler(Transform root, float s, Color civ)
    {
        Material tunic = LitMaterial(civ);
        Material cowl = LitMaterial(Color.Lerp(civ, Color.black, 0.4f));
        Material skin = LitMaterial(new Color(0.9f, 0.72f, 0.56f));
        Material hose = LitMaterial(new Color(0.3f, 0.24f, 0.2f));
        Material boots = LitMaterial(new Color(0.18f, 0.12f, 0.08f));
        Material leather = LitMaterial(new Color(0.5f, 0.34f, 0.2f));
        Material wood = LitMaterial(new Color(0.42f, 0.28f, 0.15f));
        Material linen = LitMaterial(new Color(0.85f, 0.8f, 0.65f));
        Material glow = LitMaterial(new Color(1f, 0.85f, 0.35f));
        glow.EnableKeyword("_EMISSION");
        if (glow.HasProperty("_EmissionColor")) glow.SetColor("_EmissionColor", new Color(1f, 0.7f, 0.2f));

        MedLegs(root, s, hose, boots);
        MedTorso(root, s, tunic, leather, true);
        MedHead(root, s, skin);

        // Capucha y esclavina
        Piece(root, Sphere, cowl, new Vector3(0f, s * 0.9f, -s * 0.03f), new Vector3(s * 0.27f, s * 0.2f, s * 0.27f));
        Piece(root, Sphere, cowl, new Vector3(0f, s * 0.78f, -s * 0.1f), new Vector3(s * 0.22f, s * 0.14f, s * 0.14f));
        Piece(root, Cylinder, cowl, new Vector3(0f, s * 0.72f, 0f), new Vector3(s * 0.4f, s * 0.035f, s * 0.32f));

        // Mochila con manta enrollada, y bolsa de monedas al cinto
        Piece(root, Cube, leather, new Vector3(0f, s * 0.55f, -s * 0.22f), new Vector3(s * 0.28f, s * 0.3f, s * 0.14f));
        GameObject roll = Piece(root, Cylinder, linen, new Vector3(0f, s * 0.74f, -s * 0.22f), new Vector3(s * 0.1f, s * 0.16f, s * 0.1f));
        roll.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        Piece(root, Sphere, leather, new Vector3(s * 0.2f, s * 0.38f, s * 0.14f), Vector3.one * s * 0.09f);

        // Mano y bastón de caminante con farol
        MedHand(root, s, skin, s * 0.3f, s * 0.46f, s * 0.1f);
        Piece(root, Cylinder, wood, new Vector3(s * 0.3f, s * 0.55f, s * 0.1f), new Vector3(s * 0.035f, s * 0.55f, s * 0.035f));
        Piece(root, Cube, wood, new Vector3(s * 0.34f, s * 0.98f, s * 0.1f), new Vector3(s * 0.12f, s * 0.025f, s * 0.025f));
        Piece(root, Cube, glow, new Vector3(s * 0.4f, s * 0.88f, s * 0.1f), new Vector3(s * 0.07f, s * 0.09f, s * 0.07f));
    }

    // Trabajador medieval: camisa de lino, delantal de cuero, gorro del color de la civilización,
    // pico al hombro y saco a la espalda
    static void BuildMedievalWorker(Transform root, float s, Color civ)
    {
        Material linen = LitMaterial(new Color(0.85f, 0.8f, 0.65f));
        Material apron = LitMaterial(new Color(0.45f, 0.3f, 0.17f));
        Material cap = LitMaterial(civ);
        Material skin = LitMaterial(new Color(0.9f, 0.7f, 0.54f));
        Material hose = LitMaterial(new Color(0.34f, 0.27f, 0.2f));
        Material boots = LitMaterial(new Color(0.2f, 0.14f, 0.09f));
        Material wood = LitMaterial(new Color(0.45f, 0.3f, 0.16f));
        Material iron = LitMaterial(new Color(0.5f, 0.52f, 0.56f));
        Material sack = LitMaterial(new Color(0.7f, 0.6f, 0.4f));

        MedLegs(root, s, hose, boots);
        MedTorso(root, s, linen, apron, true);
        Piece(root, Cube, apron, new Vector3(0f, s * 0.5f, s * 0.15f), new Vector3(s * 0.3f, s * 0.38f, s * 0.03f)); // delantal
        MedHead(root, s, skin);

        // Gorro de fieltro del color de la civilización
        Piece(root, Sphere, cap, new Vector3(0f, s * 0.95f, 0f), new Vector3(s * 0.24f, s * 0.16f, s * 0.24f));
        Piece(root, Cylinder, cap, new Vector3(0f, s * 0.9f, 0f), new Vector3(s * 0.26f, s * 0.015f, s * 0.26f));

        // Saco a la espalda
        Piece(root, Sphere, sack, new Vector3(0f, s * 0.56f, -s * 0.24f), new Vector3(s * 0.28f, s * 0.34f, s * 0.2f));

        // Pico al hombro: mango de madera y cabeza de hierro curva
        GameObject handle = Piece(root, Cylinder, wood, new Vector3(s * 0.3f, s * 0.72f, s * 0.04f), new Vector3(s * 0.04f, s * 0.34f, s * 0.04f));
        handle.transform.localRotation = Quaternion.Euler(0f, 0f, -18f);
        GameObject head = Piece(root, Cube, iron, new Vector3(s * 0.38f, s * 1.04f, s * 0.04f), new Vector3(s * 0.04f, s * 0.05f, s * 0.3f));
        head.transform.localRotation = Quaternion.Euler(0f, 0f, -18f);
        MedHand(root, s, skin, s * 0.27f, s * 0.62f, s * 0.06f);
    }

    // Soldado medieval a pie. kind: 0 = guerrero (cota de malla, escudo de cometa y espada), 1 = lancero (gambesón,
    // sombrero de hierro, pica y rodela), 2 = falange (piquero con gran pavés y pica corta)
    static void BuildMedievalSoldier(Transform root, float s, Color civ, int kind)
    {
        Material tabard = LitMaterial(civ);
        Material tabardDark = LitMaterial(Color.Lerp(civ, Color.black, 0.4f));
        Material mail = LitMaterial(new Color(0.55f, 0.57f, 0.6f));
        Material steel = LitMaterial(new Color(0.78f, 0.8f, 0.85f));
        Material skin = LitMaterial(new Color(0.88f, 0.68f, 0.52f));
        Material hose = LitMaterial(new Color(0.28f, 0.22f, 0.2f));
        Material boots = LitMaterial(new Color(0.18f, 0.12f, 0.09f));
        Material leather = LitMaterial(new Color(0.42f, 0.27f, 0.15f));
        Material wood = LitMaterial(new Color(0.45f, 0.3f, 0.16f));
        Material gold = LitMaterial(new Color(0.95f, 0.8f, 0.3f));
        steel.SetFloat("_Glossiness", 0.7f);
        mail.SetFloat("_Glossiness", 0.4f);

        MedLegs(root, s, hose, boots);

        if (kind == 1)
        {
            // Gambesón acolchado del color de la civilización
            MedTorso(root, s, tabard, leather, true);
            for (int i = -1; i <= 1; i++)
                Piece(root, Cube, tabardDark, new Vector3(i * s * 0.1f, s * 0.6f, s * 0.14f), new Vector3(s * 0.025f, s * 0.26f, s * 0.02f));
        }
        else
        {
            // Cota de malla con sobretodo del color de la civilización
            MedTorso(root, s, mail, leather, true);
            Piece(root, Cube, tabard, new Vector3(0f, s * 0.5f, s * 0.15f), new Vector3(s * 0.3f, s * 0.46f, s * 0.03f));
            Piece(root, Cube, tabard, new Vector3(0f, s * 0.5f, -s * 0.15f), new Vector3(s * 0.3f, s * 0.46f, s * 0.03f));
        }

        MedHead(root, s, skin);

        if (kind == 1)
        {
            // Sombrero de hierro de ala ancha (kettle hat)
            Piece(root, Sphere, steel, new Vector3(0f, s * 0.93f, 0f), new Vector3(s * 0.26f, s * 0.16f, s * 0.26f));
            Piece(root, Cylinder, steel, new Vector3(0f, s * 0.9f, 0f), new Vector3(s * 0.46f, s * 0.012f, s * 0.46f));
        }
        else
        {
            // Yelmo con nasal y cofia de malla
            Piece(root, Sphere, mail, new Vector3(0f, s * 0.84f, -s * 0.03f), new Vector3(s * 0.25f, s * 0.24f, s * 0.25f));
            Piece(root, Sphere, steel, new Vector3(0f, s * 0.92f, 0f), new Vector3(s * 0.24f, s * 0.15f, s * 0.24f));
            Piece(root, Cube, steel, new Vector3(0f, s * 0.87f, s * 0.115f), new Vector3(s * 0.03f, s * 0.11f, s * 0.02f));
        }

        MedHand(root, s, skin, s * 0.3f, s * 0.46f, s * 0.1f);

        if (kind == 0)
        {
            MedKiteShield(root, s, tabard, steel, gold, 1f);

            // Espada con cruceta y pomo
            GameObject sword = Piece(root, Cube, steel, new Vector3(s * 0.33f, s * 0.75f, s * 0.2f), new Vector3(s * 0.05f, s * 0.5f, s * 0.015f));
            sword.transform.localRotation = Quaternion.Euler(25f, 0f, -8f);
            Piece(root, Cube, gold, new Vector3(s * 0.31f, s * 0.5f, s * 0.12f), new Vector3(s * 0.18f, s * 0.03f, s * 0.03f));
            Piece(root, Sphere, gold, new Vector3(s * 0.3f, s * 0.4f, s * 0.09f), Vector3.one * s * 0.05f);
        }
        else if (kind == 1)
        {
            // Rodela redonda y pica larga con banderín
            GameObject buckler = Piece(root, Cylinder, tabardDark, new Vector3(-s * 0.35f, s * 0.55f, s * 0.06f), new Vector3(s * 0.28f, s * 0.02f, s * 0.28f));
            buckler.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            GameObject boss = Piece(root, Sphere, steel, new Vector3(-s * 0.37f, s * 0.55f, s * 0.06f), Vector3.one * s * 0.07f);
            Piece(root, Cylinder, wood, new Vector3(s * 0.3f, s * 0.85f, s * 0.1f), new Vector3(s * 0.035f, s * 0.95f, s * 0.035f));
            GameObject tip = Piece(root, Cube, steel, new Vector3(s * 0.3f, s * 1.82f, s * 0.1f), new Vector3(s * 0.06f, s * 0.2f, s * 0.02f));
            tip.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
            Piece(root, Cube, tabard, new Vector3(s * 0.3f, s * 1.62f, s * 0.02f), new Vector3(s * 0.02f, s * 0.14f, s * 0.18f));
        }
        else
        {
            // Pavés: escudo rectangular enorme, con cruz del color de la civilización, y pica corta
            Piece(root, Cube, steel, new Vector3(-s * 0.4f, s * 0.55f, s * 0.08f), new Vector3(s * 0.04f, s * 0.78f, s * 0.46f));
            Piece(root, Cube, tabard, new Vector3(-s * 0.425f, s * 0.55f, s * 0.08f), new Vector3(s * 0.02f, s * 0.7f, s * 0.38f));
            Piece(root, Cube, gold, new Vector3(-s * 0.445f, s * 0.55f, s * 0.08f), new Vector3(s * 0.012f, s * 0.6f, s * 0.07f));
            Piece(root, Cube, gold, new Vector3(-s * 0.445f, s * 0.6f, s * 0.08f), new Vector3(s * 0.012f, s * 0.07f, s * 0.32f));
            Piece(root, Cylinder, wood, new Vector3(s * 0.3f, s * 0.7f, s * 0.12f), new Vector3(s * 0.035f, s * 0.7f, s * 0.035f));
            GameObject tip = Piece(root, Cube, steel, new Vector3(s * 0.3f, s * 1.45f, s * 0.12f), new Vector3(s * 0.06f, s * 0.18f, s * 0.02f));
            tip.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
        }
    }

    // Arquero medieval: arquero con capucha y pluma, jubón de cuero, carcaj de flechas y gran arco largo
    static void BuildMedievalArcher(Transform root, float s, Color civ)
    {
        Material jerkin = LitMaterial(civ);
        Material cowl = LitMaterial(Color.Lerp(civ, Color.black, 0.45f));
        Material skin = LitMaterial(new Color(0.9f, 0.72f, 0.56f));
        Material hose = LitMaterial(new Color(0.3f, 0.26f, 0.2f));
        Material boots = LitMaterial(new Color(0.2f, 0.14f, 0.09f));
        Material leather = LitMaterial(new Color(0.5f, 0.34f, 0.2f));
        Material wood = LitMaterial(new Color(0.55f, 0.38f, 0.18f));
        Material white = LitMaterial(new Color(0.92f, 0.9f, 0.85f));
        Material steel = LitMaterial(new Color(0.75f, 0.77f, 0.82f));

        MedLegs(root, s, hose, boots);
        MedTorso(root, s, jerkin, leather, true);
        MedHead(root, s, skin);

        // Capucha con una pluma
        Piece(root, Sphere, cowl, new Vector3(0f, s * 0.92f, -s * 0.03f), new Vector3(s * 0.26f, s * 0.18f, s * 0.26f));
        GameObject feather = Piece(root, Cube, white, new Vector3(s * 0.12f, s * 1.0f, -s * 0.04f), new Vector3(s * 0.02f, s * 0.2f, s * 0.07f));
        feather.transform.localRotation = Quaternion.Euler(0f, 0f, -35f);

        // Carcaj con flechas a la espalda
        GameObject quiver = Piece(root, Cylinder, leather, new Vector3(s * 0.06f, s * 0.58f, -s * 0.2f), new Vector3(s * 0.09f, s * 0.2f, s * 0.09f));
        quiver.transform.localRotation = Quaternion.Euler(-15f, 0f, 12f);
        for (int i = -1; i <= 1; i++)
            Piece(root, Cube, white, new Vector3(s * (0.04f + i * 0.03f), s * 0.86f, -s * 0.27f), new Vector3(s * 0.02f, s * 0.1f, s * 0.02f));

        // Arco largo: dos tramos curvados y la cuerda
        MedHand(root, s, skin, s * 0.3f, s * 0.5f, s * 0.12f);
        GameObject upper = Piece(root, Cylinder, wood, new Vector3(s * 0.31f, s * 0.82f, s * 0.12f), new Vector3(s * 0.03f, s * 0.22f, s * 0.03f));
        upper.transform.localRotation = Quaternion.Euler(12f, 0f, 0f);
        GameObject lower = Piece(root, Cylinder, wood, new Vector3(s * 0.31f, s * 0.3f, s * 0.12f), new Vector3(s * 0.03f, s * 0.22f, s * 0.03f));
        lower.transform.localRotation = Quaternion.Euler(-12f, 0f, 0f);
        Piece(root, Cube, white, new Vector3(s * 0.31f, s * 0.56f, s * 0.05f), new Vector3(s * 0.008f, s * 0.82f, s * 0.008f));
        Piece(root, Cube, steel, new Vector3(s * 0.31f, s * 0.56f, s * 0.2f), new Vector3(s * 0.01f, s * 0.01f, s * 0.3f));
    }

    // Ballestero: arquero con capucha y una ballesta de madera con arco de acero y virote cargado
    static void BuildCrossbowman(Transform root, float s, Color civ)
    {
        Material jerkin = LitMaterial(civ);
        Material cowl = LitMaterial(Color.Lerp(civ, Color.black, 0.45f));
        Material skin = LitMaterial(new Color(0.9f, 0.72f, 0.56f));
        Material hose = LitMaterial(new Color(0.3f, 0.26f, 0.2f));
        Material boots = LitMaterial(new Color(0.2f, 0.14f, 0.09f));
        Material leather = LitMaterial(new Color(0.5f, 0.34f, 0.2f));
        Material wood = LitMaterial(new Color(0.45f, 0.3f, 0.16f));
        Material steel = LitMaterial(new Color(0.75f, 0.77f, 0.82f));
        Material white = LitMaterial(new Color(0.92f, 0.9f, 0.85f));

        MedLegs(root, s, hose, boots);
        MedTorso(root, s, jerkin, leather, true);
        MedHead(root, s, skin);
        Piece(root, Sphere, cowl, new Vector3(0f, s * 0.92f, -s * 0.03f), new Vector3(s * 0.26f, s * 0.18f, s * 0.26f));

        // Aljaba de virotes al cinto
        GameObject quiver = Piece(root, Cylinder, leather, new Vector3(-s * 0.16f, s * 0.42f, -s * 0.08f), new Vector3(s * 0.08f, s * 0.14f, s * 0.08f));
        quiver.transform.localRotation = Quaternion.Euler(0f, 0f, -10f);

        // Ballesta apuntando al frente: caja de madera, arco de acero cruzado, cuerda y virote
        MedHand(root, s, skin, s * 0.2f, s * 0.5f, s * 0.2f);
        MedHand(root, s, skin, s * 0.2f, s * 0.52f, s * 0.38f);
        Piece(root, Cube, wood, new Vector3(s * 0.2f, s * 0.5f, s * 0.3f), new Vector3(s * 0.06f, s * 0.06f, s * 0.5f));
        Piece(root, Cube, steel, new Vector3(s * 0.2f, s * 0.52f, s * 0.52f), new Vector3(s * 0.4f, s * 0.025f, s * 0.03f));
        Piece(root, Cube, white, new Vector3(s * 0.2f, s * 0.52f, s * 0.42f), new Vector3(s * 0.36f, s * 0.008f, s * 0.008f));
        Piece(root, Cube, steel, new Vector3(s * 0.2f, s * 0.545f, s * 0.38f), new Vector3(s * 0.015f, s * 0.015f, s * 0.3f));
    }

    // Ballesta de asedio: gran ballesta sobre un armazón de madera con ruedas, con un virote enorme en el canal
    static void BuildBallista(Transform root, float s, Color civ)
    {
        Material wood = LitMaterial(new Color(0.45f, 0.3f, 0.16f));
        Material woodDark = LitMaterial(new Color(0.3f, 0.2f, 0.1f));
        Material iron = LitMaterial(new Color(0.55f, 0.57f, 0.62f));
        Material rope = LitMaterial(new Color(0.8f, 0.72f, 0.5f));
        Material flag = LitMaterial(civ);

        // Armazón y ruedas
        Piece(root, Cube, woodDark, new Vector3(0f, s * 0.2f, 0f), new Vector3(s * 0.5f, s * 0.08f, s * 0.9f));
        for (int side = -1; side <= 1; side += 2)
        {
            GameObject wheel = Piece(root, Cylinder, woodDark, new Vector3(side * s * 0.32f, s * 0.2f, -s * 0.05f), new Vector3(s * 0.34f, s * 0.04f, s * 0.34f));
            wheel.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        }

        // Canal central, soporte y gran arco con dos brazos de madera y la cuerda
        Piece(root, Cube, wood, new Vector3(0f, s * 0.38f, s * 0.1f), new Vector3(s * 0.1f, s * 0.1f, s * 0.95f));
        Piece(root, Cube, woodDark, new Vector3(0f, s * 0.3f, s * 0.05f), new Vector3(s * 0.16f, s * 0.14f, s * 0.2f));
        for (int side = -1; side <= 1; side += 2)
        {
            GameObject arm = Piece(root, Cube, wood, new Vector3(side * s * 0.4f, s * 0.42f, s * 0.5f), new Vector3(s * 0.7f, s * 0.07f, s * 0.07f));
            arm.transform.localRotation = Quaternion.Euler(0f, side * 14f, 0f);
        }
        Piece(root, Cube, rope, new Vector3(0f, s * 0.43f, s * 0.2f), new Vector3(s * 0.9f, s * 0.015f, s * 0.015f));

        // Virote enorme con punta de hierro y banderín de la civilización
        Piece(root, Cube, woodDark, new Vector3(0f, s * 0.47f, s * 0.2f), new Vector3(s * 0.035f, s * 0.035f, s * 0.9f));
        Piece(root, Cube, iron, new Vector3(0f, s * 0.47f, s * 0.7f), new Vector3(s * 0.07f, s * 0.05f, s * 0.12f));
        Piece(root, Cube, flag, new Vector3(0f, s * 0.5f, -s * 0.2f), new Vector3(s * 0.01f, s * 0.1f, s * 0.14f));
    }

    // Fusilero: casaca del color de la civilización, chacó alto y un fusil largo con bayoneta
    static void BuildRifleman(Transform root, float s, Color civ)
    {
        Material coat = LitMaterial(civ);
        Material hat = LitMaterial(new Color(0.1f, 0.1f, 0.14f));
        Material skin = LitMaterial(new Color(0.9f, 0.72f, 0.56f));
        Material trousers = LitMaterial(new Color(0.22f, 0.24f, 0.3f));
        Material boots = LitMaterial(new Color(0.12f, 0.08f, 0.05f));
        Material leather = LitMaterial(new Color(0.8f, 0.78f, 0.7f));
        Material wood = LitMaterial(new Color(0.4f, 0.26f, 0.13f));
        Material steel = LitMaterial(new Color(0.75f, 0.77f, 0.82f));
        Material gold = LitMaterial(new Color(0.95f, 0.8f, 0.3f));

        MedLegs(root, s, trousers, boots);
        MedTorso(root, s, coat, leather, false);
        MedHead(root, s, skin);

        // Chacó con plaquita dorada
        Piece(root, Cylinder, hat, new Vector3(0f, s * 1.0f, 0f), new Vector3(s * 0.2f, s * 0.12f, s * 0.2f));
        Piece(root, Cube, gold, new Vector3(0f, s * 0.99f, s * 0.1f), new Vector3(s * 0.07f, s * 0.07f, s * 0.01f));

        // Fusil apoyado al frente con bayoneta
        MedHand(root, s, skin, s * 0.2f, s * 0.5f, s * 0.2f);
        MedHand(root, s, skin, s * 0.2f, s * 0.55f, s * 0.45f);
        GameObject stock = Piece(root, Cube, wood, new Vector3(s * 0.2f, s * 0.48f, s * 0.1f), new Vector3(s * 0.045f, s * 0.07f, s * 0.3f));
        stock.transform.localRotation = Quaternion.Euler(-8f, 0f, 0f);
        Piece(root, Cube, steel, new Vector3(s * 0.2f, s * 0.54f, s * 0.55f), new Vector3(s * 0.02f, s * 0.02f, s * 0.75f));
        Piece(root, Cube, steel, new Vector3(s * 0.2f, s * 0.54f, s * 1.02f), new Vector3(s * 0.01f, s * 0.01f, s * 0.2f));
    }

    // Granadero: casaca del color de la civilización, gorro alto y una granada de hierro en la mano
    static void BuildGrenadier(Transform root, float s, Color civ)
    {
        Material coat = LitMaterial(civ);
        Material hat = LitMaterial(new Color(0.1f, 0.1f, 0.14f));
        Material skin = LitMaterial(new Color(0.9f, 0.72f, 0.56f));
        Material trousers = LitMaterial(new Color(0.22f, 0.24f, 0.3f));
        Material boots = LitMaterial(new Color(0.12f, 0.08f, 0.05f));
        Material leather = LitMaterial(new Color(0.8f, 0.78f, 0.7f));
        Material iron = LitMaterial(new Color(0.15f, 0.15f, 0.17f));
        Material fuse = LitMaterial(new Color(0.95f, 0.55f, 0.15f));

        MedLegs(root, s, trousers, boots);
        MedTorso(root, s, coat, leather, false);
        MedHead(root, s, skin);

        // Alto gorro de granadero con plaquita del color de la civilización
        Piece(root, Cylinder, hat, new Vector3(0f, s * 1.08f, 0f), new Vector3(s * 0.18f, s * 0.2f, s * 0.18f));
        Piece(root, Cube, coat, new Vector3(0f, s * 1.1f, s * 0.09f), new Vector3(s * 0.08f, s * 0.1f, s * 0.01f));

        // Granada esférica con mecha encendida, sostenida en alto
        MedHand(root, s, skin, s * 0.2f, s * 0.7f, s * 0.25f);
        Piece(root, Sphere, iron, new Vector3(s * 0.2f, s * 0.8f, s * 0.25f), new Vector3(s * 0.16f, s * 0.16f, s * 0.16f));
        Piece(root, Cube, fuse, new Vector3(s * 0.2f, s * 0.91f, s * 0.25f), new Vector3(s * 0.02f, s * 0.07f, s * 0.02f));

        // Bandolera con más granadas
        Piece(root, Sphere, iron, new Vector3(-s * 0.12f, s * 0.45f, s * 0.14f), new Vector3(s * 0.08f, s * 0.08f, s * 0.08f));
        Piece(root, Sphere, iron, new Vector3(s * 0.12f, s * 0.45f, s * 0.14f), new Vector3(s * 0.08f, s * 0.08f, s * 0.08f));
    }

    // Cañón: cureña de madera con dos ruedas, un largo tubo de hierro y una pila de balas
    static void BuildCannon(Transform root, float s, Color civ)
    {
        Material wood = LitMaterial(new Color(0.42f, 0.28f, 0.14f));
        Material woodDark = LitMaterial(new Color(0.28f, 0.18f, 0.1f));
        Material iron = LitMaterial(new Color(0.18f, 0.18f, 0.2f));
        Material flag = LitMaterial(civ);

        Piece(root, Cube, wood, new Vector3(0f, s * 0.22f, 0f), new Vector3(s * 0.4f, s * 0.1f, s * 0.8f));
        for (int side = -1; side <= 1; side += 2)
        {
            GameObject wheel = Piece(root, Cylinder, woodDark, new Vector3(side * s * 0.27f, s * 0.24f, 0f), new Vector3(s * 0.4f, s * 0.05f, s * 0.4f));
            wheel.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        }

        GameObject barrel = Piece(root, Cylinder, iron, new Vector3(0f, s * 0.4f, s * 0.2f), new Vector3(s * 0.2f, s * 0.5f, s * 0.2f));
        barrel.transform.localRotation = Quaternion.Euler(80f, 0f, 0f);
        Piece(root, Sphere, iron, new Vector3(0f, s * 0.38f, -s * 0.3f), new Vector3(s * 0.2f, s * 0.2f, s * 0.2f));

        // Pila de balas y banderín de la civilización
        for (int i = 0; i < 3; i++)
            Piece(root, Sphere, iron, new Vector3((i - 1) * s * 0.1f, s * 0.31f, -s * 0.5f), new Vector3(s * 0.09f, s * 0.09f, s * 0.09f));
        Piece(root, Cube, flag, new Vector3(0f, s * 0.58f, -s * 0.3f), new Vector3(s * 0.01f, s * 0.12f, s * 0.16f));
    }

    // Barco de vapor: casco de hierro, dos chimeneas humeantes, una rueda de palas y un mástil con el estandarte
    static void BuildSteamship(Transform root, float s, Color civ)
    {
        Material hull = LitMaterial(new Color(0.22f, 0.22f, 0.25f));
        Material deck = LitMaterial(new Color(0.55f, 0.42f, 0.26f));
        Material stack = LitMaterial(new Color(0.08f, 0.08f, 0.09f));
        Material band = LitMaterial(civ);
        Material smoke = LitMaterial(new Color(0.6f, 0.6f, 0.62f));
        Material white = LitMaterial(new Color(0.9f, 0.9f, 0.88f));

        Piece(root, ShipHull(), hull, Vector3.zero, new Vector3(s * 1.2f, s * 1.1f, s * 1.7f));
        Piece(root, Cube, deck, new Vector3(0f, s * 0.46f, 0f), new Vector3(s * 0.6f, s * 0.06f, s * 1.1f));
        Piece(root, Cube, white, new Vector3(0f, s * 0.62f, -s * 0.15f), new Vector3(s * 0.4f, s * 0.26f, s * 0.5f)); // puente

        // Dos chimeneas con franja de color y humo
        for (int i = 0; i < 2; i++)
        {
            float z = s * (0.2f + i * 0.4f);
            Piece(root, Cylinder, stack, new Vector3(0f, s * 0.95f, z), new Vector3(s * 0.2f, s * 0.35f, s * 0.2f));
            Piece(root, Cylinder, band, new Vector3(0f, s * 1.18f, z), new Vector3(s * 0.21f, s * 0.05f, s * 0.21f));
            Piece(root, Sphere, smoke, new Vector3(0f, s * 1.5f + i * s * 0.1f, z - s * 0.05f), new Vector3(s * 0.3f, s * 0.22f, s * 0.3f));
        }

        // Ruedas de palas a ambos lados
        for (int side = -1; side <= 1; side += 2)
        {
            GameObject paddle = Piece(root, Cylinder, stack, new Vector3(side * s * 0.62f, s * 0.38f, -s * 0.05f), new Vector3(s * 0.4f, s * 0.05f, s * 0.4f));
            paddle.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        }

        // Mástil pequeño con estandarte
        Piece(root, Cylinder, deck, new Vector3(0f, s * 1.0f, -s * 0.55f), new Vector3(s * 0.04f, s * 0.5f, s * 0.04f));
        Piece(root, Cube, band, new Vector3(s * 0.05f, s * 1.45f, -s * 0.55f), new Vector3(s * 0.02f, s * 0.12f, s * 0.3f));
    }

    // Mosquetero: casaca del color de la civilización, sombrero de tres picos y un mosquete largo con bayoneta
    static void BuildMusketeer(Transform root, float s, Color civ)
    {
        Material coat = LitMaterial(civ);
        Material hat = LitMaterial(new Color(0.12f, 0.11f, 0.13f));
        Material skin = LitMaterial(new Color(0.9f, 0.72f, 0.56f));
        Material breeches = LitMaterial(new Color(0.9f, 0.88f, 0.82f));
        Material boots = LitMaterial(new Color(0.15f, 0.1f, 0.07f));
        Material leather = LitMaterial(new Color(0.5f, 0.34f, 0.2f));
        Material wood = LitMaterial(new Color(0.42f, 0.27f, 0.14f));
        Material steel = LitMaterial(new Color(0.75f, 0.77f, 0.82f));

        MedLegs(root, s, breeches, boots);
        MedTorso(root, s, coat, leather, true);
        MedHead(root, s, skin);

        // Sombrero de tres picos
        Piece(root, Cylinder, hat, new Vector3(0f, s * 0.93f, 0f), new Vector3(s * 0.34f, s * 0.025f, s * 0.34f));
        Piece(root, Cylinder, hat, new Vector3(0f, s * 0.98f, 0f), new Vector3(s * 0.2f, s * 0.05f, s * 0.2f));

        // Mosquete: culata de madera, cañón de acero y bayoneta, sujeto con ambas manos
        MedHand(root, s, skin, s * 0.2f, s * 0.5f, s * 0.2f);
        MedHand(root, s, skin, s * 0.2f, s * 0.55f, s * 0.4f);
        GameObject stock = Piece(root, Cube, wood, new Vector3(s * 0.2f, s * 0.48f, s * 0.12f), new Vector3(s * 0.05f, s * 0.07f, s * 0.3f));
        stock.transform.localRotation = Quaternion.Euler(-8f, 0f, 0f);
        Piece(root, Cube, steel, new Vector3(s * 0.2f, s * 0.54f, s * 0.5f), new Vector3(s * 0.025f, s * 0.025f, s * 0.6f));
        Piece(root, Cube, steel, new Vector3(s * 0.2f, s * 0.54f, s * 0.88f), new Vector3(s * 0.012f, s * 0.012f, s * 0.18f));
    }

    // Caballero medieval con armadura completa y sobretodo del color de la civilización. mounted: con lanza (a caballo);
    // si no, con espada larga y gran escudo (infantería pesada)
    static void BuildMedievalKnight(Transform root, float s, Color civ, bool mounted)
    {
        Material tabard = LitMaterial(civ);
        Material steel = LitMaterial(new Color(0.76f, 0.78f, 0.83f));
        Material steelDark = LitMaterial(new Color(0.5f, 0.52f, 0.57f));
        Material dark = LitMaterial(new Color(0.05f, 0.05f, 0.06f));
        Material gold = LitMaterial(new Color(0.95f, 0.8f, 0.3f));
        Material wood = LitMaterial(new Color(0.45f, 0.3f, 0.16f));
        steel.SetFloat("_Glossiness", 0.8f);

        MedLegs(root, s, steelDark, steelDark);
        for (int side = -1; side <= 1; side += 2)
            Piece(root, Cylinder, steel, new Vector3(side * s * 0.1f, s * 0.23f, s * 0.02f), new Vector3(s * 0.13f, s * 0.1f, s * 0.13f)); // grebas

        // Coraza de placas con sobretodo y hombreras
        MedTorso(root, s, steel, steelDark, true);
        Piece(root, Cube, tabard, new Vector3(0f, s * 0.5f, s * 0.15f), new Vector3(s * 0.32f, s * 0.5f, s * 0.03f));
        Piece(root, Cube, tabard, new Vector3(0f, s * 0.5f, -s * 0.15f), new Vector3(s * 0.32f, s * 0.5f, s * 0.03f));
        for (int side = -1; side <= 1; side += 2)
            Piece(root, Sphere, steel, new Vector3(side * s * 0.23f, s * 0.72f, 0f), new Vector3(s * 0.19f, s * 0.12f, s * 0.19f));

        // Yelmo cerrado con ranura de visión y penacho
        Piece(root, Cylinder, steel, new Vector3(0f, s * 0.88f, 0f), new Vector3(s * 0.22f, s * 0.13f, s * 0.22f));
        Piece(root, Sphere, steel, new Vector3(0f, s * 1.0f, 0f), new Vector3(s * 0.22f, s * 0.12f, s * 0.22f));
        Piece(root, Cube, dark, new Vector3(0f, s * 0.92f, s * 0.11f), new Vector3(s * 0.16f, s * 0.025f, s * 0.02f));
        Piece(root, Cube, dark, new Vector3(0f, s * 0.85f, s * 0.11f), new Vector3(s * 0.025f, s * 0.12f, s * 0.02f));
        Piece(root, Capsule, tabard, new Vector3(0f, s * 1.12f, -s * 0.05f), new Vector3(s * 0.06f, s * 0.1f, s * 0.16f));

        MedKiteShield(root, s, tabard, steel, gold, mounted ? 1f : 1.25f);

        if (mounted)
        {
            // Lanza larga con banderín
            GameObject lance = Piece(root, Cylinder, wood, new Vector3(s * 0.3f, s * 0.6f, s * 0.5f), new Vector3(s * 0.035f, s * 0.9f, s * 0.035f));
            lance.transform.localRotation = Quaternion.Euler(80f, 0f, 0f);
            Piece(root, Cube, tabard, new Vector3(s * 0.3f, s * 0.7f, s * 0.25f), new Vector3(s * 0.02f, s * 0.12f, s * 0.2f));
        }
        else
        {
            // Espada larga a dos manos con cruceta
            GameObject sword = Piece(root, Cube, steel, new Vector3(s * 0.33f, s * 0.82f, s * 0.2f), new Vector3(s * 0.06f, s * 0.7f, s * 0.018f));
            sword.transform.localRotation = Quaternion.Euler(20f, 0f, -6f);
            Piece(root, Cube, gold, new Vector3(s * 0.31f, s * 0.48f, s * 0.12f), new Vector3(s * 0.22f, s * 0.035f, s * 0.035f));
        }
    }

    // Trabuquete (catapulta medieval): bastidor en A, brazo largo con honda y contrapeso, y bandera de la civilización
    static void BuildMedievalTrebuchet(Transform root, float s, Color civ)
    {
        Material wood = LitMaterial(new Color(0.5f, 0.34f, 0.18f));
        Material woodDark = LitMaterial(new Color(0.3f, 0.2f, 0.1f));
        Material stone = LitMaterial(new Color(0.5f, 0.5f, 0.52f));
        Material flag = LitMaterial(civ);
        Material rope = LitMaterial(new Color(0.7f, 0.62f, 0.45f));

        // Base
        Piece(root, Cube, wood, new Vector3(0f, s * 0.1f, 0f), new Vector3(s * 0.5f, s * 0.08f, s * 0.9f));
        foreach (float z in new[] { -0.22f, 0.22f })
        {
            // Dos montantes inclinados formando una A a cada lado
            for (int side = -1; side <= 1; side += 2)
            {
                GameObject leg = Piece(root, Cube, woodDark, new Vector3(side * s * 0.2f, s * 0.42f, z * s), new Vector3(s * 0.07f, s * 0.72f, s * 0.07f));
                leg.transform.localRotation = Quaternion.Euler(0f, 0f, side * -14f);
            }
        }

        // Eje y brazo (más largo hacia atrás: honda con piedra; corto hacia delante: contrapeso)
        GameObject axle = Piece(root, Cylinder, woodDark, new Vector3(0f, s * 0.75f, 0f), new Vector3(s * 0.05f, s * 0.3f, s * 0.05f));
        axle.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        GameObject arm = Piece(root, Cylinder, wood, new Vector3(0f, s * 0.78f, -s * 0.1f), new Vector3(s * 0.06f, s * 0.6f, s * 0.06f));
        arm.transform.localRotation = Quaternion.Euler(70f, 0f, 0f);
        Piece(root, Cube, stone, new Vector3(0f, s * 0.5f, s * 0.38f), new Vector3(s * 0.3f, s * 0.26f, s * 0.26f)); // contrapeso
        Piece(root, Cube, woodDark, new Vector3(0f, s * 0.64f, s * 0.3f), new Vector3(s * 0.06f, s * 0.2f, s * 0.06f));
        Piece(root, Cube, rope, new Vector3(0f, s * 0.2f, -s * 0.55f), new Vector3(s * 0.02f, s * 0.02f, s * 0.5f)); // honda en el suelo
        Piece(root, Sphere, stone, new Vector3(0f, s * 0.14f, -s * 0.78f), Vector3.one * s * 0.12f);

        // Banderín
        Piece(root, Cylinder, woodDark, new Vector3(0f, s * 1.0f, 0f), new Vector3(s * 0.025f, s * 0.2f, s * 0.025f));
        Piece(root, Cube, flag, new Vector3(0f, s * 1.1f, s * 0.12f), new Vector3(s * 0.02f, s * 0.14f, s * 0.22f));
    }

    // Cog medieval: casco ancho con castillos de proa y popa, mástil con una gran vela cuadrada con cruz
    static void BuildMedievalCog(Transform root, float s, Color civ)
    {
        Material wood = LitMaterial(new Color(0.42f, 0.28f, 0.15f));
        Material woodLight = LitMaterial(new Color(0.62f, 0.47f, 0.28f));
        Material sail = LitMaterial(new Color(0.93f, 0.9f, 0.8f));
        Material cross = LitMaterial(civ);

        Vector3 one = Vector3.one * s;
        Piece(root, ShipHull(), wood, Vector3.zero, new Vector3(s * 1.2f, s * 1.1f, s));

        // Castillos de popa y de proa con almenas
        Piece(root, Cube, woodLight, new Vector3(0f, s * 0.5f, -s * 0.7f), new Vector3(s * 0.62f, s * 0.3f, s * 0.5f));
        Piece(root, Cube, woodLight, new Vector3(0f, s * 0.52f, s * 0.78f), new Vector3(s * 0.4f, s * 0.26f, s * 0.36f));
        for (int i = -2; i <= 2; i++)
            Piece(root, Cube, wood, new Vector3(i * s * 0.12f, s * 0.7f, -s * 0.94f), new Vector3(s * 0.07f, s * 0.08f, s * 0.05f));

        // Mástil, verga y vela cuadrada con cruz del color de la civilización
        Piece(root, Cylinder, wood, new Vector3(0f, s * 1.1f, s * 0.05f), new Vector3(s * 0.06f, s * 0.75f, s * 0.06f));
        GameObject yard = Piece(root, Cylinder, wood, new Vector3(0f, s * 1.55f, s * 0.05f), new Vector3(s * 0.035f, s * 0.5f, s * 0.035f));
        yard.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        Piece(root, Cube, sail, new Vector3(0f, s * 1.12f, s * 0.05f), new Vector3(s * 0.95f, s * 0.8f, s * 0.02f));
        Piece(root, Cube, cross, new Vector3(0f, s * 1.12f, s * 0.07f), new Vector3(s * 0.12f, s * 0.7f, s * 0.01f));
        Piece(root, Cube, cross, new Vector3(0f, s * 1.18f, s * 0.07f), new Vector3(s * 0.7f, s * 0.12f, s * 0.01f));
        Piece(root, Cube, cross, new Vector3(s * 0.05f, s * 1.92f, s * 0.05f), new Vector3(s * 0.02f, s * 0.12f, s * 0.3f));
    }

    // Galeón: casco grande con castillo de popa alto, tres mástiles con velas y estandarte del color de la civilización
    static void BuildGalleon(Transform root, float s, Color civ)
    {
        Material wood = LitMaterial(new Color(0.34f, 0.22f, 0.12f));
        Material woodLight = LitMaterial(new Color(0.58f, 0.43f, 0.25f));
        Material sail = LitMaterial(new Color(0.95f, 0.92f, 0.82f));
        Material flag = LitMaterial(civ);

        Piece(root, ShipHull(), wood, Vector3.zero, new Vector3(s * 1.3f, s * 1.2f, s * 1.5f));

        // Castillo de popa alto y de proa bajo
        Piece(root, Cube, woodLight, new Vector3(0f, s * 0.55f, -s * 0.95f), new Vector3(s * 0.7f, s * 0.4f, s * 0.6f));
        Piece(root, Cube, wood, new Vector3(0f, s * 0.82f, -s * 1.05f), new Vector3(s * 0.55f, s * 0.2f, s * 0.4f));
        Piece(root, Cube, woodLight, new Vector3(0f, s * 0.5f, s * 1.0f), new Vector3(s * 0.42f, s * 0.24f, s * 0.4f));

        // Mástil mayor y trinquete con velas cuadradas, mesana en popa
        float[] mastZ = { s * 0.3f, s * 0.85f, -s * 0.7f };
        float[] mastH = { 1.0f, 0.75f, 0.6f };
        for (int i = 0; i < 3; i++)
        {
            float h = mastH[i];
            Piece(root, Cylinder, wood, new Vector3(0f, s * (0.9f + h * 0.6f), mastZ[i]), new Vector3(s * 0.06f, s * h * 0.8f, s * 0.06f));
            Piece(root, Cube, sail, new Vector3(0f, s * (0.9f + h * 0.55f), mastZ[i] + s * 0.03f), new Vector3(s * 0.9f * h, s * 0.7f * h, s * 0.02f));
            GameObject yard = Piece(root, Cylinder, wood, new Vector3(0f, s * (0.9f + h * 0.95f), mastZ[i]), new Vector3(s * 0.03f, s * 0.48f * h, s * 0.03f));
            yard.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        }

        // Estandarte en lo alto del mástil mayor
        Piece(root, Cube, flag, new Vector3(s * 0.05f, s * 1.95f, mastZ[0]), new Vector3(s * 0.02f, s * 0.14f, s * 0.34f));
    }

    // Carro de comercio medieval: carreta cubierta con toldo del color de la civilización, barriles y un caballo de tiro
    static void BuildMedievalWagon(Transform root, float s, Color civ)
    {
        Material wood = LitMaterial(new Color(0.42f, 0.28f, 0.15f));
        Material woodLight = LitMaterial(new Color(0.62f, 0.47f, 0.28f));
        Material canvas = LitMaterial(civ);
        Material dark = LitMaterial(new Color(0.2f, 0.2f, 0.22f));
        Material barrel = LitMaterial(new Color(0.5f, 0.33f, 0.17f));

        BuildHorseModel(root, s, s * 1.15f);

        Piece(root, Cube, woodLight, new Vector3(0f, s * 0.32f, -s * 0.05f), new Vector3(s * 0.55f, s * 0.14f, s * 0.9f));  // caja
        GameObject roof = Piece(root, Cylinder, canvas, new Vector3(0f, s * 0.55f, -s * 0.05f), new Vector3(s * 0.48f, s * 0.38f, s * 0.48f)); // toldo en arco
        roof.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        Piece(root, Cube, wood, new Vector3(0f, s * 0.32f, s * 0.6f), new Vector3(s * 0.06f, s * 0.06f, s * 0.7f)); // lanza de tiro

        // Barriles detrás
        Piece(root, Cylinder, barrel, new Vector3(-s * 0.12f, s * 0.46f, -s * 0.55f), new Vector3(s * 0.14f, s * 0.12f, s * 0.14f));
        Piece(root, Cylinder, barrel, new Vector3(s * 0.12f, s * 0.46f, -s * 0.55f), new Vector3(s * 0.14f, s * 0.12f, s * 0.14f));

        // Ruedas con cubo
        foreach (float side in new[] { -1f, 1f })
        {
            GameObject wheel = Piece(root, Cylinder, dark, new Vector3(side * s * 0.33f, s * 0.2f, -s * 0.05f), new Vector3(s * 0.4f, s * 0.03f, s * 0.4f));
            wheel.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            GameObject hub = Piece(root, Cylinder, wood, new Vector3(side * s * 0.36f, s * 0.2f, -s * 0.05f), new Vector3(s * 0.1f, s * 0.03f, s * 0.1f));
            hub.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        }
    }

    // Carro de guerra medieval: carreta con paveses de madera y escudos del color de la civilización, caballo de tiro,
    // estandarte y un soldado de pie
    static void BuildMedievalWarWagon(Transform root, float s, Color civ)
    {
        Material wood = LitMaterial(new Color(0.5f, 0.34f, 0.18f));
        Material woodDark = LitMaterial(new Color(0.3f, 0.2f, 0.1f));
        Material shield = LitMaterial(civ);
        Material iron = LitMaterial(new Color(0.55f, 0.57f, 0.6f));

        BuildHorseModel(root, s, s * 0.95f);

        Piece(root, Cube, wood, new Vector3(0f, s * 0.3f, -s * 0.25f), new Vector3(s * 0.6f, s * 0.07f, s * 0.8f));       // plataforma
        for (int side = -1; side <= 1; side += 2)
        {
            Piece(root, Cube, woodDark, new Vector3(side * s * 0.3f, s * 0.46f, -s * 0.25f), new Vector3(s * 0.04f, s * 0.26f, s * 0.8f)); // costado
            for (int i = 0; i < 3; i++)
            {
                GameObject disc = Piece(root, Cylinder, shield, new Vector3(side * s * 0.33f, s * 0.46f, -s * (0.05f + i * 0.28f)), new Vector3(s * 0.22f, s * 0.015f, s * 0.22f));
                disc.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                Piece(root, Sphere, iron, new Vector3(side * s * 0.345f, s * 0.46f, -s * (0.05f + i * 0.28f)), Vector3.one * s * 0.05f);
            }
        }
        Piece(root, Cube, woodDark, new Vector3(0f, s * 0.45f, -s * 0.65f), new Vector3(s * 0.6f, s * 0.24f, s * 0.04f));
        Piece(root, Cube, woodDark, new Vector3(0f, s * 0.3f, s * 0.35f), new Vector3(s * 0.06f, s * 0.05f, s * 0.7f)); // lanza de tiro

        foreach (float side in new[] { -1f, 1f })
        {
            GameObject wheel = Piece(root, Cylinder, woodDark, new Vector3(side * s * 0.36f, s * 0.2f, -s * 0.25f), new Vector3(s * 0.42f, s * 0.03f, s * 0.42f));
            wheel.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        }

        // Estandarte y soldado de pie en la plataforma
        Piece(root, Cylinder, woodDark, new Vector3(0f, s * 0.85f, -s * 0.6f), new Vector3(s * 0.03f, s * 0.5f, s * 0.03f));
        Piece(root, Cube, shield, new Vector3(0f, s * 1.1f, -s * 0.5f), new Vector3(s * 0.02f, s * 0.2f, s * 0.3f));

        GameObject soldier = new GameObject("Soldado");
        soldier.transform.SetParent(root, false);
        soldier.transform.localPosition = new Vector3(0f, s * 0.33f, -s * 0.2f);
        soldier.transform.localScale = Vector3.one * 0.7f;
        BuildMedievalSoldier(soldier.transform, s, civ, 0);
    }

    // Dibuja la versión medieval de la unidad (todas las unidades tienen una)
    static void BuildMedievalUnit(Transform root, float size, Unit unit)
    {
        Color civ = unit.owner.color;
        float s = size;

        switch (unit.kind)
        {
            case UnitKind.Settler:   BuildMedievalSettler(root, s, civ); break;
            case UnitKind.Worker:    BuildMedievalWorker(root, s, civ); break;
            case UnitKind.Spearman:  BuildMedievalSoldier(root, s, civ, 1); break;
            case UnitKind.Phalanx:   BuildMedievalSoldier(root, s, civ, 2); break;
            case UnitKind.Archer:    BuildMedievalArcher(root, s, civ); break;
            case UnitKind.Legion:    BuildMedievalKnight(root, s, civ, false); break;
            case UnitKind.Catapult:  BuildMedievalTrebuchet(root, s, civ); break;
            case UnitKind.Sailboat:  BuildMedievalCog(root, s, civ); break;
            case UnitKind.Galleon:   BuildGalleon(root, s, civ); break;
            case UnitKind.Musketeer: BuildMusketeer(root, s, civ); break;
            case UnitKind.Rifleman:  BuildRifleman(root, s, civ); break;
            case UnitKind.Grenadier: BuildGrenadier(root, s, civ); break;
            case UnitKind.Cannon:    BuildCannon(root, s, civ); break;
            case UnitKind.Steamship: BuildSteamship(root, s, civ); break;
            case UnitKind.Crossbowman: BuildCrossbowman(root, s, civ); break;
            case UnitKind.Ballista:  BuildBallista(root, s, civ); break;
            case UnitKind.Caravan:
            case UnitKind.Merchant:  BuildMedievalWagon(root, s, civ); break;
            case UnitKind.Chariot:   BuildMedievalWarWagon(root, s, civ); break;

            case UnitKind.Horseman:
            {
                // Caballo con gualdrapa del color de la civilización y un caballero con lanza a lomos
                BuildHorseModel(root, s, 0f);
                Material caparison = LitMaterial(civ);
                Piece(root, Cube, caparison, new Vector3(-s * 0.16f, s * 0.32f, 0f), new Vector3(s * 0.03f, s * 0.28f, s * 0.5f));
                Piece(root, Cube, caparison, new Vector3(s * 0.16f, s * 0.32f, 0f), new Vector3(s * 0.03f, s * 0.28f, s * 0.5f));
                Piece(root, Cube, caparison, new Vector3(0f, s * 0.53f, 0f), new Vector3(s * 0.32f, s * 0.03f, s * 0.5f));

                GameObject rider = new GameObject("Caballero");
                rider.transform.SetParent(root, false);
                rider.transform.localPosition = new Vector3(0f, s * 0.5f, -s * 0.05f);
                rider.transform.localScale = Vector3.one * 0.75f;
                BuildMedievalKnight(rider.transform, s, civ, true);
                break;
            }

            default:                 BuildMedievalSoldier(root, s, civ, 0); break;
        }
    }

    // ---------------------------------------------------------------- Unidades contemporáneas
    // Tras el tercer Cambio de era (Edad Contemporánea) cada unidad se dibuja con su versión actual. Todas miran hacia +Z.

    static void ModWheel(Transform r, Material tyre, Material hub, float x, float y, float z, float diameter, float width)
    {
        GameObject w = Piece(r, Cylinder, tyre, new Vector3(x, y, z), new Vector3(diameter, width, diameter));
        w.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        GameObject h = Piece(r, Cylinder, hub, new Vector3(x + Mathf.Sign(x) * width * 0.6f, y, z), new Vector3(diameter * 0.5f, width * 0.2f, diameter * 0.5f));
        h.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
    }

    static GameObject ModTube(Transform r, Material m, Vector3 from, Vector3 to, float thickness)
    {
        GameObject t = Piece(r, Cylinder, m, (from + to) * 0.5f, new Vector3(thickness, (to - from).magnitude * 0.5f, thickness));
        t.transform.localRotation = Quaternion.FromToRotation(Vector3.up, to - from);
        return t;
    }

    // Soldado actual. kind: 0 = fusilero (casco, chaleco y fusil de asalto), 1 = infantería pesada (escudo antibalas),
    // 2 = granadero, 3 = tirador de precisión (fusil largo con mira)
    static void BuildModernSoldier(Transform root, float s, Color civ, int kind)
    {
        Color olive = new Color(0.3f, 0.35f, 0.24f);
        Material uni = LitMaterial(Color.Lerp(civ, olive, 0.65f));
        Material pants = LitMaterial(Color.Lerp(civ, olive, 0.8f));
        Material boots = LitMaterial(new Color(0.1f, 0.09f, 0.08f));
        Material skin = LitMaterial(new Color(0.9f, 0.72f, 0.56f));
        Material helmet = LitMaterial(Color.Lerp(civ, olive, 0.75f));
        Material vest = LitMaterial(new Color(0.16f, 0.18f, 0.16f));
        Material gun = LitMaterial(new Color(0.08f, 0.08f, 0.09f));
        Material patch = LitMaterial(civ);
        Material glass = LitMaterial(new Color(0.5f, 0.7f, 0.85f));
        Material grenade = LitMaterial(new Color(0.25f, 0.3f, 0.2f));

        MedLegs(root, s, pants, boots);
        MedTorso(root, s, uni, vest, false);
        MedHead(root, s, skin);

        // Casco con parche de la civilización, chaleco antibalas y brazalete
        Piece(root, Sphere, helmet, new Vector3(0f, s * 0.93f, -s * 0.01f), new Vector3(s * 0.25f, s * 0.17f, s * 0.26f));
        Piece(root, Cube, patch, new Vector3(0f, s * 0.95f, s * 0.12f), new Vector3(s * 0.07f, s * 0.05f, s * 0.01f));
        Piece(root, Cube, vest, new Vector3(0f, s * 0.6f, s * 0.12f), new Vector3(s * 0.3f, s * 0.3f, s * 0.05f));
        Piece(root, Cube, vest, new Vector3(0f, s * 0.6f, -s * 0.12f), new Vector3(s * 0.3f, s * 0.3f, s * 0.05f));
        Piece(root, Cylinder, patch, new Vector3(-s * 0.25f, s * 0.62f, s * 0.02f), new Vector3(s * 0.1f, s * 0.025f, s * 0.1f));

        switch (kind)
        {
            case 1:
            {
                // Escudo balístico transparente con marco de la civilización y una pistola
                MedHand(root, s, skin, -s * 0.28f, s * 0.55f, s * 0.2f);
                Piece(root, Cube, patch, new Vector3(-s * 0.28f, s * 0.58f, s * 0.3f), new Vector3(s * 0.42f, s * 0.62f, s * 0.03f));
                Piece(root, Cube, glass, new Vector3(-s * 0.28f, s * 0.6f, s * 0.32f), new Vector3(s * 0.34f, s * 0.5f, s * 0.03f));
                MedHand(root, s, skin, s * 0.2f, s * 0.55f, s * 0.25f);
                Piece(root, Cube, gun, new Vector3(s * 0.2f, s * 0.57f, s * 0.33f), new Vector3(s * 0.04f, s * 0.05f, s * 0.2f));
                break;
            }
            case 2:
            {
                // Fusil corto y una granada de mano en alto
                MedHand(root, s, skin, s * 0.2f, s * 0.5f, s * 0.2f);
                Piece(root, Cube, gun, new Vector3(s * 0.2f, s * 0.52f, s * 0.3f), new Vector3(s * 0.04f, s * 0.06f, s * 0.45f));
                MedHand(root, s, skin, -s * 0.2f, s * 0.7f, s * 0.25f);
                Piece(root, Sphere, grenade, new Vector3(-s * 0.2f, s * 0.78f, s * 0.25f), new Vector3(s * 0.12f, s * 0.14f, s * 0.12f));
                Piece(root, Cube, gun, new Vector3(-s * 0.2f, s * 0.87f, s * 0.25f), new Vector3(s * 0.03f, s * 0.03f, s * 0.05f));
                Piece(root, Sphere, grenade, new Vector3(s * 0.12f, s * 0.45f, s * 0.15f), new Vector3(s * 0.07f, s * 0.08f, s * 0.07f));
                break;
            }
            case 3:
            {
                // Fusil de precisión con mira telescópica y bípode
                MedHand(root, s, skin, s * 0.2f, s * 0.5f, s * 0.2f);
                MedHand(root, s, skin, s * 0.2f, s * 0.54f, s * 0.5f);
                Piece(root, Cube, gun, new Vector3(s * 0.2f, s * 0.52f, s * 0.15f), new Vector3(s * 0.05f, s * 0.08f, s * 0.3f));
                Piece(root, Cube, gun, new Vector3(s * 0.2f, s * 0.54f, s * 0.7f), new Vector3(s * 0.025f, s * 0.025f, s * 0.95f));
                GameObject scope = Piece(root, Cylinder, gun, new Vector3(s * 0.2f, s * 0.62f, s * 0.4f), new Vector3(s * 0.04f, s * 0.12f, s * 0.04f));
                scope.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                Piece(root, Cube, gun, new Vector3(s * 0.2f, s * 0.47f, s * 0.95f), new Vector3(s * 0.1f, s * 0.01f, s * 0.01f));
                break;
            }
            default:
            {
                // Fusil de asalto con cargador y culata
                MedHand(root, s, skin, s * 0.2f, s * 0.5f, s * 0.18f);
                MedHand(root, s, skin, s * 0.2f, s * 0.54f, s * 0.42f);
                Piece(root, Cube, gun, new Vector3(s * 0.2f, s * 0.53f, s * 0.3f), new Vector3(s * 0.045f, s * 0.07f, s * 0.55f));
                Piece(root, Cube, gun, new Vector3(s * 0.2f, s * 0.54f, s * 0.7f), new Vector3(s * 0.02f, s * 0.02f, s * 0.25f));
                Piece(root, Cube, gun, new Vector3(s * 0.2f, s * 0.44f, s * 0.32f), new Vector3(s * 0.035f, s * 0.14f, s * 0.07f));
                break;
            }
        }
    }

    // Colono actual: ropa de calle del color de la civilización, gorra, mochila y una maleta de ruedas
    static void BuildModernSettler(Transform root, float s, Color civ)
    {
        Material jacket = LitMaterial(civ);
        Material jeans = LitMaterial(new Color(0.18f, 0.25f, 0.4f));
        Material shoes = LitMaterial(new Color(0.9f, 0.9f, 0.9f));
        Material skin = LitMaterial(new Color(0.9f, 0.72f, 0.56f));
        Material cap = LitMaterial(Color.Lerp(civ, Color.white, 0.5f));
        Material pack = LitMaterial(new Color(0.2f, 0.2f, 0.22f));
        Material suitcase = LitMaterial(new Color(0.7f, 0.72f, 0.76f));
        Material dark = LitMaterial(new Color(0.1f, 0.1f, 0.1f));

        MedLegs(root, s, jeans, shoes);
        MedTorso(root, s, jacket, dark, false);
        MedHead(root, s, skin);
        Piece(root, Sphere, cap, new Vector3(0f, s * 0.94f, 0f), new Vector3(s * 0.23f, s * 0.14f, s * 0.23f));
        Piece(root, Cube, cap, new Vector3(0f, s * 0.9f, s * 0.14f), new Vector3(s * 0.2f, s * 0.015f, s * 0.12f));
        Piece(root, Cube, pack, new Vector3(0f, s * 0.58f, -s * 0.2f), new Vector3(s * 0.28f, s * 0.34f, s * 0.14f));

        // Maleta con asa extensible
        MedHand(root, s, skin, s * 0.26f, s * 0.46f, s * 0.12f);
        Piece(root, Cube, suitcase, new Vector3(s * 0.3f, s * 0.22f, s * 0.2f), new Vector3(s * 0.16f, s * 0.32f, s * 0.24f));
        Piece(root, Cylinder, dark, new Vector3(s * 0.3f, s * 0.42f, s * 0.12f), new Vector3(s * 0.025f, s * 0.12f, s * 0.025f));
    }

    // Trabajador actual: casco amarillo, chaleco reflectante naranja, vaqueros y un martillo neumático
    static void BuildModernWorker(Transform root, float s, Color civ)
    {
        Material shirt = LitMaterial(new Color(0.85f, 0.85f, 0.88f));
        Material vest = LitMaterial(new Color(1f, 0.5f, 0.1f));
        Material strip = LitMaterial(new Color(0.9f, 0.9f, 0.85f));
        Material hat = LitMaterial(new Color(1f, 0.85f, 0.1f));
        Material jeans = LitMaterial(new Color(0.2f, 0.27f, 0.42f));
        Material boots = LitMaterial(new Color(0.28f, 0.2f, 0.12f));
        Material skin = LitMaterial(new Color(0.9f, 0.7f, 0.54f));
        Material tool = LitMaterial(new Color(0.45f, 0.47f, 0.5f));
        Material patch = LitMaterial(civ);

        MedLegs(root, s, jeans, boots);
        MedTorso(root, s, shirt, vest, false);
        Piece(root, Cube, vest, new Vector3(0f, s * 0.6f, s * 0.12f), new Vector3(s * 0.3f, s * 0.3f, s * 0.04f));
        Piece(root, Cube, strip, new Vector3(0f, s * 0.58f, s * 0.145f), new Vector3(s * 0.3f, s * 0.03f, s * 0.01f));
        MedHead(root, s, skin);
        Piece(root, Sphere, hat, new Vector3(0f, s * 0.94f, 0f), new Vector3(s * 0.25f, s * 0.15f, s * 0.25f));
        Piece(root, Cube, patch, new Vector3(0f, s * 0.95f, s * 0.12f), new Vector3(s * 0.06f, s * 0.04f, s * 0.01f));

        // Martillo neumático
        MedHand(root, s, skin, s * 0.2f, s * 0.6f, s * 0.2f);
        Piece(root, Cylinder, tool, new Vector3(s * 0.2f, s * 0.38f, s * 0.2f), new Vector3(s * 0.07f, s * 0.26f, s * 0.07f));
        Piece(root, Cylinder, tool, new Vector3(s * 0.2f, s * 0.08f, s * 0.2f), new Vector3(s * 0.025f, s * 0.12f, s * 0.025f));
        Piece(root, Cube, tool, new Vector3(s * 0.2f, s * 0.66f, s * 0.2f), new Vector3(s * 0.22f, s * 0.03f, s * 0.03f));
    }

    // Todoterreno militar con ametralladora en el techo
    static void BuildModernJeep(Transform root, float s, Color civ)
    {
        Material body = LitMaterial(Color.Lerp(civ, new Color(0.3f, 0.35f, 0.24f), 0.6f));
        Material tyre = LitMaterial(new Color(0.08f, 0.08f, 0.08f));
        Material hub = LitMaterial(new Color(0.6f, 0.6f, 0.62f));
        Material glass = LitMaterial(new Color(0.5f, 0.7f, 0.85f));
        Material gun = LitMaterial(new Color(0.1f, 0.1f, 0.11f));
        Material flag = LitMaterial(civ);

        Piece(root, Cube, body, new Vector3(0f, s * 0.3f, 0f), new Vector3(s * 0.55f, s * 0.18f, s * 1.0f));
        Piece(root, Cube, body, new Vector3(0f, s * 0.45f, -s * 0.15f), new Vector3(s * 0.5f, s * 0.18f, s * 0.42f));
        GameObject wind = Piece(root, Cube, glass, new Vector3(0f, s * 0.5f, s * 0.08f), new Vector3(s * 0.46f, s * 0.2f, s * 0.02f));
        wind.transform.localRotation = Quaternion.Euler(-25f, 0f, 0f);
        for (int side = -1; side <= 1; side += 2)
        {
            ModWheel(root, tyre, hub, side * s * 0.3f, s * 0.15f, s * 0.32f, s * 0.3f, s * 0.12f);
            ModWheel(root, tyre, hub, side * s * 0.3f, s * 0.15f, -s * 0.32f, s * 0.3f, s * 0.12f);
        }
        Piece(root, Cube, gun, new Vector3(0f, s * 0.64f, -s * 0.12f), new Vector3(s * 0.06f, s * 0.08f, s * 0.14f));
        Piece(root, Cube, gun, new Vector3(0f, s * 0.66f, s * 0.12f), new Vector3(s * 0.025f, s * 0.025f, s * 0.35f));
        Piece(root, Cube, flag, new Vector3(0f, s * 0.58f, s * 0.46f), new Vector3(s * 0.2f, s * 0.04f, s * 0.04f));
    }

    // Camión: cabina y gran contenedor del color de la civilización. armed: camión blindado con torreta
    static void BuildModernTruck(Transform root, float s, Color civ, bool gold)
    {
        Material cab = LitMaterial(new Color(0.88f, 0.88f, 0.9f));
        Material box = LitMaterial(civ);
        Material stripe = LitMaterial(gold ? new Color(0.95f, 0.8f, 0.3f) : new Color(0.95f, 0.95f, 0.95f));
        Material tyre = LitMaterial(new Color(0.08f, 0.08f, 0.08f));
        Material hub = LitMaterial(new Color(0.6f, 0.6f, 0.62f));
        Material glass = LitMaterial(new Color(0.5f, 0.7f, 0.85f));
        Material chassis = LitMaterial(new Color(0.15f, 0.15f, 0.17f));

        Piece(root, Cube, chassis, new Vector3(0f, s * 0.2f, -s * 0.05f), new Vector3(s * 0.45f, s * 0.08f, s * 1.5f));
        Piece(root, Cube, cab, new Vector3(0f, s * 0.42f, s * 0.58f), new Vector3(s * 0.5f, s * 0.38f, s * 0.36f));
        Piece(root, Cube, glass, new Vector3(0f, s * 0.5f, s * 0.77f), new Vector3(s * 0.44f, s * 0.2f, s * 0.02f));
        Piece(root, Cube, box, new Vector3(0f, s * 0.5f, -s * 0.2f), new Vector3(s * 0.56f, s * 0.54f, s * 0.95f));
        Piece(root, Cube, stripe, new Vector3(0f, s * 0.5f, -s * 0.2f), new Vector3(s * 0.58f, s * 0.08f, s * 0.96f));
        for (int side = -1; side <= 1; side += 2)
        {
            ModWheel(root, tyre, hub, side * s * 0.3f, s * 0.15f, s * 0.58f, s * 0.3f, s * 0.12f);
            ModWheel(root, tyre, hub, side * s * 0.3f, s * 0.15f, -s * 0.3f, s * 0.3f, s * 0.12f);
            ModWheel(root, tyre, hub, side * s * 0.3f, s * 0.15f, -s * 0.6f, s * 0.3f, s * 0.12f);
        }
    }

    // Transporte blindado de ruedas con torreta y cañón automático
    static void BuildModernApc(Transform root, float s, Color civ)
    {
        Material body = LitMaterial(Color.Lerp(civ, new Color(0.3f, 0.35f, 0.24f), 0.6f));
        Material tyre = LitMaterial(new Color(0.08f, 0.08f, 0.08f));
        Material hub = LitMaterial(new Color(0.5f, 0.5f, 0.52f));
        Material gun = LitMaterial(new Color(0.1f, 0.1f, 0.11f));
        Material flag = LitMaterial(civ);

        Piece(root, Cube, body, new Vector3(0f, s * 0.32f, 0f), new Vector3(s * 0.6f, s * 0.26f, s * 1.3f));
        GameObject nose = Piece(root, Cube, body, new Vector3(0f, s * 0.38f, s * 0.7f), new Vector3(s * 0.56f, s * 0.2f, s * 0.3f));
        nose.transform.localRotation = Quaternion.Euler(-20f, 0f, 0f);
        Piece(root, Cylinder, body, new Vector3(0f, s * 0.54f, -s * 0.1f), new Vector3(s * 0.34f, s * 0.06f, s * 0.34f));
        GameObject barrel = Piece(root, Cylinder, gun, new Vector3(0f, s * 0.56f, s * 0.3f), new Vector3(s * 0.05f, s * 0.28f, s * 0.05f));
        barrel.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        for (int side = -1; side <= 1; side += 2)
            for (int i = 0; i < 4; i++)
                ModWheel(root, tyre, hub, side * s * 0.33f, s * 0.16f, s * (0.5f - i * 0.34f), s * 0.32f, s * 0.12f);
        Piece(root, Cube, flag, new Vector3(0f, s * 0.46f, s * 0.2f), new Vector3(s * 0.2f, s * 0.03f, s * 0.2f));
    }

    // Carro de combate: orugas, casco, torreta y largo cañón
    static void BuildModernTank(Transform root, float s, Color civ)
    {
        Material body = LitMaterial(Color.Lerp(civ, new Color(0.3f, 0.35f, 0.24f), 0.6f));
        Material track = LitMaterial(new Color(0.1f, 0.1f, 0.11f));
        Material gun = LitMaterial(new Color(0.12f, 0.12f, 0.13f));
        Material flag = LitMaterial(civ);

        for (int side = -1; side <= 1; side += 2)
        {
            Piece(root, Cube, track, new Vector3(side * s * 0.34f, s * 0.15f, 0f), new Vector3(s * 0.2f, s * 0.26f, s * 1.1f));
            Piece(root, Cylinder, track, new Vector3(side * s * 0.34f, s * 0.15f, s * 0.55f), new Vector3(s * 0.26f, s * 0.1f, s * 0.26f)).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            Piece(root, Cylinder, track, new Vector3(side * s * 0.34f, s * 0.15f, -s * 0.55f), new Vector3(s * 0.26f, s * 0.1f, s * 0.26f)).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        }
        Piece(root, Cube, body, new Vector3(0f, s * 0.3f, 0f), new Vector3(s * 0.56f, s * 0.14f, s * 1.0f));
        Piece(root, Cube, body, new Vector3(0f, s * 0.46f, -s * 0.05f), new Vector3(s * 0.4f, s * 0.14f, s * 0.5f));
        Piece(root, Cylinder, body, new Vector3(0f, s * 0.55f, -s * 0.05f), new Vector3(s * 0.2f, s * 0.04f, s * 0.2f));
        GameObject barrel = Piece(root, Cylinder, gun, new Vector3(0f, s * 0.49f, s * 0.55f), new Vector3(s * 0.07f, s * 0.5f, s * 0.07f));
        barrel.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        Piece(root, Cube, flag, new Vector3(0f, s * 0.6f, -s * 0.2f), new Vector3(s * 0.03f, s * 0.1f, s * 0.14f));
    }

    // Obús remolcado: dos ruedas, patas de apoyo y un largo tubo elevado
    static void BuildModernHowitzer(Transform root, float s, Color civ)
    {
        Material body = LitMaterial(Color.Lerp(civ, new Color(0.3f, 0.35f, 0.24f), 0.6f));
        Material tyre = LitMaterial(new Color(0.08f, 0.08f, 0.08f));
        Material hub = LitMaterial(new Color(0.55f, 0.55f, 0.58f));
        Material gun = LitMaterial(new Color(0.14f, 0.14f, 0.15f));
        Material flag = LitMaterial(civ);

        Piece(root, Cube, body, new Vector3(0f, s * 0.26f, 0f), new Vector3(s * 0.4f, s * 0.14f, s * 0.4f));
        for (int side = -1; side <= 1; side += 2)
        {
            ModWheel(root, tyre, hub, side * s * 0.3f, s * 0.2f, 0f, s * 0.38f, s * 0.1f);
            GameObject leg = Piece(root, Cube, gun, new Vector3(side * s * 0.15f, s * 0.14f, -s * 0.42f), new Vector3(s * 0.05f, s * 0.05f, s * 0.6f));
            leg.transform.localRotation = Quaternion.Euler(0f, side * 14f, 0f);
        }
        Piece(root, Cube, body, new Vector3(0f, s * 0.38f, s * 0.02f), new Vector3(s * 0.34f, s * 0.14f, s * 0.2f)); // escudo
        GameObject barrel = Piece(root, Cylinder, gun, new Vector3(0f, s * 0.5f, s * 0.35f), new Vector3(s * 0.09f, s * 0.55f, s * 0.09f));
        barrel.transform.localRotation = Quaternion.Euler(65f, 0f, 0f);
        Piece(root, Cube, flag, new Vector3(0f, s * 0.42f, -s * 0.1f), new Vector3(s * 0.01f, s * 0.12f, s * 0.14f));
    }

    // Lanzamisiles móvil: camión con rampa inclinada y tres misiles con la punta del color de la civilización
    static void BuildModernMissileLauncher(Transform root, float s, Color civ)
    {
        BuildModernTruck(root, s, Color.Lerp(civ, new Color(0.3f, 0.35f, 0.24f), 0.6f), false);
        Material rail = LitMaterial(new Color(0.15f, 0.15f, 0.17f));
        Material missile = LitMaterial(new Color(0.88f, 0.88f, 0.9f));
        Material tip = LitMaterial(civ);

        GameObject ramp = Piece(root, Cube, rail, new Vector3(0f, s * 0.9f, -s * 0.25f), new Vector3(s * 0.5f, s * 0.04f, s * 0.95f));
        ramp.transform.localRotation = Quaternion.Euler(-35f, 0f, 0f);
        for (int i = -1; i <= 1; i++)
        {
            Vector3 from = new Vector3(i * s * 0.15f, s * 0.95f, -s * 0.5f);
            Vector3 to = new Vector3(i * s * 0.15f, s * 1.35f, s * 0.0f);
            ModTube(root, missile, from, to, s * 0.1f);
            GameObject cone = Piece(root, Sphere, tip, to, new Vector3(s * 0.1f, s * 0.12f, s * 0.1f));
            cone.transform.localRotation = Quaternion.FromToRotation(Vector3.up, to - from);
        }
    }

    // Lancha motora: casco con parabrisas, cabina y dos motores fueraborda con una franja de la civilización
    static void BuildModernMotorboat(Transform root, float s, Color civ)
    {
        Material hull = LitMaterial(new Color(0.9f, 0.9f, 0.92f));
        Material band = LitMaterial(civ);
        Material glass = LitMaterial(new Color(0.5f, 0.7f, 0.85f));
        Material engine = LitMaterial(new Color(0.12f, 0.12f, 0.13f));

        Piece(root, ShipHull(), hull, Vector3.zero, new Vector3(s * 0.8f, s * 0.8f, s * 1.3f));
        Piece(root, Cube, band, new Vector3(0f, s * 0.36f, 0f), new Vector3(s * 0.52f, s * 0.04f, s * 1.0f));
        Piece(root, Cube, hull, new Vector3(0f, s * 0.5f, -s * 0.1f), new Vector3(s * 0.34f, s * 0.2f, s * 0.4f));
        GameObject wind = Piece(root, Cube, glass, new Vector3(0f, s * 0.58f, s * 0.12f), new Vector3(s * 0.32f, s * 0.16f, s * 0.02f));
        wind.transform.localRotation = Quaternion.Euler(-30f, 0f, 0f);
        for (int side = -1; side <= 1; side += 2)
            Piece(root, Cube, engine, new Vector3(side * s * 0.12f, s * 0.38f, -s * 0.62f), new Vector3(s * 0.12f, s * 0.26f, s * 0.14f));
    }

    // Buque de guerra: casco gris, superestructura, cañón de proa, radar y chimenea con franja de la civilización.
    // big: crucero con dos cañones y más superestructura
    static void BuildModernWarship(Transform root, float s, Color civ, bool big)
    {
        Material hull = LitMaterial(new Color(0.5f, 0.54f, 0.58f));
        Material deck = LitMaterial(new Color(0.4f, 0.43f, 0.47f));
        Material superMat = LitMaterial(new Color(0.62f, 0.65f, 0.68f));
        Material dark = LitMaterial(new Color(0.12f, 0.12f, 0.13f));
        Material band = LitMaterial(civ);
        Material glass = LitMaterial(new Color(0.4f, 0.55f, 0.7f));

        float len = big ? 1.9f : 1.6f;
        Piece(root, ShipHull(), hull, Vector3.zero, new Vector3(s * 1.1f, s * 1.0f, s * len));
        Piece(root, Cube, deck, new Vector3(0f, s * 0.44f, 0f), new Vector3(s * 0.56f, s * 0.05f, s * (len - 0.5f)));

        // Superestructura con ventanas, chimenea y mástil con radar
        Piece(root, Cube, superMat, new Vector3(0f, s * 0.64f, -s * 0.1f), new Vector3(s * 0.4f, s * 0.34f, s * 0.6f));
        Piece(root, Cube, glass, new Vector3(0f, s * 0.7f, s * 0.21f), new Vector3(s * 0.34f, s * 0.08f, s * 0.02f));
        Piece(root, Cylinder, superMat, new Vector3(0f, s * 0.94f, -s * 0.3f), new Vector3(s * 0.2f, s * 0.14f, s * 0.2f));
        Piece(root, Cylinder, band, new Vector3(0f, s * 1.06f, -s * 0.3f), new Vector3(s * 0.21f, s * 0.03f, s * 0.21f));
        Piece(root, Cylinder, dark, new Vector3(0f, s * 1.2f, -s * 0.05f), new Vector3(s * 0.03f, s * 0.3f, s * 0.03f));
        GameObject dish = Piece(root, Cube, dark, new Vector3(0f, s * 1.5f, -s * 0.05f), new Vector3(s * 0.3f, s * 0.03f, s * 0.05f));
        dish.transform.localRotation = Quaternion.Euler(0f, 30f, 0f);

        // Cañones de proa en torretas (uno o dos)
        int turrets = big ? 2 : 1;
        for (int i = 0; i < turrets; i++)
        {
            float z = s * (0.5f + i * 0.28f);
            Piece(root, Cylinder, deck, new Vector3(0f, s * 0.53f, z), new Vector3(s * 0.22f, s * 0.05f, s * 0.22f));
            GameObject barrel = Piece(root, Cylinder, dark, new Vector3(0f, s * 0.56f, z + s * 0.2f), new Vector3(s * 0.04f, s * 0.22f, s * 0.04f));
            barrel.transform.localRotation = Quaternion.Euler(85f, 0f, 0f);
        }

        // Helipuerto en popa con el color de la civilización
        Piece(root, Cylinder, band, new Vector3(0f, s * 0.47f, -s * 0.62f), new Vector3(s * 0.34f, s * 0.01f, s * 0.34f));
    }

    // Avión: fuselaje, cabina, alas en flecha, cola y toberas, con franjas de la civilización.
    // bomber: bombardero grande de cuatro motores; si no, caza ágil de un solo reactor
    static void BuildModernAircraft(Transform root, float s, Color civ, bool bomber)
    {
        Material body = LitMaterial(bomber ? new Color(0.5f, 0.54f, 0.5f) : new Color(0.62f, 0.66f, 0.72f));
        Material glass = LitMaterial(new Color(0.25f, 0.4f, 0.55f));
        Material stripe = LitMaterial(civ);
        Material dark = LitMaterial(new Color(0.12f, 0.12f, 0.13f));

        float len = bomber ? 1.5f : 1.1f;
        float wing = bomber ? 0.7f : 0.45f;
        float y = s * 0.3f;

        // Fuselaje (cilindro tumbado a lo largo de Z), morro y cabina
        GameObject fus = Piece(root, Cylinder, body, new Vector3(0f, y, 0f), new Vector3(s * (bomber ? 0.2f : 0.13f), s * len * 0.5f, s * (bomber ? 0.2f : 0.13f)));
        fus.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        Piece(root, Sphere, body, new Vector3(0f, y, s * len * 0.5f), new Vector3(s * (bomber ? 0.2f : 0.13f), s * (bomber ? 0.2f : 0.13f), s * 0.3f));
        Piece(root, Sphere, glass, new Vector3(0f, y + s * 0.07f, s * len * 0.32f), new Vector3(s * 0.1f, s * 0.07f, s * 0.24f));

        // Alas en flecha con franja de la civilización en la punta
        for (int side = -1; side <= 1; side += 2)
        {
            GameObject w = Piece(root, Cube, body, new Vector3(side * s * wing * 0.5f, y - s * 0.01f, -s * 0.05f), new Vector3(s * wing, s * 0.015f, s * (bomber ? 0.3f : 0.26f)));
            w.transform.localRotation = Quaternion.Euler(0f, -side * 22f, 0f);
            GameObject tip = Piece(root, Cube, stripe, new Vector3(side * s * wing * 0.93f, y - s * 0.01f, -s * 0.05f - s * wing * 0.38f), new Vector3(s * wing * 0.16f, s * 0.018f, s * 0.14f));
            tip.transform.localRotation = Quaternion.Euler(0f, -side * 22f, 0f);
        }

        // Cola: deriva vertical y estabilizadores horizontales
        float tailZ = -s * len * 0.45f;
        Piece(root, Cube, stripe, new Vector3(0f, y + s * 0.15f, tailZ), new Vector3(s * 0.015f, s * 0.22f, s * 0.2f));
        for (int side = -1; side <= 1; side += 2)
        {
            GameObject st = Piece(root, Cube, body, new Vector3(side * s * 0.14f, y, tailZ - s * 0.03f), new Vector3(s * 0.26f, s * 0.012f, s * 0.14f));
            st.transform.localRotation = Quaternion.Euler(0f, -side * 20f, 0f);
        }

        // Motores: un reactor trasero en el caza; cuatro góndolas bajo las alas en el bombardero
        if (bomber)
        {
            for (int side = -1; side <= 1; side += 2)
                for (int i = 0; i < 2; i++)
                {
                    float x = side * s * (0.2f + i * 0.22f);
                    GameObject eng = Piece(root, Cylinder, dark, new Vector3(x, y - s * 0.06f, -s * (0.02f + i * 0.08f)), new Vector3(s * 0.06f, s * 0.12f, s * 0.06f));
                    eng.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                }
        }
        else
        {
            GameObject nozzle = Piece(root, Cylinder, dark, new Vector3(0f, y, -s * len * 0.5f - s * 0.02f), new Vector3(s * 0.09f, s * 0.05f, s * 0.09f));
            nozzle.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        }
    }

    // ---------------------------------------------------------------- Unidades fortificadas

    // Dibuja (o quita) la defensa que rodea a una unidad fortificada: empalizada de estacas (Edad Antigua), muro bajo de
    // piedra (Edad Media), sacos terreros (Edad Moderna y Contemporánea) o un cerco de luz con pilones (Edad Futura),
    // más una bandera del color de la civilización. Cuelga de la unidad, así que se mueve con ella.
    public static void SyncFortifyMarker(Unit u, WorldGrid grid)
    {
        if (u.view == null) return;

        if (!u.fortified && !u.fortifying)
        {
            if (u.fortifyView != null) Object.Destroy(u.fortifyView);
            u.fortifyView = null;
            return;
        }

        // Mientras se fortifica, la defensa "crece" desde el suelo hasta completarse
        float built = u.fortified ? 1f : Mathf.Clamp01(u.fortifyProgress / Unit.FortifySeconds);
        if (u.fortifyView != null)
        {
            u.fortifyView.transform.localScale = new Vector3(1f, 0.15f + 0.85f * built, 1f);
            return;
        }

        float size = grid.spacing * 0.35f;
        float radius = size * 0.62f;

        GameObject g = new GameObject("Fortificación");
        g.transform.SetParent(u.view.transform, false);
        u.fortifyView = g;
        g.transform.localScale = new Vector3(1f, 0.15f + 0.85f * built, 1f);
        Transform t = g.transform;

        Material dark = LitMaterial(FutGraphite);
        Material civ = LitMaterial(u.owner.color);

        if (u.owner.Has(TechKind.EraChange4))
        {
            // Futuro: cerco de luz y cuatro pilones con la punta luminosa
            Material glow = FutGlow(FutCyan, 2f);
            for (int i = 0; i < 14; i++)
            {
                float a = i / 14f * Mathf.PI * 2f;
                Piece(t, Sphere, glow, new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius + new Vector3(0f, size * 0.04f, 0f), Vector3.one * size * 0.06f);
            }
            for (int i = 0; i < 4; i++)
            {
                float a = i * Mathf.PI * 0.5f + Mathf.PI * 0.25f;
                Vector3 p = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius;
                Piece(t, Cylinder, dark, p + new Vector3(0f, size * 0.17f, 0f), new Vector3(size * 0.04f, size * 0.17f, size * 0.04f));
                Piece(t, Sphere, glow, p + new Vector3(0f, size * 0.36f, 0f), Vector3.one * size * 0.07f);
            }
        }
        else if (u.owner.Has(TechKind.EraChange2))
        {
            // Moderna y contemporánea: dos hileras de sacos terreros
            Material bag = LitMaterial(new Color(0.72f, 0.62f, 0.42f));
            for (int layer = 0; layer < 2; layer++)
                for (int i = 0; i < 12; i++)
                {
                    float a = (i + layer * 0.5f) / 12f * Mathf.PI * 2f;
                    Vector3 r = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    GameObject b = Piece(t, Sphere, bag, r * radius + new Vector3(0f, size * (0.06f + layer * 0.1f), 0f), new Vector3(size * 0.2f, size * 0.1f, size * 0.13f));
                    b.transform.localRotation = Quaternion.LookRotation(r, Vector3.up);
                }
        }
        else if (u.owner.Has(TechKind.EraChange))
        {
            // Edad Media: muro bajo de piedra
            Material stone = LitMaterial(new Color(0.58f, 0.57f, 0.55f));
            Material stoneDark = LitMaterial(new Color(0.42f, 0.41f, 0.4f));
            for (int i = 0; i < 12; i++)
            {
                float a = i / 12f * Mathf.PI * 2f;
                Vector3 r = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                GameObject b = Piece(t, Cube, i % 2 == 0 ? stone : stoneDark, r * radius + new Vector3(0f, size * 0.1f, 0f), new Vector3(size * 0.22f, size * 0.2f, size * 0.12f));
                b.transform.localRotation = Quaternion.LookRotation(r, Vector3.up);
            }
        }
        else
        {
            // Edad Antigua: empalizada de estacas inclinadas hacia fuera
            Material wood = LitMaterial(new Color(0.45f, 0.3f, 0.16f));
            for (int i = 0; i < 12; i++)
            {
                float a = i / 12f * Mathf.PI * 2f;
                Vector3 r = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                GameObject b = Piece(t, Cylinder, wood, r * radius + new Vector3(0f, size * 0.14f, 0f), new Vector3(size * 0.045f, size * 0.15f, size * 0.045f));
                b.transform.localRotation = Quaternion.AngleAxis(22f, Vector3.Cross(Vector3.up, r));
            }
        }

        // Bandera de la civilización detrás de la unidad
        Piece(t, Cylinder, dark, new Vector3(-radius * 0.55f, size * 0.3f, -radius * 0.55f), new Vector3(size * 0.015f, size * 0.3f, size * 0.015f));
        Piece(t, Cube, civ, new Vector3(-radius * 0.55f + size * 0.09f, size * 0.52f, -radius * 0.55f), new Vector3(size * 0.16f, size * 0.1f, size * 0.01f));
    }

    // ---------------------------------------------------------------- Unidades futuristas
    // Tras el cuarto Cambio de era (Edad Futura): armaduras blancas con luces de energía, vehículos antigravedad y naves.
    // Todas miran hacia +Z.

    static Material FutGlow(Color c, float k = 1.8f)
    {
        Material m = LitMaterial(c);
        m.EnableKeyword("_EMISSION");
        if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", c * k);
        return m;
    }

    static readonly Color FutCyan = new Color(0.3f, 0.9f, 1f);
    static readonly Color FutWhite = new Color(0.9f, 0.93f, 0.97f);
    static readonly Color FutGraphite = new Color(0.14f, 0.15f, 0.19f);

    // Soldado futurista. kind: 0 = trooper (armadura y rifle de plasma), 1 = pesado (escudo de energía),
    // 2 = granadero (orbe de plasma), 3 = francotirador (rifle de energía largo con mira)
    static void BuildFuturisticSoldier(Transform root, float s, Color civ, int kind)
    {
        Material armor = LitMaterial(FutWhite);
        Material dark = LitMaterial(FutGraphite);
        Material accent = LitMaterial(civ);
        Material glow = FutGlow(FutCyan);
        Material skin = LitMaterial(new Color(0.9f, 0.72f, 0.56f));

        MedLegs(root, s, dark, armor);
        MedTorso(root, s, armor, dark, false);
        MedHead(root, s, skin);

        // Casco con visera luminosa, placa pectoral del color de la civilización y mochila de energía
        Piece(root, Sphere, armor, new Vector3(0f, s * 0.9f, -s * 0.01f), new Vector3(s * 0.26f, s * 0.24f, s * 0.27f));
        Piece(root, Cube, glow, new Vector3(0f, s * 0.9f, s * 0.12f), new Vector3(s * 0.2f, s * 0.07f, s * 0.04f));
        Piece(root, Cube, accent, new Vector3(0f, s * 0.62f, s * 0.13f), new Vector3(s * 0.26f, s * 0.2f, s * 0.04f));
        Piece(root, Cube, dark, new Vector3(0f, s * 0.6f, -s * 0.18f), new Vector3(s * 0.26f, s * 0.3f, s * 0.12f));
        Piece(root, Cube, glow, new Vector3(0f, s * 0.6f, -s * 0.25f), new Vector3(s * 0.1f, s * 0.2f, s * 0.02f));
        for (int side = -1; side <= 1; side += 2)
            Piece(root, Cube, glow, new Vector3(side * s * 0.1f, s * 0.24f, s * 0.07f), new Vector3(s * 0.02f, s * 0.1f, s * 0.02f)); // luces de las rodillas

        switch (kind)
        {
            case 1:
            {
                // Escudo de energía translúcido y pistola de plasma
                MedHand(root, s, armor, -s * 0.28f, s * 0.55f, s * 0.2f);
                Piece(root, Cube, accent, new Vector3(-s * 0.28f, s * 0.6f, s * 0.3f), new Vector3(s * 0.46f, s * 0.7f, s * 0.02f));
                Piece(root, Cube, glow, new Vector3(-s * 0.28f, s * 0.6f, s * 0.315f), new Vector3(s * 0.4f, s * 0.62f, s * 0.02f));
                MedHand(root, s, armor, s * 0.2f, s * 0.55f, s * 0.25f);
                Piece(root, Cube, dark, new Vector3(s * 0.2f, s * 0.57f, s * 0.33f), new Vector3(s * 0.04f, s * 0.05f, s * 0.2f));
                Piece(root, Cube, glow, new Vector3(s * 0.2f, s * 0.57f, s * 0.44f), new Vector3(s * 0.025f, s * 0.025f, s * 0.04f));
                break;
            }
            case 2:
            {
                MedHand(root, s, armor, s * 0.2f, s * 0.5f, s * 0.2f);
                Piece(root, Cube, dark, new Vector3(s * 0.2f, s * 0.52f, s * 0.3f), new Vector3(s * 0.04f, s * 0.06f, s * 0.4f));
                MedHand(root, s, armor, -s * 0.2f, s * 0.7f, s * 0.25f);
                Piece(root, Sphere, glow, new Vector3(-s * 0.2f, s * 0.8f, s * 0.25f), Vector3.one * s * 0.14f);
                break;
            }
            case 3:
            {
                MedHand(root, s, armor, s * 0.2f, s * 0.5f, s * 0.2f);
                MedHand(root, s, armor, s * 0.2f, s * 0.54f, s * 0.5f);
                Piece(root, Cube, armor, new Vector3(s * 0.2f, s * 0.52f, s * 0.15f), new Vector3(s * 0.05f, s * 0.08f, s * 0.3f));
                Piece(root, Cube, dark, new Vector3(s * 0.2f, s * 0.54f, s * 0.7f), new Vector3(s * 0.03f, s * 0.03f, s * 0.95f));
                Piece(root, Cube, glow, new Vector3(s * 0.2f, s * 0.54f, s * 1.18f), new Vector3(s * 0.02f, s * 0.02f, s * 0.04f));
                GameObject scope = Piece(root, Cylinder, glow, new Vector3(s * 0.2f, s * 0.62f, s * 0.4f), new Vector3(s * 0.04f, s * 0.1f, s * 0.04f));
                scope.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                break;
            }
            default:
            {
                // Rifle de plasma con cañón luminoso
                MedHand(root, s, armor, s * 0.2f, s * 0.5f, s * 0.18f);
                MedHand(root, s, armor, s * 0.2f, s * 0.54f, s * 0.42f);
                Piece(root, Cube, armor, new Vector3(s * 0.2f, s * 0.53f, s * 0.3f), new Vector3(s * 0.05f, s * 0.08f, s * 0.5f));
                Piece(root, Cube, dark, new Vector3(s * 0.2f, s * 0.54f, s * 0.68f), new Vector3(s * 0.03f, s * 0.03f, s * 0.3f));
                Piece(root, Cube, glow, new Vector3(s * 0.2f, s * 0.54f, s * 0.84f), new Vector3(s * 0.025f, s * 0.025f, s * 0.05f));
                break;
            }
        }
    }

    // Colono futurista: traje del color de la civilización con casco burbuja, mochila propulsora y maleta flotante
    static void BuildFuturisticSettler(Transform root, float s, Color civ)
    {
        Material suit = LitMaterial(civ);
        Material white = LitMaterial(FutWhite);
        Material dark = LitMaterial(FutGraphite);
        Material glow = FutGlow(FutCyan);
        Material skin = LitMaterial(new Color(0.9f, 0.72f, 0.56f));

        MedLegs(root, s, suit, white);
        MedTorso(root, s, suit, dark, false);
        MedHead(root, s, skin);
        Piece(root, Sphere, white, new Vector3(0f, s * 0.9f, 0f), new Vector3(s * 0.3f, s * 0.28f, s * 0.3f)); // casco burbuja
        Piece(root, Cube, glow, new Vector3(0f, s * 0.9f, s * 0.14f), new Vector3(s * 0.22f, s * 0.08f, s * 0.03f));
        Piece(root, Cube, dark, new Vector3(0f, s * 0.58f, -s * 0.2f), new Vector3(s * 0.28f, s * 0.34f, s * 0.14f)); // mochila propulsora
        for (int side = -1; side <= 1; side += 2)
            Piece(root, Cylinder, glow, new Vector3(side * s * 0.08f, s * 0.38f, -s * 0.2f), new Vector3(s * 0.05f, s * 0.02f, s * 0.05f));

        // Maleta flotante con base luminosa
        MedHand(root, s, white, s * 0.27f, s * 0.46f, s * 0.12f);
        Piece(root, Cube, white, new Vector3(s * 0.32f, s * 0.3f, s * 0.2f), new Vector3(s * 0.16f, s * 0.2f, s * 0.24f));
        Piece(root, Cylinder, glow, new Vector3(s * 0.32f, s * 0.17f, s * 0.2f), new Vector3(s * 0.12f, s * 0.01f, s * 0.12f));
    }

    // Trabajador futurista: robot con cuerpo de grafito, franja del color de la civilización, visor y herramienta de haz de energía
    static void BuildFuturisticWorker(Transform root, float s, Color civ)
    {
        Material dark = LitMaterial(FutGraphite);
        Material white = LitMaterial(FutWhite);
        Material accent = LitMaterial(civ);
        Material glow = FutGlow(FutCyan);

        for (int side = -1; side <= 1; side += 2)
        {
            Piece(root, Cube, dark, new Vector3(side * s * 0.1f, s * 0.16f, 0f), new Vector3(s * 0.09f, s * 0.32f, s * 0.12f));
            Piece(root, Cube, white, new Vector3(side * s * 0.1f, s * 0.03f, s * 0.04f), new Vector3(s * 0.12f, s * 0.06f, s * 0.2f));
        }
        Piece(root, Cube, white, new Vector3(0f, s * 0.5f, 0f), new Vector3(s * 0.36f, s * 0.36f, s * 0.26f));
        Piece(root, Cube, accent, new Vector3(0f, s * 0.5f, s * 0.14f), new Vector3(s * 0.38f, s * 0.08f, s * 0.02f));
        Piece(root, Cube, dark, new Vector3(0f, s * 0.78f, 0f), new Vector3(s * 0.22f, s * 0.18f, s * 0.2f));
        Piece(root, Cube, glow, new Vector3(0f, s * 0.8f, s * 0.11f), new Vector3(s * 0.18f, s * 0.05f, s * 0.02f)); // visor
        Piece(root, Cylinder, dark, new Vector3(0f, s * 0.92f, 0f), new Vector3(s * 0.01f, s * 0.07f, s * 0.01f)); // antena
        for (int side = -1; side <= 1; side += 2)
            Piece(root, Cube, dark, new Vector3(side * s * 0.26f, s * 0.5f, s * 0.04f), new Vector3(s * 0.08f, s * 0.26f, s * 0.08f));

        // Herramienta: soplete de energía en el brazo derecho
        Piece(root, Cube, dark, new Vector3(s * 0.26f, s * 0.36f, s * 0.2f), new Vector3(s * 0.07f, s * 0.07f, s * 0.2f));
        Piece(root, Cube, glow, new Vector3(s * 0.26f, s * 0.36f, s * 0.34f), new Vector3(s * 0.03f, s * 0.03f, s * 0.08f));
    }

    // Base de vehículo antigravedad: casco aerodinámico y cuatro discos de empuje luminosos bajo el chasis
    static void FutHoverBase(Transform root, float s, Material body, Material glow, float width, float length)
    {
        Piece(root, Cube, body, new Vector3(0f, s * 0.2f, 0f), new Vector3(s * width, s * 0.14f, s * length));
        for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2)
                Piece(root, Cylinder, glow, new Vector3(sx * s * width * 0.32f, s * 0.1f, sz * s * length * 0.32f), new Vector3(s * 0.16f, s * 0.012f, s * 0.16f));
        Piece(root, Cube, glow, new Vector3(0f, s * 0.2f, 0f), new Vector3(s * (width + 0.02f), s * 0.025f, s * (length + 0.02f)));
    }

    // Moto antigravedad (Jinete): chasis delgado, parabrisas luminoso y dos propulsores traseros
    static void BuildFuturisticHoverbike(Transform root, float s, Color civ)
    {
        Material body = LitMaterial(FutWhite);
        Material accent = LitMaterial(civ);
        Material glow = FutGlow(FutCyan);
        Material dark = LitMaterial(FutGraphite);

        FutHoverBase(root, s, body, glow, 0.3f, 0.95f);
        Piece(root, Cube, accent, new Vector3(0f, s * 0.3f, -s * 0.05f), new Vector3(s * 0.22f, s * 0.1f, s * 0.4f));
        GameObject wind = Piece(root, Cube, glow, new Vector3(0f, s * 0.38f, s * 0.2f), new Vector3(s * 0.2f, s * 0.12f, s * 0.02f));
        wind.transform.localRotation = Quaternion.Euler(-35f, 0f, 0f);
        Piece(root, Cube, dark, new Vector3(0f, s * 0.36f, s * 0.22f), new Vector3(s * 0.4f, s * 0.02f, s * 0.02f)); // manillar
        for (int side = -1; side <= 1; side += 2)
            Piece(root, Cylinder, glow, new Vector3(side * s * 0.1f, s * 0.26f, -s * 0.5f), new Vector3(s * 0.07f, s * 0.01f, s * 0.07f)).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
    }

    // Transporte blindado antigravedad con torreta de plasma
    static void BuildFuturisticApc(Transform root, float s, Color civ)
    {
        Material body = LitMaterial(FutWhite);
        Material accent = LitMaterial(civ);
        Material glow = FutGlow(FutCyan);
        Material dark = LitMaterial(FutGraphite);

        FutHoverBase(root, s, body, glow, 0.62f, 1.25f);
        Piece(root, Cube, body, new Vector3(0f, s * 0.38f, -s * 0.1f), new Vector3(s * 0.5f, s * 0.22f, s * 0.8f));
        Piece(root, Cube, accent, new Vector3(0f, s * 0.5f, -s * 0.1f), new Vector3(s * 0.52f, s * 0.03f, s * 0.82f));
        GameObject nose = Piece(root, Cube, glow, new Vector3(0f, s * 0.4f, s * 0.36f), new Vector3(s * 0.4f, s * 0.07f, s * 0.04f));
        nose.transform.localRotation = Quaternion.Euler(-25f, 0f, 0f);
        Piece(root, Sphere, dark, new Vector3(0f, s * 0.56f, -s * 0.05f), new Vector3(s * 0.28f, s * 0.14f, s * 0.28f));
        GameObject barrel = Piece(root, Cylinder, dark, new Vector3(0f, s * 0.58f, s * 0.24f), new Vector3(s * 0.04f, s * 0.2f, s * 0.04f));
        barrel.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        Piece(root, Sphere, glow, new Vector3(0f, s * 0.58f, s * 0.45f), Vector3.one * s * 0.05f);
    }

    // Camión de carga antigravedad: cabina y contenedor del color de la civilización con franja de energía
    static void BuildFuturisticCargo(Transform root, float s, Color civ, bool gold)
    {
        Material body = LitMaterial(FutWhite);
        Material box = LitMaterial(civ);
        Material glow = FutGlow(gold ? new Color(1f, 0.85f, 0.3f) : FutCyan);
        Material dark = LitMaterial(FutGraphite);

        FutHoverBase(root, s, body, glow, 0.55f, 1.5f);
        Piece(root, Cube, body, new Vector3(0f, s * 0.4f, s * 0.55f), new Vector3(s * 0.5f, s * 0.3f, s * 0.36f));
        GameObject wind = Piece(root, Cube, glow, new Vector3(0f, s * 0.45f, s * 0.74f), new Vector3(s * 0.42f, s * 0.12f, s * 0.02f));
        wind.transform.localRotation = Quaternion.Euler(-20f, 0f, 0f);
        Piece(root, Cube, box, new Vector3(0f, s * 0.46f, -s * 0.22f), new Vector3(s * 0.56f, s * 0.46f, s * 0.9f));
        Piece(root, Cube, glow, new Vector3(0f, s * 0.46f, -s * 0.22f), new Vector3(s * 0.58f, s * 0.05f, s * 0.92f));
        Piece(root, Cube, dark, new Vector3(0f, s * 0.7f, -s * 0.22f), new Vector3(s * 0.4f, s * 0.03f, s * 0.7f));
    }

    // Carro de combate antigravedad: casco afilado, torreta esférica y cañón con la punta luminosa.
    // kind: 0 = tanque, 1 = cañón de plasma (obús con bobinas y cañón muy largo)
    static void BuildFuturisticHoverTank(Transform root, float s, Color civ, int kind)
    {
        Material body = LitMaterial(FutWhite);
        Material accent = LitMaterial(civ);
        Material glow = FutGlow(FutCyan);
        Material dark = LitMaterial(FutGraphite);

        FutHoverBase(root, s, body, glow, 0.7f, 1.1f);
        Piece(root, Cube, body, new Vector3(0f, s * 0.34f, 0f), new Vector3(s * 0.55f, s * 0.14f, s * 0.8f));
        Piece(root, Cube, accent, new Vector3(0f, s * 0.42f, -s * 0.1f), new Vector3(s * 0.4f, s * 0.04f, s * 0.4f));
        Piece(root, Sphere, dark, new Vector3(0f, s * 0.5f, -s * 0.05f), new Vector3(s * 0.34f, s * 0.2f, s * 0.34f));

        float len = kind == 1 ? 1.1f : 0.8f;
        GameObject barrel = Piece(root, Cylinder, dark, new Vector3(0f, s * 0.52f, s * (0.1f + len * 0.5f)), new Vector3(s * 0.06f, s * len * 0.5f, s * 0.06f));
        barrel.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        Piece(root, Sphere, glow, new Vector3(0f, s * 0.52f, s * (0.1f + len)), Vector3.one * s * 0.07f);
        if (kind == 1)
            for (int i = 0; i < 3; i++)
            {
                GameObject coil = Piece(root, Cylinder, glow, new Vector3(0f, s * 0.52f, s * (0.3f + i * 0.28f)), new Vector3(s * 0.11f, s * 0.012f, s * 0.11f));
                coil.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            }
    }

    // Lanzamisiles antigravedad: plataforma con rampa y tres misiles de plasma
    static void BuildFuturisticLauncher(Transform root, float s, Color civ)
    {
        Material body = LitMaterial(FutWhite);
        Material glow = FutGlow(FutCyan);
        Material dark = LitMaterial(FutGraphite);
        Material tip = FutGlow(civ);

        FutHoverBase(root, s, body, glow, 0.62f, 1.2f);
        GameObject ramp = Piece(root, Cube, dark, new Vector3(0f, s * 0.5f, -s * 0.1f), new Vector3(s * 0.5f, s * 0.04f, s * 0.9f));
        ramp.transform.localRotation = Quaternion.Euler(-35f, 0f, 0f);
        for (int i = -1; i <= 1; i++)
        {
            Vector3 from = new Vector3(i * s * 0.15f, s * 0.4f, -s * 0.4f);
            Vector3 to = new Vector3(i * s * 0.15f, s * 0.85f, s * 0.15f);
            ModTube(root, body, from, to, s * 0.1f);
            Piece(root, Sphere, tip, to, new Vector3(s * 0.1f, s * 0.12f, s * 0.1f));
        }
    }

    // Caza o bombardero futurista: ala delta de fuselaje luminoso. bomber: ala volante grande con tres motores
    static void BuildFuturisticAircraft(Transform root, float s, Color civ, bool bomber)
    {
        Material body = LitMaterial(FutWhite);
        Material accent = LitMaterial(civ);
        Material glow = FutGlow(FutCyan);
        Material dark = LitMaterial(FutGraphite);

        float y = s * 0.3f;
        float span = bomber ? 0.85f : 0.55f;
        float len = bomber ? 1.2f : 1.0f;

        // Fuselaje central alargado con morro
        GameObject fus = Piece(root, Capsule, body, new Vector3(0f, y, 0f), new Vector3(s * (bomber ? 0.22f : 0.13f), s * len * 0.5f, s * (bomber ? 0.14f : 0.1f)));
        fus.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        Piece(root, Cube, glow, new Vector3(0f, y + s * 0.07f, s * len * 0.25f), new Vector3(s * 0.08f, s * 0.03f, s * 0.2f)); // cabina luminosa

        // Alas en delta (dos placas en flecha) con bordes luminosos
        for (int side = -1; side <= 1; side += 2)
        {
            GameObject w = Piece(root, Cube, body, new Vector3(side * s * span * 0.45f, y - s * 0.01f, -s * 0.12f), new Vector3(s * span, s * 0.015f, s * (bomber ? 0.5f : 0.4f)));
            w.transform.localRotation = Quaternion.Euler(0f, -side * 30f, 0f);
            GameObject e = Piece(root, Cube, glow, new Vector3(side * s * span * 0.52f, y - s * 0.005f, s * 0.04f), new Vector3(s * span * 0.95f, s * 0.012f, s * 0.02f));
            e.transform.localRotation = Quaternion.Euler(0f, -side * 30f, 0f);
            Piece(root, Cube, accent, new Vector3(side * s * span * 0.9f, y - s * 0.01f, -s * 0.3f), new Vector3(s * 0.1f, s * 0.02f, s * 0.12f));
        }

        // Motores luminosos
        int engines = bomber ? 3 : 1;
        for (int i = 0; i < engines; i++)
        {
            float x = (i - (engines - 1) * 0.5f) * s * 0.28f;
            Piece(root, Cylinder, dark, new Vector3(x, y, -s * len * 0.5f), new Vector3(s * 0.1f, s * 0.04f, s * 0.1f)).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Piece(root, Sphere, glow, new Vector3(x, y, -s * len * 0.5f - s * 0.06f), Vector3.one * s * 0.09f);
        }
    }

    // Barco antigravedad (Barco de Vela): casco blanco aerodinámico con una franja de energía y un pequeño puente
    static void BuildFuturisticBoat(Transform root, float s, Color civ)
    {
        Material hull = LitMaterial(FutWhite);
        Material accent = LitMaterial(civ);
        Material glow = FutGlow(FutCyan);

        Piece(root, ShipHull(), hull, Vector3.zero, new Vector3(s * 0.8f, s * 0.8f, s * 1.4f));
        Piece(root, Cube, glow, new Vector3(0f, s * 0.36f, 0f), new Vector3(s * 0.52f, s * 0.03f, s * 1.0f));
        Piece(root, Cube, accent, new Vector3(0f, s * 0.5f, -s * 0.1f), new Vector3(s * 0.3f, s * 0.2f, s * 0.4f));
        GameObject wind = Piece(root, Cube, glow, new Vector3(0f, s * 0.58f, s * 0.12f), new Vector3(s * 0.28f, s * 0.12f, s * 0.02f));
        wind.transform.localRotation = Quaternion.Euler(-30f, 0f, 0f);
    }

    // Buque futurista: casco blanco, superestructura con cúpula luminosa, cañones de riel y anillos de energía.
    // big: portanaves/crucero con dos cañones y más anillos
    static void BuildFuturisticWarship(Transform root, float s, Color civ, bool big)
    {
        Material hull = LitMaterial(FutWhite);
        Material dark = LitMaterial(FutGraphite);
        Material accent = LitMaterial(civ);
        Material glow = FutGlow(FutCyan);

        float len = big ? 2.0f : 1.7f;
        Piece(root, ShipHull(), hull, Vector3.zero, new Vector3(s * 1.1f, s * 1.0f, s * len));
        Piece(root, Cube, glow, new Vector3(0f, s * 0.42f, 0f), new Vector3(s * 0.58f, s * 0.02f, s * (len - 0.4f)));
        Piece(root, Cube, accent, new Vector3(0f, s * 0.46f, 0f), new Vector3(s * 0.2f, s * 0.02f, s * (len - 0.6f)));

        // Superestructura con cúpula de energía y antena
        Piece(root, Cube, hull, new Vector3(0f, s * 0.62f, -s * 0.15f), new Vector3(s * 0.36f, s * 0.3f, s * 0.6f));
        Piece(root, Sphere, glow, new Vector3(0f, s * 0.84f, -s * 0.15f), new Vector3(s * 0.26f, s * 0.2f, s * 0.26f));
        Piece(root, Cylinder, dark, new Vector3(0f, s * 1.1f, -s * 0.15f), new Vector3(s * 0.02f, s * 0.22f, s * 0.02f));

        // Cañones de riel en proa
        int turrets = big ? 2 : 1;
        for (int i = 0; i < turrets; i++)
        {
            float z = s * (0.5f + i * 0.3f);
            Piece(root, Sphere, dark, new Vector3(0f, s * 0.52f, z), new Vector3(s * 0.2f, s * 0.12f, s * 0.2f));
            GameObject barrel = Piece(root, Cylinder, dark, new Vector3(0f, s * 0.55f, z + s * 0.24f), new Vector3(s * 0.04f, s * 0.26f, s * 0.04f));
            barrel.transform.localRotation = Quaternion.Euler(85f, 0f, 0f);
            Piece(root, Sphere, glow, new Vector3(0f, s * 0.56f, z + s * 0.5f), Vector3.one * s * 0.05f);
        }

        // Anillos de energía en popa
        int rings = big ? 3 : 2;
        for (int i = 0; i < rings; i++)
            Piece(root, Cylinder, glow, new Vector3(0f, s * 0.46f, -s * (0.55f + i * 0.14f)), new Vector3(s * (0.34f - i * 0.05f), s * 0.01f, s * (0.34f - i * 0.05f)));
    }

    // Mecha de combate: robot bípedo con piernas articuladas, torso blindado con cabina luminosa, cañones de plasma en los
    // hombros y franjas del color de la civilización
    static void BuildFuturisticMecha(Transform root, float s, Color civ)
    {
        Material armor = LitMaterial(FutWhite);
        Material dark = LitMaterial(FutGraphite);
        Material accent = LitMaterial(civ);
        Material glow = FutGlow(FutCyan);

        // Piernas: pie, espinilla, rodilla luminosa y muslo
        for (int side = -1; side <= 1; side += 2)
        {
            float x = side * s * 0.2f;
            Piece(root, Cube, dark, new Vector3(x, s * 0.04f, s * 0.05f), new Vector3(s * 0.2f, s * 0.08f, s * 0.34f));
            Piece(root, Cube, armor, new Vector3(x, s * 0.25f, 0f), new Vector3(s * 0.14f, s * 0.36f, s * 0.14f));
            Piece(root, Sphere, glow, new Vector3(x, s * 0.45f, s * 0.07f), Vector3.one * s * 0.08f);
            Piece(root, Cube, armor, new Vector3(x, s * 0.58f, 0f), new Vector3(s * 0.18f, s * 0.26f, s * 0.18f));
        }

        // Cintura, torso y cabina con visor luminoso
        Piece(root, Cube, dark, new Vector3(0f, s * 0.74f, 0f), new Vector3(s * 0.5f, s * 0.1f, s * 0.3f));
        Piece(root, Cube, armor, new Vector3(0f, s * 1.0f, 0f), new Vector3(s * 0.56f, s * 0.46f, s * 0.38f));
        Piece(root, Cube, accent, new Vector3(0f, s * 1.0f, s * 0.2f), new Vector3(s * 0.4f, s * 0.12f, s * 0.02f));
        Piece(root, Cube, dark, new Vector3(0f, s * 1.3f, s * 0.04f), new Vector3(s * 0.26f, s * 0.2f, s * 0.24f));
        Piece(root, Cube, glow, new Vector3(0f, s * 1.31f, s * 0.17f), new Vector3(s * 0.2f, s * 0.07f, s * 0.02f));
        Piece(root, Cube, dark, new Vector3(0f, s * 1.0f, -s * 0.24f), new Vector3(s * 0.36f, s * 0.36f, s * 0.12f)); // reactor dorsal
        Piece(root, Cube, glow, new Vector3(0f, s * 1.0f, -s * 0.31f), new Vector3(s * 0.14f, s * 0.24f, s * 0.02f));

        // Hombros con cañón de plasma a cada lado (el derecho apunta hacia delante)
        for (int side = -1; side <= 1; side += 2)
        {
            float x = side * s * 0.42f;
            Piece(root, Sphere, armor, new Vector3(x, s * 1.1f, 0f), new Vector3(s * 0.22f, s * 0.2f, s * 0.22f));
            Piece(root, Cube, armor, new Vector3(x * 1.1f, s * 0.88f, s * 0.06f), new Vector3(s * 0.12f, s * 0.3f, s * 0.12f));
            GameObject barrel = Piece(root, Cylinder, dark, new Vector3(x * 1.1f, s * 0.78f, s * 0.34f), new Vector3(s * 0.07f, s * 0.26f, s * 0.07f));
            barrel.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Piece(root, Sphere, glow, new Vector3(x * 1.1f, s * 0.78f, s * 0.62f), Vector3.one * s * 0.07f);
        }
    }

    // Dibuja la versión futurista de la unidad (todas las unidades tienen una)
    static void BuildFuturisticUnit(Transform root, float size, Unit unit)
    {
        Color civ = unit.owner.color;
        float s = size;

        switch (unit.kind)
        {
            case UnitKind.Settler:     BuildFuturisticSettler(root, s, civ); break;
            case UnitKind.Worker:      BuildFuturisticWorker(root, s, civ); break;
            case UnitKind.Warrior:
            case UnitKind.Spearman:
            case UnitKind.Musketeer:
            case UnitKind.Rifleman:    BuildFuturisticSoldier(root, s, civ, 0); break;
            case UnitKind.Phalanx:
            case UnitKind.Legion:      BuildFuturisticSoldier(root, s, civ, 1); break;
            case UnitKind.Grenadier:   BuildFuturisticSoldier(root, s, civ, 2); break;
            case UnitKind.Archer:
            case UnitKind.Crossbowman: BuildFuturisticSoldier(root, s, civ, 3); break;
            case UnitKind.Horseman:    BuildFuturisticHoverbike(root, s, civ); break;
            case UnitKind.Chariot:     BuildFuturisticApc(root, s, civ); break;
            case UnitKind.Caravan:     BuildFuturisticCargo(root, s, civ, false); break;
            case UnitKind.Merchant:    BuildFuturisticCargo(root, s, civ, true); break;
            case UnitKind.Catapult:    BuildFuturisticLauncher(root, s, civ); break;
            case UnitKind.Ballista:
            case UnitKind.Cannon:      BuildFuturisticHoverTank(root, s, civ, 1); break;
            case UnitKind.Tank:        BuildFuturisticHoverTank(root, s, civ, 0); break;
            case UnitKind.Mecha:       BuildFuturisticMecha(root, s, civ); break;
            case UnitKind.Fighter:     BuildFuturisticAircraft(root, s, civ, false); break;
            case UnitKind.Bomber:      BuildFuturisticAircraft(root, s, civ, true); break;
            case UnitKind.Sailboat:    BuildFuturisticBoat(root, s, civ); break;
            case UnitKind.Galleon:     BuildFuturisticWarship(root, s, civ, false); break;
            case UnitKind.Steamship:   BuildFuturisticWarship(root, s, civ, true); break;
            default:                   BuildFuturisticSoldier(root, s, civ, 0); break;
        }
    }

    // Dibuja la versión actual de la unidad (todas las unidades tienen una)
    static void BuildContemporaryUnit(Transform root, float size, Unit unit)
    {
        Color civ = unit.owner.color;
        float s = size;

        switch (unit.kind)
        {
            case UnitKind.Settler:     BuildModernSettler(root, s, civ); break;
            case UnitKind.Worker:      BuildModernWorker(root, s, civ); break;
            case UnitKind.Warrior:     BuildModernSoldier(root, s, civ, 0); break;
            case UnitKind.Spearman:    BuildModernSoldier(root, s, civ, 0); break;
            case UnitKind.Musketeer:   BuildModernSoldier(root, s, civ, 0); break;
            case UnitKind.Rifleman:    BuildModernSoldier(root, s, civ, 0); break;
            case UnitKind.Phalanx:     BuildModernSoldier(root, s, civ, 1); break;
            case UnitKind.Legion:      BuildModernSoldier(root, s, civ, 1); break;
            case UnitKind.Grenadier:   BuildModernSoldier(root, s, civ, 2); break;
            case UnitKind.Archer:      BuildModernSoldier(root, s, civ, 3); break;
            case UnitKind.Crossbowman: BuildModernSoldier(root, s, civ, 3); break;
            case UnitKind.Horseman:    BuildModernJeep(root, s, civ); break;
            case UnitKind.Chariot:     BuildModernApc(root, s, civ); break;
            case UnitKind.Caravan:     BuildModernTruck(root, s, civ, false); break;
            case UnitKind.Merchant:    BuildModernTruck(root, s, civ, true); break;
            case UnitKind.Catapult:    BuildModernMissileLauncher(root, s, civ); break;
            case UnitKind.Ballista:    BuildModernHowitzer(root, s, civ); break;
            case UnitKind.Cannon:      BuildModernHowitzer(root, s, civ); break;
            case UnitKind.Tank:        BuildModernTank(root, s, civ); break;
            case UnitKind.Fighter:     BuildModernAircraft(root, s, civ, false); break;
            case UnitKind.Bomber:      BuildModernAircraft(root, s, civ, true); break;
            case UnitKind.Sailboat:    BuildModernMotorboat(root, s, civ); break;
            case UnitKind.Galleon:     BuildModernWarship(root, s, civ, false); break;
            case UnitKind.Steamship:   BuildModernWarship(root, s, civ, true); break;
            default:                   BuildModernSoldier(root, s, civ, 0); break;
        }
    }

    // ---------------------------------------------------------------- Irrigación

    static Transform irrigationRoot;

    // Casilla irrigada: tres acequias de agua paralelas con brotes verdes entre ellas
    public static void BuildIrrigation(int cell, WorldGrid grid, Transform earthTransform)
    {
        if (irrigationRoot == null)
        {
            irrigationRoot = new GameObject("Irrigaciones").transform;
            irrigationRoot.SetParent(earthTransform, false);
        }

        float size = grid.spacing * 0.5f;
        Material water = LitMaterial(new Color(0.25f, 0.6f, 0.95f));
        water.SetFloat("_Glossiness", 0.8f);
        Material soil = LitMaterial(new Color(0.32f, 0.2f, 0.1f));
        Material sprout = LitMaterial(new Color(0.35f, 0.75f, 0.25f));

        GameObject root = Root("Irrigación " + cell, irrigationRoot, grid.pos[cell], grid.dir[cell]);
        root.transform.localRotation = Quaternion.FromToRotation(Vector3.up, grid.dir[cell])
                                       * Quaternion.Euler(0f, (cell * 37) % 90, 0f);

        // Tres acequias con borde de tierra
        for (int i = -1; i <= 1; i++)
        {
            float z = i * size * 0.4f;
            Piece(root.transform, Cube, soil, new Vector3(0f, size * 0.01f, z), new Vector3(size * 1.5f, size * 0.02f, size * 0.17f));
            Piece(root.transform, Cube, water, new Vector3(0f, size * 0.025f, z), new Vector3(size * 1.5f, size * 0.012f, size * 0.1f));
        }

        // Brotes entre las acequias
        for (int row = 0; row < 2; row++)
        {
            float z = (row - 0.5f) * size * 0.4f;
            for (int i = -3; i <= 3; i++)
                Piece(root.transform, Capsule, sprout, new Vector3(i * size * 0.2f, size * 0.07f, z), new Vector3(size * 0.04f, size * 0.07f, size * 0.04f));
        }
    }

    // ---------------------------------------------------------------- Minas

    static Transform mineRoot;

    // Casilla minada: bocamina con marco de madera, montón de escombros y una vagoneta
    public static void BuildMine(int cell, WorldGrid grid, Transform earthTransform)
    {
        if (mineRoot == null)
        {
            mineRoot = new GameObject("Minas").transform;
            mineRoot.SetParent(earthTransform, false);
        }

        float size = grid.spacing * 0.5f;
        Material rock = LitMaterial(new Color(0.25f, 0.23f, 0.22f));
        Material wood = LitMaterial(new Color(0.4f, 0.26f, 0.12f));
        Material dark = LitMaterial(new Color(0.04f, 0.04f, 0.05f));
        Material ore = LitMaterial(new Color(0.85f, 0.7f, 0.25f));

        GameObject root = Root("Mina " + cell, mineRoot, grid.pos[cell], grid.dir[cell]);
        root.transform.localRotation = Quaternion.FromToRotation(Vector3.up, grid.dir[cell])
                                       * Quaternion.Euler(0f, (cell * 53) % 360, 0f);

        // Entrada: hueco oscuro con dos postes y un dintel
        Piece(root.transform, Cube, dark, new Vector3(0f, size * 0.22f, 0f), new Vector3(size * 0.5f, size * 0.44f, size * 0.12f));
        Piece(root.transform, Cube, wood, new Vector3(-size * 0.28f, size * 0.24f, 0f), new Vector3(size * 0.08f, size * 0.5f, size * 0.1f));
        Piece(root.transform, Cube, wood, new Vector3(size * 0.28f, size * 0.24f, 0f), new Vector3(size * 0.08f, size * 0.5f, size * 0.1f));
        Piece(root.transform, Cube, wood, new Vector3(0f, size * 0.5f, 0f), new Vector3(size * 0.7f, size * 0.08f, size * 0.12f));

        // Escombros y mineral junto a la entrada
        Piece(root.transform, Sphere, rock, new Vector3(-size * 0.3f, size * 0.07f, size * 0.35f), new Vector3(size * 0.35f, size * 0.14f, size * 0.3f));
        Piece(root.transform, Sphere, rock, new Vector3(size * 0.05f, size * 0.05f, size * 0.45f), new Vector3(size * 0.25f, size * 0.1f, size * 0.22f));
        Piece(root.transform, Sphere, ore, new Vector3(size * 0.32f, size * 0.06f, size * 0.38f), new Vector3(size * 0.14f, size * 0.1f, size * 0.14f));
    }

    // ---------------------------------------------------------------- Ríos

    // Dibuja los ríos del mapa como líneas azules sinuosas pegadas al relieve: finas en la fuente y más anchas al llegar
    // a la desembocadura. Quedan por debajo de las carreteras.
    public static void BuildRivers(WorldGrid grid, EarthTopography earth, Transform earthTransform)
    {
        GameObject root = new GameObject("Ríos");
        root.transform.SetParent(earthTransform, false);

        Material water = FlatMaterial(new Color(0.22f, 0.5f, 0.9f, 1f));
        Material bank = FlatMaterial(new Color(0.12f, 0.3f, 0.6f, 1f));

        float lift = grid.radius * 0.004f;
        const int steps = 6;

        for (int r = 0; r < grid.riverPaths.Count; r++)
        {
            List<int> path = grid.riverPaths[r];
            List<Vector3> points = new List<Vector3>();
            points.Add(grid.pos[path[0]] + grid.dir[path[0]] * lift);

            for (int k = 0; k + 1 < path.Count; k++)
            {
                int a = path[k], b = path[k + 1];
                Vector3 da = grid.dir[a], db = grid.dir[b];
                Vector3 along = (db - da).normalized;

                // Meandros: ondulación lateral suave que se anula en los extremos del tramo
                System.Random rng = new System.Random(a * 7919 + b * 104729 + r);
                float phase = (float)rng.NextDouble() * Mathf.PI * 2f;
                float amplitude = grid.angularSpacing * (0.12f + (float)rng.NextDouble() * 0.12f);

                for (int i = 1; i <= steps; i++)
                {
                    float t = (float)i / steps;
                    Vector3 d = Vector3.Slerp(da, db, t);

                    if (i == steps)
                    {
                        points.Add(grid.pos[b] + db * lift);
                        continue;
                    }

                    float offset = amplitude * Mathf.Sin(t * Mathf.PI) * Mathf.Sin(t * Mathf.PI * 2f + phase);
                    Vector3 side = Vector3.Cross(d, along).normalized;
                    Vector3 dd = (d + side * offset).normalized;
                    points.Add(earth.LocalSurfacePoint(dd) + dd * lift);
                }
            }

            AddRiverLine(root.transform, "Orilla " + r, points, grid.spacing * 0.09f, bank, 2998);
            AddRiverLine(root.transform, "Río " + r, points, grid.spacing * 0.06f, water, 2999);
        }
    }

    static void AddRiverLine(Transform parent, string name, List<Vector3> points, float width, Material material, int queue)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);

        LineRenderer line = go.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.positionCount = points.Count;
        line.SetPositions(points.ToArray());
        line.widthMultiplier = width;
        line.widthCurve = new AnimationCurve(new Keyframe(0f, 0.45f), new Keyframe(1f, 1.3f)); // más ancho hacia la desembocadura
        line.numCapVertices = 3;
        line.numCornerVertices = 3;

        line.sharedMaterial = new Material(material) { renderQueue = queue };
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
    }

    // ---------------------------------------------------------------- Granjas

    static Transform farmRoot;

    // Casilla con granja: campo de cultivo con surcos de tierra y trigo dorado, vallado de madera y un pajar
    public static void BuildFarm(int cell, WorldGrid grid, Transform earthTransform)
    {
        if (farmRoot == null)
        {
            farmRoot = new GameObject("Granjas").transform;
            farmRoot.SetParent(earthTransform, false);
        }

        float size = grid.spacing * 0.5f;
        Material soil = LitMaterial(new Color(0.34f, 0.22f, 0.12f));
        Material wheat = LitMaterial(new Color(0.9f, 0.76f, 0.3f));
        Material green = LitMaterial(new Color(0.45f, 0.7f, 0.25f));
        Material wood = LitMaterial(new Color(0.45f, 0.3f, 0.16f));
        Material hay = LitMaterial(new Color(0.85f, 0.7f, 0.3f));

        GameObject root = Root("Granja " + cell, farmRoot, grid.pos[cell], grid.dir[cell]);
        root.transform.localRotation = Quaternion.FromToRotation(Vector3.up, grid.dir[cell])
                                       * Quaternion.Euler(0f, (cell * 41) % 90, 0f);

        // Tierra labrada con cuatro surcos paralelos de trigo (los dos centrales, aún verdes)
        Piece(root.transform, Cube, soil, new Vector3(0f, size * 0.012f, 0f), new Vector3(size * 1.5f, size * 0.024f, size * 1.2f));
        for (int row = -2; row <= 1; row++)
        {
            float z = (row + 0.5f) * size * 0.28f;
            Material crop = (row == -1 || row == 0) ? green : wheat;
            for (int i = -4; i <= 4; i++)
                Piece(root.transform, Capsule, crop, new Vector3(i * size * 0.15f, size * 0.07f, z), new Vector3(size * 0.035f, size * 0.07f, size * 0.035f));
        }

        // Vallado de madera en los dos lados cortos
        for (int side = -1; side <= 1; side += 2)
        {
            Piece(root.transform, Cube, wood, new Vector3(side * size * 0.76f, size * 0.12f, 0f), new Vector3(size * 0.04f, size * 0.03f, size * 1.2f));
            for (int p = -2; p <= 2; p++)
                Piece(root.transform, Cube, wood, new Vector3(side * size * 0.76f, size * 0.08f, p * size * 0.28f), new Vector3(size * 0.04f, size * 0.16f, size * 0.04f));
        }

        // Pajar (montón de heno) en una esquina
        Piece(root.transform, Sphere, hay, new Vector3(size * 0.55f, size * 0.1f, size * 0.7f), new Vector3(size * 0.28f, size * 0.22f, size * 0.28f));
    }

    // ---------------------------------------------------------------- Carreteras

    static Transform roadRoot;
    static Material roadMaterial;
    static Material stoneRoadMaterial, stoneRoadBorderMaterial, stoneRoadCobble;

    // Cada tramo recuerda quién lo construyó para poder cambiarlo a piedra cuando esa civilización llegue a la Edad Media
    class RoadSegment
    {
        public int a, b;
        public Civ builder;
        public GameObject view;
    }

    static readonly List<RoadSegment> roadSegments = new List<RoadSegment>();

    /// <summary>Añade a la lista los tramos de carretera de tres en tres: casilla a, casilla b y id de quien la construyó.</summary>
    public static void ExportRoads(List<int> flat)
    {
        foreach (RoadSegment seg in roadSegments)
        {
            flat.Add(seg.a);
            flat.Add(seg.b);
            flat.Add(seg.builder != null ? seg.builder.id : 0);
        }
    }

    /// <summary>Olvida las carreteras, granjas, riegos y minas dibujados (al cargar una partida se vuelven a crear).</summary>
    public static void ResetStatics()
    {
        fogObject = null;
        roadSegments.Clear();
        roadRoot = null;
        irrigationRoot = null;
        mineRoot = null;
        farmRoot = null;
    }

    // Tramo de carretera entre dos casillas vecinas, pegado al relieve. Es de piedra si quien lo construye ya está en la Edad Media
    public static void BuildRoad(int a, int b, WorldGrid grid, EarthTopography earth, Transform earthTransform, Civ builder)
    {
        RoadSegment seg = new RoadSegment { a = a, b = b, builder = builder };
        seg.view = DrawRoad(a, b, builder, grid, earth, earthTransform);
        roadSegments.Add(seg);
    }

    // Tramo de ferrocarril sobre una carretera: balasto oscuro, dos carriles de acero y traviesas
    public static void BuildRail(int a, int b, WorldGrid grid, EarthTopography earth, Transform earthTransform, Civ builder)
    {
        DrawRoad(a, b, builder, grid, earth, earthTransform, true);
    }

    // Cambio de era de una civilización: todos sus tramos de tierra pasan a carretera de piedra
    public static void UpgradeRoads(Civ civ, WorldGrid grid, EarthTopography earth, Transform earthTransform)
    {
        foreach (RoadSegment seg in roadSegments)
        {
            if (seg.builder != civ) continue;
            if (seg.view != null) Object.Destroy(seg.view);
            seg.view = DrawRoad(seg.a, seg.b, civ, grid, earth, earthTransform);
        }
    }

    static GameObject DrawRoad(int a, int b, Civ builder, WorldGrid grid, EarthTopography earth, Transform earthTransform, bool rail = false)
    {
        if (roadRoot == null)
        {
            roadRoot = new GameObject("Carreteras").transform;
            roadRoot.SetParent(earthTransform, false);
            roadMaterial = FlatMaterial(new Color(0.66f, 0.52f, 0.33f, 1f));      // sendero de tierra pisada
            roadBorderMaterial = FlatMaterial(new Color(0.46f, 0.36f, 0.22f, 1f));  // borde de tierra removida
            pathPebbleMaterial = LitMaterial(new Color(0.5f, 0.42f, 0.3f));       // piedrecillas del camino
            stoneRoadMaterial = FlatMaterial(new Color(0.66f, 0.64f, 0.6f, 1f));  // calzada de piedra clara
            stoneRoadBorderMaterial = FlatMaterial(new Color(0.4f, 0.39f, 0.37f, 1f));
            stoneRoadCobble = LitMaterial(new Color(0.55f, 0.54f, 0.51f));        // adoquines
            stoneRoadCobbleDark = LitMaterial(new Color(0.42f, 0.41f, 0.4f));     // adoquines más oscuros
            stoneRoadCobbleLight = LitMaterial(new Color(0.68f, 0.66f, 0.62f));   // adoquines más claros
            asphaltMaterial = FlatMaterial(new Color(0.17f, 0.17f, 0.19f, 1f));   // asfalto
            asphaltShoulderMaterial = FlatMaterial(new Color(0.5f, 0.5f, 0.52f, 1f));
            asphaltLineMaterial = FlatMaterial(new Color(0.95f, 0.95f, 0.95f, 1f));
            asphaltDashMaterial = LitMaterial(new Color(0.98f, 0.82f, 0.15f));
            glassMaterial = FlatMaterial(new Color(0.45f, 0.78f, 0.95f, 0.7f));   // cristal translúcido
            glassBorderMaterial = FlatMaterial(new Color(0.1f, 0.2f, 0.35f, 0.65f));
            neonCyanMaterial = FlatMaterial(new Color(0.75f, 1f, 1f, 1f));        // núcleo brillante del neón
            neonMagentaMaterial = FlatMaterial(new Color(1f, 0.75f, 0.95f, 1f));
            neonCyanGlowMaterial = FlatMaterial(new Color(0.1f, 0.85f, 1f, 0.45f));   // halo de luz
            neonMagentaGlowMaterial = FlatMaterial(new Color(1f, 0.15f, 0.85f, 0.45f));
            neonCyanLight = FutGlow(new Color(0.2f, 0.95f, 1f));
            neonMagentaLight = FutGlow(new Color(1f, 0.25f, 0.9f));
        }

        float lift = grid.radius * 0.006f;

        GameObject go = new GameObject("Tramo " + a + "-" + b);
        go.transform.SetParent(roadRoot, false);

        // Camino sinuoso: varios puntos sobre el arco entre las dos casillas, desplazados a los lados
        // con una ondulación suave que se anula en los extremos (así empalma con los tramos vecinos).
        // Las fases dependen de la pareja de casillas, no del sentido, y cada tramo es distinto.
        const int steps = 10;
        int lo = Mathf.Min(a, b), hi = Mathf.Max(a, b);
        System.Random rng = new System.Random(lo * 7919 + hi * 104729);
        float phase1 = (float)rng.NextDouble() * Mathf.PI * 2f;
        float phase2 = (float)rng.NextDouble() * Mathf.PI * 2f;
        float amplitude = grid.angularSpacing * (0.12f + (float)rng.NextDouble() * 0.1f);

        Vector3 da = grid.dir[lo], db = grid.dir[hi];
        Vector3 along = (db - da).normalized;

        Vector3[] points = new Vector3[steps + 1];
        for (int i = 0; i <= steps; i++)
        {
            float t = (float)i / steps;
            Vector3 d = Vector3.Slerp(da, db, t);

            if (i == 0) { points[i] = grid.pos[lo] + da * lift; continue; }
            if (i == steps) { points[i] = grid.pos[hi] + db * lift; continue; }

            float wave = 0.65f * Mathf.Sin(t * Mathf.PI * 3f + phase1) + 0.35f * Mathf.Sin(t * Mathf.PI * 7f + phase2);
            float offset = amplitude * Mathf.Sin(t * Mathf.PI) * wave;
            Vector3 side = Vector3.Cross(d, along).normalized;
            Vector3 dd = (d + side * offset).normalized;

            points[i] = earth.LocalSurfacePoint(dd) + dd * lift;
        }

        if (rail)
        {
            if (railBedMaterial == null)
            {
                railBedMaterial = FlatMaterial(new Color(0.22f, 0.22f, 0.24f, 1f));   // balasto
                railSteelMaterial = FlatMaterial(new Color(0.8f, 0.82f, 0.86f, 1f));  // carriles
                railSleeperMaterial = LitMaterial(new Color(0.35f, 0.22f, 0.12f));    // traviesas
            }

            AddRoadLine(go.transform, "Balasto", points, grid.spacing * 0.09f, railBedMaterial, 3002);

            // Dos carriles paralelos a ambos lados del centro
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3[] railPoints = new Vector3[points.Length];
                for (int i = 0; i < points.Length; i++)
                {
                    Vector3 up = points[i].normalized;
                    Vector3 tangent = (points[Mathf.Min(i + 1, points.Length - 1)] - points[Mathf.Max(i - 1, 0)]).normalized;
                    Vector3 lateral = Vector3.Cross(up, tangent).normalized;
                    railPoints[i] = points[i] + lateral * side * grid.spacing * 0.02f + up * grid.spacing * 0.002f;
                }
                AddRoadLine(go.transform, "Carril", railPoints, grid.spacing * 0.008f, railSteelMaterial, 3003);
            }

            // Traviesas a lo largo del tramo
            for (int i = 1; i < steps; i++)
            {
                Vector3 up = points[i].normalized;
                Vector3 tangent = (points[i + 1] - points[i - 1]).normalized;
                Vector3 lateral = Vector3.Cross(up, tangent).normalized;
                GameObject sleeper = Piece(go.transform, Cube, railSleeperMaterial, points[i] + up * grid.spacing * 0.003f,
                    new Vector3(grid.spacing * 0.012f, grid.spacing * 0.006f, grid.spacing * 0.06f));
                sleeper.transform.localRotation = Quaternion.LookRotation(lateral, up);
            }
        }
        else if (builder != null && builder.Has(TechKind.EraChange4))
        {
            // Carretera futurista: calzada de cristal translúcido con un borde oscuro, dos neones laterales (cian y magenta)
            // con halo de luz y una hilera de pilotos luminosos en el centro
            AddRoadLine(go.transform, "Borde de cristal", points, grid.spacing * 0.105f, glassBorderMaterial, 3000);
            AddRoadLine(go.transform, "Cristal", points, grid.spacing * 0.085f, glassMaterial, 3001);

            for (int side = -1; side <= 1; side += 2)
            {
                Vector3[] edge = new Vector3[points.Length];
                for (int i = 0; i < points.Length; i++)
                {
                    Vector3 up = points[i].normalized;
                    Vector3 tangent = (points[Mathf.Min(i + 1, points.Length - 1)] - points[Mathf.Max(i - 1, 0)]).normalized;
                    Vector3 lateral = Vector3.Cross(up, tangent).normalized;
                    edge[i] = points[i] + lateral * side * grid.spacing * 0.038f + up * grid.spacing * 0.002f;
                }
                AddRoadLine(go.transform, "Halo de neón", edge, grid.spacing * 0.016f, side < 0 ? neonCyanGlowMaterial : neonMagentaGlowMaterial, 3002);
                AddRoadLine(go.transform, "Neón", edge, grid.spacing * 0.005f, side < 0 ? neonCyanMaterial : neonMagentaMaterial, 3003);
            }

            for (int i = 1; i < steps; i++)
            {
                Vector3 up = points[i].normalized;
                Vector3 tangent = (points[i + 1] - points[i - 1]).normalized;
                GameObject light = Piece(go.transform, Cube, i % 2 == 0 ? neonCyanLight : neonMagentaLight, points[i] + up * grid.spacing * 0.004f,
                    new Vector3(grid.spacing * 0.008f, grid.spacing * 0.003f, grid.spacing * 0.02f));
                light.transform.localRotation = Quaternion.LookRotation(tangent, up);
            }
        }
        else if (builder != null && builder.Has(TechKind.EraChange3))
        {
            // Carretera actual: asfalto oscuro con arcenes claros, líneas blancas en los bordes y línea central amarilla discontinua
            AddRoadLine(go.transform, "Arcén", points, grid.spacing * 0.105f, asphaltShoulderMaterial, 3000);
            AddRoadLine(go.transform, "Asfalto", points, grid.spacing * 0.085f, asphaltMaterial, 3001);

            for (int side = -1; side <= 1; side += 2)
            {
                Vector3[] edge = new Vector3[points.Length];
                for (int i = 0; i < points.Length; i++)
                {
                    Vector3 up = points[i].normalized;
                    Vector3 tangent = (points[Mathf.Min(i + 1, points.Length - 1)] - points[Mathf.Max(i - 1, 0)]).normalized;
                    Vector3 lateral = Vector3.Cross(up, tangent).normalized;
                    edge[i] = points[i] + lateral * side * grid.spacing * 0.036f + up * grid.spacing * 0.002f;
                }
                AddRoadLine(go.transform, "Línea de borde", edge, grid.spacing * 0.004f, asphaltLineMaterial, 3002);
            }

            for (int i = 1; i < steps; i += 2)
            {
                Vector3 up = points[i].normalized;
                Vector3 tangent = (points[i + 1] - points[i - 1]).normalized;
                GameObject dash = Piece(go.transform, Cube, asphaltDashMaterial, points[i] + up * grid.spacing * 0.004f,
                    new Vector3(grid.spacing * 0.006f, grid.spacing * 0.002f, grid.spacing * 0.03f));
                dash.transform.localRotation = Quaternion.LookRotation(tangent, up);
            }
        }
        else if (builder != null && builder.Has(TechKind.EraChange))
        {
            // Camino empedrado: calzada ancha de piedra con un borde de bordillos oscuros y tres hileras de adoquines
            // de distintos tonos, dispuestos en tresbolillo a lo largo y ancho del camino
            AddRoadLine(go.transform, "Borde", points, grid.spacing * 0.105f, stoneRoadBorderMaterial, 3000);
            AddRoadLine(go.transform, "Calzada", points, grid.spacing * 0.08f, stoneRoadMaterial, 3001);

            Material[] cobbleMats = { stoneRoadCobble, stoneRoadCobbleDark, stoneRoadCobbleLight };
            float cobble = grid.spacing * 0.026f;
            for (int i = 1; i < steps; i++)
            {
                Vector3 up = points[i].normalized;
                Vector3 tangent = (points[i + 1] - points[i - 1]).normalized;
                Vector3 lateral = Vector3.Cross(up, tangent).normalized;

                for (int row = -1; row <= 1; row++)
                {
                    // Las hileras alternas van desplazadas media piedra hacia delante
                    Vector3 shift = tangent * ((row % 2 == 0) ? 0f : grid.spacing * 0.014f);
                    Vector3 pos = points[i] + up * grid.spacing * 0.004f + lateral * row * grid.spacing * 0.026f + shift;
                    float stoneSize = cobble * (0.85f + (float)rng.NextDouble() * 0.3f);

                    GameObject stone = Piece(go.transform, Sphere, cobbleMats[rng.Next(cobbleMats.Length)], pos, new Vector3(stoneSize, stoneSize * 0.35f, stoneSize * 0.85f));
                    stone.transform.localRotation = Quaternion.FromToRotation(Vector3.up, up) * Quaternion.Euler(0f, (i * 53 + row * 29 + a) % 180, 0f);
                }
            }
        }
        else
        {
            // Camino de la Edad Antigua: sendero estrecho de tierra pisada con un borde irregular y piedrecillas sueltas
            AddRoadLine(go.transform, "Borde", points, grid.spacing * 0.05f, roadBorderMaterial, 3000);
            AddRoadLine(go.transform, "Calzada", points, grid.spacing * 0.028f, roadMaterial, 3001);

            for (int i = 1; i < steps; i++)
            {
                Vector3 up = points[i].normalized;
                Vector3 tangent = (points[i + 1] - points[i - 1]).normalized;
                Vector3 lateral = Vector3.Cross(up, tangent).normalized;
                float side = ((float)rng.NextDouble() - 0.5f) * 2f;
                float pebble = grid.spacing * (0.008f + (float)rng.NextDouble() * 0.008f);
                Vector3 pos = points[i] + up * grid.spacing * 0.003f + lateral * side * grid.spacing * 0.03f;

                GameObject stone = Piece(go.transform, Sphere, pathPebbleMaterial, pos, new Vector3(pebble, pebble * 0.5f, pebble * 0.8f));
                stone.transform.localRotation = Quaternion.FromToRotation(Vector3.up, up) * Quaternion.Euler(0f, (i * 71 + a) % 180, 0f);
            }
        }

        return go;
    }

    static Material roadBorderMaterial;
    static Material stoneRoadCobbleDark, stoneRoadCobbleLight;
    static Material pathPebbleMaterial;
    static Material asphaltMaterial, asphaltShoulderMaterial, asphaltLineMaterial, asphaltDashMaterial;
    static Material glassMaterial, glassBorderMaterial, neonCyanMaterial, neonMagentaMaterial, neonCyanGlowMaterial, neonMagentaGlowMaterial, neonCyanLight, neonMagentaLight;
    static Material railBedMaterial, railSteelMaterial, railSleeperMaterial;

    static void AddRoadLine(Transform parent, string name, Vector3[] points, float width, Material material, int queue)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);

        LineRenderer line = go.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.positionCount = points.Length;
        line.SetPositions(points);
        line.widthMultiplier = width;
        line.numCapVertices = 3;
        line.numCornerVertices = 3;

        line.sharedMaterial = new Material(material) { renderQueue = queue };
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
    }

    // ---------------------------------------------------------------- Niebla de guerra

    static GameObject fogObject;
    static Mesh fogMesh;
    static readonly List<Vector3> fogVertices = new List<Vector3>();
    static readonly List<Color32> fogColors = new List<Color32>();
    static readonly List<int> fogTriangles = new List<int>();

    // Malla de hexágonos oscuros sobre las casillas que el jugador no ve: casi negros si nunca se exploraron,
    // atenuados si ya se vieron alguna vez
    public static void RebuildFog(bool[] visible, bool[] explored, WorldGrid grid, EarthTopography earth, Transform earthTransform)
    {
        if (fogObject == null)
        {
            fogObject = new GameObject("Niebla de guerra");
            fogObject.transform.SetParent(earthTransform, false);
            fogObject.AddComponent<MeshFilter>();

            MeshRenderer r = fogObject.AddComponent<MeshRenderer>();
            // Shader propio con stencil: las casillas se solapan sin dejar huecos y sin oscurecer dos veces el solape.
            // El color y la transparencia vienen de cada vértice. Sin el shader se usa el material plano de siempre.
            Shader fogShader = Shader.Find("Civilogenia/Fog");
            Material m = fogShader != null ? new Material(fogShader) : FlatMaterial(Color.white);
            m.renderQueue = 3500;                      // por encima de carreteras y territorios
            r.sharedMaterial = m;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        fogVertices.Clear();
        fogColors.Clear();
        fogTriangles.Clear();

        float ang = grid.angularSpacing * 0.8f;   // más grande que la casilla: se solapan y no queda ningún hueco entre ellas
        float lift = grid.radius * 0.0045f;
        Color32 unknown = new Color32(0, 0, 4, 245);
        Color32 seen = new Color32(0, 0, 8, 140);

        for (int cell = 0; cell < grid.count; cell++)
        {
            if (visible[cell]) continue;

            Color32 col = explored[cell] ? seen : unknown;

            Vector3 d = grid.dir[cell];
            Vector3 t1 = Vector3.Cross(Vector3.up, d);
            if (t1.sqrMagnitude < 1e-6f) t1 = Vector3.Cross(Vector3.right, d);
            t1.Normalize();
            Vector3 t2 = Vector3.Cross(d, t1);

            int start = fogVertices.Count;
            fogVertices.Add(grid.pos[cell] + d * lift);
            fogColors.Add(col);

            for (int i = 0; i < 6; i++)
            {
                float a = i / 6f * Mathf.PI * 2f;
                Vector3 cd = (d + (t1 * Mathf.Cos(a) + t2 * Mathf.Sin(a)) * ang).normalized;
                fogVertices.Add(earth.LocalSurfacePoint(cd) + cd * lift);
                fogColors.Add(col);
            }

            for (int i = 0; i < 6; i++)
            {
                fogTriangles.Add(start);
                fogTriangles.Add(start + 1 + (i + 1) % 6);
                fogTriangles.Add(start + 1 + i);
            }
        }

        if (fogMesh == null)
        {
            fogMesh = new Mesh { name = "Niebla de guerra" };
            fogMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        }

        fogMesh.Clear();
        fogMesh.SetVertices(fogVertices);
        fogMesh.SetColors(fogColors);
        fogMesh.SetTriangles(fogTriangles, 0);
        fogMesh.RecalculateBounds();
        fogObject.GetComponent<MeshFilter>().sharedMesh = fogMesh;
    }

    // ---------------------------------------------------------------- Territorio

    // Malla de hexágonos planos sobre las casillas de tierra controladas por cada civilización
    public static void RebuildTerritory(Civ civ, WorldGrid grid, EarthTopography earth, Transform earthTransform)
    {
        civ.territoryDirty = false;

        if (civ.territoryObject == null)
        {
            civ.territoryObject = new GameObject("Territorio " + civ.name);
            civ.territoryObject.transform.SetParent(earthTransform, false);
            civ.territoryObject.AddComponent<MeshFilter>();

            MeshRenderer r = civ.territoryObject.AddComponent<MeshRenderer>();
            r.sharedMaterial = FlatMaterial(new Color(civ.color.r, civ.color.g, civ.color.b, 0.32f));
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();

        float ang = grid.angularSpacing * 0.56f;
        float lift = grid.radius * 0.0025f;

        foreach (City city in civ.cities)
        {
            foreach (int cell in city.tiles)
            {
                if (!grid.land[cell] || grid.tileCity[cell] != city) continue;

                Vector3 d = grid.dir[cell];
                Vector3 t1 = Vector3.Cross(Vector3.up, d);
                if (t1.sqrMagnitude < 1e-6f) t1 = Vector3.Cross(Vector3.right, d);
                t1.Normalize();
                Vector3 t2 = Vector3.Cross(d, t1);

                int start = vertices.Count;
                vertices.Add(grid.pos[cell] + d * lift);

                for (int i = 0; i < 6; i++)
                {
                    float a = i / 6f * Mathf.PI * 2f;
                    Vector3 cd = (d + (t1 * Mathf.Cos(a) + t2 * Mathf.Sin(a)) * ang).normalized;
                    vertices.Add(earth.LocalSurfacePoint(cd) + cd * lift);
                }

                for (int i = 0; i < 6; i++)
                {
                    triangles.Add(start);
                    triangles.Add(start + 1 + (i + 1) % 6);
                    triangles.Add(start + 1 + i);
                }
            }
        }

        if (civ.territoryMesh == null)
        {
            civ.territoryMesh = new Mesh { name = "Territorio" };
            civ.territoryMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        }

        civ.territoryMesh.Clear();
        civ.territoryMesh.SetVertices(vertices);
        civ.territoryMesh.SetTriangles(triangles, 0);
        civ.territoryMesh.RecalculateBounds();
        civ.territoryObject.GetComponent<MeshFilter>().sharedMesh = civ.territoryMesh;
    }
}
