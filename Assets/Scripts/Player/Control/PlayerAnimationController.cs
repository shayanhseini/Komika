using UnityEngine;

public class PlayerAnimationController : MonoBehaviour
{
    [Header("Animation References")]
    [SerializeField] private Animator animator;
    [SerializeField] private Animation legacyAnimation;
    [SerializeField] private GameManager gameManager;
    [SerializeField] private PlayerController playerController;

    [Header("State")]
    [SerializeField] private bool autoSyncWorldSpeed = false;
    [SerializeField] private WorldMovementManager worldMovement;

    [Header("Jump Synchronization")]
    [Tooltip("Total duration of the 42-frame Jump clip in seconds (42 frames @ 30fps = 1.4s)")]
    [SerializeField] private float jumpClipLength = 1.4f;
    [SerializeField] private float jumpTakeoffCrossFade = 0.05f;
    [SerializeField] private float jumpLandCrossFade = 0.12f;

    [Header("Slide Synchronization")]
    [Tooltip("Total duration of the 49-frame Slide clip in seconds (49 frames @ 30fps = 1.633s)")]
    [SerializeField] private float slideClipLength = 1.633f;
    [SerializeField] private float slideCrossFade = 0.08f;
    [SerializeField] private float slideReturnCrossFade = 0.15f;

    [Header("Lane Change Synchronization")]
    [SerializeField] private float laneChangeCrossFade = 0.06f;
    [SerializeField] private float laneReturnCrossFade = 0.12f;

    private static readonly int IsRunningHash = Animator.StringToHash("IsRunning");
    private static readonly int RunBlendHash = Animator.StringToHash("RunBlend");
    private static readonly int IsJumpingHash = Animator.StringToHash("IsJumping");
    private static readonly int IsSlidingHash = Animator.StringToHash("IsSliding");
    private static readonly int JumpProgressHash = Animator.StringToHash("JumpProgress");
    private static readonly int JumpSpeedHash = Animator.StringToHash("JumpSpeed");
    private static readonly int SlideSpeedHash = Animator.StringToHash("SlideSpeed");

    private static readonly int IdleStateHash = Animator.StringToHash("Idle");
    private static readonly int RunStateHash = Animator.StringToHash("Run");
    private static readonly int JumpStateHash = Animator.StringToHash("Jump");
    private static readonly int SlideStateHash = Animator.StringToHash("Slide");
    private static readonly int MoveLeftStateHash = Animator.StringToHash("Move_Left");
    private static readonly int MoveRightStateHash = Animator.StringToHash("Move_Right");

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

        if (playerController == null)
            playerController = GetComponent<PlayerController>();

        // Ensure character starts in Idle state and not jumping or sliding
        if (animator != null)
        {
            animator.SetBool(IsSlidingHash, false);
            animator.SetBool(IsJumpingHash, false);
        }
        SetRunning(false);
    }

    private void OnEnable()
    {
        GameEvents.GameStarted += HandleGameStarted;
        GameEvents.GameOver += HandleGameOver;

        if (playerController == null)
            playerController = GetComponent<PlayerController>();

        if (playerController != null)
        {
            playerController.OnJumpStarted += HandleJumpStarted;
            playerController.OnJumpEnded += HandleJumpEnded;
            playerController.OnSlideStarted += HandleSlideStarted;
            playerController.OnSlideEnded += HandleSlideEnded;
            playerController.OnLaneChangeStarted += HandleLaneChangeStarted;
        }
    }

    private void OnDisable()
    {
        GameEvents.GameStarted -= HandleGameStarted;
        GameEvents.GameOver -= HandleGameOver;

        if (playerController != null)
        {
            playerController.OnJumpStarted -= HandleJumpStarted;
            playerController.OnJumpEnded -= HandleJumpEnded;
            playerController.OnSlideStarted -= HandleSlideStarted;
            playerController.OnSlideEnded -= HandleSlideEnded;
            playerController.OnLaneChangeStarted -= HandleLaneChangeStarted;
        }
    }

    private void Start()
    {
        bool isPlaying = gameManager != null && gameManager.State == GameState.Playing
            && worldMovement != null && worldMovement.WorldSpeed > 0.12f;
        SetRunning(isPlaying);
    }

    private void Update()
    {
        // Precisely synchronize the Jump animation frames with the jump trajectory:
        // Progress 0.00 -> 0.50 : Rising upward (Frames 1 to 21)
        // Progress 0.50 -> 1.00 : Falling downward to ground (Frames 22 to 42)
        if (animator != null && playerController != null && playerController.IsJumping)
        {
            float progress = playerController.JumpProgress;
            animator.SetFloat(JumpProgressHash, progress);
        }

        if (animator == null)
            return;

        bool isGameplay = gameManager != null && gameManager.State == GameState.Playing;
        bool shouldRun = isGameplay && worldMovement != null && worldMovement.WorldSpeed > 0.12f;
        if (animator.GetBool(IsRunningHash) != shouldRun)
        {
            SetRunning(shouldRun);
        }

        if (shouldRun && !animator.GetBool(IsJumpingHash) && !animator.GetBool(IsSlidingHash))
        {
            float intensity = playerController != null
                ? playerController.VisionRunIntensity
                : (worldMovement != null ? Mathf.Clamp01((worldMovement.JogFactor - 0.35f) / 1.05f) : 0.5f);
            float runBlend = Mathf.Clamp01((intensity - 0.62f) / 0.34f);
            animator.SetFloat(RunBlendHash, runBlend, 0.12f, Time.deltaTime);

            float targetAnimSpeed = Mathf.Lerp(0.92f, 1.32f, runBlend);
            if (autoSyncWorldSpeed && worldMovement != null)
            {
                float ratio = Mathf.Clamp(worldMovement.WorldSpeed / 10f, 0.85f, 1.55f);
                targetAnimSpeed = Mathf.Max(targetAnimSpeed, ratio);
            }
            animator.speed = targetAnimSpeed;
        }
        else if (!animator.GetBool(IsJumpingHash) && !animator.GetBool(IsSlidingHash))
        {
            animator.SetFloat(RunBlendHash, 0f);
            animator.speed = 1.0f;
        }
    }

    private void HandleGameStarted()
    {
        bool shouldRun = worldMovement != null && worldMovement.WorldSpeed > 0.12f;
        SetRunning(shouldRun);
    }

    private void HandleGameOver()
    {
        if (animator != null)
        {
            animator.SetBool(IsJumpingHash, false);
            animator.SetBool(IsSlidingHash, false);
        }
        SetRunning(false);
    }

    public void HandleJumpStarted(float duration)
    {
        if (animator == null) return;

        // If currently sliding, cancel slide state in animator
        animator.SetBool(IsSlidingHash, false);

        // Scale playback speed dynamically so the 42 frames span exactly the jump duration:
        // Ascent (first 21 frames) plays while going UP to the apex (first 50% of time)
        // Descent (second 21 frames) plays while going DOWN to the ground (second 50% of time)
        float effectiveDuration = Mathf.Max(0.1f, duration);
        float jumpSpeed = jumpClipLength / effectiveDuration;

        animator.SetFloat(JumpSpeedHash, jumpSpeed);
        animator.SetFloat(JumpProgressHash, 0f);
        animator.SetBool(IsJumpingHash, true);

        animator.CrossFadeInFixedTime(JumpStateHash, jumpTakeoffCrossFade, 0, 0f);
    }

    public void HandleJumpEnded()
    {
        if (animator == null) return;

        animator.SetBool(IsJumpingHash, false);
        animator.SetFloat(JumpProgressHash, 1f);

        // If player triggered a slide while landing
        if (animator.GetBool(IsSlidingHash))
            return;

        bool isRunning = animator.GetBool(IsRunningHash);
        if (isRunning)
        {
            animator.CrossFadeInFixedTime(RunStateHash, jumpLandCrossFade);
        }
        else
        {
            animator.CrossFadeInFixedTime(IdleStateHash, 0.15f);
        }
    }

    public void HandleSlideStarted(float duration)
    {
        if (animator == null) return;

        // Scale playback speed dynamically so the 49 frames span the slide duration
        float effectiveDuration = Mathf.Max(0.1f, duration);
        float slideSpeed = slideClipLength / effectiveDuration;

        animator.SetFloat(SlideSpeedHash, slideSpeed);
        animator.SetBool(IsSlidingHash, true);

        animator.CrossFadeInFixedTime(SlideStateHash, slideCrossFade, 0, 0f);
    }

    public void HandleSlideEnded()
    {
        if (animator == null) return;

        animator.SetBool(IsSlidingHash, false);

        // If player jumped out of the slide, do not force Run/Idle
        if (animator.GetBool(IsJumpingHash))
            return;

        bool isRunning = animator.GetBool(IsRunningHash);
        if (isRunning)
        {
            animator.CrossFadeInFixedTime(RunStateHash, slideReturnCrossFade);
        }
        else
        {
            animator.CrossFadeInFixedTime(IdleStateHash, 0.20f);
        }
    }

    public void HandleLaneChangeStarted(int direction)
    {
        if (animator == null) return;

        // Do not interrupt jump or slide animation if currently airborne or sliding
        if (animator.GetBool(IsJumpingHash) || animator.GetBool(IsSlidingHash)) return;

        if (direction < 0)
        {
            // Moving Left -> play Move_Left
            animator.CrossFadeInFixedTime(MoveLeftStateHash, laneChangeCrossFade, 0, 0f);
        }
        else if (direction > 0)
        {
            // Moving Right -> play Move_Right
            animator.CrossFadeInFixedTime(MoveRightStateHash, laneChangeCrossFade, 0, 0f);
        }
    }

    public void SetRunning(bool isRunning)
    {
        if (animator != null)
        {
            animator.speed = 1f;
            animator.SetBool(IsRunningHash, isRunning);

            if (!animator.GetBool(IsJumpingHash) && !animator.GetBool(IsSlidingHash))
            {
                if (isRunning)
                {
                    animator.CrossFade(RunStateHash, 0.15f);
                }
                else
                {
                    animator.CrossFade(IdleStateHash, 0.20f);
                }
            }
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
