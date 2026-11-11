using UnityEngine;

public class SaveManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameManager gameManager;


    private const string TotalCoinKey = "TotalCoin";
    private const string HighScoreKey = "HighScore";


    private void OnEnable()
    {
        GameEvents.GameOver += SaveTotalCoin;
        GameEvents.GameOver += SaveHighScore;
    }


    private void OnDisable()
    {
        GameEvents.GameOver -= SaveTotalCoin;
        GameEvents.GameOver -= SaveHighScore;
    }


    private void SaveTotalCoin()
    {
        int currentCoin =
            gameManager.Score;

        int totalCoin =
            GetTotalCoin() + currentCoin;
        
        PlayerPrefs.SetInt(
            TotalCoinKey,
            totalCoin
        );

        PlayerPrefs.Save();
    }

    private void SaveHighScore()
    {
        float currentScore =
            gameManager.Distance;

        float highestScore =
            GetHighestScore();
        
        if (currentScore < highestScore)
            return;
        
        PlayerPrefs.SetFloat(
            HighScoreKey,
            currentScore
        );

        PlayerPrefs.Save();
    }


    public int GetTotalCoin()
    {
        return PlayerPrefs.GetInt(
            TotalCoinKey,
            0
        );
    }

    public float GetHighestScore()
    {
        return PlayerPrefs.GetFloat(
            HighScoreKey,
            0
        );
    }
}