using UnityEngine;
using UnityEngine.Video;

/// <summary>
/// Reproduce el vídeo de introducción (el neandertal que enciende el fuego con dos piedras, con el título 3D de
/// Civilogenia y "Así habló Zaratustra") al pulsar Play, a pantalla completa y por encima de la interfaz.
/// El vídeo debe estar en una carpeta Resources (Assets/Resources/civilogenia_neandertal_fuego_cine.mp4).
/// Enter, Intro del teclado numérico o clic con el ratón lo saltan. Mientras suena, la partida queda en pausa.
/// Escribe en la consola (prefijo [Civilogenia] Intro) qué hace y por qué termina.
/// </summary>
public class IntroVideo : MonoBehaviour
{
    const string ClipName = "Intro del Civilogenia";
    const float PrepareTimeout = 10f;
    const float IgnoreInputSeconds = 0.6f;   // un clic residual al pulsar Play no debe saltar la intro

    /// <summary>Con false no se reproduce la introducción al empezar (el resto del juego no se entera). Ponlo a true para volver a activarla.</summary>
    public static bool Enabled = true;

    /// <summary>true mientras la introducción está en pantalla (la partida y la música del juego esperan).</summary>
    public static bool Active { get; private set; }

    /// <summary>Segundos de música que ya han sonado dentro del vídeo, para que la música del juego siga desde ahí.</summary>
    public static float PlayedSeconds { get; private set; }

    // Mientras suena la intro todo el juego queda congelado (partida, rotación de la Tierra, órbitas, cámara)
    static bool pausedByIntro;
    static float previousTimeScale = 1f;

    static void PauseWorld(bool pause)
    {
        if (pause == pausedByIntro) return;
        pausedByIntro = pause;

        if (pause)
        {
            previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;
        }
        else
        {
            Time.timeScale = previousTimeScale;
        }
    }

    VideoPlayer player;
    RenderTexture target;
    bool finished;
    float startTime;
    float waited;
    float playStartTime = -1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Create()
    {
        Active = false;
        PlayedSeconds = 0f;
        pausedByIntro = false;   // por si una ejecución anterior en el editor dejó el estado a medias

        if (!Enabled) return;

        // Si la escena ya trae un objeto con este componente, se reutiliza en vez de crear otro
        IntroVideo existing = FindFirstObjectByType<IntroVideo>();

        VideoClip clip = Resources.Load<VideoClip>(ClipName);
        if (clip == null)
        {
            Debug.LogWarning("[Civilogenia] Intro: no se encuentra el vídeo '" + ClipName + "' en Resources; se omite la introducción.");
            return;
        }

        Debug.Log("[Civilogenia] Intro: vídeo cargado (" + clip.width + "x" + clip.height + ", " + clip.length.ToString("0.0") + " s, " + clip.audioTrackCount + " pista(s) de audio).");

        Active = true;
        PauseWorld(true);

        if (existing != null)
        {
            existing.Begin(clip);
            return;
        }

        GameObject go = new GameObject("Intro (vídeo)");
        go.AddComponent<IntroVideo>().Begin(clip);
    }

    /// <summary>
    /// Reproduce a pantalla completa un vídeo de la carpeta Resources (nombre sin extensión) en cualquier momento de la
    /// partida, con el juego congelado mientras suena. Enter o clic lo saltan. No hace nada si ya hay uno sonando.
    /// </summary>
    public static void PlayClip(string clipName)
    {
        if (Active) return;

        VideoClip clip = Resources.Load<VideoClip>(clipName);
        if (clip == null)
        {
            Debug.LogWarning("[Civilogenia] Vídeo: no se encuentra '" + clipName + "' en Resources.");
            return;
        }

        Debug.Log("[Civilogenia] Vídeo: " + clipName + " (" + clip.width + "x" + clip.height + ", " + clip.length.ToString("0.0") + " s).");

        Active = true;
        PauseWorld(true);
        new GameObject("Vídeo (" + clipName + ")").AddComponent<IntroVideo>().Begin(clip);
    }

    void Begin(VideoClip clip)
    {
        startTime = Time.realtimeSinceStartup;

        target = new RenderTexture((int)clip.width, (int)clip.height, 0);

        player = gameObject.AddComponent<VideoPlayer>();
        player.playOnAwake = false;
        player.isLooping = false;
        player.skipOnDrop = true;
        player.clip = clip;
        player.renderMode = VideoRenderMode.RenderTexture;
        player.targetTexture = target;
        player.audioOutputMode = clip.audioTrackCount > 0 ? VideoAudioOutputMode.Direct : VideoAudioOutputMode.None;

        player.prepareCompleted += _ =>
        {
            for (ushort track = 0; track < player.audioTrackCount; track++)
                player.SetDirectAudioVolume(track, 0.8f);
            playStartTime = Time.realtimeSinceStartup;
            Debug.Log("[Civilogenia] Intro: reproduciendo.");
            player.Play();
        };

        player.loopPointReached += _ =>
        {
            // Algunos vídeos con marcas de tiempo raras avisan del final nada más empezar: se ignora ese aviso
            float played = playStartTime < 0f ? 0f : Time.realtimeSinceStartup - playStartTime;
            if (played < player.clip.length * 0.8f)
            {
                Debug.LogWarning("[Civilogenia] Intro: fin de vídeo anunciado a los " + played.ToString("0.0") + " s; se ignora.");
                return;
            }
            Finish("fin del vídeo");
        };

        player.errorReceived += (_, message) =>
        {
            Debug.LogWarning("[Civilogenia] Intro: error del reproductor: " + message);
            Finish("error del reproductor");
        };

        player.Prepare();
    }

    void Update()
    {
        if (finished || player == null) return;

        // Tiempo de espera acumulado fotograma a fotograma, con cada fotograma limitado a 0,1 s: la carga pesada de la partida
        // (generar el planeta) bloquea el hilo principal unos segundos y no debe contar como "el vídeo no se preparó"
        waited += Mathf.Min(Time.unscaledDeltaTime, 0.1f);
        float sinceStart = waited;

        // Terminado por tiempo (por si el aviso de fin no llega)
        if (playStartTime >= 0f && Time.realtimeSinceStartup - playStartTime >= player.clip.length + 0.5f)
        {
            Finish("duración cumplida");
            return;
        }

        if (!player.isPrepared && sinceStart > PrepareTimeout)
        {
            Finish("el vídeo no llegó a prepararse en " + PrepareTimeout + " s");
            return;
        }

        bool skip = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetMouseButtonDown(0);
        if (skip && sinceStart > IgnoreInputSeconds) Finish("saltado por el jugador");
    }

    void OnGUI()
    {
        if (finished) return;

        GUI.depth = -10000; // por encima de toda la interfaz del juego

        // Otros scripts (la interfaz de la partida) escalan GUI.matrix y GUI.color y no los restauran: se parte de cero
        // para que el negro y el vídeo ocupen la pantalla entera y no un trozo reducido
        GUI.matrix = Matrix4x4.identity;
        GUI.color = Color.white;

        // Negro puro: textura blanca teñida de negro (no depende del color ni la transparencia de Texture2D.blackTexture)
        GUI.color = Color.black;
        GUI.DrawTexture(new Rect(-4, -4, Screen.width + 8, Screen.height + 8), Texture2D.whiteTexture);
        GUI.color = Color.white;

        if (player != null && player.isPlaying && target != null)
        {
            // El vídeo se ajusta a la pantalla sin deformarse; se calcula su rectángulo para tapar de negro todo lo demás
            float videoAspect = (float)target.width / target.height;
            float h = Screen.height;
            float w = h * videoAspect;
            if (w > Screen.width) { w = Screen.width; h = w / videoAspect; }

            Rect video = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);
            GUI.DrawTexture(video, target, ScaleMode.StretchToFill, false);

            // Franjas negras a los lados y arriba/abajo, por encima del vídeo, con margen para no dejar ni un píxel
            GUI.color = Color.black;
            GUI.DrawTexture(new Rect(-4, -4, video.x + 4, Screen.height + 8), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(video.xMax, -4, Screen.width - video.xMax + 4, Screen.height + 8), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(-4, -4, Screen.width + 8, video.y + 4), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(-4, video.yMax, Screen.width + 8, Screen.height - video.yMax + 4), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        GUIStyle hint = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.LowerCenter, fontSize = 16 };
        hint.normal.textColor = new Color(1f, 1f, 1f, 0.55f);
        GUI.Label(new Rect(0, 0, Screen.width, Screen.height - 14), "Enter o clic: saltar", hint);
    }

    void Finish(string reason)
    {
        if (finished) return;
        finished = true;
        Active = false;   // lo primero: pase lo que pase después, la partida se reanuda
        PauseWorld(false);
        Debug.Log("[Civilogenia] Intro: terminada (" + reason + ").");

        try
        {
            if (player != null)
            {
                if (player.isPlaying) PlayedSeconds = (float)player.time;           // saltado a mitad
                else PlayedSeconds = player.isPrepared ? (float)player.clip.length : 0f; // terminado, o no llegó a cargar
                player.Stop();
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[Civilogenia] Intro: al cerrar: " + e.Message);
        }

        if (target != null) { target.Release(); Destroy(target); }
        Destroy(gameObject);
    }

    void OnDestroy()
    {
        Active = false;   // seguridad: si el objeto desaparece por cualquier motivo, la partida no se queda congelada
        PauseWorld(false);
    }
}
