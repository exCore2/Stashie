using ItemFilterLibrary;
using Vector2N = System.Numerics.Vector2;

namespace Stashie
{
    public class FilterResult
    {
        public FilterResult(CustomFilter.Filter filter, ItemData itemData, Vector2N clickPos)
        {
            Filter = filter;
            ItemData = itemData;
            StashIndex = filter.StashIndexNode.Index;
            ClickPos = clickPos;
            SkipSwitchTab = filter.Affinity ?? false;
            ShiftForStashing = filter.Shifting ?? false;
        }

        public CustomFilter.Filter Filter { get; }
        public ItemData ItemData { get; }
        public int StashIndex { get; }
        public Vector2N ClickPos { get; }
        public bool SkipSwitchTab { get; }
        public bool ShiftForStashing { get; }
    }
}
