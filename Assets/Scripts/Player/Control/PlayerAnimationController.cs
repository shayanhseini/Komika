using UnityEngine;

public class PlayerAnimationController : MonoBehaviour
{
    [Header("Animation References")]
    [SerializeField] private Animator animator;
    [SerializeField] private Animation legacyAnimation;
    [SerializeField] private GameManager gameManager;

    [Header("State")]
    [SerializeField] private bool autoSyncWorldSpeed = false;
    [SerializeField] private WorldMovementManager worldMovement;

    private static readonly int IsRunningHash = Animator.StringToHash("IsRunning");
    private static readonly int IdleStateHash = Animator.StringToHash("Idle");
    private static readonly int RunStateHash = Animator.StringToHash("Run");

    private void Awake()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (legacyAnimation == null)
            legacyAnimation = GetComponentInChildren<Animation>();

        if (gameManager == null)
            gameManager = FindFirstObjectByType<GameManager>();

        if (worldMovement == null)
            worldMovement = FindFirstObjectByType<WorldMovementManager>();

        // Ensure character starts in Idle state
        SetRunning(false);
    }

    private void OnEnable()
    {
        GameEvents.GameStarted += HandleGameStarted;
        GameEvents.GameOver += HandleGameOver;
    }

    private void OnDisable()
    {
        GameEvents.GameStarted -= HandleGameStarted;
        GameEvents.GameOver -= HandleGameOver;
    }

    private void Start()
    {
        bool isPlaying = gameManager != null && gameManager.State == GameState.Playing;
        SetRunning(isPlaying);
    }

    private void Update()
    {
        if (autoSyncWorldSpeed && animator != null && worldMovement != null)
        {
            if (animator.GetBool(IsRunningHash))
            {
                float speedRatio = worldMovement.WorldSpeed / 10f;
                animator.speed = Mathf.Clamp(speedRatio, 0.8f, 2.0f);
            }
            else
            {
                animator.speed = 1.0f;
            }
        }
    }

    private void HandleGameStarted()
    {
        SetRunning(true);
    }

    private void HandleGameOver()
    {
        SetRunning(false);
    }

    public void SetRunning(bool isRunning)
    {
        if (animator != null)
        {
            animator.speed = 1f;
            animator.SetBool(IsRunningHash, isRunning);
            animator.CrossFade(isRunning ? RunStateHash : IdleStateHash, 0.15f);
        }

        if (legacyAnimation != null)
        {
            if (isRunning)
            {
                if (!legacyAnimation.isPlaying)
                    legacyAnimation.Play();
            }
            else
            {
                legacyAnimation.Stop();
            }
        }
    }
}
