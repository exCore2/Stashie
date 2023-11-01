using ExileCore.Shared.Attributes;
using ExileCore.Shared.Interfaces;
using ExileCore.Shared.Nodes;
using System.Collections.Generic;
using System.Windows.Forms;

namespace Stashie
{
    public class StashieSettings : ISettings
    {
        public List<string> AllStashNames = new List<string>();
        public Dictionary<string, ListIndexNode> CustomFilterOptions;
        public Dictionary<string, RangeNode<int>> CustomRefillOptions;


        public StashieSettings()
        {
            Enable = new ToggleNode(false);
            DropHotkey = Keys.F3;
            ExtraDelay = new RangeNode<int>(0, 0, 2000);
            HoverItemDelay = new RangeNode<int>(5, 0, 2000);
            StashItemDelay = new RangeNode<int>(5, 0, 2000);
            BlockInput = new ToggleNode(false);
            AlwaysUseArrow = new ToggleNode(false);
            RefillCurrency = new ToggleNode(false);
            CurrencyStashTab = new ListIndexNode();
            AllowHaveMore = new ToggleNode(false);
            CustomFilterOptions = new Dictionary<string, ListIndexNode>();
            CustomRefillOptions = new Dictionary<string, RangeNode<int>>();
            VisitTabWhenDone = new ToggleNode(false);
            TabToVisitWhenDone = new RangeNode<int>(0, 0, 40);
            BackToOriginalTab = new ToggleNode(false);
        }


        [Menu("Filter File")]
        public ListNode FilterFile { get; set; } = new ListNode();

        [Menu("Stash Hotkey")]
        public HotkeyNode DropHotkey { get; set; }
        [Menu("Extra Delay", "Delay to wait after each inventory clearing attempt(in ms).")]
        public RangeNode<int> ExtraDelay { get; set; }
        [Menu("HoverItem Delay", "Delay used to wait inbetween checks for the Hoveritem (in ms).")]
        public RangeNode<int> HoverItemDelay { get; set; }
        [Menu("StashItem Delay", "Delay used to wait after moving the mouse on an item to Stash until clicking it(in ms).")]
        public RangeNode<int> StashItemDelay { get; set; }

        [Menu("Block Input", "Block user input (except: Ctrl+Alt+Delete) when dropping items to stash.")]
        public ToggleNode BlockInput { get; set; }

        [Menu("When done, go to tab.",
            "After Stashie has dropped all items to their respective tabs, then go to the set tab.")]
        public ToggleNode VisitTabWhenDone { get; set; }

        [Menu("tab (index)")]
        public RangeNode<int> TabToVisitWhenDone { get; set; }
        [Menu("Go back to the tab you were in prior to Stashing")]
        public ToggleNode BackToOriginalTab { get; set; }
        public ToggleNode RefillCurrency { get; set; }
        public ListIndexNode CurrencyStashTab { get; set; }
        public ToggleNode AllowHaveMore { get; set; }

        [Menu("Force arrow key switching", "Always switch stash tabs via keyboard arrows")]
        public ToggleNode AlwaysUseArrow { get; set; }


        public ToggleNode Enable { get; set; }

        public int[,] IgnoredCells { get; set; } =
        {
            {0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1},
            {0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1},
            {0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1},
            {0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1},
            {0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1}
        };

    }
}