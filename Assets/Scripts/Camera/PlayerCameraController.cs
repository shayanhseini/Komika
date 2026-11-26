using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;

public class PlayerCameraController : MonoBehaviour
{
    [Header("References")]
    [FormerlySerializedAs("planeController")]
    [SerializeField] private PlayerController playerController;

    [Header("Rotation")]
    [SerializeField] private float pitchAmount = 3.5f;
    [SerializeField] private float rollAmount = 2.5f;
    [SerializeField] private float rotationSmoothness = 5f;

    [Header("Lane Follow")]
    [SerializeField, Range(0f, 1f)] private float horizontalFollowAmount = 0.52f;
    [SerializeField, Min(0.01f)] private float horizontalFollowSmoothTime = 0.12f;

    [Header("FOV")]
    [SerializeField] private Camera targetCamera;
    [SerializeField] private float normalFOV = 62f;
    [SerializeField] private float speedFOV = 70f;
    [SerializeField] private float fovTransitionDuration = 0.25f;

    private Quaternion initialRotation;
    private Vector3 initialPosition;
    private float initialPlayerLocalX;
    private float followedLocalX;
    private float horizontalFollowVelocity;
    private Coroutine fovCoroutine;

    private void Awake()
    {
        initialRotation = transform.localRotation;
        initialPosition = transform.localPosition;
        initialPlayerLocalX = GetPlayerLocalX();
        followedLocalX = initialPosition.x;

        if (targetCamera == null)
            targetCamera = GetComponent<Camera>();

        if (targetCamera != null)
            targetCamera.fieldOfView = normalFOV;
    }

    private void LateUpdate()
    {
        FollowPlayerHorizontally();
        RotateCamera();
        ApplyCameraPosition();
    }

    private void FollowPlayerHorizontally()
    {
        if (playerController == null)
            return;

        float playerOffset = GetPlayerLocalX() - initialPlayerLocalX;
        float targetX = initialPosition.x + playerOffset * horizontalFollowAmount;

        followedLocalX = Mathf.SmoothDamp(
            followedLocalX,
            targetX,
            ref horizontalFollowVelocity,
            horizontalFollowSmoothTime
        );
    }

    private void RotateCamera()
    {
        if (playerController == null)
            return;

        Vector2 input = playerController.Input;
        float targetPitch = -input.y * pitchAmount;
        float targetRoll = -input.x * rollAmount;
        Quaternion targetRotation = initialRotation * Quaternion.Euler(targetPitch, 0f, targetRoll);

        transform.localRotation = Quaternion.Lerp(
            transform.localRotation,
            targetRotation,
            rotationSmoothness * Time.deltaTime
        );
    }

    private void ApplyCameraPosition()
    {
        Vector3 targetPosition = initialPosition;
        targetPosition.x = followedLocalX;
        transform.localPosition = targetPosition;
    }

    public void SetInitialTransform(Vector3 position, Quaternion rotation)
    {
        initialPosition = transform.parent != null
            ? transform.parent.InverseTransformPoint(position)
            : position;

        initialRotation = transform.parent != null
            ? Quaternion.Inverse(transform.parent.rotation) * rotation
            : rotation;

        initialPlayerLocalX = GetPlayerLocalX();
        followedLocalX = initialPosition.x;
        horizontalFollowVelocity = 0f;
    }

    private float GetPlayerLocalX()
    {
        if (playerController == null)
            return 0f;

        if (transform.parent == null)
            return playerController.transform.position.x;

        return transform.parent.InverseTransformPoint(playerController.transform.position).x;
    }

    public void SetSpeedFOV(bool active)
    {
        if (targetCamera == null)
            return;

        float targetFOV = active ? speedFOV : normalFOV;
        if (fovCoroutine != null)
            StopCoroutine(fovCoroutine);

        fovCoroutine = StartCoroutine(ChangeFOV(targetFOV));
    }

    private IEnumerator ChangeFOV(float targetFOV)
    {
        float startFOV = targetCamera.fieldOfView;
        float elapsed = 0f;

        while (elapsed < fovTransitionDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / fovTransitionDuration));
            targetCamera.fieldOfView = Mathf.Lerp(startFOV, targetFOV, t);
            yield return null;
        }

        targetCamera.fieldOfView = targetFOV;
        fovCoroutine = null;
    }
}
