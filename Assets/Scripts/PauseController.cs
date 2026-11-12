using UnityEngine;

public class PauseController : MonoBehaviour
{
    public void TogglePause()
    {
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
