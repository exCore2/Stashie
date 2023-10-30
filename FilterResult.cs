using SharpDX;
using ItemFilterLibrary;
using ExileCore.Shared.Helpers;

namespace Stashie
{
    public class FilterResult
    {
        public FilterResult(CustomFilter filter, ItemData itemData)
        {
            Filter = filter;
            ItemData = itemData;
            StashIndex = filter.StashIndexNode.Index;
            ClickPos = itemData.CachedClickPosition.ToSharpDx();
            // TODO: affinity + shifting
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
