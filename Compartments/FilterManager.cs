using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using ExileCore2;
using ExileCore2.Shared;
using ItemFilterLibrary;
using Stashie.Classes;
using static Stashie.StashieCore;
using Vector2N = System.Numerics.Vector2;

namespace Stashie.Compartments;

internal class FilterManager
{
    public static void LoadCustomFilters()
    {
        var pickitConfigFileDirectory = Path.Combine(Main.ConfigDirectory);

        if (!Directory.Exists(pickitConfigFileDirectory))
        {
            Directory.CreateDirectory(pickitConfigFileDirectory);
            return;
        }

        var dirInfo = new DirectoryInfo(pickitConfigFileDirectory);
        Main.Settings.FilterFile.Values =
            dirInfo.GetFiles("*.json").Select(x => Path.GetFileNameWithoutExtension(x.Name)).ToList();
        if (Main.Settings.FilterFile.Values.Count != 0 &&
            !Main.Settings.FilterFile.Values.Contains(Main.Settings.FilterFile.Value))
            Main.Settings.FilterFile.Value = Main.Settings.FilterFile.Values.First();

        if (!string.IsNullOrWhiteSpace(Main.Settings.FilterFile.Value))
        {
            var filterFilePath = Path.Combine(pickitConfigFileDirectory, $"{Main.Settings.FilterFile.Value}.json");
            if (File.Exists(filterFilePath))
            {
                Main.currentFilter = FilterFileHandler.Load($"{Main.Settings.FilterFile.Value}.json", filterFilePath);

                foreach (var customFilter in Main.currentFilter)
                foreach (var filter in customFilter.Filters)
                {
                    if (!Main.Settings.CustomFilterOptions.TryGetValue(customFilter.ParentMenuName + filter.FilterName,
                            out var indexNodeS))
                    {
                        indexNodeS = new ListIndexNode { Value = "Ignore", Index = -1 };
                        Main.Settings.CustomFilterOptions.Add(customFilter.ParentMenuName + filter.FilterName,
                            indexNodeS);
                    }

                    filter.StashIndexNode = indexNodeS;
                    Main.SettingsListNodes.Add(indexNodeS);
                }
            }
            else
            {
                Main.currentFilter = null;
                Main.LogError("Item filter file not found, plugin will not work");
            }
        }
    }

    public static FilterResult CheckFilters(ItemData itemData, Vector2N clickPos)
    {
        foreach (var filter in Main.currentFilter)
        foreach (var subFilter in filter.Filters)
            try
            {
                if (!subFilter.AllowProcess)
                    continue;

                if (filter.CompareItem(itemData, subFilter.CompiledQuery))
                    return new FilterResult(subFilter, itemData, clickPos);
            }
            catch (Exception ex)
            {
                DebugWindow.LogError($"Check filters error: {ex}");
            }

        return null;
    }

    public static async SyncTask<bool> ParseItems()
    {
        var _serverData = Main.GameController.Game.IngameState.Data.ServerData;
        var invItems = _serverData.PlayerInventories[0].Inventory.InventorySlotItems;

        await TaskUtils.CheckEveryFrameWithThrow(() => invItems != null, new CancellationTokenSource(500).Token);
        Main.DropItems = [];
        Main.ClickWindowOffset = Main.GameController.Window.GetWindowRectangle().TopLeft;

        foreach (var invItem in invItems)
        {
            if (invItem.Item == null || invItem.Address == 0)
                continue;

            if (Utility.CheckIgnoreCells(invItem, (12, 5), Main.Settings.IgnoredCells))
                continue;

            var testItem = new ItemData(invItem.Item, Main.GameController);
            var result = CheckFilters(testItem, invItem.GetClientRect().Center);
            if (result != null)
                Main.DropItems.Add(result);
        }

        #region Ignore 1 max stack of wisdoms/portals

        if (Main.Settings.KeepHighestIDStack) KeepHighestStackItem("Scroll of Wisdom");

        if (Main.Settings.KeepHighestTPStack) KeepHighestStackItem("Portal Scroll");

        void KeepHighestStackItem(string itemName)
        {
            var items = Main.DropItems.Where(item => item.ItemData.BaseName == itemName).ToList();
            if (items.Count == 0)
                return;

            var maxStackItem = items.MaxBy(item => item.ItemData.StackInfo.Count);
            if (maxStackItem == null)
                return;

            Main.DropItems.Remove(maxStackItem);
        }

        #endregion

        return true;
    }

    public static List<ItemData> GetInventoryItems()
    {
        var serverData = Main.GameController.Game.IngameState.Data.ServerData;
        var invItems = serverData.PlayerInventories[0].Inventory.InventorySlotItems;

        Main.DropItems = [];
        Main.ClickWindowOffset = Main.GameController.Window.GetWindowRectangle().TopLeft;

        return (from invItem in invItems
            where invItem.Item != null && invItem.Address != 0
            select new ItemData(invItem.Item, Main.GameController)).ToList();
    }
}