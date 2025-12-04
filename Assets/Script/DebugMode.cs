using Klak.Spout;
using RenderHeads.Media.AVProVideo;
using UnityEngine;

public class DebugMode : MonoBehaviour
{
    public enum DisplayMode
    {
        NotUseDebugMode,
        Front,
        Side,
        Bottom
    }
    [SerializeField] private DisplayMode currentMode = DisplayMode.NotUseDebugMode;

    public static bool debugModeOn;
    public static string debugMyMode;

    private void OnValidate()
    {
#if !UNITY_EDITOR
        currentMode = DisplayMode.NotUseDebugMode;
#endif
#if UNITY_EDITOR
        if (currentMode == DisplayMode.NotUseDebugMode)
        {
            debugModeOn = false;
        }
        else
        {
            debugModeOn = true;
            switch (currentMode)
            {
                case DisplayMode.Front:
                    debugMyMode = Settings.FRONT;
                    break;
                case DisplayMode.Side:
                    debugMyMode = Settings.SIDE;
                    break;
                case DisplayMode.Bottom:
                    debugMyMode = Settings.BOTTOM;
                    break;
            }
        }
#endif
    }
}
