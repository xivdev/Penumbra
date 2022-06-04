using System.Collections.Generic;
using System.Linq;

namespace Penumbra.Mods;

public sealed partial class Mod
{
    public SortedList< string, object? > ChangedItems { get; } = new();
    public string LowerChangedItemsString { get; private set; } = string.Empty;

    private void ComputeChangedItems()
    {
        var identifier = GameData.GameData.GetIdentifier();
        ChangedItems.Clear();
        foreach( var gamePath in AllRedirects )
        {
            identifier.Identify( ChangedItems, gamePath.ToGamePath() );
        }

        // TODO: manipulations
        LowerChangedItemsString = string.Join( "\0", ChangedItems.Keys.Select( k => k.ToLowerInvariant() ) );
    }
}