using UnityEngine;

public class WorldMovementManager : MonoBehaviour
{
    [Header("Speed")]
    [SerializeField] private float startSpeed = 10f;
    [SerializeField] private float speedAtMaxScore = 18f;
    [SerializeField] private float speedIncreaseAfterMaxScore = 0.015f;
    
    [Header("Real Speed")]
    [SerializeField] private float realSpeedMultiplier = 20f;
    
    [Header("Difficulty")]
    [SerializeField] private DifficultyManager difficultyManager;

    
    public float RealSpeed =>
        WorldSpeed * realSpeedMultiplier;

    public float BaseWorldSpeed { get; private set; }

    public float WorldSpeed { get; private set; }

    public float SpeedMultiplier { get; private set; } = 1f;

    public bool IsMovementEnabled { get; private set; }


    private void Awake()
    {
        BaseWorldSpeed =
            startSpeed;


        WorldSpeed =
            startSpeed;


        SpeedMultiplier =
            1f;


        IsMovementEnabled =
            false;
    }


    private void OnEnable()
    {
        GameEvents.GameStarted += StartMovement;
        GameEvents.GameOver += StopMovement;
        GameEvents.ScoreChanged += UpdateSpeed;
    }


    private void OnDisable()
    {
        GameEvents.GameStarted -= StartMovement;
        GameEvents.GameOver -= StopMovement;
        GameEvents.ScoreChanged -= UpdateSpeed;
    }


    private void StartMovement()
    {
        IsMovementEnabled = true;
    }


    private void StopMovement()
    {
        IsMovementEnabled = false;
    }

    private void Update()
    {
        if (!IsMovementEnabled || difficultyManager == null)
            return;

        float newBaseSpeed = Mathf.Lerp(startSpeed, speedAtMaxScore, difficultyManager.Progress);
        float overtime = Mathf.Max(0f, difficultyManager.ElapsedGameplayTime - difficultyManager.GetSecondsToMaximumDifficulty());
        newBaseSpeed += overtime * speedIncreaseAfterMaxScore;
        BaseWorldSpeed = newBaseSpeed;
        UpdateWorldSpeed();
    }


    private void UpdateSpeed(int score)
    {
        // Difficulty is updated continuously in Update using both score and elapsed time.
    }


    public void SetSpeedMultiplier(
        float multiplier)
    {
        SpeedMultiplier =
            Mathf.Max(
                0f,
                multiplier
            );


        UpdateWorldSpeed();
    }


    private void UpdateWorldSpeed()
    {
        float targetSpeed =
            BaseWorldSpeed *
            SpeedMultiplier;
        
        if (targetSpeed >= WorldSpeed)
        {
            WorldSpeed = targetSpeed;
        }
        else
        {
            if (SpeedMultiplier <= 1f)
            {
                WorldSpeed = targetSpeed;
            }
        }
    }
}
