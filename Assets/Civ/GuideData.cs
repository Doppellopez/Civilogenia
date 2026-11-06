using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

// Datos de la guía del juego (tecnologías, unidades, edificios y maravillas).
// Se construyen al arrancar a partir de GameDefs (nombres, costes, estadísticas y efectos), así que
// cambiar un coste o un efecto en CivTypes.cs se refleja solo en la guía.
// Lo único que se mantiene aquí es qué tecnología exige cada edificio y cada unidad, porque esa
// regla vive en CivGame.Available / HasTechFor: si cambias allí un requisito, cámbialo también aquí.
public static class GuideData
{
    public static readonly string[] Eras = { "Edad Antigua", "Edad Media", "Edad Moderna", "Edad Contemporánea", "Edad Futura" };

    // era, nombre, coste, requisitos, efecto, cierra la edad
    public struct TechRow { public int era; public string name, cost, requires, effect; public bool closer; }
    public struct UnitRow { public string name, type, cost, hp, attack, defense, speed, range, requires, notes; }
    public struct BuildingRow { public string name, cost, effect, requires; public bool wonder; public int era; }

    public static readonly TechRow[] Techs;
    public static readonly UnitRow[] Units;
    public static readonly BuildingRow[] Buildings;

    // Se construyen aquí, después de inicializar las tablas de requisitos de más abajo
    static GuideData()
    {
        Techs = BuildTechs();
        Units = BuildUnits();
        Buildings = BuildBuildings();
    }

    const string FromStart = "Desde el inicio";

    // Los Cambios de era cierran cada edad
    static readonly HashSet<TechKind> EraChanges = new HashSet<TechKind> { TechKind.EraChange, TechKind.EraChange2, TechKind.EraChange3, TechKind.EraChange4 };

    // Maravilla retirada del juego (se conserva el tipo solo para las partidas guardadas)
    const BuildingKind RetiredWonder = BuildingKind.GreatLibrary;

    // ---------------------------------------------------------------- Tecnologías

    static TechRow[] BuildTechs()
    {
        List<TechRow> rows = new List<TechRow>();
        Dictionary<TechKind, int> eraOf = new Dictionary<TechKind, int>();

        foreach (KeyValuePair<TechKind, TechDef> kv in GameDefs.Techs)
        {
            TechDef d = kv.Value;
            bool closer = EraChanges.Contains(kv.Key) || kv.Key == TechKind.Omniscience;
            int era = TechEra(kv.Key);
            eraOf[kv.Key] = era;

            rows.Add(new TechRow
            {
                era = era,
                name = d.name,
                cost = Num(d.cost),
                requires = TechRequires(kv.Key, d, era),
                effect = Cap(d.unlocks),
                closer = closer,
            });
        }

        // Por edad; el Cambio de era (o la Omnisciencia) cierra la lista; dentro, por coste
        return rows.Select((r, i) => new { r, i })
                   .OrderBy(x => x.r.era).ThenBy(x => x.r.closer).ThenBy(x => ParseNum(x.r.cost)).ThenBy(x => x.i)
                   .Select(x => x.r).ToArray();
    }

    // La edad de una tecnología es el número de Cambios de era que hay que haber pasado para llegar a ella
    static int TechEra(TechKind tech)
    {
        HashSet<TechKind> seen = new HashSet<TechKind>();
        Stack<TechKind> pending = new Stack<TechKind>();
        pending.Push(tech);

        int changes = 0;
        while (pending.Count > 0)
        {
            TechKind t = pending.Pop();
            foreach (TechKind pre in Prerequisites(GameDefs.Techs[t]))
            {
                if (!seen.Add(pre)) continue;
                if (EraChanges.Contains(pre)) changes++;
                pending.Push(pre);
            }
        }
        return changes;
    }

    static IEnumerable<TechKind> Prerequisites(TechDef d)
    {
        if (d.requires.HasValue) yield return d.requires.Value;
        if (d.alsoRequires.HasValue) yield return d.alsoRequires.Value;
        if (d.requiresAll != null) foreach (TechKind t in d.requiresAll) yield return t;
    }

    static string TechRequires(TechKind kind, TechDef d, int era)
    {
        if (kind == TechKind.Omniscience) return "Todas las demás tecnologías (" + d.requiresAll.Length + ")";
        if (d.requiresAll != null) return "Las " + d.requiresAll.Length + " tecnologías de la " + Eras[era];

        List<string> names = new List<string>();
        if (d.requires.HasValue) names.Add(GameDefs.Techs[d.requires.Value].name);
        if (d.alsoRequires.HasValue) names.Add(GameDefs.Techs[d.alsoRequires.Value].name);
        return names.Count == 0 ? "Ninguna" : string.Join(" y ", names);
    }

    // ---------------------------------------------------------------- Unidades

    // Tecnologías que exige cada unidad (CivGame.HasTechFor); las que no salen aquí están desde el inicio
    static readonly Dictionary<UnitKind, TechKind[]> UnitTech = new Dictionary<UnitKind, TechKind[]>
    {
        { UnitKind.Spearman,     new[] { TechKind.Mining } },
        { UnitKind.Archer,       new[] { TechKind.Archery } },
        { UnitKind.Horseman,     new[] { TechKind.Riding } },
        { UnitKind.WarElephant,  new[] { TechKind.Riding, TechKind.Bronze } },
        { UnitKind.HussiteWagon, new[] { TechKind.Camshaft } },
        { UnitKind.Chariot,      new[] { TechKind.Wheel } },
        { UnitKind.Phalanx,      new[] { TechKind.Bronze } },
        { UnitKind.Legion,       new[] { TechKind.Iron } },
        { UnitKind.Catapult,     new[] { TechKind.Mathematics } },
        { UnitKind.Sailboat,     new[] { TechKind.Sailing } },
        { UnitKind.Galleon,      new[] { TechKind.Compass } },
        { UnitKind.Musketeer,    new[] { TechKind.Gunpowder } },
        { UnitKind.Crossbowman,  new[] { TechKind.Crossbow } },
        { UnitKind.Ballista,     new[] { TechKind.Crossbow } },
        { UnitKind.Rifleman,     new[] { TechKind.Rifle } },
        { UnitKind.Grenadier,    new[] { TechKind.Grenade } },
        { UnitKind.Cannon,       new[] { TechKind.Artillery } },
        { UnitKind.Steamship,    new[] { TechKind.SteamEngine } },
        { UnitKind.Tank,         new[] { TechKind.Tanks } },
        { UnitKind.Mecha,        new[] { TechKind.PlasmaWeapons } },
        { UnitKind.Fighter,      new[] { TechKind.Aviation } },
        { UnitKind.Bomber,       new[] { TechKind.Aviation } },
        { UnitKind.Caravan,      new[] { TechKind.Currency } },
        { UnitKind.Merchant,     new[] { TechKind.ArabicNumerals } },
    };

    // Notas que no se pueden deducir de las estadísticas
    static readonly Dictionary<UnitKind, string> UnitNotes = new Dictionary<UnitKind, string>
    {
        { UnitKind.HussiteWagon, "Mejora del Carro (y del Jinete) en la Academia; a su vez se mejora a Tanque. Muy defensivo; dispara desde detrás de su blindaje de madera. Se dibuja igual en todas las eras" },
        { UnitKind.WarElephant, "Resistente y fuerte; se dibuja igual en todas las eras" },
        { UnitKind.Merchant, "Sustituye a la Caravana; sus rutas dan el doble. También se obtiene mejorando una Caravana en una Academia" },
    };

    static readonly string[] UnitTypes = { "Civil", "Tierra", "Mar", "Aire" };

    static UnitRow[] BuildUnits()
    {
        List<UnitRow> rows = new List<UnitRow>();
        List<int> order = new List<int>();

        foreach (KeyValuePair<UnitKind, UnitDef> kv in GameDefs.Units)
        {
            UnitDef d = kv.Value;
            string type = d.naval ? "Mar" : d.air ? "Aire" : d.IsMilitary ? "Tierra" : "Civil";

            List<string> notes = new List<string>();
            if (d.popCost > 0) notes.Add("cuesta " + d.popCost + (d.popCost == 1 ? " habitante" : " habitantes"));
            if (d.capacity > 0) notes.Add("transporta " + d.capacity);
            if (UnitNotes.TryGetValue(kv.Key, out string extra)) notes.Add(extra);

            rows.Add(new UnitRow
            {
                name = d.name,
                type = type,
                cost = Num(d.cost),
                hp = Fmt(d.hp),
                attack = d.IsMilitary ? Fmt(d.attack) : "-",
                defense = d.IsMilitary ? Fmt(d.defense) : "-",
                speed = Fmt(d.speed),
                range = d.IsMilitary ? d.range.ToString() : "-",
                requires = UnitRequires(kv.Key),
                notes = string.Join("; ", notes),
            });
            order.Add(System.Array.IndexOf(UnitTypes, type));
        }

        return rows.Select((r, i) => new { r, i })
                   .OrderBy(x => order[x.i]).ThenBy(x => ParseNum(x.r.cost)).ThenBy(x => x.i)
                   .Select(x => x.r).ToArray();
    }

    static string UnitRequires(UnitKind kind)
    {
        if (!UnitTech.TryGetValue(kind, out TechKind[] techs)) return FromStart;
        return string.Join(" y ", techs.Select(t => GameDefs.Techs[t].name));
    }

    // ---------------------------------------------------------------- Edificios y maravillas

    // Tecnología que exige cada edificio (CivGame.Available); los que no salen aquí están desde el inicio
    static readonly Dictionary<BuildingKind, TechKind> BuildingTech = new Dictionary<BuildingKind, TechKind>
    {
        { BuildingKind.Granary, TechKind.Agriculture },
        { BuildingKind.Walls, TechKind.Masonry },
        { BuildingKind.Library, TechKind.Writing },
        { BuildingKind.Observatory, TechKind.Astronomy },
        { BuildingKind.AlexandriaLibrary, TechKind.Universities },
        { BuildingKind.University, TechKind.Universities },
        { BuildingKind.FullingMill, TechKind.Camshaft },
        { BuildingKind.Academy, TechKind.Cities },
        { BuildingKind.GutenbergPress, TechKind.PrintingPress },
        { BuildingKind.GalileoObservatory, TechKind.Telescope },
        { BuildingKind.FahrenheitStudy, TechKind.Thermometer },
        { BuildingKind.Newspaper, TechKind.MovableType },
        { BuildingKind.GutenbergPrint, TechKind.MovableType },
        { BuildingKind.HooverDam, TechKind.Electricity },
        { BuildingKind.HydroDam, TechKind.Electricity },
        { BuildingKind.Factory, TechKind.SteamEngine },
        { BuildingKind.RadioTower, TechKind.Radio },
        { BuildingKind.FordAssembly, TechKind.CombustionEngine },
        { BuildingKind.FlemingLab, TechKind.Penicillin },
        { BuildingKind.ManhattanProject, TechKind.NuclearEnergy },
        { BuildingKind.Internet, TechKind.Computers },
        { BuildingKind.FusionReactor, TechKind.FusionEnergy },
        { BuildingKind.QuantumComputer, TechKind.QuantumComputing },
        { BuildingKind.LunarBase, TechKind.SpaceColonization },
        { BuildingKind.GeneBank, TechKind.GeneticEngineering },
        { BuildingKind.SpaceElevator, TechKind.SpaceColonization },
        { BuildingKind.Pyramids, TechKind.Masonry },
        { BuildingKind.LighthouseAlex, TechKind.Sailing },
        { BuildingKind.GreatWall, TechKind.Construction },
        { BuildingKind.Colosseum, TechKind.Construction },
        { BuildingKind.SistineChapel, TechKind.Universities },
        { BuildingKind.MarcoPolo, TechKind.Compass },
        { BuildingKind.EiffelTower, TechKind.Electricity },
        { BuildingKind.StatueOfLiberty, TechKind.Railway },
        { BuildingKind.Hollywood, TechKind.Computers },
        { BuildingKind.ApolloProgram, TechKind.Aviation },
        { BuildingKind.UnitedNations, TechKind.Radio },
        { BuildingKind.Biosphere, TechKind.GeneticEngineering },
        { BuildingKind.Temple, TechKind.Pottery },
        { BuildingKind.Amphitheater, TechKind.Construction },
        { BuildingKind.Cathedral, TechKind.Universities },
        { BuildingKind.Theater, TechKind.MovableType },
        { BuildingKind.Hospital, TechKind.Penicillin },
        { BuildingKind.Stadium, TechKind.Radio },
        { BuildingKind.Park, TechKind.CombustionEngine },
        { BuildingKind.Mall, TechKind.SteelConcrete },
        { BuildingKind.Cinema, TechKind.Computers },
        { BuildingKind.VRPark, TechKind.ArtificialIntelligence },
        { BuildingKind.OrbitalGarden, TechKind.SpaceColonization },
        { BuildingKind.Satellite, TechKind.SpaceFlight },
        { BuildingKind.ComputerLab, TechKind.Computers },
        { BuildingKind.NuclearPlant, TechKind.NuclearEnergy },
        { BuildingKind.PrintingHouse, TechKind.PrintingPress },
        { BuildingKind.Bank, TechKind.ArabicNumerals },
        { BuildingKind.FibonacciLab, TechKind.ArabicNumerals },
    };

    // Las maravillas empiezan su efecto con "maravilla única", a veces con una condición entre paréntesis
    static readonly Regex WonderPrefix = new Regex(@"^maravilla única(?: \(([^)]*)\))?:\s*");

    static BuildingRow[] BuildBuildings()
    {
        List<BuildingRow> rows = new List<BuildingRow>();
        List<int> techCost = new List<int>();

        foreach (KeyValuePair<BuildingKind, BuildingDef> kv in GameDefs.Buildings)
        {
            if (kv.Key == RetiredWonder) continue;

            BuildingDef d = kv.Value;
            Match m = WonderPrefix.Match(d.effect);
            bool wonder = m.Success;
            string effect = wonder ? Cap(d.effect.Substring(m.Length)) + (m.Groups[1].Success ? " (" + m.Groups[1].Value + ")" : "") : Cap(d.effect);

            bool needsTech = BuildingTech.TryGetValue(kv.Key, out TechKind tech);

            rows.Add(new BuildingRow
            {
                name = d.name,
                cost = Num(d.cost),
                effect = effect,
                requires = needsTech ? GameDefs.Techs[tech].name : FromStart,
                wonder = wonder,
                era = needsTech ? TechEra(tech) : 0,
            });
            techCost.Add(needsTech ? (int)GameDefs.Techs[tech].cost : 0);
        }

        // Edificios y luego maravillas; por edad, por lo pronto que se desbloquean y por coste
        return rows.Select((r, i) => new { r, i })
                   .OrderBy(x => x.r.wonder).ThenBy(x => x.r.era).ThenBy(x => techCost[x.i]).ThenBy(x => ParseNum(x.r.cost)).ThenBy(x => x.i)
                   .Select(x => x.r).ToArray();
    }

    // ---------------------------------------------------------------- Formato

    // 1500 -> "1.500", como en el resto del juego
    static string Num(float v)
    {
        int n = (int)System.Math.Round(v);
        string s = System.Math.Abs(n).ToString(CultureInfo.InvariantCulture);
        for (int i = s.Length - 3; i > 0; i -= 3) s = s.Insert(i, ".");
        return n < 0 ? "-" + s : s;
    }

    static int ParseNum(string s) { return int.Parse(s.Replace(".", ""), CultureInfo.InvariantCulture); }

    // 0.65 -> "0.65", 10 -> "10"
    static string Fmt(float v) { return v.ToString(CultureInfo.InvariantCulture); }

    static string Cap(string s) { return string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1); }
}
