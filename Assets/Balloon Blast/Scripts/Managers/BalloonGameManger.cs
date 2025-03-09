using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using AYellowpaper.SerializedCollections;
using System.Linq;
using System.Collections;

public enum GameState
{
    NotInitialized,
    WaitingForPlayers,
    AllPlayersReady,
    GameStarted,
    GameFinished
}

[Serializable]
public struct PlayerData : INetworkSerializable, IEquatable<PlayerData>
{
    public FixedString64Bytes playerID;
    public bool isReady;
    public int score;
    public ulong clientId;
    public int playerIndex;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref playerID);
        serializer.SerializeValue(ref isReady);
        serializer.SerializeValue(ref score);
        serializer.SerializeValue(ref clientId);
        serializer.SerializeValue(ref playerIndex);
    }

    public bool Equals(PlayerData other)
    {
        return playerID.Equals(other.playerID) &&
               isReady == other.isReady &&
               score == other.score &&
               clientId == other.clientId &&
               playerIndex == other.playerIndex;
    }

    public override bool Equals(object obj)
    {
        return obj is PlayerData other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(playerID.GetHashCode(), isReady, score, clientId, playerIndex);
    }

}

/// <summary>
/// Manages the game state, player data, and network interactions for a co-located multiplayer mixed reality game.
/// Handles player registration, score updates, game state transitions, and synchronization using Unity Netcode.
/// Ensures proper game flow, including player readiness, game start, and restart functionality.
/// </summary>
public class BalloonGameManger : NetworkBehaviour
{
    NetworkList<PlayerData> players = new NetworkList<PlayerData>();
    GameUIManager gameUIManager;
    private NetworkVariable<float> GameTimer = new NetworkVariable<float>();
    private NetworkVariable<GameState> CurrentGameState = new NetworkVariable<GameState>(GameState.NotInitialized);

    public float GAME_TIME = 2;

    #region Lifecycle Methods 
    private void Start()
    {
        if (IsHost)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback += OnPlayerLeft;
        }
    }

    public override void OnNetworkSpawn()
    {
        NetworkManager.Singleton.NetworkTickSystem.Tick += OnNetworkTick;

        Debug.Log("OnNetworkSpawn CALLED!");

        gameUIManager = FindFirstObjectByType<GameUIManager>();
        if (gameUIManager != null)
        {
            gameUIManager.OnGameStateChanged(CurrentGameState.Value, CurrentGameState.Value);
        }
    }

    private void OnNetworkTick()
    {
        if (IsHost)
        {
            if (CurrentGameState.Value == GameState.GameStarted)
            {
                GameTimer.Value += Time.deltaTime;

                if (GameTimer.Value >= GAME_TIME)
                {
                    CurrentGameState.Value = GameState.GameFinished;
                }
            }
        }
    }

    public void OnRestart()
    {
        if (!IsHost) return;

        // game can only be restart when finished 
        if (CurrentGameState.Value != GameState.GameFinished) return;

        // reset all players score 
        for (int i = 0; i < players.Count; i++)
        {
            PlayerData updatedData = players[i];
            updatedData.score = 0;
            updatedData.isReady = false;
            players[i] = updatedData;
        }

        // reset timer 
        GameTimer.Value = 0;

        // reset to player wait state
        StartCoroutine(WaitAndExecute(0.5f, () =>
        {
            CurrentGameState.Value = GameState.WaitingForPlayers;
        }));
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        if (IsHost)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnPlayerLeft;
            NetworkManager.Singleton.NetworkTickSystem.Tick -= OnNetworkTick;
        }
    }
    #endregion

    #region Get Methods 
    public NetworkVariable<GameState> GetGameState() => CurrentGameState;
    public NetworkList<PlayerData> GetPlayerDataUpdate() => players;
    public NetworkVariable<float> GetGameTimer() => GameTimer;

    public int GetPlayerIndex(FixedString64Bytes playerID)
    {
        for (int i = 0; i < players.Count; i++)
        {
            if (players[i].playerID.Equals(playerID))
            {
                return players[i].playerIndex;
            }
        }
        return -1;
    }

    public List<PlayerData> GetAllPlayersData()
    {
        List<PlayerData> data = new();
        foreach (PlayerData item in players)
        {
            data.Add(item);
        }
        return data;
    }

    public PlayerData GetPlayerData(FixedString64Bytes playerID)
    {
        foreach (PlayerData playerData in players)
        {
            if (playerData.playerID.Equals(playerID)) return playerData;
        }
        return default;
    }
    #endregion

    #region Player Registration Methods
    public void RegisterPlayer(FixedString64Bytes playerID, ulong clientID)
    {
        int index = -1;

        // Manually search for the player in NetworkList
        for (int i = 0; i < players.Count; i++)
        {
            if (players[i].playerID.Equals(playerID))
            {
                index = i;
                break;
            }
        }

        if (index != -1)
        {
            // Player already exists, update clientId
            PlayerData existingPlayer = players[index];
            existingPlayer.clientId = clientID;
            players[index] = existingPlayer; // Properly update in NetworkList
        }
        else
        {
            Debug.Log("RegisterPlayerServerRpc: New Player");

            // New player joining
            PlayerData newPlayer = new PlayerData
            {
                playerID = playerID,
                isReady = false,
                playerIndex = players.Count + 1, // Assigning player number (1-based index)
                score = 0,
                clientId = clientID
            };

            players.Add(newPlayer);
            Debug.Log("Updated Player List count: " + players.Count);

            if (CurrentGameState.Value == GameState.NotInitialized)
                StartCoroutine(WaitAndExecute(0.5f, () =>
                {
                    CurrentGameState.Value = GameState.WaitingForPlayers;
                }));
        }

        Debug.Log("Updated Player List:");
        foreach (var player in players)
        {
            Debug.Log($"PlayerID: {player.playerID}, Score: {player.score}, Ready: {player.isReady}, PlayerIndex: {player.playerIndex}, Score: {player.score}");
        }

    }

    public void RemovePlayer(ulong clientID)
    {
        if (!IsHost) return;

        for (int i = 0; i < players.Count; i++)
        {
            if (players[i].clientId.Equals(clientID))
            {
                players.RemoveAt(i);
                break;
            }
        }
    }

    // called from network manager
    private void OnPlayerLeft(ulong clientId)
    {
        if (IsHost)
        {
            Debug.Log("On PLayer left found . . .");
            RemovePlayer(clientId);
        }
    }
    #endregion

    #region Player Set Methods
    [ServerRpc(RequireOwnership = false)]
    public void UpdateScoreServerRpc(FixedString64Bytes playerID, int points)
    {
        if (!IsHost) return;

        if (CurrentGameState.Value != GameState.GameStarted) return;

        for (int i = 0; i < players.Count; i++)
        {
            if (players[i].playerID.Equals(playerID))
            {
                PlayerData updatedData = players[i];
                updatedData.score += points;
                players[i] = updatedData; // Update the NetworkList entry
                break;
            }
        }
        // PlayerDataUpdate.Value = (ulong)Time.frameCount;
    }

    // for host 
    public void UpdateScore(FixedString64Bytes playerID, int points)
    {
        if (!IsHost) return;

        if (CurrentGameState.Value != GameState.GameStarted) return;

        for (int i = 0; i < players.Count; i++)
        {
            if (players[i].playerID.Equals(playerID))
            {
                PlayerData updatedData = players[i];
                updatedData.score += points;
                players[i] = updatedData; // Update the NetworkList entry
                break;
            }
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void SetPlayerReadyServerRpc(FixedString64Bytes playerID, bool isReady)
    {
        if (!IsHost) return;

        for (int i = 0; i < players.Count; i++)
        {
            if (players[i].playerID.Equals(playerID))
            {
                PlayerData updatedData = players[i];
                updatedData.isReady = isReady;
                players[i] = updatedData; // Update the NetworkList entry
                break;
            }
        }

        // PlayerDataUpdate.Value = (ulong)Time.frameCount;

        CheckAllPlayersReady();
    }
    #endregion

    #region utils
    IEnumerator WaitAndExecute(float delay, Action callback)
    {
        yield return new WaitForSeconds(delay);
        callback?.Invoke();
    }

    private void CheckAllPlayersReady()
    {
        foreach (var player in players)
        {
            if (!player.isReady) return;
        }

        CurrentGameState.Value = GameState.AllPlayersReady;
        StartCoroutine(WaitAndExecute(3f, () =>
        {
            // All players are ready, start the game
            CurrentGameState.Value = GameState.GameStarted;
        }));
    }
    #endregion


}
