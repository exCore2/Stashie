using ItemFilterLibrary;

namespace Stashie
{
    public class CustomFilter : BaseFilter
    {
        public string Name { get; set; }
        public ListIndexNode StashIndexNode { get; set; }
        public string SubmenuName { get; set; }
        public ItemFilterData Query { get; set; }
        public int Index { get; set; }
        public bool AllowProcess => StashIndexNode.Index != -1;
        public bool Shifting { get; set; }
        public bool Affinity { get; set; }
    }
}
