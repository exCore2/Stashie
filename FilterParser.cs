using ExileCore;
using ItemFilterLibrary;
using System.Collections.Generic;

namespace Stashie
{
    public class FilterParser
    {
        private const string COMMENTSYMBOLHASH = "#";
        private const string COMMENTSYMBOLSLASH = "//";
        private const string SECTIONSPLITSYMBOL = "::";

        public static List<CustomFilter> Parse(string[] filtersLines)
        {
            var allFilters = new List<CustomFilter>();

            // Iterate through each filter line
            for (var i = 0; i < filtersLines.Length; ++i)
            {
                var filterLine = filtersLines[i];

                // Remove any leading tabs from the filter line
                filterLine = filterLine.Replace("\t", "");

                // Skip the current line if it starts with the specified comment symbols or if it's empty
                if (filterLine.StartsWith(COMMENTSYMBOLHASH) || filterLine.StartsWith(COMMENTSYMBOLSLASH)) continue;
                if (filterLine.Replace(" ", "").Length == 0) continue;

                // Split the filter line into sections
                string[] sections = filterLine.Split(SECTIONSPLITSYMBOL);

                // Check if the number of sections is not equal to 5
                if (sections.Length != 5)
                {
                    DebugWindow.LogMsg($"Error: Invalid data format in line {i + 1}. Expected 5 sections separated by ':'", 5);
                    continue;
                }

                // Store the sections in variables
                string filterName = sections[0].Trim();
                string filter = sections[1].Trim();

                // Convert to bool and handle conversion errors
                if (!bool.TryParse(sections[2].Trim(), out bool shifting))
                {
                    DebugWindow.LogMsg($"Error converting 'shifting' in line {i + 1}. Defaulting to 'false'.", 5);
                    shifting = false;
                }

                if (!bool.TryParse(sections[3].Trim(), out bool affinity))
                {
                    DebugWindow.LogMsg($"Error converting 'affinity' in line {i + 1}. Defaulting to 'false'.", 5);
                    affinity = false;
                }

                string parentMenu = sections[4].Trim();

                var newFilter = new CustomFilter
                {
                    Name = filterName,
                    Shifting = shifting,
                    Affinity = affinity,
                    SubmenuName = parentMenu
                };

                newFilter.Query = ItemQuery.Load(filter, filter, i + 1);

                // Check if there was an error during processing and set the flag accordingly
                var filterErrorParse = newFilter.Query.FailedToCompile;

                // Add the parsed filter to the list if no parsing errors were encountered; otherwise, log an error message. FilterLibrary should return an error if it was incorrect anyway.
                if (!filterErrorParse)
                {
                    allFilters.Add(newFilter);
                }
                else
                {
                    DebugWindow.LogError($"Line: {i + 1}", 15);
                }
            }

            // Return the list of all parsed CustomFilter objects
            return allFilters;
        }
    }
}