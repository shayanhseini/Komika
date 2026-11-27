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

    
    [Header("Dynamic Speed Smoothing")]
    [SerializeField] private float speedRampUpRate = 28f;
    [SerializeField] private float speedSlowDownRate = 32f;

    public float RealSpeed =>
        WorldSpeed * realSpeedMultiplier;

    public float BaseWorldSpeed { get; private set; }

    public float WorldSpeed { get; private set; }

    public float SpeedMultiplier { get; private set; } = 1f;

    /// <summary>0 = standing still (Idle), ~0.55..1.05 = jog/run, ~1.05..1.48 = fast sprint.</summary>
    public float JogFactor { get; private set; }

    public bool IsMovementEnabled { get; private set; }

    public bool IsActuallyMoving => IsMovementEnabled && WorldSpeed > 0.1f;


    private void Awake()
    {
        BaseWorldSpeed =
            startSpeed;

        JogFactor = 0f;
        WorldSpeed = 0f;

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
        JogFactor = 0f;
        WorldSpeed = 0f;
    }


    private void StopMovement()
    {
        IsMovementEnabled = false;
        JogFactor = 0f;
        WorldSpeed = 0f;
    }

    private void Update()
    {
        if (!IsMovementEnabled)
        {
            WorldSpeed = Mathf.MoveTowards(WorldSpeed, 0f, speedSlowDownRate * Time.deltaTime);
            return;
        }

        if (difficultyManager != null)
        {
            float newBaseSpeed = Mathf.Lerp(startSpeed, speedAtMaxScore, difficultyManager.Progress);
            float overtime = Mathf.Max(0f, difficultyManager.ElapsedGameplayTime - difficultyManager.GetSecondsToMaximumDifficulty());
            newBaseSpeed += overtime * speedIncreaseAfterMaxScore;
            BaseWorldSpeed = newBaseSpeed;
        }

        UpdateWorldSpeed();
    }


    private void UpdateSpeed(int score)
    {
        // Difficulty is updated continuously in Update using both score and elapsed time.
    }


    public void SetJogFactor(float factor)
    {
        JogFactor = Mathf.Clamp(factor, 0f, 1.6f);
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
        float targetSpeed = IsMovementEnabled ? (BaseWorldSpeed * SpeedMultiplier * JogFactor) : 0f;
        float rate = targetSpeed >= WorldSpeed ? speedRampUpRate : speedSlowDownRate;
        WorldSpeed = Mathf.MoveTowards(WorldSpeed, targetSpeed, rate * Time.deltaTime);
        if (targetSpeed <= 0.01f && WorldSpeed < 0.05f)
            WorldSpeed = 0f;
    }
}
