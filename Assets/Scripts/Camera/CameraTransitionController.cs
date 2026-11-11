using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;

public class CameraTransitionController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform gameplayPoint;
    [FormerlySerializedAs("planeCameraController")]
    [SerializeField] private PlayerCameraController playerCameraController;
    [SerializeField] private GameManager gameManager;


    [Header("Transition")]
    [SerializeField] private float moveDuration = 2f;

    [SerializeField, Range(0f, 1f)]
    private float gameStartProgress = 0.85f;


    private Coroutine transitionCoroutine;


    public void StartGameTransition()
    {
        if (transitionCoroutine != null)
            return;


        if (playerCameraController != null)
            playerCameraController.enabled = false;


        transitionCoroutine =
            StartCoroutine(MoveToGameplay());
    }


    private IEnumerator MoveToGameplay()
    {
        Vector3 startPosition =
            transform.position;

        Quaternion startRotation =
            transform.rotation;


        Vector3 targetPosition =
            gameplayPoint.position;

        Quaternion targetRotation =
            gameplayPoint.rotation;


        float elapsed = 0f;

        bool gameStarted = false;


        while (elapsed < moveDuration)
        {
            elapsed += Time.deltaTime;


            float progress =
                elapsed / moveDuration;


            float smoothProgress =
                Mathf.SmoothStep(
                    0f,
                    1f,
                    progress
                );


            transform.position =
                Vector3.Lerp(
                    startPosition,
                    targetPosition,
                    smoothProgress
                );


            transform.rotation =
                Quaternion.Slerp(
                    startRotation,
                    targetRotation,
                    smoothProgress
                );
            
            if (!gameStarted &&
                progress >= gameStartProgress)
            {
                gameStarted = true;

                gameManager.StartGame();
            }


            yield return null;
        }
        
        transform.SetPositionAndRotation(
            targetPosition,
            targetRotation
        );


        if (playerCameraController != null)
        {
            playerCameraController.SetInitialTransform(
                targetPosition,
                targetRotation
            );

            playerCameraController.enabled = true;
        }


        transitionCoroutine = null;
    }
}
