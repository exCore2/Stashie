using ImGuiNET;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using static Stashie.StashieCore;
using Vector2N = System.Numerics.Vector2;

namespace Stashie.Compartments;

public class StashieEditorHandler
{
    public const string OverwritePopup = "Overwrite?";
    public const string FilterEditPopup = "Stashie Filter (Multi-Line)";
    public static string _editorGroupFilter = "";
    public static string _editorQueryFilter = "";
    public static string FileSaveName = "";
    public static string SelectedFileName = "";

    public static List<string> _files = [];
    public static FilterEditorContainer.Filter condEditValue = new FilterEditorContainer.Filter();
    public static FilterEditorContainer.Filter tempCondValue = new FilterEditorContainer.Filter();
    public static FilterContainerOld.FilterParent tempConversion = new FilterContainerOld.FilterParent();

    #region Filter Editor Seciton

    public static void ConverterMenu()
    {
        ImGui.TextUnformatted("This does not alter the main settings, this is only a filter file editor");

        ImGui.Spacing();

        if (ImGui.Button("\nConvert Old .ifl To New .json\nOld files will not be altered.\n "))
        {
            foreach (var file in GetFiles(".ifl"))
            {
                if (LoadOldFile(file)) // Assuming this loads into `tempConversion` which is a FilterContainerOld.FilterParent
                {
                    var oldData = tempConversion;
                    var newData = new FilterEditorContainer.FilterParent
                    {
                        ParentMenu = oldData.ParentMenu.Select(pm => new FilterEditorContainer.ParentMenu
                        {
                            MenuName = pm.MenuName,
                            Filters = pm.Filters.Select(f => new FilterEditorContainer.Filter
                            {
                                FilterName = f.FilterName,
                                RawQuery = string.Join("\n", f.RawQuery),
                                Shifting = f.Shifting,
                                Affinity = f.Affinity
                            }).ToList()
                        }).ToList()
                    };

                    // Serialize newData to JSON and save it
                    var newJson = JsonConvert.SerializeObject(newData, Formatting.Indented);
                    File.WriteAllText(Path.Combine(Main.ConfigDirectory, $"{file}.json"), newJson);
                }
                else
                {
                    Main.LogError($"Failed to load file, is it possible its not an older style?\n\t{file}", 15);
                }
            }
        }
    }

    public static void DrawEditorMenu()
    {
        if (Main.Settings.CurrentFilterOptions.ParentMenu == null) return;

        var tempFilters = new List<FilterEditorContainer.ParentMenu>(Main.Settings.CurrentFilterOptions.ParentMenu);

        if (!ImGui.CollapsingHeader($"Filters##{Main.Name}Load / Save", ImGuiTreeNodeFlags.DefaultOpen)) return;

        #region Parent

        ImGui.Indent();

        ImGui.InputTextWithHint("Filter Groups", "Group...", ref _editorGroupFilter, 100);
        ImGui.InputTextWithHint("Filter Queries", "Query...", ref _editorQueryFilter, 100);

        for (var parentIndex = 0; parentIndex < tempFilters.Count; parentIndex++)
        {
            var currentParent = tempFilters[parentIndex];
            if (!currentParent.MenuName.Contains(_editorGroupFilter, StringComparison.InvariantCultureIgnoreCase)) continue;
            if (currentParent.Filters.All(x => !x.FilterName.Contains(_editorQueryFilter, StringComparison.InvariantCultureIgnoreCase))) continue;

            ImGui.BeginChild($"##parentFilterGroup_{parentIndex}", Vector2N.Zero, ImGuiChildFlags.Border | ImGuiChildFlags.AutoResizeY);

            ImGui.InputTextWithHint("Group Name", "\"Heist Items\" etc..", ref tempFilters[parentIndex].MenuName, 200);

            #region Parents Filters

            ImGui.Indent();
            ImGui.BeginChild($"##parentFilterGroup_{parentIndex}", Vector2N.Zero, ImGuiChildFlags.Border | ImGuiChildFlags.AutoResizeY);

            #region Filter Query

            for (var filterIndex = 0; filterIndex < tempFilters[parentIndex].Filters.Count; filterIndex++)
            {
                var currentFilter = currentParent.Filters[filterIndex];
                if (!currentFilter.FilterName.ToLowerInvariant().Contains(_editorQueryFilter)) continue;
                ImGui.InputTextWithHint($"##filter_{parentIndex}_{filterIndex}", "\"Heist Items\" etc..", ref tempFilters[parentIndex].Filters[filterIndex].FilterName, 200);

                ImGui.SameLine();
                ImGui.Checkbox($"Shifting##checkbox_{parentIndex}_{filterIndex}", ref currentFilter.Shifting);
                ImGui.SameLine();
                ImGui.Checkbox($"Affinity##checkbox_{parentIndex}_{filterIndex}", ref currentFilter.Affinity);

                #region Edit Popup

                const bool showPopup = true;
                ImGui.SameLine();
                if (ImGui.Button($"Edit##filterQueryEdit_{parentIndex}_{filterIndex}"))
                {
                    condEditValue = new FilterEditorContainer.Filter
                        {FilterName = currentFilter.FilterName, Affinity = currentFilter.Affinity, RawQuery = currentFilter.RawQuery, Shifting = currentFilter.Shifting};

                    tempCondValue = new FilterEditorContainer.Filter
                        {FilterName = currentFilter.FilterName, Affinity = currentFilter.Affinity, RawQuery = currentFilter.RawQuery, Shifting = currentFilter.Shifting};

                    ImGui.OpenPopup(FilterEditPopup + $"##conditionalEditPopup_{parentIndex}_{filterIndex}");
                }

                ConditionValueEditPopup(showPopup, parentIndex, filterIndex, tempFilters);

                #endregion

                ImGui.SameLine();
                if (ImGui.Button($"Delete##filterQueryEdit_{parentIndex}_{filterIndex}"))
                {
                    tempFilters[parentIndex].Filters.RemoveAt(filterIndex);
                }
            }

            if (ImGui.Button($"[=] Add New Filter##AddNewFilter_{parentIndex}"))
            {
                tempFilters[parentIndex].Filters.Add(new FilterEditorContainer.Filter {FilterName = "", RawQuery = "", Affinity = false, Shifting = false});
            }

            #endregion

            ImGui.EndChild();
            ImGui.Unindent();

            if (ImGui.Button($"[X] Delete Group##DeleteGroup_{parentIndex}"))
            {
                tempFilters.RemoveAt(parentIndex);
            }

            #endregion

            ImGui.Unindent();
            ImGui.EndChild();
            ImGui.Spacing();
        }

        ImGui.Unindent();
        if (ImGui.Button("[=] Add New Group"))
        {
            tempFilters.Add(new FilterEditorContainer.ParentMenu {MenuName = "", Filters = [new FilterEditorContainer.Filter {FilterName = "", RawQuery = "", Affinity = false, Shifting = false}]});
        }

        #endregion

        if (ShowButtonPopup(OverwritePopup, ["Are you sure?", "STOP"], out var saveSelectedIndex))
        {
            if (saveSelectedIndex == 0)
            {
                SaveFile(Main.Settings.CurrentFilterOptions, $"{FileSaveName}.json");
            }
        }

        Main.Settings.CurrentFilterOptions.ParentMenu = tempFilters;
    }

    public static void ConditionValueEditPopup(bool showPopup, int parentIndex, int filterIndex, List<FilterEditorContainer.ParentMenu> parentMenu)
    {
        ImGui.SetNextWindowSize(new Vector2N(800, 600), ImGuiCond.Appearing);
        if (!ImGui.BeginPopupModal(FilterEditPopup + $"##conditionalEditPopup_{parentIndex}_{filterIndex}", ref showPopup, ImGuiWindowFlags.NoSavedSettings))
        {
            return;
        }

        if (ImGui.Button("Save"))
        {
            parentMenu[parentIndex].Filters[filterIndex] = tempCondValue;
            ImGui.CloseCurrentPopup();
        }

        ImGui.SameLine();

        if (ImGui.Button("Revert"))
        {
            tempCondValue = new FilterEditorContainer.Filter
                {FilterName = condEditValue.FilterName, Affinity = condEditValue.Affinity, RawQuery = condEditValue.RawQuery, Shifting = condEditValue.Shifting};

            Main.LogMessage("Reverting data back..", 7);
        }

        ImGui.SameLine();

        if (ImGui.Button("Close"))
        {
            ImGui.CloseCurrentPopup();
        }

        ImGui.Checkbox("Shifting", ref tempCondValue.Shifting);
        ImGui.Checkbox("Affinity", ref tempCondValue.Affinity);

        ImGui.InputTextMultiline($"##text{parentIndex}_{filterIndex}", ref tempCondValue.RawQuery, 15000, new Vector2N(ImGui.GetContentRegionAvail().X, ImGui.GetContentRegionAvail().Y),
            ImGuiInputTextFlags.AllowTabInput);

        ImGui.EndPopup();
    }

    public static void SaveLoadMenu()
    {
        if (!ImGui.CollapsingHeader($"Load / Save##{Main.Name}Load / Save", ImGuiTreeNodeFlags.DefaultOpen)) return;

        ImGui.Indent();
        ImGui.InputTextWithHint("##SaveAs", "File Path...", ref FileSaveName, 100);
        ImGui.SameLine();

        if (ImGui.Button("Save To File"))
        {
            _files = GetFiles(".json");

            // Sanitize the file name by replacing invalid characters
            foreach (var c in Path.GetInvalidFileNameChars()) FileSaveName = FileSaveName.Replace(c, '_');

            if (FileSaveName == string.Empty)
            {
            }
            else if (_files.Contains(FileSaveName))
            {
                ImGui.OpenPopup(OverwritePopup);
            }
            else
            {
                SaveFile(Main.Settings.CurrentFilterOptions, $"{FileSaveName}.json");
            }
        }

        ImGui.Separator();

        if (ImGui.BeginCombo("Load File##LoadNewFile", SelectedFileName))
        {
            _files = GetFiles(".json");

            foreach (var fileName in _files)
            {
                var isSelected = SelectedFileName == fileName;

                if (ImGui.Selectable(fileName, isSelected))
                {
                    SelectedFileName = fileName;
                    FileSaveName = fileName;
                    LoadNewFile(fileName);
                }

                if (isSelected)
                {
                    ImGui.SetItemDefaultFocus();
                }
            }

            ImGui.EndCombo();
        }

        ImGui.Separator();

        if (ImGui.Button("Open Filter Folder"))
        {
            var configDir = Path.Combine(Path.GetDirectoryName(Main.ConfigDirectory), "Stashie");

            if (!Directory.Exists(configDir))
            {
                Main.LogError($"Path Doesnt Exist\n{configDir}");
            }
            else
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = configDir
                });
            }
        }

        ImGui.Unindent();
    }

    public static bool ShowButtonPopup(string popupId, List<string> items, out int selectedIndex)
    {
        selectedIndex = -1;
        var isItemClicked = false;
        var showPopup = true;

        if (!ImGui.BeginPopupModal(popupId, ref showPopup, ImGuiWindowFlags.NoResize | ImGuiWindowFlags.AlwaysAutoResize))
        {
            return false;
        }

        for (var i = 0; i < items.Count; i++)
        {
            if (ImGui.Button(items[i]))
            {
                selectedIndex = i;
                isItemClicked = true;
                ImGui.CloseCurrentPopup();
            }

            ImGui.SameLine();
        }

        ImGui.EndPopup();
        return isItemClicked;
    }

    public static void SaveFile(FilterEditorContainer.FilterParent input, string filePath)
    {
        try
        {
            var fullPath = Path.Combine(Main.ConfigDirectory, filePath);
            var jsonString = JsonConvert.SerializeObject(input, Formatting.Indented);
            File.WriteAllText(fullPath, jsonString);
            Main.LogMessage($"Successfully saved file to {fullPath}.", 8);
        }
        catch (Exception e)
        {
            var fullPath = Path.Combine(Main.ConfigDirectory, filePath);

            Main.LogError($"Error saving file to {fullPath}: {e.Message}", 15);
        }
    }

    public static bool LoadOldFile(string fileName)
    {
        try
        {
            var fullPath = Path.Combine(Main.ConfigDirectory, $"{fileName}.ifl");
            var fileContent = File.ReadAllLines(fullPath);

            // Preprocess the content to remove comments
            var contentWithoutComments = RemoveComments(fileContent);

            tempConversion = JsonConvert.DeserializeObject<FilterContainerOld.FilterParent>(contentWithoutComments);

            return true;
        }
        catch (Exception e)
        {
            var fullPath = Path.Combine(Main.ConfigDirectory, $"{fileName}.ifl");
            Main.LogError($"Error loading file from {fullPath}:\n{e.Message}", 15);
            return false;
        }
    }

    public static void LoadNewFile(string fileName)
    {
        try
        {
            var fullPath = Path.Combine(Main.ConfigDirectory, $"{fileName}.json");
            var fileContent = File.ReadAllLines(fullPath);

            // Preprocess the content to remove comments
            var contentWithoutComments = RemoveComments(fileContent);

            Main.Settings.CurrentFilterOptions = JsonConvert.DeserializeObject<FilterEditorContainer.FilterParent>(contentWithoutComments);
        }
        catch (Exception e)
        {
            var fullPath = Path.Combine(Main.ConfigDirectory, $"{fileName}.json");
            Main.LogError($"Error loading file from {fullPath}:\n{e.Message}", 15);
        }
    }

    public static string RemoveComments(string[] input)
    {
        var cleanedLines = new List<string>();

        foreach (var line in input)
        {
            var commentIndex = line.IndexOf("//", StringComparison.CurrentCultureIgnoreCase);
            if (commentIndex == -1)
            {
                cleanedLines.Add(line);
            }
            else
            {
                var trimmedLine = line[..commentIndex].Trim();
                if (!string.IsNullOrWhiteSpace(trimmedLine))
                {
                    cleanedLines.Add(trimmedLine);
                }
            }
        }

        return string.Join(Environment.NewLine, cleanedLines);
    }

    public static List<string> GetFiles(string extension)
    {
        var fileList = new List<string>();

        try
        {
            var dir = new DirectoryInfo(Path.Combine(Path.GetDirectoryName(Main.ConfigDirectory), "Stashie"));

            fileList = dir.GetFiles().Where(file => file.Extension.Equals(extension, StringComparison.CurrentCultureIgnoreCase)).Select(file => Path.GetFileNameWithoutExtension(file.Name)).ToList();
        }
        catch
        {
            // no
        }

        return fileList;
    }

    #endregion
}