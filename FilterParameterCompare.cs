using ExileCore.PoEMemory.MemoryObjects;
using System;
using System.Collections.Generic;

namespace Stashie
{
    public class FilterParameterCompare : IIFilter
    {
        public int CompareInt;
        public string CompareString;
        public string CompareAffixString;
        public int CompareAffixModIndex;
        public int CompareAffixValue;
        public Func<ItemData, bool> CompDeleg;
        public Func<ItemData, int> IntParameter;
        public Func<ItemData, string> StringParameter;
        public Func<ItemData, List<ItemMod>> ModsParameter;

        public bool CompareItem(ItemData itemData)
        {
            return CompDeleg(itemData);
        }
    }
}
