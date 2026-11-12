using UnityEngine;

public class CrossroadTrafficController : MonoBehaviour
{
    [SerializeField] private GameObject carPrefab;
    [SerializeField] private Transform leftSpawnPoint;
    [SerializeField] private Transform rightSpawnPoint;
    [SerializeField] private Transform vehicleContainer;
    [SerializeField] private float vehicleSpeed = 8f;
    [SerializeField] private float initialDelay = 0.6f;
    [SerializeField] private float minimumGapSeconds = 1.8f;
    [SerializeField] private float maximumGapSeconds = 3.2f;

    private GameManager gameManager;
    private GameObject activeVehicle;
    private int travelDirection;
    private float nextSpawnTime;

    private void Awake()
    {
        gameManager = FindFirstObjectByType<GameManager>();
        if (vehicleContainer == null)
            vehicleContainer = transform;
    }

    private void OnEnable()
    {
        ClearActiveVehicle();
        nextSpawnTime = Time.time + initialDelay;
    }

    private void OnDisable()
    {
        ClearActiveVehicle();
    }

    private void Update()
    {
        if (gameManager == null || gameManager.State != GameState.Playing)
            return;

        if (activeVehicle == null || !activeVehicle.activeInHierarchy)
        {
            if (activeVehicle != null)
            {
                Destroy(activeVehicle);
                activeVehicle = null;
                nextSpawnTime = Time.time + Random.Range(minimumGapSeconds, maximumGapSeconds);
                return;
            }
            if (Time.time >= nextSpawnTime)
                SpawnVehicle();
            return;
        }

        activeVehicle.transform.localPosition += Vector3.right * (travelDirection * vehicleSpeed * Time.deltaTime);
        float destinationX = travelDirection > 0 ? rightSpawnPoint.localPosition.x : leftSpawnPoint.localPosition.x;
        if ((travelDirection > 0 && activeVehicle.transform.localPosition.x >= destinationX) ||
            (travelDirection < 0 && activeVehicle.transform.localPosition.x <= destinationX))
        {
            Destroy(activeVehicle);
            activeVehicle = null;
            nextSpawnTime = Time.time + Random.Range(minimumGapSeconds, maximumGapSeconds);
        }
    }

    private void SpawnVehicle()
    {
        if (carPrefab == null || leftSpawnPoint == null || rightSpawnPoint == null)
            return;

        travelDirection = Random.value < 0.5f ? 1 : -1;
        Transform spawnPoint = travelDirection > 0 ? leftSpawnPoint : rightSpawnPoint;
        activeVehicle = Instantiate(carPrefab, vehicleContainer);
        activeVehicle.name = "Crossroad Car";
        activeVehicle.transform.localPosition = spawnPoint.localPosition;
        activeVehicle.transform.localRotation = Quaternion.Euler(0f, travelDirection > 0 ? 90f : -90f, 0f);
        activeVehicle.transform.localScale = Vector3.one;
    }

    private void ClearActiveVehicle()
    {
        if (activeVehicle != null)
            Destroy(activeVehicle);
        activeVehicle = null;
    }
}
