using UnityEngine;

public class GameAudioController : MonoBehaviour
{
    [Header("Coin")]
    [SerializeField] private AudioSource coinSource;
    [SerializeField] private AudioClip coinClip;
    
    [Header("UI")]
    [SerializeField] private AudioSource uiSource;
    [SerializeField] private AudioClip uiButtonClip;
    
    private void OnEnable()
    {
        GameEvents.ScoreChanged += OnScoreChanged;
    }

    private void OnDisable()
    {
        GameEvents.ScoreChanged -= OnScoreChanged;
    }
    
    private void OnScoreChanged(int score)
    {
        coinSource.PlayOneShot(coinClip);
    }
    
    public void PlayButtonSound()
    {
        uiSource.PlayOneShot(uiButtonClip);
    }
    
}
