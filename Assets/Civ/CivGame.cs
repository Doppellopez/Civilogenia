using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Lógica del juego de civilizaciones en tiempo real sobre la Tierra:
/// civilizaciones, ciudades, economía, unidades, combate e IA.
/// Se crea sola al pulsar Play (si hay una EarthTopography en la escena).
/// </summary>
public partial class CivGame : MonoBehaviour
{
    [Header("Partida")]
    [Tooltip("Civilizaciones en la partida (1 = solo la tuya)")]
    [Range(1, 10)] public int civCount = 1;
    [Tooltip("Número de casillas del planeta (más = mapa más detallado, arranque más lento)")]
    public int gridCells = 6000;
    public float gameSpeed = 1f;

    /// <summary>Todo (producción, investigación, crecimiento, movimiento, combate...) tarda 3 veces más que antes.</summary>
    public const float SimulationScale = 1f / 3f;

    [Header("Carreteras")]
    [Tooltip("Segundos que tarda un colono en construir un tramo de carretera")]
    public float roadBuildSeconds = 5f;
    public float railBuildSeconds = 8f;

    [Header("Inteligencia artificial")]
    public bool aiEnabled = true;
    [Tooltip("Segundos antes de que la IA empiece a atacar")]
    public float aiGraceSeconds = 90f;

    [Tooltip("Dificultad de las civilizaciones de la IA (se elige al empezar la partida)")]
    public Difficulty difficulty = Difficulty.Normal;

    // Efectos de la dificultad en la IA (el jugador no cambia):
    //   Fácil:   -30 % de producción, ciencia, oro y crecimiento, piensa más despacio, tarda más en atacar y funda hasta 5 ciudades
    //   Normal:  como hasta ahora
    //   Difícil: +30 % de producción, ciencia, oro y crecimiento, piensa más deprisa, ataca antes y funda hasta 10 ciudades
    float AiRateMultiplier { get { return difficulty == Difficulty.Easy ? 0.7f : difficulty == Difficulty.Hard ? 1.3f : 1f; } }
    float AiThinkInterval { get { return difficulty == Difficulty.Easy ? 3f : difficulty == Difficulty.Hard ? 1f : 1.5f; } }
    float AiGraceTime { get { return difficulty == Difficulty.Easy ? aiGraceSeconds * 2.7f : difficulty == Difficulty.Hard ? aiGraceSeconds * 0.35f : aiGraceSeconds; } }
    int AiMaxCities { get { return difficulty == Difficulty.Easy ? 5 : difficulty == Difficulty.Hard ? 10 : 8; } }

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
    public bool winnerByTech;   // la victoria fue por tecnología (Omnisciencia) y no por eliminar a las demás
    // No serializado: si Unity recompila los scripts durante el Play, el estado de la partida se pierde
    // (civilizaciones, mapa...) y "ready" debe volver a false en vez de quedarse en true con datos vacíos
    [System.NonSerialized] public bool ready;
    [System.NonSerialized] public bool worldBuilt;      // el planeta y el mapa ya están generados (aún puede faltar crear las civilizaciones)
    [System.NonSerialized] public bool matchRequested;  // el menú ya eligió cuántos rivales habrá
    [System.NonSerialized] public bool startCancelled;  // se cargó una partida guardada: no se crea la partida nueva
    [System.NonSerialized] public bool startFailed;     // no hubo tierra suficiente para empezar
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
        CaptureBaseChildren();   // lo que ya cuelga de la Tierra no es de la partida (no se destruye al cargar)

        grid = new WorldGrid();
        grid.Build(earth, gridCells);
        CivViews.BuildRivers(grid, earth, EarthT);
        CivViews.BuildSpecials(grid, EarthT);
        Debug.Log("[Civilogenia] Mapa: " + grid.riverPaths.Count + " ríos.");

        worldBuilt = true;

        // El menú principal pregunta cuántas civilizaciones enemigas (manejadas por la IA) habrá; sin menú se usa civCount.
        // Si se carga una partida guardada, esta preparación se cancela.
        while (MainMenu.Enabled && !matchRequested && !startCancelled) yield return null;
        if (startCancelled) yield break;

        // Tamaño del mapa elegido en el menú: si no es el que ya está construido, se rehace la cuadrícula del planeta
        if (requestedCells > 0 && requestedCells != grid.count)
        {
            ResetWorld();
            yield return null;

            gridCells = requestedCells;
            grid = new WorldGrid();
            grid.Build(earth, gridCells);
            CivViews.BuildRivers(grid, earth, EarthT);
            CivViews.BuildSpecials(grid, EarthT);
            yield return null;
            if (startCancelled) yield break;
        }

        if (!SetupCivs())
        {
            Debug.LogError("[Civilogenia] No hay tierra suficiente para empezar la partida.");
            startFailed = true;
            yield break;
        }

        BuildInitialFog();   // la niebla ya está puesta cuando la partida empieza a verse
        ready = true;
    }

    // Construye la niebla de guerra de golpe, antes de marcar la partida como lista: si no, el planeta se vería entero
    // (también lo no explorado) hasta el primer fotograma de juego
    void BuildInitialFog()
    {
        fogMeshStale = true;
        fogTimer = 0f;
        TickFog();
    }

    /// <summary>El jugador elige cuántas civilizaciones enemigas habrá (0-5); todas las demás las maneja la IA. Empieza la partida.</summary>
    public void StartMatch(int enemies, Difficulty level = Difficulty.Normal, int playerCiv = 0, int mapSize = 0)
    {
        difficulty = level;
        requestedCells = GameDefs.MapSizes[Mathf.Clamp(mapSize, 0, GameDefs.MapSizes.Length - 1)].cells;
        playerChoice = Mathf.Clamp(playerCiv, 0, GameDefs.PlayableCivs.Length - 1);
        civCount = Mathf.Clamp(1 + enemies, 1, GameDefs.PlayableCivs.Length);
        aiEnabled = true;
        startFailed = false;
        matchRequested = true;
    }

    // ---------------------------------------------------------------- Preparación

    int playerChoice;
    int requestedCells;   // casillas del mapa elegidas en el menú (0 = las del inspector)

    /// <summary>Aplica a una civilización la elección del jugador: nombre, color, nombres de ciudades y bonus.</summary>
    void ApplyCivChoice(Civ civ, int choice)
    {
        GameDefs.CivChoice c = GameDefs.PlayableCivs[Mathf.Clamp(choice, 0, GameDefs.PlayableCivs.Length - 1)];
        civ.choice = choice;
        civ.name = c.name;
        civ.color = c.color;
        civ.cityNames = c.cityNames;
        civ.scienceBonus = c.scienceBonus;
        civ.growthBonus = c.growthBonus;
        civ.productionBonus = c.productionBonus;
        civ.goldBonus = c.goldBonus;
    }

    // Las civilizaciones enemigas se reparten al azar entre las que no ha elegido el jugador (sin repetir)
    List<int> enemyPool;

    void ShuffleEnemyPool()
    {
        enemyPool = new List<int>();
        for (int i = 0; i < GameDefs.PlayableCivs.Length; i++)
            if (i != playerChoice) enemyPool.Add(i);

        for (int i = enemyPool.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            int tmp = enemyPool[i]; enemyPool[i] = enemyPool[j]; enemyPool[j] = tmp;
        }
    }

    int EnemyChoice(int index)
    {
        return enemyPool[(index - 1) % enemyPool.Count];
    }

    bool SetupCivs()
    {
        ShuffleEnemyPool();
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

            ApplyCivChoice(civ, civ.isHuman ? playerChoice : EnemyChoice(i));
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
        if (IntroVideo.Active || MainMenu.Active) return;   // la partida espera mientras suena la introducción y está el menú

        TickFog();   // niebla de guerra: solo se ve en torno a las unidades y ciudades propias

        float dt = Time.deltaTime * gameSpeed * SimulationScale;
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
                    civ.aiTimer = AiThinkInterval;
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

    // Nombre de la siguiente ciudad: los de su civilización si los tiene, o los genéricos (repartidos por id)
    string CityNameFor(Civ civ)
    {
        string[] names = civ.cityNames ?? GameDefs.CityNames;
        int offset = civ.cityNames != null ? 0 : civ.id * 5;
        string name = names[(offset + civ.nameCounter++) % names.Length];
        return civ.nameCounter > names.Length ? name + " " + civ.nameCounter : name;
    }

    public City FoundCity(Civ civ, int cell)
    {
        if (!CanFound(civ, cell)) return null;

        City city = new City
        {
            owner = civ,
            name = CityNameFor(civ),
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
        GrantNewspapers(civ);   // con la Prensa de Gutenberg, cada ciudad nueva nace con su Periódico

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

        // Cada casilla de la ciudad con ferrocarril da además +1 de producción
        int railTiles = 0;
        foreach (int c in city.tiles) if (grid.tileCity[c] == city && grid.HasRailAt(c)) railTiles++;

        city.foodRate = total.x;
        city.prodRate = total.y + railTiles + (city.buildings.Contains(BuildingKind.Workshop) ? 2f : 0f)
                        + (city.buildings.Contains(BuildingKind.FullingMill) ? 3f : 0f);
        if (OwnsBuilding(city.owner, BuildingKind.HooverDam)) city.prodRate *= 1.25f; // Presa Hoover: +25 % de producción en todas las ciudades
        if (city.buildings.Contains(BuildingKind.HydroDam)) city.prodRate += 6f; // Presa hidroeléctrica: +6 de producción
        if (city.buildings.Contains(BuildingKind.NuclearPlant)) city.prodRate += 10f; // Central nuclear: +10 de producción
        if (city.buildings.Contains(BuildingKind.FusionReactor)) city.prodRate += 20f; // Reactor de fusión: +20 de producción
        if (city.buildings.Contains(BuildingKind.Factory)) city.prodRate *= 1.5f; // Fábrica: +50 % de producción en esta ciudad
        if (city.owner.Has(TechKind.SteamEngine)) city.prodRate *= 1.5f; // Máquina de vapor: +50 % de producción en todas las ciudades
        if (city.owner.Has(TechKind.Clock)) city.prodRate *= 1.25f; // Reloj mecánico: +25 % de producción en todas las ciudades
        if (city.owner.Has(TechKind.CombustionEngine)) city.prodRate *= 1.5f; // Motor de combustión: +50 % de producción en todas las ciudades
        if (city.owner.Has(TechKind.ArtificialIntelligence)) city.prodRate *= 1.3f; // Inteligencia artificial: +30 % de producción en todas las ciudades
        if (city.owner.Has(TechKind.Robotics)) city.prodRate *= 1.25f; // Robótica: +25 % de producción en todas las ciudades
        if (city.owner.Has(TechKind.FusionEnergy)) city.prodRate *= 1.5f; // Energía de fusión: +50 % de producción en todas las ciudades
        if (city.owner.Has(TechKind.Nanotech)) city.prodRate *= 1.2f; // Nanotecnología: +20 % de producción en todas las ciudades
        if (OwnsBuilding(city.owner, BuildingKind.Pyramids)) city.prodRate *= 1.25f; // Pirámides: +25 % de producción en todas las ciudades
        if (OwnsBuilding(city.owner, BuildingKind.FordAssembly)) city.prodRate *= 1.25f; // Cadena de montaje de Ford: +25 % de producción en todas las ciudades
        if (OwnsBuilding(city.owner, BuildingKind.ManhattanProject)) city.prodRate *= 1.5f; // Proyecto Manhattan: +50 % de producción en todas las ciudades
        city.goldRate = total.z + roadTiles + (city.buildings.Contains(BuildingKind.Market) ? 2f : 0f);
        city.scienceRate = (0.4f + 0.3f * city.pop + roadTiles) * (city.buildings.Contains(BuildingKind.Library) ? 2f : 1f);
        if (city.buildings.Contains(BuildingKind.Bank)) city.goldRate *= 1.5f; // Banco: +50 % de oro en esta ciudad
        if (city.buildings.Contains(BuildingKind.Mall)) city.goldRate *= 1.25f; // Centro comercial: +25 % de oro en esta ciudad
        if (OwnsBuilding(city.owner, BuildingKind.MarcoPolo)) city.goldRate *= 1.3f; // Marco Polo: +30 % de oro en todas las ciudades
        if (OwnsBuilding(city.owner, BuildingKind.EiffelTower)) city.goldRate *= 1.25f; // Torre Eiffel: +25 % de oro en todas las ciudades
        if (city.buildings.Contains(BuildingKind.Cathedral)) city.scienceRate += 1f; // Catedral: +1 de ciencia
        if (city.owner.Has(TechKind.ArabicNumerals))
        {
            city.goldRate *= 1.25f; // Cifras arábigas: +25 % de oro y de ciencia en todas las ciudades
            city.scienceRate *= 1.25f;
        }
        if (city.owner.Has(TechKind.Universities)) city.scienceRate *= 1.25f; // Universidades: +25 % en todas las ciudades
        if (city.owner.Has(TechKind.Electricity)) city.scienceRate *= 1.25f; // Electricidad: +25 % en todas las ciudades
        if (city.owner.Has(TechKind.Radio)) city.scienceRate *= 1.25f; // Radio: +25 % en todas las ciudades
        if (city.owner.Has(TechKind.SpaceFlight)) city.scienceRate *= 1.5f; // Vuelo espacial: +50 % en todas las ciudades
        if (city.owner.Has(TechKind.ArtificialIntelligence)) city.scienceRate *= 1.3f; // Inteligencia artificial: +30 % en todas las ciudades
        if (city.owner.Has(TechKind.QuantumComputing)) city.scienceRate *= 1.4f; // Computación cuántica: +40 % en todas las ciudades
        if (city.buildings.Contains(BuildingKind.QuantumComputer)) city.scienceRate *= 2.5f; // Computadora cuántica: +150 % en esta ciudad
        if (city.buildings.Contains(BuildingKind.ComputerLab)) city.scienceRate *= 2f; // Laboratorio informático: +100 % en esta ciudad
        if (city.owner.Has(TechKind.Philosophy)) city.scienceRate *= 1.5f; // Filosofía: +50 % en todas las ciudades
        if (city.buildings.Contains(BuildingKind.University)) city.scienceRate *= 1.75f; // Universidad: +75 % en esta ciudad
        if (city.buildings.Contains(BuildingKind.Newspaper)) city.scienceRate *= 1.5f; // Periódico: +50 % en esta ciudad
        if (city.buildings.Contains(BuildingKind.PrintingHouse)) city.scienceRate *= 2f; // Prensa: duplica la ciencia en esta ciudad
        if (city.buildings.Contains(BuildingKind.Observatory)) city.scienceRate *= city.owner.Has(TechKind.Telescope) ? 2f : 1.5f; // Observatorio: +50 % en esta ciudad (+100 % con el Telescopio)
        if (OwnsGreatLibrary(city.owner)) city.scienceRate *= 1.5f; // Gran Biblioteca: +50 % en todas las ciudades
        if (OwnsBuilding(city.owner, BuildingKind.AlexandriaLibrary)) city.scienceRate *= 1.4f; // Gran Biblioteca de Alejandría: +40 % en todas las ciudades
        if (OwnsBuilding(city.owner, BuildingKind.GutenbergPress)) city.scienceRate *= 1.5f; // Imprenta de Gutenberg: x1,5 en todas las ciudades
        if (OwnsBuilding(city.owner, BuildingKind.GalileoObservatory)) city.scienceRate *= 1.4f; // Observatorio de Galileo: +40 % en todas las ciudades
        if (OwnsBuilding(city.owner, BuildingKind.FibonacciLab)) city.scienceRate *= 1.25f; // Laboratorio de Fibonacci: +25 % en todas las ciudades
        if (OwnsBuilding(city.owner, BuildingKind.ApolloProgram)) city.scienceRate *= 1.3f; // Programa Apolo: +30 % de ciencia en todas las ciudades
        if (OwnsBuilding(city.owner, BuildingKind.RadioTower)) city.scienceRate *= 1.3f; // Torre de radio de Marconi: +30 % en todas las ciudades
        if (OwnsBuilding(city.owner, BuildingKind.ManhattanProject)) city.scienceRate *= 1.25f; // Proyecto Manhattan: x1,25 en todas las ciudades
        if (OwnsBuilding(city.owner, BuildingKind.Internet)) city.scienceRate *= 1.5f; // Internet: x1,5 en todas las ciudades

        if (city.buildings.Contains(BuildingKind.LunarBase)) { city.scienceRate += 15f; city.goldRate += 10f; } // Base lunar: +15 de ciencia y +10 de oro
        if (OwnsBuilding(city.owner, BuildingKind.SpaceElevator)) city.goldRate *= 1.4f; // Ascensor espacial: +40 % de oro en todas las ciudades

        // Escudos de energía: +100 de vida (una sola vez por ciudad)
        if (city.owner.Has(TechKind.EnergyShields) && !city.shieldApplied)
        {
            city.shieldApplied = true;
            city.maxHp += 100f;
            city.hp += 100f;
        }

        // Cada ruta comercial activa da +15 % de oro y de ciencia a la ciudad
        float trade = 1f + TradeBonusOf(city);
        city.goldRate *= trade;
        city.scienceRate *= trade;
        city.scienceRate *= 1f + city.owner.scienceBonus;   // bonus de la civilización elegida
        city.prodRate *= 1f + city.owner.productionBonus;
        city.goldRate *= 1f + city.owner.goldBonus;

        // Felicidad: feliz +10 %, descontenta -25 %, disturbios 0 (ver CivGame.Happiness.cs)
        float moodK = ApplyHappiness(city);
        city.prodRate *= moodK;
        city.scienceRate *= moodK;
        city.goldRate *= moodK;

        // Dificultad: la IA produce más o menos según el nivel elegido
        if (!city.owner.isHuman)
        {
            float k = AiRateMultiplier;
            city.prodRate *= k;
            city.scienceRate *= k;
            city.goldRate *= k;
        }

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
        float surplus = c.FoodSurplus;
        if (surplus > 0f && !c.owner.isHuman) surplus *= AiRateMultiplier;   // dificultad: la IA crece más o menos deprisa
        if (surplus > 0f && c.owner.Has(TechKind.Thermometer)) surplus *= 1.2f; // Termómetro: +20 % de crecimiento en todas las ciudades
        if (surplus > 0f && c.owner.Has(TechKind.Penicillin)) surplus *= 1.3f; // Penicilina: +30 % de crecimiento en todas las ciudades
        if (surplus > 0f && OwnsBuilding(c.owner, BuildingKind.FlemingLab)) surplus *= 1.3f; // Laboratorio de Fleming: +30 % de crecimiento en todas las ciudades
        if (surplus > 0f && c.owner.Has(TechKind.GeneticEngineering)) surplus *= 1.5f; // Ingeniería genética: +50 % de crecimiento en todas las ciudades
        if (surplus > 0f && OwnsBuilding(c.owner, BuildingKind.GeneBank)) surplus *= 1.4f; // Banco genético: +40 % de crecimiento en todas las ciudades
        if (surplus > 0f && OwnsBuilding(c.owner, BuildingKind.StatueOfLiberty)) surplus *= 1.15f; // Estatua de la Libertad: +15 % de crecimiento en todas las ciudades
        if (surplus > 0f && OwnsBuilding(c.owner, BuildingKind.Biosphere)) surplus *= 1.3f; // Biosfera: +30 % de crecimiento en todas las ciudades
        if (surplus > 0f && c.buildings.Contains(BuildingKind.Hospital)) surplus *= 1.1f; // Hospital: +10 % de crecimiento en esta ciudad
        if (surplus > 0f && OwnsBuilding(c.owner, BuildingKind.FahrenheitStudy)) surplus *= 1.3f; // Estudio de Fahrenheit: +30 % de crecimiento en todas las ciudades
        if (surplus > 0f) surplus *= 1f + c.owner.growthBonus;   // bonus de la civilización elegida
        if (surplus > 0f) surplus *= MoodGrowthFactor(c.mood);   // felicidad: feliz +10 %, descontenta la mitad, disturbios nada
        c.food += surplus * dt;

        // Sin Fuego la ciudad no pasa de PopWithoutFire habitantes
        bool canGrow = c.pop < GameDefs.PopWithoutFire || c.owner.Has(TechKind.Fire);

        if (c.food >= 12f + c.pop * 8f && canGrow)
        {
            c.food = 0f;
            c.pop++;
            if (c.owner.isHuman) GameSfx.Play(Sfx.Grow, 0.6f);

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
                if (!u.alive || u.owner == c.owner || !u.def.IsMilitary || !AtWar(c.owner, u.owner)) continue;
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
            if (c.owner.isHuman)
            {
                AddLog(c.name + ": " + d.name + " listo", c.owner.color);
                GameSfx.Play(Sfx.UnitReady);
            }
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
            if (c.itemBuilding == BuildingKind.GutenbergPrint) GrantNewspapers(c.owner);
            if (c.owner.isHuman)
            {
                AddLog(c.name + ": " + GameDefs.Buildings[c.itemBuilding].name + " construido", c.owner.color);
                GameSfx.Play(IsWonder(c.itemBuilding) ? Sfx.Wonder : Sfx.Built);
                if (c.itemBuilding == BuildingKind.Pyramids) IntroVideo.PlayClip(PyramidsVideo);   // vídeo al terminar las Pirámides (solo las tuyas)
                if (c.itemBuilding == BuildingKind.LighthouseAlex) IntroVideo.PlayClip(LighthouseVideo);   // y al terminar el Faro de Alejandría
                if (c.itemBuilding == BuildingKind.GreatWall) IntroVideo.PlayClip(GreatWallVideo);   // y al terminar la Gran Muralla
                if (c.itemBuilding == BuildingKind.FibonacciLab) IntroVideo.PlayClip(FibonacciVideo);   // y al terminar el Laboratorio de Fibonacci
                if (c.itemBuilding == BuildingKind.Colosseum)IntroVideo.PlayClip(ColosseumVideo);   // y al terminar el Coliseo
                if (c.itemBuilding == BuildingKind.AlexandriaLibrary) IntroVideo.PlayClip(AlexandriaVideo);   // y al terminar la Gran Biblioteca de Alejandría
            }
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
            case UnitKind.WarElephant: return civ.Has(TechKind.Riding) && civ.Has(TechKind.Bronze);
            case UnitKind.HussiteWagon: return civ.Has(TechKind.Camshaft);
            case UnitKind.Chariot:  return civ.Has(TechKind.Wheel);
            case UnitKind.Phalanx:  return civ.Has(TechKind.Bronze);
            case UnitKind.Legion:   return civ.Has(TechKind.Iron);
            case UnitKind.Catapult: return civ.Has(TechKind.Mathematics);
            case UnitKind.Sailboat: return civ.Has(TechKind.Sailing);
            case UnitKind.Galleon:  return civ.Has(TechKind.Compass);
            case UnitKind.Musketeer: return civ.Has(TechKind.Gunpowder);
            case UnitKind.Crossbowman: return civ.Has(TechKind.Crossbow);
            case UnitKind.Rifleman: return civ.Has(TechKind.Rifle);
            case UnitKind.Grenadier: return civ.Has(TechKind.Grenade);
            case UnitKind.Cannon:   return civ.Has(TechKind.Artillery);
            case UnitKind.Steamship: return civ.Has(TechKind.SteamEngine);
            case UnitKind.Tank:     return civ.Has(TechKind.Tanks);
            case UnitKind.Mecha:    return civ.Has(TechKind.PlasmaWeapons);
            case UnitKind.Fighter:
            case UnitKind.Bomber:   return civ.Has(TechKind.Aviation);
            case UnitKind.Ballista: return civ.Has(TechKind.Crossbow);
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
        return kind == BuildingKind.GreatLibrary || kind == BuildingKind.FibonacciLab || kind == BuildingKind.AlexandriaLibrary || kind == BuildingKind.GutenbergPress || kind == BuildingKind.HooverDam || kind == BuildingKind.GutenbergPrint || kind == BuildingKind.GalileoObservatory || kind == BuildingKind.FahrenheitStudy
            || kind == BuildingKind.RadioTower || kind == BuildingKind.FordAssembly || kind == BuildingKind.FlemingLab || kind == BuildingKind.ManhattanProject || kind == BuildingKind.Internet
            || kind == BuildingKind.GeneBank || kind == BuildingKind.SpaceElevator
            || kind == BuildingKind.Pyramids || kind == BuildingKind.LighthouseAlex || kind == BuildingKind.GreatWall || kind == BuildingKind.Colosseum
            || kind == BuildingKind.SistineChapel || kind == BuildingKind.MarcoPolo || kind == BuildingKind.EiffelTower || kind == BuildingKind.StatueOfLiberty
            || kind == BuildingKind.Hollywood || kind == BuildingKind.ApolloProgram || kind == BuildingKind.UnitedNations || kind == BuildingKind.Biosphere;
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

    // Prensa de Gutenberg: la civilización que la posee tiene un Periódico en cada una de sus ciudades
    void GrantNewspapers(Civ civ)
    {
        if (!OwnsBuilding(civ, BuildingKind.GutenbergPrint)) return;

        foreach (City city in civ.cities)
        {
            if (city.buildings.Contains(BuildingKind.Newspaper)) continue;

            city.buildings.Add(BuildingKind.Newspaper);
            if (city.hasItem && !city.itemIsUnit && city.itemBuilding == BuildingKind.Newspaper) city.hasItem = false;
            RecomputeCity(city);
            RebuildCityView(city);
        }
    }

    bool OwnsGreatLibrary(Civ civ) { return OwnsBuilding(civ, BuildingKind.GreatLibrary); }

    /// <summary>La ciudad tiene un río en alguna casilla explotable (de su territorio, o la de la propia ciudad).</summary>
    public bool CityHasRiver(City city)
    {
        if (grid.river[city.cell]) return true;
        foreach (int c in city.tiles)
            if (grid.tileCity[c] == city && grid.river[c]) return true;
        return false;
    }

    public bool Available(City city, BuildingKind kind)
    {
        if (kind == BuildingKind.Granary && !city.owner.Has(TechKind.Agriculture)) return false;
        if (kind == BuildingKind.Walls && !city.owner.Has(TechKind.Masonry)) return false;
        if (kind == BuildingKind.Library && !city.owner.Has(TechKind.Writing)) return false;
        if (kind == BuildingKind.Observatory && !city.owner.Has(TechKind.Astronomy)) return false;
        if (kind == BuildingKind.GreatLibrary) return false;   // maravilla retirada del juego (se conserva el tipo para las partidas guardadas)
        if (kind == BuildingKind.AlexandriaLibrary && (!city.owner.Has(TechKind.Universities) || WonderExists(kind))) return false;
        if (kind == BuildingKind.University && !city.owner.Has(TechKind.Universities)) return false;
        if (kind == BuildingKind.FullingMill && !city.owner.Has(TechKind.Camshaft)) return false;
        if (kind == BuildingKind.Academy && !city.owner.Has(TechKind.Cities)) return false;
        if (kind == BuildingKind.GutenbergPress && (!city.owner.Has(TechKind.PrintingPress) || WonderExists(kind))) return false;
        if (kind == BuildingKind.GalileoObservatory && (!city.owner.Has(TechKind.Telescope) || WonderExists(kind))) return false;
        if (kind == BuildingKind.FahrenheitStudy && (!city.owner.Has(TechKind.Thermometer) || WonderExists(kind))) return false;
        if (kind == BuildingKind.Newspaper &&!city.owner.Has(TechKind.MovableType)) return false;
        if (kind == BuildingKind.GutenbergPrint && (!city.owner.Has(TechKind.MovableType) || WonderExists(kind))) return false;
        if (kind == BuildingKind.HooverDam && (!city.owner.Has(TechKind.Electricity) || !CityHasRiver(city) || WonderExists(kind))) return false;
        if (kind == BuildingKind.HydroDam && (!city.owner.Has(TechKind.Electricity) || !CityHasRiver(city))) return false;
        if (kind == BuildingKind.Factory && !city.owner.Has(TechKind.SteamEngine)) return false;
        if (kind == BuildingKind.RadioTower && (!city.owner.Has(TechKind.Radio) || WonderExists(kind))) return false;
        if (kind == BuildingKind.FordAssembly && (!city.owner.Has(TechKind.CombustionEngine) || WonderExists(kind))) return false;
        if (kind == BuildingKind.FlemingLab && (!city.owner.Has(TechKind.Penicillin) || WonderExists(kind))) return false;
        if (kind == BuildingKind.ManhattanProject && (!city.owner.Has(TechKind.NuclearEnergy) || WonderExists(kind))) return false;
        if (kind == BuildingKind.Internet && (!city.owner.Has(TechKind.Computers) || WonderExists(kind))) return false;
        if (kind == BuildingKind.FusionReactor && !city.owner.Has(TechKind.FusionEnergy)) return false;
        if (kind == BuildingKind.QuantumComputer && !city.owner.Has(TechKind.QuantumComputing)) return false;
        if (kind == BuildingKind.LunarBase && !city.owner.Has(TechKind.SpaceColonization)) return false;
        if (kind == BuildingKind.GeneBank && (!city.owner.Has(TechKind.GeneticEngineering) || WonderExists(kind))) return false;
        if (kind == BuildingKind.SpaceElevator && (!city.owner.Has(TechKind.SpaceColonization) || WonderExists(kind))) return false;
        if (kind == BuildingKind.Pyramids && (!city.owner.Has(TechKind.Masonry) || WonderExists(kind))) return false;
        if (kind == BuildingKind.LighthouseAlex && (!city.owner.Has(TechKind.Sailing) || WonderExists(kind) || SeaCellNextTo(city) < 0)) return false;   // solo en ciudades costeras
        if (kind == BuildingKind.GreatWall && (!city.owner.Has(TechKind.Construction) || WonderExists(kind))) return false;
        if (kind == BuildingKind.Colosseum && (!city.owner.Has(TechKind.Construction) || WonderExists(kind))) return false;
        if (kind == BuildingKind.SistineChapel && (!city.owner.Has(TechKind.Universities) || WonderExists(kind))) return false;
        if (kind == BuildingKind.MarcoPolo && (!city.owner.Has(TechKind.Compass) || WonderExists(kind))) return false;
        if (kind == BuildingKind.EiffelTower && (!city.owner.Has(TechKind.Electricity) || WonderExists(kind))) return false;
        if (kind == BuildingKind.StatueOfLiberty && (!city.owner.Has(TechKind.Railway) || WonderExists(kind))) return false;
        if (kind == BuildingKind.Hollywood && (!city.owner.Has(TechKind.Computers) || WonderExists(kind))) return false;
        if (kind == BuildingKind.ApolloProgram && (!city.owner.Has(TechKind.Aviation) || WonderExists(kind))) return false;
        if (kind == BuildingKind.UnitedNations && (!city.owner.Has(TechKind.Radio) || WonderExists(kind))) return false;
        if (kind == BuildingKind.Biosphere && (!city.owner.Has(TechKind.GeneticEngineering) || WonderExists(kind))) return false;
        if (kind == BuildingKind.Temple && !city.owner.Has(TechKind.Pottery)) return false;
        if (kind == BuildingKind.Amphitheater && !city.owner.Has(TechKind.Construction)) return false;
        if (kind == BuildingKind.Cathedral && !city.owner.Has(TechKind.Universities)) return false;
        if (kind == BuildingKind.Theater && !city.owner.Has(TechKind.MovableType)) return false;
        if (kind == BuildingKind.Hospital && !city.owner.Has(TechKind.Penicillin)) return false;
        if (kind == BuildingKind.Stadium && !city.owner.Has(TechKind.Radio)) return false;
        if (kind == BuildingKind.Park && !city.owner.Has(TechKind.CombustionEngine)) return false;
        if (kind == BuildingKind.Mall && !city.owner.Has(TechKind.SteelConcrete)) return false;
        if (kind == BuildingKind.Cinema && !city.owner.Has(TechKind.Computers)) return false;
        if (kind == BuildingKind.VRPark && !city.owner.Has(TechKind.ArtificialIntelligence)) return false;
        if (kind == BuildingKind.OrbitalGarden && !city.owner.Has(TechKind.SpaceColonization)) return false;
        if (kind == BuildingKind.Satellite && (!city.owner.Has(TechKind.SpaceFlight) || OwnsBuilding(city.owner, kind))) return false;
        if (kind == BuildingKind.ComputerLab && !city.owner.Has(TechKind.Computers)) return false;
        if (kind == BuildingKind.NuclearPlant && !city.owner.Has(TechKind.NuclearEnergy)) return false;
        if (kind == BuildingKind.PrintingHouse && !city.owner.Has(TechKind.PrintingPress)) return false;
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
        if (city.owner.isHuman) GameSfx.Play(Sfx.Buy);
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
            case UnitKind.Horseman:
                if (civ.Has(TechKind.Camshaft)) return UnitKind.HussiteWagon;
                return civ.Has(TechKind.Wheel) ? UnitKind.Chariot : (UnitKind?)null;
            case UnitKind.Chariot: return civ.Has(TechKind.Camshaft) ? UnitKind.HussiteWagon : (UnitKind?)null;
            case UnitKind.Archer:
                if (civ.Has(TechKind.Rifle)) return UnitKind.Rifleman;
                if (civ.Has(TechKind.Gunpowder)) return UnitKind.Musketeer;
                return civ.Has(TechKind.Crossbow) ? UnitKind.Crossbowman : (UnitKind?)null;
            case UnitKind.Crossbowman:
                if (civ.Has(TechKind.Rifle)) return UnitKind.Rifleman;
                return civ.Has(TechKind.Gunpowder) ? UnitKind.Musketeer : (UnitKind?)null;
            case UnitKind.Musketeer: return civ.Has(TechKind.Rifle) ? UnitKind.Rifleman : (UnitKind?)null;
            case UnitKind.Catapult:
            case UnitKind.Ballista:
                if (civ.Has(TechKind.PlasmaWeapons)) return UnitKind.Mecha;
                if (civ.Has(TechKind.Tanks)) return UnitKind.Tank;
                return civ.Has(TechKind.Artillery) ? UnitKind.Cannon : (UnitKind?)null;
            case UnitKind.HussiteWagon:
            case UnitKind.Cannon:
                if (civ.Has(TechKind.PlasmaWeapons)) return UnitKind.Mecha;
                return civ.Has(TechKind.Tanks) ? UnitKind.Tank : (UnitKind?)null;
            case UnitKind.Tank: return civ.Has(TechKind.PlasmaWeapons) ? UnitKind.Mecha : (UnitKind?)null;
            case UnitKind.Sailboat:
                if (civ.Has(TechKind.SteamEngine)) return UnitKind.Steamship;
                return civ.Has(TechKind.Compass) ? UnitKind.Galleon : (UnitKind?)null;
            case UnitKind.Galleon: return civ.Has(TechKind.SteamEngine) ? UnitKind.Steamship : (UnitKind?)null;
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
        if (civ.isHuman) GameSfx.Play(Sfx.Research);
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

        if (civ.isHuman)
        {
            AddLog("Descubres " + def.name + " (" + def.unlocks + ")", civ.color);
            GameSfx.Play(Sfx.Tech);
        }

        // Victoria por tecnología: la primera civilización que descubre la Omnisciencia gana la partida
        if (civ.currentTech == TechKind.Omniscience && winner == null)
        {
            winner = civ;
            winnerByTech = true;
            AddLog(civ.name + " alcanza la Omnisciencia: ¡victoria por tecnología!", civ.color);
            IntroVideo.PlayClip(OmniscienceVideo);   // vídeo de victoria, sea quien sea la civilización ganadora
        }

        // La hoguera (Fuego) se ve en las ciudades; la Ganadería cambia el rendimiento de las casillas
        foreach (City c in civ.cities)
        {
            RecomputeCity(c);
            CivViews.BuildCity(c, grid, EarthT);
        }

        // Cambio de era: las carreteras de la civilización pasan a piedra y sus unidades a su modelo medieval
        bool eraChanged = civ.currentTech == TechKind.EraChange || civ.currentTech == TechKind.EraChange3 || civ.currentTech == TechKind.EraChange4;
        if (eraChanged)
            CivViews.UpgradeRoads(civ, grid, earth, EarthT);

        if (eraChanged)
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

        // Segundo Cambio de era del jugador: vídeo "Video cambio a edad moderna"
        if (civ.isHuman && def == GameDefs.Techs[TechKind.EraChange2])
            IntroVideo.PlayClip(EraChange2Video);

        // Tercer Cambio de era del jugador: vídeo "Cambio a edad contemporanea con jazz"
        if (civ.isHuman && def == GameDefs.Techs[TechKind.EraChange3])
            IntroVideo.PlayClip(EraChange3Video);

        // Cuarto Cambio de era del jugador: vídeo "Cambio a edad futurista con audio"
        if (civ.isHuman && def == GameDefs.Techs[TechKind.EraChange4])
            IntroVideo.PlayClip(EraChange4Video);
    }

    const string EraChangeVideo = "Video cambio a edad media";
    const string EraChange2Video = "Video cambio a edad moderna";
    const string EraChange3Video = "Cambio a edad contemporanea con jazz";
    const string EraChange4Video = "Cambio a edad futurista con audio";
    const string OmniscienceVideo = "Victoria por omnisciencia";
    const string PyramidsVideo = "Construcción de las pirámides";
    const string FibonacciVideo = "Construcción de Laboratorio Fibonacci";   // el archivo dice "de", no "del"
    const string ColosseumVideo ="Construcción del Coliseo";
    const string AlexandriaVideo ="Construcción de la Gran Biblioteca Alejandria";   // el archivo lleva tilde en Construcción y no en Alejandria
    const string GreatWallVideo ="Construccion de la Gran Muralla";   // el archivo se llama sin tilde
    const string LighthouseVideo = "Construcción del Faro";   // el nombre debe coincidir con el archivo (con F mayúscula)
    const string MilitaryVictoryVideo = "Video Victoria Militar";
    const string MilitaryDefeatVideo = "Vídeo derrota militar";

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
        if (human != null && (u.owner == human || IsVisible(u.cell))) GameSfx.Play(Sfx.Death, u.owner == human ? 1f : 0.5f);
        if (!u.alive) return;
        u.alive = false;

        u.owner.units.Remove(u);
        allUnits.Remove(u);
        if (u.view != null) Destroy(u.view);

        // Si se hunde el barco, se pierde también su carga
        if (u.cargo.Count > 0)
        {
            List<Unit> passengers = new List<Unit>(u.cargo);
            u.cargo.Clear();
            foreach (Unit passenger in passengers)
            {
                passenger.carriedBy = null;
                KillUnit(passenger);
            }
        }
        if (u.carriedBy != null) u.carriedBy.cargo.Remove(u);

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
        if (u.def.air) p += d * grid.spacing * 0.35f; // Los aviones vuelan a cierta altura sobre el terreno

        u.view.transform.localPosition = p;
        Quaternion rot = Quaternion.FromToRotation(Vector3.up, d);

        // Todas las unidades apuntan el frente (+Z del modelo) hacia la casilla a la que se dirigen; si no se mueven pero
        // atacan, hacia su objetivo; y si no, conservan hacia donde miraban
        int faceCell = -1;
        if (u.Moving) faceCell = u.moveTo;
        else if (u.targetCity != null) faceCell = u.targetCity.cell;
        else if (u.targetUnit != null && u.targetUnit.alive) faceCell = u.targetUnit.cell;

        if (faceCell >= 0 && faceCell != u.cell)
        {
            Vector3 toward = Vector3.ProjectOnPlane(grid.dir[faceCell] - grid.dir[u.cell], d);
            if (toward.sqrMagnitude > 1e-8f) u.heading = toward.normalized;
        }

        Vector3 fwd = Vector3.ProjectOnPlane(u.heading, d);
        if (fwd.sqrMagnitude > 1e-8f) rot = Quaternion.LookRotation(fwd.normalized, d);

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
    public bool OrderRoad(Unit u, int goal, bool rail = false)
    {
        if (u.kind != UnitKind.Worker || !grid.land[goal]) return false;

        if (rail && !u.owner.Has(TechKind.Railway))
        {
            if (u.owner.isHuman) AddLog("Necesitas descubrir el Ferrocarril para construir vías (T)", Color.red);
            return false;
        }

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
        u.railMode = rail;

        if (u.owner.isHuman) AddLog("Trabajador: construyendo " + (rail ? "ferrocarril" : "carretera") + " hacia " + RoadGoalName(goal), u.owner.color);
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
        u.railMode = false;
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
            if (u.owner.isHuman) AddLog((u.railMode ? "Ferrocarril terminado hasta " : "Carretera terminada hasta ") + RoadGoalName(u.roadGoal), u.owner.color);
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
        if (u.railMode)
        {
            // Mejora de la carretera a ferrocarril: si el tramo aún no tenía carretera, se construye también
            if (!grid.HasRail(u.cell, next) && !riverBlocks)
            {
                u.roadProgress += dt / railBuildSeconds;
                if (u.roadProgress < 1f) return;

                if (!grid.HasRoad(u.cell, next))
                {
                    grid.AddRoad(u.cell, next);
                    CivViews.BuildRoad(u.cell, next, grid, earth, EarthT, u.owner);
                }

                grid.AddRail(u.cell, next);
                CivViews.BuildRail(u.cell, next, grid, earth, EarthT, u.owner);

                // El ferrocarril cambia el rendimiento de las ciudades que controlan esas casillas
                if (grid.tileCity[u.cell] != null) RecomputeCity(grid.tileCity[u.cell]);
                if (grid.tileCity[next] != null && grid.tileCity[next] != grid.tileCity[u.cell]) RecomputeCity(grid.tileCity[next]);
                u.roadProgress = 0f;
            }
        }
        else if (!grid.HasRoad(u.cell, next) && !riverBlocks)
        {
            u.roadProgress += dt / roadBuildSeconds;
            if (u.roadProgress < 1f) return;

            grid.AddRoad(u.cell, next);
            CivViews.BuildRoad(u.cell, next, grid, earth, EarthT, u.owner);
            if (u.owner.isHuman) GameSfx.Play(Sfx.Work, 0.6f);
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

    /// <summary>Bonus de defensa por fortificación: 0, +25 %, o +50 % si está en una ciudad propia con muro.</summary>
    public float FortifyBonusOf(Unit u)
    {
        if (!u.fortified) return 0f;

        City c = grid != null ? grid.cityAt[u.cell] : null;
        if (c != null && c.alive && c.owner == u.owner && c.buildings.Contains(BuildingKind.Walls)) return Unit.WalledFortifyBonus;

        return Unit.FortifyBonus;
    }

    public float EffectiveDefense(Unit u)
    {
        return u.def.defense * (1f + FortifyBonusOf(u));
    }

    /// <summary>
    /// Fortifica (o cancela/quita la fortificación de) una unidad militar parada: tarda 10 s en estar lista y entonces gana +25 % de defensa.
    /// Se pierde al darle una orden de movimiento o ataque.
    /// </summary>
    public bool ToggleFortify(Unit u)
    {
        if (u == null || !u.alive || !u.def.IsMilitary || u.carriedBy != null) return false;

        // Ya fortificada o fortificándose: se cancela
        if (u.fortified || u.fortifying)
        {
            u.fortified = false;
            u.fortifying = false;
            u.fortifyProgress = 0f;
            CivViews.SyncFortifyMarker(u, grid);
            return true;
        }

        if (u.Moving) return false;

        // Empieza a fortificarse: estará lista en Unit.FortifySeconds (10 s)
        u.path.Clear();
        u.targetUnit = null;
        u.targetCity = null;
        u.fortifying = true;
        u.fortifyProgress = 0f;
        CivViews.SyncFortifyMarker(u, grid);
        return true;
    }

    public void OrderMove(Unit u, int cell)
    {
        if (u.owner.isHuman) GameSfx.Play(Sfx.Order);
        u.fortified = false;
        u.fortifying = false;
        u.fortifyProgress = 0f;
        CancelRoad(u);
        u.targetUnit = null;
        u.targetCity = null;
        u.foundAt = -1;
        u.joinAt = -1;
        u.boardTarget = null;
        u.disembarkAt = -1;

        int from = u.Moving ? u.moveTo : u.cell;
        List<int> path = grid.FindPath(from, cell, u.owner, u.def.naval, u.def.air);

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
        if (ship.owner != u.owner || ship.def.capacity <= 0 || ship.cargo.Count >= ship.def.capacity) return false;

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

        if (!ship.alive || ship.cargo.Count >= ship.def.capacity)
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
        ship.cargo.Add(u);
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
        if (ship.cargo.Count == 0 || !grid.land[landCell]) return false;

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
        int cell = ship.disembarkAt;
        ship.disembarkAt = -1;
        if (ship.cargo.Count == 0) return;

        List<Unit> passengers = new List<Unit>(ship.cargo);
        ship.cargo.Clear();
        foreach (Unit p in passengers)
        {
            p.carriedBy = null;
            p.cell = cell;
            p.moveTo = -1;
            p.t = 0f;
            p.path.Clear();
            if (p.view != null) p.view.SetActive(true);
            PlaceUnit(p);

            if (ship.owner.isHuman) AddLog(p.def.name + " desembarca", ship.owner.color);
        }
    }

    public void OrderAttack(Unit u, Unit target)
    {
        if (!u.def.IsMilitary || target == null) return;
        if (!AtWar(u.owner, target.owner))
        {
            if (u.owner.isHuman) AddLog("No estás en guerra con " + target.owner.name + " (D: diplomacia)", Color.red);
            return;
        }
        u.fortified = false;
        u.fortifying = false;
        u.fortifyProgress = 0f;
        CancelRoad(u);
        u.targetCity = null;
        u.targetUnit = target;
        if (u.owner.isHuman) GameSfx.Play(Sfx.Order);
        u.foundAt = -1;
        u.path.Clear();
        u.lastTargetCell = -1;
    }

    public void OrderAttack(Unit u, City target)
    {
        if (!u.def.IsMilitary || target == null) return;
        if (!AtWar(u.owner, target.owner))
        {
            if (u.owner.isHuman) AddLog("No estás en guerra con " + target.owner.name + " (D: diplomacia)", Color.red);
            return;
        }
        u.fortified = false;
        u.fortifying = false;
        u.fortifyProgress = 0f;
        CancelRoad(u);
        u.targetUnit = null;
        u.targetCity = target;
        if (u.owner.isHuman) GameSfx.Play(Sfx.Order);
        u.foundAt = -1;
        u.path.Clear();
        u.lastTargetCell = -1;
    }

    public bool TryFound(Unit settler)
    {
        if (settler.kind != UnitKind.Settler || settler.Moving) return false;

        City city = FoundCity(settler.owner, settler.cell);
        if (city == null) return false;

        if (settler.owner.isHuman) GameSfx.Play(Sfx.Found);
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
        if ((u.fortified || u.fortifying) && (u.Moving || u.path.Count > 0))   // al moverse pierde la fortificación (y cancela una en curso)
        {
            u.fortified = false;
            u.fortifying = false;
            u.fortifyProgress = 0f;
        }

        // Fortificándose: tarda FortifySeconds (segundos con el juego a velocidad normal) en estar lista
        if (u.fortifying)
        {
            u.fortifyProgress += dt / SimulationScale;
            if (u.fortifyProgress >= Unit.FortifySeconds)
            {
                u.fortifying = false;
                u.fortified = true;
            }
        }

        CivViews.SyncFortifyMarker(u, grid);   // dibuja o quita la defensa de la fortificación

        if (u.kind == UnitKind.Worker && u.owner.Has(TechKind.Robotics)) dt *= 2f; // Robótica: los trabajadores trabajan (y se mueven) el doble de rápido

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
            if (u.cargo.Count == 0) u.disembarkAt = -1;
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
        if (u.targetUnit != null && (!u.targetUnit.alive || u.targetUnit.owner == u.owner || u.targetUnit.carriedBy != null || !AtWar(u.owner, u.targetUnit.owner))) u.targetUnit = null;
        if (u.targetCity != null && (!u.targetCity.alive || u.targetCity.owner == u.owner || !AtWar(u.owner, u.targetCity.owner))) u.targetCity = null;

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
        float factor = u.def.air ? 1f // Los aviones vuelan a velocidad constante sobre cualquier terreno
            : u.def.naval ? (u.owner.Has(TechKind.Compass) ? 1.5f : 1f) * (OwnsBuilding(u.owner, BuildingKind.LighthouseAlex) ? 1.5f : 1f) // Brújula magnética y Faro de Alejandría: +50 % de velocidad en el mar cada uno
            : grid.HasRail(u.cell, u.moveTo)
            ? WorldGrid.RailSpeedFactor // Ferrocarril: el doble de rápido que una carretera
            : grid.HasRoad(u.cell, u.moveTo)
            ? WorldGrid.RoadSpeedFactor
            : grid.HasRiver(u.cell, u.moveTo)
            ? WorldGrid.RoadSpeedFactor
            : Mathf.Max(0.2f, GameDefs.MoveFactor(grid.biome[u.moveTo]));
        if (!u.def.air && !u.def.naval && u.owner.Has(TechKind.CombustionEngine)) factor *= 1.33f; // Motor de combustión: +33 % de velocidad en tierra
        if (!u.def.air && !u.def.naval && u.owner.Has(TechKind.Antigravity)) factor *= 1.5f; // Antigravedad: +50 % de velocidad en tierra
        u.t += u.def.speed * factor * dt;

        if (u.t >= 1f)
        {
            u.cell = u.moveTo;
            u.moveTo = -1;
            u.t = 0f;
            foreach (Unit p in u.cargo) p.cell = u.cell;
        }

        PlaceUnit(u);
    }

    void AutoEngage(Unit u)
    {
        float range = grid.angularSpacing * Mathf.Max(2.2f, u.def.range + 0.1f);
        float best = float.MaxValue;

        foreach (Unit e in allUnits)
        {
            if (!e.alive || e.owner == u.owner || e.carriedBy != null || !AtWar(u.owner, e.owner)) continue;

            float a = grid.AngleBetween(u.cell, e.cell);
            if (a > range || a >= best) continue;

            best = a;
            u.targetUnit = e;
            u.targetCity = null;
        }

        if (u.targetUnit != null) return;

        foreach (City c in allCities)
        {
            if (!c.alive || c.owner == u.owner || !AtWar(u.owner, c.owner)) continue;

            float a = grid.AngleBetween(u.cell, c.cell);
            if (a > range || a >= best) continue;

            best = a;
            u.targetCity = c;
        }
    }

    void Attack(Unit u)
    {
        u.attackCooldown = 1f;
        AttackSound(u);

        float mult = 0.5f + 0.5f * (u.hp / u.def.hp);
        float dmg = u.def.attack * Random.Range(0.85f, 1.15f) * mult;

        if (u.targetCity != null)
        {
            City c = u.targetCity;
            float defense = 5f + (c.buildings.Contains(BuildingKind.Walls) ? 6f : 0f) + (OwnsBuilding(c.owner, BuildingKind.GreatWall) ? 4f : 0f);   // Gran Muralla: +4 de defensa en todas las ciudades
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
            t.hp -= dmg * 6f / (6f + EffectiveDefense(t));   // fortificada: +25 % de defensa (+50 % en una ciudad propia con muro)

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
        if (human != null && (c.owner == human || newOwner == human)) GameSfx.Play(Sfx.Capture);
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
        GrantNewspapers(newOwner);   // si el conquistador tiene la Prensa de Gutenberg, la ciudad recibe su Periódico

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
        if (civ.isHuman) IntroVideo.PlayClip(MilitaryDefeatVideo);   // te quitan la última ciudad: derrota militar

        int alive = 0;
        Civ last = null;
        foreach (Civ c in civs) if (c.alive) { alive++; last = c; }

        if (alive == 1 && winner == null)
        {
            winner = last;
            AddLog(last.name + " gobierna el mundo", last.color);
            if (last.isHuman) IntroVideo.PlayClip(MilitaryVictoryVideo);   // eliminas a la última civilización rival: victoria militar
        }
    }

    // ---------------------------------------------------------------- Inteligencia artificial

    void AiThink(Civ civ)
    {
        // Investigación: prefiere la economía, la ciencia y los cambios de era, y las más baratas
        if (!civ.researching) AiResearch(civ);

        // Amenazas: refuerzos, defensa y compras en las ciudades con enemigos cerca (antes que cualquier otra decisión)
        AiDefend(civ);

        // Colonos inactivos buscan dónde fundar
        foreach (Unit u in civ.units.ToArray())
        {
            if (u.kind != UnitKind.Settler || !u.alive || u.Moving || u.path.Count > 0 || u.foundAt >= 0 || u.roadGoal >= 0) continue;
            AiSendSettler(u);
        }

        // Trabajadores inactivos mejoran el terreno de sus ciudades y las unen con carreteras
        foreach (Unit u in civ.units.ToArray())
        {
            if (u.kind != UnitKind.Worker || !u.alive || u.Moving || u.path.Count > 0 || u.roadGoal >= 0
                || u.farmAt >= 0 || u.mineAt >= 0 || u.irrigateAt >= 0) continue;
            AiWorkerJob(u);
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

        AiDiplomacy(civ);   // paz, alianzas y declaraciones de guerra
        AiNaval(civ);   // colonizar otras islas por mar (los colonos sin sitio esperan un barco)
        if (gameTime > AiGraceTime) AiMilitary(civ);
    }

    // Tecnologías que mejoran la economía, la ciencia o la población: la IA las prefiere para prosperar
    static readonly HashSet<TechKind> AiEconomyTechs = new HashSet<TechKind>
    {
        TechKind.Agriculture, TechKind.Pottery, TechKind.Writing, TechKind.Mathematics, TechKind.Astronomy, TechKind.Philosophy,
        TechKind.Currency, TechKind.Construction, TechKind.Husbandry, TechKind.Mining, TechKind.Bridges,
        TechKind.HeavyPlow, TechKind.WaterMill, TechKind.Camshaft, TechKind.ArabicNumerals, TechKind.Universities, TechKind.Cities,
        TechKind.Clock, TechKind.PrintingPress, TechKind.SteamEngine, TechKind.Electricity, TechKind.Railway, TechKind.MovableType,
        TechKind.Telescope, TechKind.Thermometer, TechKind.CombustionEngine, TechKind.Radio, TechKind.SteelConcrete,
        TechKind.Penicillin, TechKind.Computers, TechKind.NuclearEnergy, TechKind.SpaceFlight, TechKind.ArtificialIntelligence,
        TechKind.Robotics, TechKind.FusionEnergy, TechKind.QuantumComputing, TechKind.GeneticEngineering, TechKind.Nanotech, TechKind.SpaceColonization
    };

    static bool IsEraChangeTech(TechKind t)
    {
        return t == TechKind.EraChange || t == TechKind.EraChange2 || t == TechKind.EraChange3 || t == TechKind.EraChange4;
    }

    // Elige una tecnología al azar con pesos: economía x4, cambio de era x8 y las baratas más probables
    void AiResearch(Civ civ)
    {
        List<TechKind> options = new List<TechKind>();
        List<float> weights = new List<float>();
        float total = 0f;

        foreach (TechKind t in GameDefs.Techs.Keys)
        {
            if (!civ.CanResearch(t)) continue;

            float w = t == TechKind.Omniscience ? 50f : IsEraChangeTech(t) ? 8f : AiEconomyTechs.Contains(t) ? 4f : 1.5f;   // la IA corre hacia la victoria por tecnología
            w *= 300f / (GameDefs.Techs[t].cost + 300f);

            options.Add(t);
            weights.Add(w);
            total += w;
        }

        if (options.Count == 0) return;

        float pick = Random.value * total;
        for (int i = 0; i < options.Count; i++)
        {
            pick -= weights[i];
            if (pick <= 0f || i == options.Count - 1) { StartResearch(civ, options[i]); return; }
        }
    }

    // Un trabajador de la IA irriga, mina o hace granjas en el territorio de sus ciudades; si no hay nada, une las ciudades con carreteras
    void AiWorkerJob(Unit u)
    {
        Civ civ = u.owner;
        bool canIrrigate = civ.Has(TechKind.Agriculture), canMine = civ.Has(TechKind.Mining), canFarm = civ.Has(TechKind.HeavyPlow);

        if ((canIrrigate || canMine || canFarm) && civ.cities.Count > 0)
        {
            for (int attempt = 0; attempt < 14; attempt++)
            {
                City city = civ.cities[Random.Range(0, civ.cities.Count)];
                if (city.tiles.Count == 0) continue;

                int cell = city.tiles[Random.Range(0, city.tiles.Count)];
                if (grid.tileCity[cell] != city || grid.cityAt[cell] != null) continue;

                if (canFarm && grid.CanFarm(cell) && OrderFarm(u, cell)) return;
                if (canIrrigate && grid.CanIrrigate(cell) && OrderIrrigate(u, cell)) return;
                if (canMine && grid.CanMine(cell) && OrderMine(u, cell)) return;
            }
        }

        // Sin mejoras a mano: carreteras entre sus ciudades (no cada vez, para no recalcular rutas sin parar)
        if (Random.value < 0.35f) AiRoad(u);
    }

    // La unidad militar terrestre más potente que la civilización puede construir (las demás ya no se construyen)
    UnitKind AiBestMilitary(Civ civ)
    {
        UnitKind best = UnitKind.Warrior;
        float bestPower = -1f;

        foreach (KeyValuePair<UnitKind, UnitDef> kv in GameDefs.Units)
        {
            UnitDef d = kv.Value;
            if (!d.IsMilitary || d.naval || d.air || !Available(civ, kv.Key)) continue;

            float power = (d.attack * 0.6f + d.defense * 0.4f) * d.hp;
            if (power > bestPower) { bestPower = power; best = kv.Key; }
        }

        return best;
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

        // Sin sitio donde fundar en esta isla: el colono espera un barco que lo lleve a otra isla (ver AiNaval)
        u.aiWaitBoat = bestCell < 0;
        if (bestCell < 0) return;

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

        int workers = 0;
        foreach (Unit u in civ.units) if (u.kind == UnitKind.Worker) workers++;
        foreach (City o in civ.cities) if (o.hasItem && o.itemIsUnit && o.itemUnit == UnitKind.Worker) workers++;

        int military = 0;
        foreach (Unit u in civ.units) if (u.def.IsMilitary && !u.def.naval && !u.def.air) military++;

        // La más potente que se pueda construir (las antiguas quedan obsoletas)
        UnitKind bestMilitary = AiBestMilitary(civ);

        // 1) Expandirse: fundar ciudades hasta AiMaxCities
        if (civ.cities.Count + settlers < AiMaxCities && settlers == 0 && c.pop >= 2)
        {
            SetProduction(c, UnitKind.Settler);
            return;
        }

        // 2) Defensa mínima
        if (military < civ.cities.Count + 2)
        {
            SetProduction(c, bestMilitary);
            return;
        }

        // 2b) Barco de transporte para colonizar o invadir por mar (hasta 2 en total, contando los que ya se están construyendo)
        if (civ.aiWantsTransport && SeaCellNextTo(c) >= 0)
        {
            int transports = 0;
            foreach (Unit u in civ.units) if (IsTransport(u)) transports++;
            foreach (City o in civ.cities) if (o.hasItem && o.itemIsUnit && GameDefs.Units[o.itemUnit].naval && GameDefs.Units[o.itemUnit].capacity > 0) transports++;

            UnitKind? ship = AiBestTransport(c);
            if (transports < 2 && ship.HasValue)
            {
                SetProduction(c, ship.Value);
                return;
            }
        }

        // 2c) Felicidad: una ciudad descontenta o en disturbios construye el edificio que más felicidad da por coste
        if (c.mood <= MoodUnhappy)
        {
            BuildingKind? joy = BestHappinessBuilding(c);
            if (joy.HasValue)
            {
                SetProduction(c, joy.Value);
                return;
            }
        }

        // 3) Trabajadores para mejorar el terreno y construir carreteras
        if (workers < Mathf.Min(civ.cities.Count + 1, 5) && c.pop >= 2)
        {
            SetProduction(c, UnitKind.Worker);
            return;
        }

        // 4) Edificios que hacen prosperar a la ciudad, por orden de utilidad
        BuildingKind[] priority =
        {
            BuildingKind.Granary, BuildingKind.Workshop, BuildingKind.Library, BuildingKind.Market,
            BuildingKind.FullingMill, BuildingKind.University, BuildingKind.Bank, BuildingKind.Observatory,
            BuildingKind.Factory, BuildingKind.HydroDam, BuildingKind.PrintingHouse, BuildingKind.Newspaper,
            BuildingKind.ComputerLab, BuildingKind.NuclearPlant, BuildingKind.QuantumComputer, BuildingKind.FusionReactor, BuildingKind.LunarBase, BuildingKind.Walls
        };
        foreach (BuildingKind b in priority)
        {
            if (!Available(c, b)) continue;
            SetProduction(c, b);
            return;
        }

        // 5) Maravillas: la más barata disponible, solo en ciudades con buena producción
        if (c.prodRate >= 6f)
        {
            BuildingKind? wonder = null;
            float wonderCost = float.MaxValue;
            foreach (BuildingKind b in GameDefs.Buildings.Keys)
            {
                if (!IsWonder(b) || !Available(c, b)) continue;
                float cost = c.BuildingCost(b);
                if (cost < wonderCost) { wonderCost = cost; wonder = b; }
            }

            if (wonder.HasValue)
            {
                SetProduction(c, wonder.Value);
                return;
            }
        }

        // 6) Más ejército (con el que atacar a los rivales)
        SetProduction(c, bestMilitary);
    }


    void AiMilitary(Civ civ)
    {
        List<Unit> idle = new List<Unit>();
        foreach (Unit u in civ.units)
        {
            if (AiIdleSoldier(u)) idle.Add(u);
        }

        // Tropas desembarcadas en una isla donde no tenemos ciudades: atacan directamente la ciudad enemiga más cercana de ella
        foreach (Unit u in idle.ToArray())
        {
            int comp = grid.component[u.cell];
            if (comp < 0) continue;

            bool ownCityHere = false;
            foreach (City c in civ.cities) if (c.alive && grid.component[c.cell] == comp) { ownCityHere = true; break; }
            if (ownCityHere) continue;

            City near = null;
            float nearAngle = float.MaxValue;
            foreach (City e in allCities)
            {
                if (!e.alive || e.owner == civ || !AtWar(civ, e.owner) || grid.component[e.cell] != comp) continue;
                float a = grid.AngleBetween(u.cell, e.cell);
                if (a < nearAngle) { nearAngle = a; near = e; }
            }

            if (near == null) continue;

            OrderAttack(u, near);
            idle.Remove(u);
        }

        // Las que están paradas dentro de una ciudad propia se fortifican (+25 % de defensa)
        foreach (Unit u in idle)
        {
            City here = grid.cityAt[u.cell];
            if (!u.fortified && !u.fortifying && here != null && here.owner == civ) ToggleFortify(u);
        }

        // Con una ciudad amenazada no se lanzan ataques: el ejército se queda a defender
        if (civ.aiThreatened) return;

        // Se dejan algunos de guarnición
        if (idle.Count < civ.cities.Count + 3) return;

        // Objetivo: la ciudad enemiga alcanzable por tierra con mejor relación cercanía/debilidad
        Unit scout = idle[0];
        City target = null;
        float bestScore = float.MaxValue;

        foreach (City c in allCities)
        {
            if (!c.alive || c.owner == civ || !AtWar(civ, c.owner)) continue;
            if (grid.component[scout.cell] != grid.component[c.cell]) continue;

            float a = grid.AngleBetween(scout.cell, c.cell) / grid.angularSpacing;   // distancia en casillas
            float score = a + 12f * (c.hp / Mathf.Max(1f, c.maxHp)) + c.pop * 0.5f;  // más lejos, más sana y más grande = peor objetivo
            if (score >= bestScore) continue;

            bestScore = score;
            target = c;
        }

        if (target == null) return;

        // No ataca hasta reunir un ejército suficiente para esa ciudad (murallas y población la hacen más difícil)
        int needed = 3 + target.pop / 3 + (target.buildings.Contains(BuildingKind.Walls) ? 3 : 0);
        int send = idle.Count - civ.cities.Count;
        if (send < needed) return;

        for (int i = 0; i < send; i++) OrderAttack(idle[i], target);

        if (target.owner.isHuman) AddLog(civ.name + " marcha contra " + target.name + "!", civ.color);
    }
}
