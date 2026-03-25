//----------------------------------------------
//        Realistic Car Controller Pro
//
// Copyright 2014 - 2026 BoneCracker Games
// https://www.bonecrackergames.com
// Ekrem Bugra Ozdoganlar
//
//----------------------------------------------

#if UNITY_EDITOR

using UnityEngine.UIElements;

/// <summary>
/// Interface for RCCP overlay content providers.
/// Each tab implements this interface to provide its content.
/// </summary>
public interface IRCCP_OverlayContent {

    /// <summary>
    /// Creates the content for this tab.
    /// </summary>
    /// <param name="searchQuery">Current search query for filtering.</param>
    /// <returns>The root visual element for this tab's content.</returns>
    VisualElement CreateContent(string searchQuery);

    /// <summary>
    /// Called periodically to update the content.
    /// </summary>
    void OnUpdate();

    /// <summary>
    /// Called when the tab is being destroyed.
    /// </summary>
    void OnDestroy();

}

#endif
