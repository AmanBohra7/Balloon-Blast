using UnityEngine;

public class AssignLineRendererPosiitions : MonoBehaviour
{
    [SerializeField] LineRenderer lineRenderer;

    //[ContextMenu("UpdatePositions")]
    //void UpdatePositions()
    //{
    //    lineRenderer.positionCount = transform.childCount;
    //    int i = 0;
    //    foreach (Transform child in transform)
    //    {
    //        lineRenderer.SetPosition(i, child.position);
    //        ++i;
    //    }
    //}

    [ContextMenu("UpdatePositionsRecursive")]
    private void UpdatePositionsRecursive()
    {
        int level = 13;
        int count = 0;
        foreach (Transform child in transform.GetComponentsInChildren<Transform>())
        {
            if (count == level)
            {
                break;
            }

            lineRenderer.SetPosition(count, child.position);
            count++;
        }
    }

    

    private void FixedUpdate()
    {
        UpdatePositionsRecursive();
    }
}
