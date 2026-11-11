using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class GameManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private FadeController fadeController;
    [SerializeField] private WorldMovementManager worldMovement;

    public int Score { get; private set; }

    public float Distance { get; private set; }
    
    public GameState State { get; private set; }

    private void Awake()
    {
        State = GameState.Waiting;
        Score = 0;
        Distance = 0;
    }

    private void Update()
    {
        if (State != GameState.Playing)
            return;

        UpdateDistance();
    }
    
    public void StartGame()
    {
        if (State != GameState.Waiting)
            return;

        State = GameState.Playing;

        GameEvents.RaiseGameStarted();
    }


    public void AddScore(int amount)
    {
        if (State != GameState.Playing)
            return;

        Score += amount;

        GameEvents.RaiseScoreChanged(Score);
    }
    
    private void UpdateDistance()
    {
        if (worldMovement == null)
            return;

        Distance +=
            (worldMovement.RealSpeed *
            Time.deltaTime);
    }


    public void GameOver()
    {
        if (State != GameState.Playing)
            return;

        State = GameState.GameOver;

        GameEvents.RaiseGameOver();
    }


    public void RestartGameWithEffect()
    {
        StartCoroutine(RestartGameRoutine());
    }


    private IEnumerator RestartGameRoutine()
    {
        fadeController.FadeOut();

        yield return new WaitForSeconds(1f);

        RestartGame();
    }


    public void RestartGame()
    {
        Scene currentScene =
            SceneManager.GetActiveScene();

        SceneManager.LoadScene(
            currentScene.buildIndex
        );
    }


    public void Quit()
    {
        Application.Quit();
    }
}


public enum GameState
{
    Waiting,
    Playing,
    GameOver
}


