using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// ---------------------------------------------------------------------------------------------------------------------
// Guardar y cargar partida (una sola ranura: partida.json en la carpeta de datos del usuario).
// Se guarda el estado de la partida (civilizaciones, ciudades, unidades y mejoras del mapa) y la semilla del planeta;
// al cargar se regenera el planeta con esa semilla y se reconstruye todo. Las órdenes en curso de las unidades
// (rutas, obras...) no se guardan: tras cargar las unidades quedan quietas.
// ---------------------------------------------------------------------------------------------------------------------

[Serializable]
public class CivSaveData
{
    public int version = 1;
    public int seed;
    public int gridCells;
    public float gameTime;
    public string savedAt;
    public List<CivSaveCiv> civs = new List<CivSaveCiv>();
    public List<CivSaveCity> cities = new List<CivSaveCity>();
    public List<CivSaveUnit> units = new List<CivSaveUnit>();
    public List<int> roads = new List<int>();        // de tres en tres: casilla a, casilla b, id de la civilización que la construyó
    public List<int> rails = new List<int>();        // de dos en dos: casilla a, casilla b
    public List<int> farmed = new List<int>();
    public List<int> irrigated = new List<int>();
    public List<int> mined = new List<int>();
}

[Serializable]
public class CivSaveCiv
{
    public int id;
    public bool isHuman, alive, researching;
    public float gold, researchProgress;
    public int currentTech, nameCounter;
    public List<int> techs = new List<int>();
}

[Serializable]
public class CivSaveCity
{
    public int owner;
    public string name;
    public int cell, pop, claimDepth;
    public float food, prodStock, hp, maxHp, claimTimer;
    public bool hasItem, itemIsUnit;
    public int itemUnit, itemBuilding;
    public List<int> cells = new List<int>();
    public List<int> tiles = new List<int>();
    public List<int> buildings = new List<int>();
    public List<int> wonderKinds = new List<int>();
    public List<int> wonderCells = new List<int>();
    public List<int> tradePartners = new List<int>();     // índices en la lista de ciudades
    public List<int> merchantPartners = new List<int>();
}

[Serializable]
public class CivSaveUnit
{
    public int owner, kind, cell, homeCity = -1, carriedBy = -1;
    public float hp;
}

public static class CivSave
{
    public static string FilePath => Path.Combine(Application.persistentDataPath, "partida.json");

    public static bool Exists => File.Exists(FilePath);

    public static string SavedAt()
    {
        CivSaveData d = Read();
        return d == null ? null : d.savedAt;
    }

    public static CivSaveData Read()
    {
        try
        {
            if (!File.Exists(FilePath)) return null;
            return JsonUtility.FromJson<CivSaveData>(File.ReadAllText(FilePath));
        }
        catch (Exception e)
        {
            Debug.LogWarning("[Civilogenia] No se pudo leer la partida guardada: " + e.Message);
            return null;
        }
    }

    public static bool Write(CivSaveData data)
    {
        try
        {
            File.WriteAllText(FilePath, JsonUtility.ToJson(data));
            return true;
        }
        catch (Exception e)
        {
            Debug.LogWarning("[Civilogenia] No se pudo guardar la partida: " + e.Message);
            return false;
        }
    }
}

public partial class CivGame
{
    // Objetos que ya colgaban de la Tierra antes de empezar la partida (no son de la partida y no se destruyen al cargar)
    readonly HashSet<GameObject> baseChildren = new HashSet<GameObject>();

    void CaptureBaseChildren()
    {
        baseChildren.Clear();
        foreach (Transform child in EarthT) baseChildren.Add(child.gameObject);
    }

    // ---------------------------------------------------------------- Guardar

    public bool SaveGame()
    {
        if (!ready || grid == null) return false;

        CivSaveData data = new CivSaveData
        {
            seed = earth.seed,
            gridCells = gridCells,
            gameTime = gameTime,
            savedAt = DateTime.Now.ToString("dd/MM/yyyy HH:mm")
        };

        foreach (Civ civ in civs)
        {
            CivSaveCiv c = new CivSaveCiv
            {
                id = civ.id, isHuman = civ.isHuman, alive = civ.alive, researching = civ.researching,
                gold = civ.gold, researchProgress = civ.researchProgress, currentTech = (int)civ.currentTech,
                nameCounter = civ.nameCounter
            };
            foreach (TechKind t in civ.techs) c.techs.Add((int)t);
            data.civs.Add(c);
        }

        List<City> cityList = new List<City>();
        foreach (City city in allCities) if (city.alive) cityList.Add(city);

        foreach (City city in cityList)
        {
            CivSaveCity c = new CivSaveCity
            {
                owner = city.owner.id, name = city.name, cell = city.cell, pop = city.pop, claimDepth = city.claimDepth,
                food = city.food, prodStock = city.prodStock, hp = city.hp, maxHp = city.maxHp, claimTimer = city.claimTimer,
                hasItem = city.hasItem, itemIsUnit = city.itemIsUnit, itemUnit = (int)city.itemUnit, itemBuilding = (int)city.itemBuilding
            };
            c.cells.AddRange(city.cells);
            c.tiles.AddRange(city.tiles);
            foreach (BuildingKind b in city.buildings) c.buildings.Add((int)b);
            foreach (KeyValuePair<BuildingKind, int> w in city.wonderCells) { c.wonderKinds.Add((int)w.Key); c.wonderCells.Add(w.Value); }
            foreach (City p in city.tradePartners) { int i = cityList.IndexOf(p); if (i >= 0) c.tradePartners.Add(i); }
            foreach (City p in city.merchantPartners) { int i = cityList.IndexOf(p); if (i >= 0) c.merchantPartners.Add(i); }
            data.cities.Add(c);
        }

        List<Unit> unitList = new List<Unit>();
        foreach (Unit u in allUnits) if (u.alive) unitList.Add(u);

        foreach (Unit u in unitList)
        {
            data.units.Add(new CivSaveUnit
            {
                owner = u.owner.id, kind = (int)u.kind, cell = u.moveTo >= 0 && u.t >= 0.5f ? u.moveTo : u.cell, hp = u.hp,
                homeCity = u.homeCity != null ? cityList.IndexOf(u.homeCity) : -1,
                carriedBy = u.carriedBy != null ? unitList.IndexOf(u.carriedBy) : -1
            });
        }

        // Carreteras (con quien las construyó, que decide su aspecto), ferrocarril y mejoras del terreno
        List<int> roadFlat = new List<int>();
        CivViews.ExportRoads(roadFlat);
        data.roads = roadFlat;
        foreach (Vector2Int e in grid.RailEdges()) { data.rails.Add(e.x); data.rails.Add(e.y); }

        for (int i = 0; i < grid.farmed.Length; i++)
        {
            if (grid.farmed[i]) data.farmed.Add(i);
            if (grid.irrigated[i]) data.irrigated.Add(i);
            if (grid.mined[i]) data.mined.Add(i);
        }

        bool ok = CivSave.Write(data);
        AddLog(ok ? "Partida guardada (" + data.savedAt + ")" : "No se pudo guardar la partida", ok ? Color.white : Color.red);
        return ok;
    }

    // ---------------------------------------------------------------- Cargar

    void ResetWorld()
    {
        foreach (City c in allCities) if (c.view != null) Destroy(c.view);
        foreach (Unit u in allUnits) if (u.view != null) Destroy(u.view);
        foreach (Civ civ in civs) if (civ.territoryObject != null) Destroy(civ.territoryObject);

        // Todo lo demás que la partida colgó de la Tierra (ríos, carreteras, granjas, minas...)
        List<GameObject> extra = new List<GameObject>();
        foreach (Transform child in EarthT) if (!baseChildren.Contains(child.gameObject)) extra.Add(child.gameObject);
        foreach (GameObject go in extra) Destroy(go);

        CivViews.ResetStatics();

        civs.Clear();
        allCities.Clear();
        allUnits.Clear();
        log.Clear();
        human = null;
        winner = null;
    }

    /// <summary>Carga la partida guardada: regenera el planeta con su semilla y reconstruye la partida. Hay que lanzarla como corrutina.</summary>
    public IEnumerator LoadRoutine()
    {
        CivSaveData data = CivSave.Read();
        if (data == null || grid == null)
        {
            AddLog("No hay ninguna partida guardada", Color.red);
            yield break;
        }

        startCancelled = true;   // la partida nueva que esperaba el menú ya no se crea
        ready = false;
        yield return null;

        // Si la partida usa otro planeta, se regenera con su semilla
        if (earth.randomSeed || earth.seed != data.seed)
        {
            earth.randomSeed = false;
            earth.seed = data.seed;
            earth.Generate();
            yield return null;
        }

        ResetWorld();
        yield return null;

        gridCells = data.gridCells;
        grid = new WorldGrid();
        grid.Build(earth, gridCells);
        CivViews.BuildRivers(grid, earth, EarthT);
        yield return null;

        // Mejoras del terreno: granjas, riegos y minas (las carreteras y el ferrocarril, tras crear las civilizaciones)
        foreach (int cell in data.farmed) { grid.farmed[cell] = true; CivViews.BuildFarm(cell, grid, EarthT); }
        foreach (int cell in data.irrigated) { grid.irrigated[cell] = true; CivViews.BuildIrrigation(cell, grid, EarthT); }
        foreach (int cell in data.mined) { grid.mined[cell] = true; CivViews.BuildMine(cell, grid, EarthT); }

        // Civilizaciones
        foreach (CivSaveCiv s in data.civs)
        {
            Civ civ = new Civ
            {
                id = s.id,
                name = GameDefs.CivNames[s.id % GameDefs.CivNames.Length],
                color = GameDefs.CivColors[s.id % GameDefs.CivColors.Length],
                isHuman = s.isHuman,
                alive = s.alive,
                gold = s.gold,
                researching = s.researching,
                currentTech = (TechKind)s.currentTech,
                researchProgress = s.researchProgress,
                nameCounter = s.nameCounter,
                aiTimer = UnityEngine.Random.Range(0f, 2f)
            };
            foreach (int t in s.techs) civ.techs.Add((TechKind)t);

            civs.Add(civ);
            if (civ.isHuman) human = civ;
        }

        if (human == null && civs.Count > 0) human = civs[0];

        // Carreteras y ferrocarril (antes de calcular las ciudades): cada tramo se dibuja con el aspecto de quien lo construyó
        for (int i = 0; i + 2 < data.roads.Count; i += 3)
        {
            grid.AddRoad(data.roads[i], data.roads[i + 1]);
            CivViews.BuildRoad(data.roads[i], data.roads[i + 1], grid, earth, EarthT, CivById(data.roads[i + 2]) ?? human);
        }
        for (int i = 0; i + 1 < data.rails.Count; i += 2)
        {
            grid.AddRail(data.rails[i], data.rails[i + 1]);
            CivViews.BuildRail(data.rails[i], data.rails[i + 1], grid, earth, EarthT, human);
        }
        yield return null;

        // Ciudades (primero se crean todas, luego se enlazan las rutas comerciales)
        List<City> cityList = new List<City>();
        foreach (CivSaveCity s in data.cities)
        {
            Civ owner = CivById(s.owner);
            if (owner == null) { cityList.Add(null); continue; }

            City city = new City
            {
                owner = owner, name = s.name, cell = s.cell, pop = s.pop, claimDepth = s.claimDepth,
                food = s.food, prodStock = s.prodStock, hp = s.hp, maxHp = s.maxHp, claimTimer = s.claimTimer,
                hasItem = s.hasItem, itemIsUnit = s.itemIsUnit, itemUnit = (UnitKind)s.itemUnit, itemBuilding = (BuildingKind)s.itemBuilding,
                recalcTimer = 3f
            };
            city.cells.AddRange(s.cells);
            city.tiles.AddRange(s.tiles);
            foreach (int b in s.buildings) city.buildings.Add((BuildingKind)b);
            for (int i = 0; i < s.wonderKinds.Count && i < s.wonderCells.Count; i++)
            {
                city.wonderCells[(BuildingKind)s.wonderKinds[i]] = s.wonderCells[i];
                grid.wonderAt[s.wonderCells[i]] = true;
            }

            owner.cities.Add(city);
            allCities.Add(city);
            foreach (int c in city.cells) grid.cityAt[c] = city;
            grid.cityAt[city.cell] = city;
            foreach (int t in city.tiles) grid.tileCity[t] = city;

            owner.territoryDirty = true;
            cityList.Add(city);
        }

        for (int i = 0; i < data.cities.Count; i++)
        {
            City city = cityList[i];
            if (city == null) continue;
            foreach (int p in data.cities[i].tradePartners) if (p >= 0 && p < cityList.Count && cityList[p] != null) city.tradePartners.Add(cityList[p]);
            foreach (int p in data.cities[i].merchantPartners) if (p >= 0 && p < cityList.Count && cityList[p] != null) city.merchantPartners.Add(cityList[p]);
        }

        foreach (City city in cityList)
        {
            if (city == null) continue;
            RecomputeCity(city);
            RebuildCityView(city);
        }
        yield return null;

        // Unidades (los barcos y su carga se enlazan al final)
        List<Unit> unitList = new List<Unit>();
        foreach (CivSaveUnit s in data.units)
        {
            Civ owner = CivById(s.owner);
            if (owner == null) { unitList.Add(null); continue; }

            Unit u = SpawnUnit(owner, (UnitKind)s.kind, s.cell);
            u.hp = Mathf.Min(s.hp, u.def.hp);
            unitList.Add(u);
        }

        for (int i = 0; i < data.units.Count; i++)
        {
            Unit u = unitList[i];
            if (u == null) continue;

            int home = data.units[i].homeCity;
            if (home >= 0 && home < cityList.Count) u.homeCity = cityList[home];

            int ship = data.units[i].carriedBy;
            if (ship >= 0 && ship < unitList.Count && unitList[ship] != null)
            {
                u.carriedBy = unitList[ship];
                unitList[ship].cargo.Add(u);
                if (u.view != null) u.view.SetActive(false);
            }
        }

        gameTime = data.gameTime;
        AddLog("Partida cargada (" + data.savedAt + ")", human != null ? human.color : Color.white);
        if (human != null && human.cities.Count > 0) FocusOn(human.cities[0].cell);

        FreeCamera.ClearViewedCity();
        CivUI ui = FindFirstObjectByType<CivUI>();
        if (ui != null) ui.ClearSelection();

        ready = true;
    }

    Civ CivById(int id)
    {
        foreach (Civ c in civs) if (c.id == id) return c;
        return null;
    }
}
