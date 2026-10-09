using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Interfaz y control del jugador: selección con el ratón, órdenes, paneles de ciudad, unidad,
/// tecnologías, registro de eventos y etiquetas de ciudades.
///   Clic izquierdo   seleccionar unidad o ciudad
///   Clic derecho     mover / atacar (Mayús: mover y fundar ciudad al llegar)
///   B                fundar ciudad con el colono seleccionado
///   T                tecnologías      H  ayuda      C  centrar en la capital / selección
///   Espacio          pausa            1 2 3 4  velocidad x1 x2 x4 x8
/// </summary>
public class CivUI : MonoBehaviour
{
    CivGame game;

    Unit selUnit;
    City selCity;
    bool showHelp = true;
    bool showTech;
    float lastSpeed = 1f;

    // Modo carretera: 0 = no, 1 = esperando que señales la casilla de destino
    int roadPick;

    float s = 1f;                       // escala de la interfaz
    float vw, vh;                       // tamaño virtual de la pantalla
    readonly List<Rect> rects = new List<Rect>();
    List<Rect> lastRects = new List<Rect>();

    GUIStyle label, small, big, title, labelShadow;

    // ---------------------------------------------------------------- Entrada

    void Update()
    {
        if (game == null) game = CivGame.Instance;
        if (game == null || !game.ready) return;
        if (IntroVideo.Active) return;   // durante la introducción no se atiende el teclado ni el ratón del juego

        if (selUnit != null && !selUnit.alive) selUnit = null;
        if (selCity != null && !selCity.alive) selCity = null;

        // Al embarcar, la selección pasa al barco
        if (selUnit != null && selUnit.carriedBy != null) Select(selUnit.carriedBy, null);

        // El modo carretera solo tiene sentido con un colono propio seleccionado
        if (roadPick > 0 && (selUnit == null || !CanRoad(selUnit) || selUnit.owner != game.human))
            CancelRoadMode();

        HandleKeys();

        if (Input.GetMouseButtonDown(0) && !OverUi()) LeftClick();
        if (Input.GetMouseButtonDown(1) && !OverUi()) RightClick();

        // Anillo de selección
        if (selUnit != null && selUnit.selectionRing != null) selUnit.selectionRing.SetActive(true);
        if (selCity != null && selCity.selectionRing != null) selCity.selectionRing.SetActive(true);
    }

    void HandleKeys()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (game.gameSpeed > 0f) { lastSpeed = game.gameSpeed; game.gameSpeed = 0f; }
            else game.gameSpeed = lastSpeed > 0f ? lastSpeed : 1f;
        }

        if (Input.GetKeyDown(KeyCode.Alpha1)) game.gameSpeed = 1f;
        if (Input.GetKeyDown(KeyCode.Alpha2)) game.gameSpeed = 2f;
        if (Input.GetKeyDown(KeyCode.Alpha3)) game.gameSpeed = 4f;
        if (Input.GetKeyDown(KeyCode.Alpha4)) game.gameSpeed = 8f;

        if (Input.GetKeyDown(KeyCode.H)) showHelp = !showHelp;
        if (Input.GetKeyDown(KeyCode.T)) showTech = !showTech;

        if (Input.GetKeyDown(KeyCode.B) && selUnit != null && selUnit.owner == game.human)
            game.TryFound(selUnit);

        if (Input.GetKeyDown(KeyCode.J) && selUnit != null && selUnit.owner == game.human)
            game.TryJoin(selUnit);

        if (Input.GetKeyDown(KeyCode.U) && selUnit != null && selUnit.owner == game.human)
            game.TryUpgrade(selUnit);

        if (Input.GetKeyDown(KeyCode.K) && selUnit != null && selUnit.owner == game.human && selUnit.IsTrader)
            game.TryTrade(selUnit);

        if (Input.GetKeyDown(KeyCode.I) && selUnit != null && selUnit.owner == game.human && selUnit.kind == UnitKind.Worker && !selUnit.Moving)
            game.OrderIrrigate(selUnit, selUnit.cell);

        if (Input.GetKeyDown(KeyCode.M) && selUnit != null && selUnit.owner == game.human && selUnit.kind == UnitKind.Worker && !selUnit.Moving)
            game.OrderMine(selUnit, selUnit.cell);

        if (Input.GetKeyDown(KeyCode.F) && selUnit != null && selUnit.owner == game.human && selUnit.kind == UnitKind.Worker && !selUnit.Moving)
            game.OrderFarm(selUnit, selUnit.cell);

        if (Input.GetKeyDown(KeyCode.R) && selUnit != null && selUnit.owner == game.human && CanRoad(selUnit))
        {
            if (roadPick > 0) CancelRoadMode();
            else StartRoadMode();
        }

        if (Input.GetKeyDown(KeyCode.C))
        {
            if (selUnit != null) game.FocusOn(selUnit.cell);
            else if (selCity != null) game.FocusOn(selCity.cell);
            else if (game.human.cities.Count > 0) game.FocusOn(game.human.cities[0].cell);
        }
    }

    void Select(Unit u, City c)
    {
        if (selUnit != null && selUnit.selectionRing != null) selUnit.selectionRing.SetActive(false);
        if (selCity != null && selCity.selectionRing != null) selCity.selectionRing.SetActive(false);

        selUnit = u;
        selCity = c;
    }

    void StartRoadMode()
    {
        roadPick = 1;
    }

    void CancelRoadMode()
    {
        roadPick = 0;
    }

    // Clic en el mapa sobre la casilla de destino: el trabajador construye la carretera desde donde está hasta ella
    void RoadClick()
    {
        if (!PickPoint(out Vector3 dir)) return;

        int cell = game.grid.Nearest(dir);
        if (!game.grid.land[cell]) return; // sigue esperando una casilla de tierra

        City c = game.grid.cityAt[cell];
        if (c != null && c.owner != game.human)
        {
            game.AddLog("No puedes terminar una carretera en una ciudad ajena", Color.red);
            return;
        }

        if (game.grid.river[cell] && !game.human.Has(TechKind.Bridges))
        {
            game.AddLog("Necesitas descubrir la Construcción de puentes para hacer carreteras sobre un río (T)", Color.red);
            return; // sigue esperando otra casilla
        }

        if (!game.OrderRoad(selUnit, cell))
            game.AddLog("No se puede llegar a esa casilla por tierra desde ahí", Color.red);

        CancelRoadMode();
    }

    void LeftClick()
    {
        if (roadPick > 0) { RoadClick(); return; }

        if (!PickPoint(out Vector3 dir)) { Select(null, null); return; }

        Unit u = PickUnit(dir, true);
        if (u != null) { Select(u, null); return; }

        City c = PickCity(dir);
        if (c != null) { Select(null, c); return; }

        Select(null, null);
    }

    void RightClick()
    {
        if (roadPick > 0) { CancelRoadMode(); return; }
        if (selUnit == null || selUnit.owner != game.human) return;
        if (!PickPoint(out Vector3 dir)) return;

        // Ctrl + clic derecho sobre una casilla de tierra: el trabajador construye la carretera hasta ella
        if (CanRoad(selUnit) && (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)))
        {
            int roadCell = game.grid.Nearest(dir);
            City roadCity = game.grid.cityAt[roadCell];
            if (game.grid.land[roadCell] && (roadCity == null || roadCity.owner == game.human))
            {
                game.OrderRoad(selUnit, roadCell);
                return;
            }
        }

        // Ctrl + clic derecho de un colono sobre una ciudad propia: va y se une a ella (+1 de población)
        if (selUnit.kind == UnitKind.Settler && (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)))
        {
            City joinTarget = PickCity(dir);
            if (joinTarget != null && joinTarget.owner == game.human)
            {
                game.OrderJoin(selUnit, joinTarget);
                return;
            }
        }

        if (TryIrrigateClick(dir)) return;

        Unit enemyUnit = PickUnit(dir, false);

        // Unidad de tierra + clic derecho sobre un barco propio con sitio: va a embarcar
        if (enemyUnit != null && enemyUnit != selUnit && enemyUnit.owner == game.human && !selUnit.def.naval && enemyUnit.def.capacity > 0)
        {
            if (!game.OrderBoard(selUnit, enemyUnit)) game.AddLog("El barco ya va lleno", Color.red);
            return;
        }

        if (enemyUnit != null && enemyUnit.owner != game.human)
        {
            game.OrderAttack(selUnit, enemyUnit);
            return;
        }

        City enemyCity = PickCity(dir);

        if (enemyCity != null && enemyCity.owner != game.human)
        {
            game.OrderAttack(selUnit, enemyCity);
            return;
        }

        int cell = game.grid.Nearest(dir);

        // Barco con carga + clic derecho en una costa: va y desembarca allí
        if (selUnit.def.naval && selUnit.cargo != null && game.grid.land[cell])
        {
            game.OrderDisembark(selUnit, cell);
            return;
        }

        if (game.grid.land[cell] == selUnit.def.naval) return; // los barcos solo van al mar, el resto solo a tierra

        game.OrderMove(selUnit, cell);

        if (Input.GetKey(KeyCode.LeftShift) && selUnit.kind == UnitKind.Settler && game.CanFound(game.human, cell))
            selUnit.foundAt = cell;
    }

    // Mayús + clic derecho de un trabajador sobre una casilla: va y la mina (monte o montaña) o la irriga.
    // Alt + clic derecho: va y construye una granja.
    bool TryIrrigateClick(Vector3 dir)
    {
        if (selUnit.kind != UnitKind.Worker) return false;

        if (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt))
        {
            int farmCell = game.grid.Nearest(dir);
            if (!game.grid.CanFarm(farmCell))
                game.AddLog("No se puede hacer una granja ahí (mar, monte, montaña o ya hay una)", Color.red);
            else
                game.OrderFarm(selUnit, farmCell);
            return true;
        }

        if (!Input.GetKey(KeyCode.LeftShift)) return false;

        int cell = game.grid.Nearest(dir);

        if (game.grid.CanMine(cell))
        {
            game.OrderMine(selUnit, cell);
            return true;
        }

        if (!game.grid.CanIrrigate(cell))
        {
            game.AddLog("No se puede irrigar ni minar esa casilla (mar, ya irrigada o ya minada)", Color.red);
            return true;
        }

        game.OrderIrrigate(selUnit, cell);
        return true;
    }

    // Punto del planeta bajo el ratón, como dirección unitaria en el espacio local de la Tierra
    bool PickPoint(out Vector3 dir)
    {
        dir = Vector3.zero;
        Camera cam = Camera.main;
        if (cam == null) return false;

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        Transform et = game.EarthT;

        Vector3 o = et.InverseTransformPoint(ray.origin);
        Vector3 d = et.InverseTransformDirection(ray.direction).normalized;
        float R = game.grid.radius * 1.01f;

        float b = Vector3.Dot(o, d);
        float c = o.sqrMagnitude - R * R;
        float disc = b * b - c;
        if (disc < 0f) return false;

        float t = -b - Mathf.Sqrt(disc);
        if (t < 0f) return false;

        dir = (o + d * t).normalized;
        return true;
    }

    Unit PickUnit(Vector3 dir, bool onlyHuman)
    {
        float maxAngle = game.grid.angularSpacing * 0.75f;
        float best = maxAngle;
        Unit result = null;

        foreach (Unit u in game.allUnits)
        {
            if (!u.alive || u.carriedBy != null) continue;
            if (onlyHuman && u.owner != game.human) continue;

            float a = Mathf.Acos(Mathf.Clamp(Vector3.Dot(dir, u.currentDir), -1f, 1f));
            if (a < best) { best = a; result = u; }
        }

        return result;
    }

    City PickCity(Vector3 dir)
    {
        float best = game.grid.angularSpacing * 0.9f;
        City result = null;

        foreach (City c in game.allCities)
        {
            if (!c.alive) continue;

            float a = Mathf.Acos(Mathf.Clamp(Vector3.Dot(dir, game.grid.dir[c.cell]), -1f, 1f));
            if (a < best) { best = a; result = c; }
        }

        return result;
    }

    bool OverUi()
    {
        Vector2 m = new Vector2(Input.mousePosition.x / s, (Screen.height - Input.mousePosition.y) / s);
        foreach (Rect r in lastRects) if (r.Contains(m)) return true;
        return false;
    }

    // ---------------------------------------------------------------- Interfaz

    void InitStyles()
    {
        if (label != null) return;

        label = new GUIStyle(GUI.skin.label) { fontSize = 14, richText = true };
        label.normal.textColor = Color.white;

        labelShadow = new GUIStyle(label);
        labelShadow.normal.textColor = new Color(0f, 0f, 0f, 0.85f);

        small = new GUIStyle(label) { fontSize = 12 };
        big = new GUIStyle(label) { fontSize = 34, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        title = new GUIStyle(label) { fontSize = 18, fontStyle = FontStyle.Bold };
    }

    Rect Panel(Rect r)
    {
        rects.Add(r);
        GUI.Box(r, GUIContent.none);
        return r;
    }

    static string Hex(Color c) => ColorUtility.ToHtmlStringRGB(c);

    void OnGUI()
    {
        if (game == null || !game.ready) return;
        if (IntroVideo.Active) return;   // la introducción ocupa toda la pantalla

        InitStyles();

        s = Mathf.Max(0.7f, Screen.height / 800f);
        vw = Screen.width / s;
        vh = Screen.height / s;
        GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1f));

        rects.Clear();

        DrawCityLabels();
        DrawTileYields();
        DrawTopBar();
        DrawLog();
        if (showHelp) DrawHelp();
        if (selUnit != null) DrawUnitPanel();
        if (selCity != null) DrawCityPanel();
        if (showTech) DrawTechPanel();
        DrawBanner();

        lastRects = new List<Rect>(rects);
    }

    void DrawTopBar()
    {
        Rect r = Panel(new Rect(0, 0, vw, 34));
        Civ h = game.human;

        GUILayout.BeginArea(r);
        GUILayout.BeginHorizontal();

        GUILayout.Label("<b><color=#" + Hex(h.color) + ">" + h.name + "</color></b>", label, GUILayout.Width(110));
        IconLabel(ResourceIcons.Gold, Mathf.FloorToInt(h.gold) + " (" + Signed(h.goldRate) + "/s)", 150f, label);

        string research = h.researching
            ? GameDefs.Techs[h.currentTech].name + " " + Mathf.FloorToInt(100f * h.researchProgress / GameDefs.Techs[h.currentTech].cost) + "%"
            : "— (pulsa T)";
        IconLabel(ResourceIcons.Science, Signed(h.scienceRate) + "/s: " + research, 300f, label);

        GUILayout.Label("Ciudades: " + h.cities.Count + "  Unidades: " + h.units.Count, label, GUILayout.Width(200));
        GUILayout.FlexibleSpace();

        if (GUILayout.Button(game.gameSpeed <= 0f ? "▶" : "❚❚", GUILayout.Width(36))) HandleKeyPause();
        if (GUILayout.Button("x1", GUILayout.Width(34))) game.gameSpeed = 1f;
        if (GUILayout.Button("x2", GUILayout.Width(34))) game.gameSpeed = 2f;
        if (GUILayout.Button("x4", GUILayout.Width(34))) game.gameSpeed = 4f;
        if (GUILayout.Button("x8", GUILayout.Width(34))) game.gameSpeed = 8f;

        GUILayout.EndHorizontal();
        GUILayout.EndArea();
    }

    // Dibujito de un recurso seguido de su texto, en un bloque de ancho fijo
    static void IconLabel(Texture2D icon, string text, float width, GUIStyle style)
    {
        GUILayout.BeginHorizontal(GUILayout.Width(width));
        GUILayout.Label(icon, GUILayout.Width(18f), GUILayout.Height(18f));
        GUILayout.Label(text, style, GUILayout.Width(width - 22f));
        GUILayout.EndHorizontal();
    }

    void HandleKeyPause()
    {
        if (game.gameSpeed > 0f) { lastSpeed = game.gameSpeed; game.gameSpeed = 0f; }
        else game.gameSpeed = lastSpeed > 0f ? lastSpeed : 1f;
    }

    static string Signed(float v)
    {
        return (v >= 0f ? "+" : "") + v.ToString("0.0");
    }

    void DrawLog()
    {
        int shown = 0;
        float y = 42f;

        for (int i = game.log.Count - 1; i >= 0 && shown < 7; i--, shown++)
        {
            CivGame.LogEntry e = game.log[i];
            float age = Time.time - e.time;
            if (age > 25f) break;

            float alpha = Mathf.Clamp01((25f - age) / 5f);
            GUI.color = new Color(1f, 1f, 1f, alpha);

            string text = "<color=#" + Hex(e.color) + ">" + e.text + "</color>";
            Rect r = new Rect(vw - 460f, y, 450f, 22f);
            GUI.Label(new Rect(r.x + 1, r.y + 1, r.width, r.height), "<color=black>" + StripTags(e.text) + "</color>", small);
            GUI.Label(r, text, small);
            y += 20f;
        }

        GUI.color = Color.white;
    }

    static string StripTags(string s) => s;

    void DrawHelp()
    {
        Rect r = Panel(new Rect(10, vh - 176, 330, 166));
        GUILayout.BeginArea(new Rect(r.x + 8, r.y + 6, r.width - 16, r.height - 12));
        GUILayout.Label("<b>Controles</b>", label);
        GUILayout.Label("Clic izq.: seleccionar   Clic der.: mover / atacar", small);
        GUILayout.Label("Mayús + clic der. (colono): mover y fundar", small);
        GUILayout.Label("B: fundar ciudad   R o Ctrl+clic der.: carretera", small);
        GUILayout.Label("T: tecnologías   C: centrar", small);
        GUILayout.Label("Espacio: pausa   1-4: velocidad   H: ocultar", small);
        GUILayout.Label("Cursores / , . + - : mover y girar la cámara", small);
        GUILayout.Label("Esc: salir", small);
        GUILayout.EndArea();
    }

    // Solo los trabajadores construyen carreteras
    static bool CanRoad(Unit u)
    {
        return u.kind == UnitKind.Worker;
    }

    void DrawUnitPanel()
    {
        Unit u = selUnit;
        bool mine = u.owner == game.human;

        float panelHeight = mine && u.kind == UnitKind.Worker ? 340f : mine && u.kind == UnitKind.Settler ? 200f : mine && (u.def.capacity > 0 || u.IsTrader) ? (game.UpgradeTarget(u.owner, u.kind) != null ? 290f : 230f) : mine && game.UpgradeTarget(u.owner, u.kind) != null ? 225f : 162f;
        Rect r = Panel(new Rect(vw / 2f - 220f, vh - panelHeight - 10f, 440f, panelHeight));
        GUILayout.BeginArea(new Rect(r.x + 10, r.y + 8, r.width - 20, r.height - 16));

        GUILayout.Label("<b><color=#" + Hex(u.owner.color) + ">" + u.def.name + "</color></b> de " + u.owner.name, title);
        GUILayout.Label("Vida: " + Mathf.CeilToInt(u.hp) + "/" + u.def.hp
                        + (u.def.IsMilitary ? "    Ataque: " + u.def.attack + "    Defensa: " + u.def.defense : ""), label);

        if (mine)
        {
            if (u.kind == UnitKind.Settler)
            {
                bool can = !u.Moving && game.CanFound(u.owner, u.cell);
                GUI.enabled = can;
                if (GUILayout.Button("Fundar ciudad (B)")) game.TryFound(u);
                GUI.enabled = true;

                if (!can) GUILayout.Label("Necesita tierra libre a 4+ casillas de otra ciudad.", small);

                City here = game.grid.cityAt[u.cell];
                bool canJoin = !u.Moving && here != null && here.owner == u.owner;
                GUI.enabled = canJoin;
                if (GUILayout.Button("Unirse a la ciudad (+1 población) (J)")) game.TryJoin(u);
                GUI.enabled = true;
                if (!canJoin) GUILayout.Label("Unirse: entra en una ciudad propia (Ctrl + clic der. sobre ella para ir).", small);
            }
            else if (CanRoad(u))
            {
                bool hasAgriculture = game.human.Has(TechKind.Agriculture);
                bool canIrrigate = hasAgriculture && !u.Moving && game.grid.CanIrrigate(u.cell);
                if (u.irrigateAt >= 0)
                {
                    GUILayout.Label("Irrigando… " + Mathf.FloorToInt(u.irrigateProgress * 100f) + "%", small);
                }
                else
                {
                    GUI.enabled = canIrrigate;
                    if (GUILayout.Button("Irrigar esta casilla (I)")) game.OrderIrrigate(u, u.cell);
                    GUI.enabled = true;
                    if (!hasAgriculture) GUILayout.Label("Irrigar: descubre la Agricultura (T).", small);
                    else if (!canIrrigate) GUILayout.Label("Irrigar: tierra que no sea montaña (Mayús + clic der. en otra casilla).", small);
                }

                bool hasMining = game.human.Has(TechKind.Mining);
                bool canMine = hasMining && !u.Moving && game.grid.CanMine(u.cell);
                if (u.mineAt >= 0)
                {
                    GUILayout.Label("Construyendo mina… " + Mathf.FloorToInt(u.mineProgress * 100f) + "%", small);
                }
                else
                {
                    GUI.enabled = canMine;
                    if (GUILayout.Button("Construir mina aquí (M)")) game.OrderMine(u, u.cell);
                    GUI.enabled = true;
                    if (!hasMining) GUILayout.Label("Minas: descubre la Minería (T).", small);
                    else if (!canMine) GUILayout.Label("Minas: monte o montaña (Mayús + clic der. en otra casilla).", small);
                }

                bool hasPlow = game.human.Has(TechKind.HeavyPlow);
                bool canFarm = hasPlow && !u.Moving && game.grid.CanFarm(u.cell);
                if (u.farmAt >= 0)
                {
                    GUILayout.Label("Construyendo granja… " + Mathf.FloorToInt(u.farmProgress * 100f) + "%", small);
                }
                else
                {
                    GUI.enabled = canFarm;
                    if (GUILayout.Button("Construir granja aquí (F)")) game.OrderFarm(u, u.cell);
                    GUI.enabled = true;
                    if (!hasPlow) GUILayout.Label("Granjas: descubre el Arado y collera (T).", small);
                    else if (!canFarm) GUILayout.Label("Granjas: no en monte ni montaña (Alt + clic der. en otra casilla).", small);
                }

                if (u.roadGoal >= 0)
                {
                    GUILayout.Label("Construyendo carretera hacia " + game.RoadGoalName(u.roadGoal) + " (tramo " + Mathf.FloorToInt(u.roadProgress * 100f)
                                    + "%, faltan " + u.path.Count + " casillas)", small);
                }
                else if (roadPick == 1)
                {
                    GUILayout.Label("<b>Carretera:</b> haz clic en la casilla de destino (clic der.: cancelar)", small);
                }
                else if (!game.human.Has(TechKind.Wheel))
                {
                    GUILayout.Label("Caminos: descubre la Rueda (T).", small);
                }
                else if (GUILayout.Button("Construir carretera hasta una casilla (R)"))
                {
                    StartRoadMode();
                }
            }
            else if (u.IsTrader)
            {
                string home = u.homeCity != null && u.homeCity.alive ? u.homeCity.name : "—";
                GUILayout.Label("Ciudad de origen: " + home, label);

                string blocker = game.TradeBlocker(u);
                GUI.enabled = blocker == null;
                if (GUILayout.Button("Establecer ruta comercial (K)")) game.TryTrade(u);
                GUI.enabled = true;
                GUILayout.Label(blocker ?? "Une " + u.homeCity.name + " con esta ciudad: +" + Mathf.RoundToInt((u.kind == UnitKind.Merchant ? CivGame.MerchantTradeBonus : CivGame.TradeBonus) * 100f) + "% de oro y ciencia en ambas.", small);
            }
            else if (u.def.capacity > 0)
            {
                GUILayout.Label(u.cargo != null ? "Transporta: " + u.cargo.def.name : "Vacío (capacidad " + u.def.capacity + ")", label);
                GUILayout.Label("Embarcar: clic der. de una unidad sobre el barco.", small);
                GUILayout.Label("Desembarcar: clic der. del barco en una casilla de tierra.", small);
            }
            else
            {
                GUILayout.Label("Clic derecho: moverse o atacar al enemigo.", small);
            }

            UnitKind? upgrade = game.UpgradeTarget(u.owner, u.kind);
            if (upgrade != null)
            {
                string upgradeBlocker = game.UpgradeBlocker(u);
                GUI.enabled = upgradeBlocker == null;
                if (GUILayout.Button("Mejorar a " + GameDefs.Units[upgrade.Value].name + " (" + game.UpgradePrice(u) + " de oro) (U)")) game.TryUpgrade(u);
                GUI.enabled = true;
                if (upgradeBlocker != null) GUILayout.Label(upgradeBlocker, small);
            }

            if (GUILayout.Button("Detener"))
            {
                u.path.Clear();
                u.targetUnit = null;
                u.targetCity = null;
                u.foundAt = -1;
                u.joinAt = -1;
                u.boardTarget = null;
                u.disembarkAt = -1;
                game.CancelRoad(u);
                CancelRoadMode();
            }
        }

        GUILayout.EndArea();
    }

    const int ButtonsPerRow = 4;

    void DrawCityPanel()
    {
        City c = selCity;
        bool mine = c.owner == game.human;

        // Los botones de unidades y edificios se reparten en filas de ButtonsPerRow para que quepan todos
        int unitCount = 0, buildingCount = 0;
        if (mine)
        {
            foreach (UnitKind k in GameDefs.Units.Keys) if (game.Available(c, k)) unitCount++;
            foreach (BuildingKind k in GameDefs.Buildings.Keys) if (game.Available(c, k)) buildingCount++;
        }
        int unitRows = Mathf.Max(1, Mathf.CeilToInt(unitCount / (float)ButtonsPerRow));
        int buildingRows = Mathf.Max(1, Mathf.CeilToInt(buildingCount / (float)ButtonsPerRow));

        float height = mine ? 270f + (unitRows - 1 + buildingRows - 1) * 28f : 96f;
        Rect r = Panel(new Rect(vw / 2f - 270f, vh - height - 10f, 540f, height));
        GUILayout.BeginArea(new Rect(r.x + 10, r.y + 8, r.width - 20, r.height - 16));

        GUILayout.Label("<b><color=#" + Hex(c.owner.color) + ">" + c.name + "</color></b> — " + c.owner.name, title);
        GUILayout.Label("Población " + c.pop + "    Vida " + Mathf.CeilToInt(c.hp) + "/" + Mathf.CeilToInt(c.maxHp), label);

        if (mine)
        {
            float surplus = c.FoodSurplus;
            GUILayout.BeginHorizontal();
            IconLabel(ResourceIcons.Food, Signed(surplus) + "/s (" + Mathf.FloorToInt(c.food) + "/" + (12 + c.pop * 8) + ")", 150f, small);
            IconLabel(ResourceIcons.Production, c.prodRate.ToString("0.0") + "/s", 80f, small);
            IconLabel(ResourceIcons.Gold, c.goldRate.ToString("0.0") + "/s", 80f, small);
            IconLabel(ResourceIcons.Science, c.scienceRate.ToString("0.0") + "/s", 80f, small);
            GUILayout.EndHorizontal();

            string build = c.hasItem
                ? c.ItemName + " " + Mathf.FloorToInt(c.prodStock) + "/" + c.ItemCost
                : "Nada (elige abajo)";
            GUILayout.Label("<b>Construyendo:</b> " + build, label);

            if (c.hasItem)
            {
                int price = game.BuyPrice(c);
                GUI.enabled = game.human.gold >= price;
                if (GUILayout.Button("Comprar (" + price + " oro)")) game.Buy(c);
                GUI.enabled = true;
            }

            GUILayout.BeginHorizontal();
            int inRow = 0;
            foreach (KeyValuePair<UnitKind, UnitDef> kv in GameDefs.Units)
            {
                if (!game.Available(c, kv.Key)) continue;
                if (inRow == ButtonsPerRow) { GUILayout.EndHorizontal(); GUILayout.BeginHorizontal(); inRow = 0; }
                inRow++;
                if (GUILayout.Button(kv.Value.name + " (" + kv.Value.cost + ")")) game.SetProduction(c, kv.Key);
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            bool any = false;
            inRow = 0;
            foreach (KeyValuePair<BuildingKind, BuildingDef> kv in GameDefs.Buildings)
            {
                if (!game.Available(c, kv.Key)) continue;
                any = true;
                if (inRow == ButtonsPerRow) { GUILayout.EndHorizontal(); GUILayout.BeginHorizontal(); inRow = 0; }
                inRow++;
                if (GUILayout.Button(kv.Value.name + " (" + c.BuildingCost(kv.Key) + ")")) game.SetProduction(c, kv.Key);
            }
            if (!any) GUILayout.Label("Sin edificios disponibles", small);
            GUILayout.EndHorizontal();

            List<string> partners = new List<string>();
            foreach (City p in c.tradePartners) if (p.alive && p.owner == c.owner) partners.Add(p.name);
            if (partners.Count > 0)
                GUILayout.Label("Comercio con: " + string.Join(", ", partners) + " (+" + Mathf.RoundToInt(game.TradeBonusOf(c) * 100f) + "% oro y ciencia)", small);

            if (c.buildings.Count > 0)
            {
                List<string> names = new List<string>();
                foreach (BuildingKind b in c.buildings) names.Add(GameDefs.Buildings[b].name);
                GUILayout.Label("Edificios: " + string.Join(", ", names), small);
            }
        }

        GUILayout.EndArea();
    }

    // Panel de tecnologías (T): solo las que se pueden investigar ahora (las ya descubiertas no se muestran)
    void DrawTechPanel()
    {
        Civ h = game.human;

        List<KeyValuePair<TechKind, TechDef>> shown = new List<KeyValuePair<TechKind, TechDef>>();
        foreach (KeyValuePair<TechKind, TechDef> kv in GameDefs.Techs)
            if (h.CanResearch(kv.Key)) shown.Add(kv);

        Rect r = Panel(new Rect(vw / 2f - 240f, 60f, 480f, 70f + Mathf.Max(1, shown.Count) * 34f));

        GUILayout.BeginArea(new Rect(r.x + 12, r.y + 8, r.width - 24, r.height - 16));
        GUILayout.Label("<b>Tecnologías</b>", title);

        foreach (KeyValuePair<TechKind, TechDef> kv in shown)
        {
            GUILayout.BeginHorizontal();

            bool current = h.researching && h.currentTech == kv.Key;

            GUILayout.Label((current ? "▶ " : "") + kv.Value.name + " — " + kv.Value.unlocks, label, GUILayout.Width(310));

            string text = current
                ? Mathf.FloorToInt(100f * h.researchProgress / kv.Value.cost) + "%"
                : "Investigar (" + kv.Value.cost + ")";

            if (GUILayout.Button(text, GUILayout.Width(120))) game.StartResearch(h, kv.Key);

            GUILayout.EndHorizontal();
        }

        GUILayout.EndArea();
    }

    void DrawBanner()
    {
        string text = null;

        if (game.winner != null)
            text = game.winner == game.human ? "¡VICTORIA!" : game.winner.name + " gobierna el mundo";
        else if (!game.human.alive)
            text = "Has sido eliminado";

        if (text == null) return;

        Rect r = new Rect(0, vh * 0.3f, vw, 60f);
        GUI.Label(new Rect(r.x + 2, r.y + 2, r.width, r.height), text, labelShadow);
        GUI.Label(r, text, big);
    }

    // Vista de la ciudad: recursos que da cada casilla de su territorio.
    // Co = comida, Pr = producción, Or = oro, Ci = ciencia. Las que trabaja la ciudad van a todo color; el resto, atenuadas.
    void DrawTileYields()
    {
        City city = FreeCamera.ViewedCity;
        Camera cam = Camera.main;
        if (city == null || !city.alive || cam == null) return;

        Transform et = game.EarthT;
        WorldGrid grid = game.grid;

        DrawCityTotals(city);

        GUIStyle centered = new GUIStyle(small) { alignment = TextAnchor.MiddleCenter, fontSize = 11 };
        GUIStyle shadow = new GUIStyle(centered);
        shadow.normal.textColor = Color.black;

        foreach (int cell in city.tiles)
        {
            if (grid.tileCity[cell] != city) continue;

            Vector3 world = et.TransformPoint(grid.pos[cell]);
            Vector3 normal = et.TransformDirection(grid.dir[cell]);
            if (Vector3.Dot(normal, (cam.transform.position - world).normalized) < 0.1f) continue;

            Vector3 sp = cam.WorldToScreenPoint(world + normal * grid.spacing * 0.25f);
            if (sp.z <= 0f) continue;

            // Rendimiento de la casilla: terreno (con irrigación) + 1 de oro y de ciencia si tiene carretera
            bool isCenter = cell == city.cell;
            Vector3 y = grid.TileYield(cell, city.owner);
            if (isCenter) y += Vector3.one; // la ciudad aporta +1 de cada recurso por su casilla central
            float sci = 0f;
            if (grid.HasRoadAt(cell)) { y.z += 1f; sci += 1f; }

            bool active = isCenter || city.worked.Contains(cell);

            // Dibujitos de cada recurso seguidos de su cantidad (los que valen 0 no se muestran)
            List<IconValue> items = new List<IconValue>();
            if (y.x > 0f) items.Add(new IconValue(ResourceIcons.Food, y.x.ToString("0.#")));
            if (y.y > 0f) items.Add(new IconValue(ResourceIcons.Production, y.y.ToString("0.#")));
            if (y.z > 0f) items.Add(new IconValue(ResourceIcons.Gold, y.z.ToString("0.#")));
            if (sci > 0f) items.Add(new IconValue(ResourceIcons.Science, sci.ToString("0.#")));

            float x = sp.x / s;
            float yy = (Screen.height - sp.y) / s;

            if (items.Count == 0) { GUI.Label(new Rect(x - 20f, yy - 9f, 40f, 18f), "—", centered); continue; }
            DrawIconRow(x, yy, items, 14f, active ? 1f : 0.4f, centered, shadow);
        }
    }

    struct IconValue
    {
        public Texture2D icon;
        public string text;
        public IconValue(Texture2D icon, string text) { this.icon = icon; this.text = text; }
    }

    // Fila centrada en (cx, cy): dibujito + cifra, uno tras otro, con sombra para leerse sobre cualquier terreno
    void DrawIconRow(float cx, float cy, List<IconValue> items, float iconSize, float alpha, GUIStyle textStyle, GUIStyle shadowStyle)
    {
        const float gap = 2f, spacing = 6f;

        float total = 0f;
        float[] widths = new float[items.Count];
        for (int i = 0; i < items.Count; i++)
        {
            widths[i] = textStyle.CalcSize(new GUIContent(items[i].text)).x;
            total += iconSize + gap + widths[i] + (i > 0 ? spacing : 0f);
        }

        Color oldColor = GUI.color;
        float x = cx - total / 2f;

        for (int i = 0; i < items.Count; i++)
        {
            GUI.color = new Color(1f, 1f, 1f, alpha);
            GUI.DrawTexture(new Rect(x, cy - iconSize / 2f, iconSize, iconSize), items[i].icon);
            x += iconSize + gap;

            Rect t = new Rect(x, cy - 9f, widths[i] + 2f, 18f);
            GUI.color = new Color(1f, 1f, 1f, alpha);
            GUI.Label(new Rect(t.x + 1, t.y + 1, t.width, t.height), items[i].text, shadowStyle);
            GUI.Label(t, items[i].text, textStyle);

            x += widths[i] + spacing;
        }

        GUI.color = oldColor;
    }

    // Recuadro con los recursos totales de la ciudad en la vista de la ciudad
    void DrawCityTotals(City city)
    {
        Rect r = Panel(new Rect(vw / 2f - 250f, 42f, 500f, 78f));

        GUIStyle centered = new GUIStyle(label) { alignment = TextAnchor.MiddleCenter };
        GUIStyle centeredSmall = new GUIStyle(small) { alignment = TextAnchor.MiddleCenter };

        GUI.Label(new Rect(r.x, r.y + 4f, r.width, 22f),
                  "<b><color=#" + Hex(city.owner.color) + ">" + city.name + "</color></b> — recursos totales", centered);

        GUIStyle value = new GUIStyle(label) { alignment = TextAnchor.MiddleCenter, fontSize = 16, fontStyle = FontStyle.Bold };
        GUIStyle valueShadow = new GUIStyle(value);
        valueShadow.normal.textColor = new Color(0f, 0f, 0f, 0.8f);

        List<IconValue> items = new List<IconValue>
        {
            new IconValue(ResourceIcons.Food, city.foodRate.ToString("0.0") + " (" + Signed(city.FoodSurplus) + ")"),
            new IconValue(ResourceIcons.Production, city.prodRate.ToString("0.0")),
            new IconValue(ResourceIcons.Gold, city.goldRate.ToString("0.0")),
            new IconValue(ResourceIcons.Science, city.scienceRate.ToString("0.0"))
        };
        DrawIconRow(r.x + r.width / 2f, r.y + 38f, items, 22f, 1f, value, valueShadow);

        GUI.Label(new Rect(r.x, r.y + 52f, r.width, 20f),
                  "<color=#BBBBBB>por segundo · entre paréntesis, comida neta · casillas trabajadas: " + (city.worked.Count + 1) + "</color>", centeredSmall);
    }

    // Nombre y población sobre cada ciudad visible
    void DrawCityLabels()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        Transform et = game.EarthT;

        foreach (City c in game.allCities)
        {
            if (!c.alive) continue;

            Vector3 world = et.TransformPoint(game.grid.pos[c.cell]);
            Vector3 normal = et.TransformDirection(game.grid.dir[c.cell]);
            Vector3 top = world + normal * game.grid.spacing * 1.3f;

            // Solo las del hemisferio visible
            if (Vector3.Dot(normal, (cam.transform.position - world).normalized) < 0.15f) continue;

            Vector3 sp = cam.WorldToScreenPoint(top);
            if (sp.z <= 0f) continue;

            float x = sp.x / s;
            float y = (Screen.height - sp.y) / s;

            string text = "<color=#" + Hex(c.owner.color) + "><b>" + c.name + " " + c.pop + "</b></color>";
            Rect r = new Rect(x - 70f, y - 10f, 140f, 20f);

            GUIStyle centered = new GUIStyle(small) { alignment = TextAnchor.MiddleCenter };
            GUIStyle centeredShadow = new GUIStyle(centered);
            centeredShadow.normal.textColor = Color.black;

            GUI.Label(new Rect(r.x + 1, r.y + 1, r.width, r.height), "<color=black>" + c.name + " " + c.pop + "</color>", centeredShadow);
            GUI.Label(r, text, centered);
        }
    }
}
