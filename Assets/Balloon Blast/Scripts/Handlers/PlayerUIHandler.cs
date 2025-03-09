using UnityEngine;

public class PlayerUIHandler : MonoBehaviour
{
    XRPlayer playerRef;

    [SerializeField] CanvasGroup gameplayCanvas;

    public void Init(XRPlayer player)
    {
        playerRef = player;

        // can do intial setup on spawn type
    }
}
