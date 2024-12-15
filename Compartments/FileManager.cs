using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace Stashie.Compartments;

public static class FileManager
{
    private static string GetFullPath(string fileName, string extension = "")
    {
        return Path.Combine(StashieCore.Main.ConfigDirectory, $"{fileName}{extension}");
    }

    private static void HandleException(Exception e, string fullPath, bool isSaving, int logLevel = 15)
    {
        var operation = isSaving ? "saving" : "loading";
        StashieCore.Main.LogError($"Error {operation} file at {fullPath}:\n{e.Message}", logLevel);
    }

    public static void SaveToFile<T>(T objectToSave, string fileName, string extension = ".json")
    {
        var fullPath = GetFullPath(fileName, extension);
        try
        {
            var jsonString = JsonConvert.SerializeObject(objectToSave, Formatting.Indented);
            File.WriteAllText(fullPath, jsonString);
            StashieCore.Main.LogMessage($"Successfully saved file to {fullPath}.", 8);
        }
        catch (Exception e)
        {
            HandleException(e, fullPath, true);
        }
    }

    public static bool TryLoadFile<T>(string fileName, string extension, Action<T> onSuccess)
    {
        var fullPath = GetFullPath(fileName, extension);
        try
        {
            var fileContent = File.ReadAllLines(fullPath);
            var contentWithoutComments = RemoveComments(fileContent);
            var deserializedContent = JsonConvert.DeserializeObject<T>(contentWithoutComments);
            onSuccess?.Invoke(deserializedContent);
            return true;
        }
        catch (Exception e)
        {
            HandleException(e, fullPath, false);
            return false;
        }
    }

    public static string RemoveComments(string[] lines)
    {
        var cleanedLines = lines.Select(line =>
        {
            var commentIndex = line.IndexOf("//", StringComparison.OrdinalIgnoreCase);
            return commentIndex == -1 ? line : line.Substring(0, commentIndex).Trim();
        }).Where(line => !string.IsNullOrWhiteSpace(line));

        return string.Join(Environment.NewLine, cleanedLines);
    }

    public static List<string> GetFilesWithExtension(string searchDirectory, string extension)
    {
        var filesList = new List<string>();
        try
        {
            var dirInfo = new DirectoryInfo(searchDirectory);
            filesList = dirInfo.GetFiles($"*{extension}", SearchOption.AllDirectories)
                .Select(file => Path.GetFileNameWithoutExtension(file.Name)).ToList();
        }
        catch (Exception e)
        {
            StashieCore.Main.LogError($"Failed to retrieve files with extension {extension}: {e.Message}", 15);
        }

        return filesList;
    }
}