using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;

public class WorldChunk : MonoBehaviour
{
    [SerializeField] private float length = 20f;
    [SerializeField] private GameObject[] buildingVariants;

    [Header("Landmarks")]
    [SerializeField] private GameObject miladTowerLeft;
    [SerializeField] private GameObject miladTowerRight;
    [Range(0f, 1f)]
    [SerializeField] private float miladSpawnChance = 0.25f;
    [SerializeField] private bool hideAllBuildingsWhenMilad = false;

    [Header("Event")]
    public UnityEvent onResetContent;

    private int activeBuildingVariant = -1;

    public float Length => length;

    public void ResetContent(
        DifficultyManager difficultyManager,
        bool generateContent = true)
    {
        onResetContent?.Invoke();
        GameObject activeVariant = ActivateRandomBuildingVariant();
        SetGameplayLayoutActive(activeVariant, generateContent);
        UpdateMiladTowerLandmark(activeVariant);
        ChunkDifficultyPatternController patternController =
            activeVariant != null ? activeVariant.GetComponentInChildren<ChunkDifficultyPatternController>(true) : null;
        if (patternController != null)
            patternController.Configure(difficultyManager, generateContent);
    }

    private GameObject ActivateRandomBuildingVariant()
    {
        if (buildingVariants == null || buildingVariants.Length == 0)
            FindBuildingVariants();

        if (buildingVariants == null || buildingVariants.Length == 0)
            return null;

        foreach (GameObject variant in buildingVariants)
            if (variant != null)
                variant.SetActive(false);

        int nextVariant = Random.Range(0, buildingVariants.Length);
        if (buildingVariants.Length > 1 && nextVariant == activeBuildingVariant)
            nextVariant = (nextVariant + Random.Range(1, buildingVariants.Length)) % buildingVariants.Length;

        activeBuildingVariant = nextVariant;
        if (buildingVariants[activeBuildingVariant] != null)
            buildingVariants[activeBuildingVariant].SetActive(true);

        return buildingVariants[activeBuildingVariant];
    }

    private void UpdateMiladTowerLandmark(GameObject activeVariant)
    {
        EnsureMiladTowerReferences();

        bool hasAnyMilad = (miladTowerLeft != null || miladTowerRight != null);
        bool shouldSpawn = hasAnyMilad && (Random.value < miladSpawnChance);

        if (!shouldSpawn)
        {
            if (miladTowerLeft != null) miladTowerLeft.SetActive(false);
            if (miladTowerRight != null) miladTowerRight.SetActive(false);

            // Re-enable all buildings for active variant
            if (activeVariant != null)
            {
                for (int i = 0; i < activeVariant.transform.childCount; i++)
                {
                    Transform child = activeVariant.transform.GetChild(i);
                    if (child.name != "Gameplay Layout")
                        child.gameObject.SetActive(true);
                }
            }
            return;
        }

        // Determine side: Left or Right
        bool spawnRight;
        if (miladTowerRight != null && miladTowerLeft != null)
        {
            spawnRight = Random.value > 0.5f;
        }
        else
        {
            spawnRight = (miladTowerRight != null);
        }

        if (spawnRight)
        {
            if (miladTowerRight != null) miladTowerRight.SetActive(true);
            if (miladTowerLeft != null) miladTowerLeft.SetActive(false);
        }
        else
        {
            if (miladTowerLeft != null) miladTowerLeft.SetActive(true);
            if (miladTowerRight != null) miladTowerRight.SetActive(false);
        }

        // Adjust buildings in activeVariant:
        // Hide buildings on the side of Milad Tower (or all buildings if hideAllBuildingsWhenMilad)
        if (activeVariant != null)
        {
            for (int i = 0; i < activeVariant.transform.childCount; i++)
            {
                Transform child = activeVariant.transform.GetChild(i);
                if (child.name == "Gameplay Layout")
                    continue;

                if (hideAllBuildingsWhenMilad)
                {
                    child.gameObject.SetActive(false);
                    continue;
                }

                bool isLeftSide = child.localPosition.x < 0f || child.name.Contains("-1_");
                bool isRightSide = child.localPosition.x > 0f || child.name.Contains("1_");

                if (spawnRight && isRightSide)
                {
                    child.gameObject.SetActive(false);
                }
                else if (!spawnRight && isLeftSide)
                {
                    child.gameObject.SetActive(false);
                }
                else
                {
                    child.gameObject.SetActive(true);
                }
            }
        }
    }

    private void EnsureMiladTowerReferences()
    {
        if (miladTowerLeft == null)
        {
            Transform t = transform.Find("Enviroment/Env/Milad Tower Left");
            if (t != null) miladTowerLeft = t.gameObject;
        }

        if (miladTowerRight == null)
        {
            Transform t = transform.Find("Enviroment/Env/Milad Tower Right");
            if (t != null) miladTowerRight = t.gameObject;
        }
    }

    private static void SetGameplayLayoutActive(GameObject variant, bool active)
    {
        if (variant == null)
            return;

        Transform layout = variant.transform.Find("Gameplay Layout");
        if (layout != null)
            layout.gameObject.SetActive(active);
    }

    private void FindBuildingVariants()
    {
        Transform environment = transform.Find("Enviroment/Env");
        if (environment == null)
            return;

        var variants = new List<GameObject>();
        for (int i = 0; i < environment.childCount; i++)
        {
            Transform child = environment.GetChild(i);
            if (child.name.StartsWith("Building"))
                variants.Add(child.gameObject);
        }

        buildingVariants = variants.ToArray();
    }

    public void Move(Vector3 movement)
    {
        transform.position += movement;
    }

    public void MoveTo(Vector3 position)
    {
        transform.position = position;
    }
}
