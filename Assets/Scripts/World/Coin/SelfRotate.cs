using UnityEngine;

public class SelfRotate : MonoBehaviour
{
    [SerializeField] private float rotationSpeed = 100f;

    private void Update()
    {
        transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.Self);
    }
}