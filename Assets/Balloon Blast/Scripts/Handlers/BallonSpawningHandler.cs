using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class BallonSpawningHandler : NetworkBehaviour
{
    public GameObject balloonPrefab; 
    public int initialSpawnCount = 5; 
    public int maxBalloons = 20; 
    public float spawnInterval = 1f; 
    public bool simulateGuardian = true;

    private List<Vector3> guardianPoints; 
    private Queue<GameObject> balloonPool = new Queue<GameObject>(); 
    private int activeBalloons = 0; 

    public int poolCount = 0;

    public void StopSpawning()
    {
        StopAllCoroutines();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        if (IsHost)
        {
            guardianPoints = GetGuardianBounds();

            // Pre-instantiate balloons 
            for (int i = 0; i < maxBalloons; i++)
            {
                GameObject balloon = Instantiate(balloonPrefab);
                balloon.GetComponent<Balloon>().TypeIndex.Value = UnityEngine.Random.Range(0, 4);
                balloon.gameObject.name = "Balloon_" + i.ToString();
                balloonPool.Enqueue(balloon);
                balloon.SetActive(false);
            }

            Debug.Log(balloonPool.Count);
        }
    }

    private void Update()
    {
        poolCount = balloonPool.Count;
    }

    public void StartSpawning()
    {
       
        StartCoroutine(SpawnBalloonsGradually());
        StartCoroutine(SpawnBallonCountGradually());
    }

    int balloonsPerSpawn = 1; 
    int maxIncreaseRate = 3;  

    IEnumerator SpawnBallonCountGradually()
    {
        while (true)
        {
            yield return new WaitForSeconds(7.5f);
            ++balloonsPerSpawn;
            if (balloonsPerSpawn > maxIncreaseRate) balloonsPerSpawn = maxIncreaseRate;
        }
    }

    IEnumerator SpawnBalloonsGradually()
    {
        while (true)
        {
            for (int i = 0; i < balloonsPerSpawn; i++)
            {
                if (activeBalloons < maxBalloons)
                {
                    SpawnBalloon();
                    activeBalloons++;
                }
            }

            yield return new WaitForSeconds(spawnInterval); 
        }
    }

    void SpawnBalloon()
    {
        if (balloonPool.Count > 0)
        {
            //Debug.Log("Balloon spawned!");
            GameObject balloon = balloonPool.Dequeue();
            balloon.SetActive(true);
            Vector3 pose = GetRandomPointInsideBoundary();
            pose.y = UnityEngine.Random.Range(0,1.25f);
            balloon.transform.position = pose;
            StartCoroutine(ExecuteAfterFrame(2, () =>
            {
                balloon.GetComponent<NetworkObject>().Spawn();
            }));
        }
    }

    IEnumerator ExecuteAfterFrame(int frameCount,Action callback)
    {
        for(int i = 0; i< frameCount; ++i)
        {
            yield return new WaitForEndOfFrame();
        }
        callback?.Invoke();
    }

    public void RecycleBalloon(GameObject balloon)
    {
        balloon.GetComponent<NetworkObject>().Despawn(false);
        balloon.SetActive(false);
        balloonPool.Enqueue(balloon);
        activeBalloons--;
    }


    List<Vector3> GetGuardianBounds()
    {
        List<Vector3> boundaryPoints = new List<Vector3>();

        Debug.LogWarning("Using Simulated Guardian Bounds for Testing!");

        float width = 3.5f; 
        float height = 3.5f;

        Vector3 center = Vector3.zero; // Center at (0,0,0)
        boundaryPoints.Add(center + new Vector3(-width / 2, 0, -height / 2));
        boundaryPoints.Add(center + new Vector3(width / 2, 0, -height / 2));
        boundaryPoints.Add(center + new Vector3(width / 2, 0, height / 2));
        boundaryPoints.Add(center + new Vector3(-width / 2, 0, height / 2));

        return boundaryPoints;
    }

    Vector3 GetRandomPointInsideBoundary()
    {
        Vector3 min = guardianPoints[0];
        Vector3 max = guardianPoints[2];

        return new Vector3(
            UnityEngine.Random.Range(min.x, max.x),
            UnityEngine.Random.Range(1f, 2f), 
            UnityEngine.Random.Range(min.z, max.z)
        );
    }
}
