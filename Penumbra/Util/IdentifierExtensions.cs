using OtterGui.Classes;
using Penumbra.GameData.Data;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Structs;
using Penumbra.Mods.Editor;
using Penumbra.Mods.SubMods;

namespace Penumbra.Util;

public static class IdentifierExtensions
{
    public static void AddChangedItems(this ObjectIdentification identifier, IModDataContainer container,
        IDictionary<string, object?> changedItems)
    {
        foreach (var gamePath in container.Files.Keys.Concat(container.FileSwaps.Keys))
            identifier.Identify(changedItems, gamePath.ToString());

        foreach (var manip in container.Manipulations.Identifiers)
            manip.AddChangedItems(identifier, changedItems);
    }

    public static void RemoveMachinistOffhands(this SortedList<string, object?> changedItems)
    {
        for (var i = 0; i < changedItems.Count; i++)
        {
            {
                var value = changedItems.Values[i];
                if (value is EquipItem { Type: FullEquipType.GunOff })
                    changedItems.RemoveAt(i--);
            }
        }
    }

    public static void RemoveMachinistOffhands(this SortedList<string, (SingleArray<IMod>, object?)> changedItems)
    {
        for (var i = 0; i < changedItems.Count; i++)
        {
            {
                var value = changedItems.Values[i].Item2;
                if (value is EquipItem { Type: FullEquipType.GunOff })
                    changedItems.RemoveAt(i--);
            }
        }
    }
}
