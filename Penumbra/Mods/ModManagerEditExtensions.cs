using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using Dalamud.Logging;
using Penumbra.Util;

namespace Penumbra.Mods;

// Extracted to keep the main file a bit more clean.
// Contains all change functions on a specific mod that also require corresponding changes to collections.
public static class ModManagerEditExtensions
{
    public static bool RenameMod( this Mod.Manager manager, string newName, Mod mod )
    {
        if( newName.Length == 0 || string.Equals( newName, mod.Meta.Name, StringComparison.InvariantCulture ) )
        {
            return false;
        }

        mod.Meta.Name = newName;
        mod.SaveMeta();

        return true;
    }

    public static bool ChangeSortOrder( this Mod.Manager manager, Mod mod, string newSortOrder )
    {
        if( string.Equals( mod.Order.FullPath, newSortOrder, StringComparison.InvariantCultureIgnoreCase ) )
        {
            return false;
        }

        var inRoot = new Mod.SortOrder( manager.StructuredMods, mod.Meta.Name );
        if( newSortOrder == string.Empty || newSortOrder == inRoot.SortOrderName )
        {
            mod.Order = inRoot;
            manager.TemporaryModSortOrder.Remove( mod.BasePath.Name );
        }
        else
        {
            mod.Move( newSortOrder );
            manager.TemporaryModSortOrder[ mod.BasePath.Name ] = mod.Order.FullPath;
        }

        manager.Config.Save();

        return true;
    }

    public static bool RenameModFolder( this Mod.Manager manager, Mod mod, DirectoryInfo newDir, bool move = true )
    {
        if( move )
        {
            newDir.Refresh();
            if( newDir.Exists )
            {
                return false;
            }

            var oldDir = new DirectoryInfo( mod.BasePath.FullName );
            try
            {
                oldDir.MoveTo( newDir.FullName );
            }
            catch( Exception e )
            {
                PluginLog.Error( $"Error while renaming directory {oldDir.FullName} to {newDir.FullName}:\n{e}" );
                return false;
            }
        }

        var oldBasePath = mod.BasePath;
        mod.BasePath = newDir;
        mod.MetaFile = Mod.MetaFileInfo( newDir );
        manager.UpdateMod( mod );

        if( manager.TemporaryModSortOrder.ContainsKey( oldBasePath.Name ) )
        {
            manager.TemporaryModSortOrder[ newDir.Name ] = manager.TemporaryModSortOrder[ oldBasePath.Name ];
            manager.TemporaryModSortOrder.Remove( oldBasePath.Name );
            manager.Config.Save();
        }

        var idx = manager.Mods.IndexOf( mod );
        foreach( var collection in Penumbra.CollectionManager )
        {
            if( collection.Settings[ idx ] != null )
            {
                collection.Save();
            }
        }

        return true;
    }

    public static bool ChangeModGroup( this Mod.Manager manager, string oldGroupName, string newGroupName, Mod mod,
        SelectType type = SelectType.Single )
    {
        if( newGroupName == oldGroupName || mod.Meta.Groups.ContainsKey( newGroupName ) )
        {
            return false;
        }

        if( mod.Meta.Groups.TryGetValue( oldGroupName, out var oldGroup ) )
        {
            if( newGroupName.Length > 0 )
            {
                mod.Meta.Groups[ newGroupName ] = new OptionGroup()
                {
                    GroupName     = newGroupName,
                    SelectionType = oldGroup.SelectionType,
                    Options       = oldGroup.Options,
                };
            }

            mod.Meta.Groups.Remove( oldGroupName );
        }
        else
        {
            if( newGroupName.Length == 0 )
            {
                return false;
            }

            mod.Meta.Groups[ newGroupName ] = new OptionGroup()
            {
                GroupName     = newGroupName,
                SelectionType = type,
                Options       = new List< Option >(),
            };
        }

        mod.SaveMeta();

        // TODO to indices
        var idx = Penumbra.ModManager.Mods.IndexOf( mod );

        foreach( var collection in Penumbra.CollectionManager )
        {
            var settings = collection.Settings[ idx ];
            if( settings == null )
            {
                continue;
            }

            if( newGroupName.Length > 0 )
            {
                settings.Settings[ newGroupName ] = settings.Settings.TryGetValue( oldGroupName, out var value ) ? value : 0;
            }

            settings.Settings.Remove( oldGroupName );
            collection.Save();
        }

        return true;
    }

    public static bool RemoveModOption( this Mod.Manager manager, int optionIdx, OptionGroup group, Mod mod )
    {
        if( optionIdx < 0 || optionIdx >= group.Options.Count )
        {
            return false;
        }

        group.Options.RemoveAt( optionIdx );
        mod.SaveMeta();

        static int MoveMultiSetting( int oldSetting, int idx )
        {
            var bitmaskFront = ( 1 << idx ) - 1;
            var bitmaskBack  = ~( bitmaskFront | ( 1 << idx ) );
            return ( oldSetting & bitmaskFront ) | ( ( oldSetting & bitmaskBack ) >> 1 );
        }

        var idx = Penumbra.ModManager.Mods.IndexOf( mod ); // TODO
        foreach( var collection in Penumbra.CollectionManager )
        {
            var settings = collection.Settings[ idx ];
            if( settings == null )
            {
                continue;
            }

            if( !settings.Settings.TryGetValue( group.GroupName, out var setting ) )
            {
                setting = 0;
            }

            var newSetting = group.SelectionType switch
            {
                SelectType.Single => setting >= optionIdx ? setting - 1 : setting,
                SelectType.Multi  => MoveMultiSetting( setting, optionIdx ),
                _                 => throw new InvalidEnumArgumentException(),
            };

            if( newSetting != setting )
            {
                settings.Settings[ group.GroupName ] = newSetting;
                collection.Save();
                if( collection.HasCache && settings.Enabled )
                {
                    collection.CalculateEffectiveFileList( mod.Resources.MetaManipulations.Count > 0,
                        Penumbra.CollectionManager.Default                                       == collection );
                }
            }
        }

        return true;
    }
}