using ExileCore;
using ExileCore.Shared;
using ExileCore.Shared.Enums;
using Stashie.Classes;
using System;
using System.Collections;
using System.Linq;
using System.Windows.Forms;
using static Stashie.StashieCore;

namespace Stashie.Compartments;

internal class ActionsHandler
{
    public static int GetIndexOfCurrentVisibleTab() => Main.GameController.Game.IngameState.IngameUi.StashElement.IndexVisibleStash;

    public static void CleanUp()
    {
        Input.KeyUp(Keys.LControlKey);
        Input.KeyUp(Keys.Shift);
    }

    public static void HandleSwitchToTabEvent(object tab)
    {
        switch (tab)
        {
            case int index:
                Main.CoroutineWorker = new Coroutine(ActionCoRoutine.ProcessSwitchToTab(index), Main, CoroutineName);
                break;

            case string name:
                if (!RenamedAllStashNames.Contains(name))
                {
                    DebugWindow.LogMsg($"{Main.Name}: can't find tab with name '{name}'.");
                    break;
                }

                var tempIndex = RenamedAllStashNames.IndexOf(name);
                Main.CoroutineWorker = new Coroutine(ActionCoRoutine.ProcessSwitchToTab(tempIndex), Main, CoroutineName);
                DebugWindow.LogMsg($"{Main.Name}: Switching to tab with index: {tempIndex} ('{name}').");
                break;

            default:
                DebugWindow.LogMsg("The received argument is not a string or an integer.");
                break;
        }

        Core.ParallelRunner.Run(Main.CoroutineWorker);
    }

    public static IEnumerator SwitchToTab(int tabIndex)
    {
        Main.VisibleStashIndex = GetIndexOfCurrentVisibleTab();
        var travelDistance = Math.Abs(tabIndex - Main.VisibleStashIndex);
        if (travelDistance == 0)
            yield break;

        yield return SwitchToTabViaArrowKeys(tabIndex);

        yield return Delay();
    }

    public static IEnumerator SwitchToTabViaArrowKeys(int tabIndex, int numberOfTries = 1)
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

    public static IEnumerator PressKey(Keys key, int repetitions = 1)
    {
        for (var i = 0; i < repetitions; i++)
        {
            yield return Input.KeyPress(key);
        }
    }

    public static IEnumerator Delay(int ms = 0)
    {
        yield return new WaitTime(Main.Settings.ExtraDelay.Value + ms);
    }

    public static InventoryType GetTypeOfCurrentVisibleStash()
    {
        var stashPanelVisibleStash = Main.GameController.Game.IngameState.IngameUi?.StashElement?.VisibleStash;
        return stashPanelVisibleStash?.InvType ?? InventoryType.InvalidInventory;
    }

    public static IEnumerator StashItemsIncrementer()
    {
        Main.CoroutineIteration++;

        yield return StashItems();
    }

    public static IEnumerator StashItems()
    {
        Main.PublishEvent("stashie_start_drop_items", null);

        Main.VisibleStashIndex = GetIndexOfCurrentVisibleTab();
        if (Main.VisibleStashIndex < 0)
        {
            Main.LogMessage($"Stashie: VisibleStashIndex was invalid: {Main.VisibleStashIndex}, stopping.");
            yield break;
        }

        var itemsSortedByStash = Main.DropItems.OrderBy(x => x.SkipSwitchTab || x.StashIndex == Main.VisibleStashIndex ? 0 : 1).ThenBy(x => x.StashIndex).ToList();

        Input.KeyDown(Keys.LControlKey);
        Main.LogMessage($"Want to drop {itemsSortedByStash.Count} items.");
        foreach (var stashResult in itemsSortedByStash)
        {
            Main.CoroutineIteration++;
            Main.CoroutineWorker?.UpdateTicks(Main.CoroutineIteration);
            var maxTryTime = Main.DebugTimer.ElapsedMilliseconds + 2000;

            //move to correct tab
            if (!stashResult.SkipSwitchTab)
                yield return SwitchToTab(stashResult.StashIndex);

            yield return new WaitFunctionTimed(() => Main.GameController.IngameState.IngameUi.StashElement.AllInventories[Main.VisibleStashIndex] != null, true, 2000,
                $"Error while loading tab, Index: {Main.VisibleStashIndex}"); //maybe replace waittime with Setting option

            yield return new WaitFunctionTimed(() => GetTypeOfCurrentVisibleStash() != InventoryType.InvalidInventory, true, 2000,
                $"Error with inventory type, Index: {Main.VisibleStashIndex}"); //maybe replace waittime with Setting option

            yield return StashItem(stashResult);

            Main.DebugTimer.Restart();
            Main.PublishEvent("stashie_finish_drop_items_to_stash_tab", null);
        }
    }

    public static IEnumerator StashItem(FilterResult stashResult)
    {
        Input.SetCursorPos(stashResult.ClickPos + Main.ClickWindowOffset);
        yield return new WaitTime(Main.Settings.HoverItemDelay);
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

        yield return new WaitTime(Main.Settings.StashItemDelay);
    }
}