using System.Collections.Generic;
using ExileCore;
using SharpDX;
using ItemFilterLibrary;

namespace Stashie
{
    public class FilterParser
    {
        private const char SYMBOL_COMMANDSDIVIDE = ',';
        private const char SYMBOL_COMMAND_FILTER_OR = '|';
        private const char SYMBOL_NAMEDIVIDE = ':';
        private const char SYMBOL_SUBMENUNAME = ':';
        private const string COMMENTSYMBOL = "#";
        private const string COMMENTSYMBOLALT = "//";

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
                if (filterLine.StartsWith(COMMENTSYMBOL)) continue;
                if (filterLine.StartsWith(COMMENTSYMBOLALT)) continue;
                if (filterLine.Replace(" ", "").Length == 0) continue;

                // Find the index of the symbol that divides the filter name and commands
                var nameIndex = filterLine.IndexOf(SYMBOL_NAMEDIVIDE);

                // Log an error message and skip to the next line if the namedivide symbol is not found
                if (nameIndex == -1)
                {
                    DebugWindow.LogMsg("Filter parser: Can't find filter name in line: " + (i + 1), 5);
                    continue;
                }

                // Create a new CustomFilter object and populate its properties
                var newFilter = new CustomFilter { Name = filterLine.Substring(0, nameIndex).Trim(), Index = i + 1 };

                // Extract filter commands from the filter line
                var filterCommandsLine = filterLine.Substring(nameIndex + 1);

                // Process the SubmenuName if it exists
                var submenuIndex = filterCommandsLine.IndexOf(SYMBOL_SUBMENUNAME);

                if (submenuIndex != -1)
                {
                    newFilter.SubmenuName = filterCommandsLine.Substring(submenuIndex + 1);
                    filterCommandsLine = filterCommandsLine.Substring(0, submenuIndex);
                }

                newFilter.Query = ItemFilter.LoadFromStringWithLine(filterCommandsLine, nameIndex + 1);

                // Check if there was an error during processing and set the flag accordingly
                var filterErrorParse = newFilter.Query.FailedToCompile;

                // Add the parsed filter to the list if no parsing errors were encountered; otherwise, log an error message
                if (!filterErrorParse)
                {
                    allFilters.Add(newFilter);
                }
                else
                {
                    DebugWindow.LogMsg($"Line: {i + 1}", 5, Color.Red);
                }
            }

            // Return the list of all parsed CustomFilter objects
            return allFilters;
        }


    }
}