using Network;
using UnityEngine;

public class PacketDispatcher : MonoBehaviour
{

    # region 쉽게 접근하기 위해 만든 프로퍼티
    public HostPacketSender HostSender => _packetSender.Host;
    public ClientPacketSender ClientSender => _packetSender.Client;

    public HostPacketHandler HostHandler => _packetHandler.Host;
    public ClientPacketHandler ClientHandler => _packetHandler.Client;
    #endregion


    private PacketTransmitter _packetTransmitter;
    private PacketHandler _packetHandler;
    private PacketSender _packetSender;

    public void Awake()
    {
        _packetTransmitter = new PacketTransmitter(Settings.MyMode, Settings.MyIPE);
        _packetSender = new PacketSender(_packetTransmitter);
        _packetHandler = new PacketHandler(_packetSender);
    }

    private void TickProcessPacketQueue()
    {
        while (_packetTransmitter.TryDequeuePacket(out ReceivedPacket receivedPacket))
        {
            switch (receivedPacket.Packet.senderType)
            {
                case NetworkRole.FRONT:
                    _packetHandler.Client.RoutePacket(receivedPacket.Packet, receivedPacket.Sender); break;

                case NetworkRole.SIDE:
                case NetworkRole.BOTTOM:
                    _packetHandler.Host.RoutePacket(receivedPacket.Packet, receivedPacket.Sender); break;
            }
        }
    }
    void Update()
    {
        if (_packetTransmitter == null) return;
        TickProcessPacketQueue();
    }

    public bool IsHost()
    {
        return _packetTransmitter.IsHost;
    }

    private void OnDestroy()
    {
        if (_packetTransmitter != null)
        {
            _packetTransmitter.Dispose();
        }
    }
}