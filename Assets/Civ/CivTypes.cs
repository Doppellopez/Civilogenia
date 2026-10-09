using System.Collections.Generic;
using UnityEngine;

// Tipos y datos del juego de civilizaciones en tiempo real.

public enum UnitKind { Settler, Worker, Warrior, Spearman, Sailboat, Caravan, Archer, Horseman, Chariot, Phalanx, Legion, Catapult, Merchant }
public enum TechKind { Fire, Agriculture, Husbandry, Mining, Sailing, Archery, Pottery, Riding, Wheel, Masonry, Bronze, Writing, Construction, Iron, Astronomy, Mathematics, Currency, Philosophy, EraChange, HeavyPlow, WaterMill, Camshaft, ArabicNumerals, Universities, Cities, Bridges }
public enum BuildingKind{ Granary, Workshop, Market, Library, Walls, PublicBaths, GreatLibrary, Observatory, FibonacciLab, Bank, Academy, FullingMill, University, AlexandriaLibrary }

public class UnitDef
{
    public string name;
    public float cost, hp, attack, defense, speed; // speed en casillas por segundo
    public int popCost;
    public bool naval; // navega solo por mar y se construye en ciudades costeras
    public int capacity; // unidades de tierra que puede transportar
    public int range = 1; // casillas de distancia desde las que ataca (1 = cuerpo a cuerpo)
    public bool IsMilitary => attack > 0f;
}

public class TechDef
{
    public string name, unlocks;
    public float cost;
    public TechKind? requires;     // tecnología previa necesaria
    public TechKind? alsoRequires; // segunda tecnología previa, si hacen falta dos
}

public class BuildingDef
{
    public string name, effect;
    public float cost;
}

/// <summary>Datos fijos del juego: costes, estadísticas y rendimiento del terreno.</summary>
public static class GameDefs
{
    // Población máxima de una ciudad que aún no ha descubierto el Fuego
    public const int PopWithoutFire = 3;

    // Tecnologías: se investigan con ciencia; "requires" es la tecnología previa necesaria
    public static readonly Dictionary<TechKind, TechDef> Techs = new Dictionary<TechKind, TechDef>
    {
        { TechKind.Fire,        new TechDef { name = "Fuego",       cost = 15, unlocks = "las ciudades crecen a más de " + PopWithoutFire } },
        { TechKind.Agriculture, new TechDef { name = "Agricultura", cost = 25, unlocks = "irrigaciones y Granero", requires = TechKind.Fire } },
        { TechKind.Philosophy,  new TechDef { name = "Filosofía",   cost = 70, unlocks = "+50% de investigación y Cambio de era", requires = TechKind.Astronomy, alsoRequires = TechKind.Mathematics } },
        { TechKind.EraChange,   new TechDef { name = "Cambio de era", cost = 500, unlocks = "una nueva era", requires = TechKind.Philosophy } },
        // Tecnologías de la Edad Media: solo se pueden investigar tras el Cambio de era
        { TechKind.HeavyPlow,   new TechDef { name = "Arado y collera", cost = 80, unlocks = "Granja (alimento x2)", requires = TechKind.EraChange } },
        { TechKind.WaterMill,   new TechDef { name = "Molino hidráulico", cost = 85, unlocks = "+1 comida en casillas junto al mar o a un río", requires = TechKind.EraChange } },
        { TechKind.Camshaft,   new TechDef { name = "Árbol de levas", cost = 95, unlocks = "+1 producción en casillas junto al mar o a un río y Batán", requires = TechKind.WaterMill } },
        { TechKind.ArabicNumerals, new TechDef { name = "Cifras arábigas", cost = 90, unlocks = "+25% de ciencia y de oro en todas las ciudades , Banco, Mercader y Laboratorio de Fibonacci", requires = TechKind.EraChange } },
        { TechKind.Cities,     new TechDef { name = "Ciudades", cost = 100, unlocks = "+25% de crecimiento en todas las ciudades y Academia", requires = TechKind.HeavyPlow } },
        { TechKind.Universities, new TechDef { name = "Universidades", cost = 130, unlocks = "+25% de ciencia en todas las ciudades, Universidad y Gran Biblioteca de Alejandría", requires = TechKind.ArabicNumerals } },
        { TechKind.Currency, new TechDef { name = "Moneda",      cost = 40, unlocks = "Caravana de comercio", requires = TechKind.Bronze, alsoRequires = TechKind.Writing } },
        { TechKind.Mathematics,new TechDef { name = "Matemáticas", cost = 40, unlocks = "Catapulta", requires = TechKind.Writing, alsoRequires = TechKind.Wheel } },
        { TechKind.Astronomy,  new TechDef { name = "Astronomía",  cost = 55, unlocks = "Observatorio", requires = TechKind.Writing } },
        { TechKind.Iron,       new TechDef { name = "Hierro",      cost = 45, unlocks = "Legión", requires = TechKind.Bronze } },
        { TechKind.Construction,new TechDef { name = "Construcción", cost = 50, unlocks = "edificios al 90% de su coste", requires = TechKind.Wheel, alsoRequires = TechKind.Masonry } },
        { TechKind.Writing,    new TechDef { name = "Escritura",   cost = 45, unlocks = "Biblioteca y Gran Biblioteca", requires = TechKind.Pottery } },
        { TechKind.Bronze,     new TechDef { name = "Bronce",      cost = 35, unlocks = "Falange", requires = TechKind.Mining } },
        { TechKind.Bridges,    new TechDef { name = "Construcción de puentes", cost = 40, unlocks = "carreteras que atraviesan ríos", requires = TechKind.Masonry } },
        { TechKind.Masonry,   new TechDef { name = "Mampostería", cost = 35, unlocks = "Muro de la Ciudad", requires = TechKind.Mining } },
        { TechKind.Wheel,      new TechDef { name = "Rueda",       cost = 35, unlocks = "caminos y Carro", requires = TechKind.Husbandry } },
        { TechKind.Riding,     new TechDef { name = "Equitación",  cost = 35, unlocks = "Jinete", requires = TechKind.Husbandry } },
        { TechKind.Pottery,    new TechDef { name = "Cerámica",    cost = 35, unlocks = "crecimiento de población x1,5 en todas las ciudades", requires = TechKind.Agriculture } },
        { TechKind.Husbandry,  new TechDef { name = "Ganadería",   cost = 25, unlocks = "+1 comida en llanuras", requires = TechKind.Fire } },
        { TechKind.Mining,      new TechDef { name = "Minería",     cost = 25, unlocks = "minas (producción x2) y Lancero", requires = TechKind.Fire } },
        { TechKind.Sailing,     new TechDef { name = "Vela",        cost = 25, unlocks = "Barco de Vela en ciudades costeras", requires = TechKind.Fire } },
        { TechKind.Archery,     new TechDef { name = "Tiro con arco", cost = 25, unlocks = "Arquero", requires = TechKind.Fire } },
    };

    public static readonly Dictionary<UnitKind, UnitDef> Units = new Dictionary<UnitKind, UnitDef>
    {
        { UnitKind.Settler,  new UnitDef { name = "Colono",  cost = 70, hp = 10, attack = 0, defense = 0, speed = 0.6f,  popCost = 1 } },
        { UnitKind.Worker,   new UnitDef { name = "Trabajador", cost = 30, hp = 10, attack = 0, defense = 0, speed = 0.6f } },
        { UnitKind.Warrior,  new UnitDef { name = "Guerrero", cost = 40, hp = 20, attack = 6, defense = 3, speed = 0.7f } },
        { UnitKind.Spearman, new UnitDef { name = "Lancero", cost = 60, hp = 30, attack = 9, defense = 5, speed = 0.65f } },
        { UnitKind.Sailboat, new UnitDef { name = "Barco de Vela", cost = 80, hp = 25, attack = 0, defense = 2, speed = 0.3f, naval = true, capacity = 1 } },
        { UnitKind.Caravan,  new UnitDef { name = "Caravana de comercio", cost = 50, hp = 10, attack = 0, defense = 0, speed = 0.5f } },
        { UnitKind.Merchant, new UnitDef { name = "Mercader", cost = 90, hp = 15, attack = 0, defense = 0, speed = 0.7f } }, // solo se obtiene mejorando una caravana en una Academia
        { UnitKind.Catapult, new UnitDef { name = "Catapulta", cost = 100, hp = 18, attack = 14, defense = 1, speed = 0.4f, range = 3 } },
        { UnitKind.Legion,   new UnitDef { name = "Legión", cost = 110, hp = 45, attack = 12, defense = 9, speed = 0.6f } },
        { UnitKind.Phalanx,  new UnitDef { name = "Falange", cost = 80, hp = 35, attack = 7, defense = 8, speed = 0.55f } },
        { UnitKind.Chariot,  new UnitDef { name = "Carro", cost = 90, hp = 28, attack = 10, defense = 4, speed = 0.9f } },
        { UnitKind.Horseman, new UnitDef { name = "Jinete", cost = 70, hp = 22, attack = 8, defense = 3, speed = 1.1f } },
        { UnitKind.Archer,   new UnitDef { name = "Arquero", cost = 50, hp = 15, attack = 7, defense = 2, speed = 0.7f, range = 2 } },    };

    public static readonly Dictionary<BuildingKind, BuildingDef> Buildings = new Dictionary<BuildingKind, BuildingDef>
    {
        { BuildingKind.Granary,  new BuildingDef { name = "Granero",    cost = 60, effect = "crecimiento al 150%" } },
        { BuildingKind.Workshop, new BuildingDef { name = "Taller",     cost = 80, effect = "+2 producción" } },
        { BuildingKind.Market,   new BuildingDef { name = "Mercado",    cost = 70, effect = "+2 oro" } },
        { BuildingKind.Library,  new BuildingDef { name = "Biblioteca", cost = 80, effect = "dobla la ciencia de la ciudad" } },
        { BuildingKind.Walls,    new BuildingDef { name = "Muro de la Ciudad",   cost = 60, effect = "+25 vida, defensa" } },
        { BuildingKind.Observatory, new BuildingDef { name = "Observatorio", cost = 90, effect = "+50% de ciencia de la ciudad" } },
        { BuildingKind.GreatLibrary,new BuildingDef { name = "Gran Biblioteca", cost = 200, effect = "maravilla única: +50% de ciencia en todas tus ciudades" } },
        { BuildingKind.FibonacciLab,new BuildingDef { name = "Laboratorio de Fibonacci", cost = 250, effect = "maravilla única: +25% de ciencia en todas tus ciudades" } },
        { BuildingKind.Bank,     new BuildingDef { name = "Banco",      cost = 120, effect = "+50% de oro de la ciudad" } },
        { BuildingKind.Academy,  new BuildingDef { name = "Academia",   cost = 110, effect = "permite mejorar una unidad a su versión actual" } },
        { BuildingKind.FullingMill, new BuildingDef { name = "Batán",       cost = 100, effect = "+3 producción" } },
        { BuildingKind.University, new BuildingDef { name = "Universidad", cost = 150, effect = "+75% de ciencia de la ciudad" } },
        { BuildingKind.AlexandriaLibrary, new BuildingDef { name = "Gran Biblioteca de Alejandría", cost = 300, effect = "maravilla única: +40% de ciencia en todas tus ciudades" } },
        { BuildingKind.PublicBaths, new BuildingDef { name = "Baños públicos", cost = 70, effect = "+50% velocidad de crecimiento" } },
    };

    // Comida, producción, oro por casilla
    public static Vector3 Yield(Biome b)
    {
        switch (b)
        {
            case Biome.Sea:      return new Vector3(1, 0, 2);
            case Biome.Plains:   return new Vector3(2, 1, 0);
            case Biome.Forest:   return new Vector3(1, 2, 0);
            case Biome.Desert:   return new Vector3(0, 1, 1);
            case Biome.Tundra:   return new Vector3(1, 0, 0);
            case Biome.Hills:    return new Vector3(0, 2, 0);
            default:             return new Vector3(0, 1, 0); // montaña
        }
    }

    // Multiplicador de velocidad al entrar en una casilla
    public static float MoveFactor(Biome b)
    {
        switch (b)
        {
            case Biome.Forest:   return 0.7f;
            case Biome.Hills:    return 0.6f;
            case Biome.Tundra:   return 0.8f;
            case Biome.Mountain: return 0.4f;
            case Biome.Sea:      return 0f; // intransitable
            default:             return 1f;
        }
    }

    public static readonly string[] CityNames =
    {
        "Alfa", "Bruma", "Cumbre", "Delta", "Eco", "Faro", "Gaia", "Horizonte",
        "Iris", "Jade", "Kairos", "Luna", "Mirador", "Nube", "Oasis", "Puerto"
    };

    public static readonly string[] CivNames =
    {
        "Aurelia", "Borealis", "Cirene", "Dorada", "Etérea", "Fénix"
    };

    public static readonly Color[] CivColors =
    {
        new Color(0.2f, 0.55f, 1f),
        new Color(0.95f, 0.25f, 0.25f),
        new Color(0.3f, 0.85f, 0.35f),
        new Color(0.95f, 0.8f, 0.2f),
        new Color(0.75f, 0.35f, 0.95f),
        new Color(1f, 0.55f, 0.15f)
    };
}

public class Civ
{
    public int id;
    public string name;
    public Color color;
    public bool isHuman;
    public bool alive = true;

    public float gold = 30f;
    public float goldRate, scienceRate;

    public List<City> cities = new List<City>();

    // Tecnologías descubiertas y la que se está investigando ahora (con ciencia)
    public HashSet<TechKind> techs = new HashSet<TechKind>();
    public bool researching;
    public TechKind currentTech;
    public float researchProgress;

    public bool Has(TechKind t) => techs.Contains(t);

    public bool CanResearch(TechKind t)
    {
        TechDef def = GameDefs.Techs[t];
        return !Has(t)
            && (!def.requires.HasValue || Has(def.requires.Value))
            && (!def.alsoRequires.HasValue || Has(def.alsoRequires.Value));
    }

    public List<Unit> units = new List<Unit>();

    public int nameCounter;
    public float aiTimer;
    public GameObject territoryObject;
    public Mesh territoryMesh;
    public bool territoryDirty = true;

}

public class City
{
    public Civ owner;
    public string name;
    public int cell;
    public List<int> cells = new List<int>(); // celdas ocupadas: la central y una más por cada 10 de población
    public int pop = 1;

    public float food, prodStock;
    public float foodRate, prodRate, goldRate, scienceRate;
    public float hp, maxHp;
    public float shotCooldown;

    // Producción actual
    public bool hasItem;
    public bool itemIsUnit;
    public UnitKind itemUnit;
    public BuildingKind itemBuilding;

    public HashSet<BuildingKind> buildings = new HashSet<BuildingKind>();
    public Dictionary<BuildingKind, int> wonderCells = new Dictionary<BuildingKind, int>(); // celdilla libre de la ciudad donde está cada maravilla
    public HashSet<City> tradePartners = new HashSet<City>(); // ciudades con las que hay ruta comercial (+15 % oro y ciencia por cada una)
    public HashSet<City> merchantPartners = new HashSet<City>(); // las rutas de tradePartners creadas por un mercader (+20 % en vez de +15 %)
    public List<int> tiles = new List<int>();
    public HashSet<int> worked = new HashSet<int>(); // casillas que trabaja ahora (las mejores, tantas como habitantes)

    public float recalcTimer;

    // Expansión del territorio: radio al que debe llegar (crece con la población) y reloj para reclamar la siguiente casilla
    public int claimDepth = 2;
    public float claimTimer;
    public GameObject view;
    public GameObject selectionRing;
    public bool alive = true;

    // Excedente real de comida por segundo (el Granero lo deja al 150 %)
    public float FoodSurplus
    {
        get
        {
            float surplus = foodRate - 2f * pop;
            if (surplus <= 0f) return surplus;

            if (buildings.Contains(BuildingKind.Granary)) surplus *= 1.5f;
            if (buildings.Contains(BuildingKind.PublicBaths)) surplus *= 1.5f;
            if (owner.Has(TechKind.Pottery)) surplus *= 1.5f; // Cerámica: crecimiento x1,5 en todas las ciudades
            if (owner.Has(TechKind.Cities)) surplus *= 1.25f; // Ciudades: crecimiento x1,25 en todas las ciudades
            return surplus;
        }
    }

    public float ItemCost
    {
        get
        {
            if (!hasItem) return 0f;
            return itemIsUnit ? GameDefs.Units[itemUnit].cost : BuildingCost(itemBuilding);
        }
    }

    /// <summary>Coste de un edificio en esta ciudad: la Construcción lo deja al 90 %.</summary>
    public float BuildingCost(BuildingKind kind)
    {
        float cost = GameDefs.Buildings[kind].cost;
        return owner.Has(TechKind.Construction) ? Mathf.Round(cost * 0.9f) : cost;
    }

    public string ItemName
    {
        get
        {
            if (!hasItem) return "—";
            return itemIsUnit ? GameDefs.Units[itemUnit].name : GameDefs.Buildings[itemBuilding].name;
        }
    }
}

public class Unit
{
    public Civ owner;
    public UnitKind kind;
    public UnitDef def;

    public int cell;                 // casilla actual (o de origen si se está moviendo)
    public int moveTo = -1;          // casilla de destino de la arista actual
    public float t;                  // progreso 0..1 en la arista actual
    public List<int> path = new List<int>();

    public float hp;
    public float attackCooldown;
    public float scanTimer;
    public float repathTimer;
    public int lastTargetCell = -1;

    public Unit targetUnit;
    public City targetCity;
    public int foundAt = -1;         // casilla donde fundar una ciudad al llegar
    public int joinAt = -1;          // casilla de una ciudad propia a la que unirse al llegar (+1 de población)

    // Transporte marítimo: el barco lleva a "cargo"; la unidad embarcada tiene "carriedBy" y no se ve ni actúa
    public Unit cargo;
    public Unit carriedBy;
    public Unit boardTarget;         // barco al que va a embarcar
    public int disembarkAt = -1;     // casilla de tierra donde el barco desembarcará su carga

    // Caravana: ciudad donde se construyó (un extremo de la ruta comercial)
    public City homeCity;

    // Construcción de carreteras (trabajadores): construye desde donde está hasta la casilla roadGoal (-1 = ninguna)
    public int roadGoal = -1;
    public float roadProgress;

    // Irrigación (trabajadores): casilla a irrigar y progreso 0..1 del trabajo
    public int irrigateAt = -1;
    public float irrigateProgress;
    public int mineAt = -1;          // casilla de monte o montaña donde construir una mina
    public float mineProgress;
    public int farmAt = -1;          // casilla donde construir una granja
    public float farmProgress;

    public Vector2 jitter;          // pequeño desplazamiento para que no se solapen
    public Vector3 currentDir;       // dirección actual sobre la esfera (para selección)
    public Vector3 heading;          // hacia dónde mira (solo barcos); se conserva al detenerse

    public GameObject view;
    public GameObject selectionRing;
    public bool alive = true;

    public bool IsTrader => kind == UnitKind.Caravan || kind == UnitKind.Merchant; // puede establecer rutas comerciales

    public bool HasTarget => (targetUnit != null && targetUnit.alive) || (targetCity != null && targetCity.alive);
    public bool Moving => moveTo >= 0;
}
