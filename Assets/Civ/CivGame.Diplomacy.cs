using System.Collections.Generic;
using UnityEngine;

// ---------------------------------------------------------------------------------------------------------------------
// Diplomacia entre civilizaciones. Cada pareja está en Guerra, Paz o Alianza:
//   Guerra:  las unidades y ciudades se atacan.
//   Paz:     nadie ataca a nadie. Tras la paz hay una tregua en la que no se puede declarar la guerra.
//   Alianza: como la paz, y además las civilizaciones aliadas comparten la visión del mapa.
// Se empieza en Paz con todas. El jugador usa el panel de diplomacia (tecla D); la IA propone la paz si va perdiendo,
// busca aliados contra un enemigo común y a veces declara la guerra al ver que es más fuerte.
// ---------------------------------------------------------------------------------------------------------------------
public enum DiploState { War, Peace, Alliance }

/// <summary>Propuesta de una civilización de la IA dirigida al jugador, a la espera de su respuesta.</summary>
public class DiploProposal
{
    public Civ from;
    public DiploState kind;   // Peace o Alliance
}

public partial class CivGame
{
    const DiploState StartState = DiploState.Peace;
    const float TruceSeconds = 120f;     // segundos de juego tras una paz o alianza antes de poder declarar la guerra
    const float DiploMinWarSeconds = 60f;   // una guerra recién declarada no admite paz de inmediato

    class DiploRelation
    {
        public DiploState state = StartState;
        public float since;   // gameTime en que empezó este estado
    }

    readonly Dictionary<int, DiploRelation> relations = new Dictionary<int, DiploRelation>();

    /// <summary>Propuestas de la IA al jugador que esperan respuesta.</summary>
    public readonly List<DiploProposal> proposals = new List<DiploProposal>();

    static int RelationKey(Civ a, Civ b) { return Mathf.Min(a.id, b.id) * 32 + Mathf.Max(a.id, b.id); }

    DiploRelation RelationOf(Civ a, Civ b)
    {
        DiploRelation r;
        if (!relations.TryGetValue(RelationKey(a, b), out r))
        {
            r = new DiploRelation();
            relations[RelationKey(a, b)] = r;
        }
        return r;
    }

    public DiploState StateBetween(Civ a, Civ b)
    {
        if (a == b) return DiploState.Alliance;
        return RelationOf(a, b).state;
    }

    public bool AtWar(Civ a, Civ b) { return a != b && StateBetween(a, b) == DiploState.War; }
    public bool AreAllies(Civ a, Civ b) { return a != b && StateBetween(a, b) == DiploState.Alliance; }

    /// <summary>Segundos de juego que lleva la relación en su estado actual.</summary>
    float RelationAge(Civ a, Civ b) { return gameTime - RelationOf(a, b).since; }

    void SetState(Civ a, Civ b, DiploState state)
    {
        DiploRelation r = RelationOf(a, b);
        if (r.state == state) return;

        r.state = state;
        r.since = gameTime;

        // Aviso al jugador de lo que pasa entre otras civilizaciones y de lo que le afecta
        if (human == null) return;

        if (a == human || b == human)
        {
            Civ other = a == human ? b : a;
            string text = state == DiploState.War ? "¡Guerra con " + other.name + "!"
                        : state == DiploState.Peace ? "Paz con " + other.name
                        : "Alianza con " + other.name;
            AddLog(text, other.color);
            GameSfx.Play(state == DiploState.War ? Sfx.War : Sfx.Peace);
        }
        else
        {
            string text = state == DiploState.War ? a.name + " y " + b.name + " están en guerra"
                        : state == DiploState.Peace ? a.name + " y " + b.name + " hacen las paces"
                        : a.name + " y " + b.name + " se alían";
            AddLog(text, Color.white);
        }

        // Los objetivos que ya no son enemigos se abandonan
        foreach (Unit u in allUnits)
        {
            if (!u.alive) continue;
            if (u.targetUnit != null && !AtWar(u.owner, u.targetUnit.owner)) u.targetUnit = null;
            if (u.targetCity != null && !AtWar(u.owner, u.targetCity.owner)) u.targetCity = null;
        }

        // La visión compartida de las alianzas cambia el mapa que ve el jugador
        fogMeshStale = true;
    }

    int WarsOf(Civ civ)
    {
        int n = 0;
        foreach (Civ o in civs) if (o.alive && o != civ && AtWar(civ, o)) n++;
        return n;
    }

    bool CommonEnemy(Civ a, Civ b)
    {
        foreach (Civ c in civs)
            if (c.alive && c != a && c != b && AtWar(a, c) && AtWar(b, c)) return true;
        return false;
    }

    // ---------------------------------------------------------------- Acciones del jugador

    /// <summary>El jugador propone la paz a una civilización en guerra con él. Devuelve el mensaje de la respuesta.</summary>
    public string ProposePeace(Civ other)
    {
        if (!AtWar(human, other)) return "Ya no estáis en guerra.";
        if (!AiAcceptsPeace(other, human)) { AddLog(other.name + " rechaza la paz", other.color); return other.name + " rechaza la paz."; }

        SetState(human, other, DiploState.Peace);
        return other.name + " acepta la paz.";
    }

    /// <summary>El jugador propone una alianza a una civilización con la que está en paz.</summary>
    public string ProposeAlliance(Civ other)
    {
        if (StateBetween(human, other) != DiploState.Peace) return "Hace falta estar en paz para proponer una alianza.";
        if (!AiAcceptsAlliance(other, human)) { AddLog(other.name + " rechaza la alianza", other.color); return other.name + " rechaza la alianza."; }

        SetState(human, other, DiploState.Alliance);
        return other.name + " acepta la alianza.";
    }

    /// <summary>El jugador declara la guerra (desde la paz) o rompe una alianza (que pasa a paz).</summary>
    public string DeclareWar(Civ other)
    {
        DiploState state = StateBetween(human, other);

        if (state == DiploState.War) return "Ya estáis en guerra.";

        if (state == DiploState.Alliance)
        {
            SetState(human, other, DiploState.Peace);
            return "Has roto la alianza con " + other.name + ".";
        }

        if (RelationAge(human, other) < TruceSeconds)
            return "Hay una tregua: no puedes declarar la guerra todavía (" + Mathf.CeilToInt(TruceSeconds - RelationAge(human, other)) + " s).";

        SetState(human, other, DiploState.War);
        return "Has declarado la guerra a " + other.name + ".";
    }

    /// <summary>El jugador responde a una propuesta de la IA.</summary>
    public void RespondProposal(DiploProposal p, bool accept)
    {
        proposals.Remove(p);
        if (!accept) { AddLog("Rechazas la propuesta de " + p.from.name, p.from.color); return; }

        // Puede haber cambiado la situación mientras tanto
        if (!p.from.alive) return;
        if (p.kind == DiploState.Peace && AtWar(human, p.from)) SetState(human, p.from, DiploState.Peace);
        else if (p.kind == DiploState.Alliance && StateBetween(human, p.from) == DiploState.Peace) SetState(human, p.from, DiploState.Alliance);
    }

    // ---------------------------------------------------------------- Decisiones de la IA

    // Una civilización de la IA acepta la paz si va perdiendo, está en guerra con varias o, con algo de azar, si no es más fuerte
    bool AiAcceptsPeace(Civ ai, Civ from)
    {
        if (RelationAge(ai, from) < DiploMinWarSeconds) return false;

        // Naciones Unidas: mientras exista esa maravilla, todas las civilizaciones aceptan la paz
        if (WonderExists(BuildingKind.UnitedNations)) return true;

        if (WarsOf(ai) >= 2) return true;

        float ratio = ScoreOf(ai) / Mathf.Max(1f, ScoreOf(from));
        if (ratio < 1.1f) return true;

        return Random.value < (difficulty == Difficulty.Hard ? 0.1f : 0.25f);
    }

    // Acepta una alianza si lleva un tiempo en paz y tienen un enemigo común; si no, solo a veces
    bool AiAcceptsAlliance(Civ ai, Civ from)
    {
        if (StateBetween(ai, from) != DiploState.Peace || RelationAge(ai, from) < 90f) return false;
        if (CommonEnemy(ai, from)) return true;
        return Random.value < 0.15f;
    }

    void AiProposePeace(Civ ai, Civ other)
    {
        if (other.isHuman)
        {
            AiQueueProposal(ai, DiploState.Peace);
            return;
        }

        if (AiAcceptsPeace(other, ai)) SetState(ai, other, DiploState.Peace);
    }

    void AiProposeAlliance(Civ ai, Civ other)
    {
        if (other.isHuman)
        {
            AiQueueProposal(ai, DiploState.Alliance);
            return;
        }

        if (AiAcceptsAlliance(other, ai)) SetState(ai, other, DiploState.Alliance);
    }

    void AiQueueProposal(Civ from, DiploState kind)
    {
        foreach (DiploProposal p in proposals) if (p.from == from) return;   // ya hay una pendiente de esa civilización
        proposals.Add(new DiploProposal { from = from, kind = kind });
        AddLog(from.name + " te propone " + (kind == DiploState.Peace ? "la paz" : "una alianza") + " (D)", from.color);
    }

    float AiWarChance
    {
        get { return difficulty == Difficulty.Easy ? 0.04f : difficulty == Difficulty.Hard ? 0.25f : 0.12f; }
    }

    // Se llama desde AiThink; actúa cada cierto tiempo para no inundar de propuestas
    void AiDiplomacy(Civ civ)
    {
        civ.aiDiploTimer -= AiThinkInterval;
        if (civ.aiDiploTimer > 0f) return;
        civ.aiDiploTimer = Random.Range(25f, 45f);

        // Las propuestas al jugador que llevan mucho sin respuesta caducan
        if (proposals.Count > 4) proposals.RemoveAt(0);

        foreach (Civ other in civs)
        {
            if (!other.alive || other == civ) continue;

            DiploState state = StateBetween(civ, other);
            float age = RelationAge(civ, other);

            if (state == DiploState.War)
            {
                // En guerra: pide la paz si va perdiendo o pelea contra varios
                bool losing = ScoreOf(civ) < ScoreOf(other) * 0.8f;
                if ((losing || WarsOf(civ) >= 2 || civ.aiThreatened) && age > DiploMinWarSeconds) AiProposePeace(civ, other);
            }
            else if (state == DiploState.Peace)
            {
                if (CommonEnemy(civ, other) && age > 90f && Random.value < 0.5f)
                {
                    AiProposeAlliance(civ, other);
                }
                else if (gameTime > AiGraceTime && age > TruceSeconds && WarsOf(civ) == 0
                         && ScoreOf(civ) > ScoreOf(other) * 1.4f && Random.value < AiWarChance)
                {
                    // Más fuerte que su vecino y sin guerras: le declara la guerra
                    SetState(civ, other, DiploState.War);
                    if (other.isHuman) AddLog(civ.name + " te declara la guerra!", civ.color);
                }
            }
        }
    }

    // ---------------------------------------------------------------- Guardar y empezar de nuevo

    void ResetDiplomacy()
    {
        relations.Clear();
        proposals.Clear();
    }

    void ExportDiplomacy(List<int> flat)
    {
        foreach (KeyValuePair<int, DiploRelation> kv in relations)
        {
            flat.Add(kv.Key / 32);
            flat.Add(kv.Key % 32);
            flat.Add((int)kv.Value.state);
        }
    }

    void ImportDiplomacy(List<int> flat)
    {
        ResetDiplomacy();
        for (int i = 0; i + 2 < flat.Count; i += 3)
        {
            DiploRelation r = new DiploRelation { state = (DiploState)Mathf.Clamp(flat[i + 2], 0, 2), since = gameTime };
            relations[Mathf.Min(flat[i], flat[i + 1]) * 32 + Mathf.Max(flat[i], flat[i + 1])] = r;
        }
    }
}
