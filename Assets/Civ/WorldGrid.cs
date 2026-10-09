using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Malla de casillas sobre la esfera (retícula de Fibonacci): terreno, vecinas, pertenencia
/// y búsqueda de caminos A*. Todo en el espacio local de la Tierra.
/// </summary>
public class WorldGrid
{
    public int count;
    public Vector3[] dir;       // dirección unitaria de cada casilla
    public Vector3[] pos;       // punto sobre la superficie (con relieve)
    public Biome[] biome;
    public bool[] land;
    public int[][] nbr;
    public City[] tileCity;     // ciudad que controla la casilla
    public City[] cityAt;       // ciudad situada en la casilla
    public int[] component;     // masa de tierra a la que pertenece (-1 = mar)
    public List<int> componentSize = new List<int>();

    readonly HashSet<long> roads = new HashSet<long>();
    int[] roadCount;            // tramos de carretera que tocan cada casilla
    public bool[] irrigated;    // casillas con irrigación
    public bool[] mined;        // casillas con mina
    public bool[] farmed;       // casillas con granja
    public bool[] wonderAt;     // casillas ocupadas por una maravilla
    public bool[] river;        // casillas de tierra por las que pasa un río

    /// <summary>Cada río como lista de casillas, de la fuente (montaña) a la desembocadura (casilla de mar o de otro río).</summary>
    public readonly List<List<int>> riverPaths = new List<List<int>>();

    // Pares de casillas de tierra consecutivas en un río: recorrerlas va tan rápido como por una carretera
    readonly HashSet<long> riverEdges = new HashSet<long>();

    /// <summary>Pasar de a a b es seguir el curso de un río (entre dos casillas de tierra).</summary>
    public bool HasRiver(int a, int b) { return riverEdges.Contains(EdgeKey(a, b)); }

    public const float RoadSpeedFactor = 2f;

    static long EdgeKey(int a, int b)
    {
        return a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
    }

    public bool HasRoad(int a, int b) { return roads.Contains(EdgeKey(a, b)); }
    public void AddRoad(int a, int b)
    {
        if (!roads.Add(EdgeKey(a, b))) return;
        roadCount[a]++;
        roadCount[b]++;
    }

    /// <summary>La casilla tiene al menos un tramo de carretera que pasa por ella.</summary>
    public bool HasRoadAt(int cell) { return roadCount[cell] > 0; }

    public float spacing;       // distancia típica entre casillas (unidades de Unity)
    public float angularSpacing; // en radianes
    public float radius;

    public void Build(EarthTopography earth, int n)
    {
        count = n;
        radius = earth.FinalRadius;

        dir = new Vector3[n];
        pos = new Vector3[n];
        biome = new Biome[n];
        land = new bool[n];
        tileCity = new City[n];
        cityAt = new City[n];
        component = new int[n];
        roadCount = new int[n];
        irrigated = new bool[n];
        mined = new bool[n];
        farmed = new bool[n];
        wonderAt = new bool[n];
        river = new bool[n];

        float golden = Mathf.PI * (3f - Mathf.Sqrt(5f));
        for (int i = 0; i < n; i++)
        {
            float y = 1f - 2f * (i + 0.5f) / n;
            float r = Mathf.Sqrt(1f - y * y);
            float a = golden * i;

            dir[i] = new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r);
            pos[i] = earth.LocalSurfacePoint(dir[i]);
            biome[i] = earth.BiomeAt(dir[i]);
            land[i] = biome[i] != Biome.Sea;
        }

        angularSpacing = Mathf.Sqrt(4f * Mathf.PI / n);
        spacing = angularSpacing * radius;

        BuildNeighbors();
        BuildComponents();
        BuildRivers(earth);
    }

    // ---------------------------------------------------------------- Ríos

    // Genera los ríos del mapa: nacen en montañas separadas entre sí, bajan siempre por la casilla vecina más baja
    // y terminan al llegar al mar o a otro río. Con la misma semilla del planeta salen siempre los mismos ríos.
    void BuildRivers(EarthTopography earth)
    {
        riverPaths.Clear();
        riverEdges.Clear();

        float[] height = new float[count];
        for (int i = 0; i < count; i++) height[i] = earth.RelativeHeight(dir[i]);

        // Fuentes posibles: las casillas de montaña, en orden aleatorio (determinista)
        List<int> sources = new List<int>();
        for (int i = 0; i < count; i++) if (biome[i] == Biome.Mountain) sources.Add(i);

        System.Random rng = new System.Random(earth.seed * 31 + 7);
        for (int i = sources.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            int tmp = sources[i]; sources[i] = sources[j]; sources[j] = tmp;
        }

        int wanted = Mathf.Clamp(sources.Count / 5, 3, 30);
        List<int> chosen = new List<int>();

        foreach (int s in sources)
        {
            if (riverPaths.Count >= wanted) break;
            if (river[s]) continue;

            // Las fuentes no pueden estar demasiado juntas
            bool tooClose = false;
            foreach (int c in chosen)
                if (AngleBetween(s, c) < angularSpacing * 5f) { tooClose = true; break; }
            if (tooClose) continue;

            List<int> path = TraceRiver(s, height);
            if (path == null) continue;

            chosen.Add(s);
            riverPaths.Add(path);
            foreach (int c in path) if (land[c]) river[c] = true;
            for (int k = 0; k + 1 < path.Count; k++)
                if (land[path[k]] && land[path[k + 1]]) riverEdges.Add(EdgeKey(path[k], path[k + 1]));
        }
    }

    // Sigue el curso desde la fuente: cada paso va a la vecina más baja que no se haya visitado (si está en una cuenca,
    // sale por la menos alta). Devuelve null si no llega al mar ni a otro río, o si sale demasiado corto.
    List<int> TraceRiver(int source, float[] height)
    {
        List<int> path = new List<int> { source };
        HashSet<int> seen = new HashSet<int> { source };
        int cur = source;

        for (int step = 0; step < 120; step++)
        {
            int next = -1;
            float best = float.MaxValue;
            foreach (int nb in nbr[cur])
            {
                if (seen.Contains(nb) || height[nb] >= best) continue;
                best = height[nb];
                next = nb;
            }

            if (next < 0) return null;

            path.Add(next);
            seen.Add(next);

            if (!land[next]) return path.Count >= 5 ? path : null;   // desemboca en el mar
            if (river[next]) return path.Count >= 4 ? path : null;   // desemboca en otro río

            cur = next;
        }

        return null;
    }

    // Las 6 casillas más cercanas de cada una, hechas simétricas
    void BuildNeighbors()
    {
        const int k = 6;
        List<int>[] lists = new List<int>[count];
        for (int i = 0; i < count; i++) lists[i] = new List<int>(8);

        int[] best = new int[k];
        float[] bestDot = new float[k];

        for (int i = 0; i < count; i++)
        {
            for (int b = 0; b < k; b++) { best[b] = -1; bestDot[b] = -2f; }

            Vector3 di = dir[i];
            for (int j = 0; j < count; j++)
            {
                if (j == i) continue;

                float d = di.x * dir[j].x + di.y * dir[j].y + di.z * dir[j].z;
                if (d <= bestDot[k - 1]) continue;

                int slot = k - 1;
                while (slot > 0 && bestDot[slot - 1] < d)
                {
                    bestDot[slot] = bestDot[slot - 1];
                    best[slot] = best[slot - 1];
                    slot--;
                }
                bestDot[slot] = d;
                best[slot] = j;
            }

            for (int b = 0; b < k; b++)
            {
                int j = best[b];
                if (j < 0) continue;
                if (!lists[i].Contains(j)) lists[i].Add(j);
                if (!lists[j].Contains(i)) lists[j].Add(i);
            }
        }

        nbr = new int[count][];
        for (int i = 0; i < count; i++) nbr[i] = lists[i].ToArray();
    }

    void BuildComponents()
    {
        for (int i = 0; i < count; i++) component[i] = -1;

        Queue<int> queue = new Queue<int>();
        for (int i = 0; i < count; i++)
        {
            if (!land[i] || component[i] >= 0) continue;

            int id = componentSize.Count;
            int size = 0;
            component[i] = id;
            queue.Enqueue(i);

            while (queue.Count > 0)
            {
                int c = queue.Dequeue();
                size++;

                foreach (int nb in nbr[c])
                {
                    if (!land[nb] || component[nb] >= 0) continue;
                    component[nb] = id;
                    queue.Enqueue(nb);
                }
            }

            componentSize.Add(size);
        }
    }

    // ---------------------------------------------------------------- Consultas

    public int Nearest(Vector3 d)
    {
        int bestIndex = 0;
        float bestDot = -2f;

        for (int i = 0; i < count; i++)
        {
            float dot = d.x * dir[i].x + d.y * dir[i].y + d.z * dir[i].z;
            if (dot > bestDot) { bestDot = dot; bestIndex = i; }
        }

        return bestIndex;
    }

    public float AngleBetween(int a, int b)
    {
        return Mathf.Acos(Mathf.Clamp(Vector3.Dot(dir[a], dir[b]), -1f, 1f));
    }

    public bool Adjacent(int a, int b)
    {
        if (a == b) return true;
        foreach (int n in nbr[a]) if (n == b) return true;
        return false;
    }

    // Casillas a distancia de hasta "depth" saltos (incluye el origen)
    public List<int> Around(int center, int depth)
    {
        List<int> result = new List<int> { center };
        Dictionary<int, int> dist = new Dictionary<int, int> { { center, 0 } };
        Queue<int> queue = new Queue<int>();
        queue.Enqueue(center);

        while (queue.Count > 0)
        {
            int c = queue.Dequeue();
            if (dist[c] >= depth) continue;

            foreach (int nb in nbr[c])
            {
                if (dist.ContainsKey(nb)) continue;
                dist[nb] = dist[c] + 1;
                result.Add(nb);
                queue.Enqueue(nb);
            }
        }

        return result;
    }

    // owner: civilización que trabaja la casilla (sus tecnologías cambian el rendimiento); null = sin tecnologías
    public Vector3 TileYield(int cell, Civ owner = null)
    {
        Vector3 y = GameDefs.Yield(biome[cell]);
        if (owner != null && owner.Has(TechKind.Husbandry) && biome[cell] == Biome.Plains) y.x += 1f; // Ganadería: +1 comida en llanura
        if (owner != null && owner.Has(TechKind.WaterMill) && land[cell] && NearWater(cell)) y.x += 1f; // Molino hidráulico: +1 comida con agua cerca
        if (owner != null && owner.Has(TechKind.Camshaft) && land[cell] && NearWater(cell)) y.y += 1f; // Árbol de levas: +1 producción con agua cerca
        if (irrigated[cell]) y.x *= 1.5f; // la irrigación multiplica la comida de la casilla por 1,5
        if (farmed[cell]) y.x *= 2f;      // la granja duplica el alimento de la casilla
        if (mined[cell]) y.y *= 2f;       // la mina duplica la producción de la casilla
        return y;
    }

    /// <summary>La casilla tiene agua cerca: toca el mar o un río, o lo tiene en alguna casilla vecina.</summary>
    public bool NearWater(int cell)
    {
        if (river[cell]) return true;

        foreach (int nb in nbr[cell])
            if (!land[nb] || river[nb]) return true;
        return false;
    }

    /// <summary>Se puede hacer una granja en cualquier terreno de tierra salvo monte y montaña, si no tiene ya una.</summary>
    public bool CanFarm(int cell)
    {
        return land[cell] && biome[cell] != Biome.Mountain && biome[cell] != Biome.Hills && !farmed[cell] && !wonderAt[cell];
    }

    /// <summary>Se puede minar el monte y la montaña, si no tienen ya una mina.</summary>
    public bool CanMine(int cell)
    {
        return land[cell] && (biome[cell] == Biome.Hills || biome[cell] == Biome.Mountain) && !mined[cell];
    }

    /// <summary>Se puede irrigar cualquier terreno de tierra salvo la montaña, si no está ya irrigado.</summary>
    public bool CanIrrigate(int cell)
    {
        return land[cell] && biome[cell] != Biome.Mountain && !irrigated[cell];
    }

    // ---------------------------------------------------------------- Caminos A*

    // Camino de casillas (sin incluir la de origen). Solo por tierra; devuelve null si no hay.
    // No se puede atravesar una ciudad enemiga salvo que sea el destino.
    // Con naval = true solo se navega por casillas de mar.
    public List<int> FindPath(int from, int to, Civ mover, bool naval = false)
    {
        if (from == to) return new List<int>();
        if (naval)
        {
            if (land[to] || land[from]) return null;
        }
        else
        {
            if (!land[to] || !land[from]) return null;
            if (component[from] != component[to]) return null;
        }

        float[] g = new float[count];
        int[] came = new int[count];
        bool[] closed = new bool[count];
        for (int i = 0; i < count; i++) { g[i] = float.MaxValue; came[i] = -1; }

        // Montículo binario mínimo (f, nodo)
        List<KeyValuePair<float, int>> heap = new List<KeyValuePair<float, int>>();
        g[from] = 0f;
        Push(heap, Heuristic(from, to), from);

        while (heap.Count > 0)
        {
            int cur = Pop(heap);
            if (closed[cur]) continue;
            closed[cur] = true;

            if (cur == to) break;

            foreach (int nb in nbr[cur])
            {
                if (closed[nb] || land[nb] == naval) continue;

                City c = cityAt[nb];
                if (c != null && c.owner != mover && nb != to) continue;

                float factor = naval ? 1f : GameDefs.MoveFactor(biome[nb]);
                if (factor <= 0f) continue;
                if (!naval && (HasRoad(cur, nb) || HasRiver(cur, nb))) factor = RoadSpeedFactor;

                float step = Vector3.Distance(dir[cur], dir[nb]) / factor;
                float ng = g[cur] + step;

                if (ng < g[nb])
                {
                    g[nb] = ng;
                    came[nb] = cur;
                    Push(heap, ng + Heuristic(nb, to), nb);
                }
            }
        }

        if (came[to] < 0) return null;

        List<int> path = new List<int>();
        for (int c = to; c != from; c = came[c]) path.Add(c);
        path.Reverse();
        return path;
    }

    float Heuristic(int a, int b)
    {
        // Con carreteras el coste mínimo por casilla es la mitad, así que la heurística se reduce
        return Vector3.Distance(dir[a], dir[b]) / RoadSpeedFactor;
    }

    static void Push(List<KeyValuePair<float, int>> heap, float f, int node)
    {
        heap.Add(new KeyValuePair<float, int>(f, node));
        int i = heap.Count - 1;

        while (i > 0)
        {
            int parent = (i - 1) / 2;
            if (heap[parent].Key <= heap[i].Key) break;

            KeyValuePair<float, int> tmp = heap[parent];
            heap[parent] = heap[i];
            heap[i] = tmp;
            i = parent;
        }
    }

    static int Pop(List<KeyValuePair<float, int>> heap)
    {
        int result = heap[0].Value;
        int last = heap.Count - 1;
        heap[0] = heap[last];
        heap.RemoveAt(last);

        int i = 0;
        while (true)
        {
            int l = i * 2 + 1, r = l + 1, m = i;
            if (l < heap.Count && heap[l].Key < heap[m].Key) m = l;
            if (r < heap.Count && heap[r].Key < heap[m].Key) m = r;
            if (m == i) break;

            KeyValuePair<float, int> tmp = heap[m];
            heap[m] = heap[i];
            heap[i] = tmp;
            i = m;
        }

        return result;
    }
}
