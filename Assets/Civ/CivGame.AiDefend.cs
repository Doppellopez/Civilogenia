using System.Collections.Generic;
using UnityEngine;

// ---------------------------------------------------------------------------------------------------------------------
// La IA reacciona a las amenazas. Cada ciudad compara las fuerzas enemigas (en guerra) que tiene cerca con las que la
// defienden. Si el peligro es real:
//   - la ciudad amenazada pasa a producir tropas (o murallas) y, si hay oro, las compra al momento;
//   - el ejército libre acude en su ayuda (y, si el peligro es grave, se llama también a los que marchaban al ataque);
//   - colonos y trabajadores cercanos al enemigo se refugian en la ciudad propia más cercana;
//   - mientras haya una ciudad amenazada, no se lanzan ataques ni invasiones, y se pide la paz con más ganas.
// ---------------------------------------------------------------------------------------------------------------------
public partial class CivGame
{
    // Radio (en casillas) en que se considera "cerca" de una ciudad
    float AiThreatRadius { get { return difficulty == Difficulty.Easy ? 3.5f : difficulty == Difficulty.Hard ? 6.5f : 5f; } }

    // Fuerza de una unidad militar: ataque y defensa ponderados por su vida actual
    float PowerOf(Unit u)
    {
        return (u.def.attack * 0.6f + EffectiveDefense(u) * 0.4f) * Mathf.Max(1f, u.hp);
    }

    void AiDefend(Civ civ)
    {
        civ.aiThreatened = false;
        float radius = grid.angularSpacing * AiThreatRadius;

        foreach (City c in civ.cities.ToArray())
        {
            if (!c.alive) continue;

            // Fuerzas enemigas cerca y propias que defienden (las propias a menos de 2 casillas)
            float enemy = 0f, defenders = c.hp * 6f + (c.buildings.Contains(BuildingKind.Walls) ? 200f : 0f);
            foreach (Unit u in allUnits)
            {
                if (!u.alive || !u.def.IsMilitary || u.carriedBy != null) continue;

                float a = grid.AngleBetween(c.cell, u.cell);
                if (u.owner == civ)
                {
                    if (a <= grid.angularSpacing * 2.2f) defenders += PowerOf(u);
                }
                else if (AtWar(civ, u.owner) && a <= radius)
                {
                    enemy += PowerOf(u);
                }
            }

            if (enemy <= 0f) continue;

            civ.aiThreatened = true;
            float ratio = enemy / Mathf.Max(1f, defenders);

            // Peligro real: refuerzos, producción de defensa y compra
            if (ratio > 0.6f)
            {
                AiReinforce(civ, c, enemy - defenders, ratio > 1.5f);
                AiDefenseProduction(civ, c, ratio);
            }
        }

        if (civ.aiThreatened) AiProtectCivilians(civ);
    }

    // La ciudad produce lo que más ayuda: murallas si hay mucho peligro y faltan; si no, la mejor unidad militar
    void AiDefenseProduction(Civ civ, City c, float ratio)
    {
        bool producingDefense = c.hasItem && ((c.itemIsUnit && GameDefs.Units[c.itemUnit].IsMilitary) || (!c.itemIsUnit && c.itemBuilding == BuildingKind.Walls));

        if (!producingDefense)
        {
            if (ratio > 1.5f && Available(c, BuildingKind.Walls)) SetProduction(c, BuildingKind.Walls);
            else SetProduction(c, AiBestMilitary(civ));
        }

        // Comprar lo que se está construyendo si hay oro (en dificultad fácil la IA no compra en emergencias)
        if (difficulty != Difficulty.Easy && c.hasItem && !ItemBlocked(c) && BuyPrice(c) <= civ.gold) Buy(c);
    }

    // Manda soldados a la ciudad amenazada hasta cubrir la diferencia de fuerzas (máximo 6)
    void AiReinforce(Civ civ, City c, float missing, bool severe)
    {
        if (missing <= 0f) return;

        int comp = grid.component[c.cell];
        List<Unit> candidates = new List<Unit>();
        foreach (Unit u in civ.units)
        {
            if (!u.alive || !u.def.IsMilitary || u.def.naval || u.def.air || u.carriedBy != null || u.boardTarget != null) continue;
            if (grid.component[u.cell] != comp) continue;
            if (grid.AngleBetween(u.cell, c.cell) <= grid.angularSpacing * 1.5f) continue;   // ya está en la ciudad

            // Libres siempre; los que marchan al ataque, solo si el peligro es grave
            bool free = AiIdleSoldier(u) || u.fortified || u.fortifying;
            bool attacking = u.HasTarget || u.Moving || u.path.Count > 0;
            if (!free && !(severe && attacking)) continue;

            candidates.Add(u);
        }

        candidates.Sort((a, b) => grid.AngleBetween(a.cell, c.cell).CompareTo(grid.AngleBetween(b.cell, c.cell)));

        float sent = 0f;
        for (int i = 0; i < candidates.Count && i < 6 && sent < missing; i++)
        {
            Unit u = candidates[i];
            u.targetUnit = null;
            u.targetCity = null;
            OrderMove(u, c.cell);
            sent += PowerOf(u);
        }
    }

    // Colonos y trabajadores con enemigos a menos de 2,5 casillas se refugian en la ciudad propia más cercana
    void AiProtectCivilians(Civ civ)
    {
        float danger = grid.angularSpacing * 2.5f;

        foreach (Unit u in civ.units.ToArray())
        {
            if (!u.alive || u.carriedBy != null || (u.kind != UnitKind.Settler && u.kind != UnitKind.Worker)) continue;

            bool inDanger = false;
            foreach (Unit e in allUnits)
            {
                if (!e.alive || !e.def.IsMilitary || e.carriedBy != null || !AtWar(civ, e.owner)) continue;
                if (grid.AngleBetween(u.cell, e.cell) <= danger) { inDanger = true; break; }
            }

            if (!inDanger) continue;

            City refuge = null;
            float best = float.MaxValue;
            int comp = grid.component[u.cell];
            foreach (City c in civ.cities)
            {
                if (!c.alive || grid.component[c.cell] != comp) continue;
                float a = grid.AngleBetween(u.cell, c.cell);
                if (a < best) { best = a; refuge = c; }
            }

            if (refuge != null && grid.AngleBetween(u.cell, refuge.cell) > 0.01f) OrderMove(u, refuge.cell);
        }
    }
}
