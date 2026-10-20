using System.Collections.Generic;
using UnityEngine;

// ---------------------------------------------------------------------------------------------------------------------
// Atajo de pruebas (Mayús+T): desarrolla de golpe todas las tecnologías de la edad en la que está el jugador.
// La primera vez se desarrollan las normales de la edad; si ya las tiene todas, se desarrolla la tecnología que cierra la
// edad (el Cambio de era, o la Omnisciencia en la Edad Futura), que lleva a la siguiente.
// ---------------------------------------------------------------------------------------------------------------------
public partial class CivGame
{
    // Edad de cada tecnología: normales y la que cierra la edad (Cambio de era; Omnisciencia en la última)
    static readonly TechKind[][] EraTechs =
    {
        // Edad Antigua
        new[] { TechKind.Fire, TechKind.Agriculture, TechKind.Husbandry, TechKind.Mining, TechKind.Sailing, TechKind.Archery,
                TechKind.Pottery, TechKind.Riding, TechKind.Wheel, TechKind.Masonry, TechKind.Bronze, TechKind.Writing,
                TechKind.Construction, TechKind.Iron, TechKind.Astronomy, TechKind.Mathematics, TechKind.Currency,
                TechKind.Philosophy, TechKind.Bridges, TechKind.Crossbow },
        // Edad Media
        new[] { TechKind.HeavyPlow, TechKind.WaterMill, TechKind.Camshaft, TechKind.ArabicNumerals, TechKind.Universities,
                TechKind.Cities, TechKind.Compass, TechKind.Clock, TechKind.PrintingPress, TechKind.Gunpowder },
        // Edad Moderna
        new[] { TechKind.SteamEngine, TechKind.Electricity, TechKind.Railway, TechKind.MovableType, TechKind.Rifle,
                TechKind.Telescope, TechKind.Thermometer, TechKind.Grenade, TechKind.Artillery },
        // Edad Contemporánea
        new[] { TechKind.CombustionEngine, TechKind.Aviation, TechKind.Radio, TechKind.SteelConcrete, TechKind.Penicillin,
                TechKind.Tanks, TechKind.Computers, TechKind.NuclearEnergy },
        // Edad Futura
        new[] { TechKind.SpaceFlight, TechKind.ArtificialIntelligence, TechKind.Robotics, TechKind.FusionEnergy,
                TechKind.QuantumComputing, TechKind.GeneticEngineering, TechKind.Nanotech, TechKind.Antigravity,
                TechKind.PlasmaWeapons, TechKind.EnergyShields, TechKind.SpaceColonization }
    };

    // La tecnología que cierra cada edad (la que lleva a la siguiente)
    static readonly TechKind[] EraClosers =
    {
        TechKind.EraChange, TechKind.EraChange2, TechKind.EraChange3, TechKind.EraChange4, TechKind.Omniscience
    };

    static readonly string[] EraNames = { "Edad Antigua", "Edad Media", "Edad Moderna", "Edad Contemporánea", "Edad Futura" };

    /// <summary>Índice de la edad en la que está la civilización (0 Antigua ... 4 Futura).</summary>
    public int EraIndexOf(Civ civ)
    {
        if (civ.Has(TechKind.EraChange4)) return 4;
        if (civ.Has(TechKind.EraChange3)) return 3;
        if (civ.Has(TechKind.EraChange2)) return 2;
        if (civ.Has(TechKind.EraChange)) return 1;
        return 0;
    }

    /// <summary>
    /// Desarrolla todas las tecnologías de la edad actual, sin pagar ni cumplir requisitos. Devuelve el mensaje para el jugador.
    /// </summary>
    public string DevelopEraTechs(Civ civ)
    {
        int era = EraIndexOf(civ);
        bool eraChanged = false;
        int count = 0;

        foreach (TechKind t in EraTechs[era])
            if (civ.techs.Add(t)) count++;

        // Si ya las tenía todas, se desarrolla la que cierra la edad
        if (count == 0)
        {
            TechKind closer = EraClosers[era];
            if (civ.techs.Add(closer))
            {
                count = 1;
                eraChanged = closer == TechKind.EraChange || closer == TechKind.EraChange2
                          || closer == TechKind.EraChange3 || closer == TechKind.EraChange4;

                if (closer == TechKind.Omniscience && winner == null)
                {
                    winner = civ;
                    winnerByTech = true;
                    AddLog(civ.name + " alcanza la Omnisciencia: ¡victoria por tecnología!", civ.color);
                    IntroVideo.PlayClip(OmniscienceVideo);
                }
            }
        }

        // Si estaba investigando algo que ya tiene, se libera
        if (civ.researching && civ.techs.Contains(civ.currentTech))
        {
            civ.researching = false;
            civ.researchProgress = 0f;
        }

        if (count == 0) return "Ya tienes todas las tecnologías del juego.";

        // Los efectos de las tecnologías nuevas (producción, ciencia, aspecto de ciudades, carreteras y unidades)
        foreach (City c in civ.cities)
        {
            RecomputeCity(c);
            CivViews.BuildCity(c, grid, EarthT);
        }

        if (eraChanged)
        {
            CivViews.UpgradeRoads(civ, grid, earth, EarthT);

            foreach (Unit u in civ.units.ToArray())
            {
                if (!u.alive) continue;
                CivViews.BuildUnit(u, grid, EarthT);
                PlaceUnit(u);
                if (u.carriedBy != null && u.view != null) u.view.SetActive(false);
            }

            return "Has desarrollado el cambio de era: ahora estás en la " + EraNames[EraIndexOf(civ)] + ".";
        }

        return "Has desarrollado " + count + (count == 1 ? " tecnología" : " tecnologías") + " de la " + EraNames[era]
             + (count > 1 ? ". Pulsa Mayús+T otra vez para el cambio de era." : ".");
    }
}
