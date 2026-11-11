using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class FadeController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Image fadeImage;


    [Header("Settings")]
    [SerializeField] private float fadeDuration = 1f;


    private Coroutine fadeCoroutine;

    private void OnEnable()
    {
        fadeImage.gameObject.SetActive(true);
        FadeIn();
    }

    
    public void FadeIn()
    {
        fadeImage.raycastTarget = true;

        StartFade(1f, 0f);
    }


    public void FadeOut()
    {
        fadeImage.raycastTarget = true;

        StartFade(0f, 1f);
    }


    private void StartFade(float from, float to)
    {
        if (fadeCoroutine != null)
            StopCoroutine(fadeCoroutine);


        fadeCoroutine =
            StartCoroutine(
                Fade(from, to)
            );
    }


    private IEnumerator Fade(float from, float to)
    {
        Color color =
            fadeImage.color;

        color.a = from;

        fadeImage.color = color;


        float elapsed = 0f;


        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;


            float t =
                elapsed / fadeDuration;


            t = Mathf.SmoothStep(
                0f,
                1f,
                t
            );


            color.a =
                Mathf.Lerp(
                    from,
                    to,
                    t
                );


            fadeImage.color = color;


            yield return null;
        }


        color.a = to;

        fadeImage.color = color;


        // وقتی کاملاً شفاف شد،
        // دیگر جلوی کلیک UI را نگیرد.
        if (to <= 0f)
        {
            fadeImage.raycastTarget = false;
        }


        fadeCoroutine = null;
    }
}