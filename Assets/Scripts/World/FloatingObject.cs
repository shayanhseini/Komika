using UnityEngine;

public class FloatingObject : MonoBehaviour
{
    [Header("Float")]
    [SerializeField] private float amplitude = 0.2f;
    [SerializeField] private float speed = 2f;

    private Vector3 initialPosition;
    private float randomOffset;

    private void Awake()
    {
        initialPosition = transform.localPosition;

        randomOffset =
            Random.Range(0f, Mathf.PI * 2f);
    }

    private void Update()
    {
        float offset =
            Mathf.Sin(
                Time.time * speed + randomOffset
            ) * amplitude;

        Vector3 position =
            initialPosition;

        position.y += offset;

        transform.localPosition = position;
    }
}