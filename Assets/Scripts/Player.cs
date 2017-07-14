using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public class Player : NetworkBehaviour {

    public GameObject canvas;
    public Text playerNameText;
    public Dropdown playerIdentityDropdown;
    public Button eatPlayerButton;
    public Button discoverPlayerButton;
    public Button witchEliminatePlayerButton;
    public Button witchSavePlayerButton;
    public Button eliminatePlayerButton;
    public Button finishTalkingButton;
    public Image deadImage;
    public GameSceneManager gameSceneManager;

    [SyncVar(hook = "OnNameChanged")]
    public string playerName;//玩家名字
    [SyncVar(hook = "OnColorChanged")]
    public Color playerColor;//玩家颜色

    [SyncVar(hook = "OnPlayerIdentityChanged")]
    public PlayerIdentity playerIdentity;//玩家身份
    [SyncVar(hook = "OnAliveChanged")]
    public bool alive;//是否活着

    public int savePotionNum;//女巫灵药数量
    public int eliminatePotionNum;//女巫毒药数量

    public int eatCount;//当天狼人想咬计数
    public int eliminateCount;//当天被玩家票计数
    public List<GameObject> playersWhoEliminateMe;//当天投票给我的玩家列表

    public void Start()
    {
        eatPlayerButton.onClick.AddListener(OnEatPlayerButtonClicked);
        discoverPlayerButton.onClick.AddListener(OnDiscoverPlayerButtonClicked);
        witchEliminatePlayerButton.onClick.AddListener(OnWitchEliminatePlayerButtonClicked);
        witchSavePlayerButton.onClick.AddListener(OnWitchSavePlayerButtonClicked);
        eliminatePlayerButton.onClick.AddListener(OnEliminatePlayerButtonClicked);
        finishTalkingButton.onClick.AddListener(OnFinishTalkingButtonClicked);
        Debug.Log(playerName + " Start!" + " isServer:" + isServer + " isClient:" + isClient);
        canvas = GameObject.FindGameObjectWithTag("Canvas");
        transform.SetParent(canvas.transform);
        OnNameChanged(playerName);
        OnColorChanged(playerColor);
        if(GameObject.FindGameObjectWithTag("SceneManager") != null)
        {
            gameSceneManager = GameObject.FindGameObjectWithTag("SceneManager").GetComponent<GameSceneManager>();
            if (!gameSceneManager.playerGameObjects.Contains(gameObject))
            {
                gameSceneManager.playerGameObjects.Add(gameObject);
                if (hasAuthority)
                {
                    gameSceneManager.localPlayerGameObject = gameObject;
                }
            }
        }
    }

    void OnNameChanged(string value)
    {
        playerName = value;
        playerNameText.text = playerName;
        if (hasAuthority)
        {
            playerNameText.color = Color.green;
        }
    }

    void OnColorChanged(Color value)
    {
        playerColor = value;
        GetComponent<Image>().color = playerColor;
    }

    void OnPlayerIdentityChanged(PlayerIdentity value)
    {
        playerIdentity = value;
        if (hasAuthority)
        {
            if (value == PlayerIdentity.Werewolves)
            {
                playerIdentityDropdown.value = 1;
            }
            else if (value == PlayerIdentity.OrdinaryTownsfolk)
            {
                playerIdentityDropdown.value = 2;
            }
            else if (value == PlayerIdentity.FortuneTeller)
            {
                playerIdentityDropdown.value = 3;
            }
            else if (value == PlayerIdentity.Witch)
            {
                playerIdentityDropdown.value = 4;
                savePotionNum = 1;
                eliminatePotionNum = 1;
            }
            else if(value == PlayerIdentity.IsNotAllocated)
            {
                playerIdentityDropdown.value = 0;
            }
        }
        else
        {
            playerIdentityDropdown.value = 0;
        }
    }

    void OnAliveChanged(bool value)
    {
        alive = value;
        if (value)
        {
            deadImage.gameObject.SetActive(false);
        }
        else
        {
            deadImage.gameObject.SetActive(true);
        }
    }

    void OnEatPlayerButtonClicked()
    {
        gameSceneManager.SetEatPlayerButtonActive(false);
        gameSceneManager.giveupEatButton.gameObject.SetActive(false);
        gameSceneManager.localPlayerGameObject.GetComponent<Player>().CmdEatPlayer(gameObject);
    }

    void OnDiscoverPlayerButtonClicked()
    {
        gameSceneManager.SetDiscoverPlayerButtonActive(false);
        gameSceneManager.logText.text += playerName + " 的身份是： ";
        if(playerIdentity == PlayerIdentity.Werewolves)
        {
            gameSceneManager.logText.text += "狼人\n";
        }
        else if (playerIdentity == PlayerIdentity.OrdinaryTownsfolk)
        {
            gameSceneManager.logText.text += "普村\n";
        }
        else if (playerIdentity == PlayerIdentity.FortuneTeller)
        {
            gameSceneManager.logText.text += "预言家\n";
        }
        else if (playerIdentity == PlayerIdentity.Witch)
        {
            gameSceneManager.logText.text += "女巫\n";
        }
        gameSceneManager.localPlayerGameObject.GetComponent<Player>().CmdChangeToNextPhase(1f);
    }

    void OnWitchEliminatePlayerButtonClicked()
    {
        if (gameSceneManager.localPlayerGameObject.GetComponent<Player>().eliminatePotionNum > 0)
        {
            gameSceneManager.SetWitchEliminatePlayerButtonActive(false);
            gameSceneManager.localPlayerGameObject.GetComponent<Player>().eliminatePotionNum -= 1;
            gameSceneManager.localPlayerGameObject.GetComponent<Player>().CmdWitchEliminatePlayer(gameObject);
        }
        else
        {
            gameSceneManager.UpdateLogText("没有毒药了！\n");
        }
        
    }

    void OnWitchSavePlayerButtonClicked()
    {
        if(gameSceneManager.localPlayerGameObject.GetComponent<Player>().savePotionNum > 0)
        {
            gameSceneManager.SetWitchSavePlayerButtonActive(false);
            gameSceneManager.localPlayerGameObject.GetComponent<Player>().savePotionNum -= 1;
            gameSceneManager.localPlayerGameObject.GetComponent<Player>().CmdWitchSavePlayer(gameObject);
        }
        else
        {
            gameSceneManager.UpdateLogText("没有灵药了！\n");
        }
    }

    void OnEliminatePlayerButtonClicked()
    {
        gameSceneManager.SetEliminatePlayerButtonActive(false);
        gameSceneManager.giveupEliminateButton.gameObject.SetActive(false);
        gameSceneManager.localPlayerGameObject.GetComponent<Player>().CmdEliminatePlayer(gameObject);
    }

    void OnFinishTalkingButtonClicked()
    {
        gameSceneManager.SetFinishTalkingButtonActive(false);
        gameSceneManager.localPlayerGameObject.GetComponent<Player>().CmdFinishTalking();
    }

    [Command]
    void CmdEatPlayer(GameObject target)
    {
        gameSceneManager.RpcUpdateLogText(playerName + " 想咬： " + target.GetComponent<Player>().playerName + "\n", PlayerIdentity.Werewolves);
        target.GetComponent<Player>().eatCount += 1;
        gameSceneManager.cmdEatPlayerCount += 1;
        if (!gameSceneManager.toBeEatenPlayerGameObjects.Contains(target))
        {
            gameSceneManager.toBeEatenPlayerGameObjects.Add(target);
        }
        gameSceneManager.FinishAllPlayersEat();
    }

    [Command]
    public void CmdGiveupEat()
    {
        gameSceneManager.RpcUpdateLogText(playerName + "放弃咬人\n", PlayerIdentity.Werewolves);
        gameSceneManager.cmdEatPlayerCount += 1;
        gameSceneManager.FinishAllPlayersEat();
    }

    [Command]
    void CmdWitchEliminatePlayer(GameObject target)
    {
        gameSceneManager.witchEliminatePlayerGameObject = target;
    }

    [Command]
    void CmdWitchSavePlayer(GameObject target)
    {
        gameSceneManager.eatenPlayerGameObject = null;
    }
    
    [Command]
    void CmdEliminatePlayer(GameObject target)
    {
        target.GetComponent<Player>().eliminateCount += 1;
        gameSceneManager.cmdEliminatePlayerCount += 1;
        target.GetComponent<Player>().playersWhoEliminateMe.Add(gameObject);
        if (!gameSceneManager.toBeEliminatedPlayerGameObjects.Contains(target))
        {
            gameSceneManager.toBeEliminatedPlayerGameObjects.Add(target);
        }
        gameSceneManager.FinishAllPlayersEliminate();
    }

    [Command]
    public void CmdGiveupEliminate()
    {
        gameSceneManager.cmdEliminatePlayerCount += 1;
        gameSceneManager.FinishAllPlayersEliminate();
    }

    [Command]
    void CmdFinishTalking()
    {
        gameSceneManager.finishTalkingPlayersNumber += 1;
        if(gameSceneManager.finishTalkingPlayersNumber == gameSceneManager.CountAlivePlayersNumber())
        {
            CmdChangeToNextPhase(1f);
        }
    }

    [Command]
    public void CmdChangeToNextPhase(float time)
    {
        Debug.Log("客户端请求进行下一阶段");
        StartCoroutine(gameSceneManager.ChangeToNextPhase(time));
    }

    [Command]
    void CmdLogTextUpdate(string logString, PlayerIdentity playeridentity)
    {
        gameSceneManager.RpcUpdateLogText(logString, playeridentity);
    }
}
