using System.Collections;
using UnityEngine;

public class GameMusicController : MonoBehaviour
{
    [Header("Audio Sources")]
    [SerializeField] private AudioSource waitingSource;
    [SerializeField] private AudioSource gameSource;


    [Header("Volume")]
    [SerializeField, Range(0f, 1f)]
    private float waitingVolume = 1f;

    [SerializeField, Range(0f, 1f)]
    private float gameVolume = 1f;


    [Header("Crossfade")]
    [SerializeField] private float fadeDuration = 2f;


    private Coroutine crossfadeCoroutine;


    private void Awake()
    {
        waitingSource.volume = waitingVolume;
        waitingSource.loop = true;
        waitingSource.Play();


        gameSource.volume = 0f;
        gameSource.loop = true;
        gameSource.Play();
    }


    private void OnEnable()
    {
        GameEvents.GameStarted += SwitchToGameMusic;
        GameEvents.GameOver += SwitchToWaitingMusic;
    }


    private void OnDisable()
    {
        GameEvents.GameStarted -= SwitchToGameMusic;
        GameEvents.GameOver -= SwitchToWaitingMusic;
    }


    private void SwitchToGameMusic()
    {
        Crossfade(
            waitingSource,
            gameSource,
            waitingVolume,
            gameVolume
        );
    }


    private void SwitchToWaitingMusic()
    {
        Crossfade(
            gameSource,
            waitingSource,
            gameVolume,
            waitingVolume
        );
    }


    private void Crossfade(
        AudioSource from,
        AudioSource to,
        float fromVolume,
        float toVolume)
    {
        if (crossfadeCoroutine != null)
            StopCoroutine(crossfadeCoroutine);


        crossfadeCoroutine =
            StartCoroutine(
                CrossfadeCoroutine(
                    from,
                    to,
                    fromVolume,
                    toVolume
                )
            );
    }


    private IEnumerator CrossfadeCoroutine(
        AudioSource from,
        AudioSource to,
        float fromVolume,
        float toVolume)
    {
        if (!to.isPlaying)
            to.Play();


        float startFromVolume =
            from.volume;

        float startToVolume =
            to.volume;


        float elapsed = 0f;


        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;


            float t =
                Mathf.Clamp01(
                    elapsed / fadeDuration
                );


            t = Mathf.SmoothStep(
                0f,
                1f,
                t
            );


            from.volume =
                Mathf.Lerp(
                    startFromVolume,
                    0f,
                    t
                );


            to.volume =
                Mathf.Lerp(
                    startToVolume,
                    toVolume,
                    t
                );


            yield return null;
        }


        from.volume = 0f;
        to.volume = toVolume;


        from.Stop();


        crossfadeCoroutine = null;
    }
}