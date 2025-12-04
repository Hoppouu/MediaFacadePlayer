using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using UnityEngine;

namespace Network
{
    public class PacketHandlerBase
    {
        protected PacketSender _packetSender;
        private Dictionary<PacketType, Action<NetworkPacket, IPEndPoint>> _handlerMap;
        public PacketHandlerBase(PacketSender packetSender)
        {
            _handlerMap = new Dictionary<PacketType, Action<NetworkPacket, IPEndPoint>>();
            _packetSender = packetSender;
        }

        protected void RegisterHandler(PacketType type, Action<NetworkPacket, IPEndPoint> handler)
        {
            _handlerMap[type] = handler;
        }

        public void RoutePacket(NetworkPacket packet, IPEndPoint sender)
        {
            if(_handlerMap.TryGetValue(packet.packetType, out Action<NetworkPacket, IPEndPoint> handler))
            {
                handler.Invoke(packet, sender);
            }
            else
            {
                UnityEngine.Debug.LogError($"Unknown Packet Type: {packet.packetType}");
            }
        }
    }

    public class HostPacketHandler:PacketHandlerBase
    {
        public HostPacketHandler(PacketSender packetSender) : base(packetSender)
        {
            RegisterHandler(PacketType.JOIN_REQUEST, OnJoinRequest);
            RegisterHandler(PacketType.RTT_REQUEST, OnRttRequest);
        }

        private void OnJoinRequest(NetworkPacket packet, IPEndPoint sender)
        {
            NetworkManager.Instance.SetConnectionState(packet.senderType, false, sender);
            _packetSender.Host.SendJoinResponse(sender);
        }
        private void OnRttRequest(NetworkPacket packet, IPEndPoint sender)
        {
            if (!NetworkManager.Instance.SetConnectionState(packet.senderType, 1))
            {
                _packetSender.Host.SendRttResponse(packet.sendTime, sender);    // 클라이언트가 보낸 시각 다시 전송
            }
            else
            {
                if(NetworkManager.Instance.IsAllConnected())
                {
                    long _startTime = NetworkManager.GetCurTimeForTick() + NetworkManager.ConvertSecondsToTick(Settings.DelayStartTime);
                    _packetSender.Host.SendPlayRequest(_startTime);
                    VideoManager.Instance.LetsPlay(_startTime);
                    NetworkManager.Instance.StartSnycVideo();
                }
            }
        }
    }

    public class ClientPacketHandler : PacketHandlerBase
    {
        public ClientPacketHandler(PacketSender packetSender) : base(packetSender)
        {
            RegisterHandler(PacketType.JOIN_RESPONSE, OnJoinResponse);
            RegisterHandler(PacketType.PLAY_REQUEST, OnPlayRequest);
            RegisterHandler(PacketType.SYNC_REQUEST, OnSyncRequest);
            RegisterHandler(PacketType.RTT_RESPONSE, OnRttResponse);
        }

        private void OnJoinResponse(NetworkPacket packet, IPEndPoint sender)
        {
            NetworkManager.Instance.SetConnectionState(true);
            UnityEngine.Debug.Log($"this client is connected!");
        }

        private void OnPlayRequest(NetworkPacket packet, IPEndPoint sender)
        {
            VideoManager.Instance.LetsPlay(packet.time + NetworkManager.Instance.Offset);
        }

        private void OnSyncRequest(NetworkPacket packet, IPEndPoint sender)
        {
            VideoManager.Instance.SyncVideoTimeAndWait(packet.time);
        }

        private void OnRttResponse(NetworkPacket packet, IPEndPoint sender)
        {
            long curTime = NetworkManager.GetCurTimeForTick();
            long latency = (curTime - packet.time) / 2;   //(패킷 받은 시각 - 패킷 보낸 시각) / 2
            long hostTime = packet.sendTime + latency;
            NetworkManager.Instance.AddLatency(latency, curTime - hostTime);
            _packetSender.Client.SendRttRequest();
        }
    }

    public class PacketHandler
    {
        public HostPacketHandler Host { get; }
        public ClientPacketHandler Client { get; }

        public PacketHandler(PacketSender packetSender)
        {
            if (packetSender.Host != null)
            {
                Host = new HostPacketHandler(packetSender);
            }
            else
            {
                Host = null;
            }

            Client = new ClientPacketHandler(packetSender);
        }
    }
}
