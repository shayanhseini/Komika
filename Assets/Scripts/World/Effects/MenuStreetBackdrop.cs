using UnityEngine;

/// <summary>
/// Displays a compact street backdrop behind the character while waiting on the start menu (-Z camera),
/// and disables it automatically once the camera rotates toward gameplay (+Z).
/// </summary>
public class MenuStreetBackdrop : MonoBehaviour
{
    [SerializeField] private Transform cameraTransform;
    private bool started;

    private void OnEnable()
    {
        GameEvents.GameStarted += HandleGameStarted;
    }

    private void OnDisable()
    {
        GameEvents.GameStarted -= HandleGameStarted;
    }

    private void HandleGameStarted()
    {
        started = true;
    }

    private void LateUpdate()
    {
        if (!started)
            return;

        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;

        if (cameraTransform == null || cameraTransform.forward.z > 0.25f)
            gameObject.SetActive(false);
    }
}
