using UnityEngine;
using UnityEngine.InputSystem;

public class RecipeBookInput : MonoBehaviour
{
    public RecipeBook recipeBook;
    
    // Если у вас сгенерирован C# класс из InputActionAsset, например "InputSystem_Actions", 
    // его можно использовать здесь. Ниже приведен пример с использованием InputActionAsset напрямую, 
    // либо можно использовать PlayerInput.

    public InputActionReference toggleBookAction;

    private void OnEnable()
    {
        if (toggleBookAction != null)
        {
            toggleBookAction.action.performed += OnToggleBook;
            toggleBookAction.action.Enable();
        }
    }

    private void OnDisable()
    {
        if (toggleBookAction != null)
        {
            toggleBookAction.action.performed -= OnToggleBook;
            toggleBookAction.action.Disable();
        }
    }

    private void OnToggleBook(InputAction.CallbackContext context)
    {
        if (recipeBook != null)
        {
            recipeBook.ToggleBook();
        }
    }
}

