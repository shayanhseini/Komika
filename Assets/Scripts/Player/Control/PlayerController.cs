using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float movementSmoothness = 10f;

    [Header("Three Lane Controls")]
    [SerializeField] private float laneOffset = 3f;
    [SerializeField] private float touchSwipeThreshold = 60f;
    [SerializeField] private float mouseDragThreshold = 60f;
    [SerializeField] private float verticalSwipeThreshold = 60f;

    [Header("Jump & Slide")]
    [SerializeField] private float jumpHeight = 2.5f;
    [SerializeField] private float jumpDuration = 0.85f;
    [SerializeField] private float slideDuration = 0.75f;
    [SerializeField, Range(0.2f, 0.8f)] private float slideColliderHeightMultiplier = 0.45f;


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

        playerCollider = GetComponent<BoxCollider>();
        if (playerCollider != null)
        {
            standingColliderSize = playerCollider.size;
            standingColliderCenter = playerCollider.center;
        }
    }


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
        IsJumping = false;
        RestoreStandingCollider();
        ResetPointerGesture();
    }


    private void DisableControl()
    {
        canControl = false;

        Input = Vector2.zero;

        isTouching = false;
        IsJumping = false;
        targetPosition.y = lockedY;
        RestoreStandingCollider();
        ResetPointerGesture();
    }


    private void ReadInput()
    {
        Input = Vector2.zero;

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
        if (IsJumping || IsSliding)
            return;

        IsJumping = true;
        jumpElapsed = 0f;
        Input = Vector2.up;
    }


    private void StartSlide()
    {
        if (IsJumping)
            return;

        IsSliding = true;
        slideTimeRemaining = slideDuration;
        Input = Vector2.down;

        if (playerCollider == null)
            return;

        Vector3 size = standingColliderSize;
        size.y *= slideColliderHeightMultiplier;
        playerCollider.size = size;

        Vector3 center = standingColliderCenter;
        center.y -= (standingColliderSize.y - size.y) * 0.5f;
        playerCollider.center = center;
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
        IsSliding = false;
        slideTimeRemaining = 0f;

        if (playerCollider == null)
            return;

        playerCollider.size = standingColliderSize;
        playerCollider.center = standingColliderCenter;
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
