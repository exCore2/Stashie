using System.Collections.Generic;

namespace Stashie.Classes;

public class IFL
{
    public class Parent
    {
        public ParentMenu[] ParentMenu { get; set; }
    }

    public class ParentMenu
    {
        public string MenuName { get; set; }

        public List<Filter> Filters { get; set; }
    }

    public class Filter
    {
        public string FilterName { get; set; }

        public string RawQuery { get; set; }

        public bool? Shifting { get; set; }

        public bool? Affinity { get; set; }
    }
}