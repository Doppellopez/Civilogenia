using System.Collections.Generic;
using UnityEngine;

// ---------------------------------------------------------------------------------------------------------------------
// Fin de la partida y puntuaciones: la pantalla final lista las civilizaciones, encabezada por la que ha ganado.
// ---------------------------------------------------------------------------------------------------------------------
public partial class CivGame
{
    /// <summary>El jugador ha decidido salir: antes de cerrar el juego se muestra la pantalla de fin de partida.</summary>
    public bool abandoned;

    /// <summary>La partida ha terminado: hay una civilización ganadora, el jugador ha sido eliminado o ha decidido salir.</summary>
    public bool GameOver
    {
        get { return winner != null || abandoned || (human != null && !human.alive); }
    }

    /// <summary>
    /// Puntuación de una civilización: 100 por ciudad, 20 por habitante, 40 por tecnología, 15 por edificio,
    /// 150 más por cada maravilla, 5 por unidad y 1 por cada 20 de oro.
    /// </summary>
    public int ScoreOf(Civ civ)
    {
        float score = civ.techs.Count * 40f + Mathf.Floor(civ.gold / 20f);

        foreach (City c in civ.cities)
        {
            if (!c.alive) continue;

            score += 100f + c.pop * 20f;
            foreach (BuildingKind b in c.buildings) score += IsWonder(b) ? 165f : 15f;
        }

        foreach (Unit u in civ.units) if (u.alive) score += 5f;

        return Mathf.RoundToInt(score);
    }

    /// <summary>Civilizaciones ordenadas para la pantalla final: primero la ganadora y después las demás por puntuación.</summary>
    public List<Civ> Ranking()
    {
        List<Civ> list = new List<Civ>(civs);
        list.Sort((a, b) =>
        {
            if (a == b) return 0;
            if (a == winner) return -1;
            if (b == winner) return 1;
            return ScoreOf(b).CompareTo(ScoreOf(a));
        });
        return list;
    }
}
