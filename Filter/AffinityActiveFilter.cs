using ItemFilterLibrary;

namespace Stashie
{
    internal class AffinityActiveFilter : IIFilter
    {
        public bool BAffinityActive;
        public bool CompareItem(ItemData itemData, ItemFilterData filterData)
        {
            return true;
        }
    }
}