using UnityEngine;

[RequireComponent(typeof(Camera))]  
public class EditorVRCameraSetup : MonoBehaviour
{
    private void Awake()
    {
#if UNITY_EDITOR
        GetComponent<Camera>().clearFlags = CameraClearFlags.Skybox;
#endif
    }
}
