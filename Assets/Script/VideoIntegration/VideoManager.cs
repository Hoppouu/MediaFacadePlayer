using Klak.Spout;
using NUnit.Framework.Constraints;
using System.Collections;
using System.Threading.Tasks;
using Unity.VisualScripting;
using UnityEngine;
using RenderHeads.Media.AVProVideo;

public class VideoManager : MonoBehaviour
{
    public static VideoManager Instance { get; private set; }

    [SerializeField]
    private MediaPlayer _mediaPlayer;
    [SerializeField]
    private Camera _camera;
    [SerializeField]
    private SpoutSender _spoutSender1;
    [SerializeField]
    private SpoutSender _spoutSender2;
    [SerializeField]
    private SpoutSender _spoutSender3;
    public double BiasTick { get; private set; }

    private const double _SYNC_TOLERANCE = 0.3;
    private const double _ADD_SEEK_TIME = 1.5;
    private readonly long _CPU_THRESHOLD = NetworkManager.ConvertSecondsToTick(0.035); 

    private bool _isUsing = false;
    private long _startTargetTime;

    private RenderTexture _rt = null;
    private RenderTexture _rtTop = null;
    private RenderTexture _rtBottom = null;
    private bool _isSideMode = false;

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
    }

    void Start()
    {
        _camera.enabled = true;
        switch (Settings.MyMode)
        {
            case Network.NetworkRole.SIDE:
                _spoutSender1.enabled = false;
                _spoutSender2.enabled = true;
                _spoutSender3.enabled = true;
                _isSideMode = true;
                break;
            case Network.NetworkRole.FRONT:
            case Network.NetworkRole.BOTTOM:
                _spoutSender1.enabled = true;
                _spoutSender2.enabled = false;
                _spoutSender3.enabled = false;
                _isSideMode = false;
                break;
        }

        Debug.Log($"비디오 불러오기 -> {Settings.MyVideoPath}");
        PlayTargetVideo(Settings.MyVideoPath);
        BiasTick = Settings.BiasTick;
        _mediaPlayer.Events.AddListener(OnMediaPlayerEvent);
    }

    private void Update()
    {
        if(Input.GetKeyDown(KeyCode.Q))
        {
            BiasTick -= 0.01;
            Debug.Log($"BiasTick: {BiasTick}");
        }
        else if(Input.GetKeyDown(KeyCode.E))
        {
            BiasTick += 0.01;
            Debug.Log($"BiasTick: {BiasTick}");
        }
    }

    public void LetsPlay(long targetTime)
    {
        _startTargetTime = targetTime;
        if(_syncCoroutine == null)
        {
            _syncCoroutine = StartCoroutine(SyncPlay());
            Debug.Log($"{NetworkManager.ConvertTickToSeconds(_startTargetTime - NetworkManager.GetCurTimeForTick()):F3}s후 시작 예정.");
            Debug.Log($"현재 시각: {NetworkManager.ConvertTickToSeconds(NetworkManager.GetCurTimeForTick()):F3}");
        }
    }
    public MediaPlayer GetPlayer()
    {
        return _mediaPlayer;
    }
    private IEnumerator SyncPlayTime(long _expectedSyncStartTime)
    {

        while (_mediaPlayer.Control.IsSeeking())
        {
            yield return null;
        }

        while (true)
        {
            long currentTick = NetworkManager.GetCurTimeForTick();
            if (currentTick >= _expectedSyncStartTime)
            {
                break;
            }
            long remainingSeconds = _expectedSyncStartTime - currentTick;
            if (remainingSeconds > _CPU_THRESHOLD)
            {
                yield return null;
            }
            else
            {
                //마감시간이 다가오자 CPU는 초집중 상태에 들어갔다.
            }
        }
        _mediaPlayer.Control.Play();
        _isUsing = false;
        _syncCoroutine = null;
    }

    private Coroutine _syncCoroutine;
    public void SyncVideoTimeAndWait(long hostVideoTime, double latency)
    {
        double currentVideoTime = _mediaPlayer.Control.GetCurrentTime();
        double _hostVideoTime = NetworkManager.ConvertUsToSeconds(hostVideoTime) + latency;
        double diff = System.Math.Abs(_hostVideoTime - currentVideoTime);
        if (!_isUsing)
        {
            if (diff > _SYNC_TOLERANCE)
            {
                double seekTime = _hostVideoTime + _ADD_SEEK_TIME;
                _mediaPlayer.Control.Pause();
                _mediaPlayer.Control.Seek(seekTime);

                long _latency = NetworkManager.ConvertSecondsToTick(latency);
                long _biasTick = NetworkManager.ConvertSecondsToTick(BiasTick);
                long _expectedSyncStartTime = NetworkManager.GetCurTimeForTick() + NetworkManager.ConvertSecondsToTick(_ADD_SEEK_TIME) + _biasTick;
                _isUsing = true;

                Debug.Log($"시점 불일치 (차이 -> {diff:F3}s) || {currentVideoTime:F3}s -> {currentVideoTime + diff}s -> {seekTime:F3}s, {_ADD_SEEK_TIME}s 앞서 Seek 후 대기.");
                if (_syncCoroutine == null)
                {
                    _syncCoroutine = StartCoroutine(SyncPlayTime(_expectedSyncStartTime));
                }
            }
        }
    }

    private void PlayTargetVideo(string videoPath)
    {        
        bool success = _mediaPlayer.OpenMedia(videoPath, autoPlay: false);

        if (success)
        {
            Debug.Log($"[{videoPath}] 미디어 로드 성공.");
        }
        else
        {
            Debug.LogError($"[{videoPath}] 미디어 로드 실패. 경로를 확인하세요.");
        }
    }

    private IEnumerator SyncPlay()
    {
        while(true)
        {
            if (NetworkManager.GetCurTimeForTick() >= _startTargetTime)
            {
                break;
            }

            long diff = _startTargetTime - NetworkManager.GetCurTimeForTick();
            if(diff >= _CPU_THRESHOLD)
            {
                yield return null;
            }
            else
            {
                //정확한 싱크를 위해 메인쓰레드 잠깐 독점
            }
        }
        _mediaPlayer.Play();
        _syncCoroutine = null;
        Debug.Log($"{NetworkManager.ConvertTickToSeconds(NetworkManager.GetCurTimeForTick()):F3}s에 동기화 재생 시작 완료");
    }
    private void OnMediaPlayerEvent(MediaPlayer mediaPlayer, MediaPlayerEvent.EventType eventType, ErrorCode code)
    {
        if (eventType == MediaPlayerEvent.EventType.FirstFrameReady)
        {
            int width = _mediaPlayer.Info.GetVideoWidth();
            int height = _mediaPlayer.Info.GetVideoHeight();
            _rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            _camera.targetTexture = _rt;

            if (_isSideMode)
            {
                _rtTop = new RenderTexture(width, height / 2, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                _rtBottom = new RenderTexture(width, height / 2, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);

                _spoutSender2.sourceTexture = _rtTop;
                _spoutSender3.sourceTexture = _rtBottom;

                _spoutSender2.spoutName = Settings.MyMode.ToString() + "_LEFT";
                _spoutSender3.spoutName = Settings.MyMode.ToString() + "_RIGHT";

                StartCoroutine(LetsBlit());
            }
            else
            {
                _spoutSender1.sourceCamera = _camera;
                _spoutSender1.sourceTexture = _rt;
                _spoutSender1.spoutName = Settings.MyMode.ToString();
            }
        }
    }

    private IEnumerator LetsBlit()
    {
        while(true)
        {
            Graphics.SetRenderTarget(_rtTop);
            Graphics.Blit(_rt, _rtTop, new Vector2(1.0f, 0.5f), new Vector2(0.0f, 0.5f));

            Graphics.SetRenderTarget(_rtBottom);
            Graphics.Blit(_rt, _rtBottom, new Vector2(1.0f, 0.5f), new Vector2(0.0f, 0.0f));
            yield return null;
        }
    }

}