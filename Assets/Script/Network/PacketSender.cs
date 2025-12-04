using System;
using System.Net;
using UnityEngine;
namespace Network
{
    public class PacketSenderBase
    {
        protected PacketTransmitter _packetTransmitter;

        public PacketSenderBase(PacketTransmitter packetTransmitter)
        {
            _packetTransmitter = packetTransmitter;
        }
    }

    public class HostPacketSender : PacketSenderBase
    {
        public HostPacketSender(PacketTransmitter packetTransmitter) : base(packetTransmitter) { }

        public void SendJoinResponse(IPEndPoint target)
        {
            NetworkPacket packet = new NetworkPacket(Settings.MyMode,PacketType.JOIN_RESPONSE);
            _packetTransmitter.SendToClinet(packet, target);
        }

        public void SendPlayRequest(long starTime)
        { 
            NetworkPacket packet = new NetworkPacket(Settings.MyMode, PacketType.PLAY_REQUEST, starTime);
            _packetTransmitter.SendToClientByBroadcast(packet);
        }

        public void SendSyncRequest(long curPlayTime)
        {
            NetworkPacket packet = new NetworkPacket(Settings.MyMode, PacketType.SYNC_REQUEST, curPlayTime);
            _packetTransmitter.SendToClientByBroadcast(packet);
        }

        public void SendRttResponse(long clientSendTime, IPEndPoint target)
        {
            NetworkPacket packet = new NetworkPacket(Settings.MyMode, PacketType.RTT_RESPONSE, clientSendTime);
            _packetTransmitter.SendToClinet(packet, target);
        }

    }

    public class ClientPacketSender : PacketSenderBase
    {
        public ClientPacketSender(PacketTransmitter packetTransmitter) : base(packetTransmitter) { }

        public void SendJoinRequest()
        {
            NetworkPacket packet = new NetworkPacket(Settings.MyMode, PacketType.JOIN_REQUEST);
            _packetTransmitter.SendToHost(packet);
        }
        public void SendRttRequest()
        {
            NetworkPacket packet = new NetworkPacket(Settings.MyMode, PacketType.RTT_REQUEST);
            _packetTransmitter.SendToHost(packet);
        }
    }
    public class PacketSender
    {
        public HostPacketSender Host { get; }
        public ClientPacketSender Client { get; }

        public PacketSender(PacketTransmitter packetTransmitter)
        {
            if(packetTransmitter.IsHost)
            {
                Host = new HostPacketSender(packetTransmitter);
            }
            else
            {
                Client = new ClientPacketSender(packetTransmitter);
            }
        }
    }

}
