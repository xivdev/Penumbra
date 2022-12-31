using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Lumina.Excel.GeneratedSheets;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Structs;
using Penumbra.Meta.Manipulations;
using Penumbra.String.Classes;

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

    public bool WriteMod( Mod mod, WriteType writeType = WriteType.NoSwaps )
    {
        var convertedManips = new HashSet< MetaManipulation >( Swaps.Count );
        var convertedFiles  = new Dictionary< Utf8GamePath, FullPath >( Swaps.Count );
        var convertedSwaps  = new Dictionary< Utf8GamePath, FullPath >( Swaps.Count );
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
                            var path  = file.GetNewPath( mod.ModPath.FullName );
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

            Penumbra.ModManager.OptionSetFiles( mod, -1, 0, convertedFiles );
            Penumbra.ModManager.OptionSetFileSwaps( mod, -1, 0, convertedSwaps );
            Penumbra.ModManager.OptionSetManipulations( mod, -1, 0, convertedManips );
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

    public Item[] LoadEquipment( Item from, Item to )
    {
        try
        {
            Swaps.Clear();
            var ret = EquipmentSwap.CreateItemSwap( Swaps, ModRedirections, _modManipulations, from, to );
            Loaded = true;
            return ret;
        }
        catch( Exception e )
        {
            Swaps.Clear();
            Loaded = false;
            return Array.Empty< Item >();
        }
    }

    public bool LoadCustomization( BodySlot slot, GenderRace race, SetId from, SetId to )
    {
        if( !CustomizationSwap.CreateMdl( ModRedirections, slot, race, from, to, out var mdl ) )
        {
            return false;
        }

        var type = slot switch
        {
            BodySlot.Hair => EstManipulation.EstType.Hair,
            BodySlot.Face => EstManipulation.EstType.Face,
            _             => ( EstManipulation.EstType )0,
        };
        if( !ItemSwap.CreateEst( ModRedirections, _modManipulations, type, race, from, to, out var est ) )
        {
            return false;
        }

        Swaps.Add( mdl );
        if( est != null )
        {
            Swaps.Add( est );
        }

        Loaded = true;
        return true;
    }
}