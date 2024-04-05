using ExileCore;
using ExileCore.Shared;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using static Stashie.StashieCore;

namespace Stashie.Compartments;

internal class StashTabNameCoRoutine
{
    public static readonly WaitTime Wait2Sec = new WaitTime(2000);
    public static readonly WaitTime Wait1Sec = new WaitTime(1000);
    public static long _counterStashTabNamesCoroutine;

    public static void InitStashTabNameCoRoutine()
    {
        Main.StashTabNamesCoroutine = new Coroutine(StashTabNamesUpdater_Thread(), Main, StashTabsNameChecker);
        Core.ParallelRunner.Run(Main.StashTabNamesCoroutine);
    }

    public static void OnSettingsStashNameChanged(ListIndexNode node, string newValue)
    {
        node.Index = GetInventIndexByStashName(newValue);
    }

    public static void SetupOrClose()
    {
        SaveDefaultConfigsToDisk();
        Main.SettingsListNodes = new List<ListIndexNode>(100);
        FilterManager.LoadCustomFilters();

        try
        {
            Main.Settings.TabToVisitWhenDone.Max = (int)Main.GameController.Game.IngameState.IngameUi.StashElement.TotalStashes - 1;
            var names = Main.GameController.Game.IngameState.IngameUi.StashElement.AllStashNames;
            UpdateStashNames(names);
        }
        catch (Exception e)
        {
            Main.LogError($"Cant get stash names when init. {e}");
        }
    }

    public static void WriteToNonExistentFile(string path, string content)
    {
        if (File.Exists(path)) return;

        if (path == null) return;

        using var streamWriter = new StreamWriter(path, true);
        streamWriter.Write(content);
        streamWriter.Close();
    }

    public static void SaveDefaultConfigsToDisk() =>
        WriteToNonExistentFile($"{Main.ConfigDirectory}\\example filter.txt", "https://github.com/DetectiveSquirrel/Stashie/blob/master/Example%20Filter/Example.json");

    public static int GetInventIndexByStashName(string name)
    {
        var index = RenamedAllStashNames.IndexOf(name);
        if (index != -1) index--;

        return index;
    }

    public static void UpdateStashNames(ICollection<string> newNames)
    {
        Main.Settings.AllStashNames = [.. newNames];

        if (newNames.Count < 4)
        {
            Main.LogError("Can't parse names.");
            return;
        }

        RenamedAllStashNames = ["Ignore"];
        var settingsAllStashNames = Main.Settings.AllStashNames;

        for (var i = 0; i < settingsAllStashNames.Count; i++)
        {
            var realStashName = settingsAllStashNames[i];

            if (RenamedAllStashNames.Contains(realStashName))
            {
                realStashName += " (" + i + ")";
#if DebugMode
                    LogMessage("Stashie: fixed same stash name to: " + realStashName, 3);
#endif
            }

            RenamedAllStashNames.Add(realStashName ?? "%NULL%");
        }

        Main.Settings.AllStashNames.Insert(0, "Ignore");

        foreach (var lOption in Main.SettingsListNodes)
            try
            {
                lOption.SetListValues(RenamedAllStashNames);
                var inventoryIndex = GetInventIndexByStashName(lOption.Value);

                if (inventoryIndex == -1) //If the value doesn't exist in list (renamed)
                {
                    if (lOption.Index != -1) //If the value doesn't exist in list and the value was not Ignore
                    {
#if DebugMode
                        LogMessage("Tab renamed : " + lOption.Value + " to " + RenamedAllStashNames[lOption.Index + 1],
                            5);
#endif
                        if (lOption.Index + 1 >= RenamedAllStashNames.Count)
                        {
                            lOption.Index = -1;
                            lOption.Value = RenamedAllStashNames[0];
                        }
                        else
                        {
                            lOption.Value = RenamedAllStashNames[lOption.Index + 1]; //    Just update it's name
                        }
                    }
                    else
                    {
                        lOption.Value = RenamedAllStashNames[0]; //Actually it was "Ignore", we just update it (can be removed)
                    }
                }
                else //tab just change it's index
                {
#if DebugMode
                    if (lOption.Index != inventoryIndex)
                    {
                        LogMessage("Tab moved: " + lOption.Index + " to " + inventoryIndex, 5);
                    }
#endif
                    lOption.Index = inventoryIndex;
                    lOption.Value = RenamedAllStashNames[inventoryIndex + 1];
                }
            }
            catch (Exception e)
            {
                DebugWindow.LogError($"UpdateStashNames SettingsListNodes {e}");
            }

        StashieSettingsHandler.GenerateTabMenu();
    }

    public static IEnumerator StashTabNamesUpdater_Thread()
    {
        while (true)
        {
            while (!Main.GameController.Game.IngameState.InGame) yield return Wait2Sec;

            var stashPanel = Main.GameController.Game.IngameState?.IngameUi?.StashElement;

            while (stashPanel == null || !stashPanel.IsVisibleLocal) yield return Wait1Sec;

            _counterStashTabNamesCoroutine++;
            Main.StashTabNamesCoroutine?.UpdateTicks(_counterStashTabNamesCoroutine);
            var cachedNames = Main.Settings.AllStashNames;
            var realNames = stashPanel.AllStashNames;

            if (realNames.Count + 1 != cachedNames.Count)
            {
                UpdateStashNames(realNames);
                continue;
            }

            for (var index = 0; index < realNames.Count; ++index)
            {
                var cachedName = cachedNames[index + 1];
                if (cachedName.Equals(realNames[index])) continue;

                UpdateStashNames(realNames);
                break;
            }

            yield return Wait1Sec;
        }
    }
}