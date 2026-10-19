using UnityEngine;

// Sonido de los combates: suena cuando el jugador está implicado o el golpe ocurre a la vista, más suave si es ajeno.
public partial class CivGame
{
    void AttackSound(Unit u)
    {
        if (human == null) return;

        bool involved = u.owner == human
            || (u.targetUnit != null && u.targetUnit.owner == human)
            || (u.targetCity != null && u.targetCity.owner == human);

        if (!involved && !IsVisible(u.cell)) return;

        GameSfx.Play(u.targetCity != null ? Sfx.CityHit : Sfx.Hit, involved ? 1f : 0.45f);
    }
}
