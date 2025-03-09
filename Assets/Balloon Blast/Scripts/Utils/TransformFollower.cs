using UnityEngine;

public class TransformFollower : MonoBehaviour
{
    [SerializeField] Transform toFollow;

    private void Update()
    {
        if (toFollow != null)
        {
            transform.SetPositionAndRotation(toFollow.position, toFollow.rotation);
        }

    }
}
