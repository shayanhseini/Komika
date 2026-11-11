using UnityEngine;

public class DifficultyManager : MonoBehaviour
{
    [Header("Score")]
    [SerializeField] private int maxScore = 500;


    [Header("Coins")]
    [SerializeField] private int startCoinCount = 10;
    [SerializeField] private int maxCoinCount = 20;


    [Header("Obstacles")]
    [SerializeField] private int startObstacleCount = 10;
    [SerializeField] private int maxObstacleCount = 26;


    public int CoinCount { get; private set; }

    public int ObstacleCount { get; private set; }

    private void Awake()
    {
        UpdateDifficulty(0);
    }


    private void OnEnable()
    {
        GameEvents.ScoreChanged += OnScoreChanged;
    }


    private void OnDisable()
    {
        GameEvents.ScoreChanged -= OnScoreChanged;
    }


    private void OnScoreChanged(int score)
    {
        UpdateDifficulty(score);
    }


    private void UpdateDifficulty(int score)
    {
        float progress =
            Mathf.Clamp01(
                (float)score / maxScore
            );


        // Coins
        CoinCount =
            Mathf.RoundToInt(
                Mathf.Lerp(
                    startCoinCount,
                    maxCoinCount,
                    progress
                )
            );


        // Obstacles
        ObstacleCount =
            Mathf.RoundToInt(
                Mathf.Lerp(
                    startObstacleCount,
                    maxObstacleCount,
                    progress
                )
            );


    }


    public int GetMaxScore()
    {
        return maxScore;
    }
}
