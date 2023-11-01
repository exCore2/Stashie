using ExileCore;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared;
using ExileCore.Shared.Enums;
using ExileCore.Shared.Nodes;
using ImGuiNET;
using ItemFilterLibrary;
using SharpDX;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using static ExileCore.PoEMemory.MemoryObjects.ServerInventory;
using Vector2N = System.Numerics.Vector2;
using Vector4 = System.Numerics.Vector4;

namespace Stashie
{
    public class StashieCore : BaseSettingsPlugin<StashieSettings>
    {
        private const string StashTabsNameChecker = "Stash Tabs Name Checker";
        private const int WhileDelay = 5;
        private const int InputDelay = 15;
        private const string CoroutineName = "Drop To Stash";
        private readonly Stopwatch _debugTimer = new Stopwatch();
        private Vector2 _clickWindowOffset;
        private List<CustomFilter> currentFilter;
        private List<RefillProcessor> _customRefills;
        private List<FilterResult> _dropItems;
        private List<ListIndexNode> _settingsListNodes;
        private uint _coroutineIteration;
        private Coroutine _coroutineWorker;
        private Action _filterTabs;
        private string[] _stashTabNamesByIndex;
        private Coroutine _stashTabNamesCoroutine;
        private int _visibleStashIndex = -1;
        private const int MaxShownSidebarStashTabs = 31;
        private int _stashCount;
        private bool secondaryFilterActive = false;

        public StashieCore()
        {
            Name = "StashieWithLinq";
        }

        public override void ReceiveEvent(string eventId, object args)
        {
            if (!Settings.Enable.Value)
            {
                return;
            }

            switch (eventId)
            {
                case "switch_to_tab":
                    HandleSwitchToTabEvent(args);
                    break;
                default:
                    break;
            }
        }

        private void HandleSwitchToTabEvent(object tab)
        {
            switch (tab)
            {
                case int index:
                    _coroutineWorker = new Coroutine(ProcessSwitchToTab(index), this, CoroutineName);
                    break;
                case string name:
                    if (!_renamedAllStashNames.Contains(name))
                    {
                        DebugWindow.LogMsg($"{Name}: can't find tab with name '{name}'.");
                        break;
                    }

                    var tempIndex = _renamedAllStashNames.IndexOf(name);
                    _coroutineWorker = new Coroutine(ProcessSwitchToTab(tempIndex), this, CoroutineName);
                    DebugWindow.LogMsg($"{Name}: Switching to tab with index: {tempIndex} ('{name}').");
                    break;
                default:
                    DebugWindow.LogMsg("The received argument is not a string or an integer.");
                    break;
            }

            Core.ParallelRunner.Run(_coroutineWorker);
        }

        public override bool Initialise()
        {
            Settings.Enable.OnValueChanged += (sender, b) =>
            {
                if (b)
                {
                    if (Core.ParallelRunner.FindByName(StashTabsNameChecker) == null) InitStashTabNameCoRoutine();
                    _stashTabNamesCoroutine?.Resume();
                }
                else
                {
                    _stashTabNamesCoroutine?.Pause();
                }

                SetupOrClose();
            };

            InitStashTabNameCoRoutine();
            SetupOrClose();

            Input.RegisterKey(Settings.DropHotkey);

            Settings.DropHotkey.OnValueChanged += () => { Input.RegisterKey(Settings.DropHotkey); };
            Settings.SwitchFilterhotkey.OnValueChanged += () => { Input.RegisterKey(Settings.SwitchFilterhotkey); };
            _stashCount = (int)GameController.Game.IngameState.IngameUi.StashElement.TotalStashes;
            Settings.FilterFile.OnValueSelected = _ => LoadCustomFilters();

            return true;
        }

        public override void AreaChange(AreaInstance area)
        {
            if (_stashTabNamesCoroutine == null) return;
            if (_stashTabNamesCoroutine.Running)
            {
                if (!area.IsHideout && !area.IsTown &&
                    !area.DisplayName.Contains("Azurite Mine") &&
                    !area.DisplayName.Contains("Tane's Laboratory"))
                    _stashTabNamesCoroutine?.Pause();
            }
            else
            {
                if (area.IsHideout ||
                    area.IsTown ||
                    area.DisplayName.Contains("Azurite Mine") ||
                    area.DisplayName.Contains("Tane's Laboratory"))
                    _stashTabNamesCoroutine?.Resume();
            }
        }

        private void InitStashTabNameCoRoutine()
        {
            _stashTabNamesCoroutine = new Coroutine(StashTabNamesUpdater_Thread(), this, StashTabsNameChecker);
            Core.ParallelRunner.Run(_stashTabNamesCoroutine);
        }

        /// <summary>
        /// Creates a new file and adds the content to it if the file doesn't exists.
        /// If the file already exists, then no action is taken.
        /// </summary>
        /// <param name="path">The path to the file on disk</param>
        /// <param name="content">The content it should contain</param>
        private static void WriteToNonExistentFile(string path, string content)
        {
            if (File.Exists(path)) return;

            using (var streamWriter = new StreamWriter(path, true))
            {
                streamWriter.Write(content);
                streamWriter.Close();
            }
        }

        private void SaveDefaultConfigsToDisk()
        {
            var path = $"{ConfigDirectory}\\RefillCurrency.txt";

            const string refillCurrency = "//MenuName:\t\t\tClassName,\t\t\tStackSize,\tInventoryX,\tInventoryY\r\n" +
                                          "Portal Scrolls:\t\tPortal Scroll,\t\t40,\t\t\t12,\t\t\t1\r\n" +
                                          "Scrolls of Wisdom:\tScroll of Wisdom,\t40,\t\t\t12,\t\t\t2\r\n" +
                                          "//Chances:\t\t\tOrb of Chance,\t\t20,\t\t\t12,\t\t\t3";

            WriteToNonExistentFile(path, refillCurrency);
            path = $"{ConfigDirectory}\\Default Config.ifl";

            const string filtersConfig =

            #region default config String

            "// Rule Structure:\r\n" +
            "// FilterName::Filter::Shifting::Affinity::ParentMenu\r\n" +
            "\r\n" +
            "// Example Usage:\r\n" +
            "// [o] 6 Links::SocketInfo.LargestLinkSize == 6::true::false::Overwrites\r\n" +
            "\r\n" +
            "// Explanation:\r\n" +
            "// - FilterName: The name of the filter or menu.\r\n" +
            "// - Filter: The filter condition or rule.\r\n" +
            "// - Shifting: Presses the Shift key during stash operations (Ctrl + Shift).\r\n" +
            "// - Affinity: Clicks items without switching the tab.\r\n" +
            "// - ParentMenu: Groups all together with the same ParentMenu in the plugin settings\r\n" +
            "\r\n" +
            "// - This uses the Item Filter Library from https://github.com/exApiTools/ItemFilter/blob/main/README.md\r\n" +
            "\r\n" +
            "//Overwrites\r\n" +
            "[o] Blight Annointed Items::FindMods(\"grantedpassive\").Any(Values[0] >= -40000) || FindMods(\"BlightEnchantment\").Any(Values[0] >= -40000)::false::false::Overwrites\r\n" +
            "[o] Convoking Wands [ilvl86]::BaseName == \"Convoking Wand\" && ItemLevel >= 86::true::false::Overwrites\r\n" +
            "[o] Jewels::ClassName == \"Jewel\"::true::false::Overwrites\r\n" +
            "[o] City Square::IsMap && BaseName == \"City Square Map\" && MapTier == 16 && !IsCorrupted && !IsElderGuardianMap::true::false::Overwrites\r\n" +
            "[o] City Square Corrupted::IsMap && BaseName == \"City Square Map\" && MapTier == 16 && IsCorrupted && !IsElderGuardianMap::true::false::Overwrites\r\n" +
            "[o] Vaal Temple Corrupted::IsMap && BaseName == \"Vaal Temple Map\" && MapTier == 16 && IsCorrupted && !IsElderGuardianMap::true::false::Overwrites\r\n" +
            "\r\n" +
            "//Currency Tab\r\n" +
            "Currency Tab::ClassName == \"StackableCurrency\" && !ContainsString(Path, new string[] { \"CurrencyDelveCrafting\", \"CurrencyItemisedProphecy\", \"CurrencyAfflictionOrb\", \"Mushrune\", \"Essence\" }) && !ContainsString(BaseName, new string[] { \"Stacked Deck\", \"Catalyst\", \"Primeval Remnant\", \"Remnant\", \"Splinter\", \"Oil Extractor\" })::false::false::Currency Tab\r\n" +
            "\r\n" +
            "//Fragment Tab\r\n" +
            "Fragment Tab::(ClassName == \"MapFragment\" || ClassName == \"LabyrinthMapItem\" || ContainsString(BaseName, new string[] { \"Splinter\", \"Scarab\" })) && !ContainsString(BaseName, new string[] { \"Primeval Remnant\" }) && !ContainsString(Path, new string[] { \"CurrencyAfflictionShard\" })::false::false::Fragment Tab\r\n" +
            "\r\n" +
            "//Div Cards Tab\r\n" +
            "Divination Cards::ClassName == \"DivinationCard\"::false::false::Divination Tab\r\n" +
            "Stacked Decks::BaseName == \"Stacked Deck\"::false::false::Divination Tab\r\n" +
            "\r\n" +
            "//Essence Tab\r\n" +
            "Essence Tab::ContainsString(BaseName, new string[] { \"Essence\", \"Remnant\" }) && !ClassName.Contains(\"Skill Gem\") && BaseName != \"Primeval Remnant\"::false::false::Essence Tab\r\n" +
            "\r\n" +
            "//Delve Tab\r\n" +
            "Delve Tab::Path.Contains(\"CurrencyDelveCrafting\") || ClassName == \"DelveStackableSocketableCurrency\"::false::false::Delve Tab\r\n" +
            "\r\n" +
            "//Delve Tab\r\n" +
            "Maps::IsMap && !IsBlightMap && !IsElderGuardianMap::false::false::Map Tab\r\n" +
            "Blighted Maps::IsMap && IsBlightMap && !IsElderGuardianMap::false::false::Map Tab\r\n" +
            "Elder Guardian Maps::IsMap && !IsBlightMap && IsElderGuardianMap::false::false::Map Tab\r\n" +
            "Invitations::ClassName == \"MiscMapItem\" && BaseName.Contains(\"Maven's Invitation\")::false::false::Map Tab\r\n" +
            "\r\n" +
            "//Crucible\r\n" +
            "Crucible Tab::BaseName.Contains(\"Primeval Remnant\")::false::false::Crucible Tab\r\n" +
            "\r\n" +
            "//Metamorph\r\n" +
            "Metamorph All Organs::ClassName == \"MetamorphosisDNA\"::false::false::Metamorph Tab\r\n" +
            "Catalysts::BaseName.Contains(\"Catalyst\") && ClassName != \"DivinationCard\"::false::false::Metamorph Tab\r\n" +
            "\r\n" +
            "//Blight\r\n" +
            "All Oils::(BaseName.Contains(\"Oil\") && Path.Contains(\"Mushrune\")) || BaseName == \"Oil Extractor\"::false::false::Blight Tab\r\n" +
            "\r\n" +
            "//Delirium\r\n" +
            "Simu Splinters::Path.Contains(\"CurrencyAfflictionShard\")::false::false::Delirium Tab\r\n" +
            "Simu Map::Path.Contains(\"CurrencyAfflictionFragment\")::false::false::Delirium Tab\r\n" +
            "Delirium Orbs::Path.Contains(\"CurrencyAfflictionOrb\")::false::false::Delirium Tab\r\n" +
            "\r\n" +
            "//Legion\r\n" +
            "Incubators::ClassName == \"IncubatorStackable\"::false::false::Legion Tab\r\n" +
            "\r\n" +
            "//Talisman\r\n" +
            "Talismans::ClassName == \"Amulet\" && BaseName.Contains(\"Talisman\") && Rarity != ItemRarity.Unique::false::false::Talismans\r\n" +
            "\r\n" +
            "//Abyss\r\n" +
            "Abyss Jewels::ClassName == \"AbyssJewel\"::false::false::Abyss Tab\r\n" +
            "Abyss Belt::BaseName.Contains(\"Stygian\")::false::false::Abyss Tab\r\n" +
            "\r\n" +
            "//Heist\r\n" +
            "Contracts::ClassName.Contains(\"HeistContract\")::false::false::Heist Locker\r\n" +
            "Blueprints::ClassName.Contains(\"HeistBlueprint\")::false::false::Heist Locker\r\n" +
            "Heist Equipment::ClassName.Contains(\"HeistEquipment\")::false::false::Heist Locker\r\n" +
            "Heist Trinket::ClassName.Contains(\"Trinket\")::false::false::Heist Locker\r\n" +
            "Heist Objective::ClassName.Contains(\"HeistObjective\")::false::false::Heist Locker\r\n" +
            "\r\n" +
            "//Chaos Recipe (2 Chaos) (unindentified, rare and ilvl between 60-74)\r\n" +
            "C Weapons::!IsIdentified && Rarity == ItemRarity.Rare && ItemLevel >= 60 && ItemLevel <= 74 && IsWeapon::false::false::Chaos Recipe\r\n" +
            "C Jewelry::!IsIdentified && Rarity == ItemRarity.Rare && ItemLevel >= 60 && ItemLevel <= 74 && ClassName == \"Ring\" || ClassName == \"Amulet\"::false::false::Chaos Recipe\r\n" +
            "C Belts::!IsIdentified && Rarity == ItemRarity.Rare && ItemLevel >= 60 && ItemLevel <= 74 && ClassName == \"Belt\"::false::false::Chaos Recipe\r\n" +
            "C Helms::!IsIdentified && Rarity == ItemRarity.Rare && ItemLevel >= 60 && ItemLevel <= 74 && ClassName == \"Helmet\"::false::false::Chaos Recipe\r\n" +
            "C Body Armours::!IsIdentified && Rarity == ItemRarity.Rare && ItemLevel >= 60 && ItemLevel <= 74 && ClassName == \"Body Armour\"::false::false::Chaos Recipe\r\n" +
            "C Boots::!IsIdentified && Rarity == ItemRarity.Rare && ItemLevel >= 60 && ItemLevel <= 74 && ClassName == \"Boots\"::false::false::Chaos Recipe\r\n" +
            "C Boots::!IsIdentified && Rarity == ItemRarity.Rare && ItemLevel >= 60 && ItemLevel <= 74 && ClassName == \"Gloves\"::false::false::Chaos Recipe\r\n" +
            "\r\n" +
            "//Uniques\r\n" +
            "All Unique Items::Rarity == ItemRarity.Unique && ClassName != \"Map\"::false::false::Uniques\r\n" +
            "\r\n" +
            "//Veiled Items\r\n" +
            "All Veiled Items::VeiledModCount > 0::false::false::Veiled Items\r\n" +
            "\r\n" +
            "//Flasks\r\n" +
            "Quality Flasks::ClassName.Contains(\"Flask\") && ItemQuality > 0 && Rarity != ItemRarity.Unique::false::false::Flask Tab\r\n" +
            "Non Quality Flasks::ClassName.Contains(\"Flask\") && ItemQuality == 0 && Rarity != ItemRarity.Unique::false::false::Flask Tab\r\n" +
            "All Flasks::ClassName.Contains(\"Flask\") && Rarity != ItemRarity.Unique::false::false::Flask Tab\r\n" +
            "\r\n" +
            "//Skill Gems\r\n" +
            "Quality Skill Gems::ClassName.Contains(\" Skill Gem\") && ItemQuality > 0::false::false::Skill Tab\r\n" +
            "Non Quality Skill Gems::ClassName.Contains(\" Skill Gem\") && ItemQuality == 0::false::false::Skill Tab\r\n" +
            "All Skill Gems::ClassName.Contains(\" Skill Gem\")::false::false::Skill Tab\r\n" +
            "\r\n" +
            "//Jewels\r\n" +
            "Small Cluster Jewel::ClassName == \"Jewel\" && BaseName.Contains(\"Small Cluster\") && Rarity != ItemRarity.Unique::false::false::Jewels\r\n" +
            "Medium Cluster Jewel::ClassName == \"Jewel\" && BaseName.Contains(\"Medium Cluster\") && Rarity != ItemRarity.Unique::false::false::Jewels\r\n" +
            "Large Cluster Jewel::ClassName == \"Jewel\" && BaseName.Contains(\"Large Cluster\") && Rarity != ItemRarity.Unique::false::false::Jewels\r\n" +
            "All Cluster Jewel::ClassName == \"Jewel\" && BaseName.Contains(\"Cluster\") && Rarity != ItemRarity.Unique::false::false::Jewels\r\n" +
            "Jewels::ClassName == \"Jewel\" && BaseName.Contains(\"Cluster\") && Rarity != ItemRarity.Unique::false::false::Jewels\r\n" +
            "\r\n" +
            "// 6 Link + 6 Sockets\r\n" +
            "6 Link::SocketInfo.LargestLinkSize == 6::true::false::Links + Sockets\r\n" +
            "6 Sockets::SocketInfo.SocketNumber == 6::true::false::Links + Sockets\r\n" +
            "\r\n" +
            "//Others\r\n" +
            "iLVL84+ Jewelry::IsIdentified && Rarity == ItemRarity.Rare && ItemLevel >= 84 && ContainsString(ClassName, new string[] {\"Ring\", \"Amulet\"})::false::false::Others\r\n" +
            "\r\n" +
            "//Left Overs\r\n" +
            "Rares::Rarity == ItemRarity.Rare::false::false::Left Overs\r\n" +
            "Magics::Rarity == ItemRarity.Magic::false::false::Left Overs\r\n";

            #endregion

            WriteToNonExistentFile(path, filtersConfig);
        }

        public override void DrawSettings()
        {
            DrawReloadConfigButton();
            DrawIgnoredCellsSettings();
            base.DrawSettings();

            foreach (var settingsCustomRefillOption in Settings.CustomRefillOptions)
            {
                var value = settingsCustomRefillOption.Value.Value;
                ImGui.SliderInt(settingsCustomRefillOption.Key, ref value, settingsCustomRefillOption.Value.Min,
                    settingsCustomRefillOption.Value.Max);
                settingsCustomRefillOption.Value.Value = value;
            }

            _filterTabs?.Invoke();
        }

        private void LoadCustomFilters()
        {
            var pickitConfigFileDirectory = Path.Combine(ConfigDirectory);

            if (!Directory.Exists(pickitConfigFileDirectory))
            {
                Directory.CreateDirectory(pickitConfigFileDirectory);
                return;
            }

            var dirInfo = new DirectoryInfo(pickitConfigFileDirectory);
            Settings.FilterFile.Values = dirInfo.GetFiles("*.ifl").Select(x => Path.GetFileNameWithoutExtension(x.Name)).ToList();
            if (Settings.FilterFile.Values.Any() && !Settings.FilterFile.Values.Contains(Settings.FilterFile.Value))
            {
                Settings.FilterFile.Value = Settings.FilterFile.Values.First();
            }

            if (!string.IsNullOrWhiteSpace(Settings.FilterFile.Value))
            {
                var filterFilePath = Path.Combine(pickitConfigFileDirectory, $"{Settings.FilterFile.Value}.ifl");
                if (File.Exists(filterFilePath))
                {
                    var filterLines = File.ReadAllLines(filterFilePath);
                    currentFilter = FilterParser.Parse(filterLines);

                    foreach (var customFilter in currentFilter)
                    {
                        if (!Settings.CustomFilterOptions.TryGetValue(customFilter.Name, out var indexNodeS))
                        {
                            indexNodeS = new ListIndexNode { Value = "Ignore", Index = -1 };
                            Settings.CustomFilterOptions.Add(customFilter.Name, indexNodeS);
                        }

                        customFilter.StashIndexNode = indexNodeS;
                        _settingsListNodes.Add(indexNodeS);
                    }
                }
                else
                {
                    currentFilter = null;
                    LogError("Item filter file not found, plugin will not work");
                }
            }
        }

        public void SaveIgnoredSLotsFromInventoryTemplate()
        {
            Settings.IgnoredCells = new[,]
            {
                {0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0},
                {0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0},
                {0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0},
                {0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0},
                {0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0}
            };
            try
            {
                var inventory_server = GameController.IngameState.Data.ServerData.PlayerInventories[0];

                foreach (var item in inventory_server.Inventory.InventorySlotItems)
                {
                    var baseC = item.Item.GetComponent<Base>();
                    var itemSizeX = baseC.ItemCellsSizeX;
                    var itemSizeY = baseC.ItemCellsSizeY;
                    var inventPosX = item.PosX;
                    var inventPosY = item.PosY;
                    for (var y = 0; y < itemSizeY; y++)
                        for (var x = 0; x < itemSizeX; x++)
                            Settings.IgnoredCells[y + inventPosY, x + inventPosX] = 1;
                }
            }
            catch (Exception e)
            {
                LogError($"{e}", 5);
            }
        }

        private void DrawReloadConfigButton()
        {
            if (ImGui.Button("Reload config"))
            {
                LoadCustomFilters();
                GenerateMenu();
                DebugWindow.LogMsg("Reloaded Stashie config", 2, Color.LimeGreen);
            }
        }

        private void DrawIgnoredCellsSettings()
        {
            try
            {
                if (ImGui.Button("Copy Inventory")) SaveIgnoredSLotsFromInventoryTemplate();

                ImGui.SameLine();
                ImGui.TextDisabled("(?)");
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip(
                        $"Checked = Item will be ignored{Environment.NewLine}UnChecked = Item will be processed");
            }
            catch (Exception e)
            {
                DebugWindow.LogError(e.ToString(), 10);
            }

            var numb = 1;
            for (var i = 0; i < 5; i++)
                for (var j = 0; j < 12; j++)
                {
                    var toggled = Convert.ToBoolean(Settings.IgnoredCells[i, j]);
                    if (ImGui.Checkbox($"##{numb}IgnoredCells", ref toggled)) Settings.IgnoredCells[i, j] ^= 1;

                    if ((numb - 1) % 12 < 11) ImGui.SameLine();

                    numb += 1;
                }
        }

        private void GenerateMenu()
        {
            _stashTabNamesByIndex = _renamedAllStashNames.ToArray();

            _filterTabs = null;

            foreach (var customFilter in currentFilter.GroupBy(x => x.SubmenuName, e => e))
                _filterTabs += () =>
                {
                    ImGui.TextColored(new Vector4(0f, 1f, 0.022f, 1f), customFilter.Key);

                    foreach (var filter in customFilter)
                        if (Settings.CustomFilterOptions.TryGetValue(filter.Name, out var indexNode))
                        {
                            var formattableString = $"{filter.Name} => {_renamedAllStashNames[indexNode.Index + 1]}";

                            ImGui.Columns(2, formattableString, true);
                            ImGui.SetColumnWidth(0, 320);
                            ImGui.SetColumnWidth(1, 300);

                            if (ImGui.Button(formattableString, new Vector2N(300, 20)))
                                ImGui.OpenPopup(formattableString);

                            ImGui.SameLine();
                            ImGui.NextColumn();

                            var item = indexNode.Index + 1;
                            var filterName = filter.Name;

                            if (string.IsNullOrWhiteSpace(filterName))
                                filterName = "Null";

                            if (ImGui.Combo($"##{filterName}", ref item, _stashTabNamesByIndex,
                                _stashTabNamesByIndex.Length))
                            {
                                indexNode.Value = _stashTabNamesByIndex[item];
                                OnSettingsStashNameChanged(indexNode, _stashTabNamesByIndex[item]);
                            }

                            ImGui.NextColumn();
                            ImGui.Columns(1, "", false);
                            var pop = true;

                            if (!ImGui.BeginPopupModal(formattableString, ref pop,
                                ImGuiWindowFlags.NoResize | ImGuiWindowFlags.AlwaysAutoResize)) continue;
                            var x = 0;

                            foreach (var name in _renamedAllStashNames)
                            {
                                x++;

                                if (ImGui.Button($"{name}", new Vector2N(100, 20)))
                                {
                                    indexNode.Value = name;
                                    OnSettingsStashNameChanged(indexNode, name);
                                    ImGui.CloseCurrentPopup();
                                }

                                if (x % 10 != 0)
                                    ImGui.SameLine();
                            }

                            ImGui.Spacing();
                            ImGuiNative.igIndent(350);
                            if (ImGui.Button("Close", new Vector2N(100, 20)))
                                ImGui.CloseCurrentPopup();

                            ImGui.EndPopup();
                        }
                        else
                        {
                            indexNode = new ListIndexNode { Value = "Ignore", Index = -1 };
                        }
                };
        }

        private void LoadCustomRefills()
        {
            _customRefills = RefillParser.Parse(ConfigDirectory);
            if (_customRefills.Count == 0) return;

            foreach (var refill in _customRefills)
            {
                if (!Settings.CustomRefillOptions.TryGetValue(refill.MenuName, out var amountOption))
                {
                    amountOption = new RangeNode<int>(15, 0, refill.StackSize);
                    Settings.CustomRefillOptions.Add(refill.MenuName, amountOption);
                }

                amountOption.Max = refill.StackSize;
                refill.AmountOption = amountOption;
            }

            _settingsListNodes.Add(Settings.CurrencyStashTab);
        }

        public override Job Tick()
        {
            if (Core.ParallelRunner.FindByName("Stashie_DropItemsToStash") == null)
            {
                if (Settings.SwitchFilterhotkey.PressedOnce())
                {
                    secondaryFilterActive = !secondaryFilterActive;
                    SetupOrClose();
                    LogMessage($"Stashie: Currently active Filter: {(!secondaryFilterActive ? "primary" : "secondary")}", 5);
                }
            }
            if (!stashingRequirementsMet() && Core.ParallelRunner.FindByName("Stashie_DropItemsToStash") != null)
            {
                StopCoroutine("Stashie_DropItemsToStash");
                return null;
            }

            if (Settings.DropHotkey.PressedOnce())
            {
                if (Core.ParallelRunner.FindByName("Stashie_DropItemsToStash") == null)
                {
                    StartDropItemsToStashCoroutine();
                }
                else
                {
                    StopCoroutine("Stashie_DropItemsToStash");
                }
            }
            return null;
        }

        private void StartDropItemsToStashCoroutine()
        {
            _debugTimer.Reset();
            _debugTimer.Start();
            Core.ParallelRunner.Run(new Coroutine(DropToStashRoutine(), this, "Stashie_DropItemsToStash"));
        }

        private void StopCoroutine(string routineName)
        {
            var routine = Core.ParallelRunner.FindByName(routineName);
            routine?.Done();
            _debugTimer.Stop();
            _debugTimer.Reset();
            CleanUp();
        }
        private IEnumerator DropToStashRoutine()
        {
            var cursorPosPreMoving = Input.ForceMousePosition; //saving cursorposition
            //try stashing items 3 times
            var originTab = GetIndexOfCurrentVisibleTab();
            yield return ParseItems();
            for (int tries = 0; tries < 3 && _dropItems.Count > 0; ++tries)
            {
                if (_dropItems.Count > 0)
                    yield return StashItemsIncrementer();
                yield return ParseItems();
                yield return new WaitTime(Settings.ExtraDelay);
            }
            //yield return ProcessRefills(); currently bugged
            if (Settings.VisitTabWhenDone.Value)
            {
                if (Settings.BackToOriginalTab.Value)
                {
                    yield return SwitchToTab(originTab);
                }
                else
                {
                    yield return SwitchToTab(Settings.TabToVisitWhenDone.Value);
                }
            }


            //restoring cursorposition
            Input.SetCursorPos(cursorPosPreMoving);
            Input.MouseMove();
            StopCoroutine("Stashie_DropItemsToStash");
        }

        private void CleanUp()
        {
            Input.KeyUp(Keys.LControlKey);
            Input.KeyUp(Keys.Shift);
        }

        private bool stashingRequirementsMet()
        {
            return GameController.Game.IngameState.IngameUi.InventoryPanel.IsVisible &&
                    GameController.Game.IngameState.IngameUi.StashElement.IsVisibleLocal;
        }

        private IEnumerator ProcessSwitchToTab(int index)
        {
            _debugTimer.Restart();
            yield return SwitchToTab(index);
            _coroutineWorker = Core.ParallelRunner.FindByName(CoroutineName);
            _coroutineWorker?.Done();

            _debugTimer.Restart();
            _debugTimer.Stop();
        }

        private IEnumerator ParseItems()
        {
            var inventory = GameController.Game.IngameState.Data.ServerData.PlayerInventories[0].Inventory;
            var invItems = inventory.InventorySlotItems;

            yield return new WaitFunctionTimed(() => invItems != null, true, 500, "ServerInventory->InventSlotItems is null!");
            _dropItems = new List<FilterResult>();
            _clickWindowOffset = GameController.Window.GetWindowRectangle().TopLeft;
            foreach (var invItem in invItems)
            {
                if (invItem.Item == null || invItem.Address == 0) continue;
                if (CheckIgnoreCells(invItem)) continue;

                var testItem = new ItemData(invItem.Item, GameController.Files);
                var result = CheckFilters(testItem, calculateClickPos(invItem));
                if (result != null)
                    _dropItems.Add(result);
            }
        }
        private Vector2 calculateClickPos(InventSlotItem invItem)
        {
            //hacky clickpos calc work

            var InventoryPanelRectF = GameController.IngameState.IngameUi.InventoryPanel[InventoryIndex.PlayerInventory].GetClientRect();
            var CellWidth = InventoryPanelRectF.Width / 12;
            var CellHeight = InventoryPanelRectF.Height / 5;
            var itemInventPosition = invItem.InventoryPosition;

            Vector2 clickpos = new Vector2(
                InventoryPanelRectF.Location.X + (CellWidth / 2) + (itemInventPosition.X * CellWidth),
                InventoryPanelRectF.Location.Y + (CellHeight / 2) + (itemInventPosition.Y * CellHeight)
                );

            //LogMessage($"CellWidth={CellWidth}, CellHeight={CellWidth}",5);
            //LogMessage($"invPanelRectF.TopLeft.X = {InventoryPanelRectF.TopLeft.X}, invPanelRectF.TopLeft.Y = {InventoryPanelRectF.Location.Y}", 5);
            //LogMessage($"itemInventPosition.X ={itemInventPosition.X}, itemInventPosition.Y ={itemInventPosition.Y}",5);
            //LogMessage($"Clickpos.X = {clickpos.X}, Clickpos.Y = {clickpos.Y}",5);

            return clickpos;
        }

        private bool CheckIgnoreCells(InventSlotItem inventItem)
        {
            var inventPosX = inventItem.PosX;
            var inventPosY = inventItem.PosY;

            if (Settings.RefillCurrency &&
                _customRefills.Any(x => x.InventPos.X == inventPosX && x.InventPos.Y == inventPosY))
                return true;

            if (inventPosX < 0 || inventPosX >= 12) return true;

            if (inventPosY < 0 || inventPosY >= 5) return true;

            return Settings.IgnoredCells[inventPosY, inventPosX] != 0; //No need to check all item size
        }

        private FilterResult CheckFilters(ItemData itemData, Vector2 clickPos)
        {
            foreach (var filter in currentFilter)
            {
                try
                {
                    if (!filter.AllowProcess) continue;

                    if (filter.CompareItem(itemData, filter.Query)) return new FilterResult(filter, itemData, clickPos);
                }
                catch (Exception ex)
                {
                    DebugWindow.LogError($"Check filters error: {ex}");
                }
            }
            return null;
        }

        private IEnumerator StashItemsIncrementer()
        {
            _coroutineIteration++;

            yield return StashItems();
        }
        private IEnumerator StashItems()
        {
            PublishEvent("stashie_start_drop_items", null);

            _visibleStashIndex = GetIndexOfCurrentVisibleTab();
            if (_visibleStashIndex < 0)
            {
                LogMessage($"Stshie: VisibleStashIndex was invalid: {_visibleStashIndex}, stopping.");
                yield break;
            }
            var itemsSortedByStash = _dropItems.OrderBy(x => x.SkipSwitchTab || x.StashIndex == _visibleStashIndex ? 0 : 1).ThenBy(x => x.StashIndex).ToList();
            var waitedItems = new List<FilterResult>(8);

            Input.KeyDown(Keys.LControlKey);
            LogMessage($"Want to drop {itemsSortedByStash.Count} items.");
            foreach (var stashresult in itemsSortedByStash)
            {
                _coroutineIteration++;
                _coroutineWorker?.UpdateTicks(_coroutineIteration);
                var maxTryTime = _debugTimer.ElapsedMilliseconds + 2000;
                //move to correct tab
                if (!stashresult.SkipSwitchTab)
                    yield return SwitchToTab(stashresult.StashIndex);
                //this is shenanigans for items that take some time to get dumped like maps into maptab and divcards in divtab
                /*
                var waited = waitedItems.Count > 0;
                while (waited)
                {
                    waited = false;
                    var visibleInventoryItems = GameController.Game.IngameState.IngameUi
                                .InventoryPanel[InventoryIndex.PlayerInventory]
                                .VisibleInventoryItems;
                    foreach(var item in waitedItems)
                    {
                        if (!visibleInventoryItems.Contains(item.ItemData.InventoryItem)) continue;
                        yield return ClickElement(item.ClickPos);
                        waited = true;
                    }
                    yield return new WaitTime(Settings.ExtraDelay);
                    PublishEvent("stashie_finish_drop_items_to_stash_tab", null);
                    if (!waited) waitedItems.Clear();
                    if (_debugTimer.ElapsedMilliseconds > maxTryTime)
                    {
                        LogMessage($"Error while waiting for:{waitedItems.Count} items");
                        yield break;
                    }
                    yield return new WaitTime((int)GameController.IngameState.CurLatency); //maybe replace with Setting option
                }*/
                yield return new WaitFunctionTimed(() => GameController.IngameState.IngameUi.StashElement.AllInventories[_visibleStashIndex] != null,
                    true, 2000, $"Error while loading tab, Index: {_visibleStashIndex}"); //maybe replace waittime with Setting option
                yield return new WaitFunctionTimed(() => GetTypeOfCurrentVisibleStash() != InventoryType.InvalidInventory,
                    true, 2000, $"Error with inventory type, Index: {_visibleStashIndex}"); //maybe replace waittime with Setting option

                yield return StashItem(stashresult);

                _debugTimer.Restart();
                PublishEvent("stashie_finish_drop_items_to_stash_tab", null);
            }
        }

        private IEnumerator StashItem(FilterResult stashresult)
        {
            Input.SetCursorPos(stashresult.ClickPos + _clickWindowOffset);
            yield return new WaitTime(Settings.HoverItemDelay);
            /*
           //set cursor and update hoveritem
           yield return Settings.HoverItemDelay;

           var inventory = GameController.IngameState.IngameUi.InventoryPanel[InventoryIndex.PlayerInventory];
           while (inventory.HoverItem == null)
           {
               if (_debugTimer.ElapsedMilliseconds > maxTryTime)
               {
                   LogMessage($"Error while waiting for hover item. hoveritem is null, Index: {_visibleStashIndex}");
                   yield break;
               }
               Input.SetCursorPos(stashresult.ClickPos + _clickWindowOffset);
               yield return Settings.HoverItemDelay;
           }
           if (lastHoverItem != null)
           {
               while (inventory.HoverItem == null || inventory.HoverItem.Address == lastHoverItem.Address)
               {
                   if (_debugTimer.ElapsedMilliseconds > maxTryTime)
                   {
                       LogMessage($"Error while waiting for hover item. hoveritem is null, Index: {_visibleStashIndex}");
                       yield break;
                   }
                   Input.SetCursorPos(stashresult.ClickPos + _clickWindowOffset);
                   yield return Settings.HoverItemDelay;
               }
           }
           lastHoverItem = inventory.HoverItem;
           */
            //finally press the button
            //additional shift to circumvent affinities
            bool shiftused = false;
            if (stashresult.ShiftForStashing)
            {
                Input.KeyDown(Keys.ShiftKey);
                shiftused = true;
            }
            Input.Click(MouseButtons.Left);
            if (shiftused)
            {
                Input.KeyUp(Keys.ShiftKey);
            }

            yield return new WaitTime(Settings.StashItemDelay);
        }
        #region Refill

        private IEnumerator ProcessRefills()
        {
            if (!Settings.RefillCurrency.Value || _customRefills.Count == 0) yield break;

            if (Settings.CurrencyStashTab.Index == -1)
            {
                LogError("Can't process refill: CurrencyStashTab is not set.", 5);
                yield break;
            }

            var delay = (int)GameController.Game.IngameState.ServerData.Latency + Settings.ExtraDelay.Value;
            var currencyTabVisible = false;
            var inventory = GameController.Game.IngameState.IngameUi.InventoryPanel[InventoryIndex.PlayerInventory];
            var stashItems = inventory.VisibleInventoryItems;

            if (stashItems == null)
            {
                LogError("Can't process refill: VisibleInventoryItems is null!", 5);
                yield break;
            }

            _customRefills.ForEach(x => x.Clear());
            var filledCells = new int[5, 12];

            foreach (var inventItem in stashItems)
            {
                var item = inventItem.Item;
                if (item == null) continue;

                if (!Settings.AllowHaveMore.Value)
                {
                    var iPosX = inventItem.InventPosX;
                    var iPosY = inventItem.InventPosY;
                    var iBase = item.GetComponent<Base>();

                    for (var x = iPosX; x <= iPosX + iBase.ItemCellsSizeX - 1; x++)
                        for (var y = iPosY; y <= iPosY + iBase.ItemCellsSizeY - 1; y++)
                            if (x >= 0 && x <= 11 && y >= 0 && y <= 4)
                                filledCells[y, x] = 1;
                            else
                                LogMessage($"Out of range: {x} {y}", 10);
                }

                if (!item.HasComponent<ExileCore.PoEMemory.Components.Stack>()) continue;

                foreach (var refill in _customRefills)
                {
                    var bit = GameController.Files.BaseItemTypes.Translate(item.Path);
                    if (bit.BaseName != refill.CurrencyClass) continue;

                    var stack = item.GetComponent<ExileCore.PoEMemory.Components.Stack>();
                    refill.OwnedCount = stack.Size;
                    refill.ClickPos = inventItem.GetClientRect().Center;

                    if (refill.OwnedCount < 0 || refill.OwnedCount > 40)
                    {
                        LogError(
                            $"Ignoring refill: {refill.CurrencyClass}: Stack size {refill.OwnedCount} not in range 0-40 ",
                            5);
                        refill.OwnedCount = -1;
                    }

                    break;
                }
            }

            var inventoryRec = inventory.InventoryUIElement.GetClientRect();
            var cellSize = inventoryRec.Width / 12;
            var freeCellFound = false;
            var freeCelPos = new Point();

            if (!Settings.AllowHaveMore.Value)
                for (var x = 0; x <= 11; x++)
                {
                    for (var y = 0; y <= 4; y++)
                    {
                        if (filledCells[y, x] != 0) continue;

                        freeCellFound = true;
                        freeCelPos = new Point(x, y);
                        break;
                    }

                    if (freeCellFound) break;
                }

            foreach (var refill in _customRefills)
            {
                if (refill.OwnedCount == -1) continue;

                if (refill.OwnedCount == refill.AmountOption.Value) continue;

                if (refill.OwnedCount < refill.AmountOption.Value)

                #region Refill

                {
                    if (!currencyTabVisible)
                    {
                        if (Settings.CurrencyStashTab.Index != _visibleStashIndex)
                        {
                            yield return SwitchToTab(Settings.CurrencyStashTab.Index);
                        }
                        else
                        {
                            currencyTabVisible = true;
                            yield return new WaitTime(delay);
                        }
                    }

                    var moveCount = refill.AmountOption.Value - refill.OwnedCount;
                    var currentStashItems = GameController.Game.IngameState.IngameUi.StashElement.VisibleStash
                        .VisibleInventoryItems;

                    var foundSourceOfRefill = currentStashItems
                        .Where(x => GameController.Files.BaseItemTypes.Translate(x.Item.Path).BaseName ==
                                    refill.CurrencyClass).ToList();

                    foreach (var sourceOfRefill in foundSourceOfRefill)
                    {
                        var stackSize = sourceOfRefill.Item.GetComponent<ExileCore.PoEMemory.Components.Stack>().Size;
                        var getCurCount = moveCount > stackSize ? stackSize : moveCount;
                        var destination = refill.ClickPos;

                        if (refill.OwnedCount == 0)
                        {
                            destination = GetInventoryClickPosByCellIndex(inventory, refill.InventPos.X,
                                refill.InventPos.Y, cellSize);

                            // If cells is not free then continue.
                            if (GameController.Game.IngameState.IngameUi.InventoryPanel[InventoryIndex.PlayerInventory][
                                refill.InventPos.X, refill.InventPos.Y, 12] != null)
                            {
                                moveCount--;
                                LogMessage(
                                    $"Inventory ({refill.InventPos.X}, {refill.InventPos.Y}) is occupied by the wrong item!",
                                    5);
                                continue;
                            }
                        }

                        yield return SplitStack(moveCount, sourceOfRefill.GetClientRect().Center, destination);
                        moveCount -= getCurCount;
                        if (moveCount == 0) break;
                    }

                    if (moveCount > 0)
                        LogMessage($"Not enough currency (need {moveCount} more) to fill {refill.CurrencyClass} stack",
                            5);
                }

                #endregion

                else if (!Settings.AllowHaveMore.Value && refill.OwnedCount > refill.AmountOption.Value)

                #region Devastate

                {
                    if (!freeCellFound)
                    {
                        LogMessage("Can\'t find free cell in player inventory to move excess currency.", 5);
                        continue;
                    }

                    if (!currencyTabVisible)
                    {
                        if (Settings.CurrencyStashTab.Index != _visibleStashIndex)
                        {
                            yield return SwitchToTab(Settings.CurrencyStashTab.Index);
                            continue;
                        }

                        currencyTabVisible = true;
                        yield return new WaitTime(delay);
                    }

                    var destination = GetInventoryClickPosByCellIndex(inventory, freeCelPos.X, freeCelPos.Y, cellSize) +
                                      _clickWindowOffset;
                    var moveCount = refill.OwnedCount - refill.AmountOption.Value;
                    yield return new WaitTime(delay);
                    yield return SplitStack(moveCount, refill.ClickPos, destination);
                    yield return new WaitTime(delay);
                    Input.KeyDown(Keys.LControlKey);

                    yield return Input.SetCursorPositionSmooth(destination + _clickWindowOffset);
                    yield return new WaitTime(Settings.ExtraDelay);
                    Input.Click(MouseButtons.Left);
                    Input.MouseMove();
                    Input.KeyUp(Keys.LControlKey);
                    yield return new WaitTime(delay);
                }

                #endregion
            }
        }

        private static Vector2 GetInventoryClickPosByCellIndex(Inventory inventory, int indexX, int indexY,
            float cellSize)
        {
            return inventory.InventoryUIElement.GetClientRect().TopLeft +
                   new Vector2(cellSize * (indexX + 0.5f), cellSize * (indexY + 0.5f));
        }

        private IEnumerator SplitStack(int amount, Vector2 from, Vector2 to)
        {
            var delay = (int)GameController.Game.IngameState.ServerData.Latency * 2 + Settings.ExtraDelay;
            Input.KeyDown(Keys.ShiftKey);

            while (!Input.IsKeyDown(Keys.ShiftKey)) yield return new WaitTime(WhileDelay);

            yield return Input.SetCursorPositionSmooth(from + _clickWindowOffset);
            yield return new WaitTime(Settings.ExtraDelay);
            Input.Click(MouseButtons.Left);
            Input.MouseMove();
            yield return new WaitTime(InputDelay);
            Input.KeyUp(Keys.ShiftKey);
            yield return new WaitTime(InputDelay + 50);

            if (amount > 40)
            {
                LogMessage("Can't select amount more than 40, current value: " + amount, 5);
                amount = 40;
            }

            if (amount < 10)
            {
                var keyToPress = (int)Keys.D0 + amount;
                yield return Input.KeyPress((Keys)keyToPress);
            }
            else
            {
                var keyToPress = (int)Keys.D0 + amount / 10;
                yield return Input.KeyPress((Keys)keyToPress);
                yield return new WaitTime(delay);
                keyToPress = (int)Keys.D0 + amount % 10;
                yield return Input.KeyPress((Keys)keyToPress);
            }

            yield return new WaitTime(delay);
            yield return Input.KeyPress(Keys.Enter);
            yield return new WaitTime(delay + InputDelay);

            yield return Input.SetCursorPositionSmooth(to + _clickWindowOffset);
            yield return new WaitTime(Settings.ExtraDelay);
            Input.Click(MouseButtons.Left);

            yield return new WaitTime(delay + InputDelay);
        }

        #endregion

        #region Switching between StashTabs

        public IEnumerator SwitchToTab(int tabIndex)
        {
            // We don't want to Switch to a tab that we are already on or that has the magic number for affinities
            //var stashPanel = GameController.Game.IngameState.IngameUi.StashElement;

            _visibleStashIndex = GetIndexOfCurrentVisibleTab();
            var travelDistance = Math.Abs(tabIndex - _visibleStashIndex);
            if (travelDistance == 0) yield break;

            if (Settings.AlwaysUseArrow.Value || travelDistance < 2 || !SliderPresent())
                yield return SwitchToTabViaArrowKeys(tabIndex);
            else
                yield return SwitchToTabViaDropdownMenu(tabIndex);

            yield return Delay();
        }

        private IEnumerator SwitchToTabViaArrowKeys(int tabIndex, int numberOfTries = 1)
        {
            if (numberOfTries >= 3)
            {
                yield break;
            }

            var indexOfCurrentVisibleTab = GetIndexOfCurrentVisibleTab();
            var travelDistance = tabIndex - indexOfCurrentVisibleTab;
            var tabIsToTheLeft = travelDistance < 0;
            travelDistance = Math.Abs(travelDistance);

            if (tabIsToTheLeft)
            {
                yield return PressKey(Keys.Left, travelDistance);
            }
            else
            {
                yield return PressKey(Keys.Right, travelDistance);
            }

            if (GetIndexOfCurrentVisibleTab() != tabIndex)
            {
                yield return Delay(20);
                yield return SwitchToTabViaArrowKeys(tabIndex, numberOfTries + 1);
            }
        }

        private IEnumerator PressKey(Keys key, int repetitions = 1)
        {
            for (var i = 0; i < repetitions; i++)
            {
                yield return Input.KeyPress(key);
            }
        }

        private bool DropDownMenuIsVisible()
        {
            return GameController.Game.IngameState.IngameUi.StashElement.ViewAllStashPanel.IsVisible;
        }

        private IEnumerator OpenDropDownMenu()
        {
            var button = GameController.Game.IngameState.IngameUi.StashElement.ViewAllStashButton.GetClientRect();
            yield return ClickElement(button.Center);
            while (!DropDownMenuIsVisible())
            {
                yield return Delay(1);
            }
        }

        private static bool StashLabelIsClickable(int index)
        {
            return index + 1 < MaxShownSidebarStashTabs;
        }

        private bool SliderPresent()
        {
            return _stashCount > MaxShownSidebarStashTabs;
        }

        private IEnumerator ClickDropDownMenuStashTabLabel(int tabIndex)
        {
            var dropdownMenu = GameController.Game.IngameState.IngameUi.StashElement.ViewAllStashPanel;
            var stashTabLabels = dropdownMenu.GetChildAtIndex(1);

            //if the stash tab index we want to visit is less or equal to 30, then we scroll all the way to the top.
            //scroll amount (clicks) should always be (stash_tab_count - 31);
            //TODO(if the guy has more than 31*2 tabs and wants to visit stash tab 32 fx, then we need to scroll all the way up (or down) and then scroll 13 clicks after.)

            var clickable = StashLabelIsClickable(tabIndex);
            // we want to go to stash 32 (index 31).
            // 44 - 31 = 13
            // 31 + 45 - 44 = 30
            // MaxShownSideBarStashTabs + _stashCount - tabIndex = index
            var index = clickable ? tabIndex : tabIndex - (_stashCount - 1 - (MaxShownSidebarStashTabs - 1));
            var pos = stashTabLabels.GetChildAtIndex(index).GetClientRect().Center;
            MoveMouseToElement(pos);
            if (SliderPresent())
            {
                var clicks = _stashCount - MaxShownSidebarStashTabs;
                yield return Delay(3);
                VerticalScroll(scrollUp: clickable, clicks: clicks);
                yield return Delay(3);
            }

            DebugWindow.LogMsg($"Stashie: Moving to tab '{tabIndex}'.", 3, Color.LightGray);
            yield return Click();
        }

        private IEnumerator ClickElement(Vector2 pos, MouseButtons mouseButton = MouseButtons.Left)
        {
            MoveMouseToElement(pos);
            yield return Click(mouseButton);
        }

        private IEnumerator Click(MouseButtons mouseButton = MouseButtons.Left)
        {
            Input.Click(mouseButton);
            yield return Delay();
        }

        private void MoveMouseToElement(Vector2 pos)
        {
            Input.SetCursorPos(pos + GameController.Window.GetWindowRectangle().TopLeft);
        }

        private IEnumerator Delay(int ms = 0)
        {
            yield return new WaitTime(Settings.ExtraDelay.Value + ms);
        }

        private IEnumerator SwitchToTabViaDropdownMenu(int tabIndex)
        {
            if (!DropDownMenuIsVisible())
            {
                yield return OpenDropDownMenu();
            }

            yield return ClickDropDownMenuStashTabLabel(tabIndex);
        }

        private int GetIndexOfCurrentVisibleTab()
        {
            return GameController.Game.IngameState.IngameUi.StashElement.IndexVisibleStash;
        }

        private InventoryType GetTypeOfCurrentVisibleStash()
        {
            var stashPanelVisibleStash = GameController.Game.IngameState.IngameUi?.StashElement?.VisibleStash;
            return stashPanelVisibleStash?.InvType ?? InventoryType.InvalidInventory;
        }

        #endregion

        #region Stashes update

        private void OnSettingsStashNameChanged(ListIndexNode node, string newValue)
        {
            node.Index = GetInventIndexByStashName(newValue);
        }

        public override void OnClose()
        {
        }

        private void SetupOrClose()
        {
            SaveDefaultConfigsToDisk();
            _settingsListNodes = new List<ListIndexNode>(100);
            LoadCustomRefills();
            LoadCustomFilters();

            try
            {
                Settings.TabToVisitWhenDone.Max =
                    (int)GameController.Game.IngameState.IngameUi.StashElement.TotalStashes - 1;
                var names = GameController.Game.IngameState.IngameUi.StashElement.AllStashNames;
                UpdateStashNames(names);
            }
            catch (Exception e)
            {
                LogError($"Cant get stash names when init. {e}");
            }
        }

        private int GetInventIndexByStashName(string name)
        {
            var index = _renamedAllStashNames.IndexOf(name);
            if (index != -1) index--;

            return index;
        }

        private List<string> _renamedAllStashNames;

        private void UpdateStashNames(ICollection<string> newNames)
        {
            Settings.AllStashNames = newNames.ToList();

            if (newNames.Count < 4)
            {
                LogError("Can't parse names.");
                return;
            }

            _renamedAllStashNames = new List<string> { "Ignore" };
            var settingsAllStashNames = Settings.AllStashNames;

            for (var i = 0; i < settingsAllStashNames.Count; i++)
            {
                var realStashName = settingsAllStashNames[i];

                if (_renamedAllStashNames.Contains(realStashName))
                {
                    realStashName += " (" + i + ")";
#if DebugMode
                    LogMessage("Stashie: fixed same stash name to: " + realStashName, 3);
#endif
                }

                _renamedAllStashNames.Add(realStashName ?? "%NULL%");
            }

            Settings.AllStashNames.Insert(0, "Ignore");

            foreach (var lOption in _settingsListNodes)
                try
                {
                    lOption.SetListValues(_renamedAllStashNames);
                    var inventoryIndex = GetInventIndexByStashName(lOption.Value);

                    if (inventoryIndex == -1) //If the value doesn't exist in list (renamed)
                    {
                        if (lOption.Index != -1) //If the value doesn't exist in list and the value was not Ignore
                        {
#if DebugMode
                        LogMessage("Tab renamed : " + lOption.Value + " to " + _renamedAllStashNames[lOption.Index + 1],
                            5);
#endif
                            if (lOption.Index + 1 >= _renamedAllStashNames.Count)
                            {
                                lOption.Index = -1;
                                lOption.Value = _renamedAllStashNames[0];
                            }
                            else
                            {
                                lOption.Value = _renamedAllStashNames[lOption.Index + 1]; //    Just update it's name
                            }
                        }
                        else
                        {
                            lOption.Value =
                                _renamedAllStashNames[0]; //Actually it was "Ignore", we just update it (can be removed)
                        }
                    }
                    else //tab just change it's index
                    {
#if DebugMode
                    if (lOption.Index != inventoryIndex)
                    {
                        LogMessage("Tab moved: " + lOption.Index + " to " + inventoryIndex, 5);
                    }
#endif
                        lOption.Index = inventoryIndex;
                        lOption.Value = _renamedAllStashNames[inventoryIndex + 1];
                    }
                }
                catch (Exception e)
                {
                    DebugWindow.LogError($"UpdateStashNames _settingsListNodes {e}");
                }

            GenerateMenu();
        }

        private static readonly WaitTime Wait2Sec = new WaitTime(2000);
        private static readonly WaitTime Wait1Sec = new WaitTime(1000);
        private uint _counterStashTabNamesCoroutine;

        public IEnumerator StashTabNamesUpdater_Thread()
        {
            while (true)
            {
                while (!GameController.Game.IngameState.InGame) yield return Wait2Sec;

                var stashPanel = GameController.Game.IngameState?.IngameUi?.StashElement;

                while (stashPanel == null || !stashPanel.IsVisibleLocal) yield return Wait1Sec;

                _counterStashTabNamesCoroutine++;
                _stashTabNamesCoroutine?.UpdateTicks(_counterStashTabNamesCoroutine);
                var cachedNames = Settings.AllStashNames;
                var realNames = stashPanel.AllStashNames;

                if (realNames.Count + 1 != cachedNames.Count)
                {
                    UpdateStashNames(realNames);
                    continue;
                }

                for (var index = 0; index < realNames.Count; ++index)
                {
                    var cachedName = cachedNames[index + 1];
                    if (cachedName.Equals(realNames[index])) continue;

                    UpdateStashNames(realNames);
                    break;
                }

                yield return Wait1Sec;
            }
        }

        private static void VerticalScroll(bool scrollUp, int clicks)
        {
            const int wheelDelta = 120;
            if (scrollUp)
                WinApi.mouse_event(Input.MOUSE_EVENT_WHEEL, 0, 0, clicks * wheelDelta, 0);
            else
                WinApi.mouse_event(Input.MOUSE_EVENT_WHEEL, 0, 0, -(clicks * wheelDelta), 0);
        }

        #endregion
    }
}