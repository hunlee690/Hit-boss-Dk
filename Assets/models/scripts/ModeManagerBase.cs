using System;
using System.Collections.Generic;
using UnityEngine;

public class ModeManagerBase : MonoBehaviour
{
    [Header("Mode Scripts")]

    [Tooltip("Every mode-specific script that can exist on the player.")]
    public string[] allModeScripts;

    [Tooltip("Scripts that should be enabled in THIS mode.")]
    public string[] thisModeScripts;


    // =====================================================
    // APPLY MODE
    // =====================================================

    public void ApplyModeScripts()
    {
        GameObject player =
            GameObject.FindGameObjectWithTag("Player");

        if (player == null)
        {
            Debug.LogWarning(
                name + ": Player with tag 'Player' not found."
            );

            return;
        }


        MonoBehaviour[] scripts =
            player.GetComponentsInChildren<MonoBehaviour>(
                true
            );


        HashSet<string> allScripts =
            CreateNameSet(allModeScripts);

        HashSet<string> activeScripts =
            CreateNameSet(thisModeScripts);


        // Disable every known mode script first.
        foreach (MonoBehaviour script in scripts)
        {
            if (script == null)
                continue;


            string scriptName =
                script.GetType().Name;


            if (allScripts.Contains(scriptName))
            {
                script.enabled = false;
            }
        }


        // Enable only scripts belonging to this mode.
        foreach (MonoBehaviour script in scripts)
        {
            if (script == null)
                continue;


            string scriptName =
                script.GetType().Name;


            if (activeScripts.Contains(scriptName))
            {
                script.enabled = true;
            }
        }


        // Check for missing scripts.
        foreach (string wantedName in activeScripts)
        {
            bool found = false;


            foreach (MonoBehaviour script in scripts)
            {
                if (script != null &&
                    string.Equals(
                        script.GetType().Name,
                        wantedName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    found = true;
                    break;
                }
            }


            if (!found)
            {
                Debug.LogWarning(
                    "Mode script not found on Player: " +
                    wantedName
                );
            }
        }
    }


    // =====================================================
    // SCRIPT NAME LIST
    // =====================================================

    HashSet<string> CreateNameSet(
        string[] names)
    {
        HashSet<string> result =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase
            );


        if (names == null)
            return result;


        foreach (string value in names)
        {
            if (string.IsNullOrWhiteSpace(value))
                continue;


            string cleanName =
                value.Trim();


            // Allows either:
            // SkateController
            // or
            // SkateController.cs
            if (cleanName.EndsWith(
                ".cs",
                StringComparison.OrdinalIgnoreCase))
            {
                cleanName =
                    cleanName.Substring(
                        0,
                        cleanName.Length - 3
                    );
            }


            result.Add(cleanName);
        }


        return result;
    }
}