//----------------------------------------------
//        RCCP AI Setup Assistant
//
// Copyright 2026 BoneCracker Games
// https://www.bonecrackergames.com
// Ekrem Bugra Ozdoganlar
//
//----------------------------------------------

#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;

namespace BoneCrackerGames.RCCP.AIAssistant {

public static class RCCP_AISetScriptingSymbol {

    public static void SetEnabled(string defineName, bool enable) {

        // Track which groups we've already processed to avoid duplicates
        var processedGroups = new System.Collections.Generic.HashSet<BuildTargetGroup>();

        foreach (BuildTarget bt in Enum.GetValues(typeof(BuildTarget))) {

            try {
                var group = BuildPipeline.GetBuildTargetGroup(bt);

                if (group == BuildTargetGroup.Unknown)
                    continue;

                // Skip if we've already processed this group
                if (processedGroups.Contains(group))
                    continue;

                processedGroups.Add(group);

                var named = NamedBuildTarget.FromBuildTargetGroup(group);
                string defs = PlayerSettings.GetScriptingDefineSymbols(named);
                var list = defs.Split(';', StringSplitOptions.RemoveEmptyEntries).ToList();

                if (enable) {

                    if (!list.Contains(defineName))
                        list.Add(defineName);

                } else {

                    list.Remove(defineName);

                }

                string newDefs = string.Join(";", list);
                PlayerSettings.SetScriptingDefineSymbols(named, newDefs);

            } catch {
                // Skip deprecated or unsupported build targets silently
                continue;
            }

        }

    }

}

} // namespace BoneCrackerGames.RCCP.AIAssistant
#endif
