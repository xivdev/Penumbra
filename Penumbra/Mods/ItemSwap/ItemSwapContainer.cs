using Lumina.Excel.GeneratedSheets;
using Penumbra.Collections;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Structs;
using Penumbra.Meta.Manipulations;
using Penumbra.String.Classes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Penumbra.Mods.ItemSwap;

public class ItemSwapContainer
{
    private Dictionary< Utf8GamePath, FullPath > _modRedirections  = new();
    private HashSet< MetaManipulation >          _modManipulations = new();

    public IReadOnlyDictionary< Utf8GamePath, FullPath > ModRedirections
        => _modRedirections;

    public IReadOnlySet< MetaManipulation > ModManipulations
        => _modManipulations;

    public readonly List< Swap > Swaps = new();

    public bool Loaded { get; private set; }

    public void Clear()
    {
        Swaps.Clear();
        Loaded = false;
    }

    public enum WriteType
    {
        UseSwaps,
        NoSwaps,
    }

    public bool WriteMod( ModManager manager, Mod mod, WriteType writeType = WriteType.NoSwaps, DirectoryInfo? directory = null, int groupIndex = -1, int optionIndex = 0 )
    {
        var convertedManips = new HashSet< MetaManipulation >( Swaps.Count );
        var convertedFiles  = new Dictionary< Utf8GamePath, FullPath >( Swaps.Count );
        var convertedSwaps  = new Dictionary< Utf8GamePath, FullPath >( Swaps.Count );
        directory ??= mod.ModPath;
        try
        {
            foreach( var swap in Swaps.SelectMany( s => s.WithChildren() ) )
            {
                switch( swap )
                {
                    case FileSwap file:
                        // Skip, nothing to do
                        if( file.SwapToModdedEqualsOriginal )
                        {
                            continue;
                        }


                        if( writeType == WriteType.UseSwaps && file.SwapToModdedExistsInGame && !file.DataWasChanged )
                        {
                            convertedSwaps.TryAdd( file.SwapFromRequestPath, file.SwapToModded );
                        }
                        else
                        {
                            var path  = file.GetNewPath( directory.FullName );
                            var bytes = file.FileData.Write();
                            Directory.CreateDirectory( Path.GetDirectoryName( path )! );
                            File.WriteAllBytes( path, bytes );
                            convertedFiles.TryAdd( file.SwapFromRequestPath, new FullPath( path ) );
                        }

                        break;
                    case MetaSwap meta:
                        if( !meta.SwapAppliedIsDefault )
                        {
                            convertedManips.Add( meta.SwapApplied );
                        }

                        break;
                }
            }

            manager.OptionEditor.OptionSetFiles( mod, groupIndex, optionIndex, convertedFiles );
            manager.OptionEditor.OptionSetFileSwaps( mod, groupIndex, optionIndex, convertedSwaps );
            manager.OptionEditor.OptionSetManipulations( mod, groupIndex, optionIndex, convertedManips );
            return true;
        }
        catch( Exception e )
        {
            Penumbra.Log.Error( $"Could not write FileSwapContainer to {mod.ModPath}:\n{e}" );
            return false;
        }
    }

    public void LoadMod( Mod? mod, ModSettings? settings )
    {
        Clear();
        if( mod == null )
        {
            _modRedirections  = new Dictionary< Utf8GamePath, FullPath >();
            _modManipulations = new HashSet< MetaManipulation >();
        }
        else
        {
            ( _modRedirections, _modManipulations ) = ModSettings.GetResolveData( mod, settings );
        }
    }

    public ItemSwapContainer()
    {
        LoadMod( null, null );
    }

    private Func< Utf8GamePath, FullPath > PathResolver( ModCollection? collection )
        => collection != null
            ? p => collection.ResolvePath( p ) ?? new FullPath( p )
            : p => ModRedirections.TryGetValue( p, out var path ) ? path : new FullPath( p );

    private Func< MetaManipulation, MetaManipulation > MetaResolver( ModCollection? collection )
    {
        var set = collection?.MetaCache?.Manipulations.ToHashSet() ?? _modManipulations;
        return m => set.TryGetValue( m, out var a ) ? a : m;
    }

    public Item[] LoadEquipment( Item from, Item to, ModCollection? collection = null, bool useRightRing = true, bool useLeftRing = true )
    {
        Swaps.Clear();
        Loaded = false;
        var ret = EquipmentSwap.CreateItemSwap( Swaps, PathResolver( collection ), MetaResolver( collection ), from, to, useRightRing, useLeftRing );
        Loaded = true;
        return ret;
    }

    public Item[] LoadTypeSwap( EquipSlot slotFrom, Item from, EquipSlot slotTo, Item to, ModCollection? collection = null )
    {
        Swaps.Clear();
        Loaded = false;
        var ret = EquipmentSwap.CreateTypeSwap( Swaps, PathResolver( collection ), MetaResolver( collection ), slotFrom, from, slotTo, to );
        Loaded = true;
        return ret;
    }

    public bool LoadCustomization( BodySlot slot, GenderRace race, SetId from, SetId to, ModCollection? collection = null )
    {
        var pathResolver = PathResolver( collection );
        var mdl          = CustomizationSwap.CreateMdl( pathResolver, slot, race, from, to );
        var type = slot switch
        {
            BodySlot.Hair => EstManipulation.EstType.Hair,
            BodySlot.Face => EstManipulation.EstType.Face,
            _             => ( EstManipulation.EstType )0,
        };

        var metaResolver = MetaResolver( collection );
        var est          = ItemSwap.CreateEst( pathResolver, metaResolver, type, race, from, to, true );

        Swaps.Add( mdl );
        if( est != null )
        {
            Swaps.Add( est );
        }

        Loaded = true;
        return true;
    }
}