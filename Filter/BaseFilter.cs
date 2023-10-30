using System.Collections.Generic;
using JM.LinqFaster;
using ItemFilterLibrary;

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
