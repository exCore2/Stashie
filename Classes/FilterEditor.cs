using System.Collections.Generic;

namespace Stashie.Classes;

public class FilterEditor
{
    public class FilterParent
    {
        public List<ParentMenu> ParentMenu;
    }

    public class ParentMenu
    {
        public List<Filter> Filters;
        public string MenuName;
    }

    public class Filter
    {
        public bool Affinity = false;
        public string FilterName;
        public string RawQuery;
        public bool Shifting = false;
    }
}

public class FilterEditorOld
{
    public class FilterParent
    {
        public List<ParentMenu> ParentMenu;
    }

    public class ParentMenu
    {
        public List<Filter> Filters;
        public string MenuName;
    }

    public class Filter
    {
        public bool Affinity = false;
        public string FilterName;
        public List<string> RawQuery;
        public bool Shifting = false;
    }
}