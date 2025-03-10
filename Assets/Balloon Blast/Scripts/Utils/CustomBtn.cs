using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Events;

public class CustomBtn : Selectable, IPointerDownHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, ISubmitHandler
{
    [SerializeField] Image visuals;
    [SerializeField] float scaleDownAmount;
    [SerializeField] float animationTime;
    [SerializeField] UnityEvent OnClick;
    [SerializeField] LeanTweenType tweenType;

    public void OnBeginDrag(PointerEventData eventData)
    {
    }

    public void OnDrag(PointerEventData eventData)
    {
    }

    public void OnEndDrag(PointerEventData eventData)
    {
    }

    public override void OnPointerDown(PointerEventData eventData)
    {
        base.OnPointerDown(eventData);
        Vector3 pos = visuals.transform.localScale;
        LeanTween.scale(transform.gameObject, pos * scaleDownAmount, animationTime).setLoopPingPong(1).setEase(tweenType).setOnComplete(() => OnClick?.Invoke());
    }

    public void OnSubmit(BaseEventData eventData)
    {
        Vector3 pos = visuals.transform.localScale;
        LeanTween.scale(transform.gameObject, pos * scaleDownAmount, animationTime).setLoopPingPong(1).setEase(tweenType).setOnComplete(() => OnClick?.Invoke());

    }
}
