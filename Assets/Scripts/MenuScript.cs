using NUnit.Framework.Constraints;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuController : MonoBehaviour
{
    [Header("Имена сцен")]
    public string introSceneName = "Intro";
    [SerializeField] private GameObject settings;

    public void OnPlayClicked()
    {
        SceneManager.LoadScene(introSceneName);
    }
    public void OpenSettings (){settings.SetActive(true);}
    public void CloseSettings()
    {
        settings.SetActive(false);
    }
}
    