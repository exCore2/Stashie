using ExileCore;
using ExileCore.Shared;
using System.Collections;
using static Stashie.StashieCore;

namespace Stashie.Compartments;

internal class ActionCoRoutine
{
    public static void StartDropItemsToStashCoroutine()
    {
        Main.DebugTimer.Reset();
        Main.DebugTimer.Start();
        Core.ParallelRunner.Run(new Coroutine(DropToStashRoutine(), Main, "Stashie_DropItemsToStash"));
    }

    public static void StopCoroutine(string routineName)
    {
        var routine = Core.ParallelRunner.FindByName(routineName);
        routine?.Done();
        Main.DebugTimer.Stop();
        Main.DebugTimer.Reset();
        ActionsHandler.CleanUp();
        Main.PublishEvent("stashie_finish_drop_items_to_stash_tab", null);
    }

    public static IEnumerator ProcessSwitchToTab(int index)
    {
        Main.DebugTimer.Restart();
        yield return ActionsHandler.SwitchToTab(index);
        Main.CoroutineWorker = Core.ParallelRunner.FindByName(CoroutineName);
        Main.CoroutineWorker?.Done();

        Main.DebugTimer.Restart();
        Main.DebugTimer.Stop();
    }

    public static IEnumerator DropToStashRoutine()
    {
        var cursorPosPreMoving = Input.ForceMousePositionNum;

        //try stashing items 3 times
        var originTab = ActionsHandler.GetIndexOfCurrentVisibleTab();
        yield return FilterManager.ParseItems();
        for (var tries = 0; tries < 3 && Main.DropItems.Count > 0; ++tries)
        {
            if (Main.DropItems.Count > 0)
                yield return ActionsHandler.StashItemsIncrementer();

            yield return FilterManager.ParseItems();
            yield return new WaitTime(Main.Settings.ExtraDelay);
        }

        if (Main.Settings.VisitTabWhenDone.Value)
        {
            if (Main.Settings.BackToOriginalTab.Value)
            {
                yield return ActionsHandler.SwitchToTab(originTab);
            }
            else
            {
                yield return ActionsHandler.SwitchToTab(Main.Settings.TabToVisitWhenDone.Value);
            }
        }

        Input.SetCursorPos(cursorPosPreMoving);
        Input.MouseMove();
        StopCoroutine("Stashie_DropItemsToStash");
    }
}