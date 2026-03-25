//----------------------------------------------
//        Realistic Car Controller Pro
//
// Copyright 2014 - 2025 BoneCracker Games
// https://www.bonecrackergames.com
// Ekrem Bugra Ozdoganlar
//
//----------------------------------------------

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Utility class for getting the asset database path of Unity objects.
/// </summary>
public class RCCP_GetAssetPath {

    /// <summary>
    /// Returns the asset database path of the given object.
    /// </summary>
    /// <param name="pathObject">The object to get the path for.</param>
    /// <returns>The asset path, or empty string if not in editor or object not found.</returns>
    public static string GetAssetPath(Object pathObject) {

#if UNITY_EDITOR

        string path = UnityEditor.AssetDatabase.GetAssetPath(pathObject);
        return path;

#else

        return "";

#endif

    }

}
