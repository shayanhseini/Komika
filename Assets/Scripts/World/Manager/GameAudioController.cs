using UnityEngine;

public class GameAudioController : MonoBehaviour
{
    [Header("Coin")]
    [SerializeField] private AudioSource coinSource;
    [SerializeField] private AudioClip coinClip;

    [Header("UI")]
    [SerializeField] private AudioSource uiSource;
    [SerializeField] private AudioClip uiButtonClip;

    [Header("Player Movement SFX")]
    [SerializeField] private AudioSource footstepsSource;
    [SerializeField] private AudioClip footstepsClip;
    [SerializeField] private AudioSource laneShiftSource;
    [SerializeField] private AudioClip laneShiftClip;
    [SerializeField, Range(0f, 1f)] private float footstepsMinVolume = 0.30f;
    [SerializeField, Range(0f, 1f)] private float footstepsMaxVolume = 0.72f;
    [SerializeField, Range(0.5f, 1.5f)] private float footstepsMinPitch = 0.90f;
    [SerializeField, Range(0.8f, 2.0f)] private float footstepsMaxPitch = 1.42f;
    [SerializeField, Range(0f, 1f)] private float laneShiftVolume = 0.85f;

    [Header("References")]
    [SerializeField] private GameManager gameManager;
    [SerializeField] private WorldMovementManager worldMovement;
    [SerializeField] private PlayerController playerController;

    private float currentFootstepVolume;

    private void Awake()
    {
        if (gameManager == null) gameManager = FindFirstObjectByType<GameManager>();
        if (worldMovement == null) worldMovement = FindFirstObjectByType<WorldMovementManager>();
        if (playerController == null) playerController = FindFirstObjectByType<PlayerController>();

        if (footstepsSource != null)
        {
            if (footstepsClip != null && footstepsSource.clip == null)
                footstepsSource.clip = footstepsClip;
            footstepsSource.playOnAwake = false;
            footstepsSource.loop = true;
            footstepsSource.volume = 0f;
        }
        if (laneShiftSource != null)
        {
            laneShiftSource.playOnAwake = false;
            laneShiftSource.loop = false;
        }
    }

    private void OnEnable()
    {
        GameEvents.ScoreChanged += OnScoreChanged;
        GameEvents.GameOver += StopFootstepsImmediate;
        if (playerController == null)
            playerController = FindFirstObjectByType<PlayerController>();
        if (playerController != null)
            playerController.OnLaneChangeStarted += HandleLaneShiftSound;
    }

    private void OnDisable()
    {
        GameEvents.ScoreChanged -= OnScoreChanged;
        GameEvents.GameOver -= StopFootstepsImmediate;
        if (playerController != null)
            playerController.OnLaneChangeStarted -= HandleLaneShiftSound;
    }

    private void Update()
    {
        UpdateFootstepsAudio();
    }

    private void OnScoreChanged(int score)
    {
        if (coinSource != null && coinClip != null)
            coinSource.PlayOneShot(coinClip);
    }

    public void PlayButtonSound()
    {
        if (uiSource != null && uiButtonClip != null)
            uiSource.PlayOneShot(uiButtonClip);
    }

    private void HandleLaneShiftSound(int direction)
    {
        if (laneShiftClip == null) return;
        if (laneShiftSource == null) laneShiftSource = uiSource != null ? uiSource : coinSource;
        if (laneShiftSource == null) return;

        laneShiftSource.panStereo = Mathf.Clamp(direction * 0.35f, -0.5f, 0.5f);
        laneShiftSource.pitch = Random.Range(0.96f, 1.07f);
        laneShiftSource.PlayOneShot(laneShiftClip, laneShiftVolume);
    }

    private void UpdateFootstepsAudio()
    {
        if (footstepsSource == null || footstepsSource.clip == null)
            return;

        bool playingRun = gameManager != null && gameManager.State == GameState.Playing
            && Time.timeScale > 0f
            && worldMovement != null && worldMovement.IsMovementEnabled && worldMovement.WorldSpeed > 0.15f
            && (playerController == null || (!playerController.IsJumping && !playerController.IsSliding));

        if (!playingRun)
        {
            currentFootstepVolume = Mathf.MoveTowards(currentFootstepVolume, 0f, 10f * Time.unscaledDeltaTime);
            footstepsSource.volume = currentFootstepVolume;
            if (currentFootstepVolume <= 0.005f && footstepsSource.isPlaying)
                footstepsSource.Pause();
            return;
        }

        float intensity = playerController != null
            ? playerController.VisionRunIntensity
            : Mathf.Clamp01((worldMovement.JogFactor - 0.3f) / 1.15f);
        float sprintT = Mathf.Clamp01((intensity - 0.1f) / 0.9f);
        float targetVol = Mathf.Lerp(footstepsMinVolume, footstepsMaxVolume, sprintT);
        float targetPitch = Mathf.Lerp(footstepsMinPitch, footstepsMaxPitch, sprintT);

        currentFootstepVolume = Mathf.MoveTowards(currentFootstepVolume, targetVol, 8f * Time.deltaTime);
        footstepsSource.volume = currentFootstepVolume;
        footstepsSource.pitch = Mathf.MoveTowards(footstepsSource.pitch, targetPitch, 6f * Time.deltaTime);
        if (!footstepsSource.isPlaying)
            footstepsSource.Play();
    }

    private void StopFootstepsImmediate()
    {
        currentFootstepVolume = 0f;
        if (footstepsSource != null)
        {
            footstepsSource.volume = 0f;
            if (footstepsSource.isPlaying)
                footstepsSource.Stop();
        }
    }
}
