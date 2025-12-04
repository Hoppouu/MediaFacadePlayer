using Network;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using UnityEngine;

namespace Network
{
    public class NetworkEntry
    {
        public bool isConnected;
        public int rttPacketCount;
        public IPEndPoint iPEndPoint;

        public NetworkEntry()
        {
            isConnected = false;
            rttPacketCount = 0;
            iPEndPoint = null;
        }
    }

    struct TimeStruct
    {
        public long latency;
        public long offset;
    }
}

public class NetworkManager : MonoBehaviour
{
    #region static values
    public static NetworkManager Instance { get; private set; }
    public static long GetCurTimeForTick()
    {
        return System.Diagnostics.Stopwatch.GetTimestamp();
    }

    public static double ConvertTickToSeconds(long time)
    {
        return (double)time / System.Diagnostics.Stopwatch.Frequency;
    }
    public static long ConvertSecondsToTick(double time)
    {
        return (long)(time * System.Diagnostics.Stopwatch.Frequency);
    }

    public static long ConvertSecondsToUs(double time)
    {
        return (long)(time * 1000000);
    }
    public static double ConvertUsToSeconds(long time)
    {
        return (double)time / 1000000;
    }

    #endregion

    public PacketDispatcher PacketDispatcher {  get; private set; }
    public long Offset { get; private set; } = 0;
    public double Latency { get; private set; } = 0;

    public const float PACKET_INTERVAL_TIME = 0.5f;
    private Dictionary<NetworkRole, NetworkEntry> _clientEntry;
    private bool _isConnected = false;

    private List<TimeStruct> _latencyList;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
        PacketDispatcher = GetComponent<PacketDispatcher>();
        _clientEntry = new Dictionary<NetworkRole, NetworkEntry>();
        _latencyList = new List<TimeStruct>();

        _clientEntry.Add(NetworkRole.SIDE, new NetworkEntry());
        _clientEntry.Add(NetworkRole.BOTTOM, new NetworkEntry());
    }

    private void Start()
    {
        if (Settings.MyMode != NetworkRole.FRONT) StartCoroutine(ConnectToHost());

    }

    private IEnumerator SendSyncPacket()
    {
        while (true)
        {
            if (IsAllConnected() && VideoManager.Instance.GetPlayer().Control.IsPlaying())
            {
                if (PacketDispatcher.IsHost())
                {
                    long currentTIme = ConvertSecondsToUs(VideoManager.Instance.GetPlayer().Control.GetCurrentTime());
                    PacketDispatcher.HostSender.SendSyncRequest(currentTIme);
                }
            }
            yield return new WaitForSeconds(PACKET_INTERVAL_TIME);
        }
    }

    private IEnumerator ConnectToHost()
    {
        while(!_isConnected)
        {
            PacketDispatcher.ClientSender.SendJoinRequest();
            yield return new WaitForSeconds(PACKET_INTERVAL_TIME);
        }
        PacketDispatcher.ClientSender.SendRttRequest();
        Debug.Log("RTT_REQUEST 패킷 시작");
    }

    #region 패킷 핸들러/센더가 사용하는 함수
    private bool isCalced = false;
    public void AddLatency(long latency, long calculatedOffset)
    {
        if (isCalced) return;
        _latencyList.Add(new TimeStruct { latency = latency, offset = calculatedOffset });
        if (_latencyList.Count >= 100)
        {
            _latencyList.Sort((a, b) => a.latency.CompareTo(b.latency));

            long sumOffset = 0;
            long sumLatency = 0;
            int useCount = 15;

            for (int i = 0; i < useCount; i++)
            {
                sumOffset += _latencyList[i].offset;
                sumLatency += _latencyList[i].latency;
            }

            Offset = sumOffset / useCount;
            Latency = ConvertTickToSeconds(sumLatency) / useCount;
            string str = "Offset 설정 완료!\n";
            str += $"Offset = {Offset} -> ({ConvertTickToSeconds(Offset)}s)\n";
            str += $"Latency = {Latency:F6}s";
            Debug.Log(str);
            Debug.Log("RTT_REQUEST 완료");
            _latencyList.Clear();
            isCalced = true;
        }
    }
    public bool IsAllConnected()
    {
        foreach (NetworkEntry networkEntry in _clientEntry.Values)
        {
            if (networkEntry.isConnected == false) return false;
        }
        
        return true;
    }

    public void SetConnectionState(bool state)
    {
        _isConnected = state;
    }

    public void SetConnectionState(NetworkRole role, bool state, IPEndPoint iPEndPoint)
    {
        if (_clientEntry.ContainsKey(role))
        {
            _clientEntry[role].isConnected = false;
            _clientEntry[role].iPEndPoint = iPEndPoint;
            Debug.Log($"{role} is connected.");
        }
        else
        {
            Debug.LogError($"AddClient: Invalid networkrole => {role}");
            return;
        }
    }

    public bool SetConnectionState(NetworkRole role, int addNum)
    {
        if (_clientEntry.ContainsKey(role))
        {
            _clientEntry[role].rttPacketCount += addNum;
            if (_clientEntry[role].rttPacketCount >= 140)
            {
                _clientEntry[role].isConnected = true;
                return true;
            }
            else return false;
        }
        else
        {
            Debug.LogError($"AddClient: Invalid networkrole => {role}");
            return false;
        }
    }

    public IPEndPoint GetClientIPE(NetworkRole role)
    {
        return _clientEntry[role].iPEndPoint;
    }

    public void StartSnycVideo()
    {
        StartCoroutine(SendSyncPacket());
    }
    #endregion

}
