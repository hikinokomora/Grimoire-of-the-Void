using UnityEngine;

public class PhysicalBookButton : MonoBehaviour
{
    public PhysicalRecipeBook bookScript;
    public bool isNextPage = false;

    // Оставляем это на случай прямого Raycast или PhysicsRaycaster
    private void OnMouseDown()
    {
        ForceClick();
    }

    public void ForceClick()
    {
        if (bookScript != null)
        {
            if (isNextPage)
                bookScript.NextPage();
            else
                bookScript.PrevPage();
            
            Debug.Log($"[PhysicalBookButton] Кнопка '{gameObject.name}' нажата!");
        }
    }
}
