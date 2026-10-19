using UnityEngine;

// ---------------------------------------------------------------------------------------------------------------------
// Guía del juego dentro de la partida (tecla F1): controles, conceptos, edades, tecnologías, unidades, edificios,
// maravillas y reglas. Las tablas salen de GuideData (generado a partir del código del juego). Mientras está abierta,
// la partida se pausa.
// ---------------------------------------------------------------------------------------------------------------------
public partial class CivUI
{
    bool showGuide;
    int guideTab;
    Vector2 guideScroll;
    float guideSavedSpeed = 1f;
    GUIStyle guideText, guideCell, guideHead, guideEra;

    static readonly string[] GuideTabs = { "Controles", "Conceptos", "Edades", "Tecnologías", "Unidades", "Edificios", "Maravillas", "Reglas" };

    static readonly string[][] GuideKeys =
    {
        new[] { "Clic izquierdo", "Seleccionar una unidad o ciudad" },
        new[] { "Clic derecho", "Mover la unidad seleccionada o atacar a un enemigo" },
        new[] { "Mayús + clic derecho", "Colono: ir y fundar una ciudad al llegar. Trabajador: minar o irrigar la casilla" },
        new[] { "Alt + clic derecho", "Trabajador: construir una granja en la casilla" },
        new[] { "B", "Colono: fundar una ciudad donde está" },
        new[] { "J", "Colono: unirse a la ciudad en la que está (+1 habitante)" },
        new[] { "R  o  Ctrl + clic derecho", "Trabajador: construir una carretera hasta una casilla" },
        new[] { "V", "Trabajador: mejorar una carretera a ferrocarril" },
        new[] { "I  /  M  /  F", "Trabajador: irrigar / minar / hacer una granja. Con una unidad militar, F la fortifica" },
        new[] { "U", "Mejorar una unidad parada en una ciudad con Academia" },
        new[] { "K", "Caravana o Mercader: crear una ruta comercial" },
        new[] { "T", "Panel de tecnologías" },
        new[] { "Mayús + T", "Atajo de pruebas: desarrolla todas las tecnologías de tu edad (otra vez: el cambio de era)" },
        new[] { "D", "Panel de diplomacia" },
        new[] { "N", "Mostrar u ocultar el minimapa" },
        new[] { "C", "Centrar la cámara en tu capital o en lo seleccionado" },
        new[] { "Cursores, coma y punto, rueda", "Mover y girar la cámara, y zoom" },
        new[] { "Espacio", "Pausa" },
        new[] { "F1", "Esta guía" },
        new[] { "F2", "Activar o silenciar los efectos de sonido" },
        new[] { "F5  /  F9", "Guardar / cargar partida" },
        new[] { "H", "Mostrar u ocultar la ayuda en pantalla" },
        new[] { "Esc", "Menú de pausa: reanudar, guardar y salir" }
    };

    static readonly string[] GuideConcepts =
    {
        "<b>Empezar.</b> Elige cuántas civilizaciones enemigas habrá (0 a 5, siempre controladas por la IA) y la dificultad. Empiezas con una capital y un guerrero, en paz con todas. Lo primero suele ser fundar más ciudades: construye un Colono (cuesta un habitante) y llévalo a tierra libre, al menos a cuatro casillas de otra ciudad.",
        "<b>Ritmo.</b> Todo avanza despacio a propósito: producción, investigación, movimiento y combate tardan tres veces más que en una versión rápida. Espacio pausa el juego; F5 guarda y F9 carga.",
        "<b>Comida y población.</b> Una ciudad crece al acumular 12 + 8 × habitantes de comida. Sin la tecnología Fuego no pasa de 3 habitantes.",
        "<b>Producción.</b> Una ciudad construye una cosa a la vez: unidad, edificio o maravilla. Puedes comprar lo que falta pagando el doble en oro.",
        "<b>Oro y ciencia.</b> El oro sale de los mares, mercados, bancos, carreteras y comercio. La ciencia investiga la tecnología elegida en el panel (T); bibliotecas, universidades, observatorios y varias maravillas la multiplican.",
        "<b>Territorio.</b> Cada ciudad domina las casillas de su alrededor; el radio crece con la población (3 casillas desde 4 habitantes y 4 desde 8). Cada 10 habitantes añaden un barrio.",
        "<b>Carreteras.</b> Se desbloquean con la Rueda y las hacen los Trabajadores. Las unidades de tierra van el doble de rápido por ellas y por los ríos, y cada casilla de ciudad con carretera da +1 de oro y +1 de ciencia. Cruzar un río exige Construcción de puentes. El ferrocarril (tecnología Ferrocarril) cuadruplica la velocidad base y da +1 de producción por casilla.",
        "<b>Felicidad.</b> Cada ciudad parte de +6 y resta 0,5 por habitante, 0,4 por cada ciudad que tengas por encima de 4 y 1 por cada guerra en curso (hasta 3). Suman los Baños públicos (+3), Biblioteca, Universidad, Mercado, Banco y Periódico (+1), las maravillas (+3), los edificios de felicidad, varias tecnologías y los soldados dentro de la ciudad (+0,5 cada uno, hasta 3).",
        "Feliz (4 o más): +10 % de producción, ciencia, oro y crecimiento. Contenta (0 a 4): sin cambios. Descontenta (-3 a 0): -25 % de producción, ciencia y oro, y crece a la mitad. Disturbios (menos de -3): no produce ni crece. Una ciudad descontenta lleva una marca roja junto a su nombre.",
        "<b>Niebla de guerra.</b> Solo ves en torno a tus unidades (2 casillas; 3 los barcos y aviones) y tus ciudades (4). Lo explorado se ve atenuado y lo desconocido, casi negro; las unidades enemigas fuera de tu visión no se dibujan. El Satélite y el Programa Apolo descubren todo el mapa y la Omnisciencia da visión total. El minimapa (N) muestra lo que sabes y sirve para mover la cámara con un clic."
    };

    static readonly string[] GuideRules =
    {
        "<b>Atacar.</b> Selecciona una unidad militar y haz clic derecho sobre un enemigo o su ciudad. Solo puedes atacar a quien esté en guerra contigo. El daño de un golpe se multiplica por 6 y se divide por (6 + defensa del objetivo). El alcance es la distancia en casillas desde la que ataca (1 es cuerpo a cuerpo). Las unidades paradas responden solas a los enemigos cercanos y el defensor contraataca.",
        "<b>Fortificar.</b> Con una unidad militar parada, pulsa F: tarda 10 segundos y da +25 % de defensa, o +50 % dentro de una ciudad propia con Muro. Se pierde al mover o atacar. Se dibuja una defensa alrededor y una bandera de tu color.",
        "<b>Murallas.</b> El Muro da +25 de vida a la ciudad y más defensa contra asedios. Los Escudos de energía suman +100 de vida y la Gran Muralla +4 de defensa a todas tus ciudades. Las ciudades disparan a los enemigos que se acercan. Una ciudad cae cuando su vida llega a cero.",
        "<b>Mejorar unidades.</b> Una unidad parada en una ciudad con Academia se mejora con U pagando la diferencia de coste. Las versiones antiguas se vuelven obsoletas y dejan de ofrecerse.",
        "<b>Mar.</b> Los barcos solo navegan por el mar y se construyen en ciudades costeras. El Barco de Vela, el Galeón y el Barco de vapor transportan 1, 3 y 4 unidades de tierra. Para embarcar, clic derecho con una unidad de tierra sobre un barco propio con sitio; para desembarcar, clic derecho con el barco cargado sobre una costa. Los aviones vuelan sobre tierra y mar.",
        "<b>Diplomacia (D).</b> Cada pareja de civilizaciones está en Guerra, Paz o Alianza. Empiezas en paz con todas. En Paz nadie ataca y hay una tregua de 120 segundos en la que no se puede declarar la guerra; en Alianza, además, compartís la visión del mapa. Puedes proponer la paz o una alianza, declarar la guerra o romper una alianza. La IA acepta la paz si va perdiendo o está en guerra con varias, y una alianza si tenéis un enemigo común. Te propone paz o alianza con avisos que aceptas o rechazas, y a veces declara la guerra a un vecino más débil. Con las Naciones Unidas en el mundo, todas aceptan la paz.",
        "<b>La IA.</b> Investiga priorizando economía y cambios de era, funda hasta 8 ciudades (5 en Fácil, 10 en Difícil), mejora el terreno con trabajadores, coloniza e invade otras islas por mar, refuerza las ciudades amenazadas y fortifica sus guarniciones. Fácil: -30 % de producción, ciencia, oro y crecimiento, y tarda más en atacar. Difícil: +30 % y ataca antes.",
        "<b>Victoria.</b> Gana la última civilización viva o la primera que descubre la Omnisciencia (exige las otras 62 tecnologías). Al terminar, si te eliminan o abandonas, aparece la lista de civilizaciones con puntuación: 100 por ciudad, 20 por habitante, 40 por tecnología, 15 por edificio, 165 por maravilla, 5 por unidad y 1 por cada 20 de oro."
    };

    void ToggleGuide()
    {
        showGuide = !showGuide;
        if (showGuide)
        {
            guideSavedSpeed = game.gameSpeed;   // se restaura tal cual al cerrar
            game.gameSpeed = 0f;
            guideScroll = Vector2.zero;
        }
        else
        {
            game.gameSpeed = guideSavedSpeed;
        }
    }

    void InitGuideStyles()
    {
        if (guideText != null) return;

        guideText = new GUIStyle(label) { wordWrap = true, fontSize = 14 };
        guideCell = new GUIStyle(small) { wordWrap = true, fontSize = 13 };
        guideHead = new GUIStyle(small) { fontStyle = FontStyle.Bold, fontSize = 12 };
        guideHead.normal.textColor = new Color(0.62f, 0.72f, 0.82f);
        guideEra = new GUIStyle(title) { fontSize = 17 };
    }

    static readonly Color[] GuideEraColors =
    {
        new Color(0.86f, 0.63f, 0.34f), new Color(0.56f, 0.64f, 0.77f), new Color(0.87f, 0.53f, 0.38f),
        new Color(0.43f, 0.66f, 0.91f), new Color(0.72f, 0.56f, 0.95f)
    };

    void GuideRow(float[] widths, params string[] cells)
    {
        GUILayout.BeginHorizontal();
        for (int i = 0; i < cells.Length; i++)
            GUILayout.Label(cells[i], guideCell, GUILayout.Width(widths[i]));
        GUILayout.EndHorizontal();
    }

    void GuideHeadRow(float[] widths, params string[] cells)
    {
        GUILayout.BeginHorizontal();
        for (int i = 0; i < cells.Length; i++)
            GUILayout.Label(cells[i].ToUpper(), guideHead, GUILayout.Width(widths[i]));
        GUILayout.EndHorizontal();
        GUILayout.Space(2);
    }

    void DrawGuide()
    {
        if (!showGuide) return;

        InitGuideStyles();

        // Velo oscuro (también bloquea los clics en el resto de la interfaz)
        Rect all = new Rect(0, 0, vw, vh);
        rects.Add(all);
        GUI.color = new Color(0f, 0f, 0f, 0.7f);
        GUI.DrawTexture(all, Texture2D.whiteTexture);
        GUI.color = Color.white;

        float margin = 36f;
        Rect r = Panel(new Rect(margin, margin, vw - margin * 2f, vh - margin * 2f));

        GUI.Label(new Rect(r.x + 16f, r.y + 8f, r.width - 140f, 30f), "<b>Guía de Civilogenia</b>", guideEra);
        if (GUI.Button(new Rect(r.xMax - 150f, r.y + 10f, 134f, 26f), "Cerrar (F1)")) { ToggleGuide(); return; }

        GUILayout.BeginArea(new Rect(r.x + 16f, r.y + 44f, r.width - 32f, r.height - 56f));
        int newTab = GUILayout.Toolbar(guideTab, GuideTabs);
        if (newTab != guideTab) { guideTab = newTab; guideScroll = Vector2.zero; }
        GUILayout.Space(6);

        guideScroll = GUILayout.BeginScrollView(guideScroll);

        switch (guideTab)
        {
            case 0: DrawGuideKeys(); break;
            case 1: DrawGuideParagraphs(GuideConcepts); break;
            case 2: DrawGuideEras(); break;
            case 3: DrawGuideTechs(); break;
            case 4: DrawGuideUnits(); break;
            case 5: DrawGuideBuildings(false); break;
            case 6: DrawGuideBuildings(true); break;
            default: DrawGuideParagraphs(GuideRules); break;
        }

        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    void DrawGuideParagraphs(string[] paragraphs)
    {
        foreach (string p in paragraphs)
        {
            GUILayout.Label(p, guideText);
            GUILayout.Space(8);
        }
    }

    void DrawGuideKeys()
    {
        float[] w = { 230f, 640f };
        GuideHeadRow(w, "Tecla", "Qué hace");
        foreach (string[] k in GuideKeys) GuideRow(w, "<b>" + k[0] + "</b>", k[1]);
    }

    void DrawGuideEras()
    {
        GUILayout.Label("Cada edad se cierra con el <b>Cambio de era</b>, que exige todas las demás tecnologías de esa edad. Al descubrirlo suena un vídeo y cambia el aspecto de tus unidades, carreteras y ciudades.", guideText);
        GUILayout.Space(10);

        for (int e = 0; e < GuideData.Eras.Length; e++)
        {
            int count = 0;
            string closer = "";
            foreach (GuideData.TechRow t in GuideData.Techs)
            {
                if (t.era != e) continue;
                count++;
                if (t.closer) closer = t.name + " (" + t.cost + ")";
            }

            GUIStyle st = new GUIStyle(guideEra);
            st.normal.textColor = GuideEraColors[e];
            GUILayout.Label(GuideData.Eras[e], st);
            GUILayout.Label(count + " tecnologías. Para salir: " + closer + ".", guideText);
            GUILayout.Space(8);
        }
    }

    void DrawGuideTechs()
    {
        float[] w = { 190f, 56f, 260f, 380f };

        for (int e = 0; e < GuideData.Eras.Length; e++)
        {
            GUIStyle st = new GUIStyle(guideEra);
            st.normal.textColor = GuideEraColors[e];
            GUILayout.Space(6);
            GUILayout.Label(GuideData.Eras[e], st);
            GuideHeadRow(w, "Tecnología", "Coste", "Requiere", "Qué da");

            foreach (GuideData.TechRow t in GuideData.Techs)
                if (t.era == e) GuideRow(w, "<b>" + t.name + "</b>" + (t.closer ? "  (cierra la edad)" : ""), t.cost, t.requires, t.effect);
        }
    }

    void DrawGuideUnits()
    {
        GUILayout.Label("Ataque y defensa: el daño se multiplica por 6 y se divide por (6 + defensa del objetivo). Alcance: casillas desde las que ataca (1 es cuerpo a cuerpo). Velocidad: casillas por segundo en llano.", guideText);
        GUILayout.Space(6);

        float[] w = { 160f, 50f, 46f, 40f, 54f, 58f, 66f, 54f, 200f, 150f };
        GuideHeadRow(w, "Unidad", "Tipo", "Coste", "Vida", "Ataque", "Defensa", "Veloc.", "Alcance", "Requiere", "Notas");
        foreach (GuideData.UnitRow u in GuideData.Units)
            GuideRow(w, "<b>" + u.name + "</b>", u.type, u.cost, u.hp, u.attack, u.defense, u.speed, u.range, u.requires, u.notes);
    }

    void DrawGuideBuildings(bool wonders)
    {
        GUILayout.Label(wonders
            ? "Solo puede existir <b>una de cada</b> en todo el mundo: la primera civilización que la termina se la queda. Su efecto vale para todas las ciudades de su dueña, salvo que se indique otra cosa."
            : "Cada ciudad puede tener uno de cada. Construcción (-10 %), Acero y hormigón (-15 %) y Nanotecnología (-25 %) los abaratan.", guideText);
        GUILayout.Space(6);

        float[] w = { 220f, 56f, 390f, 220f };
        GuideHeadRow(w, wonders ? "Maravilla" : "Edificio", "Coste", "Efecto", "Requiere");
        foreach (GuideData.BuildingRow b in GuideData.Buildings)
            if (b.wonder == wonders) GuideRow(w, "<b>" + b.name + "</b>", b.cost, b.effect, b.requires);
    }
}
