using Oculus.Platform;
using Oculus.Platform.Models;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Assertions;
using NaughtyAttributes;
using System.Linq;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using System.Text;


[Serializable]
struct GroupMetaData
{
    public string DisplayName;
    public string IP;
    public string HostName;
}

/// <summary>
/// Handling Colocolation and Group sharing and intializating network connection
/// </summary>
public class XRConnectionHandler : MonoBehaviour
{
    public static XRConnectionHandler Instance;

    [SerializeField] NetworkManager networkManager;

    public Action OnAdvertisingFailed;
    public Action OnDiscoveringFailed;

    private OVRSpatialAnchor _syncedSptialAnchorRef;
    private Guid _activeGroupID;

    #region Lifecyle Methods
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        Core.Initialize();
        if (OVRManager.display != null)
        {
            OVRManager.display.RecenteredPose += OnRecentered;
        }
    }

    public void OnRecentered()
    {
        if (_syncedSptialAnchorRef != null)
        {
            InitiAllignment(_syncedSptialAnchorRef);
        }
    }

    private void OnDestroy()
    {
        Instance = null;
        OVRColocationSession.StopDiscoveryAsync();
        OVRColocationSession.StopAdvertisementAsync();
        OVRColocationSession.ColocationSessionDiscovered -= OnGroupSessionDiscovered;
        if (OVRManager.display != null)
            OVRManager.display.RecenteredPose += OnRecentered;
    }
    #endregion

    #region Host Setup

    public async void StartHostSetup()
    {
        Debug.Log($"[Colocation] {nameof(StartHostSetup)}:");

        // Get IP Information
        string hostName = System.Net.Dns.GetHostName();
        IPHostEntry ipEntry = System.Net.Dns.GetHostEntry(hostName);
        IPAddress[] ipAddress = ipEntry.AddressList;
        string ip = ipAddress[ipAddress.Length - 1].ToString();

        GroupMetaData groupMetaData = new();
        groupMetaData.DisplayName = "Dev_" + UnityEngine.Random.Range(11, 99).ToString();
        groupMetaData.IP = ip;
        groupMetaData.HostName = hostName;

        byte[] bytes = SerializeToByteArray<GroupMetaData>(groupMetaData);
        Debug.Log("Data: " + bytes.Length);

        // start advertising
        var advertisementResult = await StartAdvertisingAsync(bytes, groupMetaData);
        if (!advertisementResult)
        {
            Debug.LogError("[Colocation] Unable to StartAdvertisingAsync!");
            OnAdvertisingFailed?.Invoke();
            return;
        }

        // Create a new anchor 
        bool anchorCreateResult = await CreateAndShareSyncedAnchor();
        if (!advertisementResult)
        {
            Debug.LogError("[Colocation] Unable to CreateAndShareSyncedAnchor!");
            OnAdvertisingFailed?.Invoke();
            return;
        }

        // setup network layers
        UnityTransport transport = (UnityTransport)networkManager.NetworkConfig.NetworkTransport;
        transport.SetConnectionData(ip, 7777);
        bool hostInitializationResut = networkManager.StartHost();
        if (!hostInitializationResut)
        {
            Debug.LogError("[Colocation] Unable to StartHost!");
            OnAdvertisingFailed?.Invoke();
            return;
        }

        Debug.Log("[Colocation] HOST IS READY!");
    }

    async Task<bool> StartAdvertisingAsync(byte[] bytes, GroupMetaData groupMetaData)
    {
        var startAdvert = await OVRColocationSession.StartAdvertisementAsync(bytes);
        Debug.Log($"* {startAdvert.Status.ForLogging()} : {startAdvert.Success} ");
        if (!startAdvert.TryGetValue(out var guid))
        {
            return false;
        }
        Debug.Log($"+ DisplayName: \"{groupMetaData.DisplayName}\"");
        Debug.Log($"+ Uuid: {guid}");
        _activeGroupID = guid;
        return true;
    }

    private async Task<bool> CreateAndShareSyncedAnchor()
    {
        try
        {
            Debug.Log("[Colocation] creating anchor!");
            var anchor = await CreateAnchor(Vector3.zero, Quaternion.identity);
            if (anchor == null)
            {
                Debug.LogError("[Colocation] anchor creation failed!");
                return false;
            }
            if (!anchor.Localized)
            {
                Debug.LogError("[Colocation] localization failed not able to go futher!");
                return false;
            }

            var saveResult = await anchor.SaveAnchorAsync();
            if (!saveResult.Success)
            {
                Debug.LogError("[Colocation] SaveAnchorAsync failed not able to go futher!");
                return false;
            }

            Debug.Log("[Colocation] Anchor saved sucessfully: " + anchor.Uuid);

            var shareResult = await OVRSpatialAnchor.ShareAsync(new List<OVRSpatialAnchor> { anchor }, _activeGroupID);
            if (!shareResult.Success)
            {
                Debug.LogError("[Colocation]  ShareAsync to group : " + _activeGroupID.ToString() + " FAILD!");
                return false;
            }

            _syncedSptialAnchorRef = anchor;

            return true;
        }
        catch (Exception ex)
        {

            Debug.LogError("CreateAndShareSyncedAnchor failed!");
            Debug.LogError(ex.Message);
            return false;
        }
    }

    private async Task<OVRSpatialAnchor> CreateAnchor(Vector3 pPosistion, Quaternion pRotation)
    {
        try
        {
            var anchorGameObject = new GameObject("Shared Anchor")
            {
                transform =
                {
                    position = pPosistion,
                    rotation = pRotation
                }
            };

            var spatialAcnhor = anchorGameObject.AddComponent<OVRSpatialAnchor>();
            while (!spatialAcnhor.Created)
            {
                await Task.Yield();
            }

            Debug.Log("[Colocation] Synced Spatial Anchor created: " + spatialAcnhor.Uuid);
            return spatialAcnhor;

        }
        catch (Exception ex)
        {
            Debug.LogError("[Colocation] CreateAnchor failed!");
            Debug.LogError("[Colocation] " + ex.Message);
            return null;
        }
    }

    #endregion

    #region Client Setup

    public async void StartClientSetup()
    {
        //  start discovery
        OVRColocationSession.ColocationSessionDiscovered += OnGroupSessionDiscovered;

        var startDisco = await OVRColocationSession.StartDiscoveryAsync();
        Debug.Log($"* {startDisco.Status.ForLogging()}: {startDisco.Success} ");
        if (!startDisco.Success)
        {
            OnDiscoveringFailed?.Invoke();
            Debug.LogError("[Colocation] Unable to StartDiscoveringAsync!");
            return;
        }
        Debug.Log("[Colocation] StartDiscoveringAsync started .. . ! ");

    }

    private void OnGroupSessionDiscovered(OVRColocationSession.Data data)
    {
        OVRColocationSession.ColocationSessionDiscovered -= OnGroupSessionDiscovered;
        _activeGroupID = data.AdvertisementUuid;
        LoadAndAllignToAnchor();

        Debug.Log("Data: " + data.Metadata.Length);
        GroupMetaData groupMetaData = new();
        groupMetaData = DeserializeFromByteArray<GroupMetaData>(data.Metadata);

        Debug.Log($"[Colocation] DisplayName: \"{groupMetaData.DisplayName}\"");
        Debug.Log($"[Colocation] Host IP: \"{groupMetaData.IP}\"");

        // setup network layer
        UnityTransport transport = (UnityTransport)networkManager.NetworkConfig.NetworkTransport;
        transport.SetConnectionData(groupMetaData.IP, 7777);
        networkManager.StartClient();
    }

    private async void LoadAndAllignToAnchor()
    {
        try
        {
            Debug.Log("[Colocation] LoadAndAllignToAnchor started!");

            var unboundedAnchors = new List<OVRSpatialAnchor.UnboundAnchor>();
            var loadResult = await OVRSpatialAnchor.LoadUnboundSharedAnchorsAsync(_activeGroupID, unboundedAnchors);

            if (!loadResult.Success || unboundedAnchors.Count == 0)
            {
                Debug.LogError($"[Colocation] Failed at LoadAndAllignToAnchor Success:{loadResult.Success} Count:{unboundedAnchors.Count} ");
                return;
            }

            foreach (var unboundAnchor in unboundedAnchors)
            {
                if (await unboundAnchor.LocalizeAsync())
                {
                    Debug.Log("[Colocation] Anchor localization sucessfull! UUID: " + unboundAnchor.Uuid);
                    var anchorObject = new GameObject($"Anchor_{unboundAnchor.Uuid}");
                    var spatialAnchor = anchorObject.AddComponent<OVRSpatialAnchor>();
                    unboundAnchor.BindTo(spatialAnchor);
                    _syncedSptialAnchorRef = spatialAnchor;
                    InitiAllignment(_syncedSptialAnchorRef);
                    return;
                }

                Debug.LogError($"[Colocation] Failed to localize anchor UUID:{unboundAnchor.Uuid}");
            }

        }
        catch (Exception e)
        {
            Debug.LogError("[Colocation] LoadAndAllignToAnchor failed!");
        }
    }

    #endregion

    #region Utils 

    void InitiAllignment(OVRSpatialAnchor anchor)
    {
        if (!anchor || !anchor.Localized)
        {
            Debug.LogError("Not localized achor!");
            return;
        }
        StartCoroutine(AllignmentCoroutine(anchor));
    }

    IEnumerator AllignmentCoroutine(OVRSpatialAnchor anchor)
    {
        Transform _cameraRigTransform = FindAnyObjectByType<OVRCameraRig>().transform;
        var anchorTransform = anchor.transform;
        for (int i = 2; i > 0; i--)
        {
            _cameraRigTransform.position = Vector3.zero;
            _cameraRigTransform.eulerAngles = Vector3.zero;

            yield return null;

            _cameraRigTransform.position = anchorTransform.InverseTransformPoint(Vector3.zero);
            _cameraRigTransform.eulerAngles = new Vector3(0, -anchorTransform.eulerAngles.y, 0);
        }
    }

    public byte[] SerializeToByteArray<T>(T obj) where T : new()
    {
        var json = JsonUtility.ToJson(obj, prettyPrint: false) ?? "{}";
        Encoding EncodingForSerialization = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
        return EncodingForSerialization.GetBytes(json);
    }

    public T DeserializeFromByteArray<T>(byte[] bytes) where T : new()
    {
        Encoding EncodingForSerialization = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
        var json = EncodingForSerialization.GetString(bytes);
        return JsonUtility.FromJson<T>(json);
    }

    #endregion
}
