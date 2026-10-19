using UnityEngine;

/// <summary>
/// Música de fondo según la edad en la que está el jugador. Cada edad tiene su pista (carpeta Resources); al cambiar de
/// edad, la música se funde con la nueva. Si falta algún archivo, esa edad se queda sin música. Se silencia con F2
/// (junto con los efectos de sonido) y se calla mientras suena un vídeo, como la introducción o un cambio de era.
/// Se crea sola al pulsar Play.
/// </summary>
public class EraMusic : MonoBehaviour
{
    // Pista de cada edad: Antigua, Media, Moderna, Contemporánea, Futura (nombres de los archivos en Resources, sin extensión)
    static readonly string[] Tracks =
    {
        "AsiHabloZaratustra",
        "lorenzobuczek-canon-in-d-pachelbel-190929",
        "Race With Life",
        "Ilona Harpaz - Trick or Freak",
        "Cambio a edad futurista"
    };

    const float Volume = 0.3f;
    const float FadeSeconds = 2.5f;

    AudioSource current, fading;
    int currentEra = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Create()
    {
        if (FindFirstObjectByType<EraMusic>() != null) return;

        GameObject go = new GameObject("Música por edad");
        go.AddComponent<EraMusic>();
        DontDestroyOnLoad(go);
    }

    AudioSource NewSource()
    {
        AudioSource s = gameObject.AddComponent<AudioSource>();
        s.playOnAwake = false;
        s.loop = true;
        s.spatialBlend = 0f;
        s.volume = 0f;
        return s;
    }

    void Awake()
    {
        current = NewSource();
        fading = NewSource();
    }

    // Edad del jugador (0 Antigua ... 4 Futura); antes de empezar la partida, la Antigua
    static int EraNow()
    {
        CivGame game = CivGame.Instance;
        if (game == null || !game.ready || game.human == null) return 0;
        return game.EraIndexOf(game.human);
    }

    void Update()
    {
        bool silent = GameSfx.Muted || IntroVideo.Active;

        int era = EraNow();
        if (era != currentEra)
        {
            currentEra = era;

            // La pista que sonaba pasa a apagarse y la nueva entra desde cero
            AudioSource old = fading;
            fading = current;
            current = old;

            AudioClip clip = era >= 0 && era < Tracks.Length ? Resources.Load<AudioClip>(Tracks[era]) : null;
            current.Stop();
            current.clip = clip;
            current.volume = 0f;
            if (clip != null) current.Play();
        }

        float step = Time.unscaledDeltaTime / FadeSeconds * Volume;
        float target = silent ? 0f : Volume;

        if (current.clip != null) current.volume = Mathf.MoveTowards(current.volume, target, step);

        if (fading.isPlaying)
        {
            fading.volume = Mathf.MoveTowards(fading.volume, 0f, step);
            if (fading.volume <= 0.001f) fading.Stop();
        }
    }
}
