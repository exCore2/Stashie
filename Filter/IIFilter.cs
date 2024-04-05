using ItemFilterLibrary;

namespace Stashie.Filter;

public interface IIFilter
{
    bool CompareItem(ItemData itemData, ItemQuery filterData);
}