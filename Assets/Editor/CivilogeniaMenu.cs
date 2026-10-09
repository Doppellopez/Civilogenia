using UnityEditor;
using UnityEngine;

public static class CivilogeniaMenu
{
    [MenuItem("Civilogenia/Crear Tierra")]
    static void CreateEarth()
    {
        GameObject earth = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        earth.name = "Tierra";
        earth.transform.position = Vector3.zero;
        earth.AddComponent<EarthTopography>();

        Undo.RegisterCreatedObjectUndo(earth, "Crear Tierra");
        Selection.activeGameObject = earth;
    }

    [MenuItem("Civilogenia/Aplicar luz espacial (Sol + negro)")]
    static void ApplySpaceLighting()
    {
        SpaceLighting.Apply();
        UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
    }

    [MenuItem("Civilogenia/Añadir topografía a la Tierra seleccionada")]
    static void AddTopography()
    {
        GameObject go = Selection.activeGameObject;
        if (go == null) return;

        if (go.GetComponent<EarthTopography>() == null)
            Undo.AddComponent<EarthTopography>(go);
    }
}
