using Network;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using Unity.Mathematics;
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

    private Dictionary<NetworkRole, NetworkEntry> _clientEntry;
    private List<TimeStruct> _latencyList;
    private bool _isConnected = false;
    private bool _isCalcedOffset = false;

    private const float _PACKET_INTERVAL_TIME = 1.5f;
    private const float _RTT_INTERVAL_TIME = 0.01f;
    private const int _RTT_PACKET_NUM = 100;
    private const int _RTT_USE_PACKET_PERCENT = 10;
    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
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
    private IEnumerator ConnectToHost()
    {
        while (!_isConnected)
        {
            PacketDispatcher.ClientSender.SendJoinRequest();
            yield return new WaitForSeconds(_PACKET_INTERVAL_TIME);
        }
        StartCoroutine(StartHandshake());
    }
    private IEnumerator UntilRttDone()
    {
        while (!PacketDispatcher.ClientHandler.IsRttDone)
        {
            PacketDispatcher.ClientSender.SendRttDoneRequest();
            yield return new WaitForSeconds(_RTT_INTERVAL_TIME);
        }
    }

    private IEnumerator StartHandshake()
    {
        Debug.Log("Handshake 시작");
        float maxDuration = 5f;
        float curTime = 0f;
        while(_latencyList.Count <= _RTT_PACKET_NUM && curTime <= maxDuration)
        {
            PacketDispatcher.ClientSender.SendRttRequest();
            yield return new WaitForSeconds(_RTT_INTERVAL_TIME);
            curTime += _RTT_INTERVAL_TIME;
        }

        if(_latencyList.Count >= _RTT_USE_PACKET_PERCENT)
        {
            float ratio = _RTT_USE_PACKET_PERCENT / 100f;
            float _ratioNum = _latencyList.Count * ratio;
            int _useCount = (int)math.ceil(_ratioNum);
            CalcOffset(_useCount);
            StartCoroutine(UntilRttDone());
        }
        else
        {
            Debug.LogError($"Handshake 실패: 수신된 패킷 {_latencyList.Count}개");
        }
    }

    /// <param name="useCount">평균을 낼 패킷의 수</param>
    private void CalcOffset(int useCount)
    {
        _latencyList.Sort((a, b) => a.latency.CompareTo(b.latency));

        long sumOffset = 0;

        for (int i = 0; i < useCount; i++)
        {
            sumOffset += _latencyList[i].offset;
        }

        Offset = sumOffset / useCount;
        Debug.Log($"Offset 설정 완료 (Offset = {Offset}, Received RTT Packets = {_latencyList.Count})");
        Debug.Log("Handshake 완료");
        _isCalcedOffset = true;
    }

    #region 패킷 핸들러/센더가 사용하는 함수
    public long GetLatency(long sendTime)
    {
        return GetCurTimeForTick() - (sendTime + Offset);
    }
    public void AddLatency(long latency, long calculatedOffset)
    {
        if (_isCalcedOffset) return;
        _latencyList.Add(new TimeStruct { latency = latency, offset = calculatedOffset });
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

    public void CountClinetPacket(NetworkRole role)
    {
        if (_clientEntry.ContainsKey(role))
        {
            _clientEntry[role].rttPacketCount += 1;
            if (_clientEntry[role].rttPacketCount >= _RTT_PACKET_NUM)
            {
                _clientEntry[role].isConnected = true;
            }
        }
        else
        {
            Debug.LogError($"AddClient: Invalid networkrole => {role}");
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
            yield return new WaitForSeconds(_PACKET_INTERVAL_TIME);
        }
    }
#endregion

}
