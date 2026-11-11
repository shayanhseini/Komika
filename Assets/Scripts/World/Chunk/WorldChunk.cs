using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;

public class WorldChunk : MonoBehaviour
{
    [SerializeField] private float length = 20f;
    [SerializeField] private GameObject[] buildingVariants;

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
