using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.Elements.InventoryElements;
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
        public uint InventoryID { get; }
        public Vector2 clientRect { get; }

        public ItemData(InventSlotItem inventoryItem, BaseItemType baseItemType, Vector2 clickPos)
        {

            var item = inventoryItem.Item;
            InventoryID = inventoryItem.Item.InventoryId;

            Path = item.Path;
            var baseComponent = item.GetComponent<Base>();
            if (baseComponent == null) return;
            isElder = baseComponent.isElder;
            isShaper = baseComponent.isShaper;
            isCorrupted = baseComponent.isCorrupted;
            isCrusader = baseComponent.isCrusader;
            isRedeemer = baseComponent.isRedeemer;
            isWarlord = baseComponent.isWarlord;
            isHunter = baseComponent.isHunter;
            isInfluenced = isCrusader || isRedeemer || isWarlord || isHunter || isShaper || isElder;
            ScourgeTier = baseComponent.ScourgedTier;
            if(baseItemType.ClassName == "HeistContract")
            {
                var heistComp = item.GetComponent<HeistContract>();
                HeistContractJobType = heistComp.RequiredJob.Name ?? "";
                HeistContractReqJobLevel = heistComp?.RequiredJobLevel ?? 0;
            }

            var mods = item.GetComponent<Mods>();
            Rarity = mods?.ItemRarity ?? ItemRarity.Normal;
            BIdentified = mods?.Identified ?? true;
            ItemLevel = mods?.ItemLevel ?? 0;
            Veiled = mods?.ItemMods.Where(m => m.DisplayName.Contains("Veil")).Count() ?? 0;
            Fractured = mods?.CountFractured ?? 0;
            SkillGemLevel = item.GetComponent<SkillGem>()?.Level ?? 0;
            //SkillGemQualityType = (int)item.GetComponent<SkillGem>()?.QualityType;
            Synthesised = mods?.Synthesised ?? false;
            isBlightMap = mods?.ItemMods.Where(m => m.Name.Contains("InfectedMap")).Count() > 0;
            isElderGuardianMap = mods?.ItemMods.Where(m => m.Name.Contains("MapElderContainsBoss")).Count() > 0;
            Enchanted = mods?.ItemMods.Where(m => m.Name.Contains("Enchantment")).Count() > 0;
            DeliriumStacks = mods?.ItemMods.Where(m => m.Name.Contains("AfflictionMapReward")).Count() ?? 0;
            ModsNames = mods?.ItemMods.Select(mod => mod.Name).ToList();

            NumberOfSockets = item.GetComponent<Sockets>()?.NumberOfSockets ?? 0;
            LargestLinkSize = item.GetComponent<Sockets>()?.LargestLinkSize ?? 0;

            ItemQuality = item.GetComponent<Quality>()?.ItemQuality ?? 0;
            ClassName = baseItemType.ClassName;
            BaseName = baseItemType.BaseName;

            Name = baseComponent?.Name ?? "";
            Description = "";
            MapTier = item.GetComponent<Map>()?.Tier ?? 0;

            clientRect = clickPos;

            Name = mods?.UniqueName ?? Name;           
        }
        
        public Vector2 GetClickPosCache()
        {
            return clientRect;
        }
        /*
        [Obsolete]
        public Vector2 GetClickPos()
        {
            var paddingPixels = 3;
            var clientRect = InventoryItem.GetClientRect();
            var x = MathHepler.Randomizer.Next((int) clientRect.TopLeft.X + paddingPixels, (int) clientRect.TopRight.X - paddingPixels);
            var y = MathHepler.Randomizer.Next((int) clientRect.TopLeft.Y + paddingPixels, (int) clientRect.BottomLeft.Y - paddingPixels);
            return new Vector2(x, y);
        }*/
    }
}
