using System;
using System.Collections.Generic;
using System.IO;
using Stashie.Classes;
using static ExileCore2.PoEMemory.MemoryObjects.ServerInventory;

namespace Stashie.Compartments;

internal class Utility
{
    public static void SaveDefaultConfigsToDisk()
    {
        WriteToNonExistentFile($"{StashieCore.Main.ConfigDirectory}\\example filter.txt",
            "https://github.com/DetectiveSquirrel/Stashie/blob/master/Example%20Filter/Example.json");
    }

    public static void WriteToNonExistentFile(string path, string content)
    {
        if (File.Exists(path))
            return;

        if (path == null)
            return;

        using var streamWriter = new StreamWriter(path, true);
        streamWriter.Write(content);
        streamWriter.Close();
    }

    public static void SetupOrClose()
    {
        SaveDefaultConfigsToDisk();
        StashieCore.Main.SettingsListNodes = new List<ListIndexNode>(100);
        FilterManager.LoadCustomFilters();

        try
        {
            StashieCore.Main.Settings.TabToVisitWhenDone.Max =
                (int)StashieCore.Main.GameController.Game.IngameState.IngameUi.StashElement.TotalStashes - 1;
            var names = StashieCore.Main.GameController.Game.IngameState.IngameUi.StashElement.AllStashNames;
            StashTabNameCoRoutine.UpdateStashNames(names);
        }
        catch (Exception e)
        {
            StashieCore.Main.LogError($"Cant get stash names when init. {e}");
        }
    }

    public static bool CheckIgnoreCells(InventSlotItem inventItem, (int Width, int Height) containerSize,
        int[,] ignoredCells)
    {
        var inventPosX = inventItem.PosX;
        var inventPosY = inventItem.PosY;

        if (inventPosX < 0 || inventPosX >= containerSize.Width)
            return true;

        if (inventPosY < 0 || inventPosY >= containerSize.Height)
            return true;

        return ignoredCells[inventPosY, inventPosX] != 0; //No need to check all item size
    }
}