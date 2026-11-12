using UnityEngine;

public class PauseController : MonoBehaviour
{
    [SerializeField] private KomikaUIController uiController;

    public void TogglePause()
    {
        if (uiController != null)
            uiController.TogglePauseMenu();
        else
            Time.timeScale = Mathf.Approximately(Time.timeScale, 0f) ? 1f : 0f;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.P))
        {
            TogglePause();
        }
    }
}
