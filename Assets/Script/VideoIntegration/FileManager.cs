using UnityEngine;

public class FileManager : MonoBehaviour
{
    private void Awake()
    {
        Settings.Init();
    }
    private void OnApplicationQuit()
    {
        Settings.SaveAll();
    }
}
