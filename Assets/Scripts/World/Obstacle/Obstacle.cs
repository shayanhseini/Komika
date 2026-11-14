using UnityEngine;

public class Obstacle : MonoBehaviour
{
    [Header("Obstacle Type")]
    [Tooltip("If true, the player can safely slide under this barrier without dying.")]
    [SerializeField] private bool canSlideUnder = false;

    private Collider pickupCollider;

    private void Awake()
    {
        pickupCollider = GetComponent<Collider>();

        // Auto-detect Slide barriers based on object name if not explicitly set
        if (!canSlideUnder && gameObject.name.IndexOf("Slide", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            canSlideUnder = true;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player") && !other.transform.root.CompareTag("Player"))
            return;

        PlayerController playerController = other.GetComponentInParent<PlayerController>();

        // If this obstacle can be slid under, and player is actively sliding, DO NOT lose!
        if (canSlideUnder && playerController != null && playerController.IsSliding)
        {
            return;
        }

        PlayerInteraction player = other.GetComponentInParent<PlayerInteraction>();
        if (player == null)
            return;

        player.HitObstacle();

        if (pickupCollider != null)
            pickupCollider.enabled = false;

        gameObject.SetActive(false);
    }
}
