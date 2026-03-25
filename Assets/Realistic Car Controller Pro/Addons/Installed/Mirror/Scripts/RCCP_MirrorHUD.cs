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
using Mirror;

/// <summary>
/// DPI-aware replacement for Mirror's NetworkManagerHUD.
/// Disables the built-in HUD on Awake and draws the same buttons
/// scaled to the current screen resolution so they remain readable
/// on high-DPI / 4K displays.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkManager))]
public class RCCP_MirrorHUD : MonoBehaviour {

    [Tooltip("Reference width used to calculate GUI scale factor.")]
    public float referenceWidth = 1920f;

    [Tooltip("Minimum scale factor to prevent overly tiny UI.")]
    public float minScale = 1f;

    [Tooltip("Maximum scale factor to prevent overly large UI.")]
    public float maxScale = 3f;

    private NetworkManager manager;

    private void Awake() {

        manager = GetComponent<NetworkManager>();

        // Disable Mirror's built-in HUD so we don't get double buttons.
        NetworkManagerHUD builtinHUD = GetComponent<NetworkManagerHUD>();

        if (builtinHUD != null)
            builtinHUD.enabled = false;

    }

    private void OnGUI() {

        float scale = Mathf.Clamp(Screen.width / referenceWidth, minScale, maxScale);

        Matrix4x4 previousMatrix = GUI.matrix;
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));

        float scaledWidth = Screen.width / scale;
        float scaledHeight = Screen.height / scale;
        int panelWidth = 320;

        bool connected = NetworkClient.isConnected || NetworkServer.active;

        if (!connected) {

            // Center the start buttons on screen.
            int panelHeight = 200;
            float x = (scaledWidth - panelWidth) * 0.5f;
            float y = (scaledHeight - panelHeight) * 0.5f;

            GUILayout.BeginArea(new Rect(x, y, panelWidth, panelHeight));
            DrawStartButtons();
            GUILayout.EndArea();

        } else {

            // Top-center for status and stop buttons so they don't overlap the dashboard UI.
            float sx = (scaledWidth - panelWidth) * 0.5f;
            GUILayout.BeginArea(new Rect(sx, 10, panelWidth, 9999));
            DrawStatusLabels();

            if (NetworkClient.isConnected && !NetworkClient.ready) {

                if (GUILayout.Button("Client Ready")) {

                    NetworkClient.Ready();

                    if (NetworkClient.localPlayer == null)
                        NetworkClient.AddPlayer();

                }

            }

            DrawStopButtons();
            GUILayout.EndArea();

        }

        GUI.matrix = previousMatrix;

    }

    private void DrawStartButtons() {

        if (!NetworkClient.active) {

            if (GUILayout.Button("Host (Server + Client)"))
                manager.StartHost();

            GUILayout.BeginHorizontal();

            if (GUILayout.Button("Client"))
                manager.StartClient();

            manager.networkAddress = GUILayout.TextField(manager.networkAddress);

            if (Transport.active is PortTransport portTransport) {

                if (ushort.TryParse(GUILayout.TextField(portTransport.Port.ToString()), out ushort port))
                    portTransport.Port = port;

            }

            GUILayout.EndHorizontal();

            if (GUILayout.Button("Server Only"))
                manager.StartServer();

        } else {

            GUILayout.Label($"Connecting to {manager.networkAddress}..");

            if (GUILayout.Button("Cancel Connection Attempt"))
                manager.StopClient();

        }

    }

    private void DrawStatusLabels() {

        if (NetworkServer.active && NetworkClient.active)
            GUILayout.Label($"<b>Host</b>: running via {Transport.active}");
        else if (NetworkServer.active)
            GUILayout.Label($"<b>Server</b>: running via {Transport.active}");
        else if (NetworkClient.isConnected)
            GUILayout.Label($"<b>Client</b>: connected to {manager.networkAddress} via {Transport.active}");

    }

    private void DrawStopButtons() {

        if (NetworkServer.active && NetworkClient.isConnected) {

            GUILayout.BeginHorizontal();

            if (GUILayout.Button("Stop Host"))
                manager.StopHost();

            if (GUILayout.Button("Stop Client"))
                manager.StopClient();

            GUILayout.EndHorizontal();

        } else if (NetworkClient.isConnected) {

            if (GUILayout.Button("Stop Client"))
                manager.StopClient();

        } else if (NetworkServer.active) {

            if (GUILayout.Button("Stop Server"))
                manager.StopServer();

        }

    }

}
#endif
