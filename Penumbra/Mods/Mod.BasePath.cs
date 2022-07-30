using System.IO;
using Dalamud.Logging;

namespace Penumbra.Mods;

public enum ModPathChangeType
{
    Added,
    Deleted,
    Moved,
    Reloaded,
    StartingReload,
}

public partial class Mod
{
    public DirectoryInfo ModPath { get; private set; }
    public int Index { get; private set; } = -1;

    // Unused if Index < 0 but used for special temporary mods.
    public int Priority
        => 0;

    private Mod( DirectoryInfo modPath )
    {
        ModPath  = modPath;
        _default = new SubMod( this );
    }

    private static Mod? LoadMod( DirectoryInfo modPath )
    {
        modPath.Refresh();
        if( !modPath.Exists )
        {
            PluginLog.Error( $"Supplied mod directory {modPath} does not exist." );
            return null;
        }

        var mod = new Mod( modPath );
        if( !mod.Reload( out _ ) )
        {
            // Can not be base path not existing because that is checked before.
            PluginLog.Error( $"Mod at {modPath} without name is not supported." );
            return null;
        }

        return mod;
    }

    private bool Reload( out MetaChangeType metaChange )
    {
        metaChange = MetaChangeType.Deletion;
        ModPath.Refresh();
        if( !ModPath.Exists )
        {
            return false;
        }

        metaChange = LoadMeta();
        if( metaChange.HasFlag( MetaChangeType.Deletion ) || Name.Length == 0 )
        {
            return false;
        }

        LoadDefaultOption();
        LoadAllGroups();
        ComputeChangedItems();
        SetCounts();
        return true;
    }
}