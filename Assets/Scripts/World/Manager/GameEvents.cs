using System;

public static class GameEvents
{
    public static event Action GameStarted;
    public static event Action GameOver;

    public static event Action<int> ScoreChanged;

    public static event Action PlayerHit;

    public static void RaiseGameStarted()
    {
        GameStarted?.Invoke();
    }


    public static void RaiseGameOver()
    {
        GameOver?.Invoke();
    }


    public static void RaiseScoreChanged(int score)
    {
        ScoreChanged?.Invoke(score);
    }


    public static void RaisePlayerHit()
    {
        PlayerHit?.Invoke();
    }
    

}
