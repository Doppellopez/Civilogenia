using System.Collections.Generic;
using UnityEngine;

// ---------------------------------------------------------------------------------------------------------------------
// Niebla de guerra: el jugador solo ve en torno a sus unidades y ciudades. Lo que está fuera de su visión se oscurece
// (lo ya explorado se ve atenuado, lo desconocido casi negro) y las unidades enemigas fuera de visión no se dibujan.
// Las civilizaciones de la IA ven todo el mapa.
// ---------------------------------------------------------------------------------------------------------------------
public partial class CivGame
{
    [Header("Niebla de guerra")]
    [Tooltip("Con true solo se ve en torno a las unidades y ciudades propias")]
    public bool fogOfWar = true;

    // Radio de visión en casillas
    const int CityVision = 4;
    const int UnitVision = 2;
    const int FarUnitVision = 3;   // barcos y aviones ven más lejos

    [System.NonSerialized] bool[] fogVisible, fogExplored, fogNew;
    [System.NonSerialized] float fogTimer;
    [System.NonSerialized] readonly Dictionary<int, List<int>> visionCache = new Dictionary<int, List<int>>();

    public bool IsVisible(int cell) { return !fogOfWar || fogVisible == null || fogVisible[cell]; }
    public bool IsExplored(int cell) { return !fogOfWar || fogExplored == null || fogExplored[cell]; }

    /// <summary>Olvida lo explorado (al cargar o empezar otra partida).</summary>
    void ResetFog()
    {
        fogVisible = null;
        fogExplored = null;
        fogNew = null;
        fogTimer = 0f;
        visionCache.Clear();
    }

    List<int> VisionAround(int cell, int depth)
    {
        int key = cell * 8 + depth;
        List<int> list;
        if (!visionCache.TryGetValue(key, out list))
        {
            list = grid.Around(cell, depth);
            visionCache[key] = list;
        }
        return list;
    }

    void MarkVision(int cell, int depth)
    {
        foreach (int c in VisionAround(cell, depth)) fogNew[c] = true;
    }

    // Se recalcula unas cuantas veces por segundo (también en pausa, para que se vea al mover la cámara o dar órdenes)
    void TickFog()
    {
        if (!fogOfWar || human == null || grid == null) return;

        fogTimer -= Time.unscaledDeltaTime;
        if (fogTimer > 0f) return;
        fogTimer = 0.3f;

        int n = grid.count;
        if (fogVisible == null || fogVisible.Length != n)
        {
            fogVisible = new bool[n];
            fogExplored = new bool[n];
            fogNew = new bool[n];
            visionCache.Clear();
        }

        System.Array.Clear(fogNew, 0, n);

        // Omnisciencia: visión total, sin niebla
        if (human.Has(TechKind.Omniscience))
            for (int i = 0; i < n; i++) fogNew[i] = true;

        foreach (City c in human.cities)
            if (c.alive) MarkVision(c.cell, CityVision);

        foreach (Unit u in human.units)
        {
            if (!u.alive) continue;

            int depth = (u.def.naval || u.def.air) ? FarUnitVision : UnitVision;
            MarkVision(u.cell, depth);
            if (u.Moving) MarkVision(u.moveTo, depth);
        }

        // Satélite (se construye con el Vuelo espacial): cartografía todo el planeta (queda explorado, aunque no visible)
        bool satellites = OwnsBuilding(human, BuildingKind.Satellite);

        bool changed = false;
        for (int i = 0; i < n; i++)
        {
            if (fogNew[i] != fogVisible[i]) changed = true;
            if (fogNew[i]) fogExplored[i] = true;
            if (satellites && !fogExplored[i]) { fogExplored[i] = true; changed = true; }
        }

        bool[] swap = fogVisible; fogVisible = fogNew; fogNew = swap;

        if (changed || fogMeshStale)
        {
            CivViews.RebuildFog(fogVisible, fogExplored, grid, earth, EarthT);
            fogMeshStale = false;
        }

        // Unidades enemigas solo se ven dentro de la visión; las ciudades enemigas, una vez exploradas
        foreach (Unit u in allUnits)
        {
            if (!u.alive || u.view == null || u.owner == human) continue;
            u.view.SetActive(u.carriedBy == null && fogVisible[u.cell]);
        }

        foreach (City c in allCities)
        {
            if (!c.alive || c.view == null || c.owner == human) continue;
            c.view.SetActive(fogExplored[c.cell]);
        }
    }

    bool fogMeshStale = true;
}
