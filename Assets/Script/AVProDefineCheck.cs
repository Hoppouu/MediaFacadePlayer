#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Build;
using System.IO;

[InitializeOnLoad]
public static class AVProDefineCheck
{
    private const string DefineSymbol = "AVPRO_VIDEO_PRESENT";
    private const string CoreName = "AVProVideo";
    private static bool _avProIsPresent = false;

    static AVProDefineCheck()
    {
        CheckForAVProVideoFiles();
        SetDefineSymbol();
    }

    private static void CheckForAVProVideoFiles()
    {
        string avproFolderPath = Application.dataPath + $"/{CoreName}";
        _avProIsPresent = Directory.Exists(avproFolderPath);
    }

    private static void SetDefineSymbol()
    {
        BuildTargetGroup targetGroup = EditorUserBuildSettings.selectedBuildTargetGroup;
        NamedBuildTarget namedTarget = NamedBuildTarget.FromBuildTargetGroup(targetGroup);

        string definesString = PlayerSettings.GetScriptingDefineSymbols(namedTarget);
        List<string> allDefines = new List<string>(definesString.Split(new[] { ';' }, System.StringSplitOptions.RemoveEmptyEntries));

        bool requiresUpdate = false;

        if (_avProIsPresent)
        {
            if (!allDefines.Contains(DefineSymbol))
            {
                allDefines.Add(DefineSymbol);
                requiresUpdate = true;
                Debug.Log($"[AVProDefineCheck] '{DefineSymbol}' 심볼이 추가되었습니다. (AVPro Video 감지됨)");
            }
        }
        else
        {
            if (allDefines.Contains(DefineSymbol))
            {
                allDefines.Remove(DefineSymbol);
                requiresUpdate = true;
                Debug.Log($"[AVProDefineCheck] '{DefineSymbol}' 심볼이 제거되었습니다. (AVPro Video 미감지)");
            }
        }

        if (requiresUpdate)
        {
            string newDefinesString = string.Join(";", allDefines.Distinct().ToArray());
            PlayerSettings.SetScriptingDefineSymbols(namedTarget, newDefinesString);
        }
    }
}
#endif