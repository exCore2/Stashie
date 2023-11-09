using ItemFilterLibrary;
using System.Collections.Generic;

namespace Stashie
{
    public class BaseFilter : IIFilter
    {
        public bool BAny { get; set; }
        public List<Filter> Filters { get; set; } = new List<Filter>();
        public bool CompareItem(ItemData itemData, ItemQuery itemFilter)
        {
            return itemFilter.Matches(itemData);
        }

        public partial class Filter
        {
            public string FilterName { get; set; }
            public bool? Shifting { get; set; }
            public bool? Affinity { get; set; }
            public string RawQuery { get; set; }
            public ItemQuery CompiledQuery { get; set; }
        }
    }
}
