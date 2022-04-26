using System.IO;
using Dalamud.Logging;

namespace Penumbra.Mods;

public enum ModPathChangeType
{
    Added,
    Deleted,
    Moved,
}

public partial class Mod
{
    public DirectoryInfo BasePath { get; private set; }
    public int Index { get; private set; } = -1;

    private Mod( DirectoryInfo basePath )
        => BasePath = basePath;

    public static Mod? LoadMod( DirectoryInfo basePath )
    {
        basePath.Refresh();
        if( !basePath.Exists )
        {
            PluginLog.Error( $"Supplied mod directory {basePath} does not exist." );
            return null;
        }

        var mod = new Mod( basePath );
        mod.LoadMeta();
        if( mod.Name.Length == 0 )
        {
            PluginLog.Error( $"Mod at {basePath} without name is not supported." );
        }

        mod.LoadDefaultOption();
        mod.LoadAllGroups();
        mod.ComputeChangedItems();
        mod.SetCounts();

        return mod;
    }
}