using UnityEngine;

public class ChunkDifficultyPatternController : MonoBehaviour
{
    [SerializeField] private GameObject[] easyPatterns;
    [SerializeField] private GameObject[] mediumPatterns;
    [SerializeField] private GameObject[] hardPatterns;

    public void Configure(DifficultyManager difficultyManager, bool generateContent)
    {
        SetAllInactive(easyPatterns);
        SetAllInactive(mediumPatterns);
        SetAllInactive(hardPatterns);
        if (!generateContent || difficultyManager == null) return;

        int tier = difficultyManager.Tier;
        GameObject[] candidates;
        if (tier == 0)
            candidates = easyPatterns;
        else if (tier == 1)
            candidates = Random.value < 0.25f ? easyPatterns : mediumPatterns;
        else
            candidates = Random.value < 0.25f ? mediumPatterns : hardPatterns;

        if (candidates == null || candidates.Length == 0) return;
        GameObject selected = candidates[Random.Range(0, candidates.Length)];
        if (selected != null) selected.SetActive(true);
    }

    private static void SetAllInactive(GameObject[] patterns)
    {
        if (patterns == null) return;
        foreach (GameObject pattern in patterns)
            if (pattern != null) pattern.SetActive(false);
    }
}
