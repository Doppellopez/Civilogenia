using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

// ---------------------------------------------------------------------------------------------------------------------
// Capa de red del multijugador: TCP directo (LAN o IP) con un anfitrión autoritativo.
// El anfitrión simula toda la partida; los clientes le mandan órdenes y reciben instantáneas del estado (ver CivGame.Net.cs).
// Cada mensaje va como [longitud int32][tipo byte][carga JSON en UTF-8]. Los hilos de red solo encolan; todo se procesa
// en el hilo principal desde CivGame.Update.
// ---------------------------------------------------------------------------------------------------------------------

public enum NetMsg : byte
{
    Hello = 1,      // cliente -> anfitrión: nombre y civilización preferida
    Lobby = 2,      // anfitrión -> cliente: texto con los jugadores conectados
    Start = 3,      // anfitrión -> cliente: estado completo de la partida y la civilización asignada
    Snapshot = 4,   // anfitrión -> clientes: estado de la partida
    Command = 5,    // cliente -> anfitrión: una orden del jugador
    Log = 6,        // anfitrión -> cliente: mensaje para el registro del jugador
    Bye = 7,        // cualquiera: se cierra la conexión
    Disconnected = 100   // interno: la conexión se ha caído
}

public class NetPacket
{
    public int peer;        // identificador del jugador conectado (0 para el anfitrión)
    public NetMsg type;
    public string json;
}

public class NetPeer
{
    public int id;
    public string name = "Jugador";
    public int civChoice;       // civilización preferida
    public int civId = -1;      // civilización asignada al empezar la partida
    public bool joined;         // ya ha enviado su Hello
    public bool dead;
    public TcpClient tcp;
    public NetworkStream stream;
}

[Serializable] public class HelloMsg { public string name; public int civ; public int version = CivNet.ProtocolVersion; }
[Serializable] public class LobbyMsg { public string text; }
[Serializable] public class LogMsg { public string text; public float r, g, b; }

public class CivNet : MonoBehaviour
{
    public const int ProtocolVersion = 1;
    public const int DefaultPort = 7777;
    const int MaxPayload = 64 * 1024 * 1024;

    public enum Role { None, Host, Client }

    public static CivNet Instance { get; private set; }

    public Role role = Role.None;
    public bool IsHost => role == Role.Host;
    public bool IsClient => role == Role.Client;
    public bool Active => role != Role.None;

    public string status = "";          // texto para la interfaz (errores, estado de la conexión)
    public bool clientConnected;        // cliente: la conexión con el anfitrión está establecida
    public readonly List<NetPeer> peers = new List<NetPeer>();   // anfitrión: jugadores conectados

    readonly ConcurrentQueue<NetPacket> inbox = new ConcurrentQueue<NetPacket>();
    readonly object peersLock = new object();
    TcpListener listener;
    Thread acceptThread;
    TcpClient clientTcp;
    NetworkStream clientStream;
    int nextPeerId = 1;
    volatile bool closing;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Create()
    {
        if (FindFirstObjectByType<CivNet>() != null) return;
        GameObject go = new GameObject("Red");
        go.AddComponent<CivNet>();
    }

    void Awake() { Instance = this; }

    void OnDestroy() { Shutdown(); }
    void OnApplicationQuit() { Shutdown(); }

    // ---------------------------------------------------------------- Anfitrión

    public bool StartHost(int port = DefaultPort)
    {
        Shutdown();
        try
        {
            closing = false;
            listener = new TcpListener(IPAddress.Any, port);
            listener.Start();
        }
        catch (Exception e)
        {
            status = "No se pudo abrir el puerto " + port + ": " + e.Message;
            Debug.LogWarning("[Civilogenia] " + status);
            listener = null;
            return false;
        }

        role = Role.Host;
        status = "Esperando jugadores en el puerto " + port;
        acceptThread = new Thread(AcceptLoop) { IsBackground = true };
        acceptThread.Start();
        return true;
    }

    void AcceptLoop()
    {
        while (!closing)
        {
            try
            {
                TcpClient c = listener.AcceptTcpClient();
                c.NoDelay = true;
                c.SendTimeout = 3000;

                NetPeer peer;
                lock (peersLock)
                {
                    peer = new NetPeer { id = nextPeerId++, tcp = c, stream = c.GetStream() };
                    peers.Add(peer);
                }

                Thread t = new Thread(() => ReadLoop(peer.stream, peer.id)) { IsBackground = true };
                t.Start();
            }
            catch (Exception)
            {
                if (closing) return;
            }
        }
    }

    /// <summary>Jugadores humanos conectados (sin contar al anfitrión), copia segura para recorrer.</summary>
    public List<NetPeer> PeerList()
    {
        lock (peersLock) return new List<NetPeer>(peers);
    }

    public void StopAccepting()
    {
        try { if (listener != null) listener.Stop(); } catch (Exception) { }
        listener = null;
    }

    // ---------------------------------------------------------------- Cliente

    /// <summary>Empieza a conectar con el anfitrión (sin bloquear). Mira "clientConnected" y "status".</summary>
    public void StartClient(string host, int port, string playerName, int civChoice)
    {
        Shutdown();
        closing = false;
        role = Role.Client;
        clientConnected = false;
        status = "Conectando con " + host + ":" + port + "…";

        Thread t = new Thread(() =>
        {
            try
            {
                TcpClient c = new TcpClient();
                c.NoDelay = true;
                c.SendTimeout = 3000;
                IAsyncResult ar = c.BeginConnect(host, port, null, null);
                if (!ar.AsyncWaitHandle.WaitOne(6000) || !c.Connected) { c.Close(); throw new Exception("tiempo de espera agotado"); }
                c.EndConnect(ar);

                clientTcp = c;
                clientStream = c.GetStream();
                clientConnected = true;
                status = "Conectado. Esperando al anfitrión…";

                SendRaw(clientStream, NetMsg.Hello, JsonUtility.ToJson(new HelloMsg { name = playerName, civ = civChoice }));
                ReadLoop(clientStream, 0);
            }
            catch (Exception e)
            {
                if (!closing) { status = "No se pudo conectar: " + e.Message; Debug.LogWarning("[Civilogenia] " + status); }
                inbox.Enqueue(new NetPacket { peer = 0, type = NetMsg.Disconnected, json = "" });
            }
        }) { IsBackground = true };
        t.Start();
    }

    // ---------------------------------------------------------------- Envío y recepción

    void ReadLoop(NetworkStream stream, int peerId)
    {
        byte[] header = new byte[5];
        try
        {
            while (!closing)
            {
                ReadExact(stream, header, 5);
                int len = BitConverter.ToInt32(header, 0);
                if (len < 1 || len > MaxPayload) throw new IOException("mensaje no válido");
                NetMsg type = (NetMsg)header[4];

                byte[] body = new byte[len - 1];
                if (body.Length > 0) ReadExact(stream, body, body.Length);
                inbox.Enqueue(new NetPacket { peer = peerId, type = type, json = Encoding.UTF8.GetString(body) });
            }
        }
        catch (Exception)
        {
            // la conexión se cerró o se cayó
        }

        if (!closing) inbox.Enqueue(new NetPacket { peer = peerId, type = NetMsg.Disconnected, json = "" });
    }

    static void ReadExact(NetworkStream s, byte[] buf, int count)
    {
        int got = 0;
        while (got < count)
        {
            int n = s.Read(buf, got, count - got);
            if (n <= 0) throw new IOException("conexión cerrada");
            got += n;
        }
    }

    static bool SendRaw(NetworkStream stream, NetMsg type, string json)
    {
        try
        {
            byte[] body = Encoding.UTF8.GetBytes(json ?? "");
            byte[] frame = new byte[5 + body.Length];
            BitConverter.GetBytes(body.Length + 1).CopyTo(frame, 0);
            frame[4] = (byte)type;
            Buffer.BlockCopy(body, 0, frame, 5, body.Length);
            lock (stream) stream.Write(frame, 0, frame.Length);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>Cliente: manda un mensaje al anfitrión.</summary>
    public void SendToHost(NetMsg type, string json)
    {
        if (role != Role.Client || clientStream == null) return;
        if (!SendRaw(clientStream, type, json)) inbox.Enqueue(new NetPacket { peer = 0, type = NetMsg.Disconnected, json = "" });
    }

    /// <summary>Anfitrión: manda un mensaje a un jugador.</summary>
    public void SendTo(NetPeer peer, NetMsg type, string json)
    {
        if (role != Role.Host || peer == null || peer.dead || peer.stream == null) return;
        if (!SendRaw(peer.stream, type, json)) inbox.Enqueue(new NetPacket { peer = peer.id, type = NetMsg.Disconnected, json = "" });
    }

    /// <summary>Anfitrión: manda un mensaje a todos los jugadores que ya se han presentado.</summary>
    public void Broadcast(NetMsg type, string json, bool onlyInGame = false)
    {
        foreach (NetPeer p in PeerList())
            if (p.joined && (!onlyInGame || p.civId >= 0)) SendTo(p, type, json);
    }

    public bool TryDequeue(out NetPacket packet) { return inbox.TryDequeue(out packet); }

    public NetPeer PeerById(int id)
    {
        lock (peersLock) foreach (NetPeer p in peers) if (p.id == id) return p;
        return null;
    }

    public void RemovePeer(NetPeer peer)
    {
        if (peer == null) return;
        peer.dead = true;
        try { if (peer.tcp != null) peer.tcp.Close(); } catch (Exception) { }
        lock (peersLock) peers.Remove(peer);
    }

    // ---------------------------------------------------------------- Cierre

    public void Shutdown()
    {
        if (role == Role.None && listener == null && clientTcp == null) return;

        closing = true;
        try { if (listener != null) listener.Stop(); } catch (Exception) { }
        listener = null;

        lock (peersLock)
        {
            foreach (NetPeer p in peers)
            {
                p.dead = true;
                try { if (p.stream != null) SendRaw(p.stream, NetMsg.Bye, ""); if (p.tcp != null) p.tcp.Close(); } catch (Exception) { }
            }
            peers.Clear();
        }

        try { if (clientTcp != null) clientTcp.Close(); } catch (Exception) { }
        clientTcp = null;
        clientStream = null;
        clientConnected = false;
        role = Role.None;

        NetPacket drop;
        while (inbox.TryDequeue(out drop)) { }
    }

    /// <summary>Direcciones IPv4 de este equipo, para decírselas a quien quiera unirse.</summary>
    public static List<string> LocalAddresses()
    {
        List<string> list = new List<string>();
        try
        {
            foreach (IPAddress ip in Dns.GetHostEntry(Dns.GetHostName()).AddressList)
                if (ip.AddressFamily == AddressFamily.InterNetwork) list.Add(ip.ToString());
        }
        catch (Exception) { }
        return list;
    }
}
