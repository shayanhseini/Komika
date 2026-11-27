using UnityEngine;

public class CameraDistanceCulling : MonoBehaviour
{
    [SerializeField] private float renderDistance = 450f;
    [SerializeField] private float cityCullDistance = 262f;

    private Camera cam;

    private void Awake()
    {
        cam = GetComponent<Camera>();
        if (cam == null) return;
        cam.farClipPlane = renderDistance;

        if (cityCullDistance > 10f && cityCullDistance < renderDistance)
        {
            float[] distances = new float[32];
            distances[0] = cityCullDistance;
            cam.layerCullDistances = distances;
            cam.layerCullSpherical = true;
        }
    }
}