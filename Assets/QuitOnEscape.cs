using UnityEngine;

/// <summary>
/// Esc cierra el programa (en el editor detiene el modo Play).
/// Se crea sola al pulsar Play.
/// </summary>
public class QuitOnEscape : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Create()
    {
        if (FindFirstObjectByType<QuitOnEscape>() != null) return;

        GameObject go = new GameObject("Salir con Esc");
        go.AddComponent<QuitOnEscape>();
        DontDestroyOnLoad(go);
    }

    void Update()
    {
        if (!Input.GetKeyDown(KeyCode.Escape)) return;

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
