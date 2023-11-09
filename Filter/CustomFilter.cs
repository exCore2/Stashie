using ItemFilterLibrary;
using System.Collections.Generic;

namespace Stashie
{
    public class CustomFilter : BaseFilter
    {
        public string ParentMenuName { get; set; }
        public ListIndexNode StashIndexNode { get; set; }
        public bool AllowProcess => StashIndexNode.Index != -1;
    }
}
