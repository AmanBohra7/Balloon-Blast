using Oculus.Interaction.Surfaces;
using System;
using TMPro;
using Unity.Netcode;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using UnityEngine.UI;
using NUnit.Framework;
using System.Collections;
using Lean.Gui;
using System.Linq;

[System.Serializable]
public class UIPanel
{
    public CanvasGroup canvasGroup;
    public TextMeshProUGUI subHeading;

    public void Hide()
    {
        canvasGroup.alpha = 0;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.gameObject.SetActive(false);
    }

    public void Show(string text = "")
    {
        canvasGroup.alpha = 1;
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;
        canvasGroup.gameObject.SetActive(true);
        if(subHeading) subHeading.text = text;
    }
}

[System.Serializable]
public class ScorePanel
{
    public TextMeshProUGUI playerName;
    public TextMeshProUGUI playerScore;
    public GameObject panel;
}

public class GameUIManager : MonoBehaviour
{

    #region Private Memebers
    BalloonGameManger gameManger;
    Transform canvasParent;

    XRPlayer localPlayerRef;

    private int pinchCount = 3;
    private int pinchCounter = 0;
    private bool pinched = false;
    private bool _listeningForPinch = false;
    #endregion


    #region Public Memebrs
    [SerializeField] CanvasGroup canvasGroup;
    [SerializeField] PlaneSurface surface;

    [Space(10), Header("Panels")]
    [SerializeField] UIPanel startPanel;
    [SerializeField] UIPanel playerWaitPanel;
    [SerializeField] UIPanel gameplayPanel;
    [SerializeField] UIPanel scoreboardPanel;

    [Space(10), Header("Other References")]
    [SerializeField] TextMeshProUGUI userNameText_PlayerWaitPanel;
    [SerializeField] GameObject restartBtn;
    [SerializeField] GameObject startScreen;
    [SerializeField] GameObject selectionScreen;
    [SerializeField] Image pinchRing1;
    [SerializeField] Image pinchRing2;
    [SerializeField] Image pinchRing3;
    [SerializeField] TextMeshProUGUI playerCountText;
    [SerializeField] TextMeshProUGUI localPlayerScore;
    [SerializeField] GameObject gameConfirmationPanel;
    [SerializeField] GameObject gameWaitPanel;
    [SerializeField] GameObject JoiningBtns;
    [SerializeField] GameObject JoiningLoading;

    [Space(10), Header("Score Specific")]
    [SerializeField] ScorePanel scorePanel1;
    [SerializeField] ScorePanel scorePanel2;
    [SerializeField] ScorePanel scorePanel3;

    public Action<FixedString64Bytes> OnWinnerAnnounced;
    #endregion


    public void RegisterLocalPlayer(XRPlayer player)
    {
        localPlayerRef = player;
        Debug.Log("Updated Player List:");
        if(_currentState == GameState.WaitingForPlayers)
        {
            StartCoroutine(ListenForPinching());
        }
    }

    public void OnPressedStart()
    {
        startScreen.SetActive(false);
        selectionScreen.SetActive(true);
        startPanel.canvasGroup.interactable = true;
    }

    private void Start()
    {
        gameManger = FindFirstObjectByType<BalloonGameManger>();
        gameManger.GetGameState().OnValueChanged += OnGameStateChanged;
        gameManger.GetPlayerDataUpdate().OnListChanged += OnPlayersDataUpdated;
        gameManger.GetGameTimer().OnValueChanged += OnGameTimerValueUpdated;

        XRConnectionHandler.Instance.OnAdvertisingFailed += OnConnectionFailed;
        XRConnectionHandler.Instance.OnDiscoveringFailed += OnConnectionFailed;

        OnGameStateChanged(gameManger.GetGameState().Value, (gameManger.GetGameState().Value));
#if UNITY_EDITOR
        transform.position = Vector3.up * Camera.main.transform.position.y;
#endif
        canvasParent = transform.GetChild(0);
        canvasParent.GetChild(0).eulerAngles = Vector3.up * 180;

        canvasParent.gameObject.SetActive(false);
        StartCoroutine(WaitAndExecute(0.25f, () => canvasParent.gameObject.SetActive(true)));
    }

    private void Update()
    {
        if(canvasParent != null)
        {
            canvasParent.LookAt(Camera.main.transform);
            canvasParent.eulerAngles = Vector3.up * canvasParent.eulerAngles.y;
        }
    }

    private void OnGameTimerValueUpdated(float previousValue, float newValue)
    {

        int remainingTime = Mathf.Max(0, Mathf.RoundToInt(gameManger.GAME_TIME - newValue));
        gameplayPanel.subHeading.text = $"00:{remainingTime:D2}";

    }

    private void OnPlayersDataUpdated(NetworkListEvent<PlayerData> changeEvent)
    {
        userNameText_PlayerWaitPanel.text = "You are: Player " +
            gameManger.GetPlayerIndex(localPlayerRef.PlayerID);

        localPlayerScore.text = gameManger.GetPlayerData(localPlayerRef.PlayerID).score.ToString();

        playerCountText.text = gameManger.GetAllPlayersData().Count + " Joined";
    }


    private void OnDestroy()
    {
        if (gameManger != null)
        {
            gameManger.GetGameState().OnValueChanged -= OnGameStateChanged;
            gameManger.GetPlayerDataUpdate().OnListChanged -= OnPlayersDataUpdated;
            gameManger.GetGameTimer().OnValueChanged -= OnGameTimerValueUpdated;
        }
       

        XRConnectionHandler.Instance.OnAdvertisingFailed -= OnConnectionFailed;
        XRConnectionHandler.Instance.OnDiscoveringFailed -= OnConnectionFailed;
    }

    private void OnConnectionFailed()
    {
        JoiningBtns.SetActive(true);
        JoiningLoading.SetActive(false);
        startPanel.canvasGroup.interactable = true;
    }

    private GameState _currentState = GameState.GameFinished;
    public void OnGameStateChanged(GameState previousValue, GameState newValue)
    {
        if (_currentState == newValue) return;
        _currentState = newValue;

        HideAllPanels();
        surface.enabled = newValue is GameState.NotInitialized or GameState.GameFinished;

        switch (newValue)
        {
            case GameState.NotInitialized:
                startScreen.SetActive(true);
                selectionScreen.SetActive(false);
                JoiningBtns.SetActive(true);
                JoiningLoading.SetActive(false);
                startPanel.Show();
               // if(localPlayerRef) localPlayerRef.DisableHandItems(); 
                break;

            case GameState.WaitingForPlayers:
                _listeningForPinch = false;
                playerWaitPanel.Show();
                gameConfirmationPanel.SetActive(false);
                gameWaitPanel.SetActive(false);
                if (localPlayerRef != null) StartCoroutine(ListenForPinching());

                break;

            case GameState.AllPlayersReady:
                playerWaitPanel.Show();
                break;

            case GameState.GameStarted:
                gameplayPanel.Show();
                break;

            case GameState.GameFinished:
                ShowWinners();
                restartBtn.SetActive(NetworkManager.Singleton.IsHost);
                scoreboardPanel.Show();
                break;
        }
    }

   

    // for local player
    private IEnumerator ListenForPinching()
    {
        if (_listeningForPinch) yield break;
        _listeningForPinch = true;

        pinchCounter = 0;

        pinchRing1.fillAmount = pinchRing2.fillAmount = pinchRing3.fillAmount = 0.68f;

        gameConfirmationPanel.SetActive(true);

        Debug.Log("Listening for index: " + localPlayerRef.PlayerIndex);
        while (pinchCounter < pinchCount) 
        {
            bool isPinching = localPlayerRef.IsPinching;

            if (!pinched && isPinching)
            {
                pinched = true;
                pinchCounter++;
                UpdateWaitingStateVisuals();

                if (pinchCounter == pinchCount)
                {
                    OnPlayerReady();
                    yield break;  
                }
            }
            else if (pinched && !isPinching)
            {
                pinched = false;
            }

            yield return new WaitForSeconds(0.1f);
        }
    }


    // this will update based on pinch counter
    void UpdateWaitingStateVisuals()
    {
        if(pinchCounter == 1)
            LeanTween.value(0.68f, 1, 0.35f).setOnUpdate((float value) => { pinchRing1.fillAmount = value;});
        if(pinchCounter == 2)
            LeanTween.value(0.68f, 1, 0.35f).setOnUpdate((float value) => { pinchRing2.fillAmount = value; });
        if(pinchCounter == 3)
            LeanTween.value(0.68f, 1, 0.35f).setOnUpdate((float value) => { pinchRing3.fillAmount = value; });
    }


    void ShowWinners()
    {
        List<PlayerData> players = gameManger.GetAllPlayersData()
            .OrderByDescending(p => p.score)
            .ToList();

        ScorePanel[] scorePanels = { scorePanel1, scorePanel2, scorePanel3 };

        for (int i = 0; i < scorePanels.Length; i++)
        {
            bool hasPlayer = i < players.Count;
            scorePanels[i].panel.SetActive(hasPlayer);

            if (hasPlayer)
            {
                var player = players[i];
                scorePanels[i].playerName.text = $"Player {player.playerIndex}" +
                    (player.playerID == localPlayerRef.PlayerID ? " (You)" : "");
                scorePanels[i].playerScore.text = $"<b>{player.score}</b> Balloons";
            }
        }

        OnWinnerAnnounced?.Invoke(players[0].playerID);
    }

    public void OnStartGame()
    {
        
#if UNITY_EDITOR
        NetworkManager.Singleton.StartHost();
#else
        XRConnectionHandler.Instance.StartHostSetup();
#endif
        startPanel.canvasGroup.interactable = false;
        StartCoroutine(WaitAndExecute(0.25f, () =>
        {
            JoiningBtns.SetActive(false);
            JoiningLoading.SetActive(true);
        }));
    }

    public void OnJoinGame()
    {
#if UNITY_EDITOR
        NetworkManager.Singleton.StartClient();
#else
        XRConnectionHandler.Instance.StartClientSetup();
#endif
        startPanel.canvasGroup.interactable = false;
        StartCoroutine(WaitAndExecute(0.25f, () =>
        {
            JoiningBtns.SetActive(false);
            JoiningLoading.SetActive(true);
        }));
    }



    void HideAllPanels()
    {
        startPanel.Hide();
        playerWaitPanel.Hide();
        gameplayPanel.Hide();
        scoreboardPanel.Hide();
    }

    public void OnPlayerReady()
    {
        StartCoroutine(WaitAndExecute(0.45f, () =>
        {
            gameConfirmationPanel.SetActive(false);
            gameWaitPanel.SetActive(true);
        }));
        gameManger.SetPlayerReadyServerRpc(localPlayerRef.PlayerID,true);
        playerWaitPanel.canvasGroup.interactable = false;
    }

    public void OnRestartPressed()
    {
        scoreboardPanel.canvasGroup.interactable = false;
        gameManger.OnRestart();
    }

    IEnumerator WaitAndExecute(float delay,Action callback)
    {
        yield return new WaitForSeconds(delay);
        callback?.Invoke();
    }
}
