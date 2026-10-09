using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Lógica del juego de civilizaciones en tiempo real sobre la Tierra:
/// civilizaciones, ciudades, economía, unidades, combate e IA.
/// Se crea sola al pulsar Play (si hay una EarthTopography en la escena).
/// </summary>
public class CivGame : MonoBehaviour
{
    [Header("Partida")]
    [Tooltip("Civilizaciones en la partida (1 = solo la tuya)")]
    [Range(1, 6)] public int civCount = 1;
    [Tooltip("Número de casillas del planeta (más = mapa más detallado, arranque más lento)")]
    public int gridCells = 6000;
    public float gameSpeed = 1f;

    [Header("Carreteras")]
    [Tooltip("Segundos que tarda un colono en construir un tramo de carretera")]
    public float roadBuildSeconds = 5f;

    [Header("Inteligencia artificial")]
    public bool aiEnabled = true;
    [Tooltip("Segundos antes de que la IA empiece a atacar")]
    public float aiGraceSeconds = 90f;

    [Header("Planeta")]
    [Tooltip("Velocidad de giro de la Tierra durante la partida")]
    public float earthSpin = 0.4f;

    public static CivGame Instance { get; private set; }

    public EarthTopography earth;
    public WorldGrid grid;
    public List<Civ> civs = new List<Civ>();
    public Civ human;
    public List<Unit> allUnits = new List<Unit>();
    public List<City> allCities = new List<City>();
    public Civ winner;
    // No serializado: si Unity recompila los scripts durante el Play, el estado de la partida se pierde
    // (civilizaciones, mapa...) y "ready" debe volver a false en vez de quedarse en true con datos vacíos
    [System.NonSerialized] public bool ready;
    public float gameTime;

    public struct LogEntry { public string text; public float time; public Color color; }
    public List<LogEntry> log = new List<LogEntry>();

    public Transform EarthT => earth.transform;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Create()
    {
        if (FindFirstObjectByType<EarthTopography>() == null) return;
        if (FindFirstObjectByType<CivGame>() != null) return;

        GameObject go = new GameObject("Juego de civilizaciones");
        go.AddComponent<CivGame>();
        go.AddComponent<CivUI>();
    }

    void Awake()
    {
        Instance = this;
    }

    IEnumerator Start()
    {
        earth = FindFirstObjectByType<EarthTopography>();

        // Espera a que el planeta termine de generarse
        while (earth != null && !earth.IsReady) yield return null;
        yield return null;
        if (earth == null) yield break;

        earth.rotationSpeed = earthSpin;

        grid = new WorldGrid();
        grid.Build(earth, gridCells);
        CivViews.BuildRivers(grid, earth, EarthT);
        Debug.Log("[Civilogenia] Mapa: " + grid.riverPaths.Count + " ríos.");

        if (!SetupCivs())
        {
            Debug.LogError("[Civilogenia] No hay tierra suficiente para empezar la partida.");
            yield break;
        }

        ready = true;
    }

    // ---------------------------------------------------------------- Preparación

    bool SetupCivs()
    {
        List<int> starts = ChooseStarts(civCount);
        if (starts.Count == 0) return false;

        for (int i = 0; i < starts.Count; i++)
        {
            Civ civ = new Civ
            {
                id = i,
                name = GameDefs.CivNames[i % GameDefs.CivNames.Length],
                color = GameDefs.CivColors[i % GameDefs.CivColors.Length],
                isHuman = i == 0,
                aiTimer = Random.Range(0f, 2f)
            };

            civs.Add(civ);
            if (civ.isHuman) human = civ;

            City capital = FoundCity(civ, starts[i]);
            if (capital != null)
            {
                SpawnUnit(civ, UnitKind.Warrior, starts[i]);
                capital.hp = capital.maxHp;
            }
        }

        AddLog("¡Comienza la partida! Eres " + human.name + ". Ayuda: H", human.color);
        if (human.cities.Count > 0) FocusOn(human.cities[0].cell);
        return true;
    }

    List<int> ChooseStarts(int n)
    {
        int main = 0;
        for (int i = 1; i < grid.componentSize.Count; i++)
            if (grid.componentSize[i] > grid.componentSize[main]) main = i;

        List<int> candidates = new List<int>();
        for (int i = 0; i < grid.count; i++)
        {
            if (!grid.land[i]) continue;
            if (Mathf.Abs(grid.dir[i].y) > 0.8f) continue;
            if (grid.componentSize[grid.component[i]] < 40) continue;
            if (grid.biome[i] != Biome.Plains && grid.biome[i] != Biome.Forest) continue;
            candidates.Add(i);
        }

        // Si no hay sitio bueno, vale cualquier tierra
        if (candidates.Count == 0)
            for (int i = 0; i < grid.count; i++) if (grid.land[i]) candidates.Add(i);

        List<int> chosen = new List<int>();
        if (candidates.Count == 0) return chosen;

        List<int> inMain = candidates.FindAll(c => grid.component[c] == main);
        chosen.Add(inMain.Count > 0 ? inMain[Random.Range(0, inMain.Count)] : candidates[Random.Range(0, candidates.Count)]);

        float minSeparation = 7f * grid.angularSpacing;

        while (chosen.Count < n)
        {
            int bestMain = PickFarthest(candidates, chosen, main, true, out float angleMain);
            int pick = bestMain;

            // Si en el continente principal ya no cabe otra, se usa la mejor de cualquier masa de tierra
            if (pick < 0 || angleMain < minSeparation)
            {
                int any = PickFarthest(candidates, chosen, main, false, out float angleAny);
                if (any >= 0 && angleAny >= minSeparation * 0.6f) pick = any;
            }

            if (pick < 0) break;
            chosen.Add(pick);
        }

        return chosen;
    }

    int PickFarthest(List<int> candidates, List<int> chosen, int component, bool onlyComponent, out float bestAngle)
    {
        int best = -1;
        bestAngle = -1f;

        foreach (int c in candidates)
        {
            if (onlyComponent && grid.component[c] != component) continue;

            float minAngle = float.MaxValue;
            foreach (int s in chosen) minAngle = Mathf.Min(minAngle, grid.AngleBetween(c, s));

            if (minAngle > bestAngle) { bestAngle = minAngle; best = c; }
        }

        return best;
    }

    public void FocusOn(int cell)
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        Vector3 current = EarthT.TransformDirection(grid.dir[cell]);
        Vector3 wanted = (cam.transform.position - EarthT.position).normalized;
        EarthT.rotation = Quaternion.FromToRotation(current, wanted) * EarthT.rotation;
    }

    public void AddLog(string text, Color color)
    {
        log.Add(new LogEntry { text = text, time = Time.time, color = color });
        if (log.Count > 40) log.RemoveAt(0);
    }

    // ---------------------------------------------------------------- Bucle principal

    void Update()
    {
        if (!ready) return;
        if (IntroVideo.Active) return;   // la partida espera mientras suena la introducción

        float dt = Time.deltaTime * gameSpeed;
        if (dt <= 0f) return;
        gameTime += dt;

        foreach (City c in allCities.ToArray())
            if (c.alive) TickCity(c, dt);

        foreach (Civ civ in civs)
        {
            if (!civ.alive) continue;

            TickResearch(civ, dt);
            civ.gold += civ.goldRate * dt;

            if (aiEnabled && !civ.isHuman)
            {
                civ.aiTimer -= dt;
                if (civ.aiTimer <= 0f)
                {
                    civ.aiTimer = 1.5f;
                    AiThink(civ);
                }
            }
        }

        foreach (Unit u in allUnits.ToArray())
            if (u.alive) TickUnit(u, dt);

        foreach (Civ civ in civs)
            if (civ.territoryDirty) CivViews.RebuildTerritory(civ, grid, earth, EarthT);
    }

    // ---------------------------------------------------------------- Ciudades

    public bool CanFound(Civ civ, int cell)
    {
        if (!grid.land[cell] || grid.biome[cell] == Biome.Mountain) return false;
        if (grid.cityAt[cell] != null) return false;

        City owner = grid.tileCity[cell];
        if (owner != null && owner.owner != civ) return false;

        foreach (int c in grid.Around(cell, 3))
            if (grid.cityAt[c] != null) return false;

        return true;
    }

    public City FoundCity(Civ civ, int cell)
    {
        if (!CanFound(civ, cell)) return null;

        City city = new City
        {
            owner = civ,
            name = GameDefs.CityNames[(civ.id * 5 + civ.nameCounter++) % GameDefs.CityNames.Length]
                   + (civ.nameCounter > GameDefs.CityNames.Length ? " " + civ.nameCounter : ""),
            cell = cell,
            maxHp = 40f,
            recalcTimer = 3f
        };
        city.hp = city.maxHp;

        civ.cities.Add(city);
        allCities.Add(city);
        grid.cityAt[cell] = city;

        ClaimTiles(city, 2);
        RecomputeCity(city);
        RebuildCityView(city);

        civ.territoryDirty = true;
        AddLog(civ.name + " funda " + city.name, civ.color);
        return city;
    }

    // La ciudad ocupa una celda por cada 10 de población (mínimo 1, la central)
    public const int PopPerCell = 10;

    void RebuildCityView(City c)
    {
        UpdateFootprint(c);
        CivViews.BuildCity(c, grid, EarthT);
    }

    void UpdateFootprint(City c)
    {
        if (c.cells.Count == 0) c.cells.Add(c.cell);

        int wanted = Mathf.Max(1, Mathf.CeilToInt(c.pop / (float)PopPerCell));

        // Libera las celdas sobrantes (las últimas añadidas)
        while (c.cells.Count > wanted)
        {
            int last = c.cells[c.cells.Count - 1];
            grid.cityAt[last] = null;
            c.cells.RemoveAt(c.cells.Count - 1);
        }

        // Ocupa celdas vecinas libres de tierra, las más cercanas al centro primero
        while (c.cells.Count < wanted)
        {
            int best = -1;
            float bestDot = -2f;

            foreach (int cell in c.cells)
            {
                foreach (int nb in grid.nbr[cell])
                {
                    if (!grid.land[nb] || grid.biome[nb] == Biome.Mountain || grid.cityAt[nb] != null || grid.wonderAt[nb]) continue;
                    if (grid.tileCity[nb] != null && grid.tileCity[nb] != c) continue;

                    float dot = Vector3.Dot(grid.dir[nb], grid.dir[c.cell]);
                    if (dot > bestDot) { bestDot = dot; best = nb; }
                }
            }

            if (best < 0) break; // sin sitio: se queda con las que tiene

            c.cells.Add(best);
            grid.cityAt[best] = c;
            if (grid.tileCity[best] == null) { grid.tileCity[best] = c; c.tiles.Add(best); }
        }

        c.owner.territoryDirty = true;
    }

    void ClaimTiles(City city, int depth)
    {
        foreach (int c in grid.Around(city.cell, depth))
        {
            if (grid.tileCity[c] != null) continue;
            grid.tileCity[c] = city;
            city.tiles.Add(c);
        }

        city.owner.territoryDirty = true;
    }

    // Segundos que tarda una ciudad en reclamar cada casilla nueva de su territorio. Antes todo el anillo se reclamaba de golpe
    // en cuanto la ciudad llegaba a 4 u 8 habitantes; ahora se va ampliando de una en una.
    public const float TerritoryTileSeconds = 10f;

    // Expansión gradual: cada TerritoryTileSeconds la ciudad reclama la casilla libre más cercana dentro de su radio objetivo
    void TickTerritory(City c, float dt)
    {
        c.claimTimer += dt;
        if (c.claimTimer < TerritoryTileSeconds) return;
        c.claimTimer = 0f;

        int best = -1;
        float bestAngle = float.MaxValue;
        foreach (int cell in grid.Around(c.cell, c.claimDepth))
        {
            if (grid.tileCity[cell] != null) continue;
            float a = grid.AngleBetween(c.cell, cell);
            if (a < bestAngle) { bestAngle = a; best = cell; }
        }

        if (best < 0) return;

        grid.tileCity[best] = c;
        c.tiles.Add(best);
        c.owner.territoryDirty = true;
        RecomputeCity(c);
    }

    // Elige las casillas que trabaja la ciudad (las mejores, tantas como habitantes) y calcula sus tasas
    public void RecomputeCity(City city)
    {
        List<int> candidates = new List<int>();
        foreach (int c in city.tiles) if (c != city.cell && grid.tileCity[c] == city && !grid.wonderAt[c]) candidates.Add(c);

        candidates.Sort((a, b) => TileScore(b, city.owner).CompareTo(TileScore(a, city.owner)));

        Vector3 total = grid.TileYield(city.cell, city.owner) + new Vector3(1f, 1f, 1f);
        int worked = Mathf.Min(city.pop, candidates.Count);
        city.worked.Clear();
        for (int i = 0; i < worked; i++)
        {
            total += grid.TileYield(candidates[i], city.owner);
            city.worked.Add(candidates[i]);
        }

        // Cada casilla de la ciudad con carretera da +1 de oro y +1 de ciencia
        int roadTiles = 0;
        foreach (int c in city.tiles) if (grid.tileCity[c] == city && grid.HasRoadAt(c)) roadTiles++;

        city.foodRate = total.x;
        city.prodRate = total.y + (city.buildings.Contains(BuildingKind.Workshop) ? 2f : 0f)
                        + (city.buildings.Contains(BuildingKind.FullingMill) ? 3f : 0f);
        city.goldRate = total.z + roadTiles + (city.buildings.Contains(BuildingKind.Market) ? 2f : 0f);
        city.scienceRate = (0.4f + 0.3f * city.pop + roadTiles) * (city.buildings.Contains(BuildingKind.Library) ? 2f : 1f);
        if (city.buildings.Contains(BuildingKind.Bank)) city.goldRate *= 1.5f; // Banco: +50 % de oro en esta ciudad
        if (city.owner.Has(TechKind.ArabicNumerals))
        {
            city.goldRate *= 1.25f; // Cifras arábigas: +25 % de oro y de ciencia en todas las ciudades
            city.scienceRate *= 1.25f;
        }
        if (city.owner.Has(TechKind.Universities)) city.scienceRate *= 1.25f; // Universidades: +25 % en todas las ciudades
        if (city.owner.Has(TechKind.Philosophy)) city.scienceRate *= 1.5f; // Filosofía: +50 % en todas las ciudades
        if (city.buildings.Contains(BuildingKind.University)) city.scienceRate *= 1.75f; // Universidad: +75 % en esta ciudad
        if (city.buildings.Contains(BuildingKind.Observatory)) city.scienceRate *= 1.5f; // Observatorio: +50 % en esta ciudad
        if (OwnsGreatLibrary(city.owner)) city.scienceRate *= 1.5f; // Gran Biblioteca: +50 % en todas las ciudades
        if (OwnsBuilding(city.owner, BuildingKind.AlexandriaLibrary)) city.scienceRate *= 1.4f; // Gran Biblioteca de Alejandría: +40 % en todas las ciudades
        if (OwnsBuilding(city.owner, BuildingKind.FibonacciLab)) city.scienceRate *= 1.25f; // Laboratorio de Fibonacci: +25 % en todas las ciudades

        // Cada ruta comercial activa da +15 % de oro y de ciencia a la ciudad
        float trade = 1f + TradeBonusOf(city);
        city.goldRate *= trade;
        city.scienceRate *= trade;

        UpdateCivRates(city.owner);
    }

    float TileScore(int c, Civ owner)
    {
        Vector3 y = grid.TileYield(c, owner);
        return y.x * 1.4f + y.y * 1.2f + y.z * 0.8f;
    }

    void UpdateCivRates(Civ civ)
    {
        civ.goldRate = 0f;
        civ.scienceRate = 0f;

        foreach (City c in civ.cities)
        {
            civ.goldRate += c.goldRate;
            civ.scienceRate += c.scienceRate;
        }
    }

    void TickCity(City c, float dt)
    {
        c.recalcTimer -= dt;
        if (c.recalcTimer <= 0f)
        {
            c.recalcTimer = 3f;
            RecomputeCity(c);
        }

        TickTerritory(c, dt);

        // Comida y crecimiento
        c.food += c.FoodSurplus * dt;

        // Sin Fuego la ciudad no pasa de PopWithoutFire habitantes
        bool canGrow = c.pop < GameDefs.PopWithoutFire || c.owner.Has(TechKind.Fire);

        if (c.food >= 12f + c.pop * 8f && canGrow)
        {
            c.food = 0f;
            c.pop++;

            // El territorio se amplía poco a poco hacia el nuevo radio (ver TickTerritory)
            if (c.pop == 4) c.claimDepth = Mathf.Max(c.claimDepth, 3);
            if (c.pop == 8) c.claimDepth = Mathf.Max(c.claimDepth, 4);

            RecomputeCity(c);
            RebuildCityView(c);
            if (c.owner.isHuman) AddLog(c.name + " crece a " + c.pop + " habitantes", c.owner.color);
        }
        else if (c.food < -10f && c.pop > 1)
        {
            c.pop--;
            c.food = 0f;
            RecomputeCity(c);
            RebuildCityView(c);
            if (c.owner.isHuman) AddLog("Hambruna en " + c.name, Color.red);
        }
        else if (c.food < 0f && c.pop == 1)
        {
            c.food = 0f;
        }

        // Producción
        if (c.hasItem && !c.itemIsUnit && IsWonder(c.itemBuilding) && WonderExists(c.itemBuilding))
        {
            // Otra ciudad se adelantó: la maravilla ya existe, esta producción se cancela (lo acumulado se conserva)
            c.hasItem = false;
            if (c.owner.isHuman) AddLog(c.name + ": " + GameDefs.Buildings[c.itemBuilding].name + " ya existe en el mundo", Color.red);
        }

        if (c.hasItem)
        {
            bool blocked = ItemBlocked(c);
            if (!blocked)
            {
                c.prodStock += c.prodRate * dt;
                if (c.prodStock >= c.ItemCost) CompleteItem(c);
            }
        }

        c.hp = Mathf.Min(c.maxHp, c.hp + 0.5f * dt);

        // Las ciudades disparan a los enemigos que tengan al lado
        c.shotCooldown -= dt;
        if (c.shotCooldown <= 0f)
        {
            foreach (Unit u in allUnits.ToArray())
            {
                if (!u.alive || u.owner == c.owner || !u.def.IsMilitary) continue;
                if (!grid.Adjacent(c.cell, u.cell)) continue;

                u.hp -= 3f + (c.buildings.Contains(BuildingKind.Walls) ? 2f : 0f);
                if (u.hp <= 0f) KillUnit(u);

                c.shotCooldown = 1.5f;
                break;
            }

            if (c.shotCooldown <= 0f) c.shotCooldown = 0.5f;
        }
    }

    bool ItemBlocked(City c)
    {
        if (!c.itemIsUnit) return false;
        UnitDef d = GameDefs.Units[c.itemUnit];
        return d.popCost > 0 && c.pop <= d.popCost;
    }

    void CompleteItem(City c)
    {
        c.prodStock -= c.ItemCost;
        c.hasItem = false;

        if (c.itemIsUnit)
        {
            UnitDef d = GameDefs.Units[c.itemUnit];
            c.pop -= d.popCost;
            int spawnCell = d.naval ? SeaCellNextTo(c) : c.cell;
            Unit built = SpawnUnit(c.owner, c.itemUnit, spawnCell >= 0 ? spawnCell : c.cell);
            if (built.kind == UnitKind.Caravan) built.homeCity = c;
            RecomputeCity(c);
            RebuildCityView(c);
            if (c.owner.isHuman) AddLog(c.name + ": " + d.name + " listo", c.owner.color);
        }
        else
        {
            c.buildings.Add(c.itemBuilding);
            if (c.itemBuilding == BuildingKind.Walls) c.maxHp += 25f;
            if (IsWonder(c.itemBuilding)) PlaceWonder(c, c.itemBuilding);

            // Las maravillas de ciencia mejoran todas las ciudades de la civilización
            if (IsWonder(c.itemBuilding))
                foreach (City other in c.owner.cities) RecomputeCity(other);

            RecomputeCity(c);
            RebuildCityView(c);
            if (c.owner.isHuman) AddLog(c.name + ": " + GameDefs.Buildings[c.itemBuilding].name + " construido", c.owner.color);
        }
    }

    public bool Available(Civ civ, UnitKind kind)
    {
        if (!HasTechFor(civ, kind)) return false;

        // Obsoleta: si existe una versión más actual que se pueda construir, la antigua ya no se construye
        UnitKind? newer = UpgradeTarget(civ, kind);
        return newer == null || !HasTechFor(civ, newer.Value);
    }

    bool HasTechFor(Civ civ, UnitKind kind)
    {
        // Sin tecnologías solo hay colonos, trabajadores y guerreros; el resto exige su tecnología
        switch (kind)
        {
            case UnitKind.Spearman: return civ.Has(TechKind.Mining);
            case UnitKind.Archer:   return civ.Has(TechKind.Archery);
            case UnitKind.Horseman: return civ.Has(TechKind.Riding);
            case UnitKind.Chariot:  return civ.Has(TechKind.Wheel);
            case UnitKind.Phalanx:  return civ.Has(TechKind.Bronze);
            case UnitKind.Legion:   return civ.Has(TechKind.Iron);
            case UnitKind.Catapult: return civ.Has(TechKind.Mathematics);
            case UnitKind.Sailboat: return civ.Has(TechKind.Sailing);
            case UnitKind.Caravan:  return civ.Has(TechKind.Currency);
            case UnitKind.Merchant: return false; // solo mejorando una caravana
            default:                return true;
        }
    }

    public bool Available(City city, UnitKind kind)
    {
        if (!Available(city.owner, kind)) return false;
        return !GameDefs.Units[kind].naval || SeaCellNextTo(city) >= 0;
    }

    /// <summary>Casilla de mar contigua a la ciudad (donde aparecen los barcos), o -1 si no tiene acceso al agua.</summary>
    public int SeaCellNextTo(City city)
    {
        foreach (int nb in grid.nbr[city.cell])
            if (!grid.land[nb]) return nb;
        return -1;
    }

    // Las maravillas (Gran Biblioteca, Laboratorio de Fibonacci): solo puede existir una de cada en todo el mundo
    static bool IsWonder(BuildingKind kind)
    {
        return kind == BuildingKind.GreatLibrary || kind == BuildingKind.FibonacciLab || kind == BuildingKind.AlexandriaLibrary;
    }

    // La maravilla se levanta en una celdilla libre del territorio de la ciudad (la más cercana al centro);
    // si no queda ninguna, se dibuja junto a la ciudad como antes
    void PlaceWonder(City c, BuildingKind kind)
    {
        int best = -1;
        float bestAngle = float.MaxValue;

        foreach (int cell in c.tiles)
        {
            if (grid.tileCity[cell] != c || !grid.land[cell] || grid.biome[cell] == Biome.Mountain) continue;
            if (grid.cityAt[cell] != null || grid.farmed[cell] || grid.wonderAt[cell]) continue;

            float a = grid.AngleBetween(c.cell, cell);
            if (a < bestAngle) { bestAngle = a; best = cell; }
        }

        if (best < 0) return;

        grid.wonderAt[best] = true;
        c.wonderCells[kind] = best;
    }

    bool WonderExists(BuildingKind kind)
    {
        foreach (City c in allCities)
            if (c.alive && c.buildings.Contains(kind)) return true;
        return false;
    }

    bool GreatLibraryExists() { return WonderExists(BuildingKind.GreatLibrary); }

    bool OwnsBuilding(Civ civ, BuildingKind kind)
    {
        foreach (City c in civ.cities)
            if (c.buildings.Contains(kind)) return true;
        return false;
    }

    bool OwnsGreatLibrary(Civ civ) { return OwnsBuilding(civ, BuildingKind.GreatLibrary); }

    public bool Available(City city, BuildingKind kind)
    {
        if (kind == BuildingKind.Granary && !city.owner.Has(TechKind.Agriculture)) return false;
        if (kind == BuildingKind.Walls && !city.owner.Has(TechKind.Masonry)) return false;
        if (kind == BuildingKind.Library && !city.owner.Has(TechKind.Writing)) return false;
        if (kind == BuildingKind.Observatory && !city.owner.Has(TechKind.Astronomy)) return false;
        if (kind == BuildingKind.GreatLibrary && (!city.owner.Has(TechKind.Writing) || GreatLibraryExists())) return false;
        if (kind == BuildingKind.AlexandriaLibrary && (!city.owner.Has(TechKind.Universities) || WonderExists(kind))) return false;
        if (kind == BuildingKind.University && !city.owner.Has(TechKind.Universities)) return false;
        if (kind == BuildingKind.FullingMill && !city.owner.Has(TechKind.Camshaft)) return false;
        if (kind == BuildingKind.Academy && !city.owner.Has(TechKind.Cities)) return false;
        if (kind == BuildingKind.Bank && !city.owner.Has(TechKind.ArabicNumerals)) return false;
        if (kind == BuildingKind.FibonacciLab && (!city.owner.Has(TechKind.ArabicNumerals) || WonderExists(kind))) return false;
        return !city.buildings.Contains(kind);
    }

    public void SetProduction(City city, UnitKind kind)
    {
        if (!Available(city, kind)) return;
        city.hasItem = true;
        city.itemIsUnit = true;
        city.itemUnit = kind;
    }

    public void SetProduction(City city, BuildingKind kind)
    {
        if (!Available(city, kind)) return;
        city.hasItem = true;
        city.itemIsUnit = false;
        city.itemBuilding = kind;
    }

    public int BuyPrice(City city)
    {
        if (!city.hasItem) return 0;
        return Mathf.CeilToInt(Mathf.Max(0f, city.ItemCost - city.prodStock) * 2f);
    }

    public bool Buy(City city)
    {
        if (!city.hasItem || ItemBlocked(city)) return false;

        int price = BuyPrice(city);
        if (city.owner.gold < price) return false;

        city.owner.gold -= price;
        city.prodStock = city.ItemCost;
        CompleteItem(city);
        return true;
    }

    // ---------------------------------------------------------------- Mejora de unidades (Academia)

    /// <summary>Unidad actual en la que se transforma 'kind' con las tecnologías de la civilización, o null si no tiene mejora.</summary>
    public UnitKind? UpgradeTarget(Civ civ, UnitKind kind)
    {
        switch (kind)
        {
            case UnitKind.Warrior:
                if (civ.Has(TechKind.Iron)) return UnitKind.Legion;
                if (civ.Has(TechKind.Mining)) return UnitKind.Spearman;
                return null;
            case UnitKind.Spearman: return civ.Has(TechKind.Iron) ? UnitKind.Legion : (UnitKind?)null;
            case UnitKind.Horseman: return civ.Has(TechKind.Wheel) ? UnitKind.Chariot : (UnitKind?)null;
            case UnitKind.Caravan:  return civ.Has(TechKind.ArabicNumerals) ? UnitKind.Merchant : (UnitKind?)null;
            default: return null;
        }
    }

    /// <summary>Oro que cuesta la mejora: la diferencia de coste entre la unidad nueva y la actual.</summary>
    public int UpgradePrice(Unit u)
    {
        UnitKind? target = UpgradeTarget(u.owner, u.kind);
        if (target == null) return 0;
        return Mathf.Max(0, Mathf.CeilToInt(GameDefs.Units[target.Value].cost - u.def.cost));
    }

    /// <summary>Por qué no se puede mejorar la unidad ahora (null si se puede).</summary>
    public string UpgradeBlocker(Unit u)
    {
        if (UpgradeTarget(u.owner, u.kind) == null) return "No tiene mejora con tus tecnologías.";

        City here = grid.cityAt[u.cell];
        if (u.Moving || here == null || here.owner != u.owner || !here.buildings.Contains(BuildingKind.Academy))
            return "Debe estar parada en una ciudad tuya con Academia.";

        if (u.owner.gold < UpgradePrice(u)) return "Faltan " + (UpgradePrice(u) - Mathf.FloorToInt(u.owner.gold)) + " de oro.";
        return null;
    }

    public bool TryUpgrade(Unit u)
    {
        if (u == null || !u.alive || UpgradeBlocker(u) != null) return false;

        UnitKind target = UpgradeTarget(u.owner, u.kind).Value;
        float hpFraction = u.hp / u.def.hp;
        string oldName = u.def.name;

        u.owner.gold -= UpgradePrice(u);
        u.kind = target;
        u.def = GameDefs.Units[target];
        u.hp = u.def.hp * hpFraction;

        CivViews.BuildUnit(u, grid, EarthT);
        PlaceUnit(u);

        if (u.owner.isHuman) AddLog(oldName + " mejorado a " + u.def.name, u.owner.color);
        return true;
    }

    // ---------------------------------------------------------------- Tecnologías

    public bool StartResearch(Civ civ, TechKind t)
    {
        if (!civ.CanResearch(t)) return false;

        civ.researching = true;
        civ.currentTech = t;
        civ.researchProgress = 0f;
        return true;
    }

    void TickResearch(Civ civ, float dt)
    {
        if (!civ.researching) return;

        civ.researchProgress += civ.scienceRate * dt;

        TechDef def = GameDefs.Techs[civ.currentTech];
        if (civ.researchProgress < def.cost) return;

        civ.techs.Add(civ.currentTech);
        civ.researching = false;
        civ.researchProgress = 0f;

        if (civ.isHuman) AddLog("Descubres " + def.name + " (" + def.unlocks + ")", civ.color);

        // La hoguera (Fuego) se ve en las ciudades; la Ganadería cambia el rendimiento de las casillas
        foreach (City c in civ.cities)
        {
            RecomputeCity(c);
            CivViews.BuildCity(c, grid, EarthT);
        }

        // Cambio de era: las carreteras de la civilización pasan a piedra y sus unidades a su modelo medieval
        if (civ.currentTech == TechKind.EraChange)
            CivViews.UpgradeRoads(civ, grid, earth, EarthT);

        if (civ.currentTech == TechKind.EraChange)
            foreach (Unit u in civ.units.ToArray())
            {
                if (!u.alive) continue;
                CivViews.BuildUnit(u, grid, EarthT);
                PlaceUnit(u);
                if (u.carriedBy != null && u.view != null) u.view.SetActive(false);
            }

        // Cambio de era del jugador: vídeo "Video cambio a edad media" (Assets/Resources) a pantalla completa
        if (civ.isHuman && def == GameDefs.Techs[TechKind.EraChange])
            IntroVideo.PlayClip(EraChangeVideo);
    }

    const string EraChangeVideo = "Video cambio a edad media";

    // ---------------------------------------------------------------- Unidades

    public Unit SpawnUnit(Civ civ, UnitKind kind, int cell)
    {
        Unit u = new Unit
        {
            owner = civ,
            kind = kind,
            def = GameDefs.Units[kind],
            cell = cell,
            jitter = new Vector2(Random.Range(-0.3f, 0.3f), Random.Range(-0.3f, 0.3f)) * grid.spacing,
            scanTimer = Random.Range(0f, 0.6f)
        };
        u.hp = u.def.hp;

        civ.units.Add(u);
        allUnits.Add(u);

        CivViews.BuildUnit(u, grid, EarthT);
        PlaceUnit(u);
        return u;
    }

    public void KillUnit(Unit u)
    {
        if (!u.alive) return;
        u.alive = false;

        u.owner.units.Remove(u);
        allUnits.Remove(u);
        if (u.view != null) Destroy(u.view);

        // Si se hunde el barco, se pierde también su carga
        if (u.cargo != null)
        {
            Unit passenger = u.cargo;
            u.cargo = null;
            passenger.carriedBy = null;
            KillUnit(passenger);
        }
        if (u.carriedBy != null) u.carriedBy.cargo = null;

        if (u.owner.isHuman) AddLog("Pierdes un " + u.def.name, Color.red);
        CheckElimination(u.owner);
    }

    void PlaceUnit(Unit u)
    {
        Vector3 d, p;

        if (u.Moving)
        {
            d = Vector3.Slerp(grid.dir[u.cell], grid.dir[u.moveTo], u.t);
            // Entre casillas se interpolan los puntos de la superficie (el planeta es un esferoide)
            p = Vector3.Lerp(grid.pos[u.cell], grid.pos[u.moveTo], u.t);
        }
        else
        {
            d = grid.dir[u.cell];
            p = grid.pos[u.cell];
        }

        u.currentDir = d;

        Vector3 t1 = Vector3.Cross(Vector3.up, d);
        if (t1.sqrMagnitude < 1e-6f) t1 = Vector3.Cross(Vector3.right, d);
        t1.Normalize();
        Vector3 t2 = Vector3.Cross(d, t1);

        p += t1 * u.jitter.x + t2 * u.jitter.y;

        u.view.transform.localPosition = p;
        Quaternion rot = Quaternion.FromToRotation(Vector3.up, d);

        // Barcos y caravanas apuntan el frente (+Z del modelo) hacia la casilla a la que se dirigen
        if (u.def.naval || u.IsTrader)
        {
            if (u.Moving)
            {
                Vector3 toward = Vector3.ProjectOnPlane(grid.dir[u.moveTo] - grid.dir[u.cell], d);
                if (toward.sqrMagnitude > 1e-8f) u.heading = toward.normalized;
            }

            Vector3 fwd = Vector3.ProjectOnPlane(u.heading, d);
            if (fwd.sqrMagnitude > 1e-8f) rot = Quaternion.LookRotation(fwd.normalized, d);
        }

        u.view.transform.localRotation = rot;
    }

    // ---------------------------------------------------------------- Carreteras

    // Orden de carretera hasta una ciudad propia (la usa la IA)
    public bool OrderRoad(Unit u, City to)
    {
        if (to == null || !to.alive || to.owner != u.owner) return false;
        return OrderRoad(u, to.cell);
    }

    /// <summary>
    /// Orden de carretera: el trabajador construye, casilla a casilla y desde donde está, una carretera
    /// visible hasta la casilla de tierra indicada. Las casillas que ya tienen carretera no cuestan tiempo.
    /// </summary>
    public bool OrderRoad(Unit u, int goal)
    {
        if (u.kind != UnitKind.Worker || !grid.land[goal]) return false;

        if (grid.river[goal] && !u.owner.Has(TechKind.Bridges))
        {
            if (u.owner.isHuman) AddLog("Necesitas descubrir la Construcción de puentes para hacer carreteras sobre un río (T)", Color.red);
            return false;
        }

        // No se puede terminar una carretera dentro de una ciudad ajena
        City at = grid.cityAt[goal];
        if (at != null && at.owner != u.owner) return false;

        if (!u.owner.Has(TechKind.Wheel))
        {
            if (u.owner.isHuman) AddLog("Necesitas descubrir la Rueda para construir caminos (T)", Color.red);
            return false;
        }

        int start = u.Moving ? u.moveTo : u.cell;
        if (start == goal) return false;

        List<int> route = grid.FindPath(start, goal, u.owner);
        if (route == null) return false;

        u.targetUnit = null;
        u.targetCity = null;
        u.foundAt = -1;

        u.irrigateAt = -1;
        u.irrigateProgress = 0f;
        u.mineAt = -1;
        u.mineProgress = 0f;
        u.farmAt = -1;
        u.farmProgress = 0f;
        u.path = route;
        u.roadGoal = goal;
        u.roadProgress = 0f;

        if (u.owner.isHuman) AddLog("Trabajador: construyendo carretera hacia " + RoadGoalName(goal), u.owner.color);
        return true;
    }

    // Nombre de la ciudad que hay en la casilla, o "la casilla elegida"
    public string RoadGoalName(int goal)
    {
        City c = grid.cityAt[goal];
        return c != null ? c.name : "la casilla elegida";
    }

    public void CancelRoad(Unit u)
    {
        u.roadGoal = -1;
        u.roadProgress = 0f;
        u.irrigateAt = -1;       // cualquier orden nueva cancela también la irrigación en curso
        u.irrigateProgress = 0f;
        u.mineAt = -1;           // ... y la mina en curso
        u.mineProgress = 0f;
        u.farmAt = -1;           // ... y la granja en curso
        u.farmProgress = 0f;
    }

    // ---------------------------------------------------------------- Granjas

    [Header("Granjas")]
    [Tooltip("Segundos que tarda un trabajador en construir una granja")]
    public float farmSeconds = 12f;

    /// <summary>
    /// Orden de granja: el trabajador va a la casilla y construye una granja (alimento x2).
    /// Requiere el Arado y collera de la Edad Media.
    /// </summary>
    public bool OrderFarm(Unit u, int cell)
    {
        if (u.kind != UnitKind.Worker || !grid.CanFarm(cell)) return false;

        if (!u.owner.Has(TechKind.HeavyPlow))
        {
            if (u.owner.isHuman) AddLog("Necesitas descubrir el Arado y collera para construir granjas (T)", Color.red);
            return false;
        }

        int from = u.Moving ? u.moveTo : u.cell;
        List<int> route = grid.FindPath(from, cell, u.owner);
        if (route == null) return false;

        CancelRoad(u);
        u.targetUnit = null;
        u.targetCity = null;
        u.foundAt = -1;

        u.path = route;
        u.farmAt = cell;
        u.farmProgress = 0f;

        if (u.owner.isHuman) AddLog("Trabajador: construyendo granja", u.owner.color);
        return true;
    }

    // Trabajo de la granja una vez en la casilla; devuelve true si el trabajador sigue ocupado
    bool TickFarming(Unit u, float dt)
    {
        if (u.farmAt < 0) return false;

        if (!grid.CanFarm(u.farmAt)) { CancelRoad(u); return false; }
        if (u.cell != u.farmAt)
        {
            // De camino: lo mueve el código normal; si se quedó sin ruta, la recalcula o abandona
            if (u.path.Count == 0)
            {
                List<int> route = grid.FindPath(u.cell, u.farmAt, u.owner);
                if (route == null || route.Count == 0) CancelRoad(u);
                else u.path = route;
            }
            return false;
        }

        u.farmProgress += dt / farmSeconds;
        if (u.farmProgress < 1f) return true;

        int cell = u.farmAt;
        grid.farmed[cell] = true;
        CivViews.BuildFarm(cell, grid, EarthT);

        if (grid.tileCity[cell] != null) RecomputeCity(grid.tileCity[cell]);
        if (u.owner.isHuman) AddLog("Granja construida: alimento x2", u.owner.color);

        u.farmAt = -1;
        u.farmProgress = 0f;
        return true;
    }

    // ---------------------------------------------------------------- Minas

    [Header("Minas")]
    [Tooltip("Segundos que tarda un trabajador en construir una mina")]
    public float mineSeconds = 10f;

    /// <summary>
    /// Orden de minar: el trabajador va a la casilla de monte o montaña y construye una mina
    /// (producción x2). Requiere la Minería.
    /// </summary>
    public bool OrderMine(Unit u, int cell)
    {
        if (u.kind != UnitKind.Worker || !grid.CanMine(cell)) return false;

        if (!u.owner.Has(TechKind.Mining))
        {
            if (u.owner.isHuman) AddLog("Necesitas descubrir la Minería para construir minas (T)", Color.red);
            return false;
        }

        int from = u.Moving ? u.moveTo : u.cell;
        List<int> route = grid.FindPath(from, cell, u.owner);
        if (route == null) return false;

        CancelRoad(u);
        u.targetUnit = null;
        u.targetCity = null;
        u.foundAt = -1;

        u.path = route;
        u.mineAt = cell;
        u.mineProgress = 0f;

        if (u.owner.isHuman) AddLog("Trabajador: construyendo mina", u.owner.color);
        return true;
    }

    // Trabajo de minería una vez en la casilla; devuelve true si el trabajador sigue ocupado
    bool TickMining(Unit u, float dt)
    {
        if (u.mineAt < 0) return false;

        if (!grid.CanMine(u.mineAt)) { CancelRoad(u); return false; }
        if (u.cell != u.mineAt)
        {
            // De camino: lo mueve el código normal; si se quedó sin ruta, la recalcula o abandona
            if (u.path.Count == 0)
            {
                List<int> route = grid.FindPath(u.cell, u.mineAt, u.owner);
                if (route == null || route.Count == 0) CancelRoad(u);
                else u.path = route;
            }
            return false;
        }

        u.mineProgress += dt / mineSeconds;
        if (u.mineProgress < 1f) return true;

        int cell = u.mineAt;
        grid.mined[cell] = true;
        CivViews.BuildMine(cell, grid, EarthT);

        if (grid.tileCity[cell] != null) RecomputeCity(grid.tileCity[cell]);
        if (u.owner.isHuman) AddLog("Mina construida: producción x2", u.owner.color);

        u.mineAt = -1;
        u.mineProgress = 0f;
        return true;
    }

    // ---------------------------------------------------------------- Irrigación

    [Header("Irrigación")]
    [Tooltip("Segundos que tarda un trabajador en irrigar una casilla")]
    public float irrigateSeconds = 8f;

    /// <summary>
    /// Orden de irrigar: el trabajador va a la casilla y la irriga (comida x1,5).
    /// Vale cualquier terreno de tierra salvo la montaña.
    /// </summary>
    public bool OrderIrrigate(Unit u, int cell)
    {
        if (u.kind != UnitKind.Worker || !grid.CanIrrigate(cell)) return false;

        if (!u.owner.Has(TechKind.Agriculture))
        {
            if (u.owner.isHuman) AddLog("Necesitas descubrir la Agricultura para irrigar (T)", Color.red);
            return false;
        }

        int from = u.Moving ? u.moveTo : u.cell;
        List<int> route = grid.FindPath(from, cell, u.owner);
        if (route == null) return false;

        CancelRoad(u);
        u.targetUnit = null;
        u.targetCity = null;
        u.foundAt = -1;

        u.path = route;
        u.irrigateAt = cell;
        u.irrigateProgress = 0f;

        if (u.owner.isHuman) AddLog("Trabajador: irrigando casilla", u.owner.color);
        return true;
    }

    // Trabajo de irrigación una vez en la casilla; devuelve true si el trabajador sigue ocupado
    bool TickIrrigation(Unit u, float dt)
    {
        if (u.irrigateAt < 0) return false;

        if (!grid.CanIrrigate(u.irrigateAt)) { CancelRoad(u); return false; }
        if (u.cell != u.irrigateAt)
        {
            // De camino: lo mueve el código normal; si se quedó sin ruta, la recalcula o abandona
            if (u.path.Count == 0)
            {
                List<int> route = grid.FindPath(u.cell, u.irrigateAt, u.owner);
                if (route == null || route.Count == 0) CancelRoad(u);
                else u.path = route;
            }
            return false;
        }

        u.irrigateProgress += dt / irrigateSeconds;
        if (u.irrigateProgress < 1f) return true;

        int cell = u.irrigateAt;
        grid.irrigated[cell] = true;
        CivViews.BuildIrrigation(cell, grid, EarthT);

        if (grid.tileCity[cell] != null) RecomputeCity(grid.tileCity[cell]);
        if (u.owner.isHuman) AddLog("Casilla irrigada: comida x1,5", u.owner.color);

        u.irrigateAt = -1;
        u.irrigateProgress = 0f;
        return true;
    }

    void TickRoadBuilder(Unit u, float dt)
    {
        // Si en el destino se levantó una ciudad ajena, se abandona la obra
        City goalCity = grid.cityAt[u.roadGoal];
        if (goalCity != null && goalCity.owner != u.owner)
        {
            CancelRoad(u);
            u.path.Clear();
            return;
        }

        if (u.cell == u.roadGoal)
        {
            if (u.owner.isHuman) AddLog("Carretera terminada hasta " + RoadGoalName(u.roadGoal), u.owner.color);
            CancelRoad(u);
            u.path.Clear();
            return;
        }

        if (u.path.Count == 0)
        {
            List<int> route = grid.FindPath(u.cell, u.roadGoal, u.owner);
            if (route == null || route.Count == 0) { CancelRoad(u); return; }
            u.path = route;
        }

        int next = u.path[0];

        // Sin la Construcción de puentes los ríos no se pavimentan: el trabajador los cruza sin construir en los tramos
        // que tocan una casilla de río
        bool riverBlocks = !u.owner.Has(TechKind.Bridges) && (grid.river[u.cell] || grid.river[next]);
        if (!grid.HasRoad(u.cell, next) && !riverBlocks)
        {
            u.roadProgress += dt / roadBuildSeconds;
            if (u.roadProgress < 1f) return;

            grid.AddRoad(u.cell, next);
            CivViews.BuildRoad(u.cell, next, grid, earth, EarthT, u.owner);
            u.roadProgress = 0f;
        }

        BeginMove(u);
    }

    // Una civilización de la IA sin sitio donde fundar une con carreteras sus ciudades aún no conectadas
    void AiRoad(Unit u)
    {
        Civ civ = u.owner;
        if (civ.cities.Count < 2) return;

        City bestA = null, bestB = null;
        float bestAngle = float.MaxValue;
        City here = grid.cityAt[u.cell];

        for (int i = 0; i < civ.cities.Count; i++)
        {
            for (int j = i + 1; j < civ.cities.Count; j++)
            {
                City a = civ.cities[i], b = civ.cities[j];
                if (grid.component[a.cell] != grid.component[b.cell]) continue;

                float angle = grid.AngleBetween(a.cell, b.cell);
                if (angle >= bestAngle) continue;
                if (RoadComplete(a.cell, b.cell, civ)) continue;

                bestAngle = angle;
                bestA = a;
                bestB = b;
            }
        }

        if (bestA == null) return;

        // Si ya está en una de las dos, construye hacia la otra; si no, va primero a una de ellas
        if (here == bestA) OrderRoad(u, bestB);
        else if (here == bestB) OrderRoad(u, bestA);
        else OrderMove(u, bestA.cell);
    }

    bool RoadComplete(int from, int to, Civ civ)
    {
        List<int> path = grid.FindPath(from, to, civ);
        if (path == null) return true; // inalcanzable: no hay nada que construir

        int prev = from;
        foreach (int c in path)
        {
            if (!grid.HasRoad(prev, c)) return false;
            prev = c;
        }

        return true;
    }

    public void OrderMove(Unit u, int cell)
    {
        CancelRoad(u);
        u.targetUnit = null;
        u.targetCity = null;
        u.foundAt = -1;
        u.joinAt = -1;
        u.boardTarget = null;
        u.disembarkAt = -1;

        int from = u.Moving ? u.moveTo : u.cell;
        List<int> path = grid.FindPath(from, cell, u.owner, u.def.naval);

        if (path == null) return;
        u.path = path;
    }

    // ---------------------------------------------------------------- Comercio

    public const float TradeBonus = 0.15f;          // ruta creada por una caravana
    public const float MerchantTradeBonus = 0.20f;  // ruta creada por un mercader

    /// <summary>Rutas comerciales activas: con ciudades vivas que sigan siendo de la misma civilización.</summary>
    public int TradeRoutes(City city)
    {
        int n = 0;
        foreach (City p in city.tradePartners)
            if (p.alive && p.owner == city.owner) n++;
        return n;
    }

    /// <summary>Bonus total de oro y ciencia de la ciudad por sus rutas activas (0,15 por las de caravana, 0,20 por las de mercader).</summary>
    public float TradeBonusOf(City city)
    {
        float bonus = 0f;
        foreach (City p in city.tradePartners)
        {
            if (!p.alive || p.owner != city.owner) continue;
            bonus += city.merchantPartners.Contains(p) ? MerchantTradeBonus : TradeBonus;
        }
        return bonus;
    }

    /// <summary>
    /// Motivo por el que la caravana no puede establecer comercio ahora mismo, o null si puede.
    /// Debe estar parada en una ciudad propia distinta de la de origen y sin ruta ya con ella.
    /// </summary>
    public string TradeBlocker(Unit u)
    {
        if (!u.IsTrader || u.carriedBy != null) return "Solo una caravana o un mercader puede comerciar.";

        City home = u.homeCity;
        if (home == null || !home.alive || home.owner != u.owner) return "La ciudad de origen ya no es tuya.";

        City here = u.Moving ? null : grid.cityAt[u.cell];
        if (here == null || here.owner != u.owner) return "Llévala a otra ciudad propia.";
        if (here == home) return "Llévala a una ciudad distinta de " + home.name + ".";
        if (home.tradePartners.Contains(here))
        {
            // Una ruta de mercader sustituye a la de caravana entre las mismas ciudades (+20 % en vez de +15 %)
            bool replaces = u.kind == UnitKind.Merchant && !home.merchantPartners.Contains(here);
            if (!replaces) return home.name + " ya comercia con " + here.name + (u.kind == UnitKind.Merchant ? " por mercader." : ".");
        }

        return null;
    }

    /// <summary>Orden de la caravana: establece la ruta comercial entre su ciudad de origen y la ciudad donde está.</summary>
    public bool TryTrade(Unit u)
    {
        string blocker = TradeBlocker(u);
        if (blocker != null)
        {
            if (u.owner.isHuman) AddLog(blocker, Color.red);
            return false;
        }

        EstablishTrade(u, u.homeCity, grid.cityAt[u.cell]);
        return true;
    }

    void EstablishTrade(Unit u, City a, City b)
    {
        a.tradePartners.Add(b);
        b.tradePartners.Add(a);

        bool merchant = u.kind == UnitKind.Merchant;
        if (merchant)
        {
            a.merchantPartners.Add(b);
            b.merchantPartners.Add(a);
        }

        RecomputeCity(a);
        RecomputeCity(b);

        if (u.owner.isHuman)
            AddLog("Comercio entre " + a.name + " y " + b.name + ": +" + Mathf.RoundToInt((merchant ? MerchantTradeBonus : TradeBonus) * 100f) + "% de oro y ciencia en ambas", u.owner.color);

        KillSilently(u);
    }

    // ---------------------------------------------------------------- Transporte marítimo

    /// <summary>Una unidad de tierra va hasta la costa junto al barco y embarca en él.</summary>
    public bool OrderBoard(Unit u, Unit ship)
    {
        if (u.def.naval || u.carriedBy != null || ship == null || !ship.alive) return false;
        if (ship.owner != u.owner || ship.def.capacity <= 0 || ship.cargo != null) return false;

        CancelRoad(u);
        u.targetUnit = null;
        u.targetCity = null;
        u.foundAt = -1;
        u.joinAt = -1;
        u.path.Clear();
        u.lastTargetCell = -1;
        u.boardTarget = ship;
        return true;
    }

    // Devuelve true si la unidad está ocupada yendo a embarcar (o acaba de embarcar)
    bool TickBoarding(Unit u)
    {
        Unit ship = u.boardTarget;
        if (ship == null) return false;

        if (!ship.alive || ship.cargo != null)
        {
            u.boardTarget = null;
            return false;
        }

        if (grid.Adjacent(u.cell, ship.cell))
        {
            Board(u, ship);
            return true;
        }

        if (u.path.Count == 0 || u.lastTargetCell != ship.cell || u.repathTimer <= 0f)
        {
            u.lastTargetCell = ship.cell;
            u.repathTimer = 2f;

            List<int> best = null;
            foreach (int nb in grid.nbr[ship.cell])
            {
                if (!grid.land[nb]) continue;
                List<int> p = grid.FindPath(u.cell, nb, u.owner);
                if (p != null && (best == null || p.Count < best.Count)) best = p;
            }

            if (best == null)
            {
                u.boardTarget = null;
                if (u.owner.isHuman) AddLog("No se puede llegar al barco por tierra", Color.red);
                return false;
            }

            u.path = best;
        }

        if (u.path.Count > 0) BeginMove(u);
        return true;
    }

    void Board(Unit u, Unit ship)
    {
        ship.cargo = u;
        u.carriedBy = ship;
        u.boardTarget = null;
        u.path.Clear();
        u.cell = ship.cell;
        u.moveTo = -1;
        u.t = 0f;
        if (u.view != null) u.view.SetActive(false);

        if (u.owner.isHuman) AddLog(u.def.name + " embarca en el " + ship.def.name, u.owner.color);
    }

    /// <summary>El barco lleva su carga hasta el mar junto a la casilla de tierra y la desembarca allí.</summary>
    public bool OrderDisembark(Unit ship, int landCell)
    {
        if (ship.cargo == null || !grid.land[landCell]) return false;

        City city = grid.cityAt[landCell];
        if (city != null && city.owner != ship.owner) return false;

        int from = ship.Moving ? ship.moveTo : ship.cell;

        List<int> best = null;
        if (grid.Adjacent(from, landCell)) best = new List<int>();
        else
        {
            foreach (int nb in grid.nbr[landCell])
            {
                if (grid.land[nb]) continue;
                List<int> p = grid.FindPath(from, nb, ship.owner, true);
                if (p != null && (best == null || p.Count < best.Count)) best = p;
            }
        }

        if (best == null)
        {
            if (ship.owner.isHuman) AddLog("El barco no puede llegar a esa costa", Color.red);
            return false;
        }

        ship.targetUnit = null;
        ship.targetCity = null;
        ship.path = best;
        ship.disembarkAt = landCell;
        return true;
    }

    void Unload(Unit ship)
    {
        Unit p = ship.cargo;
        int cell = ship.disembarkAt;
        ship.disembarkAt = -1;
        if (p == null) return;

        ship.cargo = null;
        p.carriedBy = null;
        p.cell = cell;
        p.moveTo = -1;
        p.t = 0f;
        p.path.Clear();
        if (p.view != null) p.view.SetActive(true);
        PlaceUnit(p);

        if (ship.owner.isHuman) AddLog(p.def.name + " desembarca", ship.owner.color);
    }

    public void OrderAttack(Unit u, Unit target)
    {
        if (!u.def.IsMilitary || target == null) return;
        CancelRoad(u);
        u.targetCity = null;
        u.targetUnit = target;
        u.foundAt = -1;
        u.path.Clear();
        u.lastTargetCell = -1;
    }

    public void OrderAttack(Unit u, City target)
    {
        if (!u.def.IsMilitary || target == null) return;
        CancelRoad(u);
        u.targetUnit = null;
        u.targetCity = target;
        u.foundAt = -1;
        u.path.Clear();
        u.lastTargetCell = -1;
    }

    public bool TryFound(Unit settler)
    {
        if (settler.kind != UnitKind.Settler || settler.Moving) return false;

        City city = FoundCity(settler.owner, settler.cell);
        if (city == null) return false;

        KillSilently(settler);
        return true;
    }

    /// <summary>El colono se une a la ciudad propia en cuya casilla está: +1 de población y desaparece.</summary>
    public bool TryJoin(Unit settler)
    {
        if (settler.kind != UnitKind.Settler || settler.Moving) return false;

        City city = grid.cityAt[settler.cell];
        if (city == null || city.owner != settler.owner) return false;

        city.pop++;
        RecomputeCity(city);
        RebuildCityView(city);
        if (settler.owner.isHuman) AddLog("Un colono se une a " + city.name + " (población " + city.pop + ")", city.owner.color);

        KillSilently(settler);
        return true;
    }

    /// <summary>Orden de ir a una ciudad propia y unirse a ella al llegar.</summary>
    public bool OrderJoin(Unit u, City city)
    {
        if (u.kind != UnitKind.Settler || city == null || !city.alive || city.owner != u.owner) return false;

        OrderMove(u, city.cell);
        u.joinAt = city.cell;

        if (u.cell == city.cell && !u.Moving) return TryJoin(u);
        return true;
    }

    void KillSilently(Unit u)
    {
        u.alive = false;
        u.owner.units.Remove(u);
        allUnits.Remove(u);
        if (u.view != null) Destroy(u.view);
    }

    void TickUnit(Unit u, float dt)
    {
        u.attackCooldown -= dt;
        u.scanTimer -= dt;
        u.repathTimer -= dt;

        if (u.carriedBy != null) return; // embarcada: la lleva el barco

        if (u.Moving)
        {
            AdvanceMove(u, dt);
            return;
        }

        // Barco: desembarcar la carga al llegar junto a la costa elegida
        if (u.disembarkAt >= 0)
        {
            if (u.cargo == null) u.disembarkAt = -1;
            else if (grid.Adjacent(u.cell, u.disembarkAt))
            {
                Unload(u);
                return;
            }
        }

        // Ir a embarcar en un barco
        if (TickBoarding(u)) return;

        // Fundar una ciudad al llegar
        if (u.foundAt >= 0 && u.cell == u.foundAt && u.kind == UnitKind.Settler)
        {
            u.foundAt = -1;
            TryFound(u);
            return;
        }

        // Unirse a la ciudad al llegar
        if (u.joinAt >= 0 && u.cell == u.joinAt)
        {
            u.joinAt = -1;
            TryJoin(u);
            return;
        }

        // Irrigar la casilla de destino al llegar
        if (TickIrrigation(u, dt)) return;

        // Construir la mina de la casilla de destino al llegar
        if (TickMining(u, dt)) return;

        // Construir la granja de la casilla de destino al llegar
        if (TickFarming(u, dt)) return;

        // Construir una carretera entre dos ciudades
        if (u.roadGoal >= 0)
        {
            TickRoadBuilder(u, dt);
            return;
        }

        // Validar objetivos
        if (u.targetUnit != null && (!u.targetUnit.alive || u.targetUnit.owner == u.owner || u.targetUnit.carriedBy != null)) u.targetUnit = null;
        if (u.targetCity != null && (!u.targetCity.alive || u.targetCity.owner == u.owner)) u.targetCity = null;

        if (u.def.IsMilitary && u.HasTarget)
        {
            int targetCell = u.targetCity != null ? u.targetCity.cell : u.targetUnit.cell;

            // Cuerpo a cuerpo: casilla contigua; a distancia (arquero): dentro de su alcance
            bool inRange = u.def.range <= 1
                ? grid.Adjacent(u.cell, targetCell)
                : grid.AngleBetween(u.cell, targetCell) <= grid.angularSpacing * (u.def.range + 0.1f);

            if (inRange)
            {
                u.path.Clear();
                if (u.attackCooldown <= 0f) Attack(u);
                return;
            }

            if (u.path.Count == 0 || u.lastTargetCell != targetCell || u.repathTimer <= 0f)
            {
                List<int> path = grid.FindPath(u.cell, targetCell, u.owner);
                u.lastTargetCell = targetCell;
                u.repathTimer = 2f;

                if (path == null)
                {
                    u.targetUnit = null;
                    u.targetCity = null;
                    return;
                }

                u.path = path;
            }
        }

        if (u.path.Count > 0)
        {
            BeginMove(u);
            return;
        }

        // Inactivo: los militares atacan a lo que tengan cerca y todos se curan
        if (u.def.IsMilitary && u.scanTimer <= 0f)
        {
            u.scanTimer = 0.6f;
            AutoEngage(u);
        }

        u.hp = Mathf.Min(u.def.hp, u.hp + 0.3f * dt);
    }

    void BeginMove(Unit u)
    {
        int next = u.path[0];
        u.path.RemoveAt(0);

        City c = grid.cityAt[next];
        if (c != null && c.owner != u.owner)
        {
            u.path.Clear();
            return;
        }

        u.moveTo = next;
        u.t = 0f;
    }

    void AdvanceMove(Unit u, float dt)
    {
        // Las carreteras y recorrer un río duplican la velocidad en ese tramo, sea cual sea el terreno
        float factor = u.def.naval ? 1f
            : (grid.HasRoad(u.cell, u.moveTo) || grid.HasRiver(u.cell, u.moveTo))
            ? WorldGrid.RoadSpeedFactor
            : Mathf.Max(0.2f, GameDefs.MoveFactor(grid.biome[u.moveTo]));
        u.t += u.def.speed * factor * dt;

        if (u.t >= 1f)
        {
            u.cell = u.moveTo;
            u.moveTo = -1;
            u.t = 0f;
            if (u.cargo != null) u.cargo.cell = u.cell;
        }

        PlaceUnit(u);
    }

    void AutoEngage(Unit u)
    {
        float range = grid.angularSpacing * Mathf.Max(2.2f, u.def.range + 0.1f);
        float best = float.MaxValue;

        foreach (Unit e in allUnits)
        {
            if (!e.alive || e.owner == u.owner || e.carriedBy != null) continue;

            float a = grid.AngleBetween(u.cell, e.cell);
            if (a > range || a >= best) continue;

            best = a;
            u.targetUnit = e;
            u.targetCity = null;
        }

        if (u.targetUnit != null) return;

        foreach (City c in allCities)
        {
            if (!c.alive || c.owner == u.owner) continue;

            float a = grid.AngleBetween(u.cell, c.cell);
            if (a > range || a >= best) continue;

            best = a;
            u.targetCity = c;
        }
    }

    void Attack(Unit u)
    {
        u.attackCooldown = 1f;

        float mult = 0.5f + 0.5f * (u.hp / u.def.hp);
        float dmg = u.def.attack * Random.Range(0.85f, 1.15f) * mult;

        if (u.targetCity != null)
        {
            City c = u.targetCity;
            float defense = 5f + (c.buildings.Contains(BuildingKind.Walls) ? 6f : 0f);
            c.hp -= dmg * 6f / (6f + defense);

            if (c.hp <= 0f)
            {
                CaptureCity(c, u.owner);
                u.targetCity = null;
            }
        }
        else if (u.targetUnit != null)
        {
            Unit t = u.targetUnit;
            t.hp -= dmg * 6f / (6f + t.def.defense);

            // El defensor contraataca
            if (t.def.IsMilitary && !t.HasTarget) t.targetUnit = u;

            if (t.hp <= 0f)
            {
                KillUnit(t);
                u.targetUnit = null;
            }
        }
    }

    void CaptureCity(City c, Civ newOwner)
    {
        Civ old = c.owner;
        old.cities.Remove(c);
        old.territoryDirty = true;

        c.owner = newOwner;
        newOwner.cities.Add(c);
        newOwner.territoryDirty = true;

        c.hp = c.maxHp * 0.5f;
        c.pop = Mathf.Max(1, c.pop - 1);
        c.hasItem = false;
        c.prodStock = 0f;

        RecomputeCity(c);
        UpdateCivRates(old);
        RebuildCityView(c);

        AddLog(newOwner.name + " conquista " + c.name + " (de " + old.name + ")", newOwner.color);
        CheckElimination(old);
    }

    // ---------------------------------------------------------------- Fin de partida

    void CheckElimination(Civ civ)
    {
        if (!civ.alive || civ.cities.Count > 0) return;

        foreach (Unit u in civ.units)
            if (u.kind == UnitKind.Settler) return;

        civ.alive = false;
        foreach (Unit u in civ.units.ToArray()) KillSilently(u);

        AddLog(civ.name + " ha sido eliminada", Color.red);

        int alive = 0;
        Civ last = null;
        foreach (Civ c in civs) if (c.alive) { alive++; last = c; }

        if (alive == 1 && winner == null)
        {
            winner = last;
            AddLog(last.name + " gobierna el mundo", last.color);
        }
    }

    // ---------------------------------------------------------------- Inteligencia artificial

    void AiThink(Civ civ)
    {
        // Investigación: elige una tecnología pendiente al azar
        if (!civ.researching)
        {
            List<TechKind> options = new List<TechKind>();
            foreach (TechKind t in GameDefs.Techs.Keys) if (civ.CanResearch(t)) options.Add(t);
            if (options.Count > 0) StartResearch(civ, options[Random.Range(0, options.Count)]);
        }

        // Colonos inactivos buscan dónde fundar
        foreach (Unit u in civ.units.ToArray())
        {
            if (u.kind != UnitKind.Settler || !u.alive || u.Moving || u.path.Count > 0 || u.foundAt >= 0 || u.roadGoal >= 0) continue;
            AiSendSettler(u);
        }

        // Producción
        foreach (City c in civ.cities.ToArray())
        {
            if (!c.hasItem) AiChooseProduction(c);
        }

        // Comprar si sobra el oro
        if (civ.gold > 120f)
        {
            foreach (City c in civ.cities)
            {
                if (!c.hasItem || BuyPrice(c) > civ.gold * 0.6f) continue;
                if (Buy(c)) break;
            }
        }

        if (gameTime > aiGraceSeconds) AiMilitary(civ);
    }

    void AiSendSettler(Unit u)
    {
        if (CanFound(u.owner, u.cell) && u.owner.cities.Count == 0)
        {
            u.foundAt = u.cell;
            return;
        }

        // Búsqueda por anchura sobre tierra hasta 10 saltos
        Dictionary<int, int> dist = new Dictionary<int, int> { { u.cell, 0 } };
        Queue<int> queue = new Queue<int>();
        queue.Enqueue(u.cell);

        int bestCell = -1;
        float bestScore = float.MinValue;

        while (queue.Count > 0)
        {
            int c = queue.Dequeue();
            int d = dist[c];

            if (CanFound(u.owner, c))
            {
                float score = 0f;
                foreach (int t in grid.Around(c, 2))
                {
                    Vector3 y = grid.TileYield(t);
                    float v = y.x * 1.2f + y.y + y.z * 0.5f;
                    if (grid.tileCity[t] != null) v *= 0.2f;
                    score += v;
                }

                score -= d * 0.7f;
                if (score > bestScore) { bestScore = score; bestCell = c; }
            }

            if (d >= 10) continue;

            foreach (int nb in grid.nbr[c])
            {
                if (dist.ContainsKey(nb) || !grid.land[nb]) continue;
                dist[nb] = d + 1;
                queue.Enqueue(nb);
            }
        }

        // Sin sitio donde fundar: mejor construir carreteras entre sus ciudades
        if (bestCell < 0)
        {
            AiRoad(u);
            return;
        }

        if (bestCell == u.cell)
        {
            u.foundAt = u.cell;
            return;
        }

        List<int> path = grid.FindPath(u.cell, bestCell, u.owner);
        if (path == null) return;

        u.path = path;
        u.foundAt = bestCell;
    }

    void AiChooseProduction(City c)
    {
        Civ civ = c.owner;

        int settlers = 0;
        foreach (Unit u in civ.units) if (u.kind == UnitKind.Settler) settlers++;
        foreach (City o in civ.cities) if (o.hasItem && o.itemIsUnit && o.itemUnit == UnitKind.Settler) settlers++;

        int military = 0;
        foreach (Unit u in civ.units) if (u.def.IsMilitary) military++;

        // La más actual que se pueda construir (las antiguas quedan obsoletas)
        UnitKind bestMilitary = Available(civ, UnitKind.Legion) ? UnitKind.Legion
                              : Available(civ, UnitKind.Spearman) ? UnitKind.Spearman : UnitKind.Warrior;

        if (civ.cities.Count + settlers < 5 && settlers == 0 && c.pop >= 2)
        {
            SetProduction(c, UnitKind.Settler);
            return;
        }

        if (military < 2 * civ.cities.Count + 2)
        {
            SetProduction(c, bestMilitary);
            return;
        }

        BuildingKind[] priority = { BuildingKind.Granary, BuildingKind.Workshop, BuildingKind.Library, BuildingKind.Market, BuildingKind.Walls };
        foreach (BuildingKind b in priority)
        {
            if (!Available(c, b)) continue;
            SetProduction(c, b);
            return;
        }

        SetProduction(c, bestMilitary);
    }

    void AiMilitary(Civ civ)
    {
        List<Unit> idle = new List<Unit>();
        foreach (Unit u in civ.units)
        {
            if (u.def.IsMilitary && !u.Moving && u.path.Count == 0 && !u.HasTarget) idle.Add(u);
        }

        // Se dejan algunos de guarnición
        if (idle.Count < civ.cities.Count + 3) return;

        // Ciudad enemiga más cercana que se pueda alcanzar por tierra
        Unit scout = idle[0];
        City target = null;
        float bestAngle = float.MaxValue;

        foreach (City c in allCities)
        {
            if (!c.alive || c.owner == civ) continue;

            float a = grid.AngleBetween(scout.cell, c.cell);
            if (a >= bestAngle) continue;
            if (grid.component[scout.cell] != grid.component[c.cell]) continue;

            bestAngle = a;
            target = c;
        }

        if (target == null) return;

        int send = idle.Count - civ.cities.Count;
        for (int i = 0; i < send; i++) OrderAttack(idle[i], target);

        if (target.owner.isHuman) AddLog(civ.name + " marcha contra " + target.name + "!", civ.color);
    }
}
