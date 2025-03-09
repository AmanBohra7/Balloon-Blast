using Oculus.Interaction.Input;
using System;
using System.Collections;
using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public class XRPlayer
    : NetworkBehaviour
{
    private BalloonGameManger gameManager;

    [Header("Visuals")]
    [SerializeField] Transform headVisuals;
    [SerializeField] Transform leftHandVisuals;
    [SerializeField] Transform rightHandVisuals;

    [Space(10)]
    public HandItemHandler handItemHandler;

    bool _initialized = false;

    [Space(10), Header("References")]
    [SerializeField] TextMeshProUGUI nameText;
    [SerializeField] TextMeshProUGUI scoreText;

    [Space(10)]
    [SerializeField] GameObject confettie;

    private Transform centerEye;

    [Space(10)]
    [SerializeField] Vector3 offset;

    OVRCameraRig cameraRig;

    public PlayerData MyData { get; private set; }

    public int PlayerIndex { get; private set; }

    private NetworkVariable<FixedString64Bytes> SyncedPlayerID = new NetworkVariable<FixedString64Bytes>("", NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);


    public FixedString64Bytes PlayerID => SyncedPlayerID.Value;

    Hand playerHandRef;

    public bool IsPinching;

    NetworkVariable<int> SyncedItemIndex = new NetworkVariable<int>(-1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    public override void OnNetworkSpawn()
    {
        FindFirstObjectByType<GameUIManager>().OnWinnerAnnounced += OnWinnedAnnounced;


        //HandItemEnabled.OnValueChanged += OnHandItemEnabledValueChanged;

        base.OnNetworkSpawn();
        gameManager = FindFirstObjectByType<BalloonGameManger>();
        gameManager.GetGameState().OnValueChanged += OnGameStateChanged;
        gameManager.GetPlayerDataUpdate().OnListChanged += OnPlayerDataUpdated;

        NetworkManager.Singleton.NetworkTickSystem.Tick += OnNetworkTick;

        SyncedItemIndex.OnValueChanged += OnSyncedItemValueChanged;

        if (IsOwner)
        {
            if (Application.isEditor || Debug.isDebugBuild)
            {
                // Use a random ID per instance (useful for local testing)
                SyncedPlayerID.Value = Guid.NewGuid().ToString();
            }
            else
            {
                // Persistent ID for real multiplayer (across different devices)
                if (!PlayerPrefs.HasKey("PlayerID"))
                {
                    PlayerPrefs.SetString("PlayerID", Guid.NewGuid().ToString());
                    PlayerPrefs.Save();
                }
                SyncedPlayerID.Value = PlayerPrefs.GetString("PlayerID");
            }

            Debug.Log($"Assigned PlayerID: {SyncedPlayerID}");


            // Register localplayer in GameUIHandler
            GameUIManager gameUIManager = FindFirstObjectByType<GameUIManager>();
            if (gameUIManager != null)
            {
                gameUIManager.RegisterLocalPlayer(this);
            }

            // Register localplayer in GameplayHandler
            GameplayHandler gameplayHandler = FindFirstObjectByType<GameplayHandler>();
            if (gameplayHandler != null)
            {
                gameplayHandler.RegisterLocalPlayer(this);
                playerHandRef = gameplayHandler.GetHandReference();
                Debug.Log("Got playerHandRef: " + playerHandRef.name);
            }

            // Inform server about this player ID
            StartCoroutine(WaitAndExecute(0.5f, () =>
            {
                if (!IsHost) RegisterPlayerServerRpc(SyncedPlayerID.Value, OwnerClientId);
                else gameManager.RegisterPlayer(SyncedPlayerID.Value, OwnerClientId);
            }));

        }


        cameraRig = FindFirstObjectByType<OVRCameraRig>();

        centerEye = Camera.main.transform;

        if (IsOwner)
        {
            transform.position = Vector3.zero;
            transform.rotation = Quaternion.identity;
            _initialized = true;
            nameText.text = "Player " + (NetworkManager.Singleton.LocalClientId + 1).ToString();

            headVisuals.GetChild(0).gameObject.SetActive(false);
            leftHandVisuals.GetChild(0).gameObject.SetActive(false);
            rightHandVisuals.GetChild(0).gameObject.SetActive(false);
        }
        else
        {
            nameText.text = "Player " + (OwnerClientId + 1).ToString();
            leftHandVisuals.GetChild(0).gameObject.SetActive(false);
            rightHandVisuals.GetChild(0).gameObject.SetActive(false);
        }

    }


    void OnWinnedAnnounced(FixedString64Bytes id)
    {
        if (id == MyData.playerID)
        {
            if (centerEye != null)
            Instantiate(confettie, centerEye.position + (Vector3.one * 0.2f), Quaternion.identity);
        }
    }

    private void OnSyncedItemValueChanged(int previousValue, int newValue)
    {
        if (!IsOwner)
        {
            handItemHandler.UpdateItem(newValue);
        }
    }


    private void OnNetworkTick()
    {
        if (IsOwner) IsPinching = (playerHandRef.GetIndexFingerIsPinching() || Input.GetKey(KeyCode.P));

       
        bool CanUseHandItem = gameManager.GetGameState().Value == GameState.WaitingForPlayers || gameManager.GetGameState().Value == GameState.AllPlayersReady ||
            gameManager.GetGameState().Value == GameState.GameStarted;

        if (IsOwner && CanUseHandItem && playerHandRef)
        {
            
            Pose fingeTip;
            playerHandRef.GetJointPoseFromWrist(HandJointId.HandIndexTip, out fingeTip);

            handItemHandler.UpdatePose(fingeTip);

            if (playerHandRef.GetIndexFingerIsPinching() || Input.GetKey(KeyCode.S))
            {
                if (SyncedItemIndex.Value != handItemHandler.ShowItem())
                {
                    Debug.Log("testeateata");
                    SyncedItemIndex.Value = handItemHandler.ShowItem();
                }
            }
            else
            {
                handItemHandler.hideItem();
                if (SyncedItemIndex.Value != -1) SyncedItemIndex.Value = -1;
            }
        } 

        if(IsOwner && !CanUseHandItem) 
            if(SyncedItemIndex.Value != -1) SyncedItemIndex.Value = -1;


        if (_initialized && IsOwner)
        {
            headVisuals.transform.position =
                centerEye.position + offset;

            leftHandVisuals.position = cameraRig.leftControllerAnchor.position;
            leftHandVisuals.rotation = cameraRig.leftControllerAnchor.rotation;

            rightHandVisuals.position = cameraRig.rightControllerAnchor.position;
            rightHandVisuals.rotation = cameraRig.rightControllerAnchor.rotation;

            if (Input.GetKeyDown(KeyCode.Space))
            {
                if (IsHost)
                {
                    gameManager.UpdateScoreServerRpc(PlayerID, 1);
                }
                else
                {
                    UpdatePlayerScoreServerRpc(PlayerID, 1);
                }
            }
        }

        if (!IsOwner)
        {
            if (headVisuals != null)
            {
                headVisuals.transform.LookAt(Camera.main.transform);
                headVisuals.eulerAngles = Vector3.up * headVisuals.eulerAngles.y;
            }

        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void RegisterPlayerServerRpc(FixedString64Bytes id,ulong clientID)
    {
        gameManager.RegisterPlayer(id, clientID);
    }

    [ServerRpc(RequireOwnership = false)]
    public void UpdatePlayerScoreServerRpc(FixedString64Bytes id,int score)
    {
        gameManager.UpdateScoreServerRpc(id,score);
    }

    private void OnPlayerDataUpdated(NetworkListEvent<PlayerData> changeEvent)
    {
        if(changeEvent.Value.playerID == SyncedPlayerID.Value)
        {
            MyData = changeEvent.Value;
            scoreText.text = MyData.score.ToString();
            gameObject.name = "Player " + MyData.playerIndex;
        }
        
    }


    IEnumerator WaitAndExecute(float delay,Action callback)
    {
        yield return new WaitForSeconds(delay);
        callback?.Invoke(); 
    }


    public override void OnDestroy()
    {
        base.OnDestroy();
        if (IsOwner && gameManager != null)
        {
            gameManager.GetGameState().OnValueChanged -= OnGameStateChanged;  
            gameManager.GetPlayerDataUpdate().OnListChanged += OnPlayerDataUpdated;
            SyncedItemIndex.OnValueChanged -= OnSyncedItemValueChanged;
          //  HandItemEnabled.OnValueChanged -= OnHandItemEnabledValueChanged;
        }
    }

    private void OnGameStateChanged(GameState previousValue, GameState newValue)
    {
        switch (newValue)
        {
            case GameState.NotInitialized:
                break;
            case GameState.WaitingForPlayers:
                break;
            case GameState.AllPlayersReady:
                break;
            case GameState.GameStarted:
                break;
        }
    }

}
