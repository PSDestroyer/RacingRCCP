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
using Mirror;
using Mirror.Discovery;
using System;
using System.Collections.Generic;

public class RCCP_MirrorManager : NetworkManager {

    /// <summary>
    /// Singleton instance.
    /// </summary>
    public static RCCP_MirrorManager Instance;

    /// <summary>
    /// Gameplay scene name to load target level.
    /// </summary>
    public string gameplaySceneName = "";

    [Header("UI Elements")]

    /// <summary>
    /// Input field for player nickname.
    /// </summary>
    public InputField nickPanel;

    /// <summary>
    /// Panel for browsing available servers.
    /// </summary>
    public GameObject browseRoomsPanel;

    /// <summary>
    /// Parent object containing server entries.
    /// </summary>
    public GameObject roomsContent;

    /// <summary>
    /// Panel displaying chat messages.
    /// </summary>
    public GameObject chatLinesPanel;

    /// <summary>
    /// Parent object containing chat line entries.
    /// </summary>
    public GameObject chatLinesContent;

    /// <summary>
    /// UI element shown when no servers are available.
    /// </summary>
    public GameObject noRoomsYet;

    /// <summary>
    /// Button to create a new server (host).
    /// </summary>
    public GameObject createRoomButton;

    /// <summary>
    /// Button to exit the current server.
    /// </summary>
    public GameObject exitRoomButton;

    [Header("UI Texts")]

    /// <summary>
    /// Status text showing connection progress.
    /// </summary>
    public Text status;

    /// <summary>
    /// Text showing total connected players.
    /// </summary>
    public Text totalOnlinePlayers;

    /// <summary>
    /// Text showing total discovered servers.
    /// </summary>
    public Text totalRooms;

    /// <summary>
    /// Text displaying network address.
    /// </summary>
    public Text region;

    [Header("Other Prefabs")]

    /// <summary>
    /// Prefab for server entry UI.
    /// </summary>
    public RCCP_MirrorUIRoom roomPrefab;

    [Header("Network Discovery")]

    /// <summary>
    /// Reference to NetworkDiscovery component for LAN server browsing.
    /// </summary>
    public NetworkDiscovery networkDiscovery;

    /// <summary>
    /// Cached informer singleton.
    /// </summary>
    private RCCP_UI_Informer uiInformer;

    /// <summary>
    /// Discovered server entries.
    /// </summary>
    private Dictionary<long, ServerResponse> discoveredServers;

    /// <summary>
    /// Active server entry GameObjects.
    /// </summary>
    private Dictionary<long, GameObject> serverListEntries;

    /// <summary>
    /// Local player nickname.
    /// </summary>
    private string playerNickname = "";

    public override void Awake() {

        if (Instance == null) {

            Instance = this;

            // Set playerPrefab so Mirror auto-spawns a vehicle on connect
            if (playerPrefab == null) {

                RCCP_CarController[] vehicles = RCCP_MirrorSync.GetVehicleList();

                if (vehicles != null && vehicles.Length > 0 && vehicles[0] != null)
                    playerPrefab = vehicles[0].gameObject;

            }

            base.Awake();

        } else {

            Destroy(gameObject);
            return;

        }

    }

    public override void OnStartServer() {

        base.OnStartServer();
        RCCP_MirrorSync.EnsureSpawnInfrastructure();

    }

    public override void Start() {

        base.Start();

        uiInformer = RCCP_UI_Informer.Instance;

        discoveredServers = new Dictionary<long, ServerResponse>();
        serverListEntries = new Dictionary<long, GameObject>();

        status.text = "Ready to connect";

        nickPanel.text = "New Player " + UnityEngine.Random.Range(0, 99999).ToString();

        if (networkDiscovery != null)
            networkDiscovery.OnServerFound.AddListener(OnDiscoveredServer);

    }

    /// <summary>
    /// Called when a LAN server is discovered via NetworkDiscovery.
    /// </summary>
    public void OnDiscoveredServer(ServerResponse info) {

        if (discoveredServers.ContainsKey(info.serverId)) {

            discoveredServers[info.serverId] = info;

            if (serverListEntries.TryGetValue(info.serverId, out var entry)) {

                entry.GetComponent<RCCP_MirrorUIRoom>().Check(
                    info.uri.ToString(),
                    "LAN Server"
                );

            }

        } else {

            discoveredServers.Add(info.serverId, info);

            var newEntry = Instantiate(roomPrefab.gameObject);
            newEntry.transform.SetParent(roomsContent.transform);
            newEntry.transform.localScale = Vector3.one;
            newEntry.GetComponent<RCCP_MirrorUIRoom>().Check(
                info.uri.ToString(),
                "LAN Server"
            );
            serverListEntries.Add(info.serverId, newEntry);

        }

        noRoomsYet.SetActive(serverListEntries.Count == 0);

        UpdateStats();

    }

    /// <summary>
    /// Updates the lobby statistics.
    /// </summary>
    private void UpdateStats() {

        totalOnlinePlayers.text = "Connected Players: " + NetworkServer.connections.Count;
        totalRooms.text = "Discovered Servers: " + discoveredServers.Count;
        region.text = "Address: " + networkAddress;

    }

    /// <summary>
    /// Initiates connection or hosting with the entered nickname.
    /// </summary>
    public void Connect() {

        if (nickPanel.text.Length < 4) {

            if (uiInformer)
                uiInformer.Display("4 Characters Needed At Least");

            return;

        }

        playerNickname = nickPanel.text;
        nickPanel.gameObject.SetActive(false);

        // Start LAN discovery to find servers
        browseRoomsPanel.SetActive(true);
        createRoomButton.SetActive(true);
        exitRoomButton.SetActive(false);
        chatLinesPanel.SetActive(false);

        status.text = "Browsing for servers...";

        if (uiInformer)
            uiInformer.Display("Browsing for LAN servers...");

        if (networkDiscovery != null)
            networkDiscovery.StartDiscovery();

    }

    /// <summary>
    /// Creates a new server (host mode).
    /// </summary>
    public void CreateRoom() {

        // Clear discovered servers
        ClearServerList();

        StartHost();

        if (networkDiscovery != null) {

            networkDiscovery.StopDiscovery();
            networkDiscovery.AdvertiseServer();

        }

        status.text = "Hosting server";

        nickPanel.gameObject.SetActive(false);
        browseRoomsPanel.SetActive(false);
        createRoomButton.SetActive(false);
        exitRoomButton.SetActive(true);
        chatLinesPanel.SetActive(true);

        if (uiInformer)
            uiInformer.Display("Hosting server, you can spawn your vehicle from 'Options' menu");

        if (ShouldChangeToGameplayScene())
            ServerChangeScene(gameplaySceneName);

    }

    /// <summary>
    /// Joins a discovered server by its URI.
    /// </summary>
    public void JoinSelectedRoom(RCCP_MirrorUIRoom room) {

        if (networkDiscovery != null)
            networkDiscovery.StopDiscovery();

        // Prefer URI-based client start so scheme/port from discovery are preserved.
        if (Uri.TryCreate(room.roomNameString, UriKind.Absolute, out Uri roomUri)) {

            StartClient(roomUri);

        } else {

            networkAddress = room.roomNameString;
            StartClient();

        }

        status.text = "Joining server...";

        nickPanel.gameObject.SetActive(false);
        browseRoomsPanel.SetActive(false);
        createRoomButton.SetActive(false);
        exitRoomButton.SetActive(true);
        chatLinesPanel.SetActive(true);

        if (uiInformer)
            uiInformer.Display("Joining server, you can spawn your vehicle from 'Options' menu");

    }

    /// <summary>
    /// Joins a server at the specified address directly.
    /// </summary>
    public void JoinServer(string address) {

        if (networkDiscovery != null)
            networkDiscovery.StopDiscovery();

        networkAddress = address;
        StartClient();

        status.text = "Joining server...";

        nickPanel.gameObject.SetActive(false);
        browseRoomsPanel.SetActive(false);
        createRoomButton.SetActive(false);
        exitRoomButton.SetActive(true);
        chatLinesPanel.SetActive(true);

        if (uiInformer)
            uiInformer.Display("Joining server...");

    }

    public override void OnClientConnect() {

        base.OnClientConnect();

        status.text = "Connected";

        nickPanel.gameObject.SetActive(false);
        browseRoomsPanel.SetActive(false);
        createRoomButton.SetActive(false);
        exitRoomButton.SetActive(true);
        chatLinesPanel.SetActive(true);

        if (uiInformer)
            uiInformer.Display("Connected to server, you can spawn your vehicle from 'Options' menu");

    }

    public override void OnClientDisconnect() {

        base.OnClientDisconnect();

        status.text = "Disconnected";

        if (uiInformer)
            uiInformer.Display("Disconnected from server");

        nickPanel.gameObject.SetActive(true);
        browseRoomsPanel.SetActive(false);
        createRoomButton.SetActive(false);
        exitRoomButton.SetActive(false);
        chatLinesPanel.SetActive(false);

    }

    /// <summary>
    /// Leaves the current server.
    /// </summary>
    public void ExitRoom() {

        if (NetworkServer.active && NetworkClient.isConnected) {

            // Host: stop everything
            StopHost();

        } else if (NetworkClient.isConnected) {

            // Client: just disconnect
            StopClient();

        }

        if (networkDiscovery != null)
            networkDiscovery.StopDiscovery();

        status.text = "Disconnected";

        nickPanel.gameObject.SetActive(true);
        browseRoomsPanel.SetActive(false);
        createRoomButton.SetActive(false);
        exitRoomButton.SetActive(false);
        chatLinesPanel.SetActive(false);

        ClearServerList();

    }

    /// <summary>
    /// Clears the discovered server list UI.
    /// </summary>
    private void ClearServerList() {

        foreach (var entry in serverListEntries)
            Destroy(entry.Value);

        serverListEntries.Clear();
        discoveredServers.Clear();

    }

    /// <summary>
    /// Returns the player nickname.
    /// </summary>
    public string GetNickname() {

        return playerNickname;

    }

    /// <summary>
    /// True when gameplay scene name is valid and can be loaded.
    /// </summary>
    private bool ShouldChangeToGameplayScene() {

        if (string.IsNullOrWhiteSpace(gameplaySceneName))
            return false;

        if (gameplaySceneName.Trim().Equals("Gameplay Scene Name", StringComparison.OrdinalIgnoreCase))
            return false;

        if (Application.CanStreamedLevelBeLoaded(gameplaySceneName))
            return true;

        Debug.LogWarning($"RCCP_MirrorManager: Gameplay scene '{gameplaySceneName}' is not in Build Settings. Host will stay in current scene.");
        return false;

    }

}
#endif
