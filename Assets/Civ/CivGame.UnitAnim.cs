using UnityEngine;

// ---------------------------------------------------------------------------------------------------------------------
// Animación de las unidades. Los modelos son piezas sueltas sin esqueleto, así que se anima el modelo entero con un pequeño
// movimiento procedural encima de la posición y la rotación que fija PlaceUnit: pasos al caminar, vaivén de los barcos,
// vuelo de los aviones, embestida al atacar, golpes de los trabajadores y respiración en reposo.
// ---------------------------------------------------------------------------------------------------------------------
public partial class CivGame
{
    // El ataque pone attackCooldown a 1 (segundos de juego); la embestida dura el primer tramo de esa cuenta atrás
    const float AttackAnimSeconds = 0.1f;

    static bool IsWheeled(UnitKind kind)
    {
        return kind == UnitKind.Chariot || kind == UnitKind.Catapult || kind == UnitKind.Ballista || kind == UnitKind.Cannon
            || kind == UnitKind.Tank || kind == UnitKind.HussiteWagon || kind == UnitKind.Caravan;   // el mercader va a pie, no lleva ruedas
    }

    bool IsWorking(Unit u)
    {
        return u.kind == UnitKind.Worker && !u.Moving && (u.irrigateAt >= 0 || u.mineAt >= 0 || u.farmAt >= 0 || u.roadGoal >= 0);
    }

    /// <summary>Añade el movimiento de animación a la posición y rotación que PlaceUnit acaba de fijar.</summary>
    void AnimateUnit(Unit u, Vector3 up)
    {
        Transform tr = u.view.transform;
        UnitDef def = u.def;
        float size = grid.spacing * 0.35f;
        float time = Time.time + u.id * 0.37f;   // cada unidad con su propio compás

        float lift = 0f;   // elevación sobre el suelo
        float roll = 0f;   // balanceo a los lados (grados)
        float pitch = 0f;  // cabeceo; positivo = hacia delante (grados)
        float push = 0f;   // avance (o retroceso) a lo largo del frente

        if (def.air)
        {
            lift = Mathf.Sin(time * 2f) * size * 0.05f;
            if (u.Moving)
            {
                roll = Mathf.Sin(time * 1.5f) * 6f;
                pitch = 6f;
            }
        }
        else if (def.naval)
        {
            float k = u.Moving ? 1f : 0.6f;
            lift = Mathf.Sin(time * 1.6f) * size * 0.04f * k;
            roll = Mathf.Sin(time * 1.3f) * 4f * k;
            pitch = Mathf.Sin(time * 1.1f + 1f) * 3f * k;
        }
        else if (u.Moving)
        {
            // Paso: cada vez más rápido cuanto más veloz es la unidad; las pesadas y las de ruedas apenas saltan
            float s = Mathf.Sin(time * (4f + 10f * def.speed));
            bool wheeled = IsWheeled(u.kind);
            float weight = def.hp >= 80f ? 0.5f : 1f;
            float hop = wheeled ? 0.02f : u.kind == UnitKind.Horseman ? 0.12f : 0.09f;

            lift = Mathf.Abs(s) * size * hop * weight;
            roll = s * (wheeled ? 0.8f : u.kind == UnitKind.WarElephant ? 5f : 3.5f) * weight;
            pitch = wheeled ? 0f : u.kind == UnitKind.Horseman ? 3f + s * 3f : 3f;
        }
        else if (IsWorking(u))
        {
            // El trabajador golpea con la herramienta
            pitch = Mathf.Abs(Mathf.Sin(time * 5f)) * 14f;
        }
        else if (!u.fortified && !u.fortifying)
        {
            lift = Mathf.Sin(time * 2f) * size * 0.012f;   // respiración
        }

        // Ataque: el cuerpo a cuerpo embiste hacia el objetivo y el de distancia retrocede por el disparo
        if (def.IsMilitary && u.attackCooldown > 1f - AttackAnimSeconds && u.attackCooldown <= 1f)
        {
            float k = Mathf.Sin((1f - u.attackCooldown) / AttackAnimSeconds * Mathf.PI);
            if (def.range <= 1) { push = size * 0.35f * k; pitch += 10f * k; }
            else { push = -size * 0.12f * k; pitch -= 4f * k; }
        }

        Quaternion baseRot = tr.localRotation;
        tr.localPosition += up * lift + baseRot * Vector3.forward * push;
        tr.localRotation = baseRot * Quaternion.Euler(pitch, 0f, roll);

        SpinParts(u);
    }

    const float FanSpeed = 1500f;   // grados por segundo de las hélices

    // Las ruedas giran según lo que avanza la unidad (con la velocidad del juego) y las hélices giran siempre
    void SpinParts(Unit u)
    {
        if (u.spinners == null || u.spinners.Count == 0 || u.spinFrame == Time.frameCount) return;
        u.spinFrame = Time.frameCount;

        float travel = u.Moving ? u.def.speed * gameSpeed * SimulationScale * grid.spacing * Time.deltaTime : 0f;   // distancia recorrida este fotograma

        foreach (UnitSpinner sp in u.spinners)
        {
            if (sp.part == null) continue;

            if (sp.wheel) sp.angle += travel / sp.radius * Mathf.Rad2Deg;
            else sp.angle += FanSpeed * Time.deltaTime;

            sp.part.localRotation = sp.baseRot * Quaternion.AngleAxis(sp.angle, sp.axis);
        }
    }
}
