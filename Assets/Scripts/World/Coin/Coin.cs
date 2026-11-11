using UnityEngine;

public class Coin : MonoBehaviour
{
    [SerializeField] private int scoreValue = 1;

    private GameManager gameManager;


    private void Awake()
    {
        gameManager =
            FindFirstObjectByType<GameManager>();
    }


    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player") &&
            !other.transform.root.CompareTag("Player"))
            return;
        Collect();
    }


    private void Collect()
    {
        gameManager.AddScore(scoreValue);
        gameObject.SetActive(false);
    }
}
