using ItemFilterLibrary;

namespace Stashie
{
    public interface IIFilter
    {
        bool CompareItem(ItemData itemData, ItemQuery filterData);
    }
}
