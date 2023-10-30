using ItemFilterLibrary;

namespace Stashie
{
    public interface IIFilter
    {
        bool CompareItem(ItemData itemData, ItemFilterData filterData);
    }
}
