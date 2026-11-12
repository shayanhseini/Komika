using UnityEngine;

public class CurvedWorldController : MonoBehaviour
{
    [SerializeField] private Transform curveAnchor;
    [SerializeField, Range(0f, 0.004f)] private float verticalCurveStrength = 0.0012f;
    [SerializeField, Range(-0.002f, 0.002f)] private float horizontalCurveStrength;
    [SerializeField, Min(20f)] private float maximumCurveDistance = 160f;

    private static readonly int CurveOriginId = Shader.PropertyToID("_KomikaCurveOrigin");
    private static readonly int CurveParamsId = Shader.PropertyToID("_KomikaCurveParams");

    private void LateUpdate()
    {
        Vector3 origin = curveAnchor != null ? curveAnchor.position : transform.position;
        Shader.SetGlobalVector(CurveOriginId, new Vector4(origin.x, origin.y, origin.z, 1f));
        Shader.SetGlobalVector(CurveParamsId, new Vector4(verticalCurveStrength, horizontalCurveStrength, maximumCurveDistance, 0f));
    }

    private void OnDisable()
    {
        Shader.SetGlobalVector(CurveParamsId, Vector4.zero);
    }
}
