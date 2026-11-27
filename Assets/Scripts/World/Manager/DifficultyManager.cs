using UnityEngine;

public class DifficultyManager : MonoBehaviour
{
    [Header("Difficulty Progress")]
    [SerializeField] private int maxScore = 500;
    [SerializeField] private float secondsToMaximumDifficulty = 120f;

    [Header("Coins")]
    [SerializeField] private int startCoinCount = 10;
    [SerializeField] private int maxCoinCount = 20;

    [Header("Obstacles")]
    [SerializeField] private int startObstacleCount = 10;
    [SerializeField] private int maxObstacleCount = 26;

    public int CoinCount { get; private set; }
    public int ObstacleCount { get; private set; }
    public float Progress { get; private set; }
    public float ElapsedGameplayTime { get; private set; }
    public int Tier => Progress < 0.3f ? 0 : Progress < 0.68f ? 1 : 2;

    [Header("Movement Gate")]
    [SerializeField] private WorldMovementManager worldMovement;

    private int currentScore;
    private bool isRunning;

    private void Awake()
    {
        if (worldMovement == null)
            worldMovement = FindFirstObjectByType<WorldMovementManager>();
        UpdateDifficulty();
    }

    private void OnEnable()
    {
        GameEvents.GameStarted += StartDifficulty;
        GameEvents.GameOver += StopDifficulty;
        GameEvents.ScoreChanged += OnScoreChanged;
    }

    private void OnDisable()
    {
        GameEvents.GameStarted -= StartDifficulty;
        GameEvents.GameOver -= StopDifficulty;
        GameEvents.ScoreChanged -= OnScoreChanged;
    }

    private void Update()
    {
        if (!isRunning) return;
        if (worldMovement == null || worldMovement.WorldSpeed > 0.2f)
            ElapsedGameplayTime += Time.deltaTime;
        UpdateDifficulty();
    }

    private void StartDifficulty()
    {
        isRunning = true;
        ElapsedGameplayTime = 0f;
        currentScore = 0;
        UpdateDifficulty();
    }

    private void StopDifficulty() => isRunning = false;

    private void OnScoreChanged(int score)
    {
        currentScore = score;
        UpdateDifficulty();
    }

    private void UpdateDifficulty()
    {
        float scoreProgress = maxScore > 0 ? (float)currentScore / maxScore : 1f;
        float timeProgress = secondsToMaximumDifficulty > 0f ? ElapsedGameplayTime / secondsToMaximumDifficulty : 1f;
        Progress = Mathf.Clamp01(Mathf.Max(scoreProgress, timeProgress));
        CoinCount = Mathf.RoundToInt(Mathf.Lerp(startCoinCount, maxCoinCount, Progress));
        ObstacleCount = Mathf.RoundToInt(Mathf.Lerp(startObstacleCount, maxObstacleCount, Progress));
    }

    public int GetMaxScore() => maxScore;
    public float GetSecondsToMaximumDifficulty() => secondsToMaximumDifficulty;
}
