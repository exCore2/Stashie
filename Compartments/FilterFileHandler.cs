using System;
using System.Collections.Generic;
using System.IO;
using ExileCore2;
using ItemFilterLibrary;
using Newtonsoft.Json;
using Stashie.Classes;
using Stashie.Filter;

namespace Stashie.Compartments;

public class FilterFileHandler
{
    public static List<CustomFilter> Load(string fileName, string filePath)
    {
        List<CustomFilter> allFilters = [];

        try
        {
            var fileContents = File.ReadAllText(filePath);

            var newFilters = JsonConvert.DeserializeObject<IFL.Parent>(fileContents);

            foreach (var parentMenu in newFilters.ParentMenu)
            {
                var newParent = new CustomFilter
                {
                    ParentMenuName = parentMenu.MenuName
                };

                foreach (var filter in parentMenu.Filters)
                {
                    var compiledQuery = ItemQuery.Load(filter.RawQuery.Replace("\n", ""));
                    var filterErrorParse = compiledQuery.FailedToCompile;

                    if (filterErrorParse)
                        DebugWindow.LogError(
                            $"[Stashie] JSON Error loading. Parent: {parentMenu.MenuName}, Filter: {filter.FilterName}",
                            15);
                    else
                        newParent.Filters.Add(new CustomFilter.Filter
                        {
                            FilterName = filter.FilterName,
                            RawQuery = filter.RawQuery,
                            Shifting = filter.Shifting ?? false,
                            Affinity = filter.Affinity ?? false,
                            CompiledQuery = compiledQuery
                        });
                }

                if (newParent.Filters.Count > 0)
                    allFilters.Add(newParent);
            }
        }
        catch (Exception ex)
        {
            DebugWindow.LogError($"[Stashie] Failed Loading filter {fileName}\nException: {ex.Message}", 15);
        }

        return allFilters;
    }
}