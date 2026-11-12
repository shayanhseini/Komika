using UnityEngine;


public class Obstacle : MonoBehaviour
{
    private Collider pickupCollider;
    
    private void Awake()
    {
        pickupCollider = GetComponent<Collider>();


    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player") && !other.transform.root.CompareTag("Player"))
            return;

        PlayerInteraction player =
            other.GetComponentInParent<PlayerInteraction>();

        if (player == null)
            return;

        player.HitObstacle();
        
        if (pickupCollider != null)
            pickupCollider.enabled = false;
        
        gameObject.SetActive(false);
    }
}
