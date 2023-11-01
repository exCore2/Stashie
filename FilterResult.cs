using ItemFilterLibrary;
using SharpDX;

namespace Stashie
{
    public class FilterResult
    {
        public FilterResult(CustomFilter filter, ItemData itemData, Vector2 clickPos)
        {
            Filter = filter;
            ItemData = itemData;
            StashIndex = filter.StashIndexNode.Index;
            ClickPos = clickPos;
            SkipSwitchTab = filter.Affinity;
            ShiftForStashing = filter.Shifting;
        }

        public CustomFilter Filter { get; }
        public ItemData ItemData { get; }
        public int StashIndex { get; }
        public Vector2 ClickPos { get; }
        public bool SkipSwitchTab { get; }
        public bool ShiftForStashing { get; }
    }
}
