using UnityEngine;
using UnityEngine.EventSystems;

public class CharacterCarouselSwipe : MonoBehaviour, IBeginDragHandler, IEndDragHandler
{
    [SerializeField] private KomikaUIController controller;
    [SerializeField] private float swipeThreshold = 60f;

    private Vector2 dragStart;

    public void OnBeginDrag(PointerEventData eventData)
    {
        dragStart = eventData.position;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (controller == null)
            return;

        float horizontalDrag = eventData.position.x - dragStart.x;
        if (Mathf.Abs(horizontalDrag) < swipeThreshold)
            return;

        if (horizontalDrag < 0f)
            controller.ShowNextCharacter();
        else
            controller.ShowPreviousCharacter();
    }
}
