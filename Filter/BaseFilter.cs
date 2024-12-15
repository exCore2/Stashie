using ItemFilterLibrary;

namespace Stashie.Filter;

public class BaseFilter : IIFilter
{
    public bool BAny { get; set; }

    public bool CompareItem(ItemData itemData, ItemQuery itemFilter)
    {
        return itemFilter.Matches(itemData);
    }
}