using System.Collections.Generic;
using System.IO;
using System.Linq;
using Penumbra.Mods.Manager;

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
    public DirectoryInfo ModPath { get; internal set; }
    public string Identifier
        => Index >= 0 ? ModPath.Name : Name;
    public int Index { get; internal set; } = -1;

    public bool IsTemporary
        => Index < 0;

    // Unused if Index < 0 but used for special temporary mods.
    public int Priority
        => 0;

    internal Mod( DirectoryInfo modPath )
    {
        ModPath  = modPath;
        _default = new SubMod( this );
    }

    public static Mod? LoadMod( ModManager modManager, DirectoryInfo modPath, bool incorporateMetaChanges )
    {
        modPath.Refresh();
        if( !modPath.Exists )
        {
            Penumbra.Log.Error( $"Supplied mod directory {modPath} does not exist." );
            return null;
        }

        var mod = new Mod(modPath);
        if (mod.Reload(modManager, incorporateMetaChanges, out _))
            return mod;

        // Can not be base path not existing because that is checked before.
        Penumbra.Log.Warning( $"Mod at {modPath} without name is not supported." );
        return null;

    }

    internal bool Reload(ModManager modManager, bool incorporateMetaChanges, out ModDataChangeType modDataChange )
    {
        modDataChange = ModDataChangeType.Deletion;
        ModPath.Refresh();
        if( !ModPath.Exists )
        {
            return false;
        }

        modDataChange = modManager.DataEditor.LoadMeta(this);
        if( modDataChange.HasFlag( ModDataChangeType.Deletion ) || Name.Length == 0 )
        {
            return false;
        }

        modManager.DataEditor.LoadLocalData(this);

        LoadDefaultOption();
        LoadAllGroups();
        if( incorporateMetaChanges )
        {
            IncorporateAllMetaChanges(true);
        }

        return true;
    }

    // Convert all .meta and .rgsp files to their respective meta changes and add them to their options.
    // Deletes the source files if delete is true.
    private void IncorporateAllMetaChanges( bool delete )
    {
        var            changes    = false;
        List< string > deleteList = new();
        foreach( var subMod in AllSubMods.OfType< SubMod >() )
        {
            var (localChanges, localDeleteList) =  subMod.IncorporateMetaChanges( ModPath, false );
            changes                             |= localChanges;
            if( delete )
            {
                deleteList.AddRange( localDeleteList );
            }
        }

        SubMod.DeleteDeleteList( deleteList, delete );

        if( changes )
        {
            Penumbra.SaveService.SaveAllOptionGroups(this);
        }
    }
}