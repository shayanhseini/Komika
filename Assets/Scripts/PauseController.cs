using UnityEngine;

public class PauseController : MonoBehaviour
{
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.P))
        {
            if (Time.timeScale == 0)
                Time.timeScale = 1;
            else
                Time.timeScale = 0;
        }
    }
}