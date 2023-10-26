using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.Elements.InventoryElements;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.PoEMemory.Models;
using ExileCore.Shared.Enums;
using ExileCore.Shared.Helpers;
using SharpDX;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using static ExileCore.PoEMemory.MemoryObjects.ServerInventory;

namespace Stashie
{
    public class ItemData
    {
        private const string ClassNameHeistContract = "HeistContract";
        private const string ModNameVeil = "Veil";
        private const string ModNameEnchantment = "Enchantment";
        private const string ModNameInfectedMap = "InfectedMap";
        private const string ModNameAfflictionMapReward = "AfflictionMapReward";
        private const string ModNameMapElderContainsBoss = "MapElderContainsBoss";

        public string Path { get; }
        public string ClassName { get; }
        public string BaseName { get; }
        public string Name { get; }
        public string Description { get; }
        public string ProphecyName { get; }
        public string ProphecyDescription { get; }
        public string HeistContractJobType { get; }
        //public string ClusterJewelBase { get; }
        public ItemRarity Rarity { get; }
        public int ItemQuality { get; }
        public int Veiled { get; }
        public int Fractured { get; }
        public int ItemLevel { get; }
        public int MapTier { get; }
        public int NumberOfSockets { get; }
        public int LargestLinkSize { get; }
        public int DeliriumStacks { get; }
        //public int ClusterJewelpassives { get; }
        public int HeistContractReqJobLevel { get; }
        public int ScourgeTier { get; }
        public bool BIdentified { get; }
        public bool isCorrupted { get; }
        public bool isElder { get; }
        public bool isShaper { get; }
        public bool isCrusader { get; }
        public bool isRedeemer { get; }
        public bool isHunter { get; }
        public bool isWarlord { get; }
        public bool isInfluenced { get; }
        public bool Synthesised { get; }
        public bool isBlightMap { get; }
        public bool isElderGuardianMap { get; }
        public bool Enchanted { get; }
        public int SkillGemLevel { get; }
        public int SkillGemQualityType { get; }
        public List<string> ModsNames { get; }
        public List<ItemMod> ItemMods { get; }
        public uint InventoryID { get; }
        public Vector2 clientRect { get; }

        public ItemData(InventSlotItem inventoryItem, BaseItemType baseItemType, Vector2 clickPos)
        {
            if (inventoryItem.Item == null) return;

            // Component Declarations
            var item = inventoryItem.Item;
            item.TryGetComponent<Map>(out var mapComp);
            item.TryGetComponent<Base>(out var baseComp);
            item.TryGetComponent<Mods>(out var modsComp);
            item.TryGetComponent<Sockets>(out var socketsComp);
            item.TryGetComponent<Quality>(out var qualityComp);
            item.TryGetComponent<SkillGem>(out var skillGemComp);
            item.TryGetComponent<HeistContract>(out var heistComp);


            // Processing Components
            Path = item.Path;
            InventoryID = item.InventoryId;
            ScourgeTier = baseComp?.ScourgedTier ?? 0;
            isElder = baseComp?.isElder ?? false;
            isShaper = baseComp?.isShaper ?? false;
            isHunter = baseComp?.isHunter ?? false;
            isWarlord = baseComp?.isWarlord ?? false;
            isCrusader = baseComp?.isCrusader ?? false;
            isRedeemer = baseComp?.isRedeemer ?? false;
            isCorrupted = baseComp?.isCorrupted ?? false;
            isInfluenced = isCrusader || isRedeemer || isWarlord || isHunter || isShaper || isElder;

            // Processing Heist Contract
            if (baseItemType.ClassName == ClassNameHeistContract)
            {
                HeistContractJobType = heistComp?.RequiredJob?.Name ?? "";
                HeistContractReqJobLevel = heistComp?.RequiredJobLevel ?? 0;
            }

            // Processing Mods
            ItemMods = modsComp?.ItemMods;
            Name = modsComp?.UniqueName ?? Name;
            ItemLevel = modsComp?.ItemLevel ?? 0;
            Fractured = modsComp?.CountFractured ?? 0;
            BIdentified = modsComp?.Identified ?? true;
            Synthesised = modsComp?.Synthesised ?? false;
            Enchanted = modsComp?.EnchantedMods?.Count > 0;
            Rarity = modsComp?.ItemRarity ?? ItemRarity.Normal;
            ModsNames = modsComp?.ItemMods?.Select(mod => mod.Name).ToList();
            Veiled = modsComp?.ItemMods?.Count(m => m.DisplayName.Contains(ModNameVeil)) ?? 0;
            isBlightMap = modsComp?.ItemMods?.Count(m => m.Name.Contains(ModNameInfectedMap)) > 0;
            DeliriumStacks = modsComp?.ItemMods?.Count(m => m.Name.Contains(ModNameAfflictionMapReward)) ?? 0;
            isElderGuardianMap = modsComp?.ItemMods?.Count(m => m.Name.Contains(ModNameMapElderContainsBoss)) > 0;

            // Processing Skill Gem
            SkillGemLevel = skillGemComp?.Level ?? 0;

            // Processing Sockets
            NumberOfSockets = socketsComp?.NumberOfSockets ?? 0;
            LargestLinkSize = socketsComp?.LargestLinkSize ?? 0;

            // Processing Quality
            ItemQuality = qualityComp?.ItemQuality ?? 0;

            // Other Assignments
            Description = "";
            Name = baseComp?.Name ?? "";
            BaseName = baseItemType.BaseName;
            ClassName = baseItemType.ClassName;

            // Processing Map
            MapTier = mapComp?.Tier ?? 0;

            // Final Assignment
            clientRect = clickPos;
        }

        public Vector2 GetClickPosCache()
        {
            return clientRect;
        }
    }
}