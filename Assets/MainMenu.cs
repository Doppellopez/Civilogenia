using UnityEngine;

/// <summary>
/// Pantalla de menú que aparece al terminar el vídeo de introducción (o nada más empezar si no hay intro):
/// Jugar, Controles y Salir. Mientras está abierta la partida queda congelada y la interfaz del juego oculta.
/// Enter equivale a Jugar. Se crea sola al pulsar Play.
/// </summary>
public class MainMenu : MonoBehaviour
{
    /// <summary>Con false no se muestra el menú al empezar (el juego arranca directamente tras la introducción).</summary>
    public static bool Enabled = true;

    /// <summary>true mientras el menú está en pantalla (la partida, la cámara y la interfaz del juego esperan).</summary>
    public static bool Active { get; private set; }

    const float IgnoreInputSeconds = 0.5f;   // el Enter o el clic que saltan la intro no deben pulsar un botón del menú

    bool shown;
    bool showControls;
    bool loading;
    float openTime;
    float previousTimeScale = 1f;

    GUIStyle title, subtitle, button, body, hint;
    float styleScale = -1f;

    static readonly string[] Controls =
    {
        "Clic izquierdo: seleccionar unidad o ciudad",
        "Clic derecho: mover o atacar  (Mayús: mover y fundar ciudad)",
        "B: fundar ciudad con el colono seleccionado",
        "R: construir carretera    V: mejorar a ferrocarril    F: granja",
        "T: tecnologías    H: ayuda    C: centrar la cámara",
        "Espacio: pausa    1 2 3 4: velocidad x1 x2 x4 x8",
        "Cursores o , . : mover la cámara    Rueda: zoom    Doble clic: seguir un punto",
        "F5: guardar partida    F9: cargar partida",
        "Esc: salir del juego"
    };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Create()
    {
        Active = false;
        if (!Enabled || FindFirstObjectByType<MainMenu>() != null) return;

        GameObject go = new GameObject("Menú principal");
        go.AddComponent<MainMenu>();
    }

    void Update()
    {
        // La introducción tiene prioridad: el menú sale en cuanto termina (o enseguida si no hay vídeo)
        if (!shown && !IntroVideo.Active)
        {
            shown = true;
            Open();
        }

        if (!Active || loading || Time.unscaledTime - openTime < IgnoreInputSeconds) return;

        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) Play();
    }

    void Open()
    {
        Active = true;
        showControls = false;
        openTime = Time.unscaledTime;
        previousTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
        Time.timeScale = 0f;
        Debug.Log("[Civilogenia] Menú principal abierto.");
    }

    // Cargar partida: espera a que el mundo esté listo, carga la partida guardada y empieza a jugar
    System.Collections.IEnumerator LoadRoutine()
    {
        loading = true;
        yield return null;
        yield return null;   // deja pintar el aviso "Cargando partida…" antes de que el trabajo bloquee el hilo principal

        CivGame game = CivGame.Instance;
        while (game == null || !game.ready) { yield return null; game = CivGame.Instance; }

        yield return StartCoroutine(game.LoadRoutine());
        loading = false;
        Play();
    }

    void Play()
    {
        if (!Active || loading) return;
        Active = false;
        Time.timeScale = previousTimeScale;
        Debug.Log("[Civilogenia] Menú principal: a jugar.");
    }

    static void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void OnDestroy()
    {
        // Seguridad: si el objeto desaparece con el menú abierto, la partida no se queda congelada
        if (Active) Time.timeScale = previousTimeScale;
        Active = false;
    }

    void BuildStyles(float k)
    {
        title = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = Mathf.RoundToInt(86 * k), fontStyle = FontStyle.Bold };
        title.normal.textColor = new Color(1f, 0.95f, 0.82f);

        subtitle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = Mathf.RoundToInt(24 * k), fontStyle = FontStyle.Italic };
        subtitle.normal.textColor = new Color(0.9f, 0.82f, 0.64f);

        button = new GUIStyle(GUI.skin.button) { fontSize = Mathf.RoundToInt(26 * k), fontStyle = FontStyle.Bold };

        body = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = Mathf.RoundToInt(21 * k) };
        body.normal.textColor = Color.white;

        hint = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.LowerCenter, fontSize = Mathf.RoundToInt(16 * k) };
        hint.normal.textColor = new Color(1f, 1f, 1f, 0.5f);

        styleScale = k;
    }

    void OnGUI()
    {
        if (!Active) return;

        GUI.depth = -9000;               // por encima de la interfaz del juego (la intro va aún por encima)
        GUI.matrix = Matrix4x4.identity; // otros scripts escalan la matriz y el color y no los restauran
        GUI.color = Color.white;

        float k = Screen.height / 720f;
        if (!Mathf.Approximately(k, styleScale)) BuildStyles(k);

        // Velo oscuro sobre la Tierra
        GUI.color = new Color(0f, 0f, 0f, 0.6f);
        GUI.DrawTexture(new Rect(-4, -4, Screen.width + 8, Screen.height + 8), Texture2D.whiteTexture);
        GUI.color = Color.white;

        float cx = Screen.width * 0.5f;

        // Título con sombra
        Rect titleRect = new Rect(0, Screen.height * 0.12f, Screen.width, 110f * k);
        Color shadow = title.normal.textColor;
        title.normal.textColor = new Color(0f, 0f, 0f, 0.7f);
        GUI.Label(new Rect(titleRect.x + 3f * k, titleRect.y + 3f * k, titleRect.width, titleRect.height), "CIVILOGENIA", title);
        title.normal.textColor = shadow;
        GUI.Label(titleRect, "CIVILOGENIA", title);
        GUI.Label(new Rect(0, titleRect.yMax - 10f * k, Screen.width, 40f * k), "De la Edad de Piedra a las estrellas", subtitle);

        bool wasEnabled = GUI.enabled;
        GUI.enabled = !loading && Time.unscaledTime - openTime >= IgnoreInputSeconds;

        float bw = 340f * k, bh = 58f * k, gap = 16f * k;
        float y = Screen.height * 0.43f;

        if (!showControls)
        {
            if (GUI.Button(new Rect(cx - bw / 2f, y, bw, bh), "Jugar", button)) Play();
            y += bh + gap;

            // Cargar partida: solo si hay una guardada (F5 en la partida guarda)
            bool canLoad = CivSave.Exists;
            bool enabledBefore = GUI.enabled;
            GUI.enabled = enabledBefore && canLoad;
            if (GUI.Button(new Rect(cx - bw / 2f, y, bw, bh), canLoad ? "Cargar partida" : "Cargar partida (no hay ninguna)", button))
                StartCoroutine(LoadRoutine());
            GUI.enabled = enabledBefore;
            y += bh + gap;
            if (GUI.Button(new Rect(cx - bw / 2f, y, bw, bh), "Controles", button)) showControls = true;
            y += bh + gap;
            if (GUI.Button(new Rect(cx - bw / 2f, y, bw, bh), "Salir", button)) Quit();
        }
        else
        {
            float lineH = 40f * k;
            for (int i = 0; i < Controls.Length; i++)
                GUI.Label(new Rect(0, y + i * lineH - 40f * k, Screen.width, lineH), Controls[i], body);

            y += Controls.Length * lineH - 20f * k;
            if (GUI.Button(new Rect(cx - bw / 2f, y, bw, bh), "Volver", button)) showControls = false;
        }

        GUI.enabled = wasEnabled;

        if (loading)
        {
            GUI.color = new Color(0f, 0f, 0f, 0.75f);
            GUI.DrawTexture(new Rect(-4, -4, Screen.width + 8, Screen.height + 8), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(0, 0, Screen.width, Screen.height), "Cargando partida…", title);
            return;
        }

        GUI.Label(new Rect(0, 0, Screen.width, Screen.height - 14f * k), "Enter: jugar", hint);
    }
}
