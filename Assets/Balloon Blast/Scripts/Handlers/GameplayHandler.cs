using Oculus.Interaction.Input;
using System;
using Unity.Netcode;
using UnityEngine;

public class GameplayHandler : MonoBehaviour
{
    BalloonGameManger gameManager;

    [SerializeField] BallonSpawningHandler balloonSpawner;

    [SerializeField] Hand rightHandRef;

    XRPlayer localPlayerRef;

    public void RegisterLocalPlayer(XRPlayer player)
    {
        localPlayerRef = player;
    }

    public Hand GetHandReference() => rightHandRef;


    private void Start()
    {
        gameManager = FindFirstObjectByType<BalloonGameManger>();
        gameManager.GetGameState().OnValueChanged += OnGameStateChanged;
    }

    private void OnGameStateChanged(GameState previousValue, GameState newValue)
    {
        Debug.Log("OnGameStateChanged in GameplayHandler " + newValue);

        switch (newValue)
        {
            case GameState.NotInitialized:
                if (NetworkManager.Singleton.IsHost) balloonSpawner.StopSpawning();
                break;
            case GameState.WaitingForPlayers:
                break;
            case GameState.AllPlayersReady:
                break;
            case GameState.GameStarted:
                if(NetworkManager.Singleton.IsHost) balloonSpawner.StartSpawning();
                break;
            case GameState.GameFinished:
                if (NetworkManager.Singleton.IsHost) balloonSpawner.StopSpawning();
                break;
        }
    }

    private void OnDestroy()
    {
        gameManager.GetGameState().OnValueChanged -= OnGameStateChanged;
    }
}
