using UnityEngine;

public class WorldMovingEffect : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private WorldMovementManager worldMovement;


    private void Update()
    {
        if (worldMovement == null)
            return;

        if (!worldMovement.IsMovementEnabled)
            return;


        Vector3 movement =
            Vector3.back *
            worldMovement.WorldSpeed *
            Time.deltaTime;


        transform.position += movement;
    }
}