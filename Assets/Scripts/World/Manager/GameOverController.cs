using UnityEngine;

public class GameOverController : MonoBehaviour
{
    [SerializeField] private GameObject gameOverPanel;


    private void Awake()
    {
        gameOverPanel.SetActive(false);
    }


    private void OnEnable()
    {
        GameEvents.GameOver += ShowGameOver;
    }


    private void OnDisable()
    {
        GameEvents.GameOver -= ShowGameOver;
    }


    private void ShowGameOver()
    {
        gameOverPanel.SetActive(true);
    }
}