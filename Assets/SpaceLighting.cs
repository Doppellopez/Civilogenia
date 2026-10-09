using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Iluminación de espacio: una sola luz direccional y todo lo demás negro
/// (sin luz ambiental, sin skybox, sin reflejos, fondo negro).
/// Se aplica sola al pulsar Play en cualquier escena.
/// </summary>
public static class SpaceLighting
{
    public static readonly Vector3 SunEuler = new Vector3(35f, -40f, 0f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void OnSceneLoaded()
    {
        Apply();
    }

    public static void Apply()
    {
        // Sin luz ambiental ni cielo: lo que no toca el Sol queda negro
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = Color.black;
        RenderSettings.ambientIntensity = 0f;
        RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
        RenderSettings.reflectionIntensity = 0f;
        RenderSettings.fog = false;

        // Fondo negro en todas las cámaras
        foreach (Camera cam in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
        }

        // Una única luz direccional: se reutiliza la primera y se apagan las demás luces
        Light sun = null;
        foreach (Light light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
        {
            if (sun == null && light.type == LightType.Directional)
            {
                sun = light;
                continue;
            }

            light.enabled = false;
        }

        if (sun == null)
        {
            GameObject go = new GameObject("Sol (luz direccional)");
            go.transform.rotation = Quaternion.Euler(SunEuler);
            sun = go.AddComponent<Light>();
            sun.type = LightType.Directional;
        }

        sun.color = new Color(1f, 0.97f, 0.92f);
        sun.intensity = 1.3f;
        sun.shadows = LightShadows.Soft;
        RenderSettings.sun = sun;
    }
}
