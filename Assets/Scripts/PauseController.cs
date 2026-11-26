using UnityEngine;
using UnityEngine.InputSystem;

public class PauseController : MonoBehaviour
{
    [SerializeField] private KomikaUIController uiController;

    public void TogglePause()
    {
        if (uiController != null)
        {
            uiController.TogglePauseMenu();
        }
    }

    void Update()
    {
        Keyboard kb = Keyboard.current;
        if (kb != null && (kb.pKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame))
        {
            TogglePause();
        }
    }
}
