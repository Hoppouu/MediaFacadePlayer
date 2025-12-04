using Network;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using UnityEngine;

public static class Settings
{
    #region const variable
    public const string MODE = "Mode";
    public const string FRONT = "Front";
    public const string SIDE = "Side";
    public const string BOTTOM = "Bottom";
    public const string FRONT_IP = "Front_IP";
    public const string FRONT_PORT = "Front_Port";
    public const string SIDE_IP = "Side_IP";
    public const string SIDE_PORT = "Side_Port";
    public const string BOTTOM_IP = "Bottom_IP";
    public const string BOTTOM_PORT = "Bottom_Port";
    public const string DELAY_START_TIME = "Delay_Start_Time";

    private const string SETTINGS_FILE_NAME = "settings.ini";
    private const string VIDEO_PATH_FILE_NAME = "FilePath.txt";
    #endregion

    #region 파일 I/O로 읽어오는 변수들
    public static IPEndPoint HostIPE { get; private set; }
    public static IPEndPoint SideIPE { get; private set; }
    public static IPEndPoint BottomIPE { get; private set; }
    public static IPEndPoint MyIPE { get; private set; }    //MyMode에 따라서 IP/PORT가 들어갑니다.
    public static NetworkRole MyMode { get; private set; }
    public static string VideoPath { get; private set; }
    public static string FrontVideoPath { get; private set; }
    public static string SideVideoPath { get; private set; }
    public static string BottomVideoPath { get; private set; }
    public static string MyVideoPath { get; private set; }  //MyMode에 따라서 비디오 경로(_F, _S, _B 중 하나)가 들어갑니다.
    public static float DelayStartTime { get; private set; }

    private static Dictionary<string, string> _dict;
    private static string _settingsFilePath;
    private static string _videoPathPath;
    #endregion


    static Settings()
    {
        string streamingAssetsPath = Application.streamingAssetsPath;
        _settingsFilePath = Path.Combine(streamingAssetsPath, SETTINGS_FILE_NAME);
        _videoPathPath = Path.Combine(streamingAssetsPath, VIDEO_PATH_FILE_NAME);

        _dict = LoadSettings();
        VideoPath = LoadVideoPath();

        HostIPE = new IPEndPoint(IPAddress.Parse(_dict[FRONT_IP]), int.Parse(_dict[FRONT_PORT]));
        SideIPE = new IPEndPoint(IPAddress.Parse(_dict[SIDE_IP]), int.Parse(_dict[SIDE_PORT]));
        BottomIPE = new IPEndPoint(IPAddress.Parse(_dict[BOTTOM_IP]), int.Parse(_dict[BOTTOM_PORT]));
        DelayStartTime = float.Parse(_dict[DELAY_START_TIME]);
        SetValueByMyMode(_dict[MODE]);

#if UNITY_EDITOR
        if (DebugMode.debugModeOn)
        {
            SetValueByMyMode(DebugMode.debugMyMode);
        }
#endif

    }

    private static void SetValueByMyMode(string myMode)
    {
        switch (myMode)
        {
            case FRONT:
                MyMode = NetworkRole.FRONT;
                MyIPE = HostIPE;
                MyVideoPath = FrontVideoPath;
                break;

            case SIDE:
                MyMode = NetworkRole.SIDE;
                MyIPE = SideIPE;
                MyVideoPath = SideVideoPath;
                break;

            case BOTTOM:
                MyMode = NetworkRole.BOTTOM;
                MyIPE = BottomIPE;
                MyVideoPath = BottomVideoPath;
                break;
        }
    }

    private static Dictionary<string, string> LoadSettings()
    {
        if (File.Exists(_settingsFilePath))
        {
            Debug.Log($"[INFO] 설정 파일 발견: {_settingsFilePath}");
            return ReadSettingsFromFile();
        }
        else
        {
            Debug.Log($"[INFO] 설정 파일 없음. {_settingsFilePath}에 기본 양식으로 파일 생성 중...");
            CreateDefaultSettingsFile();
            return ReadSettingsFromFile();
        }
    }

    private static Dictionary<string, string> ReadSettingsFromFile()
    {
        var settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            string[] lines = File.ReadAllLines(_settingsFilePath);

            foreach (string line in lines)
            {
                if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith("#"))
                    continue;

                int separatorIndex = line.IndexOf('=');
                if (separatorIndex > 0)
                {
                    string key = line.Substring(0, separatorIndex).Trim();
                    string value = line.Substring(separatorIndex + 1).Trim();

                    if (!string.IsNullOrEmpty(key))
                    {
                        settings[key] = value;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ERROR] 설정 파일 읽기 실패: {ex.Message}");
        }
        return settings;
    }

    private static void CreateDefaultSettingsFile()
    {
        string directoryPath = Path.GetDirectoryName(_settingsFilePath);
        if (!Directory.Exists(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        string defaultContent =
@$"{MODE}={FRONT}
{FRONT_IP}=127.0.0.1
{FRONT_PORT}=11000
{SIDE_IP}=127.0.0.1
{SIDE_PORT}=0
{BOTTOM_IP}=127.0.0.1
{BOTTOM_PORT}=0
{DELAY_START_TIME}=2
";
        try
        {
            File.WriteAllText(_settingsFilePath, defaultContent, Encoding.UTF8);
            Debug.Log($"[INFO] {_settingsFilePath} 파일 생성 완료.");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ERROR] 설정 파일 생성 실패: {ex.Message}");
        }
    }


    private static string AddVideoName(string originalPath, string addString)
    {
        string extension = Path.GetExtension(originalPath);
        string pathWithoutExtension = Path.ChangeExtension(originalPath, null);
        string modifiedPath = pathWithoutExtension + addString;
        string finalPath = modifiedPath + extension;
        return finalPath;
    }

    private static string LoadVideoPath()
    {
        if (File.Exists(_videoPathPath))
        {
            Debug.Log($"[INFO] 비디오 경로 파일 발견: {_videoPathPath}");
            try
            {
                string originalPath = File.ReadAllText(_videoPathPath, Encoding.UTF8).Trim();
                FrontVideoPath = AddVideoName(originalPath, "_F");
                SideVideoPath = AddVideoName(originalPath, "_S");
                BottomVideoPath = AddVideoName(originalPath, "_B");
                return originalPath;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ERROR] 비디오 경로 파일 읽기 실패: {ex.Message}");
                return string.Empty;
            }
        }
        else
        {
            Debug.Log($"[INFO] 비디오 경로 파일 없음. {_videoPathPath}에 파일 생성 중...");
            CreateDefaultVideoPathFile();
            return LoadVideoPath();
        }
    }
    private static void CreateDefaultVideoPathFile()
    {
        string directoryPath = Path.GetDirectoryName(_videoPathPath);
        if (!Directory.Exists(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }
        string defaultPathContent = "C:\\YourVideoFolderPath\\";
        try
        {
            File.WriteAllText(_videoPathPath, defaultPathContent, Encoding.UTF8);
            Debug.Log($"[INFO] {_videoPathPath} 파일 생성 완료. 기본값: {defaultPathContent}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ERROR] 비디오 경로 파일 생성 실패: {ex.Message}");
        }
    }
}