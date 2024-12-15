using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using ExileCore2;
using ExileCore2.PoEMemory.Components;
using ExileCore2.PoEMemory.MemoryObjects;
using ExileCore2.Shared.Enums;
using ImGuiNET;
using Stashie.Classes;
using static Stashie.StashieCore;
using Vector2N = System.Numerics.Vector2;
using Vector4N = System.Numerics.Vector4;

namespace Stashie.Compartments;

public class StashieSettingsHandler
{
    public static void SaveIgnoredSlotsFromInventoryTemplate()
    {
        Main.Settings.IgnoredCells = new int[5, 12];
        Main.Settings.IgnoredExpandedCells = new int[5, 4];

        try
        {
            // Player Inventory
            var inventory_server =
                Main.GameController.IngameState.Data.ServerData.PlayerInventories[(int)InventorySlotE.MainInventory1];
            UpdateIgnoredCells(inventory_server, Main.Settings.IgnoredCells);
        }
        catch (Exception e)
        {
            Main.LogError($"{e}", 5);
        }
    }

    private static void UpdateIgnoredCells(InventoryHolder server_items, int[,] ignoredCells)
    {
        foreach (var item in server_items.Inventory.InventorySlotItems)
        {
            var baseC = item.Item.GetComponent<Base>();
            var itemSizeX = baseC.ItemCellsSizeX;
            var itemSizeY = baseC.ItemCellsSizeY;
            var inventPosX = item.PosX;
            var inventPosY = item.PosY;
            for (var y = 0; y < itemSizeY; y++)
            for (var x = 0; x < itemSizeX; x++)
                ignoredCells[y + inventPosY, x + inventPosX] = 1;
        }
    }

    public static void GenerateTabMenu()
    {
        Main.StashTabNamesByIndex = [.. RenamedAllStashNames];

        Main.FilterTabs = null;

        foreach (var parent in Main.currentFilter)
            Main.FilterTabs += () =>
            {
                ImGui.TextColored(new Vector4N(0f, 1f, 0.022f, 1f), parent.ParentMenuName);

                foreach (var filter in parent.Filters)
                    if (Main.Settings.CustomFilterOptions.TryGetValue(parent.ParentMenuName + filter.FilterName,
                            out var indexNode))
                    {
                        var strId = $"{filter.FilterName}##{parent.ParentMenuName + filter.FilterName}";

                        ImGui.Columns(2, strId, true);
                        ImGui.SetColumnWidth(0, 320);

                        if (ImGui.Button(strId, new Vector2N(300, 20)))
                            ImGui.OpenPopup(strId);

                        ImGui.SameLine();
                        ImGui.NextColumn();

                        var item = indexNode.Index + 1;
                        var filterName = filter.FilterName;

                        if (string.IsNullOrWhiteSpace(filterName))
                            filterName = "Null";

                        if (ImGui.Combo($"##{parent.ParentMenuName + filter.FilterName}", ref item,
                                Main.StashTabNamesByIndex, Main.StashTabNamesByIndex.Length))
                        {
                            indexNode.Value = Main.StashTabNamesByIndex[item];
                            StashTabNameCoRoutine.OnSettingsStashNameChanged(indexNode,
                                Main.StashTabNamesByIndex[item]);
                        }

                        var specialTag = "";

                        if (filter.Shifting != null && (bool)filter.Shifting) specialTag += "Holds Shift";

                        if (filter.Affinity != null && (bool)filter.Affinity)
                            specialTag += !string.IsNullOrEmpty(specialTag) ? ", Expects Affinity" : "Expects Affinity";

                        ImGui.SameLine();
                        ImGui.Text($"{specialTag}");

                        ImGui.NextColumn();
                        ImGui.Columns(1, "", false);
                        var pop = true;

                        if (!ImGui.BeginPopupModal(strId, ref pop,
                                ImGuiWindowFlags.NoResize | ImGuiWindowFlags.AlwaysAutoResize))
                            continue;

                        var x = 0;

                        foreach (var name in RenamedAllStashNames)
                        {
                            x++;

                            if (ImGui.Button($"{name}", new Vector2N(100, 20)))
                            {
                                indexNode.Value = name;
                                StashTabNameCoRoutine.OnSettingsStashNameChanged(indexNode, name);
                                ImGui.CloseCurrentPopup();
                            }

                            if (x % 10 != 0)
                                ImGui.SameLine();
                        }

                        ImGui.Spacing();
                        ImGuiNative.igIndent(350);
                        if (ImGui.Button("Close", new Vector2N(100, 20)))
                            ImGui.CloseCurrentPopup();

                        ImGui.EndPopup();
                    }
                    else
                    {
                        indexNode = new ListIndexNode { Value = "Ignore", Index = -1 };
                    }
            };
    }

    public static void DrawReloadConfigButton()
    {
        if (!ImGui.Button("Reload config"))
            return;

        FilterManager.LoadCustomFilters();
        GenerateTabMenu();
        DebugWindow.LogMsg("Reloaded Stashie config", 2, Color.LimeGreen);
    }

    public static void DrawIgnoredCellsSettings()
    {
        try
        {
            if (ImGui.Button("Copy Inventory"))
                SaveIgnoredSlotsFromInventoryTemplate();

            ImGui.SameLine();
            ImGui.TextDisabled("(?)");
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(
                    $"Checked = Item will be ignored{Environment.NewLine}UnChecked = Item will be processed");
        }
        catch (Exception e)
        {
            DebugWindow.LogError(e.ToString(), 10);
        }

        ImGui.Columns(2, "", true);
        ImGui.SetColumnWidth(0, 120);

        var numb = 1;
        for (var i = 0; i < 5; i++)
        for (var j = 0; j < 4; j++)
        {
            var toggled = Convert.ToBoolean(Main.Settings.IgnoredExpandedCells[i, j]);
            if (ImGui.Checkbox($"##{numb}IgnoredBackpackInventoryCells", ref toggled))
                Main.Settings.IgnoredExpandedCells[i, j] ^= 1;

            if ((numb - 1) % 4 < 3)
                ImGui.SameLine();

            numb += 1;
        }

        ImGui.NextColumn();
        numb = 1;
        for (var i = 0; i < 5; i++)
        for (var j = 0; j < 12; j++)
        {
            var toggled = Convert.ToBoolean(Main.Settings.IgnoredCells[i, j]);
            if (ImGui.Checkbox($"##{numb}IgnoredMainInventoryCells", ref toggled))
                Main.Settings.IgnoredCells[i, j] ^= 1;

            if ((numb - 1) % 12 < 11)
                ImGui.SameLine();

            numb += 1;
        }

        // Settings to 0 breaks normal settings draws, core has 1 column for sliders?
        ImGui.Columns(1);
    }

    public static void FilePicker()
    {
        DrawReloadConfigButton();
        DrawIgnoredCellsSettings();
        if (ImGui.Button("Open Filter Folder"))
        {
            var configDir = Main.ConfigDirectory;
            var directoryToOpen = Directory.Exists(configDir);

            if (!directoryToOpen)
            {
                // Log error when the config directory doesn't exist
            }

            if (configDir != null) Process.Start("explorer.exe", configDir);
        }
    }
}