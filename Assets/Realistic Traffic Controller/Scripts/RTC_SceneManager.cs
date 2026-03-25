//----------------------------------------------
//        Realistic Traffic Controller
//
// Copyright © 2014 - 2024 BoneCracker Games
// https://www.bonecrackergames.com
//
//----------------------------------------------

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Scene manager that contains all lanes, waypoints, vehicles ,etc...
/// </summary>
[AddComponentMenu("BoneCracker Games/Realistic Traffic Controller/RTC Scene Manager")]
public class RTC_SceneManager : RTC_Singleton<RTC_SceneManager> {

    /// <summary>
    ///  All lanes.
    /// </summary>
    public RTC_Lane[] allLanes;

    /// <summary>
    /// All waypoints.
    /// </summary>
    public RTC_Waypoint[] allWaypoints;

    /// <summary>
    /// All traffic vehicles.
    /// </summary>
    public RTC_CarController[] allVehicles;

    /// <summary>
    /// Traffic spawner.
    /// </summary>
    public RTC_TrafficSpawner spawner;

    /// <summary>
    /// Dont spawn arounds. RTC_TrafficSpawner won't spawn or enable vehicles nearby them.
    /// </summary>
    public RTC_DontSpawnAround[] dontSpawnArounds;

    private void Awake() {

        //Application.targetFrameRate = 100;

        //  Getting all lanes, waypoints, and don't spawn arounds.
        UpdateEverything();

    }

    /// <summary>
    /// Updates all active lanes and waypoints.
    /// </summary>
    public void UpdateEverything() {

#if !UNITY_2022_1_OR_NEWER
        //  Getting all lanes, waypoints, and lights.
        allLanes = FindObjectsOfType<RTC_Lane>();
        allWaypoints = FindObjectsOfType<RTC_Waypoint>();
        dontSpawnArounds = FindObjectsOfType<RTC_DontSpawnAround>();
#else
        //  Getting all lanes, waypoints, and lights.
        allLanes = FindObjectsByType<RTC_Lane>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        allWaypoints = FindObjectsByType<RTC_Waypoint>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        dontSpawnArounds = FindObjectsByType<RTC_DontSpawnAround>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
#endif

        for (int i = 0; i < allLanes.Length; i++) {

            if (allLanes[i] != null)
                allLanes[i].UpdateLane();

        }

    }

    private void OnEnable() {

        //  Listening events for traffic vehicle spawn, and spawner.
        RTC_TrafficSpawner.OnSpawnerSpawned += RTC_TrafficSpawner_OnSpawnerSpawned;
        RTC_CarController.OnTrafficSpawned += RTC_CarController_OnTrafficSpawned;
        RTC_CarController.OnTrafficDeSpawned += RTC_CarController_OnTrafficDeSpawned;
        RTC_DontSpawnAround.OnDontSpawnAroundSpawned += RTC_DontSpawnAround_OnDontSpawnAroundSpawned;
        RTC_DontSpawnAround.OnDontSpawnAroundDisabled += RTC_DontSpawnAround_OnDontSpawnAroundDisabled;

    }

    /// <summary>
    /// When don't spawn around object is disabled, remove it from the list.
    /// </summary>
    /// <param name="dontSpawnAround"></param>
    private void RTC_DontSpawnAround_OnDontSpawnAroundDisabled(RTC_DontSpawnAround dontSpawnAround) {

        List<RTC_DontSpawnAround> dontSpawnAroundsList = new List<RTC_DontSpawnAround>();

        for (int i = 0; i < dontSpawnArounds.Length; i++) {

            if (dontSpawnArounds[i] != null)
                dontSpawnAroundsList.Add(dontSpawnArounds[i]);

        }

        dontSpawnAroundsList.Remove(dontSpawnAround);
        dontSpawnArounds = dontSpawnAroundsList.ToArray();

    }

    /// <summary>
    /// When don't spawn around object is enabled, add it to the list.
    /// </summary>
    /// <param name="dontSpawnAround"></param>
    private void RTC_DontSpawnAround_OnDontSpawnAroundSpawned(RTC_DontSpawnAround dontSpawnAround) {

        List<RTC_DontSpawnAround> dontSpawnAroundsList = new List<RTC_DontSpawnAround>();

        for (int i = 0; i < dontSpawnArounds.Length; i++) {

            if (dontSpawnArounds[i] != null)
                dontSpawnAroundsList.Add(dontSpawnArounds[i]);

        }

        dontSpawnAroundsList.Add(dontSpawnAround);
        dontSpawnArounds = dontSpawnAroundsList.ToArray();

    }

    /// <summary>
    /// When a traffic vehicle spawned, add it to the list.
    /// </summary>
    /// <param name="newVehicle"></param>
    private void RTC_CarController_OnTrafficSpawned(RTC_CarController newVehicle) {

        //  Adding new vehicle to the array.
        List<RTC_CarController> all = new List<RTC_CarController>();
        all.AddRange(allVehicles);

        if (!all.Contains(newVehicle))
            all.Add(newVehicle);

        allVehicles = all.ToArray();

    }

    /// <summary>
    /// When a traffic vehicle de-spawned, remove it from the list.
    /// </summary>
    /// <param name="newVehicle"></param>
    private void RTC_CarController_OnTrafficDeSpawned(RTC_CarController newVehicle) {

        //  Adding new vehicle to the array.
        List<RTC_CarController> all = new List<RTC_CarController>();
        all.AddRange(allVehicles);

        if (all.Contains(newVehicle))
            all.Remove(newVehicle);

        allVehicles = all.ToArray();

    }

    /// <summary>
    /// When a traffic spawner spawned, assign field.
    /// </summary>
    /// <param name="newSpawner"></param>
    private void RTC_TrafficSpawner_OnSpawnerSpawned(RTC_TrafficSpawner newSpawner) {

        //  Scene shouldn't contain multiple spawners. If there is one, destroy it.
        if (spawner)
            Destroy(spawner.gameObject);

        spawner = newSpawner;

    }

    private void OnDisable() {

        RTC_TrafficSpawner.OnSpawnerSpawned -= RTC_TrafficSpawner_OnSpawnerSpawned;
        RTC_CarController.OnTrafficSpawned -= RTC_CarController_OnTrafficSpawned;
        RTC_CarController.OnTrafficDeSpawned -= RTC_CarController_OnTrafficDeSpawned;
        RTC_DontSpawnAround.OnDontSpawnAroundSpawned -= RTC_DontSpawnAround_OnDontSpawnAroundSpawned;
        RTC_DontSpawnAround.OnDontSpawnAroundDisabled -= RTC_DontSpawnAround_OnDontSpawnAroundDisabled;

    }

}
