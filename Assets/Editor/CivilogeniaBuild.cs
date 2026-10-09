using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class CivilogeniaBuild
{
    const string ScenePath = "Assets/Scenes/Civilogenia.unity";
    const string OutputFolder = "Exe";

    // Guarda la escena actual, incluye los shaders que se buscan por nombre y compila para Windows.
    [MenuItem("Civilogenia/Compilar (Windows)")]
    public static void BuildWindows()
    {
        Scene scene = SceneManager.GetActiveScene();

        // Una escena sin guardar no puede ir en el build
        Directory.CreateDirectory("Assets/Scenes");
        string path = string.IsNullOrEmpty(scene.path) ? ScenePath : scene.path;

        if (!EditorSceneManager.SaveScene(scene, path))
        {
            Debug.LogError("[Civilogenia] No se pudo guardar la escena en " + path);
            return;
        }

        EnsureShaderIncluded("Standard");
        EnsureShaderIncluded("Sprites/Default");
        EnsureShaderIncluded("Unlit/Texture");

        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = new[] { path },
            locationPathName = Path.Combine(OutputFolder, "Civilogenia.exe"),
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        BuildSummary summary = report.summary;

        if (summary.result == BuildResult.Succeeded)
            Debug.Log("[Civilogenia] Build correcto: " + Path.GetFullPath(options.locationPathName)
                      + " (" + (summary.totalSize / 1048576) + " MB)");
        else
            Debug.LogError("[Civilogenia] Build fallido: " + summary.result + ", errores: " + summary.totalErrors);
    }

    // Shader.Find solo funciona en un build si el shader está en "Always Included Shaders"
    static void EnsureShaderIncluded(string shaderName)
    {
        Shader shader = Shader.Find(shaderName);
        if (shader == null) return;

        SerializedObject graphics = new SerializedObject(
            AssetDatabase.LoadAssetAtPath<Object>("ProjectSettings/GraphicsSettings.asset"));
        SerializedProperty list = graphics.FindProperty("m_AlwaysIncludedShaders");

        for (int i = 0; i < list.arraySize; i++)
            if (list.GetArrayElementAtIndex(i).objectReferenceValue == shader) return;

        list.InsertArrayElementAtIndex(list.arraySize);
        list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = shader;
        graphics.ApplyModifiedProperties();
    }
}
