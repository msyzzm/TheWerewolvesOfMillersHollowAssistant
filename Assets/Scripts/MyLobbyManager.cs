using Prototype.NetworkLobby;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class MyLobbyManager : LobbyManager
{
    public RectTransform playerSpawnPos;

    public override void OnServerAddPlayer(NetworkConnection conn, short playerControllerId)
    {
        var player = (GameObject)GameObject.Instantiate(playerPrefab, Vector3.zero, Quaternion.identity, playerSpawnPos);
        NetworkServer.AddPlayerForConnection(conn, player, playerControllerId);
    }
}
