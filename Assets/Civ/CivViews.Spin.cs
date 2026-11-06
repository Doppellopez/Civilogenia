using System.Collections.Generic;
using UnityEngine;

// Piezas de las unidades que giran: ruedas (también las ruedas de palas de los barcos de vapor) y hélices de motor.
// Los modelos son piezas sueltas, así que las ruedas se reconocen por su forma y se les añaden radios para que se note que giran.

/// <summary>Una pieza que gira sobre sí misma; CivGame.AnimateUnit la mueve.</summary>
public class UnitSpinner
{
    public Transform part;
    public Quaternion baseRot;   // rotación original de la pieza
    public Vector3 axis;         // eje local sobre el que gira
    public bool wheel;           // rueda: gira según lo que avanza la unidad; si no, gira siempre (hélice)
    public float radius;         // radio de la rueda (para que su giro case con lo que avanza)
    public float angle;          // grados girados hasta ahora
}

public static partial class CivViews
{
    static Material spokeMaterial;

    static Material SpokeMaterial => spokeMaterial != null ? spokeMaterial : (spokeMaterial = LitMaterial(new Color(0.78f, 0.78f, 0.8f)));

    // Una rueda es un disco fino: un cilindro tumbado (eje a lo largo de X) con la altura mucho menor que el diámetro
    static bool LooksLikeWheel(Transform t, Mesh mesh)
    {
        if (mesh != Cylinder || t.name != "Pieza") return false;
        if (t.localScale.y > t.localScale.x * 0.35f) return false;
        return Quaternion.Angle(t.localRotation, Quaternion.Euler(0f, 0f, 90f)) < 1f;
    }

    /// <summary>Busca las ruedas del modelo, les pone dos radios cruzados y devuelve todo lo que debe girar (ruedas y hélices).</summary>
    public static List<UnitSpinner> CollectSpinners(Transform root)
    {
        List<UnitSpinner> list = new List<UnitSpinner>();

        foreach (MeshFilter mf in root.GetComponentsInChildren<MeshFilter>(true))
        {
            Transform t = mf.transform;

            if (LooksLikeWheel(t, mf.sharedMesh))
            {
                // Dos barras cruzadas que sobresalen un poco de las dos caras del disco
                Piece(t, Cube, SpokeMaterial, Vector3.zero, new Vector3(1f, 3f, 0.12f));
                Piece(t, Cube, SpokeMaterial, Vector3.zero, new Vector3(0.12f, 3f, 1f));

                list.Add(new UnitSpinner { part = t, baseRot = t.localRotation, axis = Vector3.down, wheel = true, radius = Mathf.Max(0.0001f, t.localScale.x * 0.5f) });
            }
            else if (t.name == "Hélice")
            {
                list.Add(new UnitSpinner { part = t, baseRot = t.localRotation, axis = Vector3.forward, wheel = false });
            }
        }

        return list;
    }

    // Hélice (o ventilador de turbina) de dos palas cruzadas, que mira hacia +Z. La recoge CollectSpinners por su nombre.
    static GameObject Fan(Transform root, Vector3 pos, float radius)
    {
        GameObject pivot = new GameObject("Hélice");
        pivot.transform.SetParent(root, false);
        pivot.transform.localPosition = pos;

        Piece(pivot.transform, Cube, SpokeMaterial, Vector3.zero, new Vector3(radius * 2f, radius * 0.18f, radius * 0.08f));
        Piece(pivot.transform, Cube, SpokeMaterial, Vector3.zero, new Vector3(radius * 0.18f, radius * 2f, radius * 0.08f));
        return pivot;
    }
}
