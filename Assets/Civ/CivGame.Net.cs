using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ---------------------------------------------------------------------------------------------------------------------
// Multijugador (parte del juego). Modelo: anfitrión autoritativo.
//   Anfitrión: simula toda la partida (también las civilizaciones de los demás jugadores y la IA), ejecuta las órdenes que
//              llegan de los clientes como si las diera esa civilización y envía el estado cada 0,25 s.
//   Cliente:   no simula nada. Al unirse recibe el estado completo (mismo formato que el guardado) y reconstruye la partida;
//              después aplica las instantáneas, mueve las unidades entre una y otra, y manda sus órdenes al anfitrión.
// Todas las órdenes del jugador (mover, producir, investigar, diplomacia...) pasan por "NetCmd": en el cliente se envían, en el
// resto de casos devuelve false y la orden se ejecuta normalmente.
// ---------------------------------------------------------------------------------------------------------------------

[Serializable]
public class NetCommand
{
    public string op;
    public int a = -1, b = -1, c = -1;
}

[Serializable]
public class StartMsg
{
    public int civId;
    public CivSaveData data;
}

public partial class CivGame
{
    public const int MaxNetPlayers = 8;
    const float SnapshotInterval = 0.25f;   // segundos entre instantáneas
    const float StructureInterval = 2f;     // cada cuánto se reenvía también lo que cambia poco (territorio, edificios, carreteras...)

    [NonSerialized] bool netGame;           // la partida es multijugador (anfitrión o cliente)
    int nextNetId = 1;                      // siguiente identificador de unidad o ciudad
    NetPeer execPeer;                       // anfitrión: jugador remoto cuya orden se está ejecutando ahora
    readonly List<NetPeer> netPlayers = new List<NetPeer>();   // anfitrión: jugadores remotos de la partida, en el orden de sus civilizaciones
    float snapTimer, structTimer;
    bool netStructDirty;
    bool lobbyDirty;
    string pendingSnapshot;                 // cliente: última instantánea recibida y aún sin aplicar

    [NonSerialized] public string netLobbyText = "";   // cliente: lista de jugadores que manda el anfitrión
    [NonSerialized] public bool netJoining;            // cliente: ha empezado la partida y se está cargando
    [NonSerialized] public bool netLost;               // se perdió la conexión

    public bool IsMultiplayer { get { return netGame; } }
    public bool IsNetClient { get { return netGame && CivNet.Instance != null && CivNet.Instance.IsClient; } }

    /// <summary>true si la civilización es la del jugador que tiene delante este ordenador.</summary>
    public bool IsLocal(Civ civ) { return civ != null && civ == human; }

    // ---------------------------------------------------------------- Avisos

    /// <summary>Mensaje para el jugador que lleva esa civilización: al registro local o por red al jugador remoto. La IA no recibe nada.</summary>
    void Notify(Civ civ, string text, Color color)
    {
        if (civ == null || IsNetClient) return;   // el cliente no genera avisos de la simulación: los manda el anfitrión

        if (civ == human) { AddLog(text, color); return; }

        if (civ.remote)
        {
            NetPeer p = PeerOfCiv(civ);
            if (p != null) CivNet.Instance.SendTo(p, NetMsg.Log, JsonUtility.ToJson(new LogMsg { text = text, r = color.r, g = color.g, b = color.b }));
        }
    }

    // Mientras se ejecuta la orden de un jugador remoto, lo que el juego escribe en el registro es para él
    bool NetRedirectLog(string text, Color color)
    {
        if (execPeer == null) return false;
        CivNet.Instance.SendTo(execPeer, NetMsg.Log, JsonUtility.ToJson(new LogMsg { text = text, r = color.r, g = color.g, b = color.b }));
        return true;
    }

    NetPeer PeerOfCiv(Civ civ)
    {
        CivNet net = CivNet.Instance;
        if (net == null || civ == null) return null;
        foreach (NetPeer p in net.PeerList()) if (!p.dead && p.civId == civ.id) return p;
        return null;
    }

    // ---------------------------------------------------------------- Órdenes (cliente)

    /// <summary>Cliente: manda la orden al anfitrión y devuelve true (la orden no se ejecuta aquí). En cualquier otro caso devuelve false.</summary>
    bool NetCmd(string op, int a = -1, int b = -1, int c = -1)
    {
        if (!IsNetClient || execPeer != null) return false;

        CivNet.Instance.SendToHost(NetMsg.Command, JsonUtility.ToJson(new NetCommand { op = op, a = a, b = b, c = c }));
        return true;
    }

    bool NetCommandRespond(DiploProposal p, bool accept)
    {
        if (!IsNetClient) return false;

        NetCmd("respond", p.from.id, (int)p.kind, accept ? 1 : 0);
        proposals.Remove(p);
        return true;
    }

    // ---------------------------------------------------------------- Bucle de red

    // Se llama cada fotograma, también en el menú y mientras se carga: lobby, órdenes y mensajes
    void NetPump()
    {
        CivNet net = CivNet.Instance;
        if (net == null || !net.Active) return;

        NetPacket pk;
        int n = 0;
        while (n++ < 300 && net.TryDequeue(out pk))
        {
            if (net.IsHost) HostHandle(net, pk);
            else ClientHandle(pk);
        }

        if (net.IsHost && !netGame && lobbyDirty)
        {
            lobbyDirty = false;
            net.Broadcast(NetMsg.Lobby, JsonUtility.ToJson(new LobbyMsg { text = HostLobbyText() }));
        }
    }

    public string HostLobbyText()
    {
        string text = "Tú (anfitrión)";
        CivNet net = CivNet.Instance;
        if (net == null) return text;

        foreach (NetPeer p in net.PeerList())
            if (p.joined && !p.dead) text += "\n" + p.name;
        return text;
    }

    /// <summary>Jugadores remotos conectados y presentados (lobby del anfitrión).</summary>
    public int NetJoinedCount()
    {
        int n = 0;
        CivNet net = CivNet.Instance;
        if (net == null) return 0;
        foreach (NetPeer p in net.PeerList()) if (p.joined && !p.dead) n++;
        return n;
    }

    // ---------------------------------------------------------------- Anfitrión

    void HostHandle(CivNet net, NetPacket pk)
    {
        NetPeer peer = net.PeerById(pk.peer);
        if (peer == null) return;

        switch (pk.type)
        {
            case NetMsg.Hello:
            {
                HelloMsg hello = JsonUtility.FromJson<HelloMsg>(pk.json);
                if (netGame || hello == null || hello.version != CivNet.ProtocolVersion || NetJoinedCount() >= MaxNetPlayers - 1)
                {
                    net.SendTo(peer, NetMsg.Bye, "");
                    net.RemovePeer(peer);
                    break;
                }

                peer.name = string.IsNullOrEmpty(hello.name) ? "Jugador " + peer.id : hello.name;
                peer.civChoice = hello.civ;
                peer.joined = true;
                lobbyDirty = true;
                break;
            }

            case NetMsg.Command:
                if (netGame && !IsNetClient) HostExecute(peer, JsonUtility.FromJson<NetCommand>(pk.json));
                break;

            case NetMsg.Bye:
            case NetMsg.Disconnected:
                if (netGame)
                {
                    Civ civ = CivById(peer.civId);
                    if (civ != null && civ.remote)
                    {
                        civ.remote = false;
                        civ.isHuman = false;   // la IA toma el mando de la civilización
                        AddLog(civ.name + " se ha desconectado: la IA toma el mando", civ.color);
                    }
                }
                net.RemovePeer(peer);
                lobbyDirty = true;
                break;
        }
    }

    /// <summary>Anfitrión: fija las civilizaciones de los jugadores remotos (sin repetir) antes de crear la partida. Devuelve cuántos son.</summary>
    public int NetPrepareHost(int hostChoice)
    {
        extraHumanChoices.Clear();
        netPlayers.Clear();

        CivNet net = CivNet.Instance;
        HashSet<int> taken = new HashSet<int> { hostChoice };
        int max = GameDefs.PlayableCivs.Length;

        foreach (NetPeer p in net.PeerList())
        {
            if (p.dead) continue;
            if (!p.joined || netPlayers.Count >= MaxNetPlayers - 1)
            {
                net.SendTo(p, NetMsg.Bye, "");
                net.RemovePeer(p);
                continue;
            }

            int c = Mathf.Clamp(p.civChoice, 0, max - 1);
            for (int guard = 0; taken.Contains(c) && guard < max; guard++) c = (c + 1) % max;
            taken.Add(c);

            p.civChoice = c;
            extraHumanChoices.Add(c);
            netPlayers.Add(p);
        }

        return netPlayers.Count;
    }

    /// <summary>Anfitrión: la partida ya está creada; envía el estado completo a cada jugador y empieza a emitir instantáneas.</summary>
    public void NetHostBegin()
    {
        CivNet net = CivNet.Instance;
        if (net == null || !net.IsHost) return;

        for (int i = 0; i < netPlayers.Count; i++)
        {
            netPlayers[i].civId = i + 1;
            if (i + 1 < civs.Count) civs[i + 1].remote = true;
        }

        net.StopAccepting();
        netGame = true;
        snapTimer = 0f;
        structTimer = 0f;

        CivSaveData data = BuildSaveData(true, true);
        foreach (NetPeer p in netPlayers)
            net.SendTo(p, NetMsg.Start, JsonUtility.ToJson(new StartMsg { civId = p.civId, data = data }));
    }

    void NetHostUpdate()
    {
        if (netPlayers.Count == 0) return;

        snapTimer += Time.unscaledDeltaTime;
        structTimer += Time.unscaledDeltaTime;
        if (snapTimer < SnapshotInterval) return;
        snapTimer = 0f;

        bool structure = structTimer >= StructureInterval || (netStructDirty && structTimer >= 0.5f);
        if (structure) { structTimer = 0f; netStructDirty = false; }

        CivNet.Instance.Broadcast(NetMsg.Snapshot, JsonUtility.ToJson(BuildSaveData(true, structure)), true);
    }

    // Ejecuta la orden de un jugador remoto como si la diera su civilización
    void HostExecute(NetPeer peer, NetCommand cmd)
    {
        Civ actor = CivById(peer.civId);
        if (cmd == null || actor == null || !actor.alive || !actor.remote) return;

        Civ savedHuman = human;
        human = actor;
        execPeer = peer;
        try { DispatchCommand(actor, cmd); }
        catch (Exception e) { Debug.LogWarning("[Civilogenia] Orden de red no válida (" + cmd.op + "): " + e.Message); }
        finally { human = savedHuman; execPeer = null; }
    }

    Unit NetUnit(int id, Civ owner)
    {
        foreach (Unit u in allUnits) if (u.id == id) return (owner == null || u.owner == owner) && u.alive ? u : null;
        return null;
    }

    City NetCity(int id, Civ owner)
    {
        foreach (City c in allCities) if (c.id == id) return (owner == null || c.owner == owner) && c.alive ? c : null;
        return null;
    }

    bool ValidCell(int cell) { return grid != null && cell >= 0 && cell < grid.count; }

    void DispatchCommand(Civ actor, NetCommand c)
    {
        Unit u;
        City city;

        switch (c.op)
        {
            case "move": u = NetUnit(c.a, actor); if (u != null && ValidCell(c.b)) OrderMove(u, c.b); break;
            case "produce":
                city = NetCity(c.a, actor);
                if (city == null) break;
                if (c.c == 1) SetProduction(city, (UnitKind)c.b); else SetProduction(city, (BuildingKind)c.b);
                break;
            case "buy": city = NetCity(c.a, actor); if (city != null) Buy(city); break;
            case "upgrade": u = NetUnit(c.a, actor); if (u != null) TryUpgrade(u); break;
            case "research": StartResearch(actor, (TechKind)c.b); break;
            case "road": u = NetUnit(c.a, actor); if (u != null && ValidCell(c.b)) OrderRoad(u, c.b, c.c == 1); break;
            case "cancelroad": u = NetUnit(c.a, actor); if (u != null) CancelRoad(u); break;
            case "farm": u = NetUnit(c.a, actor); if (u != null && ValidCell(c.b)) OrderFarm(u, c.b); break;
            case "mine": u = NetUnit(c.a, actor); if (u != null && ValidCell(c.b)) OrderMine(u, c.b); break;
            case "irrigate": u = NetUnit(c.a, actor); if (u != null && ValidCell(c.b)) OrderIrrigate(u, c.b); break;
            case "fortify": u = NetUnit(c.a, actor); if (u != null) ToggleFortify(u); break;
            case "trade": u = NetUnit(c.a, actor); if (u != null) TryTrade(u); break;
            case "board":
                u = NetUnit(c.a, actor);
                Unit ship = NetUnit(c.b, actor);
                if (u != null && ship != null) OrderBoard(u, ship);
                break;
            case "disembark": u = NetUnit(c.a, actor); if (u != null && ValidCell(c.b)) OrderDisembark(u, c.b); break;
            case "attackunit":
                u = NetUnit(c.a, actor);
                Unit target = NetUnit(c.b, null);
                if (u != null && target != null) OrderAttack(u, target);
                break;
            case "attackcity":
                u = NetUnit(c.a, actor);
                City targetCity = NetCity(c.b, null);
                if (u != null && targetCity != null) OrderAttack(u, targetCity);
                break;
            case "found": u = NetUnit(c.a, actor); if (u != null) TryFound(u); break;
            case "join": u = NetUnit(c.a, actor); if (u != null) TryJoin(u); break;
            case "joincity":
                u = NetUnit(c.a, actor);
                city = NetCity(c.b, actor);
                if (u != null && city != null) OrderJoin(u, city);
                break;

            case "peace": { Civ o = CivById(c.a); if (o != null && o != actor) AddLog(ProposePeace(o), Color.white); break; }
            case "ally": { Civ o = CivById(c.a); if (o != null && o != actor) AddLog(ProposeAlliance(o), Color.white); break; }
            case "war": { Civ o = CivById(c.a); if (o != null && o != actor) AddLog(DeclareWar(o), Color.white); break; }
            case "respond":
                foreach (DiploProposal p in new List<DiploProposal>(proposals))
                    if (p.from.id == c.a && (int)p.kind == c.b && p.to == actor) { RespondProposal(p, c.c == 1); break; }
                break;
        }
    }

    // ---------------------------------------------------------------- Cliente

    void ClientHandle(NetPacket pk)
    {
        switch (pk.type)
        {
            case NetMsg.Lobby:
                LobbyMsg lobby = JsonUtility.FromJson<LobbyMsg>(pk.json);
                if (lobby != null) netLobbyText = lobby.text;
                break;

            case NetMsg.Start:
                if (!netJoining && !netGame)
                {
                    StartMsg start = JsonUtility.FromJson<StartMsg>(pk.json);
                    if (start != null && start.data != null) StartCoroutine(ClientJoinRoutine(start));
                }
                break;

            case NetMsg.Snapshot:
                pendingSnapshot = pk.json;   // solo importa la última
                break;

            case NetMsg.Log:
                LogMsg log = JsonUtility.FromJson<LogMsg>(pk.json);
                if (log != null) AddLog(log.text, new Color(log.r, log.g, log.b));
                break;

            case NetMsg.Bye:
            case NetMsg.Disconnected:
                netLost = true;
                AddLog("Se ha perdido la conexión con el anfitrión", Color.red);
                break;
        }
    }

    IEnumerator ClientJoinRoutine(StartMsg msg)
    {
        netJoining = true;
        netGame = true;   // desde ya el cliente no simula (Update no hace nada mientras "ready" sea false)

        while (!worldBuilt) yield return null;
        yield return StartCoroutine(LoadFromData(msg.data, msg.civId));

        netJoining = false;
    }

    void NetClientUpdate(float dt)
    {
        if (pendingSnapshot != null)
        {
            string json = pendingSnapshot;
            pendingSnapshot = null;
            try { ApplySnapshot(JsonUtility.FromJson<CivSaveData>(json)); }
            catch (Exception e) { Debug.LogWarning("[Civilogenia] Instantánea no válida: " + e); }
        }

        // Entre una instantánea y otra las unidades siguen su camino para que el movimiento se vea fluido
        foreach (Unit u in allUnits.ToArray())
            if (u.alive && u.Moving && u.carriedBy == null && u.view != null) AdvanceMove(u, dt);

        foreach (Civ civ in civs)
            if (civ.territoryDirty) CivViews.RebuildTerritory(civ, grid, earth, EarthT);
    }

    static bool SameInts(List<int> a, List<int> b)
    {
        if (a.Count != b.Count) return false;
        for (int i = 0; i < a.Count; i++) if (a[i] != b[i]) return false;
        return true;
    }

    // Aplica el estado que manda el anfitrión sobre los objetos que ya existen (se buscan por identificador) y crea o retira el resto
    void ApplySnapshot(CivSaveData d)
    {
        if (d == null || grid == null || human == null) return;

        gameTime = d.gameTime;

        // ---- Civilizaciones
        foreach (CivSaveCiv s in d.civs)
        {
            Civ civ = CivById(s.id);
            if (civ == null) continue;

            bool wasAlive = civ.alive;
            civ.isHuman = s.isHuman;
            civ.alive = s.alive;
            civ.gold = s.gold;
            civ.researching = s.researching;
            civ.currentTech = (TechKind)s.currentTech;
            civ.researchProgress = s.researchProgress;
            civ.nameCounter = s.nameCounter;

            List<TechKind> gained = null;
            foreach (int t in s.techs)
                if (!civ.techs.Contains((TechKind)t)) { if (gained == null) gained = new List<TechKind>(); gained.Add((TechKind)t); }

            if (gained != null) ClientTechsGained(civ, gained);
            if (wasAlive && !civ.alive && civ == human) IntroVideo.PlayClip(MilitaryDefeatVideo);
        }

        Civ newWinner = d.winner >= 0 ? CivById(d.winner) : null;
        if (newWinner != null && winner == null && d.winnerByTech) IntroVideo.PlayClip(OmniscienceVideo);
        winner = newWinner;
        winnerByTech = d.winnerByTech;

        MergeDiplomacy(d.diplomacy);

        proposals.Clear();
        for (int i = 0; i + 2 < d.proposals.Count; i += 3)
        {
            Civ from = CivById(d.proposals[i]);
            Civ to = d.proposals[i + 2] >= 0 ? CivById(d.proposals[i + 2]) : null;
            if (from != null) proposals.Add(new DiploProposal { from = from, to = to, kind = (DiploState)Mathf.Clamp(d.proposals[i + 1], 0, 2) });
        }

        // ---- Ciudades
        Dictionary<int, City> cityMap = new Dictionary<int, City>();
        foreach (City c in allCities) cityMap[c.id] = c;

        List<City> cityList = new List<City>();   // en el orden de la instantánea (las rutas comerciales se refieren a posiciones)
        List<City> rebuild = new List<City>();
        bool recomputeAll = d.structure;

        foreach (CivSaveCity s in d.cities)
        {
            Civ owner = CivById(s.owner);
            if (owner == null) { cityList.Add(null); continue; }

            City city;
            bool changed = false;

            if (!cityMap.TryGetValue(s.id, out city))
            {
                city = new City { id = s.id, owner = owner, name = s.name, cell = s.cell, recalcTimer = 3f };
                owner.cities.Add(city);
                allCities.Add(city);
                grid.cityAt[s.cell] = city;
                cityMap[s.id] = city;
                owner.territoryDirty = true;
                changed = true;
            }
            else if (city.owner != owner)
            {
                if (IsLocal(city.owner) || IsLocal(owner)) GameSfx.Play(Sfx.Capture);
                city.owner.cities.Remove(city);
                city.owner.territoryDirty = true;
                city.owner = owner;
                owner.cities.Add(city);
                owner.territoryDirty = true;
                changed = true;
            }

            if (city.pop != s.pop) changed = true;
            city.name = s.name;
            city.pop = s.pop;
            city.food = s.food;
            city.prodStock = s.prodStock;
            city.hp = s.hp;
            city.maxHp = s.maxHp;
            city.claimDepth = s.claimDepth;
            city.hasItem = s.hasItem;
            city.itemIsUnit = s.itemIsUnit;
            city.itemUnit = (UnitKind)s.itemUnit;
            city.itemBuilding = (BuildingKind)s.itemBuilding;

            if (d.structure)
            {
                if (!SameInts(city.cells, s.cells))
                {
                    foreach (int c in city.cells) if (grid.cityAt[c] == city) grid.cityAt[c] = null;
                    city.cells.Clear();
                    city.cells.AddRange(s.cells);
                    foreach (int c in city.cells) grid.cityAt[c] = city;
                    grid.cityAt[city.cell] = city;
                    changed = true;
                }

                if (!SameInts(city.tiles, s.tiles))
                {
                    foreach (int t in city.tiles) if (grid.tileCity[t] == city) grid.tileCity[t] = null;
                    city.tiles.Clear();
                    city.tiles.AddRange(s.tiles);
                    foreach (int t in city.tiles) grid.tileCity[t] = city;
                    owner.territoryDirty = true;
                }

                bool sameBuildings = city.buildings.Count == s.buildings.Count;
                if (sameBuildings) foreach (int b in s.buildings) if (!city.buildings.Contains((BuildingKind)b)) { sameBuildings = false; break; }
                if (!sameBuildings)
                {
                    city.buildings.Clear();
                    foreach (int b in s.buildings) city.buildings.Add((BuildingKind)b);
                    changed = true;
                }

                city.wonderCells.Clear();
                for (int i = 0; i < s.wonderKinds.Count && i < s.wonderCells.Count; i++)
                {
                    city.wonderCells[(BuildingKind)s.wonderKinds[i]] = s.wonderCells[i];
                    grid.wonderAt[s.wonderCells[i]] = true;
                }

                city.shieldApplied = owner.Has(TechKind.EnergyShields);   // el maxHp recibido ya incluye ese bonus
            }

            cityList.Add(city);
            if (changed) rebuild.Add(city);
        }

        if (d.structure)
        {
            for (int i = 0; i < d.cities.Count; i++)
            {
                City city = cityList[i];
                if (city == null) continue;
                city.tradePartners.Clear();
                city.merchantPartners.Clear();
                foreach (int p in d.cities[i].tradePartners) if (p >= 0 && p < cityList.Count && cityList[p] != null) city.tradePartners.Add(cityList[p]);
                foreach (int p in d.cities[i].merchantPartners) if (p >= 0 && p < cityList.Count && cityList[p] != null) city.merchantPartners.Add(cityList[p]);
            }

            ApplyImprovements(d);
        }

        foreach (City c in rebuild) { RecomputeCity(c); RebuildCityView(c); }
        if (recomputeAll) foreach (City c in cityList) if (c != null && !rebuild.Contains(c)) RecomputeCity(c);

        // ---- Unidades
        Dictionary<int, Unit> unitMap = new Dictionary<int, Unit>();
        foreach (Unit u in allUnits) unitMap[u.id] = u;

        List<Unit> unitList = new List<Unit>();
        HashSet<int> present = new HashSet<int>();

        foreach (CivSaveUnit s in d.units)
        {
            Civ owner = CivById(s.owner);
            if (owner == null) { unitList.Add(null); continue; }

            Unit u;
            if (!unitMap.TryGetValue(s.id, out u))
            {
                u = SpawnUnit(owner, (UnitKind)s.kind, s.cell);
                u.id = s.id;
                unitMap[s.id] = u;
            }
            else if ((int)u.kind != s.kind)
            {
                u.kind = (UnitKind)s.kind;
                u.def = GameDefs.Units[u.kind];
                CivViews.BuildUnit(u, grid, EarthT);
            }

            present.Add(s.id);
            unitList.Add(u);

            u.hp = s.hp;
            u.fortified = s.fortified;
            u.fortifying = s.fortifying;

            if (s.moveTo >= 0)
            {
                bool sameEdge = u.moveTo == s.moveTo && u.cell == s.cell;
                if (!sameEdge || s.t > u.t) { u.cell = s.cell; u.moveTo = s.moveTo; u.t = s.t; }
            }
            else
            {
                u.cell = s.cell;
                u.moveTo = -1;
                u.t = 0f;
            }
        }

        for (int i = 0; i < d.units.Count; i++)
        {
            Unit u = unitList[i];
            if (u == null) continue;

            CivSaveUnit s = d.units[i];
            u.homeCity = s.homeCity >= 0 && s.homeCity < cityList.Count ? cityList[s.homeCity] : null;

            Unit ship = s.carriedBy >= 0 && s.carriedBy < unitList.Count ? unitList[s.carriedBy] : null;
            if (u.carriedBy != ship)
            {
                if (u.carriedBy != null) u.carriedBy.cargo.Remove(u);
                u.carriedBy = ship;
                if (ship != null && !ship.cargo.Contains(u)) ship.cargo.Add(u);
                if (u.view != null) u.view.SetActive(ship == null);
            }
        }

        foreach (Unit u in allUnits.ToArray())
        {
            if (!u.alive) continue;
            if (!present.Contains(u.id))
            {
                if (u.carriedBy != null) u.carriedBy.cargo.Remove(u);
                if (human != null && IsVisible(u.cell)) GameSfx.Play(Sfx.Death, u.owner == human ? 1f : 0.5f);
                KillSilently(u);
                continue;
            }

            if (u.view != null) { PlaceUnit(u); CivViews.SyncFortifyMarker(u, grid); }
        }

        fogMeshStale = true;
    }

    // Lo que ocurre en un cliente cuando su civilización o una rival descubre tecnologías (como TickResearch en el anfitrión)
    void ClientTechsGained(Civ civ, List<TechKind> gained)
    {
        bool era = false;
        foreach (TechKind t in gained)
        {
            civ.techs.Add(t);
            if (t == TechKind.EraChange || t == TechKind.EraChange3 || t == TechKind.EraChange4) era = true;
        }

        if (IsLocal(civ)) GameSfx.Play(Sfx.Tech);

        foreach (City c in civ.cities)
        {
            RecomputeCity(c);
            CivViews.BuildCity(c, grid, EarthT);
        }

        if (!era) return;

        CivViews.UpgradeRoads(civ, grid, earth, EarthT);
        foreach (Unit u in civ.units.ToArray())
        {
            if (!u.alive || u.view == null) continue;
            CivViews.BuildUnit(u, grid, EarthT);
            PlaceUnit(u);
            if (u.carriedBy != null) u.view.SetActive(false);
        }
    }

    // Carreteras, ferrocarril y mejoras del terreno: solo se añade lo que el cliente aún no tiene
    void ApplyImprovements(CivSaveData d)
    {
        foreach (int cell in d.farmed) if (cell >= 0 && cell < grid.count && !grid.farmed[cell]) { grid.farmed[cell] = true; CivViews.BuildFarm(cell, grid, EarthT); }
        foreach (int cell in d.irrigated) if (cell >= 0 && cell < grid.count && !grid.irrigated[cell]) { grid.irrigated[cell] = true; CivViews.BuildIrrigation(cell, grid, EarthT); }
        foreach (int cell in d.mined) if (cell >= 0 && cell < grid.count && !grid.mined[cell]) { grid.mined[cell] = true; CivViews.BuildMine(cell, grid, EarthT); }

        for (int i = 0; i + 2 < d.roads.Count; i += 3)
        {
            int a = d.roads[i], b = d.roads[i + 1];
            if (grid.HasRoad(a, b)) continue;
            grid.AddRoad(a, b);
            CivViews.BuildRoad(a, b, grid, earth, EarthT, CivById(d.roads[i + 2]) ?? human);
        }

        for (int i = 0; i + 1 < d.rails.Count; i += 2)
        {
            int a = d.rails[i], b = d.rails[i + 1];
            if (grid.HasRail(a, b)) continue;
            grid.AddRail(a, b);
            CivViews.BuildRail(a, b, grid, earth, EarthT, human);
        }
    }

    // Como ImportDiplomacy, pero conservando "since" de las relaciones que no han cambiado y avisando de lo que afecta al jugador
    void MergeDiplomacy(List<int> flat)
    {
        bool changed = false;
        for (int i = 0; i + 2 < flat.Count; i += 3)
        {
            Civ a = CivById(flat[i]), b = CivById(flat[i + 1]);
            if (a == null || b == null || a == b) continue;

            DiploState state = (DiploState)Mathf.Clamp(flat[i + 2], 0, 2);
            DiploRelation r = RelationOf(a, b);
            if (r.state == state) continue;

            r.state = state;
            r.since = gameTime;
            changed = true;

            if (a == human || b == human)
            {
                Civ other = a == human ? b : a;
                string text = state == DiploState.War ? "¡Guerra con " + other.name + "!"
                            : state == DiploState.Peace ? "Paz con " + other.name
                            : "Alianza con " + other.name;
                AddLog(text, other.color);
                GameSfx.Play(state == DiploState.War ? Sfx.War : Sfx.Peace);
            }
        }

        if (changed) fogMeshStale = true;
    }
}
