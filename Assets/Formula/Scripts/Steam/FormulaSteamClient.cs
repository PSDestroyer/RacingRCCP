using System;
using System.Collections.Generic;
using UnityEngine;

#if STEAMWORKS_NET && !UNITY_SWITCH
using Steamworks;
#endif

namespace FormulaGame
{
    [DefaultExecutionOrder(-10000)]
    public sealed class FormulaSteamClient : MonoBehaviour
    {
        private static FormulaSteamClient instance;
        private readonly HashSet<string> pendingAchievements = new HashSet<string>();
        private string personaName;

        public static string PersonaName => instance != null ? instance.personaName : null;

#if STEAMWORKS_NET && !UNITY_SWITCH
        private bool initialized;
        private bool statsReady;
        private Callback<UserStatsReceived_t> statsReceived;
        private Callback<UserStatsStored_t> statsStored;
        private Callback<UserAchievementStored_t> achievementStored;
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Create()
        {
#if STEAMWORKS_NET && !UNITY_SWITCH
            if (instance != null)
                return;

            var host = new GameObject("Formula Steam Client");
            DontDestroyOnLoad(host);
            instance = host.AddComponent<FormulaSteamClient>();
#endif
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            FormulaSession.LapCompleted += OnLapCompleted;
            FormulaSession.Finished += OnFinished;

#if STEAMWORKS_NET && !UNITY_SWITCH
            InitializeSteam();
#endif
        }

#if STEAMWORKS_NET && !UNITY_SWITCH
        private void InitializeSteam()
        {
            try
            {
                ESteamAPIInitResult result = SteamAPI.InitEx(out string message);
                if (result != ESteamAPIInitResult.k_ESteamAPIInitResult_OK)
                {
                    Debug.LogWarning($"[Formula Steam] Steam initialization skipped: {message} ({result}).");
                    return;
                }

                initialized = true;
                ReadPersonaName();
                uint appId = SteamUtils.GetAppID().m_AppId;
                if (appId == 480)
                {
                    Debug.LogWarning("[Formula Steam] Running with the development App ID 480. Replace steam_appid.txt with the Formula App ID before testing achievements.");
                    return;
                }

                statsReceived = Callback<UserStatsReceived_t>.Create(OnStatsReceived);
                statsStored = Callback<UserStatsStored_t>.Create(OnStatsStored);
                achievementStored = Callback<UserAchievementStored_t>.Create(OnAchievementStored);
                // Steamworks SDK 1.65 synchronizes current stats before the game
                // process starts; RequestCurrentStats is no longer exposed.
                statsReady = true;
                FlushAchievements();
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Formula Steam] Steam is unavailable; the game will continue without achievements. {exception.Message}");
            }
        }

        private void ReadPersonaName()
        {
            try
            {
                personaName = SteamFriends.GetPersonaName();
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Formula Steam] Steam profile name is unavailable. {exception.Message}");
            }
        }
#endif

        private void OnLapCompleted()
        {
            foreach (string achievement in FormulaAchievementRules.ForCompletedLap(FormulaSession.LastLapValid))
                Unlock(achievement);
        }

        private void OnFinished(FormulaReceipt receipt)
        {
            FormulaCatalog.CareerEvent formulaEvent = FormulaCatalog.Current?.Event(receipt?.eventId);
            FormulaAssets.Track track = formulaEvent == null ? null : FormulaAssets.Current?.FindTrack(formulaEvent.trackId);
            float reference = track == null ? 0 : track.referenceLapSeconds;

            foreach (string achievement in FormulaAchievementRules.ForFinishedEvent(receipt, formulaEvent, reference))
                Unlock(achievement);
        }

        private void Unlock(string apiName)
        {
            if (string.IsNullOrEmpty(apiName))
                return;

            pendingAchievements.Add(apiName);
#if STEAMWORKS_NET && !UNITY_SWITCH
            FlushAchievements();
#endif
        }

#if STEAMWORKS_NET && !UNITY_SWITCH
        private void Update()
        {
            if (initialized)
                SteamAPI.RunCallbacks();
        }

        private void OnStatsReceived(UserStatsReceived_t callback)
        {
            if (!initialized || callback.m_nGameID != SteamUtils.GetAppID().m_AppId)
                return;

            statsReady = callback.m_eResult == EResult.k_EResultOK;
            if (!statsReady)
            {
                Debug.LogWarning($"[Formula Steam] Could not load achievement state: {callback.m_eResult}.");
                return;
            }

            FlushAchievements();
        }

        private void FlushAchievements()
        {
            if (!initialized || !statsReady || pendingAchievements.Count == 0)
                return;

            bool changed = false;
            var completed = new List<string>();
            foreach (string apiName in pendingAchievements)
            {
                if (SteamUserStats.GetAchievement(apiName, out bool achieved) && achieved)
                {
                    completed.Add(apiName);
                    continue;
                }

                if (SteamUserStats.SetAchievement(apiName))
                {
                    changed = true;
                    completed.Add(apiName);
                }
                else
                {
                    Debug.LogWarning($"[Formula Steam] Achievement '{apiName}' was rejected. Confirm that its API name is published in Steamworks App Admin.");
                }
            }

            foreach (string apiName in completed)
                pendingAchievements.Remove(apiName);

            if (changed && !SteamUserStats.StoreStats())
                Debug.LogWarning("[Formula Steam] Steam rejected StoreStats; unlocked achievements may be retried by Steam later.");
        }

        private void OnStatsStored(UserStatsStored_t callback)
        {
            if (callback.m_nGameID == SteamUtils.GetAppID().m_AppId && callback.m_eResult != EResult.k_EResultOK)
                Debug.LogWarning($"[Formula Steam] StoreStats completed with {callback.m_eResult}.");
        }

        private static void OnAchievementStored(UserAchievementStored_t callback)
        {
            if (callback.m_nGameID == SteamUtils.GetAppID().m_AppId)
                Debug.Log($"[Formula Steam] Achievement stored: {callback.m_rgchAchievementName}.");
        }
#endif

        private void OnDestroy()
        {
            FormulaSession.LapCompleted -= OnLapCompleted;
            FormulaSession.Finished -= OnFinished;

#if STEAMWORKS_NET && !UNITY_SWITCH
            statsReceived?.Dispose();
            statsStored?.Dispose();
            achievementStored?.Dispose();

            if (initialized)
            {
                SteamAPI.Shutdown();
                initialized = false;
            }
#endif

            if (instance == this)
                instance = null;
        }
    }
}
