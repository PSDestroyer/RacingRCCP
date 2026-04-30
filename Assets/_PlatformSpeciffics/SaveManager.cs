using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;
using _Assets._PlatformSpeciffics.Switch;
using System.IO;

namespace HalvaStudio.Save
{
    public class SaveManager : Singleton<SaveManager>
    {
        public SaveData saveData;
        [SerializeField] private SaveData defaultSaveData;

        public override void AwakeInit()
        {
            Debug.Log("Initializing SaveManager...");
            Load();
        }

        private void Load()
        {
            if (saveData == null)
            {
                saveData = new SaveData();
            }
            if (saveData.carDetails == null || saveData.carDetails.Count == 0)
            {
                saveData.carDetails = new SaveData().carDetails; // Assign default values
            }
#if UNITY_EDITOR
            saveData = (SaveData)LoadEditor(typeof(SaveData));
#else
            saveData = (SaveData)LoadSwitch(typeof(SaveData));
#endif
        }

        public void Save(bool forceSave = false)
        {
            Debug.Log("Saving data...");
#if UNITY_EDITOR
            SaveEditor(saveData);
#else
            SaveSwitch(saveData, forceSave);
#endif
        }

        #region Editor

        public void SaveEditor(object saveObject)
        {
            try
            {
                string jsonFile = JsonConvert.SerializeObject(saveObject);
                string savePath = GetSavePath();

                File.WriteAllText(savePath, jsonFile);

                Debug.Log("Save completed.");
            }
            catch (Exception e)
            {
                Debug.LogError("Error saving data: " + e.Message);
            }
        }

        private string GetSavePath()
        {
            string savePath = Path.Combine(Application.persistentDataPath, "save.json");
            Debug.Log("Save Path: " + savePath);
            return savePath;
        }

        public object LoadEditor(System.Type objectType)
        {
            string savePath = GetSavePath();
            object returnObject = null;

            if (File.Exists(savePath))
            {
                try
                {
                    string jsonFile = File.ReadAllText(savePath);
                    returnObject = JsonConvert.DeserializeObject(jsonFile, objectType);
                }
                catch (Exception e)
                {
                    Debug.LogError("Error loading data: " + e.Message);
                    returnObject = defaultSaveData ?? new SaveData();
                }
            }
            else
            {
                Debug.LogError("Save file not found. Using default data.");
                returnObject = defaultSaveData ?? new SaveData();
            }

            return returnObject;
        }

        #endregion
#if UNITY_SWITCH && !UNITY_EDITOR
        #region Switch

        public void SaveSwitch(object saveObject, bool forceSave = false)
        {
            try
            {
                string jsonFile = JsonConvert.SerializeObject(saveObject);
                NintendoSave.Save(jsonFile, forceSave);
                Debug.Log("Save completed.");
            }
            catch (Exception e)
            {
                Debug.LogError("Error saving data: " + e.Message);
            }
        }

        public object LoadSwitch(System.Type objectType)
        {
            bool successful = false;
            string jsonFile = NintendoSave.Load(ref successful);

            if (jsonFile == null)
            {
                Debug.LogError("Save file not found. Using default data.");
                return defaultSaveData ?? new SaveData();
            }

            try
            {
                return JsonConvert.DeserializeObject(jsonFile, objectType);
            }
            catch (Exception e)
            {
                Debug.LogError("Error deserializing data: " + e.Message);
                return defaultSaveData ?? new SaveData();
            }
        }

        #endregion
#endif
        [System.Serializable]
        public class SaveData
        {
            [Header("Player Data")]
            public string PlayerName;
            public int money;
            public int exp;
            public int currentCar;
            public string selectedMapName = string.Empty;
            public string selectedTrackName = string.Empty;
            public int selectedMapIndex = -1;
            public int selectedTrackIndex = -1;
            public int selectedMissionIndex = -1;
            public int currentMap = -1;
            public int currentMissionMapId = -1;
            public int currentMissionRaceType = -1;
            public int currentMapTrackCount = 0;
            public int currentTrackMissionCount = 0;
            public int currentRaceTime;
            public int currentRaceLap;
            public int currentRaceTarget;
            public int currentRacePay;

            [Header("Settings")]
            public int lookSensitivity;
            public string difficulty;
            public float soundLevel = 0.7f;
            public float VehicleLevel = 0.7f;
            public float musicLevel = 0.4f;
            public bool vibrationsState = true;
            public bool easyDriftMode = false;
            public bool indicatorState;
            public string inputRebindsJson = string.Empty;

            public int currentLevel;
            public int MaxLevel;
            public int GiftCount;
            public float averagRating;
            public int[] rating;

            public Dictionary<string, CarSpecs> carDetails = new Dictionary<string, CarSpecs>();
            public List<string> unlockedTrackKeys = new List<string>();
            public List<string> unlockedMissionKeys = new List<string>();
            public List<string> completedMissionKeys = new List<string>();

            // NOU
            // public RCCP_CustomizationLoadout customizationLoadout;
            public Dictionary<string, RCCP_CustomizationLoadout> customizationLoadouts 
                = new Dictionary<string, RCCP_CustomizationLoadout>();
            public SaveData()
            {
                carDetails = new Dictionary<string, CarSpecs>
                {
                    { "CTR", new CarSpecs(true, 220, 240, true, 0, 60, 1, 2000) }
                };

                customizationLoadouts = new Dictionary<string, RCCP_CustomizationLoadout>();

            }

            public class CarSpecs
            {
                public bool isBought;
                public int power;
                public int topSpeed;
                public bool turbo;
                public int color;
                public int steerAngle;
                public int traction;
                public int brake;

                public CarSpecs(bool isBought, int power, int TopSpeed, bool turbo, int color, int steerAngle, int traction, int brake)
                {
                    this.isBought = isBought;
                    this.power = power;
                    this.turbo = turbo;
                    this.color = color;
                    this.steerAngle = steerAngle;
                    this.traction = traction;
                    this.brake = brake;
                    this.topSpeed = TopSpeed;
                }
            }
        }

        #region Custom Methods
        public void EnsureMissionProgressInitialized()
        {
            if (saveData == null)
                saveData = new SaveData();

            EnsureMissionProgressCollections();

            UnlockTrackInternal(0, 0);
            UnlockMissionInternal(0, 0, 0);
        }

        public bool IsTrackUnlocked(int mapIndex, int trackIndex)
        {
            EnsureMissionProgressInitialized();

            if (mapIndex == 0 && trackIndex == 0)
                return true;

            return saveData.unlockedTrackKeys.Contains(GetTrackKey(mapIndex, trackIndex));
        }

        public bool IsMissionUnlocked(int mapIndex, int trackIndex, int missionIndex)
        {
            EnsureMissionProgressInitialized();

            if (mapIndex == 0 && trackIndex == 0 && missionIndex == 0)
                return true;

            return saveData.unlockedMissionKeys.Contains(GetMissionKey(mapIndex, trackIndex, missionIndex));
        }

        public bool IsMissionCompleted(int mapIndex, int trackIndex, int missionIndex)
        {
            EnsureMissionProgressInitialized();
            return saveData.completedMissionKeys.Contains(GetMissionKey(mapIndex, trackIndex, missionIndex));
        }

        public void CompleteCurrentMissionAndUnlockNext()
        {
            if (saveData == null)
                saveData = new SaveData();

            EnsureMissionProgressInitialized();

            int mapIndex = saveData.selectedMapIndex;
            int trackIndex = saveData.selectedTrackIndex;
            int missionIndex = saveData.selectedMissionIndex;

            if (mapIndex < 0 || trackIndex < 0 || missionIndex < 0)
                return;

            string missionKey = GetMissionKey(mapIndex, trackIndex, missionIndex);

            if (!saveData.completedMissionKeys.Contains(missionKey))
                saveData.completedMissionKeys.Add(missionKey);

            UnlockTrackInternal(mapIndex, trackIndex);
            UnlockMissionInternal(mapIndex, trackIndex, missionIndex);

            int missionsInTrack = Mathf.Max(0, saveData.currentTrackMissionCount);
            int tracksInMap = Mathf.Max(0, saveData.currentMapTrackCount);

            if (missionIndex + 1 < missionsInTrack)
            {
                UnlockMissionInternal(mapIndex, trackIndex, missionIndex + 1);
            }
            else if (trackIndex + 1 < tracksInMap)
            {
                UnlockTrackInternal(mapIndex, trackIndex + 1);
                UnlockMissionInternal(mapIndex, trackIndex + 1, 0);
            }

            Save();
        }

        public void SaveCustomizationLoadout(string saveKey, RCCP_CustomizationLoadout loadout, bool autoSaveToDisk = true)
        {
            if (saveData == null)
                saveData = new SaveData();

            if (saveData.customizationLoadouts == null)
                saveData.customizationLoadouts = new Dictionary<string, RCCP_CustomizationLoadout>();

            if (loadout == null)
                loadout = new RCCP_CustomizationLoadout();

            saveData.customizationLoadouts[saveKey] = loadout;

            if (autoSaveToDisk)
                Save();
        }

        public RCCP_CustomizationLoadout LoadCustomizationLoadout(string saveKey)
        {
            if (saveData == null)
                saveData = new SaveData();

            if (saveData.customizationLoadouts == null)
                saveData.customizationLoadouts = new Dictionary<string, RCCP_CustomizationLoadout>();

            if (saveData.customizationLoadouts.TryGetValue(saveKey, out RCCP_CustomizationLoadout loadout) && loadout != null)
                return loadout;

            return new RCCP_CustomizationLoadout();
        }

        public void DeleteCustomizationLoadout(string saveKey, bool autoSaveToDisk = true)
        {
            if (saveData == null)
                saveData = new SaveData();

            if (saveData.customizationLoadouts == null)
                saveData.customizationLoadouts = new Dictionary<string, RCCP_CustomizationLoadout>();

            if (saveData.customizationLoadouts.ContainsKey(saveKey))
                saveData.customizationLoadouts.Remove(saveKey);

            if (autoSaveToDisk)
                Save();
        }
        public void SaveCar(string carName, bool isBought, int power,int TopSpeed, bool turbo, int tireFriction, int steerAngle,int traction,int brake)
        {
            if (saveData.carDetails == null)
            {
                saveData.carDetails = new Dictionary<string, SaveData.CarSpecs>();
            }

            saveData.carDetails[carName] = new SaveData.CarSpecs(isBought, power,TopSpeed, turbo, tireFriction, steerAngle, traction ,brake);
        }

        public SaveData.CarSpecs GetCarSpecs(string carName)
        {
            if (saveData.carDetails != null && saveData.carDetails.ContainsKey(carName))
            {
                return saveData.carDetails[carName];
            }

            // Return default car specs if car is not found
            return new SaveData.CarSpecs(false, 0,0 , false, 0, 0,0,0);
        }

        public bool IsCarBought(string carName)
        {
            return saveData.carDetails != null && saveData.carDetails.ContainsKey(carName) && saveData.carDetails[carName].isBought;
        }

        private void EnsureMissionProgressCollections()
        {
            if (saveData.unlockedTrackKeys == null)
                saveData.unlockedTrackKeys = new List<string>();

            if (saveData.unlockedMissionKeys == null)
                saveData.unlockedMissionKeys = new List<string>();

            if (saveData.completedMissionKeys == null)
                saveData.completedMissionKeys = new List<string>();
        }

        private void UnlockTrackInternal(int mapIndex, int trackIndex)
        {
            string key = GetTrackKey(mapIndex, trackIndex);

            if (!saveData.unlockedTrackKeys.Contains(key))
                saveData.unlockedTrackKeys.Add(key);
        }

        private void UnlockMissionInternal(int mapIndex, int trackIndex, int missionIndex)
        {
            UnlockTrackInternal(mapIndex, trackIndex);

            string key = GetMissionKey(mapIndex, trackIndex, missionIndex);

            if (!saveData.unlockedMissionKeys.Contains(key))
                saveData.unlockedMissionKeys.Add(key);
        }

        private string GetTrackKey(int mapIndex, int trackIndex)
        {
            return $"{mapIndex}:{trackIndex}";
        }

        private string GetMissionKey(int mapIndex, int trackIndex, int missionIndex)
        {
            return $"{mapIndex}:{trackIndex}:{missionIndex}";
        }

        #endregion
    }
}
