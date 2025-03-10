using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;


public class Balloon : NetworkBehaviour
{

    [SerializeField] AudioClip blastClip;
    [SerializeField] List<ParticleSystem> blastParticles;
    [SerializeField] List<GameObject> balloonTypes;
    [SerializeField] Transform particlePosition;
    [SerializeField] LineRenderer lineRenderer;

    public NetworkVariable<int> TypeIndex = new NetworkVariable<int>();

    public float forceMagnitude = 10f; // Adjust the force strength

    BalloonGameManger gameManger;
    bool blasted = false;

    private void Start()
    {
        gameManger = FindFirstObjectByType<BalloonGameManger>();
        blasted = false;
    }

    private void OnEnable()
    {
        blasted = false;
        lineRenderer.enabled = false;
        StartCoroutine(ExecuteAfterFrame(20, () =>
        {
            lineRenderer.enabled = true;
        }));
    }

    IEnumerator ExecuteAfterFrame(int frameCount, Action callback)
    {
        for (int i = 0; i < frameCount; ++i)
        {
            yield return new WaitForEndOfFrame();
        }
        callback?.Invoke();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        TypeIndex.OnValueChanged += OnTypeIndexValueChanged;

        int i = 0;
        foreach (var item in balloonTypes)
        {
            item.SetActive(i == TypeIndex.Value);
            ++i;
        }

        blasted = false;
        GetComponent<BalloonFloating>().enabled = IsHost;
    }

    private void OnTypeIndexValueChanged(int previousValue, int newValue)
    {
        int i = 0;
        foreach (var item in balloonTypes)
        {
            item.SetActive(i == newValue);
            ++i;
        }
    }

    public void Pop(FixedString64Bytes hitByPlayerID)
    {
        if (blasted) return;

        blasted = true;

        if (hitByPlayerID.Length != 0) {
            if (IsHost)
            {
                gameManger.UpdateScore(hitByPlayerID,1);
            }
            else
            {
                gameManger.UpdateScoreServerRpc(hitByPlayerID, 1);
            }
        }
        
        if (IsHost)
        {
            FindFirstObjectByType<BallonSpawningHandler>().RecycleBalloon(gameObject);
        }
        else
        {
            RequestRecyleServerRpc();
        }
    }

    public override void OnNetworkDespawn()
    {
        if (!IsHost) Destroy(gameObject);
    }

    [ServerRpc(RequireOwnership = false)]
    void RequestRecyleServerRpc()
    {
        FindFirstObjectByType<BallonSpawningHandler>().RecycleBalloon(gameObject);
        NetworkObject.Despawn(false);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("PinEdge"))
        {
            XRPlayer playerRef = collision.gameObject.GetComponent<PlayerReferenceHelper>().playerReference;
            if (playerRef != null)
            {
                // push 
                if (collision.gameObject.GetComponent<PushItem>())
                {
                    Vector3 collisionNormal = collision.contacts[0].normal;
                    if (NetworkManager.Singleton.IsHost) PushItem(collisionNormal);
                    else PushBalloonServerRpc(collisionNormal);
                }
                else
                {
                    // blast 
                    if (NetworkManager.Singleton.IsHost) ExecuteClientRpc();
                    else ExecuteBlastEffectServerRpc();
                    Pop(playerRef.PlayerID);
                }
            }
        }
    }

    // push 
    [ServerRpc]
    void PushBalloonServerRpc(Vector3 collisionNormal)
    {
        PushItem(collisionNormal);
    }

    void PushItem(Vector3 collisionNormal)
    {
        Rigidbody rb = GetComponent<Rigidbody>();
        rb.AddForce(-collisionNormal * forceMagnitude, ForceMode.Impulse);
    }

    [ServerRpc]
    void ExecuteBlastEffectServerRpc()
    {
        ExecuteClientRpc();
    }

    [ClientRpc]
    void ExecuteClientRpc()
    {
        //Debug.Log("ExecuteClientRpc");
        AudioSource.PlayClipAtPoint(blastClip, particlePosition.position,0.55f);
        Instantiate(blastParticles[TypeIndex.Value], particlePosition.position, Quaternion.identity,null);
    }

    void Update()
    {
        if(IsHost)
        if (transform.position.y >= 2.8f) Pop("");
    }
}
