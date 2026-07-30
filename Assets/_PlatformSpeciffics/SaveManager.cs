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
#if UNITY_EDITOR
            saveData = (SaveData)LoadEditor(typeof(SaveData));
#else
            saveData = (SaveData)LoadSwitch(typeof(SaveData));
#endif
            NormalizeLoadedData();
        }

        private void NormalizeLoadedData()
        {
            if (saveData == null)
                saveData = new SaveData();

            if (saveData.carDetails == null || saveData.carDetails.Count == 0)
                saveData.carDetails = new SaveData().carDetails;

            if (saveData.customizationLoadouts == null)
                saveData.customizationLoadouts = new Dictionary<string, RCCP_CustomizationLoadout>();

            if (saveData.completedCareerMissions == null)
                saveData.completedCareerMissions = new List<string>();

            saveData.money = Mathf.Max(0, saveData.money);
            saveData.exp = Mathf.Max(0, saveData.exp);
            saveData.currentLevel = Mathf.Max(1, saveData.currentLevel);
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
            public bool indicatorState;
            public string languageCode;

            public int currentLevel = 1;
            public int MaxLevel;
            public int GiftCount;
            public float averagRating;
            public int[] rating;

            public Dictionary<string, CarSpecs> carDetails = new Dictionary<string, CarSpecs>();
            public List<string> completedCareerMissions = new List<string>();

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
                completedCareerMissions = new List<string>();

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

        #endregion
    }
}
