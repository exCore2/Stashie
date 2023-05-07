using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stashie.Filter
{
    public class ModsFilter : IIFilter
    {
        public bool inverse;
        public string mod;
        public bool CompareItem(ItemData itemData)
        {
            if (inverse)
            {
                return !itemData.ModsNames.Contains(mod);
            }
            else
            {
                return itemData.ModsNames.Contains(mod);
            }
        }
    }
}
