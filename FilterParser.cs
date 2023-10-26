using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using ExileCore;
using ExileCore.PoEMemory.MemoryObjects;
using JM.LinqFaster;
using SharpDX;
using Stashie.Filter;

namespace Stashie
{
    public class FilterParser
    {
        private const char SYMBOL_COMMANDSDIVIDE = ',';
        private const char SYMBOL_COMMAND_FILTER_OR = '|';
        private const char SYMBOL_NAMEDIVIDE = ':';
        private const char SYMBOL_SUBMENUNAME = ':';
        private const char SYMBOL_NOT = '!';
        private const string COMMENTSYMBOL = "#";
        private const string COMMENTSYMBOLALT = "//";

        //String compare
        private const string PARAMETER_CLASSNAME = "classname";
        private const string PARAMETER_BASENAME = "basename";
        private const string PARAMETER_PATH = "path";
        private const string PARAMETER_NAME = "name";
        private const string PARAMETER_DESCRIPTION = "desc";
        private const string PARAMETER_CLUSTERJEWELBASE = "clusterjewelbase";

        //Number compare
        private const string PARAMETER_QUALITY = "itemquality";
        private const string PARAMETER_RARITY = "rarity";
        private const string PARAMETER_ILVL = "ilvl";
        private const string PARAMETER_MapTier = "tier";
        private const string PARAMETER_NUMBER_OF_SOCKETS = "numberofsockets";
        private const string PARAMETER_LARGEST_LINK_SIZE = "numberoflinks";
        private const string PARAMETER_VEILED = "veiled";
        private const string PARAMETER_FRACTUREDMODS = "fractured";
        private const string PARAMETER_SKILLGEMLEVEL = "skillgemlevel";
        private const string PARAMTER_CLUSTERJEWELPASSIVES = "clusterjewelpassives";
        private const string PARAMETER_DELIRIUMREWARDSAMOUNT = "deliriumrewards";
        private const string PARAMETER_SCOURGETIER = "scourgetier";
        private const string PARAMETER_MODAFFIX = "affix";

        //Boolean
        private const string PARAMETER_IDENTIFIED = "identified";
        private const string PARAMETER_ISCORRUPTED = "corrupted";
        private const string PARAMETER_ISINFLUENCED = "influenced";
        private const string PARAMETER_ISELDER = "Elder";
        private const string PARAMETER_ISSHAPER = "Shaper";
        private const string PARAMETER_ISCRUSADER = "Crusader";
        private const string PARAMETER_ISHUNTER = "Hunter";
        private const string PARAMETER_ISREDEEMER = "Redeemer";
        private const string PARAMETER_ISWARLORD = "Warlord";
        private const string PARAMETER_ISSYNTHESISED = "Synthesised";
        private const string PARAMETER_ISBLIGHTEDMAP = "blightedMap";
        private const string PARAMETER_ISELDERGUARDIANMAP = "elderGuardianMap";
        private const string PARAMETER_AFFINITYACTIVE = "affinity";
        private const string PARAMETER_IGNOREAFFINITY = "shifting";

        //Operations
        private const string OPERATION_NONEQUALITY = "!=";
        private const string OPERATION_LESSEQUAL = "<=";
        private const string OPERATION_BIGGERQUAL = ">=";
        private const string OPERATION_EQUALITY = "=";
        private const string OPERATION_BIGGER = ">";
        private const string OPERATION_LESS = "<";
        private const string OPERATION_CONTAINS = "^";
        private const string OPERATION_NOTCONTAINS = "!^";
        private const string OPERATION_PLUS = "+"; //maybe not needed yet

        private static readonly string[] Operations =
        {
            OPERATION_NONEQUALITY,
            OPERATION_LESSEQUAL,
            OPERATION_BIGGERQUAL,
            OPERATION_NOTCONTAINS,
            OPERATION_EQUALITY,
            OPERATION_BIGGER,
            OPERATION_LESS,
            OPERATION_CONTAINS,
            OPERATION_PLUS
        };

        public static List<CustomFilter> Parse(string[] filtersLines)
        {
            var allFilters = new List<CustomFilter>();

            for (var i = 0; i < filtersLines.Length; ++i)
            {
                var filterLine = filtersLines[i];

                filterLine = filterLine.Replace("\t", "");

                if (filterLine.StartsWith(COMMENTSYMBOL)) continue;
                if (filterLine.StartsWith(COMMENTSYMBOLALT)) continue;

                if (filterLine.Replace(" ", "").Length == 0) continue;

                var nameIndex = filterLine.IndexOf(SYMBOL_NAMEDIVIDE);

                if (nameIndex == -1)
                {
                    DebugWindow.LogMsg("Filter parser: Can't find filter name in line: " + (i + 1), 5);
                    continue;
                }

                var newFilter = new CustomFilter { Name = filterLine.Substring(0, nameIndex).Trim(), Index = i + 1 };

                var filterCommandsLine = filterLine.Substring(nameIndex + 1);

                var submenuIndex = filterCommandsLine.IndexOf(SYMBOL_SUBMENUNAME);

                if (submenuIndex != -1)
                {
                    newFilter.SubmenuName = filterCommandsLine.Substring(submenuIndex + 1);
                    filterCommandsLine = filterCommandsLine.Substring(0, submenuIndex);
                }

                var filterCommands = filterCommandsLine.Split(SYMBOL_COMMANDSDIVIDE);
                newFilter.Commands = filterCommandsLine;

                var filterErrorParse = false;

                foreach (var command in filterCommands)
                {
                    if (string.IsNullOrEmpty(command.Replace(" ", ""))) continue;

                    if (command.Contains(SYMBOL_COMMAND_FILTER_OR))
                    {
                        var orFilterCommands = command.Split(SYMBOL_COMMAND_FILTER_OR);
                        var newOrFilter = new BaseFilter { BAny = true };
                        newFilter.Filters.Add(newOrFilter);

                        foreach (var t in orFilterCommands)
                        {
                            if (ProcessCommand(newOrFilter, t)) continue;
                            filterErrorParse = true;
                            break;
                        }

                        if (filterErrorParse) break;
                    }
                    else
                    {
                        if (ProcessCommand(newFilter, command)) continue;

                        filterErrorParse = true;
                        break;
                    }
                }

                if (!filterErrorParse)
                {
                    allFilters.Add(newFilter);
                }
                else
                {
                    DebugWindow.LogMsg($"Line: {i + 1}", 5, Color.Red);
                }
            }

            return allFilters;
        }

        private static bool ProcessCommand(BaseFilter newFilter, string command)
        {
            command = command.Trim();
            if (command.Contains(PARAMETER_AFFINITYACTIVE))
            {
                var affinityActiveCommand = new AffinityActiveFilter { BAffinityActive = command[0] != SYMBOL_NOT };
                newFilter.Filters.Add(affinityActiveCommand);
                return true;
            }
            if (command.Contains(PARAMETER_IGNOREAFFINITY))
            {
                var affinityIgnoreCommand = new AffinityIgnoreFilter { BAffinityIgnore = command[0] != SYMBOL_NOT };
                newFilter.Filters.Add(affinityIgnoreCommand);
                return true;
            }
            if (command.Contains(PARAMETER_IDENTIFIED))
            {
                var identCommand = new IdentifiedItemFilter { BIdentified = command[0] != SYMBOL_NOT };
                newFilter.Filters.Add(identCommand);
                return true;
            }

            if (command.Contains(PARAMETER_ISCORRUPTED))
            {
                var corruptedCommand = new CorruptedItemFilter { BCorrupted = command[0] != SYMBOL_NOT };
                newFilter.Filters.Add(corruptedCommand);
                return true;
            }

            if (command.Contains(PARAMETER_ISELDER))
            {
                var elderCommand = new ElderItemFiler { isElder = command[0] != SYMBOL_NOT };
                newFilter.Filters.Add(elderCommand);
                return true;
            }

            if (command.Contains(PARAMETER_ISSHAPER))
            {
                var shaperCommand = new ShaperItemFilter { isShaper = command[0] != SYMBOL_NOT };
                newFilter.Filters.Add(shaperCommand);
                return true;
            }

            if (command.Contains(PARAMETER_ISCRUSADER))
            {
                var crusaderCommand = new CrusaderItemFilter { isCrusader = command[0] != SYMBOL_NOT };
                newFilter.Filters.Add(crusaderCommand);
                return true;
            }

            if (command.Contains(PARAMETER_ISHUNTER))
            {
                var hunterCommand = new HunterItemFilter { isHunter = command[0] != SYMBOL_NOT };
                newFilter.Filters.Add(hunterCommand);
                return true;
            }

            if (command.Contains(PARAMETER_ISREDEEMER))
            {
                var redeemerCommand = new RedeemerItemFilter { isRedeemer = command[0] != SYMBOL_NOT };
                newFilter.Filters.Add(redeemerCommand);
                return true;
            }

            if (command.Contains(PARAMETER_ISWARLORD))
            {
                var warordCommand = new WarlordItemFilter { isWarlord = command[0] != SYMBOL_NOT };
                newFilter.Filters.Add(warordCommand);
                return true;
            }

            if (command.Contains(PARAMETER_ISINFLUENCED))
            {
                var influencedCommand = new AnyInfluenceItemFilter { isInfluenced = command[0] != SYMBOL_NOT };
                newFilter.Filters.Add(influencedCommand);
                return true;
            }

            if (command.Contains(PARAMETER_ISBLIGHTEDMAP))
            {
                var blightedMapCommand = new BlightedMapFilter { isBlightMap = command[0] != SYMBOL_NOT };
                newFilter.Filters.Add(blightedMapCommand);
                return true;
            }

            if (command.Contains(PARAMETER_ISELDERGUARDIANMAP))
            {
                var elderGuardianMapCommand = new ElderGuardianMapFilter
                { isElderGuardianMap = command[0] != SYMBOL_NOT };
                newFilter.Filters.Add(elderGuardianMapCommand);
                return true;
            }

            string parameter;
            string operation;
            string stringValue;
            string value;
            string affixValueIndex;

            if (!ParseCommand(command, out parameter, out operation, out stringValue, out value, out affixValueIndex))
            {
                DebugWindow.LogMsg($"Unknown operation: {command}", 5, Color.Red);
                return false;
            }

            var stringComp = new FilterParameterCompare { CompareString = value };



            switch (parameter.ToLower())
            {
                case PARAMETER_CLASSNAME:
                    stringComp.StringParameter = data => data.ClassName;
                    break;
                case PARAMETER_BASENAME:
                    stringComp.StringParameter = data => data.BaseName;
                    break;
                case PARAMETER_NAME:
                    stringComp.StringParameter = data => data.Name;
                    break;
                case PARAMETER_PATH:
                    stringComp.StringParameter = data => data.Path;
                    break;
                case PARAMETER_DESCRIPTION:
                    stringComp.StringParameter = data => data.Description;
                    break;
                case PARAMETER_RARITY:
                    stringComp.StringParameter = data => data.Rarity.ToString();
                    break;
                case PARAMETER_QUALITY:
                    stringComp.IntParameter = data => data.ItemQuality;
                    stringComp.CompareInt = int.Parse(value);
                    stringComp.StringParameter = data => data.ItemQuality.ToString();
                    break;
                case PARAMETER_MapTier:
                    stringComp.IntParameter = data => data.MapTier;
                    stringComp.CompareInt = int.Parse(value);
                    stringComp.StringParameter = data => data.MapTier.ToString();
                    break;
                case PARAMETER_ILVL:
                    stringComp.IntParameter = data => data.ItemLevel;
                    stringComp.CompareInt = int.Parse(value);
                    stringComp.StringParameter = data => data.ItemLevel.ToString();
                    break;
                case PARAMETER_NUMBER_OF_SOCKETS:
                    stringComp.IntParameter = data => data.NumberOfSockets;
                    stringComp.CompareInt = int.Parse(value);
                    stringComp.StringParameter = data => data.NumberOfSockets.ToString();
                    break;
                case PARAMETER_LARGEST_LINK_SIZE:
                    stringComp.IntParameter = data => data.LargestLinkSize;
                    stringComp.CompareInt = int.Parse(value);
                    stringComp.StringParameter = data => data.LargestLinkSize.ToString();
                    break;
                case PARAMETER_MODAFFIX:
                    stringComp.CompareAffixModIndex = int.Parse(affixValueIndex);
                    stringComp.ModsParameter = data => data.ItemMods;
                    stringComp.CompareAffixValue = int.Parse(value);
                    stringComp.CompareAffixString = stringValue;
                    break;
                case PARAMETER_VEILED:
                    stringComp.IntParameter = data => data.Veiled;
                    stringComp.CompareInt = int.Parse(value);
                    stringComp.StringParameter = data => data.Veiled.ToString();
                    break;
                case PARAMETER_FRACTUREDMODS:
                    stringComp.IntParameter = data => data.Fractured;
                    stringComp.CompareInt = int.Parse(value);
                    stringComp.StringParameter = data => data.Fractured.ToString();
                    break;
                case PARAMETER_DELIRIUMREWARDSAMOUNT:
                    stringComp.IntParameter = data => data.DeliriumStacks;
                    stringComp.CompareInt = int.Parse(value);
                    stringComp.StringParameter = data => data.DeliriumStacks.ToString();
                    break;
                case PARAMETER_SCOURGETIER:
                    stringComp.IntParameter = data => data.ScourgeTier;
                    stringComp.CompareInt = int.Parse(value);
                    stringComp.StringParameter = data => data.ScourgeTier.ToString();
                    break;
                case PARAMETER_SKILLGEMLEVEL:
                    stringComp.IntParameter = data => data.SkillGemLevel;
                    stringComp.CompareInt = int.Parse(value);
                    stringComp.StringParameter = data => data.SkillGemLevel.ToString();
                    break;

                default:
                    DebugWindow.LogMsg($"Filter parser: Parameter is not defined in code: {parameter}", 10);
                    return false;
            }

            if (parameter.ToLower() != PARAMETER_MODAFFIX)
            {
                switch (operation.ToLower())
                {
                    case OPERATION_EQUALITY:
                        stringComp.CompDeleg = data => stringComp.StringParameter(data).Equals(stringComp.CompareString);
                        break;
                    case OPERATION_NONEQUALITY:
                        stringComp.CompDeleg = data => !stringComp.StringParameter(data).Equals(stringComp.CompareString);
                        break;
                    case OPERATION_CONTAINS:
                        stringComp.CompDeleg = data => stringComp.StringParameter(data).Contains(stringComp.CompareString);
                        break;
                    case OPERATION_NOTCONTAINS:
                        stringComp.CompDeleg = data => !stringComp.StringParameter(data).Contains(stringComp.CompareString);
                        break;

                    case OPERATION_BIGGER:
                        if (stringComp.IntParameter == null)
                        {
                            DebugWindow.LogMsg(
                                $"Filter parser error: Can't compare string parameter with {OPERATION_BIGGER} (numerical) operation. Statement: {command}",
                                10);

                            return false;
                        }

                        stringComp.CompDeleg = data => stringComp.IntParameter(data) > stringComp.CompareInt;
                        break;
                    case OPERATION_LESS:
                        if (stringComp.IntParameter == null)
                        {
                            DebugWindow.LogMsg(
                                $"Filter parser error: Can't compare string parameter with {OPERATION_LESS} (numerical) operation. Statement: {command}",
                                10);

                            return false;
                        }

                        stringComp.CompDeleg = data =>
                        {
                            return stringComp.IntParameter(data) < stringComp.CompareInt;
                        };
                        break;
                    case OPERATION_LESSEQUAL:
                        if (stringComp.IntParameter == null)
                        {
                            DebugWindow.LogMsg(
                                $"Filter parser error: Can't compare string parameter with {OPERATION_LESSEQUAL} (numerical) operation. Statement: {command}",
                                10);

                            return false;
                        }

                        stringComp.CompDeleg = data => stringComp.IntParameter(data) <= stringComp.CompareInt;
                        break;

                    case OPERATION_BIGGERQUAL:
                        if (stringComp.IntParameter == null)
                        {
                            DebugWindow.LogMsg(
                                $"Filter parser error: Can't compare string parameter with {OPERATION_BIGGERQUAL} (numerical) operation. Statement: {command}",
                                10);

                            return false;
                        }

                        stringComp.CompDeleg = data => stringComp.IntParameter(data) >= stringComp.CompareInt;
                        break;

                    default:
                        DebugWindow.LogMsg($"Filter parser: Operation is not defined in code: {operation}", 10);
                        return false;
                }
            }
            else
            {
                switch (operation.ToLower())
                {
                    case OPERATION_EQUALITY:
                        if (stringComp.ModsParameter == null)
                        {
                            return false;
                        }
                        stringComp.CompDeleg = data => stringComp.ModsParameter(data).Any(itemMod => itemMod.Name.ToLower().Contains(stringComp.CompareAffixString.ToLower())
                                                                                                           && itemMod.Values != null
                                                                                                           && itemMod.Values.Count > stringComp.CompareAffixModIndex
                                                                                                           && itemMod.Values[stringComp.CompareAffixModIndex] == stringComp.CompareAffixValue);
                        break;
                    case OPERATION_NONEQUALITY:
                        if (stringComp.ModsParameter == null)
                        {
                            return false;
                        }
                        stringComp.CompDeleg = data => stringComp.ModsParameter(data).Any(itemMod => itemMod.Name.ToLower().Contains(stringComp.CompareAffixString.ToLower())
                                                                                                           && itemMod.Values != null
                                                                                                           && itemMod.Values.Count > stringComp.CompareAffixModIndex
                                                                                                           && itemMod.Values[stringComp.CompareAffixModIndex] != stringComp.CompareAffixValue);
                        break;
                    case OPERATION_BIGGER:
                        if (stringComp.ModsParameter == null)
                        {
                            return false;
                        }
                        stringComp.CompDeleg = data => stringComp.ModsParameter(data).Any(itemMod => itemMod.Name.ToLower().Contains(stringComp.CompareAffixString.ToLower())
                                                                                                           && itemMod.Values != null
                                                                                                           && itemMod.Values.Count > stringComp.CompareAffixModIndex
                                                                                                           && itemMod.Values[stringComp.CompareAffixModIndex] > stringComp.CompareAffixValue);
                        break;
                    case OPERATION_LESS:
                        if (stringComp.ModsParameter == null)
                        {
                            return false;
                        }
                        stringComp.CompDeleg = data => stringComp.ModsParameter(data).Any(itemMod => itemMod.Name.ToLower().Contains(stringComp.CompareAffixString.ToLower())
                                                                                                           && itemMod.Values != null
                                                                                                           && itemMod.Values.Count > stringComp.CompareAffixModIndex
                                                                                                           && itemMod.Values[stringComp.CompareAffixModIndex] < stringComp.CompareAffixValue);
                        break;
                    case OPERATION_LESSEQUAL:
                        if (stringComp.ModsParameter == null)
                        {
                            return false;
                        }
                        stringComp.CompDeleg = data => stringComp.ModsParameter(data).Any(itemMod => itemMod.Name.ToLower().Contains(stringComp.CompareAffixString.ToLower())
                                                                                                           && itemMod.Values != null
                                                                                                           && itemMod.Values.Count > stringComp.CompareAffixModIndex
                                                                                                           && itemMod.Values[stringComp.CompareAffixModIndex] <= stringComp.CompareAffixValue);
                        break;
                    case OPERATION_BIGGERQUAL:
                        if (stringComp.ModsParameter == null)
                        {
                            return false;
                        }
                        stringComp.CompDeleg = data => stringComp.ModsParameter(data).Any(itemMod => itemMod.Name.ToLower().Contains(stringComp.CompareAffixString.ToLower())
                                                                                                           && itemMod.Values != null
                                                                                                           && itemMod.Values.Count > stringComp.CompareAffixModIndex
                                                                                                           && itemMod.Values[stringComp.CompareAffixModIndex] >= stringComp.CompareAffixValue);
                        break;
                    default:
                        DebugWindow.LogMsg($"Filter parser: Operation is not defined in code: {operation}", 10);
                        return false;
                }
            }
            newFilter.Filters.Add(stringComp);
            return true;
        }

        private static bool ParseCommand(string command, out string parameter, out string operation, out string stringValue, out string value, out string affixValueIndex)
        {
            parameter = "";
            operation = "";
            stringValue = "";
            value = "";
            affixValueIndex = "0";

            var regex = new Regex(@"(?=.*affix(?:\[\d+\])?=)affix(?:\[(\d+)\])?=", RegexOptions.IgnoreCase);

            if (regex.IsMatch(command))
            {
                var match = regex.Match(command);
                if (match.Groups[1].Success)
                {
                    affixValueIndex = match.Groups[1].Value;
                }

                command = regex.Replace(command, "");

                var operationIndex = -1;

                foreach (var t in Operations)
                {
                    operationIndex = command.IndexOf(t, StringComparison.Ordinal);

                    if (operationIndex == -1) continue;

                    operation = t;
                    break;
                }

                if (operationIndex == -1) return false;

                parameter = PARAMETER_MODAFFIX;
                stringValue = command.Substring(0, operationIndex).Trim();
                value = command.Substring(operationIndex + operation.Length).Trim();
            }
            else
            {
                var operationIndex = -1;

                foreach (var t in Operations)
                {
                    operationIndex = command.IndexOf(t, StringComparison.Ordinal);

                    if (operationIndex == -1) continue;

                    operation = t;
                    break;
                }

                if (operationIndex == -1) return false;

                parameter = command.Substring(0, operationIndex).Trim();
                value = command.Substring(operationIndex + operation.Length).Trim();
            }
            return true;
        }
    }
}