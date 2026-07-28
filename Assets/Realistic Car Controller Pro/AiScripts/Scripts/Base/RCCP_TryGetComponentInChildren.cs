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
/// Utility class for finding components in immediate children of a transform.
/// Unlike GetComponentInChildren, this only searches direct children, not the entire hierarchy.
/// </summary>
public class RCCP_TryGetComponentInChildren {

    /// <summary>
    /// Searches for a component of type T in the immediate children of the given transform.
    /// </summary>
    /// <typeparam name="T">The type of component to find.</typeparam>
    /// <param name="transform">The parent transform to search children of.</param>
    /// <returns>The first component of type T found, or default if none found.</returns>
    public static T Get<T>(Transform transform) {

        T comp;

        for (int i = 0; i < transform.childCount; i++) {

            if (transform.GetChild(i).TryGetComponent<T>(out comp))
                return comp;

        }

        return default;

    }

}
