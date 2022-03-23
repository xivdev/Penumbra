using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using Dalamud.Logging;
using Penumbra.Mod;

namespace Penumbra.Mods;

// Extracted to keep the main file a bit more clean.
// Contains all change functions on a specific mod that also require corresponding changes to collections.
public static class ModManagerEditExtensions
{
    public static bool RenameMod( this ModManager manager, string newName, ModData mod )
    {
        if( newName.Length == 0 || string.Equals( newName, mod.Meta.Name, StringComparison.InvariantCulture ) )
        {
            return false;
        }

        mod.Meta.Name = newName;
        mod.SaveMeta();

        return true;
    }

    public static bool ChangeSortOrder( this ModManager manager, ModData mod, string newSortOrder )
    {
        if( string.Equals( mod.SortOrder.FullPath, newSortOrder, StringComparison.InvariantCultureIgnoreCase ) )
        {
            return false;
        }

        var inRoot = new SortOrder( manager.StructuredMods, mod.Meta.Name );
        if( newSortOrder == string.Empty || newSortOrder == inRoot.SortOrderName )
        {
            mod.SortOrder = inRoot;
            manager.Config.ModSortOrder.Remove( mod.BasePath.Name );
        }
        else
        {
            mod.Move( newSortOrder );
            manager.Config.ModSortOrder[ mod.BasePath.Name ] = mod.SortOrder.FullPath;
        }

        manager.Config.Save();

        return true;
    }

    public static bool RenameModFolder( this ModManager manager, ModData mod, DirectoryInfo newDir, bool move = true )
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
        mod.MetaFile = ModData.MetaFileInfo( newDir );
        manager.UpdateMod( mod );

        if( manager.Config.ModSortOrder.ContainsKey( oldBasePath.Name ) )
        {
            manager.Config.ModSortOrder[ newDir.Name ] = manager.Config.ModSortOrder[ oldBasePath.Name ];
            manager.Config.ModSortOrder.Remove( oldBasePath.Name );
            manager.Config.Save();
        }

        foreach( var collection in Penumbra.CollectionManager.Collections )
        {
            if( collection.Settings.TryGetValue( oldBasePath.Name, out var settings ) )
            {
                collection.Settings[ newDir.Name ] = settings;
                collection.Settings.Remove( oldBasePath.Name );
                collection.Save();
            }

            if( collection.Cache != null )
            {
                collection.Cache.RemoveMod( newDir );
                collection.AddMod( mod );
            }
        }

        return true;
    }

    public static bool ChangeModGroup( this ModManager manager, string oldGroupName, string newGroupName, ModData mod,
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

        foreach( var collection in Penumbra.CollectionManager.Collections )
        {
            if( !collection.Settings.TryGetValue( mod.BasePath.Name, out var settings ) )
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

    public static bool RemoveModOption( this ModManager manager, int optionIdx, OptionGroup group, ModData mod )
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

        foreach( var collection in Penumbra.CollectionManager.Collections )
        {
            if( !collection.Settings.TryGetValue( mod.BasePath.Name, out var settings ) )
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
                if( collection.Cache != null && settings.Enabled )
                {
                    collection.CalculateEffectiveFileList( mod.Resources.MetaManipulations.Count > 0,
                        Penumbra.CollectionManager.IsActive( collection ) );
                }
            }
        }

        return true;
    }
}