using System.Collections.Generic;
using System.Linq;

namespace Penumbra.Mods;

public sealed partial class Mod2
{
    public SortedList<string, object?> ChangedItems { get; } = new();
    public string LowerChangedItemsString { get; private set; } = string.Empty;

    public void ComputeChangedItems()
    {
        var identifier = GameData.GameData.GetIdentifier();
        ChangedItems.Clear();
        foreach( var (file, _) in AllFiles )
        {
            identifier.Identify( ChangedItems, file.ToGamePath() );
        }

        // TODO: manipulations
        LowerChangedItemsString = string.Join( "\0", ChangedItems.Keys.Select( k => k.ToLowerInvariant() ) );
    }
}