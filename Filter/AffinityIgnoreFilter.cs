using ItemFilterLibrary;

namespace Stashie
{
    internal class AffinityIgnoreFilter : IIFilter
    {
        public bool BAffinityIgnore;
        public bool CompareItem(ItemData itemData, ItemFilterData filterData)
        {
            return true;
        }
    }
}