using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using ExileCore2;
using ExileCore2.Shared;
using ExileCore2.Shared.Enums;
using Stashie.Classes;
using static Stashie.StashieCore;

namespace Stashie.Compartments;

internal class ActionsHandler
{
    public static int GetIndexOfCurrentVisibleTab()
    {
        return Main.GameController.Game.IngameState.IngameUi.StashElement.IndexVisibleStash;
    }

    public static void CleanUp()
    {
        Input.KeyUp(Keys.LControlKey);
        Input.KeyUp(Keys.Shift);
    }

    public static void HandleSwitchToTabEvent(object tab)
    {
        Func<SyncTask<bool>> task = null;
        switch (tab)
        {
            case int index:
                task = () => ActionCoRoutine.ProcessSwitchToTab(index);
                break;

            case string name:
                if (!RenamedAllStashNames.Contains(name))
                {
                    DebugWindow.LogMsg($"{Main.Name}: can't find tab with name '{name}'.");
                    break;
                }

                var tempIndex = RenamedAllStashNames.IndexOf(name);
                task = () => ActionCoRoutine.ProcessSwitchToTab(tempIndex);
                DebugWindow.LogMsg($"{Main.Name}: Switching to tab with index: {tempIndex} ('{name}').");
                break;

            default:
                DebugWindow.LogMsg("The received argument is not a string or an integer.");
                break;
        }

        if (task != null) TaskRunner.Run(task, CoroutineName);
    }

    public static async SyncTask<bool> SwitchToTab(int tabIndex)
    {
        Main.VisibleStashIndex = GetIndexOfCurrentVisibleTab();
        var travelDistance = Math.Abs(tabIndex - Main.VisibleStashIndex);
        if (travelDistance == 0)
            return true;

        await SwitchToTabViaArrowKeys(tabIndex);

        await Delay();
        return true;
    }

    public static async SyncTask<bool> SwitchToTabViaArrowKeys(int tabIndex, int numberOfTries = 1)
    {
        if (numberOfTries >= 3) return true;

        var indexOfCurrentVisibleTab = GetIndexOfCurrentVisibleTab();
        var travelDistance = tabIndex - indexOfCurrentVisibleTab;
        var tabIsToTheLeft = travelDistance < 0;
        travelDistance = Math.Abs(travelDistance);

        if (tabIsToTheLeft)
            await PressKey(Keys.Left, travelDistance);
        else
            await PressKey(Keys.Right, travelDistance);

        if (GetIndexOfCurrentVisibleTab() != tabIndex)
        {
            await Delay(20);
            await SwitchToTabViaArrowKeys(tabIndex, numberOfTries + 1);
        }

        return true;
    }

    public static async SyncTask<bool> PressKey(Keys key, int repetitions = 1)
    {
        for (var i = 0; i < repetitions; i++)
        {
            Input.KeyDown(key);
            await Task.Delay(10);
            Input.KeyUp(key);
            await Task.Delay(10);
        }

        return true;
    }

    public static async SyncTask<bool> Delay(int ms = 0)
    {
        await Task.Delay(Main.Settings.ExtraDelay.Value + ms);
        return true;
    }

    public static InventoryType GetTypeOfCurrentVisibleStash()
    {
        var stashPanelVisibleStash = Main.GameController.Game.IngameState.IngameUi?.StashElement?.VisibleStash;
        return stashPanelVisibleStash?.InvType ?? InventoryType.InvalidInventory;
    }

    public static async SyncTask<bool> StashItemsIncrementer()
    {
        await StashItems();
        return true;
    }

    public static async SyncTask<bool> StashItems()
    {
        Main.PublishEvent("stashie_start_drop_items", null);

        Main.VisibleStashIndex = GetIndexOfCurrentVisibleTab();
        if (Main.VisibleStashIndex < 0)
        {
            Main.LogMessage($"Stashie: VisibleStashIndex was invalid: {Main.VisibleStashIndex}, stopping.");
            return true;
        }

        var itemsSortedByStash = Main.DropItems
            .OrderBy(x => x.SkipSwitchTab || x.StashIndex == Main.VisibleStashIndex ? 0 : 1).ThenBy(x => x.StashIndex)
            .ToList();

        Input.KeyDown(Keys.LControlKey);
        Main.LogMessage($"Want to drop {itemsSortedByStash.Count} items.");
        foreach (var stashResult in itemsSortedByStash)
        {
            //move to correct tab
            if (!stashResult.SkipSwitchTab)
                await SwitchToTab(stashResult.StashIndex);

            await TaskUtils.CheckEveryFrameWithThrow(
                () => Main.GameController.IngameState.IngameUi.StashElement.AllInventories[Main.VisibleStashIndex] !=
                      null,
                new CancellationTokenSource(Main.Settings.StashingCancelTimer.Value).Token);
            //maybe replace waittime with Setting option

            await TaskUtils.CheckEveryFrameWithThrow(
                () => GetTypeOfCurrentVisibleStash() != InventoryType.InvalidInventory,
                new CancellationTokenSource(Main.Settings.StashingCancelTimer.Value).Token);
            //maybe replace waittime with Setting option

            await StashItem(stashResult);

            Main.DebugTimer.Restart();
        }

        return true;
    }

    public static async SyncTask<bool> StashItem(FilterResult stashResult)
    {
        Input.SetCursorPos(stashResult.ClickPos + Main.ClickWindowOffset);
        await Task.Delay(Main.Settings.HoverItemDelay);
        var shiftUsed = false;
        if (stashResult.ShiftForStashing)
        {
            Input.KeyDown(Keys.ShiftKey);
            shiftUsed = true;
        }

        Input.Click(MouseButtons.Left);
        if (shiftUsed) Input.KeyUp(Keys.ShiftKey);

        await Task.Delay(Main.Settings.StashItemDelay);
        return true;
    }
}