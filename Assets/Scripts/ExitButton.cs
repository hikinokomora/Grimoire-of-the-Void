using UnityEngine;

public class ExitButton : MonoBehaviour
{
    public void QuitGame()
    {
        Debug.Log("🚪 Выход из игры...");
#if UNITY_EDITOR
        // В редакторе просто останавливаем Play Mode
        UnityEditor.EditorApplication.isPlaying = false;
#else
        // В билде закрываем приложение
        Application.Quit();
#endif
    }
}