using System;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;
using NaughtyAttributes;
using Oculus.Interaction;
using Unity.Collections.LowLevel.Unsafe;

public enum SpaceState
{
    MR = 1,
    VR = 2
}

public class ExperiencSyncHandler : NetworkBehaviour
{
    [SerializeField] SpaceState currentSpaceState = SpaceState.MR;

    [SerializeField] public bool _initialized = false;

    public UnityEvent OnConnectedToNetwork;

    [SerializeField] GameObject room3D;

    NetworkVariable<int> currentSpaceStateSynced = new NetworkVariable<int>(1,NetworkVariableReadPermission.Everyone,NetworkVariableWritePermission.Server);

    private void Start()
    {
        currentSpaceStateSynced.OnValueChanged += OnSpaceStateValueChanged;
    }

    public override void OnNetworkSpawn()
    {
        if (!IsHost)
        {
            ChangeSpaceStateRemote((SpaceState)currentSpaceStateSynced.Value);
        }
    }

    private void OnSpaceStateValueChanged(int previousValue, int newValue)
    {
        Debug.Log("[NGO] called OnSpaceStateValueChanged");
        if (!IsHost)
        {
            Debug.Log("[NGO] OnSpaceStateValueChanged");
            ChangeSpaceStateRemote((SpaceState)newValue);
        }
    }

   

    private void Update()
    {
        if(!_initialized && NetworkManager.Singleton.IsClient)
        {
            _initialized = true;
            OnConnectedToNetwork?.Invoke();
        }
    }

    void ChangeSpaceStateRemote(SpaceState newValue)
    {
        switch ((SpaceState)newValue)
        {
            case SpaceState.MR:
                SetupMRMode();
                break;
            case SpaceState.VR:
                SetupVRMode();
                break;
        }
    }

    [ContextMenu("ToggleSpaceState")]
    public void ToggleSpaceState()
    {
        if (!_initialized) return;

        if (!IsHost) return;

        Debug.Log("[NGO] ToggleSpaceState");
        switch (currentSpaceState)
        {
            case SpaceState.MR:
                SetupVRMode();
                break;
            case SpaceState.VR:
                SetupMRMode();
                break;
        }

        currentSpaceStateSynced.Value = (int)currentSpaceState;
    }

    void SetupMRMode()
    {
        // switch to MR
        currentSpaceState = SpaceState.MR;
       // Camera.main.clearFlags = CameraClearFlags.SolidColor;
        room3D.SetActive(false);
        table.SetActive(true);
    }

    void SetupVRMode()
    {
        // switch to VR
        currentSpaceState = SpaceState.VR;
       // Camera.main.clearFlags = CameraClearFlags.Skybox;
        room3D.SetActive(true);
        table.SetActive(false);
    }

    [Space(10)]
    [SerializeField] CanvasGroup logs;
    [SerializeField] GameObject table;

    public void ToggleLogs()
    {
        logs.alpha = logs.alpha == 1 ? 0 : 1;
    }

    public void ToggleTable()
    {
        table.SetActive(!table.activeSelf);
    }
}
