using ItemFilterLibrary;
using System.Collections.Generic;

namespace Stashie
{
    public class BaseFilter : IIFilter
    {
        public List<IIFilter> Filters { get; } = new List<IIFilter>();
        public bool BAny { get; set; }
        public bool CompareItem(ItemData itemData, ItemFilterData itemFilter)
        {
            return ItemFilter.Matches(itemData, itemFilter.CompiledQuery);
        }
    }
}
