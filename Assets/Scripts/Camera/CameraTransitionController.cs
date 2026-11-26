using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;

public class CameraTransitionController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform idlePoint;
    [SerializeField] private Transform gameplayPoint;
    [FormerlySerializedAs("planeCameraController")]
    [SerializeField] private PlayerCameraController playerCameraController;
    [SerializeField] private GameManager gameManager;

    [Header("Idle Camera Setup")]
    [SerializeField] private Vector3 idlePosition = new Vector3(0.0f, 0.45f, -90.50f);
    [SerializeField] private Vector3 idleEulerAngles = new Vector3(4.0f, 180f, 0f);

    [Header("Transition Settings")]
    [SerializeField] private float moveDuration = 1.6f;
    [SerializeField] private float arcSideAmount = 2.6f;
    [SerializeField, Range(0f, 1f)] private float gameStartProgress = 0.70f;

    private Coroutine transitionCoroutine;

    private void Awake()
    {
        if (gameManager == null)
            gameManager = FindFirstObjectByType<GameManager>();

        if (playerCameraController == null)
            playerCameraController = GetComponent<PlayerCameraController>();

        bool autoStartAfterRestart = PlayerPrefs.GetInt("KomikaAutoStartAfterRestart", 0) == 1;
        if (autoStartAfterRestart)
        {
            ApplyGameplayTransform();
        }
        else
        {
            ApplyIdleTransform();
        }
    }

    public void ApplyIdleTransform()
    {
        Vector3 pos = idlePoint != null ? idlePoint.position : idlePosition;
        Quaternion rot = idlePoint != null ? idlePoint.rotation : Quaternion.Euler(idleEulerAngles);

        transform.SetPositionAndRotation(pos, rot);

        if (playerCameraController != null)
            playerCameraController.enabled = false;
    }

    public void ApplyGameplayTransform()
    {
        Vector3 targetPosition = gameplayPoint != null ? gameplayPoint.position : new Vector3(0f, 2.55f, -98.40f);
        Quaternion targetRotation = gameplayPoint != null ? gameplayPoint.rotation : Quaternion.Euler(10.0f, 0f, 0f);

        transform.SetPositionAndRotation(targetPosition, targetRotation);

        if (playerCameraController != null)
        {
            playerCameraController.SetInitialTransform(targetPosition, targetRotation);
            playerCameraController.enabled = true;
        }
    }

    public void StartGameTransition()
    {
        if (transitionCoroutine != null)
            return;

        if (playerCameraController != null)
            playerCameraController.enabled = false;

        transitionCoroutine = StartCoroutine(MoveToGameplay());
    }

    private IEnumerator MoveToGameplay()
    {
        Vector3 startPosition = transform.position;
        Quaternion startRotation = transform.rotation;

        Vector3 targetPosition = gameplayPoint != null ? gameplayPoint.position : new Vector3(0f, 2.55f, -98.40f);
        Quaternion targetRotation = gameplayPoint != null ? gameplayPoint.rotation : Quaternion.Euler(10.0f, 0f, 0f);

        Transform playerTransform = null;
        var pObj = GameObject.FindGameObjectWithTag("Player") ?? GameObject.Find("Player");
        if (pObj != null) playerTransform = pObj.transform;

        Vector3 playerPos = playerTransform != null ? playerTransform.position : new Vector3(0f, -0.52f, -92.84f);
        Vector3 lookTarget = playerPos + Vector3.up * 0.85f;

        float elapsed = 0f;
        bool gameStarted = false;

        while (elapsed < moveDuration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / moveDuration);
            float smoothProgress = Mathf.SmoothStep(0f, 1f, progress);

            // 1. Calculate arc position (sweeps smoothly out to the side around the character)
            Vector3 basePosition = Vector3.Lerp(startPosition, targetPosition, smoothProgress);
            float arcOffset = Mathf.Sin(Mathf.PI * smoothProgress) * arcSideAmount;
            Vector3 currentPos = basePosition + Vector3.right * arcOffset;

            // 2. Calculate rotation: dynamic look-at tracking character in early phase, blending into gameplay view
            Vector3 toTarget = lookTarget - currentPos;
            Quaternion lookAtCharacter = toTarget.sqrMagnitude > 0.001f 
                ? Quaternion.LookRotation(toTarget.normalized, Vector3.up) 
                : startRotation;

            float blendWeight = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((progress - 0.25f) / 0.75f));
            Quaternion currentRot = Quaternion.Slerp(lookAtCharacter, targetRotation, blendWeight);

            transform.SetPositionAndRotation(currentPos, currentRot);

            if (!gameStarted && progress >= gameStartProgress)
            {
                gameStarted = true;
                if (gameManager != null)
                {
                    gameManager.StartGame();
                }
            }

            yield return null;
        }

        transform.SetPositionAndRotation(targetPosition, targetRotation);

        if (playerCameraController != null)
        {
            playerCameraController.SetInitialTransform(targetPosition, targetRotation);
            playerCameraController.enabled = true;
        }

        transitionCoroutine = null;
    }
}
