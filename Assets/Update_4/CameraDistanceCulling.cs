using UnityEngine;

public class CameraDistanceCulling : MonoBehaviour
{
    [SerializeField] private float renderDistance = 100f;

    private Camera cam;

    private void Awake()
    {
        cam = GetComponent<Camera>();
        cam.farClipPlane = renderDistance;
    }
}