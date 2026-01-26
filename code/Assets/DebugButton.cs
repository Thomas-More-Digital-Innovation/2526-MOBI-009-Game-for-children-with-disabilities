using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public class DebugButton : MonoBehaviour
{
    public void LoadGame1()
    {
        SceneManager.LoadScene(1);
    }
    public void LoadGame2()
    {
        SceneManager.LoadScene(2);
    }
}
