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
    bool choosingCiv;          // pantalla "Elige tu civilización" que sale al pulsar Jugar
    int civChoice;             // índice en GameDefs.PlayableCivs
    bool choosingSize;         // pantalla "Tamaño del mapa", entre la civilización y los rivales
    int mapSize;               // índice en GameDefs.MapSizes
    bool choosingRivals;       // pantalla "¿Cuántos rivales?" que sale al pulsar Jugar
    int rivals = 3;            // civilizaciones enemigas, manejadas por la IA
    int difficultyIndex = 1;   // 0 fácil, 1 normal, 2 difícil
    static readonly string[] DifficultyNames = { "Fácil", "Normal", "Difícil" };
    static readonly string[] DifficultyHints =
    {
        "La IA produce y crece un 30% menos y tarda más en atacar",
        "La IA juega con las mismas reglas que tú",
        "La IA produce y crece un 30% más y ataca antes"
    };
    const int MaxRivals = 9;
    // Multijugador (ver CivGame.Net.cs): el anfitrión abre una sala y los demás se unen por IP
    int mpMode;                // 0 = un jugador, 1 = anfitrión, 2 = se une a una sala
    bool mpMenu;               // pantalla "Multijugador": crear o unirse
    bool hostLobby;            // sala del anfitrión: jugadores conectados y botón Comenzar
    bool joinForm;             // formulario para unirse: nombre e IP
    bool joinWait;             // conectado (o conectando): se espera a que el anfitrión empiece
    string joinIp = "127.0.0.1";
    string playerName = "Jugador";
    string mpError = "";
    bool loading;
    string loadingText = "Cargando partida…";
    float openTime;
    float previousTimeScale = 1f;

    GUIStyle title, subtitle, button, body, hint, hint2, number;
    float styleScale = -1f;

    static readonly string[] Controls =
    {
        "Clic izquierdo: seleccionar unidad o ciudad",
        "Clic derecho: mover o atacar  (Mayús: mover y fundar ciudad)",
        "B: fundar ciudad con el colono seleccionado",
        "R: construir carretera    V: mejorar a ferrocarril    F: granja",
        "F (unidad militar): fortificar, +25% de defensa (+50% en ciudad con muro)",
        "T: tecnologías    H: ayuda    C: centrar la cámara",
        "Espacio: pausa",
        "Cursores o , . : mover la cámara    Rueda: zoom    Doble clic: seguir un punto",
        "N: minimapa (clic para ir a un punto del planeta)",
        "D: diplomacia (proponer paz o alianzas, declarar guerra)",
        "F1: guía del juego (durante la partida)",
        "F2: activar o silenciar los efectos de sonido",
        "Mayús+T: desarrollar todas las tecnologías de la edad actual",
        "F5: guardar partida    F9: cargar partida",
        "Esc: menú de pausa (guardar y salir)"
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

        if (Active && joinWait) UpdateJoinWait();

        if (!Active || loading || Time.unscaledTime - openTime < IgnoreInputSeconds) return;

        if (Inp.GetKeyDown(KeyCode.Return) || Inp.GetKeyDown(KeyCode.KeypadEnter))
        {
            if (mpMenu || hostLobby || joinWait) return;
            if (joinForm) { Connect(); return; }

            if (choosingRivals) { if (mpMode == 1) OpenHostLobby(); else StartCoroutine(StartRoutine()); }
            else if (choosingSize) { choosingSize = false; choosingRivals = true; }
            else if (choosingCiv) AfterCivChosen();
            else if (!showControls) choosingCiv = true;
        }
    }

    void Open()
    {
        Active = true;
        showControls = false;
        choosingCiv = false;
        choosingSize = false;
        choosingRivals = false;
        mpMenu = false;
        hostLobby = false;
        joinForm = false;
        joinWait = false;
        mpMode = 0;
        openTime = Time.unscaledTime;
        previousTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
        Time.timeScale = 0f;
        Debug.Log("[Civilogenia] Menú principal abierto.");
    }

    // Cargar partida: espera a que el mundo esté listo, carga la partida guardada y empieza a jugar
    System.Collections.IEnumerator LoadRoutine()
    {
        loading = true;
        loadingText = "Cargando partida…";
        yield return null;
        yield return null;   // deja pintar el aviso "Cargando partida…" antes de que el trabajo bloquee el hilo principal

        CivGame game = CivGame.Instance;
        while (game == null || !game.worldBuilt) { yield return null; game = CivGame.Instance; }

        yield return StartCoroutine(game.LoadRoutine());
        loading = false;
        Play();
    }

    // Nueva partida: el juego crea tu civilización y las enemigas (manejadas por la IA) y empieza
    System.Collections.IEnumerator StartRoutine()
    {
        loading = true;
        loadingText = "Preparando partida…";
        yield return null;
        yield return null;

        CivGame game = CivGame.Instance;
        while (game == null || !game.worldBuilt) { yield return null; game = CivGame.Instance; }

        game.StartMatch(rivals, (Difficulty)difficultyIndex, civChoice, mapSize);
        while (!game.ready && !game.startFailed) yield return null;

        loading = false;
        if (game.startFailed) { choosingRivals = false; choosingSize = false; choosingCiv = false; yield break; }   // no había tierra: se vuelve al menú
        choosingRivals = false;
        Play();
        IntroVideo.PlayClip(IntroClip);   // la introducción sale tras pulsar Jugar, ya con la partida creada
    }

    const string IntroClip = "Intro del Civilogenia";

    // ---------------------------------------------------------------- Multijugador

    void AfterCivChosen()
    {
        choosingCiv = false;
        if (mpMode == 2) joinForm = true;
        else choosingSize = true;
    }

    void BackToMpMenu()
    {
        if (CivNet.Instance != null) CivNet.Instance.Shutdown();
        choosingCiv = choosingSize = choosingRivals = hostLobby = joinForm = joinWait = false;
        mpMenu = true;
        mpMode = 0;
    }

    // El anfitrión abre el puerto y espera a los demás jugadores
    void OpenHostLobby()
    {
        mpError = "";
        if (!CivNet.Instance.StartHost())
        {
            mpError = CivNet.Instance.status;
            BackToMpMenu();
            return;
        }

        choosingRivals = false;
        hostLobby = true;
    }

    // Cliente: conecta con "ip" o "ip:puerto"
    void Connect()
    {
        string host = joinIp.Trim();
        int port = CivNet.DefaultPort;
        int colon = host.LastIndexOf(':');
        if (colon > 0 && int.TryParse(host.Substring(colon + 1), out int parsed)) { port = parsed; host = host.Substring(0, colon); }

        if (host.Length == 0) { mpError = "Escribe la IP del anfitrión."; return; }

        mpError = "";
        CivNet.Instance.StartClient(host, port, playerName.Trim().Length > 0 ? playerName.Trim() : "Jugador", civChoice);
        joinForm = false;
        joinWait = true;
    }

    // Cliente a la espera: pasa a jugar en cuanto el anfitrión empieza y la partida está cargada
    void UpdateJoinWait()
    {
        CivGame game = CivGame.Instance;
        CivNet net = CivNet.Instance;

        if (game != null && game.netJoining) { loading = true; loadingText = "Uniéndose a la partida…"; }

        if (game != null && game.ready && game.IsNetClient)
        {
            loading = false;
            joinWait = false;
            Play();
            return;
        }

        if (net != null && !loading && (!net.Active || (game != null && game.netLost)))
        {
            mpError = string.IsNullOrEmpty(net.status) ? "Se ha perdido la conexión." : net.status;
            BackToMpMenu();
        }
    }

    // El anfitrión empieza: se crea la partida con los jugadores de la sala, se les manda el estado y se juega
    System.Collections.IEnumerator HostStartRoutine()
    {
        loading = true;
        loadingText = "Preparando partida…";
        yield return null;
        yield return null;

        CivGame game = CivGame.Instance;
        while (game == null || !game.worldBuilt) { yield return null; game = CivGame.Instance; }

        int remotes = game.NetPrepareHost(civChoice);
        game.StartMatch(rivals, (Difficulty)difficultyIndex, civChoice, mapSize);
        while (!game.ready && !game.startFailed) yield return null;

        loading = false;
        if (game.startFailed) { BackToMpMenu(); yield break; }

        hostLobby = false;
        if (remotes > 0) game.NetHostBegin();
        else CivNet.Instance.Shutdown();   // nadie se unió: partida normal

        Play();
        IntroVideo.PlayClip(IntroClip);   // en el multijugador no se reproduce
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

        hint2 = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = Mathf.RoundToInt(16 * k) };
        hint2.normal.textColor = new Color(1f, 1f, 1f, 0.6f);

        number = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = Mathf.RoundToInt(44 * k), fontStyle = FontStyle.Bold };
        number.normal.textColor = new Color(1f, 0.95f, 0.82f);

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

        if (mpMenu)
        {
            GUI.Label(new Rect(0, y - 20f * k, Screen.width, 44f * k), "Multijugador", body);
            y += 34f * k;

            if (mpError.Length > 0) { GUI.Label(new Rect(0, y - 6f * k, Screen.width, 28f * k), mpError, hint2); y += 30f * k; }

            if (GUI.Button(new Rect(cx - bw / 2f, y, bw, bh), "Crear partida", button)) { mpError = ""; mpMenu = false; mpMode = 1; choosingCiv = true; }
            y += bh + gap;
            if (GUI.Button(new Rect(cx - bw / 2f, y, bw, bh), "Unirse a una partida", button)) { mpError = ""; mpMenu = false; mpMode = 2; choosingCiv = true; }
            y += bh + gap;
            if (GUI.Button(new Rect(cx - bw / 2f, y, bw, bh), "Volver", button)) mpMenu = false;
        }
        else if (hostLobby)
        {
            CivGame game = CivGame.Instance;
            GUI.Label(new Rect(0, y - 40f * k, Screen.width, 44f * k), "Sala de la partida", body);

            string ips = string.Join("   ", CivNet.LocalAddresses().ToArray());
            hint2.wordWrap = true;
            GUI.Label(new Rect(cx - 320f * k, y, 640f * k, 54f * k), "Que los demás se unan a: " + (ips.Length > 0 ? ips : "(IP no encontrada)") + "   puerto " + CivNet.DefaultPort, hint2);
            y += 58f * k;

            string players = game != null ? game.HostLobbyText() : "";
            int lines = players.Split('\n').Length;
            GUI.Label(new Rect(cx - 280f * k, y, 560f * k, 28f * k * lines + 10f * k), players, body);
            y += 28f * k * lines + 24f * k;

            if (GUI.Button(new Rect(cx - bw / 2f, y, bw, bh), "Comenzar partida", button)) StartCoroutine(HostStartRoutine());
            y += bh + gap;
            if (GUI.Button(new Rect(cx - bw / 2f, y, bw, bh), "Cancelar", button)) BackToMpMenu();
        }
        else if (joinForm)
        {
            GUI.Label(new Rect(0, y - 40f * k, Screen.width, 44f * k), "Unirse a una partida", body);
            y += 4f * k;

            GUI.Label(new Rect(cx - 280f * k, y, 560f * k, 28f * k), "Tu nombre", hint2);
            y += 28f * k;
            playerName = GUI.TextField(new Rect(cx - 200f * k, y, 400f * k, 40f * k), playerName, 20, button);
            y += 50f * k;

            GUI.Label(new Rect(cx - 280f * k, y, 560f * k, 28f * k), "IP del anfitrión (ejemplo 192.168.1.20 o 192.168.1.20:7777)", hint2);
            y += 28f * k;
            joinIp = GUI.TextField(new Rect(cx - 200f * k, y, 400f * k, 40f * k), joinIp, 64, button);
            y += 54f * k;

            if (mpError.Length > 0) { GUI.Label(new Rect(0, y - 6f * k, Screen.width, 28f * k), mpError, hint2); y += 30f * k; }

            if (GUI.Button(new Rect(cx - bw / 2f, y, bw, bh), "Conectar", button)) Connect();
            y += bh + gap;
            if (GUI.Button(new Rect(cx - bw / 2f, y, bw, bh), "Volver", button)) { joinForm = false; choosingCiv = true; }
        }
        else if (joinWait)
        {
            CivGame game = CivGame.Instance;
            CivNet net = CivNet.Instance;
            GUI.Label(new Rect(0, y - 20f * k, Screen.width, 44f * k), net != null ? net.status : "", body);
            y += 40f * k;

            string players = game != null ? game.netLobbyText : "";
            if (players.Length > 0)
            {
                int lines = players.Split('\n').Length;
                GUI.Label(new Rect(0, y, Screen.width, 28f * k), "Jugadores en la sala", hint2);
                y += 30f * k;
                GUI.Label(new Rect(cx - 280f * k, y, 560f * k, 28f * k * lines + 10f * k), players, body);
                y += 28f * k * lines + 20f * k;
            }

            if (GUI.Button(new Rect(cx - bw / 2f, y, bw, bh), "Cancelar", button)) BackToMpMenu();
        }
        else if (choosingCiv)
        {
            y -= 60f * k;   // la cuadrícula de civilizaciones ocupa más: se sube todo para que quepa el botón Volver
            GUI.Label(new Rect(0, y - 20f * k, Screen.width, 44f * k), "Elige tu civilización", body);
            y += 34f * k;

            string[] names = new string[GameDefs.PlayableCivs.Length];
            for (int i = 0; i < names.Length; i++) names[i] = GameDefs.PlayableCivs[i].name;
            const int cols = 4;
            int rows = (names.Length + cols - 1) / cols;
            civChoice = GUI.SelectionGrid(new Rect(cx - 380f * k, y, 760f * k, 42f * k * rows), civChoice, names, cols, button);
            y += 42f * k * rows + 10f * k;

            GameDefs.CivChoice pick = GameDefs.PlayableCivs[civChoice];
            hint2.wordWrap = true;
            GUI.Label(new Rect(cx - 280f * k, y, 560f * k, 60f * k), pick.description, hint2);
            y += 64f * k;
            if (pick.cityNames != null)
            {
                GUI.Label(new Rect(cx - 280f * k, y, 560f * k, 50f * k),
                    "Ciudades: " + string.Join(", ", pick.cityNames, 0, Mathf.Min(6, pick.cityNames.Length)) + "…", hint2);
                y += 40f * k;
            }

            if (GUI.Button(new Rect(cx - bw / 2f, y, bw, bh), "Continuar", button)) AfterCivChosen();
            y += bh + gap;
            if (GUI.Button(new Rect(cx - bw / 2f, y, bw, bh), "Volver", button)) { choosingCiv = false; if (mpMode != 0) { mpMode = 0; mpMenu = true; } }
        }
        else if (choosingSize)
        {
            GUI.Label(new Rect(0, y - 20f * k, Screen.width, 44f * k), "Tamaño del mapa", body);
            y += 34f * k;

            string[] sizes = new string[GameDefs.MapSizes.Length];
            for (int i = 0; i < sizes.Length; i++) sizes[i] = GameDefs.MapSizes[i].name;
            mapSize = GUI.Toolbar(new Rect(cx - 280f * k, y, 560f * k, 48f * k), mapSize, sizes, button);
            y += 58f * k;
            GUI.Label(new Rect(0, y, Screen.width, 28f * k), GameDefs.MapSizes[mapSize].hint, hint2);
            y += 44f * k;

            if (GUI.Button(new Rect(cx - bw / 2f, y, bw, bh), "Continuar", button)) { choosingSize = false; choosingRivals = true; }
            y += bh + gap;
            if (GUI.Button(new Rect(cx - bw / 2f, y, bw, bh), "Volver", button)) { choosingSize = false; choosingCiv = true; }
        }
        else if (choosingRivals)
        {
            GUI.Label(new Rect(0, y - 20f * k, Screen.width, 44f * k), "Civilizaciones enemigas", body);
            GUI.Label(new Rect(0, y + 14f * k, Screen.width, 30f * k), "Las manejará la IA", hint2);
            y += 64f * k;

            // Selector: − número +
            float sw = 64f * k;
            if (GUI.Button(new Rect(cx - 150f * k, y, sw, bh), "−", button)) rivals = Mathf.Max(0, rivals - 1);
            GUI.Label(new Rect(cx - 70f * k, y, 140f * k, bh), rivals.ToString(), number);
            if (GUI.Button(new Rect(cx + 150f * k - sw, y, sw, bh), "+", button)) rivals = Mathf.Min(MaxRivals, rivals + 1);
            y += bh + gap * 0.6f;

            // Dificultad de la IA
            GUI.Label(new Rect(0, y, Screen.width, 30f * k), "Dificultad", body);
            y += 32f * k;
            difficultyIndex = GUI.Toolbar(new Rect(cx - 180f * k, y, 360f * k, 44f * k), difficultyIndex, DifficultyNames, button);
            y += 46f * k;
            GUI.Label(new Rect(0, y, Screen.width, 24f * k), DifficultyHints[difficultyIndex], hint2);
            y += 30f * k + gap * 0.6f;

            if (GUI.Button(new Rect(cx - bw / 2f, y, bw, bh), mpMode == 1 ? "Abrir sala" : "Comenzar", button))
            {
                if (mpMode == 1) OpenHostLobby();
                else StartCoroutine(StartRoutine());
            }
            y += bh + gap;
            if (GUI.Button(new Rect(cx - bw / 2f, y, bw, bh), "Volver", button)) { choosingRivals = false; choosingSize = true; }
        }
        else if (!showControls)
        {
            if (GUI.Button(new Rect(cx - bw / 2f, y, bw, bh), "Jugar", button)) choosingCiv = true;
            y += bh + gap;

            // Cargar partida: solo si hay una guardada (F5 en la partida guarda)
            bool canLoad = CivSave.Exists;
            bool enabledBefore = GUI.enabled;
            GUI.enabled = enabledBefore && canLoad;
            if (GUI.Button(new Rect(cx - bw / 2f, y, bw, bh), canLoad ? "Cargar partida" : "Cargar partida (no hay ninguna)", button))
                StartCoroutine(LoadRoutine());
            GUI.enabled = enabledBefore;
            y += bh + gap;
            if (GUI.Button(new Rect(cx - bw / 2f, y, bw, bh), "Multijugador", button)) { mpError = ""; mpMenu = true; }
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
            GUI.color = Color.black;   // opaco: mientras se prepara la partida no se ve el planeta sin niebla
            GUI.DrawTexture(new Rect(-4, -4, Screen.width + 8, Screen.height + 8), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(0, 0, Screen.width, Screen.height), loadingText, title);
            return;
        }

        if (mpMenu || hostLobby || joinWait) return;
        GUI.Label(new Rect(0, 0, Screen.width, Screen.height - 14f * k), joinForm ? "Enter: conectar" : choosingRivals ? (mpMode == 1 ? "Enter: abrir sala" : "Enter: comenzar") : (choosingCiv || choosingSize) ? "Enter: continuar" : "Enter: jugar", hint);
    }
}
