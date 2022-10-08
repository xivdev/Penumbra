using System;
using Lumina.Excel.GeneratedSheets;
using Penumbra.Api.Enums;
using Action = Lumina.Excel.GeneratedSheets.Action;

namespace Penumbra.GameData.Enums;

public static class ChangedItemExtensions
{
    public static (ChangedItemType, uint) ChangedItemToTypeAndId( object? item )
    {
        return item switch
        {
            null     => ( ChangedItemType.None, 0 ),
            Item i   => ( ChangedItemType.Item, i.RowId ),
            Action a => ( ChangedItemType.Action, a.RowId ),
            _        => ( ChangedItemType.Customization, 0 ),
        };
    }

    public static object? GetObject( this ChangedItemType type, uint id )
    {
        return type switch
        {
            ChangedItemType.None          => null,
            ChangedItemType.Item          => ObjectIdentification.DataManager?.GetExcelSheet< Item >()?.GetRow( id ),
            ChangedItemType.Action        => ObjectIdentification.DataManager?.GetExcelSheet< Action >()?.GetRow( id ),
            ChangedItemType.Customization => null,
            _                             => throw new ArgumentOutOfRangeException( nameof( type ), type, null ),
        };
    }
}