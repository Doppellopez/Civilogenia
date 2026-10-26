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

    static bool buildOk;

    // Compilación por línea de comandos (-batchmode -executeMethod CivilogeniaBuild.BuildFromCommandLine):
    // abre la escena del juego y compila; sale con código 0 si el build es correcto y 1 si falla.
    public static void BuildFromCommandLine()
    {
        EditorSceneManager.OpenScene("Assets/prueba.unity");
        BuildWindows();
        EditorApplication.Exit(buildOk ? 0 : 1);
    }

    // Igual que BuildFromCommandLine pero para Linux (Ubuntu, x86_64): -executeMethod CivilogeniaBuild.BuildLinuxFromCommandLine
    public static void BuildLinuxFromCommandLine()
    {
        EditorSceneManager.OpenScene("Assets/prueba.unity");
        BuildLinux();
        EditorApplication.Exit(buildOk ? 0 : 1);
    }

    [MenuItem("Civilogenia/Compilar (Linux)")]
    public static void BuildLinux()
    {
        Build(BuildTarget.StandaloneLinux64, Path.Combine("ExeLinux", "Civilogenia.x86_64"));
    }

    [MenuItem("Civilogenia/Compilar (Windows)")]
    public static void BuildWindows()
    {
        Build(BuildTarget.StandaloneWindows64, Path.Combine(OutputFolder, "Civilogenia.exe"));
    }

    // Guarda la escena actual, incluye los shaders que se buscan por nombre y compila para la plataforma indicada.
    static void Build(BuildTarget target, string outputPath)
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
        EnsureShaderIncluded("Civilogenia/Fog");

        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = new[] { path },
            locationPathName = outputPath,
            target = target,
            options = BuildOptions.None
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        BuildSummary summary = report.summary;

        buildOk = summary.result == BuildResult.Succeeded;

        if (buildOk)
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
