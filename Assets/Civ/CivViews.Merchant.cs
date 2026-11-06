using UnityEngine;

public static partial class CivViews
{
    // Mercader medieval: un comerciante a pie con túnica larga del color de la civilización, barba y sombrero con pluma, que
    // guía de la cuerda a una mula de carga con alforjas, cajas y un fardo de telas. Se dibuja igual en todas las eras. Mira hacia +Z.
    static void BuildMerchantModel(Transform root, float s, Color civ)
    {
        Material cloth = LitMaterial(civ);
        Material clothDark = LitMaterial(Color.Lerp(civ, Color.black, 0.4f));
        Material skin = LitMaterial(new Color(0.92f, 0.74f, 0.58f));
        Material hose = LitMaterial(new Color(0.3f, 0.22f, 0.17f));
        Material boots = LitMaterial(new Color(0.16f, 0.11f, 0.08f));
        Material leather = LitMaterial(new Color(0.5f, 0.34f, 0.18f));
        Material wood = LitMaterial(new Color(0.42f, 0.28f, 0.15f));
        Material woodLight = LitMaterial(new Color(0.62f, 0.47f, 0.28f));
        Material sack = LitMaterial(new Color(0.78f, 0.7f, 0.55f));
        Material gold = LitMaterial(new Color(0.9f, 0.72f, 0.2f));
        Material beard = LitMaterial(new Color(0.32f, 0.2f, 0.12f));
        Material feather = LitMaterial(new Color(0.94f, 0.92f, 0.86f));
        Material rope = LitMaterial(new Color(0.7f, 0.6f, 0.4f));

        // ---- Mula de carga detrás del mercader
        float hz = -s * 0.35f;
        BuildHorseModel(root, s, hz);

        Piece(root, Cube, cloth, new Vector3(0f, s * 0.54f, hz), new Vector3(s * 0.38f, s * 0.05f, s * 0.52f));   // manta con el color de la civilización
        for (int side = -1; side <= 1; side += 2)
        {
            Piece(root, Sphere, leather, new Vector3(side * s * 0.21f, s * 0.42f, hz), new Vector3(s * 0.12f, s * 0.22f, s * 0.3f));          // alforja
            Piece(root, Cube, gold, new Vector3(side * s * 0.275f, s * 0.46f, hz), new Vector3(s * 0.01f, s * 0.05f, s * 0.05f));            // hebilla
        }
        Piece(root, Cube, woodLight, new Vector3(0f, s * 0.67f, hz - s * 0.12f), new Vector3(s * 0.24f, s * 0.17f, s * 0.24f));                // caja
        Piece(root, Cube, wood, new Vector3(0f, s * 0.76f, hz - s * 0.12f), new Vector3(s * 0.26f, s * 0.015f, s * 0.26f));                   // tapa
        GameObject bale = Piece(root, Cylinder, clothDark, new Vector3(0f, s * 0.7f, hz + s * 0.14f), new Vector3(s * 0.13f, s * 0.18f, s * 0.13f));
        bale.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);                                                                       // fardo de telas
        Piece(root, Sphere, sack, new Vector3(0f, s * 0.78f, hz + s * 0.08f), new Vector3(s * 0.14f, s * 0.14f, s * 0.14f));                   // saco

        // ---- Mercader a pie delante (f = tamaño de la figura)
        float f = s * 0.85f;
        float pz = s * 0.62f;
        Vector3 At(float x, float y, float z) { return new Vector3(x * f, y * f, pz + z * f); }
        Vector3 Sc(float x, float y, float z) { return new Vector3(x * f, y * f, z * f); }

        for (int side = -1; side <= 1; side += 2)
        {
            Piece(root, Sphere, boots, At(side * 0.09f, 0.05f, 0.04f), Sc(0.13f, 0.1f, 0.2f));
            Piece(root, Capsule, hose, At(side * 0.09f, 0.22f, 0f), Sc(0.11f, 0.14f, 0.11f));
        }

        // Túnica larga con dobladillo, cinturón con hebilla dorada, bolsa de monedas y cuello
        Piece(root, Cylinder, cloth, At(0f, 0.33f, 0f), Sc(0.36f, 0.12f, 0.3f));
        Piece(root, Capsule, cloth, At(0f, 0.5f, 0f), Sc(0.34f, 0.2f, 0.26f));
        Piece(root, Cylinder, gold, At(0f, 0.22f, 0f), Sc(0.37f, 0.012f, 0.31f));
        Piece(root, Cylinder, leather, At(0f, 0.44f, 0f), Sc(0.38f, 0.02f, 0.3f));
        Piece(root, Cube, gold, At(0f, 0.44f, 0.15f), Sc(0.06f, 0.05f, 0.02f));
        Piece(root, Sphere, leather, At(0.2f, 0.34f, 0.08f), Sc(0.1f, 0.12f, 0.1f));
        Piece(root, Cylinder, clothDark, At(0f, 0.64f, 0f), Sc(0.4f, 0.03f, 0.32f));

        // Brazo izquierdo caído y brazo derecho adelantado sujetando la cuerda
        Piece(root, Capsule, cloth, At(-0.21f, 0.5f, 0.02f), Sc(0.09f, 0.14f, 0.09f));
        Piece(root, Sphere, skin, At(-0.23f, 0.37f, 0.04f), Sc(0.08f, 0.08f, 0.08f));
        GameObject rightArm = Piece(root, Capsule, cloth, At(0.22f, 0.5f, 0.1f), Sc(0.09f, 0.14f, 0.09f));
        rightArm.transform.localRotation = Quaternion.Euler(50f, 0f, -10f);
        Vector3 hand = At(0.2f, 0.37f, 0.2f);
        Piece(root, Sphere, skin, hand, Sc(0.08f, 0.08f, 0.08f));

        // Cabeza con barba, sombrero redondo con ala y pluma
        Piece(root, Sphere, skin, At(0f, 0.78f, 0f), Sc(0.24f, 0.24f, 0.24f));
        Piece(root, Sphere, skin, At(0f, 0.76f, 0.12f), Sc(0.05f, 0.05f, 0.05f));
        Piece(root, Sphere, beard, At(0f, 0.7f, 0.07f), Sc(0.15f, 0.1f, 0.11f));
        Piece(root, Cylinder, clothDark, At(0f, 0.92f, 0f), Sc(0.3f, 0.07f, 0.3f));
        Piece(root, Cylinder, clothDark, At(0f, 0.87f, 0f), Sc(0.36f, 0.01f, 0.36f));
        Piece(root, Cylinder, gold, At(0f, 0.9f, 0f), Sc(0.31f, 0.012f, 0.31f));
        GameObject plume = Piece(root, Cube, feather, At(0.12f, 0.98f, -0.02f), Sc(0.02f, 0.15f, 0.05f));
        plume.transform.localRotation = Quaternion.Euler(0f, 0f, -25f);

        // ---- Cuerda desde su mano hasta el hocico de la mula
        ModTube(root, rope, hand, new Vector3(0f, s * 0.68f, hz + s * 0.66f), s * 0.012f);
    }
}
