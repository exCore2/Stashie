using System.Collections;
using System.Collections.Generic;

namespace Stashie;

public class FilterEditorContainer
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
        public string FilterName;
        public string RawQuery;
        public bool Shifting = false;
        public bool Affinity = false;
    }
}

public class FilterContainerOld
{
    public class FilterParent : IEnumerable
    {
        public List<ParentMenu> ParentMenu;
        public IEnumerator GetEnumerator()
        {
            yield break;
        }
    }

    public class ParentMenu
    {
        public List<Filter> Filters;
        public string MenuName;
    }

    public class Filter
    {
        public string FilterName;
        public List<string> RawQuery;
        public bool Shifting = false;
        public bool Affinity = false;
    }
}