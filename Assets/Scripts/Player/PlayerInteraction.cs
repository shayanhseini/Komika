using UnityEngine;

public class PlayerInteraction : MonoBehaviour
{
    [SerializeField] private GameManager gameManager;
    
    public void HitObstacle()
    {
        GameEvents.RaisePlayerHit();
        DestroyPlayer();
    }

    

    private void DestroyPlayer()
    {
        Debug.Log("Player Destroyed");

        gameManager.GameOver();
    }
}
