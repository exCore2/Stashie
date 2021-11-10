using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.Elements.InventoryElements;
using ExileCore.PoEMemory.Models;
using ExileCore.Shared.Enums;
using ExileCore.Shared.Helpers;
using SharpDX;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Stashie
{
    public class ItemData
    {
        //public static readonly List<string> goodRewards = new List<string>{ "Drops additional Currency Items", "Drops additional Currency Shards", "Drops additional Fossils", "Drops additional Divination Cards", "Drops additional Quality Gems", "Drops additional Map Fragments", "Drops additional Catalysts", "Drops additonal Essences", "Drops additional Legion Incubators", "Drops additional Polished Scarabs" };
        //private static readonly List<string> badRewards = new List<string> { "additionalveiledarmour", "additionalrareweapons", "additionalrarearmour", "additionalperanduscoins", "additionalraretalismans", "arareweapon" };
        //private static readonly List<string> mediocreRewards = new List<string> { "amapitem", "additionalmaps", "rarejewellery", "itemisedprophecies", "enchantedboots", "additionalrustedscarabs", "ashaperweapon", "auniqueweapon", "anabyssaljewel", "incursionweapon", "additonaluniqueitems", "additionalbreachsplinters" };
        //private static readonly HashSet<string> goodRewardsHS = new HashSet<string>(goodRewards);
        public NormalInventoryItem InventoryItem { get; }
        public string Path { get; }
        public string ClassName { get; }
        public string BaseName { get; }
        public string Name { get; }
        public string Description { get; }
        public string ProphecyName { get; }
        public string ProphecyDescription { get; }
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
        public int MetamorphSampleRewardsAmount { get; }
        public int MetamorphSampleGoodRewardsAmount { get; }
        public int MetamorphSampleBadRewardsAmount { get; }
        public uint InventoryID { get; }
        public Vector2 clientRect { get; }

        public ItemData(NormalInventoryItem inventoryItem, BaseItemType baseItemType)
        {
            InventoryItem = inventoryItem;
            InventoryID = inventoryItem.Item.InventoryId;
            var item = inventoryItem.Item;
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


            NumberOfSockets = item.GetComponent<Sockets>()?.NumberOfSockets ?? 0;
            LargestLinkSize = item.GetComponent<Sockets>()?.LargestLinkSize ?? 0;

            ItemQuality = item.GetComponent<Quality>()?.ItemQuality ?? 0;
            ClassName = baseItemType.ClassName;
            BaseName = baseItemType.BaseName;

            Name = baseComponent?.Name ?? "";
            Description = "";
            MapTier = item.GetComponent<Map>()?.Tier ?? 0;
            clientRect = InventoryItem.GetClientRect().Center;
            
            if (Name == "Prophecy")
            {
                BaseName = "Prophecy";
                var prophecyComponent = item.HasComponent<Prophecy>()? item.GetComponent<Prophecy>() : null;
                if (prophecyComponent == null) return;
                Name = prophecyComponent.DatProphecy?.Name?.ToLower() ?? "";
                Name = Regex.Replace(Name, @"[ ,']", "");
                Description = prophecyComponent.DatProphecy?.PredictionText?.ToLower() ?? "";
                Description = Regex.Replace(Description, @"[ ,']", "");
            }
            else
            {
                Name = mods?.UniqueName ?? "";
            }
            
            //if (BaseName.StartsWith("Metamorph"))
            //{
            //    var stats = mods?.HumanStats;
            //    if (stats != null)
            //    {
            //        MetamorphSampleRewardsAmount = stats.Count();                   
            //
            //        //var _stats = stats.Select(str => str.ToLower()).ToList();
            //        //_stats = _stats.Select(str => str.Replace(" ", "")).ToList();
            //        //_stats = _stats.Select(x => x.Substring(5)).ToList();
            //
            //        //MetamorphSampleGoodRewardsAmount = stats.Count(x => goodRewardsHS.Contains(x));
            //
            //        //MetamorphSampleGoodRewardsAmount = _stats.Where(stat => goodRewards.Any(rewards => rewards.Equals(stat))).Count();
            //        //MetamorphSampleBadRewardsAmount = _stats.Where(stat => badRewards.Any(rewards => rewards.Equals(stat))).Count();
            //    }else
            //    {
            //        MetamorphSampleRewardsAmount = -1;
            //    }
            //}            
        }
        
        public Vector2 GetClickPosCache()
        {
            return clientRect;
        }

        public Vector2 GetClickPos()
        {
            var paddingPixels = 3;
            var clientRect = InventoryItem.GetClientRect();
            var x = MathHepler.Randomizer.Next((int) clientRect.TopLeft.X + paddingPixels, (int) clientRect.TopRight.X - paddingPixels);
            var y = MathHepler.Randomizer.Next((int) clientRect.TopLeft.Y + paddingPixels, (int) clientRect.BottomLeft.Y - paddingPixels);
            return new Vector2(x, y);
        }
        
        //public override string ToString()
        //{
        //    /*
        //    FieldInfo[] fields = typeof(ItemData).GetFields(BindingFlags.NonPublic | BindingFlags.Instance);
        //    string str = "\n";
        //    foreach (var field in fields)
        //    {
        //        var name = field.Name;
        //        var value = field.GetValue(this).ToString();
        //        if (name == "goodRewards" || name == "goodRewardsHS" || name == "InventoryItem" || name == "clientRect") continue;
        //        str += name + ": " + value + "\n";
        //    }
        //    return str;
        //    */
        //    
        //    string itemdata = "\n" +
        //        nameof(InventoryID) + ": " + InventoryID + "\n" +
        //        nameof(Path) + ": " + Path + "\n" +
        //        nameof(ClassName) + ": " + ClassName + "\n" +
        //        nameof(BaseName) + ": " + BaseName + "\n" +
        //        nameof(Name) + ": " + Name + "\n" +
        //        nameof(Description) + ": " + Description + "\n" +
        //        nameof(ProphecyName) + ": " + ProphecyName + "\n" +
        //        nameof(ProphecyDescription) + ": " + ProphecyDescription + "\n" +
        //        nameof(Rarity) + ": " + Rarity + "\n" +
        //        nameof(ItemQuality) + ": " + ItemQuality + "\n" +
        //        nameof(Veiled) + ": " + Veiled + "\n" +
        //        nameof(Fractured) + ": " + Fractured + "\n" +
        //        nameof(ItemLevel) + ": " + ItemLevel + "\n" +
        //        nameof(MapTier) + ": " + MapTier + "\n" +
        //        nameof(NumberOfSockets) + ": " + NumberOfSockets + "\n" +
        //        nameof(LargestLinkSize) + ": " + LargestLinkSize + "\n" +
        //        nameof(BIdentified) + ": " + BIdentified + "\n" +
        //        nameof(isCorrupted) + ": " + isCorrupted + "\n" +
        //        nameof(isCorrupted) + ": " + isCorrupted + "\n" +
        //        nameof(isShaper) + ": " + isShaper + "\n" +
        //        nameof(isCrusader) + ": " + isCrusader + "\n" +
        //        nameof(isRedeemer) + ": " + isRedeemer + "\n" +
        //        nameof(isHunter) + ": " + isHunter + "\n" +
        //        nameof(isWarlord) + ": " + isWarlord + "\n" +
        //        nameof(isInfluenced) + ": " + isInfluenced + "\n" +
        //        nameof(Synthesised) + ": " + Synthesised + "\n" +
        //        nameof(isBlightMap) + ": " + isBlightMap + "\n" +
        //        nameof(isElderGuardianMap) + ": " + isElderGuardianMap + "\n" +
        //        nameof(Enchanted) + ": " + Enchanted + "\n" +
        //        nameof(SkillGemLevel) + ": " + SkillGemLevel + "\n" +
        //        nameof(SkillGemQualityType) + ": " + SkillGemQualityType + "\n" +
        //        nameof(MetamorphSampleRewardsAmount) + ": " + MetamorphSampleRewardsAmount + "\n" +
        //        nameof(MetamorphSampleGoodRewardsAmount) + ": " + MetamorphSampleGoodRewardsAmount + "\n" +
        //        nameof(MetamorphSampleBadRewardsAmount) + ": " + MetamorphSampleBadRewardsAmount + "\n";
        //    return itemdata;
        //
        //}
    }
}
