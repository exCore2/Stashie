using ItemFilterLibrary;
using Stashie.Filter;
using Vector2N = System.Numerics.Vector2;

namespace Stashie.Classes;

public class FilterResult(CustomFilter.Filter filter, ItemData itemData, Vector2N clickPos)
{
    public CustomFilter.Filter Filter { get; } = filter;
    public ItemData ItemData { get; } = itemData;
    public int StashIndex { get; } = filter.StashIndexNode.Index;
    public Vector2N ClickPos { get; } = clickPos;
    public bool SkipSwitchTab { get; } = filter.Affinity ?? false;
    public bool ShiftForStashing { get; } = filter.Shifting ?? false;
}