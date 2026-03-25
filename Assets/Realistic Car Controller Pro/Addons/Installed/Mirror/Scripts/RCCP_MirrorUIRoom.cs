//----------------------------------------------
//        Realistic Car Controller Pro
//
// Copyright © 2014 - 2026 BoneCracker Games
// https://www.bonecrackergames.com
// Ekrem Bugra Ozdoganlar
//
//----------------------------------------------

#if RCCP_MIRROR && MIRROR
using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

public class RCCP_MirrorUIRoom : MonoBehaviour {

    public string roomNameString = "";

    public Text roomName;
    public Text maxPlayers;

    public void Check(string _roomName, string _maxPlayers) {

        roomNameString = _roomName;
        roomName.text = _roomName;
        maxPlayers.text = _maxPlayers;

    }

    public void JoinRoom() {

        RCCP_MirrorManager.Instance.JoinSelectedRoom(this);

    }

}
#endif
