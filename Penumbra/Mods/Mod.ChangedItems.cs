using System.Collections.Generic;
using System.Linq;

namespace Penumbra.Mods;

public sealed partial class Mod
{
    public SortedList< string, object? > ChangedItems { get; } = new();
    public string LowerChangedItemsString { get; private set; } = string.Empty;

    private void ComputeChangedItems()
    {
        ChangedItems.Clear();
        foreach( var gamePath in AllRedirects )
        {
            Penumbra.Identifier.Identify( ChangedItems, gamePath.ToString() );
        }

        // TODO: manipulations
        LowerChangedItemsString = string.Join( "\0", ChangedItems.Keys.Select( k => k.ToLowerInvariant() ) );
    }
}