using TMPro;
using UnityEngine;

public class KomikaUIController : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject menuExtensions;
    [SerializeField] private GameObject characterPanel;
    [SerializeField] private GameObject howToPlayPanel;
    [SerializeField] private GameObject gameplayHud;

    [Header("Character Selection")]
    [SerializeField] private Renderer playerRenderer;
    [SerializeField] private Material[] characterMaterials;
    [SerializeField] private GameObject[] selectedIndicators;
    [SerializeField] private RectTransform[] characterCards;
    [SerializeField] private CanvasGroup[] characterCardGroups;
    [SerializeField] private float carouselSpacing = 430f;
    [SerializeField] private float carouselSmoothness = 12f;

    [Header("HUD")]
    [SerializeField] private TMP_Text coinText;
    [SerializeField] private TMP_Text distanceText;
    [SerializeField] private TMP_Text speedText;
    [SerializeField] private GameManager gameManager;
    [SerializeField] private WorldMovementManager worldMovement;

    private int coinCount;
    private int selectedCharacterIndex;

    private void Awake()
    {
        CloseOverlayPanels();
        if (menuExtensions != null)
            menuExtensions.SetActive(true);
        if (gameplayHud != null)
            gameplayHud.SetActive(false);
        SelectCharacter(PlayerPrefs.GetInt("KomikaSelectedCharacter", 0));
    }

    private void OnEnable()
    {
        GameEvents.GameStarted += HandleGameStarted;
        GameEvents.GameOver += HandleGameOver;
        GameEvents.ScoreChanged += HandleScoreChanged;
    }

    private void OnDisable()
    {
        GameEvents.GameStarted -= HandleGameStarted;
        GameEvents.GameOver -= HandleGameOver;
        GameEvents.ScoreChanged -= HandleScoreChanged;
    }

    private void Update()
    {
        UpdateCharacterCarousel();

        if (gameplayHud == null || !gameplayHud.activeInHierarchy)
            return;

        if (coinText != null)
            coinText.text = coinCount.ToString("D4");
        if (distanceText != null && gameManager != null)
            distanceText.text = $"{gameManager.Distance:000} M";
        if (speedText != null && worldMovement != null)
            speedText.text = $"{worldMovement.RealSpeed:000} KM/H";
    }

    public void OpenCharacterPanel()
    {
        if (characterPanel != null)
            characterPanel.SetActive(true);
        if (howToPlayPanel != null)
            howToPlayPanel.SetActive(false);
    }

    public void OpenHowToPlayPanel()
    {
        if (howToPlayPanel != null)
            howToPlayPanel.SetActive(true);
        if (characterPanel != null)
            characterPanel.SetActive(false);
    }

    public void CloseOverlayPanels()
    {
        if (characterPanel != null)
            characterPanel.SetActive(false);
        if (howToPlayPanel != null)
            howToPlayPanel.SetActive(false);
    }

    public void SelectCharacter(int index)
    {
        if (characterMaterials == null || characterMaterials.Length == 0)
            return;

        index = Mathf.Clamp(index, 0, characterMaterials.Length - 1);
        selectedCharacterIndex = index;
        if (playerRenderer != null && characterMaterials[index] != null)
            playerRenderer.sharedMaterial = characterMaterials[index];

        PlayerPrefs.SetInt("KomikaSelectedCharacter", index);
        PlayerPrefs.Save();

        if (selectedIndicators == null)
            return;
        for (int i = 0; i < selectedIndicators.Length; i++)
            if (selectedIndicators[i] != null)
                selectedIndicators[i].SetActive(i == index);
    }

    public void ShowNextCharacter()
    {
        if (characterMaterials == null || characterMaterials.Length == 0)
            return;
        SelectCharacter((selectedCharacterIndex + 1) % characterMaterials.Length);
    }

    public void ShowPreviousCharacter()
    {
        if (characterMaterials == null || characterMaterials.Length == 0)
            return;
        SelectCharacter((selectedCharacterIndex - 1 + characterMaterials.Length) % characterMaterials.Length);
    }

    private void UpdateCharacterCarousel()
    {
        if (characterPanel == null || !characterPanel.activeInHierarchy || characterCards == null)
            return;

        float blend = 1f - Mathf.Exp(-carouselSmoothness * Time.unscaledDeltaTime);
        for (int i = 0; i < characterCards.Length; i++)
        {
            RectTransform card = characterCards[i];
            if (card == null)
                continue;

            int offset = i - selectedCharacterIndex;
            float targetX = offset * carouselSpacing;
            card.anchoredPosition = Vector2.Lerp(card.anchoredPosition, new Vector2(targetX, 0f), blend);
            float targetScale = offset == 0 ? 1f : 0.68f;
            card.localScale = Vector3.Lerp(card.localScale, Vector3.one * targetScale, blend);

            if (characterCardGroups != null && i < characterCardGroups.Length && characterCardGroups[i] != null)
            {
                float alpha = Mathf.Abs(offset) <= 1 ? (offset == 0 ? 1f : 0.42f) : 0f;
                characterCardGroups[i].alpha = Mathf.Lerp(characterCardGroups[i].alpha, alpha, blend);
                characterCardGroups[i].blocksRaycasts = offset == 0;
            }
        }
    }

    private void HandleGameStarted()
    {
        CloseOverlayPanels();
        if (menuExtensions != null)
            menuExtensions.SetActive(false);
        if (gameplayHud != null)
            gameplayHud.SetActive(true);
    }

    private void HandleGameOver()
    {
        if (gameplayHud != null)
            gameplayHud.SetActive(false);
        if (menuExtensions != null)
            menuExtensions.SetActive(false);
    }

    private void HandleScoreChanged(int score) => coinCount = score;
}
