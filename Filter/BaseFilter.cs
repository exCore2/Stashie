using ItemFilterLibrary;
using System.Collections.Generic;

namespace Stashie
{
    public class BaseFilter : IIFilter
    {
        public bool BAny { get; set; }
        public bool CompareItem(ItemData itemData, ItemQuery itemFilter)
        {
            return itemFilter.Matches(itemData);
        }
    }
}
