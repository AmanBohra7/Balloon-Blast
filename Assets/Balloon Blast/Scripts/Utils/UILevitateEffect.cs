using UnityEngine;
using UnityEngine.UI;

public class UILevitateEffect : MonoBehaviour
{
    public float moveAmount = 5f;  // Max movement distance
    public float moveSpeed = 2f;   // Speed of movement
    public bool lockX = false;     // Lock X movement
    public bool lockY = false;     // Lock Y movement

    private Vector3 originalPosition;

    void Start()
    {
        originalPosition = transform.localPosition;
        StartLevitation();
    }


    void StartLevitation()
    {
        Vector3 targetOffset = new Vector3(
            lockX ? 0 : Random.Range(-moveAmount, moveAmount),
            lockY ? 0 : Random.Range(-moveAmount, moveAmount),
            0
        );

        Vector3 targetPosition = originalPosition + targetOffset;

        LeanTween.moveLocal(gameObject, targetPosition, moveSpeed)
            .setEaseInOutSine()
            .setLoopPingPong()
            .setDelay(Random.Range(0f, moveSpeed)); 
    }
}
