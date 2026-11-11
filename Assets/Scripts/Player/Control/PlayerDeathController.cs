using UnityEngine;

public class PlayerDeathController : MonoBehaviour
{
    private void OnEnable()
    {
        GameEvents.GameOver += DisablePlayer;
    }


    private void OnDisable()
    {
        GameEvents.GameOver -= DisablePlayer;
    }


    private void DisablePlayer()
    {
        gameObject.SetActive(false);
    }
}
