using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuController : MonoBehaviour
{
    [Header("Имена сцен")]
    public string introSceneName = "Intro";

    public void OnPlayClicked()
    {
        SceneManager.LoadScene(introSceneName);
    }
}
