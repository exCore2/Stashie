using ExileCore;
using ItemFilterLibrary;
using Newtonsoft.Json;
using Stashie.Filter;
using System;
using System.Collections.Generic;
using System.IO;

namespace Stashie.Compartments;

public class FilterParser
{
    public static List<CustomFilter> Load(string fileName, string filePath)
    {
        List<CustomFilter> allFilters = [];

        try
        {
            var fileContents = File.ReadAllText(filePath);

            var newFilters = JsonConvert.DeserializeObject<IFL>(fileContents);

            foreach (var parentMenu in newFilters.ParentMenu)
            {
                var newParent = new CustomFilter
                {
                    ParentMenuName = parentMenu.MenuName
                };

                foreach (var filter in parentMenu.Filters)
                {
                    var compiledQuery = ItemQuery.Load(filter.RawQuery.Replace("\n", ""));

                    // Check if there was an error during processing and set the flag accordingly
                    var filterErrorParse = compiledQuery.FailedToCompile;

                    // Add the parsed filter to the list if no parsing errors were encountered; otherwise, log an error message. FilterLibrary should return an error if it was incorrect anyway.
                    if (filterErrorParse)
                    {
                        DebugWindow.LogError($"[Stashie] JSON Error loading. Parent: {parentMenu.MenuName}, Filter: {filter.FilterName}", 15);
                    }
                    else
                    {
                        newParent.Filters.Add(new CustomFilter.Filter
                        {
                            FilterName = filter.FilterName,
                            RawQuery = filter.RawQuery,
                            Shifting = filter.Shifting ?? false,
                            Affinity = filter.Affinity ?? false,
                            CompiledQuery = compiledQuery
                        });
                    }
                }

                if (newParent.Filters.Count > 0) allFilters.Add(newParent);
            }
        }
        catch (Exception ex)
        {
            DebugWindow.LogError($"[Stashie] Failed Loading filter {fileName}\nException: {ex.Message}", 15);
        }

        // Return the list of all parsed CustomFilter objects
        return allFilters;
    }

    public class IFL
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