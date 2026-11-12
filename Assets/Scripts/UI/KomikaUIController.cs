using System.Collections;
using TMPro;
using UnityEngine;

public class KomikaUIController : MonoBehaviour
{
    private const string AutoStartAfterRestartKey = "KomikaAutoStartAfterRestart";

    [Header("Panels")]
    [SerializeField] private GameObject menuExtensions;
    [SerializeField] private GameObject characterPanel;
    [SerializeField] private GameObject howToPlayPanel;
    [SerializeField] private GameObject gameplayHud;
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private CanvasGroup menuGroup;
    [SerializeField] private CanvasGroup characterGroup;
    [SerializeField] private CanvasGroup howToPlayGroup;
    [SerializeField] private CanvasGroup hudGroup;
    [SerializeField] private CanvasGroup pauseGroup;
    [SerializeField] private CanvasGroup gameOverGroup;
    [SerializeField] private CanvasGroup[] allPanelGroups;
    [SerializeField] private float panelFadeDuration = 0.38f;

    [Header("Character Selection")]
    [SerializeField] private Renderer playerRenderer;
    [SerializeField] private Material[] characterMaterials;
    [SerializeField] private GameObject[] selectedIndicators;
    [SerializeField] private RectTransform[] characterCards;
    [SerializeField] private CanvasGroup[] characterCardGroups;
    [SerializeField] private float carouselSpacing = 430f;
    [SerializeField] private float carouselSmoothness = 12f;

    [Header("Records")]
    [SerializeField] private TMP_Text currentRecordText;
    [SerializeField] private TMP_Text hudBestRecordText;
    [SerializeField] private TMP_Text menuBestRecordText;
    [SerializeField] private TMP_Text gameOverRecordText;
    [SerializeField] private TMP_Text gameOverBestRecordText;

    [Header("References")]
    [SerializeField] private GameManager gameManager;
    [SerializeField] private SaveManager saveManager;

    private int selectedCharacterIndex;
    private float displayedBestRecord;
    private bool isPaused;
    private Coroutine panelTransition;

    private void Awake()
    {
        Time.timeScale = 1f;
        bool autoStartAfterRestart = PlayerPrefs.GetInt(AutoStartAfterRestartKey, 0) == 1;
        displayedBestRecord = saveManager != null ? saveManager.GetHighestScore() : 0f;
        UpdateRecordLabels();
        SetInitialPanelState(!autoStartAfterRestart);
        SelectCharacter(PlayerPrefs.GetInt("KomikaSelectedCharacter", 0));

        if (autoStartAfterRestart)
        {
            PlayerPrefs.DeleteKey(AutoStartAfterRestartKey);
            PlayerPrefs.Save();
            StartCoroutine(AutoStartAfterReload());
        }
    }

    private void OnEnable()
    {
        GameEvents.GameStarted += HandleGameStarted;
        GameEvents.GameOver += HandleGameOver;
    }

    private void OnDisable()
    {
        GameEvents.GameStarted -= HandleGameStarted;
        GameEvents.GameOver -= HandleGameOver;
        Time.timeScale = 1f;
    }

    private void Update()
    {
        UpdateCharacterCarousel();
        if (gameManager == null || gameManager.State != GameState.Playing)
            return;
        if (gameManager.Distance >= displayedBestRecord)
            displayedBestRecord = gameManager.Distance;
        UpdateRecordLabels();
    }

    public void OpenCharacterPanel() => TransitionTo(characterGroup);
    public void OpenHowToPlayPanel() => TransitionTo(howToPlayGroup);
    public void CloseOverlayPanels() => TransitionTo(menuGroup);

    public void PauseGame()
    {
        if (isPaused || gameManager == null || gameManager.State != GameState.Playing) return;
        isPaused = true;
        Time.timeScale = 0f;
        TransitionTo(pauseGroup);
    }

    public void ResumeGame()
    {
        if (!isPaused) return;
        isPaused = false;
        Time.timeScale = 1f;
        TransitionTo(hudGroup);
    }

    public void TogglePauseMenu() { if (isPaused) ResumeGame(); else PauseGame(); }

    public void RestartGame()
    {
        Time.timeScale = 1f;
        PlayerPrefs.SetInt(AutoStartAfterRestartKey, 1);
        PlayerPrefs.Save();
        if (gameManager != null) gameManager.RestartGameWithEffect();
    }

    public void ReturnToMainMenu()
    {
        Time.timeScale = 1f;
        PlayerPrefs.DeleteKey(AutoStartAfterRestartKey);
        PlayerPrefs.Save();
        if (gameManager != null) gameManager.RestartGame();
    }

    private IEnumerator AutoStartAfterReload()
    {
        yield return null;
        UIManager uiManager = FindFirstObjectByType<UIManager>();
        if (uiManager != null)
            uiManager.StartGame();
        else if (gameManager != null)
            gameManager.StartGame();
    }

    public void SelectCharacter(int index)
    {
        if (characterMaterials == null || characterMaterials.Length == 0) return;
        index = Mathf.Clamp(index, 0, characterMaterials.Length - 1);
        selectedCharacterIndex = index;
        if (playerRenderer != null && characterMaterials[index] != null) playerRenderer.sharedMaterial = characterMaterials[index];
        PlayerPrefs.SetInt("KomikaSelectedCharacter", index);
        PlayerPrefs.Save();
        if (selectedIndicators == null) return;
        for (int i = 0; i < selectedIndicators.Length; i++)
            if (selectedIndicators[i] != null) selectedIndicators[i].SetActive(i == index);
    }

    public void ShowNextCharacter() { if (characterMaterials != null && characterMaterials.Length > 0) SelectCharacter((selectedCharacterIndex + 1) % characterMaterials.Length); }
    public void ShowPreviousCharacter() { if (characterMaterials != null && characterMaterials.Length > 0) SelectCharacter((selectedCharacterIndex - 1 + characterMaterials.Length) % characterMaterials.Length); }

    private void HandleGameStarted()
    {
        isPaused = false;
        Time.timeScale = 1f;
        TransitionTo(hudGroup);
    }

    private void HandleGameOver()
    {
        isPaused = false;
        Time.timeScale = 1f;
        if (gameManager != null) displayedBestRecord = Mathf.Max(displayedBestRecord, gameManager.Distance);
        UpdateRecordLabels();
        TransitionTo(gameOverGroup);
    }

    private void UpdateRecordLabels()
    {
        float current = gameManager != null ? gameManager.Distance : 0f;
        string currentValue = $"{Mathf.FloorToInt(current):0000} M";
        string bestValue = $"{Mathf.FloorToInt(Mathf.Max(displayedBestRecord, current)):0000} M";
        if (currentRecordText != null) currentRecordText.text = currentValue;
        if (hudBestRecordText != null) hudBestRecordText.text = bestValue;
        if (menuBestRecordText != null) menuBestRecordText.text = bestValue;
        if (gameOverRecordText != null) gameOverRecordText.text = currentValue;
        if (gameOverBestRecordText != null) gameOverBestRecordText.text = bestValue;
    }

    private void SetInitialPanelState(bool showMenu)
    {
        if (allPanelGroups == null) return;
        foreach (CanvasGroup group in allPanelGroups) SetGroupImmediate(group, showMenu && group == menuGroup);
    }

    private void TransitionTo(CanvasGroup target)
    {
        if (target == null || allPanelGroups == null) return;
        if (panelTransition != null) StopCoroutine(panelTransition);
        panelTransition = StartCoroutine(FadePanels(target));
    }

    private IEnumerator FadePanels(CanvasGroup target)
    {
        float[] startingAlpha = new float[allPanelGroups.Length];
        for (int i = 0; i < allPanelGroups.Length; i++)
        {
            CanvasGroup group = allPanelGroups[i];
            if (group == null) continue;
            group.gameObject.SetActive(true);
            group.interactable = false;
            group.blocksRaycasts = false;
            startingAlpha[i] = group.alpha;
        }
        float elapsed = 0f;
        while (elapsed < panelFadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / panelFadeDuration));
            for (int i = 0; i < allPanelGroups.Length; i++)
            {
                CanvasGroup group = allPanelGroups[i];
                if (group != null) group.alpha = Mathf.Lerp(startingAlpha[i], group == target ? 1f : 0f, t);
            }
            yield return null;
        }
        foreach (CanvasGroup group in allPanelGroups) SetGroupImmediate(group, group == target);
        panelTransition = null;
    }

    private static void SetGroupImmediate(CanvasGroup group, bool visible)
    {
        if (group == null) return;
        group.alpha = visible ? 1f : 0f;
        group.interactable = visible;
        group.blocksRaycasts = visible;
        group.gameObject.SetActive(visible);
    }

    private void UpdateCharacterCarousel()
    {
        if (characterGroup == null || !characterGroup.gameObject.activeInHierarchy || characterCards == null) return;
        float blend = 1f - Mathf.Exp(-carouselSmoothness * Time.unscaledDeltaTime);
        for (int i = 0; i < characterCards.Length; i++)
        {
            RectTransform card = characterCards[i];
            if (card == null) continue;
            int offset = i - selectedCharacterIndex;
            card.anchoredPosition = Vector2.Lerp(card.anchoredPosition, new Vector2(offset * carouselSpacing, 0f), blend);
            card.localScale = Vector3.Lerp(card.localScale, Vector3.one * (offset == 0 ? 1f : 0.68f), blend);
            if (characterCardGroups != null && i < characterCardGroups.Length && characterCardGroups[i] != null)
            {
                characterCardGroups[i].alpha = Mathf.Lerp(characterCardGroups[i].alpha, Mathf.Abs(offset) <= 1 ? (offset == 0 ? 1f : 0.42f) : 0f, blend);
                characterCardGroups[i].blocksRaycasts = offset == 0;
            }
        }
    }
}
