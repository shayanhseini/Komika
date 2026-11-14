using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float movementSmoothness = 18f;

    [Header("Three Lane Controls")]
    [SerializeField] private float laneOffset = 3f;
    [SerializeField] private float touchSwipeThreshold = 60f;
    [SerializeField] private float mouseDragThreshold = 60f;
    [SerializeField] private float verticalSwipeThreshold = 60f;

    [Header("Jump & Slide")]
    [SerializeField] private float jumpHeight = 2.5f;
    [SerializeField] private float jumpDuration = 0.85f;
    [Tooltip("Total duration of slide in seconds (matched with 49-frame slide animation)")]
    [SerializeField] private float slideDuration = 1.25f;
    [SerializeField, Range(0.2f, 0.8f)] private float slideColliderHeightMultiplier = 0.45f;
    [SerializeField] private Transform slideVisual;
    [Tooltip("If true, bone animation handles the slide pose so visual mesh is not unnaturally squashed")]
    [SerializeField] private bool useRiggedAnimationForSlide = true;
    [SerializeField, Range(0.2f, 0.8f)] private float slideVisualHeightMultiplier = 0.45f;
    [SerializeField] private float slideVisualTransitionSpeed = 14f;


    [Header("Visual Scale")]
    [Tooltip("Scale multiplier for the character model. 1.5 - 1.7 is recommended for realistic runner proportion.")]
    [SerializeField] private float modelScale = 1.6f;
    [SerializeField] private bool autoApplyModelScale = true;

    [Header("Movement Limits")]
    [SerializeField] private float xLimit = 5f;


    [Header("Player Rotation")]
    [SerializeField] private float maxRoll = 25f;
    [SerializeField] private float rotationSpeed = 5f;


    [Header("Wing Rotation")]
    [SerializeField] private Transform leftWing;
    [SerializeField] private Transform rightWing;

    [SerializeField] private float wingRollAmount = 15f;
    [SerializeField] private float wingRotationSpeed = 5f;


    public Vector2 Input { get; private set; }
    public bool IsJumping { get; private set; }
    public bool IsSliding { get; private set; }

    public float JumpDuration => jumpDuration;
    public float JumpElapsed => jumpElapsed;
    public float JumpProgress => (IsJumping && jumpDuration > 0f) ? Mathf.Clamp01(jumpElapsed / jumpDuration) : 0f;

    public event System.Action<float> OnJumpStarted;
    public event System.Action OnJumpEnded;
    public event System.Action<float> OnSlideStarted;
    public event System.Action OnSlideEnded;
    public event System.Action<int> OnLaneChangeStarted;


    private Quaternion initialRotation;

    private Quaternion leftWingInitialRotation;
    private Quaternion rightWingInitialRotation;

    private bool canControl;

    private Vector3 targetPosition;

    private Vector2 lastTouchPosition;
    private bool isTouching;
    private int currentLane = 1;
    private float lockedY;
    private Vector2 mouseDragStart;
    private bool isMouseDragging;
    private bool laneChangedThisGesture;
    private float jumpElapsed;
    private float slideTimeRemaining;
    private BoxCollider playerCollider;
    private Vector3 standingColliderSize;
    private Vector3 standingColliderCenter;
    private Vector3 standingVisualScale;
    private Vector3 standingVisualPosition;


    private void Awake()
    {
        // Player initial rotation
        initialRotation = transform.rotation;


        // Wings initial local rotations
        if (leftWing != null)
        {
            leftWingInitialRotation =
                leftWing.localRotation;
        }


        if (rightWing != null)
        {
            rightWingInitialRotation =
                rightWing.localRotation;
        }


        targetPosition =
            transform.position;

        lockedY = transform.position.y;
        currentLane = GetClosestLane(transform.position.x);

        canControl = false;

        if (slideVisual == null)
        {
            slideVisual = transform.Find("Character_Visual");
            if (slideVisual == null)
                slideVisual = transform.Find("Character_Mesh");
            if (slideVisual == null)
                slideVisual = transform.Find("Cube");
        }

        if (slideVisual != null)
        {
            if (autoApplyModelScale && modelScale > 0.01f)
            {
                slideVisual.localScale = Vector3.one * modelScale;
            }
            standingVisualScale = slideVisual.localScale;
            standingVisualPosition = slideVisual.localPosition;
        }

        playerCollider = GetComponent<BoxCollider>();
        if (playerCollider != null)
        {
            if (autoApplyModelScale && modelScale > 0.01f)
            {
                playerCollider.size = new Vector3(0.5f * modelScale, 1.05f * modelScale, 0.5f * modelScale);
                playerCollider.center = new Vector3(0f, -0.47f + (1.05f * modelScale) * 0.5f, 0f);
            }
            standingColliderSize = playerCollider.size;
            standingColliderCenter = playerCollider.center;
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (autoApplyModelScale && modelScale > 0.01f)
        {
            if (slideVisual == null)
                slideVisual = transform.Find("Character_Visual");

            if (slideVisual != null)
            {
                slideVisual.localScale = Vector3.one * modelScale;
                standingVisualScale = slideVisual.localScale;
            }

            var col = GetComponent<BoxCollider>();
            if (col != null)
            {
                col.size = new Vector3(0.5f * modelScale, 1.05f * modelScale, 0.5f * modelScale);
                col.center = new Vector3(0f, -0.47f + (1.05f * modelScale) * 0.5f, 0f);
            }
        }
    }
#endif


    private void OnEnable()
    {
        GameEvents.GameStarted += EnableControl;
        GameEvents.GameOver += DisableControl;
    }


    private void OnDisable()
    {
        GameEvents.GameStarted -= EnableControl;
        GameEvents.GameOver -= DisableControl;
    }


    private void Update()
    {
        if (!canControl)
            return;


        ReadInput();

        UpdateJumpAndSlide();
        UpdateSlideVisual();

        Move();

        ClampPosition();

        RotatePlayer();

        RotateWings();
    }


    private void EnableControl()
    {
        canControl = true;

        targetPosition =
            transform.position;

        lockedY = transform.position.y;
        currentLane = GetClosestLane(transform.position.x);

        Input = Vector2.zero;

        isTouching = false;
        if (IsJumping)
        {
            IsJumping = false;
            OnJumpEnded?.Invoke();
        }
        RestoreStandingCollider();
        ResetPointerGesture();
    }


    private void DisableControl()
    {
        canControl = false;

        Input = Vector2.zero;

        isTouching = false;
        if (IsJumping)
        {
            IsJumping = false;
            OnJumpEnded?.Invoke();
        }
        targetPosition.y = lockedY;
        RestoreStandingCollider();
        ResetPointerGesture();
    }


    private void ReadInput()
    {
        Input = Vector2.zero;

        // Keyboard controls (useful for Editor testing and PC controls)
        if (Keyboard.current != null)
        {
            if (Keyboard.current.downArrowKey.wasPressedThisFrame || Keyboard.current.sKey.wasPressedThisFrame)
            {
                StartSlide();
            }
            else if (Keyboard.current.upArrowKey.wasPressedThisFrame || Keyboard.current.wKey.wasPressedThisFrame || Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                StartJump();
            }
            else if (Keyboard.current.leftArrowKey.wasPressedThisFrame || Keyboard.current.aKey.wasPressedThisFrame)
            {
                SetLane(currentLane - 1);
            }
            else if (Keyboard.current.rightArrowKey.wasPressedThisFrame || Keyboard.current.dKey.wasPressedThisFrame)
            {
                SetLane(currentLane + 1);
            }
        }

        if (Touchscreen.current != null &&
            Touchscreen.current.primaryTouch.press.isPressed)
        {
            ReadTouchInput();
            return;
        }

        isTouching = false;

        if (Mouse.current == null)
            return;

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            isMouseDragging = true;
            laneChangedThisGesture = false;
            mouseDragStart = Mouse.current.position.ReadValue();
            return;
        }

        if (Mouse.current.leftButton.isPressed &&
            isMouseDragging &&
            !laneChangedThisGesture)
        {
            Vector2 drag = Mouse.current.position.ReadValue() - mouseDragStart;
            HandleGesture(drag, mouseDragThreshold);
        }

        if (Mouse.current.leftButton.wasReleasedThisFrame)
            ResetPointerGesture();
    }


    private void ReadTouchInput()
    {
        var touch = Touchscreen.current.primaryTouch;

        if (touch.press.wasPressedThisFrame)
        {
            isTouching = true;
            laneChangedThisGesture = false;

            lastTouchPosition =
                touch.position.ReadValue();

            Input = Vector2.zero;

            return;
        }

        if (touch.press.isPressed &&
            isTouching &&
            !laneChangedThisGesture)
        {
            Vector2 currentTouchPosition =
                touch.position.ReadValue();

            Vector2 delta =
                currentTouchPosition -
                lastTouchPosition;

            HandleGesture(delta, touchSwipeThreshold);
        }

        if (touch.press.wasReleasedThisFrame)
        {
            isTouching = false;
            laneChangedThisGesture = false;

            Input = Vector2.zero;
        }
    }


    private void Move()
    {
        targetPosition.x = Mathf.Clamp(targetPosition.x, -xLimit, xLimit);


        transform.position =
            Vector3.Lerp(
                transform.position,
                targetPosition,
                movementSmoothness *
                Time.deltaTime
            );
    }


    private void ClampPosition()
    {
        Vector3 position =
            transform.position;


        position.x =
            Mathf.Clamp(
                position.x,
                -xLimit,
                xLimit
            );


        position.y = targetPosition.y;


        transform.position =
            position;
    }


    private void RotatePlayer()
    {
        float targetRoll =
            -Input.x * maxRoll;


        float targetPitch = 0f;


        Quaternion targetRotation =
            initialRotation *
            Quaternion.Euler(
                targetPitch,
                0f,
                targetRoll
            );


        transform.rotation =
            Quaternion.Lerp(
                transform.rotation,
                targetRotation,
                rotationSpeed *
                Time.deltaTime
            );
    }


    private void RotateWings()
    {
        if (leftWing == null && rightWing == null)
            return;


        float horizontalRotation =
            -Input.x * wingRollAmount;


        float verticalRotation = 0f;


        float leftRotation =
            verticalRotation +
            horizontalRotation;


        float rightRotation =
            verticalRotation -
            horizontalRotation;


        if (leftWing != null)
        {
            Quaternion targetRotation =
                leftWingInitialRotation *
                Quaternion.Euler(
                    leftRotation,
                    0f,
                    0f
                );


            leftWing.localRotation =
                Quaternion.Lerp(
                    leftWing.localRotation,
                    targetRotation,
                    wingRotationSpeed *
                    Time.deltaTime
                );
        }


        if (rightWing != null)
        {
            Quaternion targetRotation =
                rightWingInitialRotation *
                Quaternion.Euler(
                    rightRotation,
                    0f,
                    0f
                );


            rightWing.localRotation =
                Quaternion.Lerp(
                    rightWing.localRotation,
                    targetRotation,
                    wingRotationSpeed *
                    Time.deltaTime
                );
        }
    }


    private void SetLane(int requestedLane)
    {
        int nextLane = Mathf.Clamp(requestedLane, 0, 2);
        if (nextLane == currentLane)
            return;

        int direction = nextLane > currentLane ? 1 : -1;
        currentLane = nextLane;
        Input = new Vector2(direction, 0f);
        targetPosition.x = (currentLane - 1) * laneOffset;
        OnLaneChangeStarted?.Invoke(direction);
    }


    private void HandleGesture(Vector2 drag, float horizontalThreshold)
    {
        if (laneChangedThisGesture)
            return;

        if (Mathf.Abs(drag.x) > Mathf.Abs(drag.y))
        {
            if (Mathf.Abs(drag.x) < horizontalThreshold)
                return;

            SetLane(currentLane + (drag.x > 0f ? 1 : -1));
        }
        else
        {
            if (Mathf.Abs(drag.y) < verticalSwipeThreshold)
                return;

            if (drag.y > 0f)
                StartJump();
            else
                StartSlide();
        }

        laneChangedThisGesture = true;
    }


    private void StartJump()
    {
        if (IsJumping)
            return;

        if (IsSliding)
        {
            RestoreStandingCollider();
            RestoreStandingVisualImmediately();
        }

        IsJumping = true;
        jumpElapsed = 0f;
        Input = Vector2.up;
        OnJumpStarted?.Invoke(jumpDuration);
    }


    private void StartSlide()
    {
        if (IsJumping)
        {
            // Cancel jump and drop down immediately into slide
            IsJumping = false;
            targetPosition.y = lockedY;
            OnJumpEnded?.Invoke();
        }

        IsSliding = true;
        slideTimeRemaining = slideDuration;
        Input = Vector2.down;

        if (playerCollider != null)
        {
            Vector3 size = standingColliderSize;
            size.y *= slideColliderHeightMultiplier;
            playerCollider.size = size;

            Vector3 center = standingColliderCenter;
            center.y -= (standingColliderSize.y - size.y) * 0.5f;
            playerCollider.center = center;
        }

        OnSlideStarted?.Invoke(slideDuration);
    }


    private void UpdateJumpAndSlide()
    {
        if (IsJumping)
        {
            jumpElapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(jumpElapsed / jumpDuration);
            targetPosition.y = lockedY + 4f * jumpHeight * progress * (1f - progress);

            if (progress >= 1f)
            {
                IsJumping = false;
                targetPosition.y = lockedY;
                OnJumpEnded?.Invoke();
            }
        }
        else
        {
            targetPosition.y = lockedY;
        }

        if (!IsSliding)
            return;

        slideTimeRemaining -= Time.deltaTime;
        if (slideTimeRemaining <= 0f)
            RestoreStandingCollider();
    }


    private void RestoreStandingCollider()
    {
        bool wasSliding = IsSliding;
        IsSliding = false;
        slideTimeRemaining = 0f;

        if (playerCollider != null)
        {
            playerCollider.size = standingColliderSize;
            playerCollider.center = standingColliderCenter;
        }

        if (wasSliding)
        {
            OnSlideEnded?.Invoke();
        }
    }

    private void UpdateSlideVisual()
    {
        if (slideVisual == null)
            return;

        Vector3 targetScale = standingVisualScale;
        Vector3 targetPosition = standingVisualPosition;

        if (IsSliding && !useRiggedAnimationForSlide)
        {
            targetScale.y *= slideVisualHeightMultiplier;
            targetPosition.y -= (standingVisualScale.y - targetScale.y) * 0.5f;
        }

        float blend = 1f - Mathf.Exp(-slideVisualTransitionSpeed * Time.deltaTime);
        slideVisual.localScale = Vector3.Lerp(slideVisual.localScale, targetScale, blend);
        slideVisual.localPosition = Vector3.Lerp(slideVisual.localPosition, targetPosition, blend);
    }

    private void RestoreStandingVisualImmediately()
    {
        if (slideVisual == null)
            return;

        slideVisual.localScale = standingVisualScale;
        slideVisual.localPosition = standingVisualPosition;
    }


    private int GetClosestLane(float xPosition)
    {
        if (laneOffset <= Mathf.Epsilon)
            return 1;

        return Mathf.Clamp(
            Mathf.RoundToInt(xPosition / laneOffset) + 1,
            0,
            2
        );
    }


    private void ResetPointerGesture()
    {
        isMouseDragging = false;
        laneChangedThisGesture = false;
    }
}
