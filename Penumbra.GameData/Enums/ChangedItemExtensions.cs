using Penumbra.Api.Enums;
using Penumbra.GameData.Structs;
using Action = Lumina.Excel.GeneratedSheets.Action;

namespace Penumbra.GameData.Enums;

public static class ChangedItemExtensions
{
    public static (ChangedItemType, uint) ChangedItemToTypeAndId(object? item)
    {
        return item switch
        {
            null        => (ChangedItemType.None, 0),
            EquipItem i => (ChangedItemType.Item, i.Id),
            Action a    => (ChangedItemType.Action, a.RowId),
            _           => (ChangedItemType.Customization, 0),
        };
    }
}
