using System.Collections.Generic;
using UnityEngine;

// ---------------------------------------------------------------------------------------------------------------------
// La IA cruza el mar: usa barcos de transporte para colonizar otras islas y para invadirlas.
//
//  - Colonizar: los colonos que no encuentran sitio libre en su isla esperan un barco; el barco los lleva a la costa de
//    otra isla con un buen sitio y allí fundan una ciudad (AiSendSettler).
//  - Invadir: si una civilización no tiene enemigos en su isla, embarca soldados y los desembarca junto a una ciudad
//    enemiga de otra isla; en tierra atacan esa ciudad (AiMilitary). Desde las islas colonizadas se repite el proceso.
//
// Cada operación la lleva un barco: aiInvade (ciudad objetivo) o aiColonize + aiTargetCell (sitio donde fundar),
// aiPassengers (los que embarcan) y aiSailing (false = cargando en el puerto, true = navegando hacia la costa).
// ---------------------------------------------------------------------------------------------------------------------
public partial class CivGame
{
    const float AiLoadTimeout = 60f;      // segundos de juego que espera a que suban los pasajeros
    const float AiVoyageTimeout = 400f;   // segundos de juego máximos para llegar y desembarcar
    const float AiScanInterval = 12f;     // cada cuánto busca sitios para colonizar (recorre todo el mapa)

    static bool IsTransport(Unit u) { return u.alive && u.def.naval && u.def.capacity > 0; }

    // Soldado de tierra libre: parado, sin órdenes ni objetivo y que no va a embarcar
    static bool AiIdleSoldier(Unit u)
    {
        return u.alive && u.def.IsMilitary && !u.def.naval && !u.def.air && !u.Moving && u.path.Count == 0
            && !u.HasTarget && u.carriedBy == null && u.boardTarget == null;
    }

    // Una ciudad se puede invadir por mar si junto a ella hay tierra libre que toque el agua
    bool AiCoastal(City t)
    {
        foreach (int nb in grid.nbr[t.cell])
        {
            if (!grid.land[nb] || grid.cityAt[nb] != null) continue;
            foreach (int sea in grid.nbr[nb]) if (!grid.land[sea]) return true;
        }
        return false;
    }

    bool TouchesSea(int cell)
    {
        foreach (int nb in grid.nbr[cell]) if (!grid.land[nb]) return true;
        return false;
    }

    static void AiNavalReset(Unit ship)
    {
        ship.aiInvade = null;
        ship.aiColonize = false;
        ship.aiTargetCell = -1;
        ship.aiSailing = false;
        ship.aiPassengers = null;
        ship.aiOpTimer = 0f;
    }

    // Mejor barco de transporte que puede construir la ciudad (más capacidad primero)
    UnitKind? AiBestTransport(City c)
    {
        UnitKind[] ships = { UnitKind.Steamship, UnitKind.Galleon, UnitKind.Sailboat };
        foreach (UnitKind k in ships) if (Available(c, k)) return k;
        return null;
    }

    // Puerto propio más cercano (con costa) a una casilla de tierra y en su misma masa de tierra
    City AiPortNear(Civ civ, int cell)
    {
        int comp = grid.component[cell];
        City port = null;
        float best = float.MaxValue;
        foreach (City p in civ.cities)
        {
            if (!p.alive || grid.component[p.cell] != comp || SeaCellNextTo(p) < 0) continue;
            float a = grid.AngleBetween(cell, p.cell);
            if (a < best) { best = a; port = p; }
        }
        return port;
    }

    // Mejor sitio para una ciudad nueva en otra isla: costero, con buen terreno y no muy lejos del puerto
    int AiPickColonySite(Civ civ, City port)
    {
        int comp = grid.component[port.cell];

        List<int> candidates = new List<int>();
        for (int c = 0; c < grid.count; c++)
        {
            if (!grid.land[c] || grid.component[c] < 0 || grid.component[c] == comp) continue;
            if (!TouchesSea(c) || !CanFound(civ, c)) continue;
            candidates.Add(c);
        }

        if (candidates.Count == 0) return -1;

        candidates.Sort((a, b) => grid.AngleBetween(port.cell, a).CompareTo(grid.AngleBetween(port.cell, b)));

        int best = -1;
        float bestScore = float.MinValue;
        for (int i = 0; i < Mathf.Min(12, candidates.Count); i++)
        {
            int c = candidates[i];
            float score = 0f;
            foreach (int t in grid.Around(c, 2))
            {
                Vector3 y = grid.TileYield(t);
                float v = y.x * 1.2f + y.y + y.z * 0.5f;
                if (grid.tileCity[t] != null) v *= 0.2f;
                score += v;
            }

            score -= grid.AngleBetween(port.cell, c) / grid.angularSpacing * 0.25f;
            if (score > bestScore) { bestScore = score; best = c; }
        }

        return best;
    }

    void AiNaval(Civ civ)
    {
        // 1) Continuar las operaciones en curso y buscar un barco libre
        int ships = 0;
        Unit idleShip = null;
        foreach (Unit s in civ.units.ToArray())
        {
            if (!IsTransport(s)) continue;
            ships++;

            if (s.AiBusy) { AiNavalContinue(civ, s); continue; }

            if (!s.Moving && s.path.Count == 0 && s.cargo.Count == 0 && s.disembarkAt < 0 && s.carriedBy == null)
                if (idleShip == null || s.def.capacity > idleShip.def.capacity) idleShip = s;
        }

        civ.aiNavalTimer -= AiThinkInterval;

        // 2) Colonizar: colonos que no tienen sitio en su isla esperan un barco
        Unit settler = null;
        foreach (Unit u in civ.units)
            if (u.alive && u.kind == UnitKind.Settler && u.aiWaitBoat && !u.Moving && u.path.Count == 0 && u.carriedBy == null && u.boardTarget == null)
            { settler = u; break; }

        if (settler != null && civ.cities.Count < AiMaxCities)
        {
            City port = AiPortNear(civ, settler.cell);
            if (port == null) { settler.aiWaitBoat = false; return; }   // su isla no tiene puerto propio

            if (idleShip == null) { civ.aiWantsTransport = ships < 2; return; }

            if (civ.aiNavalTimer > 0f) return;
            civ.aiNavalTimer = AiScanInterval;

            int site = AiPickColonySite(civ, port);
            if (site < 0) return;   // no queda sitio libre al alcance: el colono seguirá esperando

            if (AiStartOperation(civ, idleShip, port, null, site, new List<Unit> { settler })) return;
        }

        // 3) Invadir (no antes del periodo de gracia): puerto propio y ciudad enemiga costera de otra isla
        //    (si hay enemigos por tierra en su isla, la guerra es terrestre)
        if (gameTime <= AiGraceTime || civ.aiThreatened) return;   // con una ciudad amenazada no se invade

        City invPort = null, target = null;
        float best = float.MaxValue;

        foreach (City p in civ.cities)
        {
            if (!p.alive || SeaCellNextTo(p) < 0) continue;

            int comp = grid.component[p.cell];
            if (comp < 0) continue;

            bool landWar = false;
            foreach (City e in allCities)
                if (e.alive && AtWar(civ, e.owner) && grid.component[e.cell] == comp) { landWar = true; break; }
            if (landWar) continue;

            foreach (City e in allCities)
            {
                if (!e.alive || e.owner == civ || !AtWar(civ, e.owner) || !AiCoastal(e)) continue;

                float a = grid.AngleBetween(p.cell, e.cell);
                if (a < best) { best = a; invPort = p; target = e; }
            }
        }

        if (invPort == null) { if (settler == null) civ.aiWantsTransport = false; return; }

        // Ejército disponible en la isla del puerto (se dejan tantos de guarnición como ciudades)
        int portComp = grid.component[invPort.cell];
        List<Unit> army = new List<Unit>();
        foreach (Unit u in civ.units)
            if (AiIdleSoldier(u) && grid.component[u.cell] == portComp) army.Add(u);

        if (army.Count < civ.cities.Count + 2) { if (settler == null) civ.aiWantsTransport = false; return; }

        if (idleShip == null) { civ.aiWantsTransport = ships < 2; return; }

        if (settler == null) civ.aiWantsTransport = false;

        army.Sort((a, b) => grid.AngleBetween(a.cell, invPort.cell).CompareTo(grid.AngleBetween(b.cell, invPort.cell)));
        int k = Mathf.Min(idleShip.def.capacity, army.Count - civ.cities.Count);
        AiStartOperation(civ, idleShip, invPort, target, -1, army.GetRange(0, Mathf.Max(0, k)));
    }

    // El barco va al puerto y los pasajeros van a embarcar. target = ciudad que invadir, o site = casilla donde colonizar
    bool AiStartOperation(Civ civ, Unit ship, City port, City target, int site, List<Unit> passengers)
    {
        if (passengers.Count == 0) return false;

        int sea = SeaCellNextTo(port);
        OrderMove(ship, sea);
        if (ship.cell != sea && ship.path.Count == 0) return false;   // no puede llegar al puerto

        ship.aiPassengers = new List<Unit>();
        foreach (Unit p in passengers)
            if (OrderBoard(p, ship)) ship.aiPassengers.Add(p);

        if (ship.aiPassengers.Count == 0) { AiNavalReset(ship); return false; }

        ship.aiInvade = target;
        ship.aiColonize = target == null;
        ship.aiTargetCell = site;
        ship.aiSailing = false;
        ship.aiOpTimer = 0f;
        return true;
    }

    // Cargando en el puerto, navegando hacia la costa o volviendo a casa si algo falla
    void AiNavalContinue(Civ civ, Unit ship)
    {
        bool colonize = ship.aiColonize;
        City t = ship.aiInvade;

        // La operación ya no tiene sentido: la ciudad cayó o cambió de dueño, o el sitio de la colonia ya no es válido
        if (!colonize && (t == null || !t.alive || t.owner == civ || !AtWar(civ, t.owner))) { AiNavalFinish(civ, ship); return; }
        if (colonize && (ship.aiTargetCell < 0 || !CanFound(civ, ship.aiTargetCell))) { AiNavalFinish(civ, ship); return; }

        ship.aiOpTimer += AiThinkInterval;

        if (!ship.aiSailing)
        {
            // Cargando: espera a que suban los asignados (o a que se agote el tiempo)
            int pending = 0;
            if (ship.aiPassengers != null)
                foreach (Unit p in ship.aiPassengers) if (p.alive && p.carriedBy == null && p.boardTarget == ship) pending++;

            if (pending > 0 && ship.aiOpTimer < AiLoadTimeout) return;

            if (ship.cargo.Count == 0) { AiNavalReset(ship); return; }

            bool ok = false;
            if (colonize)
            {
                ok = OrderDisembark(ship, ship.aiTargetCell);
            }
            else
            {
                // Tierra libre junto a la ciudad objetivo
                foreach (int nb in grid.nbr[t.cell])
                {
                    if (!grid.land[nb] || grid.cityAt[nb] != null) continue;
                    if (OrderDisembark(ship, nb)) { ok = true; break; }
                }
            }

            if (!ok) { AiNavalFinish(civ, ship); return; }

            ship.aiSailing = true;
            ship.aiOpTimer = 0f;
            if (!colonize) Notify(t.owner, civ.name + " envía una flota hacia " + t.name + "!", civ.color);
            return;
        }

        // Navegando: la operación acaba cuando la carga ha desembarcado
        if (ship.cargo.Count == 0) { AiNavalReset(ship); return; }

        bool stuck = !ship.Moving && ship.path.Count == 0 && ship.disembarkAt < 0;
        if (stuck || ship.aiOpTimer > AiVoyageTimeout) AiNavalFinish(civ, ship);
    }

    // Termina la operación; si el barco aún lleva gente, la desembarca en la ciudad propia con costa más cercana
    void AiNavalFinish(Civ civ, Unit ship)
    {
        City home = null;
        float best = float.MaxValue;
        foreach (City c in civ.cities)
        {
            if (!c.alive || SeaCellNextTo(c) < 0) continue;
            float a = grid.AngleBetween(ship.cell, c.cell);
            if (a < best) { best = a; home = c; }
        }

        AiNavalReset(ship);
        if (home != null && ship.cargo.Count > 0) OrderDisembark(ship, home.cell);
    }
}
