using System;
using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Vuelve al menú principal desde una partida: cierra la red, recarga la escena y vuelve a lanzar los componentes que el juego
/// crea solo al empezar (partida, menú, interfaz, red...), sin repetir la introducción.
/// </summary>
public class GameFlow : MonoBehaviour
{
    static GameFlow instance;
    static bool busy;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        instance = null;
        busy = false;   // (el aviso vive en la instancia, que desaparece al empezar un nuevo Play)
    }

    public static void ReturnToMenu()
    {
        if (busy) return;

        if (instance == null)
        {
            GameObject go = new GameObject("Flujo del juego");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<GameFlow>();
        }

        instance.StartCoroutine(instance.Reload());
    }

    bool overlay;               // pantalla negra con aviso mientras se rehace el mundo
    GUIStyle overlayStyle;

    void OnGUI()
    {
        if (!overlay) return;

        GUI.depth = -10000;   // por encima de todo
        GUI.matrix = Matrix4x4.identity;
        GUI.color = Color.black;
        GUI.DrawTexture(new Rect(-4, -4, Screen.width + 8, Screen.height + 8), Texture2D.whiteTexture);
        GUI.color = Color.white;

        if (overlayStyle == null)
            overlayStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, wordWrap = true, richText = true };
        overlayStyle.fontSize = Mathf.RoundToInt(Screen.height / 720f * 36f);
        overlayStyle.normal.textColor = new Color(1f, 0.95f, 0.82f);

        GUI.Label(new Rect(0, 0, Screen.width, Screen.height),
            Loc.T("Volviendo al menú principal…") + "\n\n<size=" + Mathf.RoundToInt(Screen.height / 720f * 20f) + ">"
            + Loc.T("Se está rehaciendo el mundo; puede tardar unos segundos.") + "</size>", overlayStyle);
    }

    IEnumerator Reload()
    {
        busy = true;
        overlay = true;
        Debug.Log("[Civilogenia] Volviendo al menú principal…");

        // Dos fotogramas para que el aviso llegue a pantalla antes del trabajo pesado, que bloquea el hilo principal
        yield return null;
        yield return null;

        if (CivNet.Instance != null) CivNet.Instance.Shutdown();   // el anfitrión avisa a los demás; el cliente se desconecta
        CivViews.ResetStatics();
        Time.timeScale = 1f;

        Scene scene = SceneManager.GetActiveScene();
        AsyncOperation load;
#if UNITY_EDITOR
        // En el editor la escena puede no estar en los Build Settings
        load = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(scene.path, new LoadSceneParameters(LoadSceneMode.Single));
#else
        load = SceneManager.LoadSceneAsync(scene.buildIndex);
#endif
        while (load != null && !load.isDone) yield return null;
        yield return null;

        // Los componentes del juego se crean con [RuntimeInitializeOnLoadMethod(AfterSceneLoad)], que solo salta al arrancar:
        // se vuelven a llamar a mano. La introducción no se repite.
        IntroVideo.Enabled = false;   // no se repite la introducción al volver al menú
        IntroVideo.CancelAll();       // por si ya había una en marcha
        RunSceneLoadHooks();
        IntroVideo.CancelAll();
        yield return null;
        IntroVideo.CancelAll();       // el vídeo que se cree en este fotograma se cierra también

        // El aviso se quita cuando el mundo nuevo está listo (o tras un máximo de espera, por si algo fallara)
        float waited = 0f;
        CivGame game;
        while (((game = CivGame.Instance) == null || !game.worldBuilt) && waited < 180f)
        {
            waited += Time.unscaledDeltaTime;
            yield return null;
        }

        overlay = false;
        busy = false;
    }

    static void RunSceneLoadHooks()
    {
        const BindingFlags flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        foreach (Type type in typeof(GameFlow).Assembly.GetTypes())
        {
            foreach (MethodInfo method in type.GetMethods(flags))
            {
                RuntimeInitializeOnLoadMethodAttribute attr = method.GetCustomAttribute<RuntimeInitializeOnLoadMethodAttribute>();
                if (attr == null || attr.loadType != RuntimeInitializeLoadType.AfterSceneLoad || method.GetParameters().Length != 0) continue;

                try { method.Invoke(null, null); }
                catch (Exception e) { Debug.LogWarning("[Civilogenia] No se pudo reiniciar " + type.Name + "." + method.Name + ": " + e.Message); }
            }
        }
    }
}
