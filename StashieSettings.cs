using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Windows.Forms;
using ExileCore2.Shared.Attributes;
using ExileCore2.Shared.Interfaces;
using ExileCore2.Shared.Nodes;
using Stashie.Classes;

namespace Stashie;

public class StashieSettings : ISettings
{
    public List<string> AllStashNames = [];
    public Dictionary<string, ListIndexNode> CustomFilterOptions = [];

    [Menu("Filter File")] public ListNode FilterFile { get; set; } = new();

    [Menu("Stash Hotkey")] public HotkeyNode DropHotkey { get; set; } = Keys.F3;

    [JsonIgnore]
    [Menu("Debug Inspect Items", "Inspect inventory items as ItemFilterLibrary items.")]
    public ToggleNode InspectInventoryItems { get; set; } = new(false);

    [Menu("Extra Delay", "Delay to wait after each inventory clearing attempt(in ms).")]
    public RangeNode<int> ExtraDelay { get; set; } = new(25, 0, 2000);

    [Menu("Stashing Cancel Timer", "Time to wait while stashing before canceling (in ms).")]
    public RangeNode<int> StashingCancelTimer { get; set; } = new(2000, 0, 15000);

    [Menu("HoverItem Delay", "Delay used to wait between checks for the Hover item (in ms).")]
    public RangeNode<int> HoverItemDelay { get; set; } = new(25, 0, 2000);

    [Menu("StashItem Delay", "Delay used to wait after moving the mouse on an item to Stash until clicking it(in ms).")]
    public RangeNode<int> StashItemDelay { get; set; } = new(25, 0, 2000);

    [Menu("When done, go to tab.",
        "After Stashie has dropped all items to their respective tabs, then go to the set tab.")]
    public ToggleNode VisitTabWhenDone { get; set; } = new(false);

    [Menu("tab (index)")] public RangeNode<int> TabToVisitWhenDone { get; set; } = new(0, 0, 40);

    [Menu("Go back to the tab you were in prior to Stashing")]
    public ToggleNode BackToOriginalTab { get; set; } = new(false);

    [Menu("Keep Highest TP Scroll Stack")] public ToggleNode KeepHighestTPStack { get; set; } = new(false);

    [Menu("Keep Highest ID Scroll Stack")] public ToggleNode KeepHighestIDStack { get; set; } = new(false);

    public int[,] IgnoredCells { get; set; } = new int[5, 12];

    public int[,] IgnoredExpandedCells { get; set; } = new int[5, 4];

    public string ConfigLastSaved { get; set; } = "";
    public string ConfigLastSelected { get; set; } = "";

    public FilterEditor.FilterParent CurrentFilterOptions { get; set; } = new();

    public ToggleNode Enable { get; set; } = new(false);
}