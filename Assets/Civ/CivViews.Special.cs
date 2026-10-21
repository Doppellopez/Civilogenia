using System.Collections.Generic;
using UnityEngine;

public static partial class CivViews
{
    // Recursos especiales del mapa: un manojo de trigo dorado en las planicies y una vaca en las colinas
    public static void BuildSpecials(WorldGrid grid, Transform earthTransform)
    {
        specialViews.Clear();
        specialMaterials.Clear();

        GameObject parent = new GameObject("Recursos especiales");
        parent.transform.SetParent(earthTransform, false);

        List<KeyValuePair<int, Transform>> built = new List<KeyValuePair<int, Transform>>();
        for (int cell = 0; cell < grid.count; cell++)
        {
            int before = parent.transform.childCount;

            if (grid.special[cell] == WorldGrid.Wheat) BuildWheat(cell, grid, parent.transform);
            else if (grid.special[cell] == WorldGrid.Cattle) BuildCattle(cell, grid, parent.transform);
            else if (grid.special[cell] == WorldGrid.Fish) BuildFish(cell, grid, parent.transform);
            else if (grid.special[cell] == WorldGrid.Stone) BuildStone(cell, grid, parent.transform);

            if (parent.transform.childCount > before) built.Add(new KeyValuePair<int, Transform>(cell, parent.transform.GetChild(before)));
        }

        CombineSpecials(parent.transform, built);
    }

    // Vista de cada recurso (una sola malla por casilla) para mostrarlo u ocultarlo según la niebla
    static readonly Dictionary<int, GameObject> specialViews = new Dictionary<int, GameObject>();
    static readonly Dictionary<string, Material> specialMaterials = new Dictionary<string, Material>();

    // Cada recurso se construye con muchas piezas sueltas (un objeto y un material por pieza): en un mapa grande son decenas
    // de miles de objetos y de llamadas de dibujo. Cada casilla se junta en una sola malla (con un submaterial por color)
    // y los materiales se comparten entre todos los recursos.
    static void CombineSpecials(Transform parent, List<KeyValuePair<int, Transform>> built)
    {
        Matrix4x4 toParent = parent.worldToLocalMatrix;

        foreach (KeyValuePair<int, Transform> entry in built)
        {
            Transform root = entry.Value;
            Dictionary<string, List<CombineInstance>> groups = new Dictionary<string, List<CombineInstance>>();
            List<string> order = new List<string>();

            foreach (MeshFilter f in root.GetComponentsInChildren<MeshFilter>(true))
            {
                MeshRenderer r = f.GetComponent<MeshRenderer>();
                if (r == null || f.sharedMesh == null || r.sharedMaterial == null) continue;

                Material m = r.sharedMaterial;
                string key = m.shader.name + "|" + ColorUtility.ToHtmlStringRGBA(m.color) + "|" + m.renderQueue;
                if (!specialMaterials.ContainsKey(key)) specialMaterials[key] = m;
                else if (specialMaterials[key] != m) Object.Destroy(m);   // ya hay un material igual: se comparte

                if (!groups.TryGetValue(key, out List<CombineInstance> list))
                {
                    list = new List<CombineInstance>();
                    groups[key] = list;
                    order.Add(key);
                }
                list.Add(new CombineInstance { mesh = f.sharedMesh, transform = toParent * f.transform.localToWorldMatrix });
            }

            // Una malla por color y, juntas, una sola malla con un submaterial por color
            CombineInstance[] perColor = new CombineInstance[order.Count];
            Material[] mats = new Material[order.Count];
            List<Mesh> temp = new List<Mesh>();
            for (int i = 0; i < order.Count; i++)
            {
                Mesh colorMesh = new Mesh();
                colorMesh.CombineMeshes(groups[order[i]].ToArray(), true, true);
                temp.Add(colorMesh);
                perColor[i] = new CombineInstance { mesh = colorMesh, transform = Matrix4x4.identity };
                mats[i] = specialMaterials[order[i]];
            }

            Mesh mesh = new Mesh { name = "Recurso " + entry.Key };
            mesh.CombineMeshes(perColor, false, false);
            foreach (Mesh t in temp) Object.Destroy(t);

            GameObject view = new GameObject("Recurso " + entry.Key);
            view.transform.SetParent(parent, false);
            view.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer mr = view.AddComponent<MeshRenderer>();
            mr.sharedMaterials = mats;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            specialViews[entry.Key] = view;

            root.gameObject.SetActive(false);
            Object.Destroy(root.gameObject);
        }
    }

    /// <summary>Muestra solo los recursos de casillas ya exploradas (null = se ven todos).</summary>
    public static void ShowSpecials(bool[] explored)
    {
        foreach (KeyValuePair<int, GameObject> kv in specialViews)
        {
            if (kv.Value == null) continue;
            bool on = explored == null || explored[kv.Key];
            if (kv.Value.activeSelf != on) kv.Value.SetActive(on);
        }
    }

    static void BuildWheat(int cell, WorldGrid grid, Transform parent)
    {
        float size = grid.spacing * 0.5f;
        Material gold = M(0.95f, 0.78f, 0.2f);
        Material stalk = M(0.6f, 0.62f, 0.2f);
        Material soil = M(0.38f, 0.26f, 0.13f);

        GameObject root = Root("Trigo " + cell, parent, grid.pos[cell], grid.dir[cell]);
        root.transform.localRotation = Quaternion.FromToRotation(Vector3.up, grid.dir[cell]) * Quaternion.Euler(0f, (cell * 37) % 360, 0f);

        Piece(root.transform, Cylinder, soil, new Vector3(0f, size * 0.01f, 0f), new Vector3(size * 0.9f, size * 0.01f, size * 0.9f));
        for (int i = 0; i < 9; i++)
        {
            float a = i * 2.4f;
            float r = size * (0.08f + 0.28f * Mathf.Sqrt(i / 9f));
            Vector3 p = new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
            Piece(root.transform, Capsule, stalk, p + new Vector3(0f, size * 0.13f, 0f), new Vector3(size * 0.025f, size * 0.13f, size * 0.025f));
            Piece(root.transform, Capsule, gold, p + new Vector3(0f, size * 0.3f, 0f), new Vector3(size * 0.05f, size * 0.07f, size * 0.05f));
        }
    }

    // Banco de peces: tres peces plateados que asoman sobre el agua con unas ondas
    static void BuildFish(int cell, WorldGrid grid, Transform parent)
    {
        float size = grid.spacing * 0.5f;
        Material body = M(0.72f, 0.82f, 0.9f);
        Material belly = M(0.95f, 0.78f, 0.45f);
        Material ripple = M(0.55f, 0.75f, 0.95f);

        GameObject root = Root("Pescado " + cell, parent, grid.pos[cell], grid.dir[cell]);
        root.transform.localRotation = Quaternion.FromToRotation(Vector3.up, grid.dir[cell]) * Quaternion.Euler(0f, (cell * 71) % 360, 0f);
        Transform t = root.transform;

        Piece(t, Cylinder, ripple, new Vector3(0f, size * 0.005f, 0f), new Vector3(size * 0.95f, size * 0.004f, size * 0.95f));
        for (int i = 0; i < 3; i++)
        {
            float a = i * 2.1f + 0.5f;
            Vector3 c = new Vector3(Mathf.Cos(a) * size * 0.22f, size * 0.06f, Mathf.Sin(a) * size * 0.22f);
            float yaw = -a * Mathf.Rad2Deg + 90f;
            GameObject fish = new GameObject("Pez");
            fish.transform.SetParent(t, false);
            fish.transform.localPosition = c;
            fish.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            Piece(fish.transform, Sphere, body, Vector3.zero, new Vector3(size * 0.3f, size * 0.12f, size * 0.1f));
            Piece(fish.transform, Sphere, belly, new Vector3(0f, -size * 0.03f, 0f), new Vector3(size * 0.24f, size * 0.06f, size * 0.09f));
            Piece(fish.transform, Cube, body, new Vector3(-size * 0.18f, 0f, 0f), new Vector3(size * 0.05f, size * 0.14f, size * 0.02f));
        }
    }

    // Cantera de piedra: bloques de piedra clara tallados y apilados junto a un peñasco
    static void BuildStone(int cell, WorldGrid grid, Transform parent)
    {
        float size = grid.spacing * 0.5f;
        Material light = M(0.78f, 0.77f, 0.74f);
        Material mid = M(0.6f, 0.6f, 0.58f);
        Material dark = M(0.42f, 0.42f, 0.42f);

        GameObject root = Root("Piedra " + cell, parent, grid.pos[cell], grid.dir[cell]);
        root.transform.localRotation = Quaternion.FromToRotation(Vector3.up, grid.dir[cell]) * Quaternion.Euler(0f, (cell * 43) % 360, 0f);
        Transform t = root.transform;

        Piece(t, Sphere, dark, new Vector3(-size * 0.2f, size * 0.1f, size * 0.15f), new Vector3(size * 0.4f, size * 0.22f, size * 0.32f));
        Piece(t, Cube, light, new Vector3(size * 0.12f, size * 0.06f, -size * 0.05f), new Vector3(size * 0.3f, size * 0.12f, size * 0.2f), 12f);
        Piece(t, Cube, mid, new Vector3(size * 0.14f, size * 0.17f, -size * 0.04f), new Vector3(size * 0.24f, size * 0.1f, size * 0.17f), -8f);
        Piece(t, Cube, light, new Vector3(size * 0.25f, size * 0.05f, size * 0.22f), new Vector3(size * 0.2f, size * 0.1f, size * 0.14f), 30f);
        Piece(t, Cube, mid, new Vector3(-size * 0.05f, size * 0.04f, -size * 0.28f), new Vector3(size * 0.18f, size * 0.08f, size * 0.12f), -20f);
    }

    static void BuildCattle(int cell, WorldGrid grid, Transform parent)
    {
        float size = grid.spacing * 0.5f;
        Material hide = M(0.93f, 0.92f, 0.88f);
        Material spot = M(0.28f, 0.2f, 0.15f);
        Material pink = M(0.9f, 0.62f, 0.6f);
        Material horn = M(0.88f, 0.84f, 0.7f);

        GameObject root = Root("Ganado " + cell, parent, grid.pos[cell], grid.dir[cell]);
        root.transform.localRotation = Quaternion.FromToRotation(Vector3.up, grid.dir[cell]) * Quaternion.Euler(0f, (cell * 59) % 360, 0f);

        Transform t = root.transform;
        float h = size * 0.34f;
        Piece(t, Cube, hide, new Vector3(0f, h, 0f), new Vector3(size * 0.55f, size * 0.24f, size * 0.26f));
        Piece(t, Cube, spot, new Vector3(-size * 0.08f, h + size * 0.115f, size * 0.03f), new Vector3(size * 0.2f, size * 0.03f, size * 0.17f));
        Piece(t, Cube, hide, new Vector3(size * 0.34f, h + size * 0.07f, 0f), new Vector3(size * 0.2f, size * 0.18f, size * 0.18f));
        Piece(t, Cube, pink, new Vector3(size * 0.45f, h + size * 0.03f, 0f), new Vector3(size * 0.06f, size * 0.09f, size * 0.14f));
        Piece(t, Cube, horn, new Vector3(size * 0.33f, h + size * 0.19f, size * 0.08f), new Vector3(size * 0.03f, size * 0.07f, size * 0.03f));
        Piece(t, Cube, horn, new Vector3(size * 0.33f, h + size * 0.19f, -size * 0.08f), new Vector3(size * 0.03f, size * 0.07f, size * 0.03f));
        Piece(t, Cube, spot, new Vector3(-size * 0.3f, h + size * 0.02f, 0f), new Vector3(size * 0.03f, size * 0.2f, size * 0.03f));
        for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2)
                Piece(t, Cube, hide, new Vector3(sx * size * 0.2f, h * 0.45f, sz * size * 0.09f), new Vector3(size * 0.07f, h * 0.9f, size * 0.07f));
    }
}
