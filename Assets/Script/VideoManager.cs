using Klak.Spout;
using NUnit.Framework.Constraints;
using RenderHeads.Media.AVProVideo;
using System.Collections;
using System.Threading.Tasks;
using Unity.VisualScripting;
using UnityEngine;

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

    private const double _SYNC_TOLERANCE = 0.5;
    private const double _ADD_SEEK_TIME = 1.0;

    private bool _isUsing = false;
    private bool _isWaitingPlay = false;

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
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
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

        switch (Settings.MyMode)
        {
            case Network.NetworkRole.SIDE:
            case Network.NetworkRole.BOTTOM:
            _mediaPlayer.PlatformOptionsWindows._audioMode = Windows.AudioOutput.None;
                break;
            case Network.NetworkRole.FRONT:
            _mediaPlayer.PlatformOptionsWindows._audioMode = Windows.AudioOutput.System;
                break;
        }

    }

    void Start()
    {
        Debug.Log($"비디오 불러오기 -> {Settings.MyVideoPath}");
        PlayTargetVideo(Settings.MyVideoPath);

        _mediaPlayer.Events.AddListener(OnMediaPlayerEvent);
    }

    private void Update()
    {
        SyncPlay();
    }

    public void LetsPlay(long targetTime)
    {
        _startTargetTime = targetTime;
        _isWaitingPlay = true;
        Debug.Log($"[SYNC] 로컬({NetworkManager.ConvertTickToSeconds(_startTargetTime - NetworkManager.GetCurTimeForTick())}s)후 시작 예정.");
    }
    public MediaPlayer GetPlayer()
    {
        return _mediaPlayer;
    }

    IEnumerator SyncPlayTime(long _expectedSyncStartTime)
    {
        while (true)
        {
            long currentTick = NetworkManager.GetCurTimeForTick();
            if (currentTick >= _expectedSyncStartTime)
            {
                _mediaPlayer.Control.Play();
                break;
            }
            double remainingSeconds = NetworkManager.ConvertTickToSeconds(_expectedSyncStartTime - currentTick);
            if (remainingSeconds > 0.035f)
            {
                yield return null;
            }
            else
            {
                //마감시간이 다가오자 CPU는 초집중 상태에 들어갔다.
            }
        }
        _isUsing = false;
        _syncCoroutine = null;
    }

    private Coroutine _syncCoroutine;
    public void SyncVideoTimeAndWait(long hostVideoTime, double latency)
    {
        double currentVideoTime = _mediaPlayer.Control.GetCurrentTime();
        double _hostVideoTime = NetworkManager.ConvertUsToSeconds(hostVideoTime);
        double diff = System.Math.Abs((_hostVideoTime + latency) - currentVideoTime);
        if (!_isUsing)
        {
            if (diff > _SYNC_TOLERANCE)
            {
                double seekTime = _hostVideoTime + _ADD_SEEK_TIME;
                _mediaPlayer.Control.Pause();
                _mediaPlayer.Control.Seek(seekTime);

                long _latency = NetworkManager.ConvertSecondsToTick(latency);
                long _biasTick = NetworkManager.ConvertSecondsToTick(-0.15);
                long _expectedSyncStartTime = NetworkManager.GetCurTimeForTick() + NetworkManager.ConvertSecondsToTick(_ADD_SEEK_TIME) - _latency + _biasTick;
                _isUsing = true;

                Debug.Log($"[CLIENT] 시점 불일치 (차이 -> {diff:F3}s) || {currentVideoTime:F3}s -> {currentVideoTime + diff}s -> {seekTime:F3}s, {_ADD_SEEK_TIME}s 앞서 Seek 후 대기.");
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

    private void SyncPlay()
    {
        if (_isWaitingPlay)
        {
            if (NetworkManager.GetCurTimeForTick() >= _startTargetTime)
            {
                _mediaPlayer.Play();
                _isWaitingPlay = false;
                Debug.Log($"[SYNC] {NetworkManager.ConvertTickToSeconds(NetworkManager.GetCurTimeForTick()):F3}s에 동기화 재생 시작!");
            }
        }
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