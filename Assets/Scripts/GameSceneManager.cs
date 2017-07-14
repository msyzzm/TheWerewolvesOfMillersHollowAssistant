using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Events;
using UnityEngine.UI;
using System;
using System.Threading;

public class GameSceneManager : NetworkBehaviour
{
    public InputField werewolvesNumberInputField;
    public InputField ordinaryTownsfolkNumberInputField;
    public Toggle fortuneTellerToggle;
    public Toggle witchToggle;
    public Button gameStartButton;
    public Button finishPhaseButton;
    public Button giveupEatButton;
    public Button giveupEliminateButton;
    public Text logText;

    public List<GameObject> playerGameObjects;//全部玩家
    public GameObject localPlayerGameObject;//本地玩家

    public bool isFirstDay;//是否为第一天
    public List<GameObject> werewolvesPlayerGameObjects;//狼人玩家
    public List<GameObject> ordinaryTownsfolkPlayerGameObjects;//普村玩家
    public GameObject fortuneTellerPlayerGameObject;//预言家玩家
    public GameObject witchPlayerGameObject;//女巫玩家
    public List<GameObject> toBeEatenPlayerGameObjects;//当天狼人想咬的玩家集合
    public List<GameObject> toBeEliminatedPlayerGameObjects;//当天被票的玩家集合
    [SyncVar]
    public GameObject eatenPlayerGameObject;//当天被咬的玩家
    public GameObject eliminatePlayerGameObject;//当天被放逐的玩家
    public GameObject witchEliminatePlayerGameObject;//当天被女巫毒的玩家
    public int cmdEatPlayerCount;//当天狼人想要投票计数
    public int cmdEliminatePlayerCount;//当天玩家投票计数
    public int finishTalkingPlayersNumber;//当天结束发言的玩家计数
    
    public GamePhase currentGamePhase;//当前游戏阶段

    void Start()
    {
        Debug.Log("GameSceneManager" + " Start!");

        finishPhaseButton.onClick.AddListener(OnFinishPhaseButtonClicked);
        giveupEatButton.onClick.AddListener(OnGiveupEatButton);
        giveupEliminateButton.onClick.AddListener(OnGiveupEliminateButtonClicked);
        if (isServer)
        {
            GameObject[] gs = GameObject.FindGameObjectsWithTag("Player");
            foreach (GameObject p in gs)
            {
                p.GetComponent<Player>().gameSceneManager = this;
                if (!playerGameObjects.Contains(p))
                {
                    playerGameObjects.Add(p);
                }
                if (p.GetComponent<Player>().hasAuthority)
                {
                    localPlayerGameObject = p;
                }
            }
            werewolvesNumberInputField.onEndEdit.AddListener(OnWerewolvesNumberInputFieldTextChanged);
            ordinaryTownsfolkNumberInputField.onEndEdit.AddListener(OnOrdinaryTownsfolkNumberInputFieldTextChanged);
            fortuneTellerToggle.onValueChanged.AddListener(OnFortuneTellerToggleChanged);
            witchToggle.onValueChanged.AddListener(OnWitchToggleChanged);
            gameStartButton.onClick.AddListener(OnGameStartButtonClicked);
            StartCoroutine(ChangeToNextPhase(2));
        }
        else
        {
            SetGameSettingInteractable(false);
        }
    }

    public IEnumerator ChangeToNextPhase(float time)
    {
        yield return new WaitForSeconds(time);
        if(currentGamePhase == GamePhase.Loaded)
        {
            RpcChangeCurrentGamePhase(GamePhase.GameSetting);
        }
        if (currentGamePhase == GamePhase.GameSetting)
        {
            RpcChangeCurrentGamePhase(GamePhase.WerewolvesEat);
        }
        else if (currentGamePhase == GamePhase.WerewolvesEat)
        {
            if (fortuneTellerToggle.isOn)
            {
                RpcChangeCurrentGamePhase(GamePhase.FortuneTellerDiscover);
            }
            else if (witchToggle.isOn)
            {
                RpcChangeCurrentGamePhase(GamePhase.WitchUsePotion);
            }
            else
            {
                ChangeToDiscussOrGameSettingPhase();
            }
        }
        else if (currentGamePhase == GamePhase.FortuneTellerDiscover)
        {
            if (witchToggle.isOn)
            {
                RpcChangeCurrentGamePhase(GamePhase.WitchUsePotion);
            }
            else
            {
                ChangeToDiscussOrGameSettingPhase();
            }
        }
        else if (currentGamePhase == GamePhase.WitchUsePotion)
        {
            ChangeToDiscussOrGameSettingPhase();
        }
        else if (currentGamePhase == GamePhase.AllPlayersDiscuss)
        {
            RpcChangeCurrentGamePhase(GamePhase.AllPlayersEliminate);
        }
        else if (currentGamePhase == GamePhase.AllPlayersEliminate)
        {
            if (JudgeGameOver())
            {
                RpcChangeCurrentGamePhase(GamePhase.GameSetting);
            }
            else
            {
                ClearTheDayData();
                RpcChangeCurrentGamePhase(GamePhase.WerewolvesEat);
            }
        }
    }

    [Server]
    void ChangeToDiscussOrGameSettingPhase()
    {
        if (eatenPlayerGameObject != null)
        {
            eatenPlayerGameObject.GetComponent<Player>().alive = false;
            RpcUpdateLogText(eatenPlayerGameObject.GetComponent<Player>().playerName + "死了\n", PlayerIdentity.IsNotAllocated);
        }
        if (witchEliminatePlayerGameObject != null)
        {
            witchEliminatePlayerGameObject.GetComponent<Player>().alive = false;
            RpcUpdateLogText(witchEliminatePlayerGameObject.GetComponent<Player>().playerName + "死了\n", PlayerIdentity.IsNotAllocated);
        }
        if (JudgeGameOver())
        {
            RpcChangeCurrentGamePhase(GamePhase.GameSetting); ;
        }
        else
        {
            RpcChangeCurrentGamePhase(GamePhase.AllPlayersDiscuss);
        }
    }

    public IEnumerator WaitOneSecond()
    {
        yield return new WaitForSeconds(1f);
    }

    [ClientRpc]
    void RpcChangeCurrentGamePhase(GamePhase newGamePhase)
    {
        currentGamePhase = newGamePhase;
        ResetPhaseUI();
        if (newGamePhase == GamePhase.GameSetting)
        {
            UpdateLogText("房主设置身份。\n");
            isFirstDay = true;
            werewolvesPlayerGameObjects.Clear();//狼人玩家
            ordinaryTownsfolkPlayerGameObjects.Clear();//普村玩家
            fortuneTellerPlayerGameObject = null;//预言家玩家
            witchPlayerGameObject = null;//女巫玩家
            foreach (GameObject p in playerGameObjects)
            {
                p.GetComponent<Player>().playerIdentity = PlayerIdentity.IsNotAllocated;
                p.GetComponent<Player>().savePotionNum = 0;
                p.GetComponent<Player>().eliminatePotionNum = 0;
            }
            if (isServer)
            {
                SetGameSettingInteractable(true);
            }
            else
            {
                SetGameSettingInteractable(false);
            }
        }
        else if (newGamePhase == GamePhase.WerewolvesEat)
        {
            if (isFirstDay)
            {
                if (localPlayerGameObject.GetComponent<Player>().playerIdentity == PlayerIdentity.Werewolves)
                {
                    foreach (GameObject g in playerGameObjects)
                    {
                        if(g.GetComponent<Player>().playerIdentity == PlayerIdentity.Werewolves)
                        {
                            g.GetComponent<Player>().playerIdentityDropdown.value = 1;
                        }
                    }
                }
            }
            UpdateLogText("狼人行动。\n");
            SetEatPlayerButtonActive(true);
        }
        else if (newGamePhase == GamePhase.FortuneTellerDiscover)
        {
            UpdateLogText("预言家行动。\n");
            SetDiscoverPlayerButtonActive(true);
            if (!localPlayerGameObject.GetComponent<Player>().alive && (localPlayerGameObject.GetComponent<Player>().playerIdentity == PlayerIdentity.FortuneTeller))
            {
                localPlayerGameObject.GetComponent<Player>().CmdChangeToNextPhase(5f);
            }
        }
        else if (newGamePhase == GamePhase.WitchUsePotion)
        {
            UpdateLogText("女巫行动。\n");
            SetWitchButtonActive(true);
            if (!localPlayerGameObject.GetComponent<Player>().alive && (localPlayerGameObject.GetComponent<Player>().playerIdentity == PlayerIdentity.Witch))
            {
                localPlayerGameObject.GetComponent<Player>().CmdChangeToNextPhase(5f);
            }
        }
        else if (newGamePhase == GamePhase.AllPlayersDiscuss)
        {
            UpdateLogText("玩家讨论。\n");
            SetFinishTalkingButtonActive(true);
        }
        else if (newGamePhase == GamePhase.AllPlayersEliminate)
        {
            UpdateLogText("玩家投票。\n");
            SetEliminatePlayerButtonActive(true);
        }
    }

    // listen to WerewolvesNumberInputField
    void OnWerewolvesNumberInputFieldTextChanged(string value)
    {
        RpcWerewolvesNumberInputFieldTextChanged(value);
    }

    // synchronize WerewolvesNumberInputField
    [ClientRpc]
    void RpcWerewolvesNumberInputFieldTextChanged(string value)
    {
        werewolvesNumberInputField.text = value;
    }

    // listen to OrdinaryTownsfolkNumberInputField
    void OnOrdinaryTownsfolkNumberInputFieldTextChanged(string value)
    {
        RpcOrdinaryTownsfolkNumberInputFieldTextChanged(value);
    }

    // synchronize OrdinaryTownsfolkNumberInputField
    [ClientRpc]
    void RpcOrdinaryTownsfolkNumberInputFieldTextChanged(string value)
    {
        ordinaryTownsfolkNumberInputField.text = value;
    }

    void OnFortuneTellerToggleChanged(bool value)
    {
        RpcFortuneTellerToggleChanged(value);
    }

    [ClientRpc]
    void RpcFortuneTellerToggleChanged(bool value)
    {
        fortuneTellerToggle.isOn = value;
    }

    void OnWitchToggleChanged(bool value)
    {
        RpcWitchToggleChanged(value);
    }

    [ClientRpc]
    void RpcWitchToggleChanged(bool value)
    {
        witchToggle.isOn = value;
    }

    void OnGameStartButtonClicked()
    {
        if (playerGameObjects.Count == CountIdentityNumber())
        {
            if (werewolvesNumberInputField.text == "0")
            {
                UpdateLogText("狼人数量不能为0。\n");
            }
            else
            {
                SetGameSettingInteractable(false);
                PlayerIdentityAllocate();
                StartCoroutine(ChangeToNextPhase(4f));
            }
        }
        else
        {
            UpdateLogText("人数不符。\n");
        }
    }

    void OnGiveupEatButton()
    {
        giveupEatButton.gameObject.SetActive(false);
        localPlayerGameObject.GetComponent<Player>().CmdGiveupEat();
    }

    void OnGiveupEliminateButtonClicked()
    {
        giveupEliminateButton.gameObject.SetActive(false);
        localPlayerGameObject.GetComponent<Player>().CmdGiveupEliminate();
    }

    void OnFinishPhaseButtonClicked()
    {
        localPlayerGameObject.GetComponent<Player>().CmdChangeToNextPhase(1f);
    }

    int CountIdentityNumber()
    {
        int count = 0;

        count += Convert.ToInt32(werewolvesNumberInputField.text);
        count += Convert.ToInt32(ordinaryTownsfolkNumberInputField.text);
        if (fortuneTellerToggle.isOn)
        {
            count += 1;
        }
        if (witchToggle.isOn)
        {
            count += 1;
        }
        return count;
    }

    public int CountAlivePlayersNumber(PlayerIdentity pid)
    {
        int count = 0;
        foreach (GameObject g in playerGameObjects)
        {
            if (g.GetComponent<Player>().alive && g.GetComponent<Player>().playerIdentity == pid)
            {
                count += 1;
            }
        }
        return count;
    }

    public int CountAlivePlayersNumber()
    {
        int count = 0;
        foreach (GameObject g in playerGameObjects)
        {
            if (g.GetComponent<Player>().alive)
            {
                count += 1;
            }
        }
        return count;
    }

    [Server]
    void PlayerIdentityAllocate()
    {
        List<PlayerIdentity> list = new List<PlayerIdentity>();
        System.Random random = new System.Random();
        for (int i = 0; i < Convert.ToInt32(werewolvesNumberInputField.text); i++)
        {
            list.Add(PlayerIdentity.Werewolves);
        }
        for (int i = 0; i < Convert.ToInt32(ordinaryTownsfolkNumberInputField.text); i++)
        {
            list.Add(PlayerIdentity.OrdinaryTownsfolk);
        }
        if (fortuneTellerToggle.isOn)
        {
            list.Add(PlayerIdentity.FortuneTeller);
        }
        if (witchToggle.isOn)
        {
            list.Add(PlayerIdentity.Witch);
        }
        for (int i = 0; i < playerGameObjects.Count; i++)
        {
            int j = random.Next(0, list.Count);
            playerGameObjects[i].GetComponent<Player>().playerIdentity = list[j];
            playerGameObjects[i].GetComponent<Player>().alive = true;
            playerGameObjects[i].GetComponent<Player>().deadImage.gameObject.SetActive(false);
            if (list[j] == PlayerIdentity.Werewolves)
            {
                werewolvesPlayerGameObjects.Add(playerGameObjects[i]);
            }
            else if (list[j] == PlayerIdentity.OrdinaryTownsfolk)
            {
                ordinaryTownsfolkPlayerGameObjects.Add(playerGameObjects[i]);
            }
            list.RemoveAt(j);
        }
    }

    void SetGameSettingInteractable(bool value)
    {
        werewolvesNumberInputField.interactable = value;
        ordinaryTownsfolkNumberInputField.interactable = value;
        fortuneTellerToggle.interactable = value;
        witchToggle.interactable = value;
        gameStartButton.gameObject.SetActive(value);
    }

    public void SetEatPlayerButtonActive(bool value)
    {
        if (localPlayerGameObject.GetComponent<Player>().alive && (localPlayerGameObject.GetComponent<Player>().playerIdentity == PlayerIdentity.Werewolves))
        {
            giveupEatButton.gameObject.SetActive(true);
            foreach (GameObject playerGameObject in playerGameObjects)
            {
                if (playerGameObject.GetComponent<Player>().alive)
                {
                    playerGameObject.GetComponent<Player>().eatPlayerButton.gameObject.SetActive(value);
                }
            }
        }
    }

    public void SetDiscoverPlayerButtonActive(bool value)
    {
        if (localPlayerGameObject.GetComponent<Player>().alive && (localPlayerGameObject.GetComponent<Player>().playerIdentity == PlayerIdentity.FortuneTeller))
        {
            foreach (GameObject playerGameObject in playerGameObjects)
            {
                if (playerGameObject.GetComponent<Player>().alive)
                {
                    playerGameObject.GetComponent<Player>().discoverPlayerButton.gameObject.SetActive(value);
                }
            }
        }
    }

    public void SetWitchEliminatePlayerButtonActive(bool value)
    {
        if (value)
        {
            if (localPlayerGameObject.GetComponent<Player>().alive && (localPlayerGameObject.GetComponent<Player>().playerIdentity == PlayerIdentity.Witch))
            {
                foreach (GameObject playerGameObject in playerGameObjects)
                {
                    if (playerGameObject.GetComponent<Player>().alive && playerGameObject != eatenPlayerGameObject)
                    {
                        playerGameObject.GetComponent<Player>().witchEliminatePlayerButton.gameObject.SetActive(value);
                    }
                    else if(playerGameObject.GetComponent<Player>().alive && playerGameObject == eatenPlayerGameObject && localPlayerGameObject.GetComponent<Player>().savePotionNum <= 0)
                    {
                        playerGameObject.GetComponent<Player>().witchEliminatePlayerButton.gameObject.SetActive(value);
                    }
                }
            }
        }
        else
        {
            foreach (GameObject playerGameObject in playerGameObjects)
            {
                playerGameObject.GetComponent<Player>().witchEliminatePlayerButton.gameObject.SetActive(value);
            }
        }
    }

    public void SetWitchSavePlayerButtonActive(bool value)
    {
        if(eatenPlayerGameObject != null)
        {
            if (value)
            {
                if (localPlayerGameObject.GetComponent<Player>().alive && (localPlayerGameObject.GetComponent<Player>().playerIdentity == PlayerIdentity.Witch) && (localPlayerGameObject.GetComponent<Player>().savePotionNum > 0))
                {
                    eatenPlayerGameObject.GetComponent<Player>().witchSavePlayerButton.gameObject.SetActive(value);
                }
            }
            else
            {
                eatenPlayerGameObject.GetComponent<Player>().witchSavePlayerButton.gameObject.SetActive(value);
            }
        }
    }

    public void SetWitchButtonActive(bool value)
    {
        if (localPlayerGameObject.GetComponent<Player>().alive && (localPlayerGameObject.GetComponent<Player>().playerIdentity == PlayerIdentity.Witch))
        {
            finishPhaseButton.gameObject.SetActive(value);
        }
        SetWitchEliminatePlayerButtonActive(value);
        SetWitchSavePlayerButtonActive(value);
    }

    public void SetFinishTalkingButtonActive(bool value)
    {
        if (localPlayerGameObject.GetComponent<Player>().alive)
        {
            localPlayerGameObject.GetComponent<Player>().finishTalkingButton.gameObject.SetActive(value);
        }
    }

    public void SetEliminatePlayerButtonActive(bool value)
    {
        if (localPlayerGameObject.GetComponent<Player>().alive)
        {
            giveupEliminateButton.gameObject.SetActive(value);
            foreach (GameObject playerGameObject in playerGameObjects)
            {
                if (playerGameObject.GetComponent<Player>().alive)
                {
                    playerGameObject.GetComponent<Player>().eliminatePlayerButton.gameObject.SetActive(value);
                }
            }
        }
    }

    [ClientRpc]
    public void RpcUpdateLogText(string logString, PlayerIdentity playeridentity)
    {
        if ((localPlayerGameObject.GetComponent<Player>().playerIdentity == playeridentity) || (playeridentity == PlayerIdentity.IsNotAllocated))
        {
            logText.text += logString;
        }
    }

    public void UpdateLogText(string logString)
    {
        logText.text += logString;
    }

    IEnumerator ForceChengePhaseOnTime(float t, GamePhase oldGamePhase)
    {
        yield return new WaitForSeconds(t);
        if (oldGamePhase == currentGamePhase)
        {
            StartCoroutine(ChangeToNextPhase(1f));
        }
    }

    void ResetPhaseUI()
    {
        finishPhaseButton.gameObject.SetActive(false);
        giveupEliminateButton.gameObject.SetActive(false);
        foreach (GameObject player in playerGameObjects)
        {
            player.GetComponent<Player>().eatPlayerButton.gameObject.SetActive(false);
            player.GetComponent<Player>().discoverPlayerButton.gameObject.SetActive(false);
            player.GetComponent<Player>().witchEliminatePlayerButton.gameObject.SetActive(false);
            player.GetComponent<Player>().witchSavePlayerButton.gameObject.SetActive(false);
            player.GetComponent<Player>().eliminatePlayerButton.gameObject.SetActive(false);
            player.GetComponent<Player>().finishTalkingButton.gameObject.SetActive(false);
        }
    }

    [Server]
    public void FinishAllPlayersEat()
    {
        if (cmdEatPlayerCount == CountAlivePlayersNumber(PlayerIdentity.Werewolves))
        {
            foreach (GameObject player in toBeEatenPlayerGameObjects)
            {
                if (eatenPlayerGameObject == null)
                {
                    eatenPlayerGameObject = player;
                }
                else if (player.GetComponent<Player>().eatCount > eatenPlayerGameObject.GetComponent<Player>().eatCount)
                {
                    eatenPlayerGameObject = player;
                }
            }
            toBeEatenPlayerGameObjects.Clear();
            if(eatenPlayerGameObject == null)
            {
                RpcUpdateLogText("今夜狼人没有咬人。\n",PlayerIdentity.Werewolves);
            }
            else
            {
                RpcUpdateLogText("今夜狼人咬了："+ eatenPlayerGameObject.GetComponent<Player>().playerName+"。\n", PlayerIdentity.Werewolves);
            }
            StartCoroutine(ChangeToNextPhase(2f));
        }
    }

    [Server]
    public void FinishAllPlayersEliminate()
    {
        if (cmdEliminatePlayerCount == CountAlivePlayersNumber())
        {
            foreach (GameObject player in toBeEliminatedPlayerGameObjects)
            {
                if (eliminatePlayerGameObject == null)
                {
                    eliminatePlayerGameObject = player;
                }
                else if (player.GetComponent<Player>().eliminateCount > eliminatePlayerGameObject.GetComponent<Player>().eliminateCount)
                {
                    eliminatePlayerGameObject = player;
                }
            }
            if (eliminatePlayerGameObject != null)
            {
                eliminatePlayerGameObject.GetComponent<Player>().alive = false;
                string eliminateInfo = "投票情况：\n";
                foreach(GameObject g in toBeEliminatedPlayerGameObjects)
                {
                    foreach(GameObject ge in g.GetComponent<Player>().playersWhoEliminateMe)
                    {
                        eliminateInfo = eliminateInfo + ge.GetComponent<Player>().playerName + ", ";
                    }
                    eliminateInfo = eliminateInfo + "投票给" + g.GetComponent<Player>().playerName + "。\n";
                }
                RpcUpdateLogText(eliminateInfo + eliminatePlayerGameObject.GetComponent<Player>().playerName + "被放逐了。\n", PlayerIdentity.IsNotAllocated);
            }
            else
            {
                RpcUpdateLogText("无人投票。\n",PlayerIdentity.IsNotAllocated);
            }
            toBeEliminatedPlayerGameObjects.Clear();
            if (isFirstDay)
            {
                isFirstDay = false;
            }
            StartCoroutine(ChangeToNextPhase(2f));
        }
    }

    //清理当天数据
    [Server]
    public void ClearTheDayData()
    {
        eatenPlayerGameObject = null;//当天被咬的玩家
        eliminatePlayerGameObject = null;//当天被放逐的玩家
        witchEliminatePlayerGameObject = null;//当天被女巫毒的玩家
        cmdEatPlayerCount = 0;//当天狼人想要投票计数
        cmdEliminatePlayerCount = 0;//当天玩家投票计数
        finishTalkingPlayersNumber = 0;//当天结束发言的玩家计数
        foreach (GameObject p in playerGameObjects)
        {
            p.GetComponent<Player>().eatCount = 0;
            p.GetComponent<Player>().eliminateCount = 0;
            p.GetComponent<Player>().playersWhoEliminateMe.Clear();
        }
    }

    [Server]
    public bool JudgeGameOver()
    {
        int AliveWerewolves = 0;
        int AliveTownsfolks = 0;
        foreach (GameObject player in playerGameObjects)
        {
            if (player.GetComponent<Player>().alive && (player.GetComponent<Player>().playerIdentity == PlayerIdentity.Werewolves))
            {
                AliveWerewolves += 1;
            }
            else if (player.GetComponent<Player>().alive && (player.GetComponent<Player>().playerIdentity != PlayerIdentity.Werewolves))
            {
                AliveTownsfolks += 1;
            }
        }
        if (AliveWerewolves == 0)
        {
            RpcGameOver(PlayerIdentity.OrdinaryTownsfolk);
            ClearTheDayData();
            return true;
        }
        else if (AliveTownsfolks == 0)
        {
            RpcGameOver(PlayerIdentity.Werewolves);
            ClearTheDayData();
            return true;
        }
        else
        {
            return false;
        }
    }

    [ClientRpc]
    public void RpcEliminatePlayer(GameObject p)
    {
        UpdateLogText(p.GetComponent<Player>().playerName + "被放逐了。\n");
        //p.GetComponent<Player>().alive = false;
        //p.GetComponent<Player>().deadImage.gameObject.SetActive(true);
        //p.GetComponent<Player>().finishTalkingButton.gameObject.SetActive(false);
    }

    [ClientRpc]
    public void RpcKillPlayer(GameObject p)
    {
        UpdateLogText(p.GetComponent<Player>().playerName + "死了。\n");
        //p.GetComponent<Player>().alive = false;
        //p.GetComponent<Player>().deadImage.gameObject.SetActive(true);
        //p.GetComponent<Player>().finishTalkingButton.gameObject.SetActive(false);
    }

    [ClientRpc]
    public void RpcGameOver(PlayerIdentity pi)
    {
        if (pi == PlayerIdentity.Werewolves)
        {
            UpdateLogText("狼人获胜！\n");
        }
        else if (pi == PlayerIdentity.OrdinaryTownsfolk)
        {
            UpdateLogText("村民获胜！\n");
        }
        else
        {
            UpdateLogText("游戏继续。\n");
        }
    }
}