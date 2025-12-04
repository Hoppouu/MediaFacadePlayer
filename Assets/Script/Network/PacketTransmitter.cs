using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;


namespace Network
{
    public enum NetworkRole : byte
    {
        FRONT,
        SIDE,
        BOTTOM
    };
    public enum PacketType : byte
    {
        NONE = 0,
        JOIN_REQUEST = 1,
        JOIN_RESPONSE = 2,
        RTT_REQUEST = 3,
        RTT_RESPONSE = 4,
        PLAY_REQUEST = 11,
        SYNC_REQUEST = 12,
        SYNC_RESPONSE = 13
    };

    public class NetworkPacket
    {
        public readonly NetworkRole senderType;
        public readonly PacketType packetType;
        public readonly long sendTime;
        public readonly long time;


        public NetworkPacket() { } 
        //수신용
        public NetworkPacket(NetworkRole senderType, PacketType packetType, long sendTime, long time)
        {
            this.senderType = senderType;
            this.packetType = packetType;
            this.sendTime = sendTime;
            this.time = time;
        }

        //송신용
        public NetworkPacket(NetworkRole senderType, PacketType packetType)
        {
            this.senderType = senderType;
            this.packetType = packetType;
            this.sendTime = NetworkManager.GetCurTimeForTick();
            this.time = 0;
        }
        public NetworkPacket(NetworkRole senderType, PacketType packetType, long time)
        {
            this.senderType = senderType;
            this.packetType = packetType;
            this.sendTime = NetworkManager.GetCurTimeForTick();
            this.time = time;
        }
    }

    public class ReceivedPacket
    {
        public NetworkPacket Packet { get; }
        public IPEndPoint Sender { get; }

        public ReceivedPacket(NetworkPacket packet, IPEndPoint sender)
        {
            Packet = packet;
            Sender = sender;
        }
    }


    public class PacketTransmitter : IDisposable
    {
        public Queue<ReceivedPacket> _receivedPackets;
        public bool IsHost { get; private set; }

        private readonly UdpClient _udpClient;
        private readonly CancellationTokenSource _cts;

        public PacketTransmitter(NetworkRole myMode, IPEndPoint myIPE)
        {

            if (myMode == NetworkRole.FRONT)    IsHost = true;
            else IsHost = false;

            _udpClient = new UdpClient(myIPE.Port);
            UnityEngine.Debug.Log($"Start udp on {myMode} mode by {myIPE}");
            if(myIPE.Port == 0) UnityEngine.Debug.Log($"0 port is bound to {((IPEndPoint)_udpClient.Client.LocalEndPoint).Port}");

            _cts = new CancellationTokenSource();
            _receivedPackets = new Queue<ReceivedPacket>();

            Task.Run(() => ReceiveLoop(_cts.Token));
        }

        ~PacketTransmitter()
        {
            _udpClient.Close();
        }
        public void Dispose()
        {
            if(_cts != null)
            {
                _cts.Cancel();
            }

            if(_udpClient != null)
            {
                _udpClient.Close();
                UnityEngine.Debug.Log("Udp disposion complete.");
            }
            
        }

        public bool TryDequeuePacket(out ReceivedPacket receivedPacket)
        {
            return _receivedPackets.TryDequeue(out receivedPacket);
        }

        public void SendToHost(NetworkPacket packet)
        {
            if (_udpClient == null) return;

            try
            {
                byte[] data = Serialize(packet);
                _udpClient.Send(data, data.Length, Settings.HostIPE);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"UDP Send Error: {ex.Message}");
            }
        }

        public void SendToClinet(NetworkPacket packet, IPEndPoint target)
        {
            if (_udpClient == null) return;

            try
            {
                byte[] data = Serialize(packet);
                _udpClient.Send(data, data.Length, target);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"UDP Send Error: {ex.Message}");
            }
        }

        public void SendToClientByBroadcast(NetworkPacket packet)
        {
            SendToClinet(packet, NetworkManager.Instance.GetClientIPE(NetworkRole.SIDE));
            SendToClinet(packet, NetworkManager.Instance.GetClientIPE(NetworkRole.BOTTOM));
        }

        private async Task ReceiveLoop(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                UdpReceiveResult result;
                IPEndPoint sender;
                try
                {
                    result = await _udpClient.ReceiveAsync();
                    sender = result.RemoteEndPoint;

                    byte[] data = result.Buffer;

                    if(data.Length > 0)
                    {
                        NetworkPacket packet = Deserialize(data);
                        _receivedPackets.Enqueue(new ReceivedPacket(packet, sender));
                    }
                }
                catch (SocketException ex) when (ex.SocketErrorCode == SocketError.ConnectionReset)
                {
                    UnityEngine.Debug.LogWarning($"UDP Receive: ICMP Port Unreachable 응답 수신. 다음 수신 시도. (호스트 접속 대기 중)");
                }
                catch (Exception ex)
                {
                    if(!_cts.IsCancellationRequested)
                    {
                        UnityEngine.Debug.LogError($"UDP Receive Error: {ex.Message}");
                    }
                }
            }


        }

        private byte[] Serialize(NetworkPacket packet)
        {
            using (MemoryStream memoryStream = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(memoryStream))
            {
                writer.Write((byte)packet.senderType);
                writer.Write((byte)packet.packetType);
                writer.Write(packet.sendTime);
                writer.Write(packet.time);
                return memoryStream.ToArray();
            }
        }

        private NetworkPacket Deserialize(byte[] data)
        {
            NetworkRole _senderType = (NetworkRole)data[0];
            PacketType _packetType = (PacketType)data[1];
            long _sendTime = BitConverter.ToInt64(data, 2);
            long _time = BitConverter.ToInt64(data, 10);

            return new NetworkPacket(_senderType, _packetType, _sendTime, _time);
        }
    }
}


