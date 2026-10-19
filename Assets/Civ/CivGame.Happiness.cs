using UnityEngine;

// ---------------------------------------------------------------------------------------------------------------------
// Felicidad de las ciudades: crecer sin límite tiene un coste. Cada ciudad suma y resta puntos de felicidad:
//   + 6 de base
//   - 0,5 por habitante
//   - 0,4 por cada ciudad que el imperio tenga por encima de 4
//   - 1 por cada guerra en curso (hasta 3)
//   + edificios: Baños públicos +3, Biblioteca, Universidad, Mercado, Banco y Periódico +1 cada uno, maravillas +3
//   + tecnologías: Filosofía, Termómetro, Penicilina, Radio y Ordenadores +1 cada una
//   + 0,5 por cada soldado de guarnición (hasta 3)
// Según el total, la ciudad está: Feliz (4 o más), Contenta (0 a 4), Descontenta (-3 a 0) o en Disturbios (menos de -3).
//   Feliz:       +10 % de producción, ciencia, oro y crecimiento
//   Descontenta: -25 % de producción, ciencia y oro, y crece a la mitad
//   Disturbios:  no produce nada ni crece
// ---------------------------------------------------------------------------------------------------------------------
public partial class CivGame
{
    public const int MoodRiot = 0, MoodUnhappy = 1, MoodContent = 2, MoodHappy = 3;

    public static int MoodFor(float happiness)
    {
        if (happiness >= 4f) return MoodHappy;
        if (happiness >= 0f) return MoodContent;
        if (happiness >= -3f) return MoodUnhappy;
        return MoodRiot;
    }

    public static string MoodName(int mood)
    {
        switch (mood)
        {
            case MoodHappy:   return "<color=#8fe08f>Feliz</color>";
            case MoodContent: return "Contenta";
            case MoodUnhappy: return "<color=#ffb04a>Descontenta</color>";
            default:          return "<color=#ff6a5a>Disturbios</color>";
        }
    }

    static float HappinessOfBuilding(BuildingKind b)
    {
        switch (b)
        {
            case BuildingKind.PublicBaths: return 3f;
            case BuildingKind.Temple:
            case BuildingKind.Park:          return 2f;
            case BuildingKind.Amphitheater:
            case BuildingKind.Theater:
            case BuildingKind.Hospital:
            case BuildingKind.Mall:         return 3f;
            case BuildingKind.Cathedral:    return 4f;
            case BuildingKind.Stadium:
            case BuildingKind.Cinema:       return 5f;
            case BuildingKind.VRPark:       return 6f;
            case BuildingKind.OrbitalGarden: return 8f;
            case BuildingKind.Library:
            case BuildingKind.University:
            case BuildingKind.Market:
            case BuildingKind.Bank:
            case BuildingKind.Newspaper:   return 1f;
            default:                       return IsWonder(b) ? 3f : 0f;
        }
    }

    public float HappinessOf(City c)
    {
        float h = 6f - c.pop * 0.5f;

        int cities = c.owner.cities.Count;
        if (cities > 4) h -= (cities - 4) * 0.4f;

        if (!WonderExists(BuildingKind.UnitedNations)) h -= Mathf.Min(3, WarsOf(c.owner));   // cansancio de guerra (las Naciones Unidas lo eliminan)

        foreach (BuildingKind b in c.buildings) h += HappinessOfBuilding(b);

        Civ o = c.owner;
        if (o.Has(TechKind.Philosophy)) h += 1f;
        if (o.Has(TechKind.Thermometer)) h += 1f;
        if (o.Has(TechKind.Penicillin)) h += 1f;
        if (o.Has(TechKind.Radio)) h += 1f;
        if (o.Has(TechKind.Computers)) h += 1f;

        // Maravillas que dan felicidad a todas las ciudades del dueño
        if (OwnsBuilding(o, BuildingKind.Colosseum)) h += 2f;
        if (OwnsBuilding(o, BuildingKind.SistineChapel)) h += 3f;
        if (OwnsBuilding(o, BuildingKind.EiffelTower)) h += 1f;
        if (OwnsBuilding(o, BuildingKind.StatueOfLiberty)) h += 1f;
        if (OwnsBuilding(o, BuildingKind.Hollywood)) h += 3f;
        if (OwnsBuilding(o, BuildingKind.Biosphere)) h += 2f;

        // Guarnición: soldados propios en la ciudad
        int garrison = 0;
        foreach (Unit u in allUnits)
            if (u.alive && u.owner == o && u.def.IsMilitary && !u.def.naval && !u.def.air && u.cell == c.cell) garrison++;
        h += Mathf.Min(garrison, 3) * 0.5f;

        return h;
    }

    // Se llama desde RecomputeCity: calcula la felicidad, avisa al jugador de los cambios y devuelve el multiplicador de producción
    float ApplyHappiness(City city)
    {
        float happiness = HappinessOf(city);
        int mood = MoodFor(happiness);
        int before = city.mood;

        city.happiness = happiness;
        city.mood = mood;

        if (city.owner.isHuman && mood != before)
        {
            if (mood == MoodRiot) GameSfx.Play(Sfx.Alert);
            if (mood == MoodRiot) AddLog("¡Disturbios en " + city.name + "! No produce nada. Construye Baños públicos o guarnece la ciudad.", Color.red);
            else if (mood == MoodUnhappy && before > MoodUnhappy) AddLog(city.name + " está descontenta (producción -25%).", new Color(1f, 0.7f, 0.3f));
            else if (mood >= MoodContent && before <= MoodUnhappy) AddLog(city.name + " recupera la calma.", Color.white);
        }

        return mood == MoodHappy ? 1.1f : mood == MoodUnhappy ? 0.75f : mood == MoodRiot ? 0f : 1f;
    }

    /// <summary>El edificio de felicidad disponible que más felicidad da por coste (null si no hay ninguno).</summary>
    BuildingKind? BestHappinessBuilding(City c)
    {
        BuildingKind? best = null;
        float bestValue = 0f;

        foreach (BuildingKind b in GameDefs.Buildings.Keys)
        {
            float h = HappinessOfBuilding(b);
            if (h <= 0f || IsWonder(b) || !Available(c, b)) continue;

            float value = h / c.BuildingCost(b);
            if (value > bestValue) { bestValue = value; best = b; }
        }

        return best;
    }

    // Multiplicador del crecimiento según el ánimo de la ciudad
    static float MoodGrowthFactor(int mood)
    {
        return mood == MoodHappy ? 1.1f : mood == MoodUnhappy ? 0.5f : mood == MoodRiot ? 0f : 1f;
    }
}
