using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ExileCore2;
using ExileCore2.Shared;
using static Stashie.StashieCore;

namespace Stashie.Compartments;

public static class TaskRunner
{
    private static readonly ConcurrentDictionary<string, CancellationTokenSource> Tasks = [];

    public static void Run(Func<SyncTask<bool>> task, string name)
    {
        var cts = new CancellationTokenSource();
        Tasks[name] = cts;
        Task.Run(async () =>
        {
            var sTask = task();
            while (sTask != null && !cts.Token.IsCancellationRequested)
            {
                TaskUtils.RunOrRestart(ref sTask, () => null);
                await TaskUtils.NextFrame();
            }

            Tasks.TryRemove(new KeyValuePair<string, CancellationTokenSource>(name, cts));
        });
    }

    public static void Stop(string name)
    {
        if (Tasks.TryGetValue(name, out var cts))
        {
            cts.Cancel();
            Tasks.TryRemove(new KeyValuePair<string, CancellationTokenSource>(name, cts));
        }
    }

    public static bool Has(string name)
    {
        return Tasks.ContainsKey(name);
    }
}

internal class ActionCoRoutine
{
    public static void StartDropItemsToStashCoroutine()
    {
        Main.DebugTimer.Reset();
        Main.DebugTimer.Start();
        TaskRunner.Run(DropToStashRoutine, "Stashie_DropItemsToStash");
    }

    public static void StopCoroutine(string routineName)
    {
        TaskRunner.Stop(routineName);
        Main.DebugTimer.Stop();
        Main.DebugTimer.Reset();
        ActionsHandler.CleanUp();
        Main.PublishEvent("stashie_finish_drop_items_to_stash_tab", null);
    }

    public static async SyncTask<bool> ProcessSwitchToTab(int index)
    {
        Main.DebugTimer.Restart();
        await ActionsHandler.SwitchToTab(index);
        TaskRunner.Stop(CoroutineName);

        Main.DebugTimer.Restart();
        Main.DebugTimer.Stop();
        return true;
    }

    public static async SyncTask<bool> DropToStashRoutine()
    {
        var cursorPosPreMoving = Input.ForceMousePosition;

        //try stashing items 3 times
        var originTab = ActionsHandler.GetIndexOfCurrentVisibleTab();
        await FilterManager.ParseItems();
        for (var tries = 0; tries < 3 && Main.DropItems.Count > 0; ++tries)
        {
            if (Main.DropItems.Count > 0)
                await ActionsHandler.StashItemsIncrementer();

            await FilterManager.ParseItems();
            await Task.Delay(Main.Settings.ExtraDelay);
        }

        if (Main.Settings.VisitTabWhenDone.Value)
        {
            if (Main.Settings.BackToOriginalTab.Value)
                await ActionsHandler.SwitchToTab(originTab);
            else
                await ActionsHandler.SwitchToTab(Main.Settings.TabToVisitWhenDone.Value);
        }

        Input.SetCursorPos(cursorPosPreMoving);
        Input.MouseMove();
        StopCoroutine("Stashie_DropItemsToStash");
        return true;
    }
}