using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// ---------------------------------------------------------------------------------------------------------------------
// Servidor dedicado: el mismo juego arrancado sin jugador local (Civilogenia.exe -batchmode -dedicated).
// Abre el puerto como anfitrión, espera a los jugadores (de uno o de varios equipos), crea la partida y la simula.
// La civilización del anfitrión (la 0) la maneja la IA; los jugadores se unen desde "Multijugador > Unirse" con la IP del servidor.
// Guarda la partida cada pocos minutos y al cerrarse; al arrancar la continúa si existe (los jugadores retoman sus plazas
// por orden de llegada).
//
// Configuración: archivo "servidor.cfg" junto al ejecutable (se crea solo la primera vez). Cada ajuste también se puede dar por
// línea de comandos con un guion delante (-port 7777) y eso manda sobre el archivo.
//
//   port         puerto TCP                                                        7777
//   players      máximo de jugadores humanos (1-7); con la sala llena la partida empieza sola     4
//   minplayers   jugadores necesarios para que pueda empezar                       1
//   wait         segundos de espera tras llegar a minplayers (0 = empezar ya, -1 = esperar a que se llene la sala)  60
//   rivals       civilizaciones de la IA                                           3
//   difficulty   0 fácil, 1 normal, 2 difícil                                      1
//   mapsize      índice de GameDefs.MapSizes                                       0
//   autosave     segundos entre guardados                                          120
//   save         archivo de guardado                                               servidor.json (carpeta de datos del usuario)
//   newgame      true para ignorar la partida guardada                             false
//   relay        true para aceptar jugadores por Unity Relay (código en pantalla y en codigo.txt)   true
//   tcp          true para aceptar jugadores por IP y puerto (puede ir a la vez que Relay)          true
//
// Archivos de control junto al ejecutable: parar.txt cierra el servidor guardando (PararServidor.bat) y empezar.txt
// empieza la partida con los jugadores que haya ya en la sala (EmpezarPartida.bat).
// ---------------------------------------------------------------------------------------------------------------------
public class DedicatedServer : MonoBehaviour
{
    const string ConfigFileName = "servidor.cfg";
    const string StopFileName = "parar.txt";
    const string StartFileName = "empezar.txt";

    const string DefaultConfig =
        "# Configuración del servidor de Civilogenia. Una línea por ajuste (clave=valor). Reinicia el servidor para aplicar los cambios.\r\n" +
        "# La línea de comandos (por ejemplo -port 7800) manda sobre este archivo.\r\n" +
        "\r\n" +
        "# Puerto TCP. Ábrelo en el cortafuegos y en el router si hay jugadores de fuera de tu red.\r\n" +
        "port=7777\r\n" +
        "# Máximo de jugadores humanos (1 a 7). Con la sala llena la partida empieza sola.\r\n" +
        "players=4\r\n" +
        "# Jugadores necesarios para poder empezar.\r\n" +
        "minplayers=1\r\n" +
        "# Segundos de espera, desde que se llega a minplayers, por si se une alguien más (se reinicia con cada jugador nuevo).\r\n" +
        "# 0 = empezar nada más llegar a minplayers; -1 = esperar siempre a que se llene la sala.\r\n" +
        "wait=60\r\n" +
        "# Civilizaciones controladas por la IA.\r\n" +
        "rivals=3\r\n" +
        "# Dificultad de la IA: 0 fácil, 1 normal, 2 difícil.\r\n" +
        "difficulty=1\r\n" +
        "# Tamaño del mapa (índice de la lista de tamaños del juego, 0 = el más pequeño).\r\n" +
        "mapsize=0\r\n" +
        "# Segundos entre guardados automáticos.\r\n" +
        "autosave=120\r\n" +
        "# true = ignorar la partida guardada y empezar un mundo nuevo.\r\n" +
        "newgame=false\r\n" +
        "# Unity Relay: los jugadores se unen con un código sin abrir puertos (necesita los paquetes de Relay y el proyecto enlazado a Unity Cloud).\r\n" +
        "# El código sale en la ventana del servidor y se guarda en codigo.txt.\r\n" +
        "relay=true\r\n" +
        "# Conexión directa por IP y puerto. Puede ir a la vez que Relay.\r\n" +
        "tcp=true\r\n";

    /// <summary>true si el juego se arrancó con -dedicated: no hay menú ni introducción.</summary>
    public static bool IsDedicated { get; private set; }

    int port = CivNet.DefaultPort, players = 4, minPlayers = 1, waitSeconds = 60, rivals = 3, difficulty = 1, mapSize = 0, autosave = 120;
    bool newGame, useTcp = true, useRelay = true;
    string savePath, stopPath, startPath;
    string exeDir;
    readonly Dictionary<string, string> cfg = new Dictionary<string, string>();
    bool running;   // la partida está en marcha (solo entonces se guarda)

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Detect()
    {
        IsDedicated = Array.IndexOf(Environment.GetCommandLineArgs(), "-dedicated") >= 0;
        if (!IsDedicated) return;

        IntroVideo.Enabled = false;
        Application.runInBackground = true;
        Application.targetFrameRate = 30;
        QualitySettings.vSyncCount = 0;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Create()
    {
        if (!IsDedicated || FindFirstObjectByType<DedicatedServer>() != null) return;
        new GameObject("Servidor dedicado").AddComponent<DedicatedServer>();
    }

    static void Log(string text) { Debug.Log("[Civilogenia][Servidor] " + text); }

    // ---------------------------------------------------------------- Configuración

    void LoadConfig()
    {
        string path = Path.Combine(exeDir, ConfigFileName);
        try
        {
            if (!File.Exists(path))
            {
                File.WriteAllText(path, DefaultConfig);
                Log("Creado " + ConfigFileName + " con los valores por defecto.");
            }

            foreach (string raw in File.ReadAllLines(path))
            {
                string line = raw.Trim();
                int eq = line.IndexOf('=');
                if (line.Length == 0 || line[0] == '#' || eq <= 0) continue;
                cfg[line.Substring(0, eq).Trim().ToLowerInvariant()] = line.Substring(eq + 1).Trim();
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("[Civilogenia][Servidor] No se pudo leer " + ConfigFileName + ": " + e.Message + ". Se usan los valores por defecto.");
        }
    }

    // La línea de comandos manda sobre el archivo, y el archivo sobre el valor por defecto
    string Setting(string key)
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] != "-" + key) continue;
            if (i + 1 >= args.Length) return "true";
            bool isValue = !args[i + 1].StartsWith("-") || int.TryParse(args[i + 1], out _);   // "-1" es un valor; "-newgame" no
            return isValue ? args[i + 1] : "true";   // -newgame sin valor = true
        }
        return cfg.TryGetValue(key, out string v) ? v : null;
    }

    int IntSetting(string key, int fallback)
    {
        string v = Setting(key);
        return v != null && int.TryParse(v, out int n) ? n : fallback;
    }

    bool BoolSetting(string key, bool fallback)
    {
        string v = Setting(key);
        if (v == null) return fallback;
        v = v.ToLowerInvariant();
        return v == "true" || v == "1" || v == "si" || v == "sí" || v == "yes";
    }

    void Start()
    {
        exeDir = Path.GetDirectoryName(Application.dataPath);   // la carpeta del ejecutable (Civilogenia_Data/..)
        LoadConfig();

        port = Mathf.Clamp(IntSetting("port", port), 1, 65535);
        players = Mathf.Clamp(IntSetting("players", players), 1, CivGame.MaxNetPlayers - 1);
        minPlayers = Mathf.Clamp(IntSetting("minplayers", minPlayers), 1, players);
        waitSeconds = IntSetting("wait", waitSeconds);
        rivals = Mathf.Clamp(IntSetting("rivals", rivals), 0, 9);
        difficulty = Mathf.Clamp(IntSetting("difficulty", difficulty), 0, 2);
        mapSize = Mathf.Max(0, IntSetting("mapsize", mapSize));
        autosave = Mathf.Max(10, IntSetting("autosave", autosave));
        newGame = BoolSetting("newgame", false);
        useTcp = BoolSetting("tcp", true);
        useRelay = BoolSetting("relay", true);
        savePath = Setting("save") ?? Path.Combine(Application.persistentDataPath, "servidor.json");

        stopPath = Path.Combine(exeDir, StopFileName);
        startPath = Path.Combine(exeDir, StartFileName);
        try { if (File.Exists(stopPath)) File.Delete(stopPath); } catch (Exception) { }
        try { if (File.Exists(startPath)) File.Delete(startPath); } catch (Exception) { }

        Log("Configuración: puerto " + port + ", " + minPlayers + "-" + players + " jugador(es), espera " + waitSeconds + " s, " + rivals
            + " IA, dificultad " + difficulty + ", mapa " + mapSize + ", guardado cada " + autosave + " s.");

        StartCoroutine(Run());
        StartCoroutine(WatchStopFile());
    }

    // ---------------------------------------------------------------- Partida

    IEnumerator Run()
    {
        Log("Iniciando servidor: generando el mundo…");
        CivGame game;
        while ((game = CivGame.Instance) == null || !game.worldBuilt) yield return null;
        Log("Mundo construido (planeta, mapa y ríos listos).");
        CivNet net;
        while ((net = CivNet.Instance) == null) yield return null;

        CivSaveData saved = newGame ? null : CivSave.ReadFrom(savePath);
        bool resume = saved != null && saved.playerCivs.Count > 0;
        int maxPlayers = resume ? Mathf.Min(players, saved.playerCivs.Count) : players;
        int minNeeded = Mathf.Min(minPlayers, maxPlayers);

        bool listening = false;   // al menos una vía de conexión (TCP o Relay) está abierta

        if (useTcp)
        {
            if (net.StartHost(port)) listening = true;
            else if (!useRelay)
            {
                Debug.LogError("[Civilogenia][Servidor] " + net.status);
                Application.Quit(1);
                yield break;
            }
            else Debug.LogWarning("[Civilogenia][Servidor] " + net.status + " Se sigue solo con Relay.");
        }

        if (useRelay)
        {
            if (!CivNet.RelayAvailable)
            {
                Log("AVISO: Relay no está disponible en este ejecutable (instala los paquetes com.unity.services.relay y com.unity.transport y vuelve a compilar).");
            }
            else
            {
                Log("Pidiendo una sala a Unity Relay…");
                net.StartRelayHost(maxPlayers);
                while (!net.relayReady && !net.relayFailed) yield return null;

                if (net.relayReady)
                {
                    listening = true;
                    Log("Relay listo. CÓDIGO PARA UNIRSE: " + net.relayJoinCode);
                    try { File.WriteAllText(Path.Combine(exeDir, "codigo.txt"), net.relayJoinCode); } catch (Exception) { }
                }
                else
                {
                    Log("AVISO: no se pudo abrir Relay (" + net.relayError + ")." + (useTcp ? " Se sigue solo con conexión directa por IP." : ""));
                }
            }
        }

        if (!listening)
        {
            Debug.LogError("[Civilogenia][Servidor] No hay ninguna vía de conexión abierta (ni TCP ni Relay).");
            Application.Quit(1);
            yield break;
        }

        if (resume && saved.playerCivs.Count < players)
            Log("AVISO: la partida guardada solo tiene " + saved.playerCivs.Count + " plaza(s) de jugador y se pidieron " + players
                + ". Para empezar una partida nueva con " + players + " jugadores, arranca con -newgame.");

        string ips = string.Join(", ", CivNet.LocalAddresses());
        Log("Esperando conexiones" + (useTcp ? " por IP en el puerto " + port + (ips.Length > 0 ? " (IP en la red local: " + ips + ")" : "") : "")
            + (net.relayReady ? (useTcp ? " y" : "") + " por Relay con el código " + net.relayJoinCode : "") + ". "
            + (resume ? "Se continuará la partida guardada. " : "") + "Plazas: " + maxPlayers + ".");

        yield return StartCoroutine(WaitForPlayers(game, minNeeded, maxPlayers));

        int remotes;
        if (resume)
        {
            remotes = game.NetPrepareResume(saved.playerCivs.Count);
            yield return StartCoroutine(game.LoadFromData(saved, -1));
            game.NetHostResumeSetup();
            game.NetHostBegin(saved.playerCivs);
        }
        else
        {
            remotes = game.NetPrepareHost(0);
            game.StartMatch(rivals, (Difficulty)difficulty, 0, mapSize);
            while (!game.ready && !game.startFailed) yield return null;

            if (game.startFailed)
            {
                Debug.LogError("[Civilogenia][Servidor] No se pudo crear la partida.");
                Application.Quit(1);
                yield break;
            }

            game.NetHostHandOverToAi();   // la civilización 0 no la juega nadie: la IA se encarga
            game.NetHostBegin();
        }

        running = true;
        Log("Partida " + (resume ? "continuada" : "en marcha") + " con " + remotes + " jugador(es). Se guarda cada " + autosave + " s en " + savePath);

        // Si todos se van, el servidor sigue simulando (la IA toma el mando de cada civilización que se desconecta)
        while (true)
        {
            yield return new WaitForSeconds(autosave);
            Save(game);
            Log("Jugadores conectados: " + game.NetJoinedCount());
        }
    }

    // La sala se abre hasta que se llena, o hay "minplayers" y pasa el tiempo de espera, o se crea empezar.txt
    IEnumerator WaitForPlayers(CivGame game, int minNeeded, int maxPlayers)
    {
        int last = -1;
        float deadline = -1f;
        float nextNotice = 0f;

        while (true)
        {
            int n = game.NetJoinedCount();

            if (n != last)
            {
                last = n;
                Log("Jugadores en la sala: " + n + "/" + maxPlayers);
                deadline = n >= minNeeded && waitSeconds > 0 ? Time.unscaledTime + waitSeconds : -1f;   // cada jugador nuevo reinicia la espera
                nextNotice = 0f;
            }

            if (n >= maxPlayers) { Log("Sala llena."); yield break; }

            if (n >= 1 && File.Exists(startPath))
            {
                try { File.Delete(startPath); } catch (Exception) { }
                Log("Inicio solicitado con " + n + " jugador(es).");
                yield break;
            }

            if (n >= minNeeded && waitSeconds == 0) yield break;

            if (deadline >= 0f)
            {
                float left = deadline - Time.unscaledTime;
                if (left <= 0f) yield break;
                if (Time.unscaledTime >= nextNotice)
                {
                    Log("La partida empieza en " + Mathf.CeilToInt(left) + " s (o antes si se llena la sala).");
                    nextNotice = Time.unscaledTime + 15f;
                }
            }

            yield return null;
        }
    }

    void Save(CivGame game)
    {
        if (!running || game == null) return;
        Log(game.SaveServerGame(savePath) ? "Partida guardada." : "No se pudo guardar la partida.");
    }

    // Crear parar.txt junto al ejecutable (PararServidor.bat) cierra el servidor guardando antes la partida
    IEnumerator WatchStopFile()
    {
        while (true)
        {
            yield return new WaitForSeconds(1f);
            bool stop = false;
            try { stop = File.Exists(stopPath); } catch (Exception) { }
            if (!stop) continue;

            Log("Cierre solicitado.");
            try { File.Delete(stopPath); } catch (Exception) { }
            Application.Quit();
            yield break;
        }
    }

    void OnApplicationQuit()
    {
        Save(CivGame.Instance);
        if (CivNet.Instance != null) CivNet.Instance.Shutdown();
    }
}
