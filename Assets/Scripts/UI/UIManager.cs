using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject startPanel;
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private GameObject gamePanel;


    [Header("Canvas Groups")]
    [SerializeField] private CanvasGroup startCanvasGroup;
    [SerializeField] private CanvasGroup gameCanvasGroup;
    [SerializeField] private CanvasGroup gameOverCanvasGroup;


    [Header("Fade")]
    [SerializeField] private float fadeDuration = 0.5f;


    [Header("Score")]
    [SerializeField] private TMP_Text coinText;
    [SerializeField] private TMP_Text finalCoinText;
    [SerializeField] private TMP_Text totalCoinText;
    [SerializeField] private TMP_Text highestScoreText;


    [Header("Flight Info")]
    [SerializeField] private TMP_Text speedText;
    [SerializeField] private TMP_Text distanceText;

    
    [Header("References")]
    [SerializeField] private GameManager gameManager;
    [SerializeField] private SaveManager saveManager;
    [SerializeField] private WorldMovementManager worldMovement;
    [SerializeField] private Transform player;

    [SerializeField] private CameraTransitionController cameraTransition;
    [SerializeField] private Button startButton;


    private readonly Dictionary<CanvasGroup, Coroutine> fadeCoroutines =
        new Dictionary<CanvasGroup, Coroutine>();


    private void Awake()
    {
        bool autoStartAfterRestart = PlayerPrefs.GetInt("KomikaAutoStartAfterRestart", 0) == 1;
        if (autoStartAfterRestart)
        {
            if (startPanel != null)
                startPanel.SetActive(false);
            if (startCanvasGroup != null)
            {
                startCanvasGroup.alpha = 0f;
                startCanvasGroup.interactable = false;
                startCanvasGroup.blocksRaycasts = false;
            }
        }
        else
        {
            ShowStartUI();
        }

        if (gamePanel != null)
            gamePanel.SetActive(false);

        if (gameOverPanel != null)
            gameOverPanel.SetActive(false);


        UpdateCoin(0);
        UpdateFinalScore(0);

    }


    private void OnEnable()
    {
        GameEvents.GameStarted += HandleGameStarted;
        GameEvents.GameOver += HandleGameOver;
        GameEvents.ScoreChanged += UpdateCoin;

        HandleLoadGame();
    }


    private void OnDisable()
    {
        GameEvents.GameStarted -= HandleGameStarted;
        GameEvents.GameOver -= HandleGameOver;
        GameEvents.ScoreChanged -= UpdateCoin;

    }
    private void Update()
    {
        if (gameManager == null)
            return;


        if (gameManager.State != GameState.Playing)
            return;


        UpdateFlightInfo();
    }
    
    // ==================================================
    // Game State
    // ==================================================

    private void HandleLoadGame()
    {
                
        int totalCoin =
            saveManager.GetTotalCoin();

        if (totalCoinText != null)
            totalCoinText.text = totalCoin.ToString("D4");
        
        int highestScore =
            (int)saveManager.GetHighestScore();
        if (highestScoreText != null)
            highestScoreText.text = highestScore.ToString("D4");
    }
    
    private void HandleGameStarted()
    {
        FadeOut(startCanvasGroup);
        FadeIn(gameCanvasGroup);
    }


    private void HandleGameOver()
    {
        HidePanel(startPanel);

        FadeOut(gameCanvasGroup);
        
        ShowPanel(gameOverPanel);
        FadeIn(gameOverCanvasGroup);

        UpdateFinalScore(gameManager.Distance);
    }
    
    // ==================================================
    // Score
    // ==================================================

    private void UpdateCoin(int score)
    {
        if (coinText == null)
            return;


        coinText.text =
            score.ToString("D4");
    }


    private void UpdateFinalScore(float score)
    {
        if (finalCoinText == null)
            return;

        int scoreInt = (int)score;

        finalCoinText.text =
            scoreInt.ToString("D4");
    }


    // ==================================================
    // Flight Info
    // ==================================================

    private void UpdateFlightInfo()
    {
        UpdateSpeed();
        UpdateDistance();
    }


    private void UpdateSpeed()
    {
        if (speedText == null ||
            worldMovement == null)
            return;


        float displayedSpeed =
            worldMovement.RealSpeed;


        speedText.text =
            $"{displayedSpeed:000}";
    }


    private void UpdateDistance()
    {
        if (distanceText == null ||
            gameManager == null)
            return;

        distanceText.text =
            $"{gameManager.Distance:000}";
    }


    // ==================================================
    // Start Panel
    // ==================================================

    private void ShowStartUI()
    {
        if (startPanel != null)
            startPanel.SetActive(true);


        if (startCanvasGroup != null)
        {
            startCanvasGroup.alpha = 1f;
            startCanvasGroup.interactable = true;
            startCanvasGroup.blocksRaycasts = true;
        }
    }


    // ==================================================
    // Buttons
    // ==================================================

    public void StartGame()
    {
        if (startButton != null)
            startButton.interactable = false;


        if (cameraTransition != null)
            cameraTransition.StartGameTransition();
    }
    
    // ==================================================
    // Fade
    // ==================================================

    private void FadeIn(CanvasGroup canvasGroup)
    {
        if (canvasGroup == null)
            return;


        canvasGroup.gameObject.SetActive(true);


        StartFade(
            canvasGroup,
            canvasGroup.alpha,
            1f
        );
    }


    private void FadeOut(CanvasGroup canvasGroup)
    {
        if (canvasGroup == null)
            return;


        StartFade(
            canvasGroup,
            canvasGroup.alpha,
            0f
        );
    }


    private void StartFade(
        CanvasGroup canvasGroup,
        float from,
        float to)
    {
        if (fadeCoroutines.TryGetValue(
                canvasGroup,
                out Coroutine existingCoroutine))
        {
            StopCoroutine(existingCoroutine);

            fadeCoroutines.Remove(canvasGroup);
        }


        Coroutine newCoroutine =
            StartCoroutine(
                FadeCanvasGroup(
                    canvasGroup,
                    from,
                    to
                )
            );


        fadeCoroutines[canvasGroup] =
            newCoroutine;
    }


    private IEnumerator FadeCanvasGroup(
        CanvasGroup canvasGroup,
        float from,
        float to)
    {
        canvasGroup.alpha = from;


        if (to < from)
        {
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
        else
        {
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }


        float elapsed = 0f;


        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;


            float t =
                Mathf.Clamp01(
                    elapsed /
                    fadeDuration
                );


            t = Mathf.SmoothStep(
                0f,
                1f,
                t
            );


            canvasGroup.alpha =
                Mathf.Lerp(
                    from,
                    to,
                    t
                );


            yield return null;
        }


        canvasGroup.alpha = to;


        if (to <= 0f)
        {
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.gameObject.SetActive(false);
        }
        else
        {
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }


        fadeCoroutines.Remove(canvasGroup);
    }


    // ==================================================
    // Panel Visibility
    // ==================================================

    private void HidePanel(GameObject panel)
    {
        if (panel != null)
            panel.SetActive(false);
    }


    private void ShowPanel(GameObject panel)
    {
        if (panel != null)
            panel.SetActive(true);
    }
    
}
