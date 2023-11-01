using ExileCore.PoEMemory;
using ExileCore.PoEMemory.Elements;
using ExileCore.PoEMemory.MemoryObjects;
using ItemFilterLibrary;

namespace Stashie;

public class StashieItemData : ItemData
{
    public StashieItemData(Entity queriedItem, FilesContainer fs) : base(queriedItem, fs)
    {
    }
}