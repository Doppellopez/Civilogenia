using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
#if CIV_RELAY
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Networking.Transport;
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
#endif

// ---------------------------------------------------------------------------------------------------------------------
// Unity Relay: permite jugar por internet sin abrir puertos. El anfitrión (o el servidor dedicado) pide una asignación a Relay,
// recibe un código de 6 caracteres y los jugadores se unen escribiendo ese código en lugar de la IP.
//
// Va por UDP con Unity Transport (canal fiable y ordenado). Por encima se usa el mismo formato de mensajes que el TCP directo
// ([longitud][tipo][JSON]), troceado en paquetes pequeños y recompuesto al recibir, así que el resto del juego no se entera.
// El TCP directo sigue funcionando a la vez: el anfitrión puede aceptar jugadores por IP y por código de Relay.
//
// Todo el código de Relay está detrás de CIV_RELAY, que se activa solo (Editor/RelaySetup.cs) al instalar los paquetes
// com.unity.services.relay y com.unity.transport. Sin ellos el juego compila igual y el Relay queda desactivado.
// ---------------------------------------------------------------------------------------------------------------------

/// <summary>Estado de un extremo de la conexión por Relay: bytes recibidos pendientes de recomponer y cola de envío.</summary>
public class RelayLink
{
#if CIV_RELAY
    public NetworkConnection conn;
#endif
    public readonly List<byte> rx = new List<byte>();
    public readonly Queue<byte[]> tx = new Queue<byte[]>();
    public int txOffset;        // bytes ya enviados del primer mensaje de la cola
    public int pendingBytes;    // bytes en cola de envío
}

public partial class CivNet
{
    /// <summary>true si el juego incluye el soporte de Relay (paquetes instalados).</summary>
    public static bool RelayAvailable
    {
        get
        {
#if CIV_RELAY
            return true;
#else
            return false;
#endif
        }
    }

    /// <summary>Anfitrión: código que los jugadores escriben para unirse por Relay (vacío hasta que está listo).</summary>
    public string relayJoinCode = "";
    public bool relayReady;         // anfitrión: la asignación de Relay está lista
    public bool relayFailed;        // anfitrión: no se pudo preparar Relay (el motivo está en relayError)
    public string relayError = "";

    bool relayClient;               // este cliente está conectado por Relay

    bool relayPending;              // anfitrión: se ha pedido la sala a Relay y aún no está lista

#if !CIV_RELAY
    bool RelayActive { get { return false; } }

    /// <summary>Anfitrión: pide una asignación a Relay para "maxClients" jugadores y abre la sala por código.</summary>
    public void StartRelayHost(int maxClients)
    {
        relayFailed = true;
        relayError = "Relay no está instalado (faltan los paquetes com.unity.services.relay y com.unity.transport).";
    }

    /// <summary>Cliente: se une a una sala de Relay con su código.</summary>
    public void StartRelayClient(string joinCode, string playerName, int civChoice)
    {
        Shutdown();
        role = Role.Client;
        status = "Relay no está instalado en esta versión del juego.";
        inbox.Enqueue(new NetPacket { peer = 0, type = NetMsg.Disconnected, json = "" });
    }

    bool RelaySend(NetPeer peer, NetMsg type, string json) { return false; }
    bool RelaySendToHost(NetMsg type, string json) { return false; }
    void RelayRemovePeer(NetPeer peer) { }
    void RelayStopAccepting() { }
    void RelayShutdown() { }
    void RelayUpdate() { }
#else
    const int RelayChunk = 1000;            // bytes por paquete: cabe de sobra en un paquete UDP con las cabeceras de Relay
    const int SnapshotBacklogLimit = 256 * 1024;   // si un jugador va tan atrasado, se salta alguna instantánea

    NetworkDriver relayDriver;
    NetworkPipeline relayPipeline;
    RelayLink relayClientLink;
    bool relayAcceptClosed;
    string relayHelloName = "Jugador";
    int relayHelloCiv;

    bool RelayActive { get { return relayDriver.IsCreated || relayPending; } }

    static async Task EnsureServices()
    {
        if (UnityServices.State != ServicesInitializationState.Initialized) await UnityServices.InitializeAsync();
        if (!AuthenticationService.Instance.IsSignedIn) await AuthenticationService.Instance.SignInAnonymouslyAsync();
    }

    void CreateRelayDriver(RelayServerData data)
    {
        NetworkSettings settings = new NetworkSettings();
        settings.WithRelayParameters(ref data);
        relayDriver = NetworkDriver.Create(settings);
        relayPipeline = relayDriver.CreatePipeline(typeof(ReliableSequencedPipelineStage));
    }

    // ---------------------------------------------------------------- Anfitrión

    /// <summary>Anfitrión: pide una asignación a Relay para "maxClients" jugadores y abre la sala por código.
    /// No bloquea: mira relayReady / relayFailed y relayJoinCode.</summary>
    public void StartRelayHost(int maxClients)
    {
        relayReady = false;
        relayFailed = false;
        relayError = "";
        relayJoinCode = "";
        closing = false;
        relayPending = true;
        RelayHostAsync(Mathf.Max(1, maxClients));
    }

    async void RelayHostAsync(int maxClients)
    {
        try
        {
            await EnsureServices();
            if (!relayPending) return;   // se canceló (Shutdown) mientras tanto
            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(maxClients);
            string code = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
            if (!relayPending) return;

            RelayServerData data = AllocationUtils.ToRelayServerData(allocation, "dtls");
            CreateRelayDriver(data);
            relayDriver.Bind(NetworkEndpoint.AnyIpv4);
            relayDriver.Listen();

            relayAcceptClosed = false;
            if (role == Role.None) role = Role.Host;
            relayJoinCode = code;
            relayReady = true;
            relayPending = false;
        }
        catch (Exception e)
        {
            relayPending = false;
            relayFailed = true;
            relayError = e.Message;
            Debug.LogWarning("[Civilogenia] Relay: " + e.Message);
        }
    }

    // ---------------------------------------------------------------- Cliente

    /// <summary>Cliente: se une a una sala de Relay con su código (sin bloquear). Mira "clientConnected" y "status".</summary>
    public void StartRelayClient(string joinCode, string playerName, int civChoice)
    {
        Shutdown();
        closing = false;
        role = Role.Client;
        relayClient = true;
        clientConnected = false;
        relayHelloName = playerName;
        relayHelloCiv = civChoice;
        status = "Conectando con Relay (código " + joinCode + ")…";
        RelayClientAsync(joinCode.Trim().ToUpperInvariant());
    }

    async void RelayClientAsync(string code)
    {
        try
        {
            await EnsureServices();
            JoinAllocation allocation = await RelayService.Instance.JoinAllocationAsync(code);
            if (closing || !relayClient) return;   // se canceló mientras tanto

            RelayServerData data = AllocationUtils.ToRelayServerData(allocation, "dtls");
            CreateRelayDriver(data);
            relayClientLink = new RelayLink { conn = relayDriver.Connect() };
        }
        catch (Exception e)
        {
            if (closing) return;
            status = "No se pudo unir por Relay: " + e.Message;
            Debug.LogWarning("[Civilogenia] " + status);
            inbox.Enqueue(new NetPacket { peer = 0, type = NetMsg.Disconnected, json = "" });
        }
    }

    // ---------------------------------------------------------------- Envío

    static void RelayEnqueue(RelayLink link, NetMsg type, string json)
    {
        byte[] frame = Frame(type, json);
        link.tx.Enqueue(frame);
        link.pendingBytes += frame.Length;
    }

    bool RelaySend(NetPeer peer, NetMsg type, string json)
    {
        if (peer.relay == null || !relayDriver.IsCreated) return false;
        if (type == NetMsg.Snapshot && peer.relay.pendingBytes > SnapshotBacklogLimit) return true;   // solo importa la última
        RelayEnqueue(peer.relay, type, json);
        return true;
    }

    bool RelaySendToHost(NetMsg type, string json)
    {
        if (relayClientLink == null || !relayDriver.IsCreated) return true;   // aún conectando: el Hello se manda al conectar
        RelayEnqueue(relayClientLink, type, json);
        return true;
    }

    // Manda lo que quepa en el canal fiable; el resto sigue en cola para el siguiente fotograma
    void RelayFlush(RelayLink link)
    {
        while (link.tx.Count > 0)
        {
            byte[] head = link.tx.Peek();
            int n = Math.Min(RelayChunk, head.Length - link.txOffset);

            if (relayDriver.BeginSend(relayPipeline, link.conn, out DataStreamWriter writer) < 0) return;   // cola llena

            NativeArray<byte> chunk = new NativeArray<byte>(n, Allocator.Temp);
            NativeArray<byte>.Copy(head, link.txOffset, chunk, 0, n);
            writer.WriteBytes(chunk);
            chunk.Dispose();

            if (writer.HasFailedWrites) { relayDriver.AbortSend(writer); return; }
            if (relayDriver.EndSend(writer) < 0) return;

            link.txOffset += n;
            link.pendingBytes -= n;
            if (link.txOffset >= head.Length)
            {
                link.tx.Dequeue();
                link.txOffset = 0;
            }
        }
    }

    // ---------------------------------------------------------------- Recepción

    // Recompone los mensajes a partir de los bytes recibidos
    void RelayParse(RelayLink link, int peerId)
    {
        while (link.rx.Count >= 5)
        {
            int len = link.rx[0] | (link.rx[1] << 8) | (link.rx[2] << 16) | (link.rx[3] << 24);
            if (len < 1 || len > MaxPayload)
            {
                link.rx.Clear();
                inbox.Enqueue(new NetPacket { peer = peerId, type = NetMsg.Disconnected, json = "" });
                return;
            }

            if (link.rx.Count < 4 + len) return;   // falta parte del mensaje

            NetMsg type = (NetMsg)link.rx[4];
            string json = Encoding.UTF8.GetString(link.rx.GetRange(5, len - 1).ToArray());
            link.rx.RemoveRange(0, 4 + len);
            inbox.Enqueue(new NetPacket { peer = peerId, type = type, json = json });
        }
    }

    // Atiende los eventos de una conexión. Devuelve false si se ha cerrado.
    bool RelayReadEvents(RelayLink link, int peerId, bool isClient)
    {
        NetworkEvent.Type evt;
        while ((evt = relayDriver.PopEventForConnection(link.conn, out DataStreamReader stream, out NetworkPipeline _)) != NetworkEvent.Type.Empty)
        {
            if (evt == NetworkEvent.Type.Connect)
            {
                if (!isClient) continue;
                clientConnected = true;
                status = "Conectado. Esperando al anfitrión…";
                RelayEnqueue(link, NetMsg.Hello, JsonUtility.ToJson(new HelloMsg { name = relayHelloName, civ = relayHelloCiv }));
            }
            else if (evt == NetworkEvent.Type.Data)
            {
                NativeArray<byte> bytes = new NativeArray<byte>(stream.Length, Allocator.Temp);
                stream.ReadBytes(bytes);
                link.rx.AddRange(bytes.ToArray());
                bytes.Dispose();
                RelayParse(link, peerId);
            }
            else if (evt == NetworkEvent.Type.Disconnect)
            {
                if (!closing) inbox.Enqueue(new NetPacket { peer = peerId, type = NetMsg.Disconnected, json = "" });
                return false;
            }
        }

        return true;
    }

    void RelayUpdate()
    {
        if (!relayDriver.IsCreated) return;

        relayDriver.ScheduleUpdate().Complete();

        if (role == Role.Host)
        {
            NetworkConnection incoming;
            while ((incoming = relayDriver.Accept()).IsCreated)
            {
                if (relayAcceptClosed) { relayDriver.Disconnect(incoming); continue; }   // la partida ya ha empezado

                lock (peersLock)
                    peers.Add(new NetPeer { id = nextPeerId++, viaRelay = true, relay = new RelayLink { conn = incoming } });
            }

            foreach (NetPeer p in PeerList())
            {
                if (!p.viaRelay || p.dead) continue;
                if (!RelayReadEvents(p.relay, p.id, false)) { p.dead = true; continue; }
                RelayFlush(p.relay);
            }
        }
        else if (role == Role.Client && relayClient && relayClientLink != null)
        {
            if (!RelayReadEvents(relayClientLink, 0, true)) { relayClientLink = null; relayClient = false; }
            else RelayFlush(relayClientLink);
        }

        relayDriver.ScheduleFlushSend(default).Complete();
    }

    // ---------------------------------------------------------------- Cierre

    void RelayRemovePeer(NetPeer peer)
    {
        if (peer.relay == null || !relayDriver.IsCreated) return;
        try
        {
            RelayEnqueue(peer.relay, NetMsg.Bye, "");
            RelayFlush(peer.relay);
            relayDriver.ScheduleFlushSend(default).Complete();
            relayDriver.Disconnect(peer.relay.conn);
        }
        catch (Exception) { }
        peer.relay = null;
    }

    void RelayStopAccepting() { relayAcceptClosed = true; }

    void RelayShutdown()
    {
        relayClient = false;
        relayClientLink = null;
        relayJoinCode = "";
        relayReady = false;
        relayPending = false;

        if (!relayDriver.IsCreated) return;

        try
        {
            lock (peersLock)
                foreach (NetPeer p in peers)
                    if (p.viaRelay && p.relay != null) relayDriver.Disconnect(p.relay.conn);

            relayDriver.ScheduleUpdate().Complete();
            relayDriver.Dispose();
        }
        catch (Exception) { }

        relayDriver = default;
    }
#endif
}
