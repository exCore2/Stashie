﻿using ExileCore;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared;
using ExileCore.Shared.Enums;
using ExileCore.Shared.Helpers;
using ImGuiNET;
using ItemFilterLibrary;
using SharpDX;
using Stashie.Filter;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using static ExileCore.PoEMemory.MemoryObjects.ServerInventory;
using Vector2N = System.Numerics.Vector2;
using Vector4 = System.Numerics.Vector4;

namespace Stashie;

public class StashieCore : BaseSettingsPlugin<StashieSettings>
{
    private const string StashTabsNameChecker = "Stash Tabs Name Checker";
    private const string CoroutineName = "Drop To Stash";
    private readonly Stopwatch _debugTimer = new Stopwatch();
    private Vector2N _clickWindowOffset;
    private long _coroutineIteration;
    private Coroutine _coroutineWorker;
    private List<FilterResult> _dropItems;
    private Action _filterTabs;
    private List<ListIndexNode> _settingsListNodes;
    private string[] _stashTabNamesByIndex;
    private Coroutine _stashTabNamesCoroutine;
    private int _visibleStashIndex = -1;
    private List<CustomFilter> currentFilter;

    public StashieCore()
    {
        Name = "Stashie With Linq";
    }

    public override void ReceiveEvent(string eventId, object args)
    {
        if (!Settings.Enable.Value)
        {
            return;
        }

        switch (eventId)
        {
            case "switch_to_tab":
                HandleSwitchToTabEvent(args);
                break;
        }
    }

    private void HandleSwitchToTabEvent(object tab)
    {
        switch (tab)
        {
            case int index:
                _coroutineWorker = new Coroutine(ProcessSwitchToTab(index), this, CoroutineName);
                break;

            case string name:
                if (!_renamedAllStashNames.Contains(name))
                {
                    DebugWindow.LogMsg($"{Name}: can't find tab with name '{name}'.");
                    break;
                }

                var tempIndex = _renamedAllStashNames.IndexOf(name);
                _coroutineWorker = new Coroutine(ProcessSwitchToTab(tempIndex), this, CoroutineName);
                DebugWindow.LogMsg($"{Name}: Switching to tab with index: {tempIndex} ('{name}').");
                break;

            default:
                DebugWindow.LogMsg("The received argument is not a string or an integer.");
                break;
        }

        Core.ParallelRunner.Run(_coroutineWorker);
    }

    public override bool Initialise()
    {
        Settings.Enable.OnValueChanged += (sender, b) =>
        {
            if (b)
            {
                if (Core.ParallelRunner.FindByName(StashTabsNameChecker) == null) InitStashTabNameCoRoutine();
                _stashTabNamesCoroutine?.Resume();
            }
            else
            {
                _stashTabNamesCoroutine?.Pause();
            }

            SetupOrClose();
        };

        InitStashTabNameCoRoutine();
        SetupOrClose();

        Input.RegisterKey(Settings.DropHotkey);

        Settings.DropHotkey.OnValueChanged += () => { Input.RegisterKey(Settings.DropHotkey); };
        Settings.FilterFile.OnValueSelected = _ => LoadCustomFilters();

        return true;
    }

    public override void AreaChange(AreaInstance area)
    {
        if (_stashTabNamesCoroutine == null) return;
        if (_stashTabNamesCoroutine.Running)
        {
            if (!area.IsHideout && !area.IsTown && !area.DisplayName.Contains("Azurite Mine")) _stashTabNamesCoroutine?.Pause();
        }
        else
        {
            if (area.IsHideout || area.IsTown || area.DisplayName.Contains("Azurite Mine")) _stashTabNamesCoroutine?.Resume();
        }
    }

    private void InitStashTabNameCoRoutine()
    {
        _stashTabNamesCoroutine = new Coroutine(StashTabNamesUpdater_Thread(), this, StashTabsNameChecker);
        Core.ParallelRunner.Run(_stashTabNamesCoroutine);
    }

    private static void WriteToNonExistentFile(string path, string content)
    {
        if (File.Exists(path)) return;

        if (path == null) return;

        using var streamWriter = new StreamWriter(path, true);
        streamWriter.Write(content);
        streamWriter.Close();
    }

    private void SaveDefaultConfigsToDisk() => WriteToNonExistentFile($"{ConfigDirectory}\\example filter.ifl", "https://github.com/DetectiveSquirrel/Stashie/blob/master/Example%20Filters/Example.ifl");

    public override void DrawSettings()
    {
        DrawReloadConfigButton();
        DrawIgnoredCellsSettings();

        if (ImGui.Button("Open Filter Folder"))
        {
            var configDir = ConfigDirectory;
            var directoryToOpen = Directory.Exists(configDir);

            if (!directoryToOpen)
            {
                // Log error when the config directory doesn't exist
            }

            if (configDir != null)
            {
                Process.Start("explorer.exe", configDir);
            }
        }

        base.DrawSettings();

        _filterTabs?.Invoke();
    }

    private void LoadCustomFilters()
    {
        var pickitConfigFileDirectory = Path.Combine(ConfigDirectory);

        if (!Directory.Exists(pickitConfigFileDirectory))
        {
            Directory.CreateDirectory(pickitConfigFileDirectory);
            return;
        }

        var dirInfo = new DirectoryInfo(pickitConfigFileDirectory);
        Settings.FilterFile.Values = dirInfo.GetFiles("*.ifl").Select(x => Path.GetFileNameWithoutExtension(x.Name)).ToList();
        if (Settings.FilterFile.Values.Count != 0 && !Settings.FilterFile.Values.Contains(Settings.FilterFile.Value))
        {
            Settings.FilterFile.Value = Settings.FilterFile.Values.First();
        }

        if (!string.IsNullOrWhiteSpace(Settings.FilterFile.Value))
        {
            var filterFilePath = Path.Combine(pickitConfigFileDirectory, $"{Settings.FilterFile.Value}.ifl");
            if (File.Exists(filterFilePath))
            {
                currentFilter = FilterParser.Load($"{Settings.FilterFile.Value}.ifl", filterFilePath);

                foreach (var customFilter in currentFilter)
                {
                    foreach (var filter in customFilter.Filters)
                    {
                        if (!Settings.CustomFilterOptions.TryGetValue(customFilter.ParentMenuName + filter.FilterName, out var indexNodeS))
                        {
                            indexNodeS = new ListIndexNode {Value = "Ignore", Index = -1};
                            Settings.CustomFilterOptions.Add(customFilter.ParentMenuName + filter.FilterName, indexNodeS);
                        }

                        filter.StashIndexNode = indexNodeS;
                        _settingsListNodes.Add(indexNodeS);
                    }
                }
            }
            else
            {
                currentFilter = null;
                LogError("Item filter file not found, plugin will not work");
            }
        }
    }

    public void SaveIgnoredSLotsFromInventoryTemplate()
    {
        Settings.IgnoredCells = new[,]
        {
            {0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0},
            {0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0},
            {0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0},
            {0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0},
            {0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0}
        };

        Settings.IgnoredExpandedCells = new[,]
        {
            {0, 0, 0, 0},
            {0, 0, 0, 0},
            {0, 0, 0, 0},
            {0, 0, 0, 0},
            {0, 0, 0, 0}
        };

        try
        {
            // Player Inventory
            var inventory_server = GameController.IngameState.Data.ServerData.PlayerInventories[(int)InventorySlotE.MainInventory1];
            UpdateIgnoredCells(inventory_server, Settings.IgnoredCells);

            // Affliction Rucksack
            var backpack_server = GameController.IngameState.Data.ServerData.PlayerInventories[(int)InventorySlotE.ExpandedMainInventory1];
            UpdateIgnoredCells(backpack_server, Settings.IgnoredExpandedCells);
        }
        catch (Exception e)
        {
            LogError($"{e}", 5);
        }

        void UpdateIgnoredCells(InventoryHolder server_items, int[,] ignoredCells)
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
    }

    private void DrawReloadConfigButton()
    {
        if (ImGui.Button("Reload config"))
        {
            LoadCustomFilters();
            GenerateMenu();
            DebugWindow.LogMsg("Reloaded Stashie config", 2, Color.LimeGreen);
        }
    }

    private void DrawIgnoredCellsSettings()
    {
        try
        {
            if (ImGui.Button("Copy Inventory")) SaveIgnoredSLotsFromInventoryTemplate();

            ImGui.SameLine();
            ImGui.TextDisabled("(?)");
            if (ImGui.IsItemHovered()) ImGui.SetTooltip($"Checked = Item will be ignored{Environment.NewLine}UnChecked = Item will be processed");
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
            var toggled = Convert.ToBoolean(Settings.IgnoredExpandedCells[i, j]);
            if (ImGui.Checkbox($"##{numb}IgnoredBackpackInventoryCells", ref toggled)) Settings.IgnoredExpandedCells[i, j] ^= 1;

            if ((numb - 1) % 4 < 3) ImGui.SameLine();

            numb += 1;
        }

        ImGui.NextColumn();
        numb = 1;
        for (var i = 0; i < 5; i++)
        for (var j = 0; j < 12; j++)
        {
            var toggled = Convert.ToBoolean(Settings.IgnoredCells[i, j]);
            if (ImGui.Checkbox($"##{numb}IgnoredMainInventoryCells", ref toggled)) Settings.IgnoredCells[i, j] ^= 1;

            if ((numb - 1) % 12 < 11) ImGui.SameLine();

            numb += 1;
        }

        // Settings to 0 breaks normal settings draws, core has 1 column for sliders?
        ImGui.Columns(1);
    }

    private void GenerateMenu()
    {
        _stashTabNamesByIndex = [.. _renamedAllStashNames];

        _filterTabs = null;

        foreach (var parent in currentFilter)
            _filterTabs += () =>
            {
                ImGui.TextColored(new Vector4(0f, 1f, 0.022f, 1f), parent.ParentMenuName);

                foreach (var filter in parent.Filters)
                    if (Settings.CustomFilterOptions.TryGetValue(parent.ParentMenuName + filter.FilterName, out var indexNode))
                    {
                        var strId = $"{filter.FilterName}##{parent.ParentMenuName + filter.FilterName}";

                        ImGui.Columns(2, strId, true);
                        ImGui.SetColumnWidth(0, 320);

                        if (ImGui.Button(strId, new Vector2N(300, 20))) ImGui.OpenPopup(strId);

                        ImGui.SameLine();
                        ImGui.NextColumn();

                        var item = indexNode.Index + 1;
                        var filterName = filter.FilterName;

                        if (string.IsNullOrWhiteSpace(filterName)) filterName = "Null";

                        if (ImGui.Combo($"##{parent.ParentMenuName + filter.FilterName}", ref item, _stashTabNamesByIndex, _stashTabNamesByIndex.Length))
                        {
                            indexNode.Value = _stashTabNamesByIndex[item];
                            OnSettingsStashNameChanged(indexNode, _stashTabNamesByIndex[item]);
                        }

                        var specialTag = "";

                        if (filter.Shifting != null && (bool)filter.Shifting)
                        {
                            specialTag += "Holds Shift";
                        }

                        if (filter.Affinity != null && (bool)filter.Affinity)
                        {
                            specialTag += !string.IsNullOrEmpty(specialTag) ? ", Expects Affinity" : "Expects Affinity";
                        }

                        ImGui.SameLine();
                        ImGui.Text($"{specialTag}");

                        ImGui.NextColumn();
                        ImGui.Columns(1, "", false);
                        var pop = true;

                        if (!ImGui.BeginPopupModal(strId, ref pop, ImGuiWindowFlags.NoResize | ImGuiWindowFlags.AlwaysAutoResize)) continue;
                        var x = 0;

                        foreach (var name in _renamedAllStashNames)
                        {
                            x++;

                            if (ImGui.Button($"{name}", new Vector2N(100, 20)))
                            {
                                indexNode.Value = name;
                                OnSettingsStashNameChanged(indexNode, name);
                                ImGui.CloseCurrentPopup();
                            }

                            if (x % 10 != 0) ImGui.SameLine();
                        }

                        ImGui.Spacing();
                        ImGuiNative.igIndent(350);
                        if (ImGui.Button("Close", new Vector2N(100, 20))) ImGui.CloseCurrentPopup();

                        ImGui.EndPopup();
                    }
                    else
                    {
                        indexNode = new ListIndexNode {Value = "Ignore", Index = -1};
                    }
            };
    }

    public override Job Tick()
    {
        if (!StashingRequirementsMet() && Core.ParallelRunner.FindByName("Stashie_DropItemsToStash") != null)
        {
            StopCoroutine("Stashie_DropItemsToStash");
            return null;
        }

        if (Settings.DropHotkey.PressedOnce())
        {
            if (Core.ParallelRunner.FindByName("Stashie_DropItemsToStash") == null)
            {
                StartDropItemsToStashCoroutine();
            }
            else
            {
                StopCoroutine("Stashie_DropItemsToStash");
            }
        }

        return null;
    }

    private void StartDropItemsToStashCoroutine()
    {
        _debugTimer.Reset();
        _debugTimer.Start();
        Core.ParallelRunner.Run(new Coroutine(DropToStashRoutine(), this, "Stashie_DropItemsToStash"));
    }

    private void StopCoroutine(string routineName)
    {
        var routine = Core.ParallelRunner.FindByName(routineName);
        routine?.Done();
        _debugTimer.Stop();
        _debugTimer.Reset();
        CleanUp();
    }

    private IEnumerator DropToStashRoutine()
    {
        var cursorPosPreMoving = Input.ForceMousePositionNum; //saving cursor position
        //try stashing items 3 times
        var originTab = GetIndexOfCurrentVisibleTab();
        yield return ParseItems();
        for (var tries = 0; tries < 3 && _dropItems.Count > 0; ++tries)
        {
            if (_dropItems.Count > 0) yield return StashItemsIncrementer();
            yield return ParseItems();
            yield return new WaitTime(Settings.ExtraDelay);
        }

        if (Settings.VisitTabWhenDone.Value)
        {
            if (Settings.BackToOriginalTab.Value)
            {
                yield return SwitchToTab(originTab);
            }
            else
            {
                yield return SwitchToTab(Settings.TabToVisitWhenDone.Value);
            }
        }

        //restoring cursor position
        Input.SetCursorPos(cursorPosPreMoving);
        Input.MouseMove();
        StopCoroutine("Stashie_DropItemsToStash");
    }

    private static void CleanUp()
    {
        Input.KeyUp(Keys.LControlKey);
        Input.KeyUp(Keys.Shift);
    }

    private bool StashingRequirementsMet() => GameController.Game.IngameState.IngameUi.InventoryPanel.IsVisible && GameController.Game.IngameState.IngameUi.StashElement.IsVisibleLocal;

    private IEnumerator ProcessSwitchToTab(int index)
    {
        _debugTimer.Restart();
        yield return SwitchToTab(index);
        _coroutineWorker = Core.ParallelRunner.FindByName(CoroutineName);
        _coroutineWorker?.Done();

        _debugTimer.Restart();
        _debugTimer.Stop();
    }

    private IEnumerator ParseItems()
    {
        var _serverData = GameController.Game.IngameState.Data.ServerData;
        var invItems = _serverData.PlayerInventories[0].Inventory.InventorySlotItems;

        yield return new WaitFunctionTimed(() => invItems != null, true, 500, "ServerInventory->InventSlotItems is null!");
        _dropItems = [];
        _clickWindowOffset = GameController.Window.GetWindowRectangle().TopLeft.ToVector2Num();

        foreach (var invItem in invItems)
        {
            if (invItem.Item == null || invItem.Address == 0) continue;
            if (CheckIgnoreCells(invItem, (12, 5), Settings.IgnoredCells)) continue;

            var testItem = new ItemData(invItem.Item, GameController);
            var result = CheckFilters(testItem, invItem.GetClientRect().Center.ToVector2Num());
            if (result != null) _dropItems.Add(result);
        }

        if (GameController.IngameState.IngameUi.InventoryPanel[InventoryIndex.PlayerExpandedInventory].IsVisible)
        {
            var inventoryInventorySlotItems = _serverData.PlayerInventories[(int)InventorySlotE.ExpandedMainInventory1].Inventory.InventorySlotItems;

            foreach (var expandedInvItem in inventoryInventorySlotItems)
            {
                if (expandedInvItem.Item == null || expandedInvItem.Address == 0) continue;
                if (CheckIgnoreCells(expandedInvItem, (4, 5), Settings.IgnoredExpandedCells)) continue;

                var testItem = new ItemData(expandedInvItem.Item, GameController);
                var result = CheckFilters(testItem, GetExpandedClientRect(expandedInvItem).Center.ToVector2Num());
                if (result != null) _dropItems.Add(result);
            }
        }
    }

    public RectangleF GetExpandedClientRect(InventSlotItem item)
    {
        var playerInventElement = GameController.IngameState.IngameUi.InventoryPanel[InventoryIndex.PlayerExpandedInventory];
        var inventClientRect = playerInventElement.GetClientRect();
        var cellSize = inventClientRect.Width / 4;
        return item.Location.GetItemRect(inventClientRect.X, inventClientRect.Y, cellSize);
    }

    private static bool CheckIgnoreCells(InventSlotItem inventItem, (int Width, int Height) containerSize, int[,] ignoredCells)
    {
        var inventPosX = inventItem.PosX;
        var inventPosY = inventItem.PosY;

        if (inventPosX < 0 || inventPosX >= containerSize.Width) return true;

        if (inventPosY < 0 || inventPosY >= containerSize.Height) return true;

        return ignoredCells[inventPosY, inventPosX] != 0; //No need to check all item size
    }

    private FilterResult CheckFilters(ItemData itemData, Vector2N clickPos)
    {
        foreach (var filter in currentFilter)
        {
            foreach (var subFilter in filter.Filters)
            {
                try
                {
                    if (!subFilter.AllowProcess) continue;

                    if (filter.CompareItem(itemData, subFilter.CompiledQuery)) return new FilterResult(subFilter, itemData, clickPos);
                }
                catch (Exception ex)
                {
                    DebugWindow.LogError($"Check filters error: {ex}");
                }
            }
        }

        return null;
    }

    private IEnumerator StashItemsIncrementer()
    {
        _coroutineIteration++;

        yield return StashItems();
    }

    private IEnumerator StashItems()
    {
        PublishEvent("stashie_start_drop_items", null);

        _visibleStashIndex = GetIndexOfCurrentVisibleTab();
        if (_visibleStashIndex < 0)
        {
            LogMessage($"Stashie: VisibleStashIndex was invalid: {_visibleStashIndex}, stopping.");
            yield break;
        }

        var itemsSortedByStash = _dropItems.OrderBy(x => x.SkipSwitchTab || x.StashIndex == _visibleStashIndex ? 0 : 1).ThenBy(x => x.StashIndex).ToList();

        Input.KeyDown(Keys.LControlKey);
        LogMessage($"Want to drop {itemsSortedByStash.Count} items.");
        foreach (var stashResult in itemsSortedByStash)
        {
            _coroutineIteration++;
            _coroutineWorker?.UpdateTicks(_coroutineIteration);
            var maxTryTime = _debugTimer.ElapsedMilliseconds + 2000;

            //move to correct tab
            if (!stashResult.SkipSwitchTab) yield return SwitchToTab(stashResult.StashIndex);

            yield return new WaitFunctionTimed(() => GameController.IngameState.IngameUi.StashElement.AllInventories[_visibleStashIndex] != null, true, 2000,
                $"Error while loading tab, Index: {_visibleStashIndex}"); //maybe replace waittime with Setting option

            yield return new WaitFunctionTimed(() => GetTypeOfCurrentVisibleStash() != InventoryType.InvalidInventory, true, 2000, $"Error with inventory type, Index: {_visibleStashIndex}"); //maybe replace waittime with Setting option

            yield return StashItem(stashResult);

            _debugTimer.Restart();
            PublishEvent("stashie_finish_drop_items_to_stash_tab", null);
        }
    }

    private IEnumerator StashItem(FilterResult stashResult)
    {
        Input.SetCursorPos(stashResult.ClickPos + _clickWindowOffset);
        yield return new WaitTime(Settings.HoverItemDelay);
        var shiftUsed = false;
        if (stashResult.ShiftForStashing)
        {
            Input.KeyDown(Keys.ShiftKey);
            shiftUsed = true;
        }

        Input.Click(MouseButtons.Left);
        if (shiftUsed)
        {
            Input.KeyUp(Keys.ShiftKey);
        }

        yield return new WaitTime(Settings.StashItemDelay);
    }

    #region Switching between StashTabs

    public IEnumerator SwitchToTab(int tabIndex)
    {
        // We don't want to Switch to a tab that we are already on or that has the magic number for affinities
        //var stashPanel = GameController.Game.IngameState.IngameUi.StashElement;

        _visibleStashIndex = GetIndexOfCurrentVisibleTab();
        var travelDistance = Math.Abs(tabIndex - _visibleStashIndex);
        if (travelDistance == 0) yield break;

        yield return SwitchToTabViaArrowKeys(tabIndex);

        yield return Delay();
    }

    private IEnumerator SwitchToTabViaArrowKeys(int tabIndex, int numberOfTries = 1)
    {
        if (numberOfTries >= 3)
        {
            yield break;
        }

        var indexOfCurrentVisibleTab = GetIndexOfCurrentVisibleTab();
        var travelDistance = tabIndex - indexOfCurrentVisibleTab;
        var tabIsToTheLeft = travelDistance < 0;
        travelDistance = Math.Abs(travelDistance);

        if (tabIsToTheLeft)
        {
            yield return PressKey(Keys.Left, travelDistance);
        }
        else
        {
            yield return PressKey(Keys.Right, travelDistance);
        }

        if (GetIndexOfCurrentVisibleTab() != tabIndex)
        {
            yield return Delay(20);
            yield return SwitchToTabViaArrowKeys(tabIndex, numberOfTries + 1);
        }
    }

    private static IEnumerator PressKey(Keys key, int repetitions = 1)
    {
        for (var i = 0; i < repetitions; i++)
        {
            yield return Input.KeyPress(key);
        }
    }

    private IEnumerator Click(MouseButtons mouseButton = MouseButtons.Left)
    {
        Input.Click(mouseButton);
        yield return Delay();
    }

    private void MoveMouseToElement(Vector2N pos)
    {
        Input.SetCursorPos(pos + GameController.Window.GetWindowRectangle().TopLeft.ToVector2Num());
    }

    private IEnumerator Delay(int ms = 0)
    {
        yield return new WaitTime(Settings.ExtraDelay.Value + ms);
    }

    private int GetIndexOfCurrentVisibleTab() => GameController.Game.IngameState.IngameUi.StashElement.IndexVisibleStash;

    private InventoryType GetTypeOfCurrentVisibleStash()
    {
        var stashPanelVisibleStash = GameController.Game.IngameState.IngameUi?.StashElement?.VisibleStash;
        return stashPanelVisibleStash?.InvType ?? InventoryType.InvalidInventory;
    }

    #endregion Switching between StashTabs

    #region Stashes update

    private void OnSettingsStashNameChanged(ListIndexNode node, string newValue)
    {
        node.Index = GetInventIndexByStashName(newValue);
    }

    private void SetupOrClose()
    {
        SaveDefaultConfigsToDisk();
        _settingsListNodes = new List<ListIndexNode>(100);
        LoadCustomFilters();

        try
        {
            Settings.TabToVisitWhenDone.Max = (int)GameController.Game.IngameState.IngameUi.StashElement.TotalStashes - 1;
            var names = GameController.Game.IngameState.IngameUi.StashElement.AllStashNames;
            UpdateStashNames(names);
        }
        catch (Exception e)
        {
            LogError($"Cant get stash names when init. {e}");
        }
    }

    private int GetInventIndexByStashName(string name)
    {
        var index = _renamedAllStashNames.IndexOf(name);
        if (index != -1) index--;

        return index;
    }

    private List<string> _renamedAllStashNames;

    private void UpdateStashNames(ICollection<string> newNames)
    {
        Settings.AllStashNames = [.. newNames];

        if (newNames.Count < 4)
        {
            LogError("Can't parse names.");
            return;
        }

        _renamedAllStashNames = ["Ignore"];
        var settingsAllStashNames = Settings.AllStashNames;

        for (var i = 0; i < settingsAllStashNames.Count; i++)
        {
            var realStashName = settingsAllStashNames[i];

            if (_renamedAllStashNames.Contains(realStashName))
            {
                realStashName += " (" + i + ")";
#if DebugMode
                    LogMessage("Stashie: fixed same stash name to: " + realStashName, 3);
#endif
            }

            _renamedAllStashNames.Add(realStashName ?? "%NULL%");
        }

        Settings.AllStashNames.Insert(0, "Ignore");

        foreach (var lOption in _settingsListNodes)
            try
            {
                lOption.SetListValues(_renamedAllStashNames);
                var inventoryIndex = GetInventIndexByStashName(lOption.Value);

                if (inventoryIndex == -1) //If the value doesn't exist in list (renamed)
                {
                    if (lOption.Index != -1) //If the value doesn't exist in list and the value was not Ignore
                    {
#if DebugMode
                        LogMessage("Tab renamed : " + lOption.Value + " to " + _renamedAllStashNames[lOption.Index + 1],
                            5);
#endif
                        if (lOption.Index + 1 >= _renamedAllStashNames.Count)
                        {
                            lOption.Index = -1;
                            lOption.Value = _renamedAllStashNames[0];
                        }
                        else
                        {
                            lOption.Value = _renamedAllStashNames[lOption.Index + 1]; //    Just update it's name
                        }
                    }
                    else
                    {
                        lOption.Value = _renamedAllStashNames[0]; //Actually it was "Ignore", we just update it (can be removed)
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
                    lOption.Value = _renamedAllStashNames[inventoryIndex + 1];
                }
            }
            catch (Exception e)
            {
                DebugWindow.LogError($"UpdateStashNames _settingsListNodes {e}");
            }

        GenerateMenu();
    }

    private static readonly WaitTime Wait2Sec = new WaitTime(2000);
    private static readonly WaitTime Wait1Sec = new WaitTime(1000);
    private long _counterStashTabNamesCoroutine;

    public IEnumerator StashTabNamesUpdater_Thread()
    {
        while (true)
        {
            while (!GameController.Game.IngameState.InGame) yield return Wait2Sec;

            var stashPanel = GameController.Game.IngameState?.IngameUi?.StashElement;

            while (stashPanel == null || !stashPanel.IsVisibleLocal) yield return Wait1Sec;

            _counterStashTabNamesCoroutine++;
            _stashTabNamesCoroutine?.UpdateTicks(_counterStashTabNamesCoroutine);
            var cachedNames = Settings.AllStashNames;
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

    #endregion Stashes update
}