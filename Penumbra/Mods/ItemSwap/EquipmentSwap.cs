using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Lumina.Data.Parsing;
using Lumina.Excel.GeneratedSheets;
using Penumbra.GameData.Data;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Files;
using Penumbra.GameData.Structs;
using Penumbra.Interop.Structs;
using Penumbra.Meta.Files;
using Penumbra.Meta.Manipulations;
using Penumbra.String.Classes;

namespace Penumbra.Mods.ItemSwap;

public static class EquipmentSwap
{
    public static Item[] CreateItemSwap( List< Swap > swaps, IReadOnlyDictionary< Utf8GamePath, FullPath > redirections, HashSet< MetaManipulation > manips, Item itemFrom,
        Item itemTo )
    {
        // Check actual ids, variants and slots. We only support using the same slot.
        LookupItem( itemFrom, out var slotFrom, out var idFrom, out var variantFrom );
        LookupItem( itemTo, out var slotTo, out var idTo, out var variantTo );
        if( slotFrom != slotTo )
        {
            throw new ItemSwap.InvalidItemTypeException();
        }

        if( !CreateEqp( manips, slotFrom, idFrom, idTo, out var eqp ) )
        {
            throw new Exception( "Could not get Eqp Entry for Swap." );
        }

        if( eqp != null )
        {
            swaps.Add( eqp );
        }

        if( !CreateGmp( manips, slotFrom, idFrom, idTo, out var gmp ) )
        {
            throw new Exception( "Could not get Gmp Entry for Swap." );
        }

        if( gmp != null )
        {
            swaps.Add( gmp );
        }


        var (imcFileFrom, variants, affectedItems) = GetVariants( slotFrom, idFrom, idTo, variantFrom );
        var imcFileTo = new ImcFile( new ImcManipulation( slotFrom, variantTo, idTo.Value, default ) );

        var isAccessory = slotFrom.IsAccessory();
        var estType = slotFrom switch
        {
            EquipSlot.Head => EstManipulation.EstType.Head,
            EquipSlot.Body => EstManipulation.EstType.Body,
            _              => ( EstManipulation.EstType )0,
        };

        var mtrlVariantTo = imcFileTo.GetEntry( ImcFile.PartIndex( slotFrom ), variantTo ).MaterialId;
        foreach( var gr in Enum.GetValues< GenderRace >() )
        {
            if( CharacterUtility.EqdpIdx( gr, isAccessory ) < 0 )
            {
                continue;
            }

            if( !ItemSwap.CreateEst( redirections, manips, estType, gr, idFrom, idTo, out var est ) )
            {
                throw new Exception( "Could not get Est Entry for Swap." );
            }

            if( est != null )
            {
                swaps.Add( est );
            }

            if( !CreateEqdp( redirections, manips, slotFrom, gr, idFrom, idTo, mtrlVariantTo, out var eqdp ) )
            {
                throw new Exception( "Could not get Eqdp Entry for Swap." );
            }

            if( eqdp != null )
            {
                swaps.Add( eqdp );
            }
        }

        foreach( var variant in variants )
        {
            if( !CreateImc( redirections, manips, slotFrom, idFrom, idTo, variant, variantTo, imcFileFrom, imcFileTo, out var imc ) )
            {
                throw new Exception( "Could not get IMC Entry for Swap." );
            }

            swaps.Add( imc );
        }


        return affectedItems;
    }

    public static bool CreateEqdp( IReadOnlyDictionary< Utf8GamePath, FullPath > redirections, HashSet< MetaManipulation > manips, EquipSlot slot, GenderRace gr, SetId idFrom,
        SetId idTo, byte mtrlTo, out MetaSwap? meta )
    {
        var (gender, race) = gr.Split();
        var eqdpFrom = new EqdpManipulation( ExpandedEqdpFile.GetDefault( gr, slot.IsAccessory(), idFrom.Value ), slot, gender, race, idFrom.Value );
        var eqdpTo   = new EqdpManipulation( ExpandedEqdpFile.GetDefault( gr, slot.IsAccessory(), idTo.Value ), slot, gender, race, idTo.Value );
        meta                  = new MetaSwap( manips, eqdpFrom, eqdpTo );
        var (ownMtrl, ownMdl) = meta.SwapApplied.Eqdp.Entry.ToBits( slot );
        if( ownMdl )
        {
            if( !CreateMdl( redirections, slot, gr, idFrom, idTo, mtrlTo, out var mdl ) )
            {
                return false;
            }

            meta.ChildSwaps.Add( mdl );
        }
        else if( !ownMtrl && meta.SwapAppliedIsDefault )
        {
            meta = null;
        }

        return true;
    }

    public static bool CreateMdl( IReadOnlyDictionary< Utf8GamePath, FullPath > redirections, EquipSlot slot, GenderRace gr, SetId idFrom, SetId idTo, byte mtrlTo,
        out FileSwap mdl )
    {
        var mdlPathFrom = GamePaths.Equipment.Mdl.Path( idFrom, gr, slot );
        var mdlPathTo   = GamePaths.Equipment.Mdl.Path( idTo, gr, slot );
        if( !FileSwap.CreateSwap( ResourceType.Mdl, redirections, mdlPathFrom, mdlPathTo, out mdl ) )
        {
            return false;
        }

        foreach( ref var fileName in mdl.AsMdl()!.Materials.AsSpan() )
        {
            if( !CreateMtrl( redirections, slot, idFrom, idTo, mtrlTo, ref fileName, ref mdl.DataWasChanged, out var mtrl ) )
            {
                return false;
            }

            if( mtrl != null )
            {
                mdl.ChildSwaps.Add( mtrl );
            }
        }

        return true;
    }

    private static (GenderRace, GenderRace) TraverseEqdpTree( GenderRace genderRace, SetId modelId, EquipSlot slot )
    {
        var model     = GenderRace.Unknown;
        var material  = GenderRace.Unknown;
        var accessory = slot.IsAccessory();
        foreach( var gr in genderRace.Dependencies() )
        {
            var entry = ExpandedEqdpFile.GetDefault( gr, accessory, modelId.Value );
            var (b1, b2) = entry.ToBits( slot );
            if( b1 && material == GenderRace.Unknown )
            {
                material = gr;
                if( model != GenderRace.Unknown )
                {
                    return ( model, material );
                }
            }

            if( b2 && model == GenderRace.Unknown )
            {
                model = gr;
                if( material != GenderRace.Unknown )
                {
                    return ( model, material );
                }
            }
        }

        return ( GenderRace.MidlanderMale, GenderRace.MidlanderMale );
    }

    private static void LookupItem( Item i, out EquipSlot slot, out SetId modelId, out byte variant )
    {
        slot = ( ( EquipSlot )i.EquipSlotCategory.Row ).ToSlot();
        if( !slot.IsEquipmentPiece() )
        {
            throw new ItemSwap.InvalidItemTypeException();
        }

        modelId = ( ( Quad )i.ModelMain ).A;
        variant = ( byte )( ( Quad )i.ModelMain ).B;
    }

    private static (ImcFile, byte[], Item[]) GetVariants( EquipSlot slot, SetId idFrom, SetId idTo, byte variantFrom )
    {
        var    entry = new ImcManipulation( slot, variantFrom, idFrom.Value, default );
        var    imc   = new ImcFile( entry );
        Item[] items;
        byte[] variants;
        if( idFrom.Value == idTo.Value )
        {
            items    = Penumbra.Identifier.Identify( idFrom, variantFrom, slot ).ToArray();
            variants = new[] { variantFrom };
        }
        else
        {
            items = Penumbra.Identifier.Identify( slot.IsEquipment()
                ? GamePaths.Equipment.Mdl.Path( idFrom, GenderRace.MidlanderMale, slot )
                : GamePaths.Accessory.Mdl.Path( idFrom, GenderRace.MidlanderMale, slot ) ).Select( kvp => kvp.Value ).OfType< Item >().ToArray();
            variants = Enumerable.Range( 0, imc.Count + 1 ).Select( i => ( byte )i ).ToArray();
        }

        return ( imc, variants, items );
    }

    public static bool CreateGmp( HashSet< MetaManipulation > manips, EquipSlot slot, SetId idFrom, SetId idTo, out MetaSwap? gmp )
    {
        if( slot is not EquipSlot.Head )
        {
            gmp = null;
            return true;
        }

        var manipFrom = new GmpManipulation( ExpandedGmpFile.GetDefault( idFrom.Value ), idFrom.Value );
        var manipTo   = new GmpManipulation( ExpandedGmpFile.GetDefault( idTo.Value ), idTo.Value );
        gmp = new MetaSwap( manips, manipFrom, manipTo );
        return true;
    }

    public static bool CreateImc( IReadOnlyDictionary< Utf8GamePath, FullPath > redirections, HashSet< MetaManipulation > manips, EquipSlot slot, SetId idFrom, SetId idTo,
        byte variantFrom, byte variantTo, ImcFile imcFileFrom, ImcFile imcFileTo, out MetaSwap imc )
    {
        var entryFrom        = imcFileFrom.GetEntry( ImcFile.PartIndex( slot ), variantFrom );
        var entryTo          = imcFileTo.GetEntry( ImcFile.PartIndex( slot ), variantTo );
        var manipulationFrom = new ImcManipulation( slot, variantFrom, idFrom.Value, entryFrom );
        var manipulationTo   = new ImcManipulation( slot, variantTo, idTo.Value, entryTo );
        imc = new MetaSwap( manips, manipulationFrom, manipulationTo );

        if( !AddDecal( redirections, imc.SwapToModded.Imc.Entry.DecalId, imc ) )
        {
            return false;
        }

        if( !AddAvfx( redirections, idFrom, idTo, imc.SwapToModded.Imc.Entry.VfxId, imc ) )
        {
            return false;
        }

        // IMC also controls sound, Example: Dodore Doublet, but unknown what it does?
        // IMC also controls some material animation, Example: The Howling Spirit and The Wailing Spirit, but unknown what it does.

        return true;
    }
    
    // Example: Crimson Standard Bracelet
    public static bool AddDecal( IReadOnlyDictionary< Utf8GamePath, FullPath > redirections, byte decalId, MetaSwap imc )
    {
        if( decalId != 0 )
        {
            var decalPath = GamePaths.Equipment.Decal.Path( decalId );
            if( !FileSwap.CreateSwap( ResourceType.Tex, redirections, decalPath, decalPath, out var swap ) )
            {
                return false;
            }

            imc.ChildSwaps.Add( swap );
        }

        return true;
    }

    
    // Example: Abyssos Helm / Body
    public static bool AddAvfx( IReadOnlyDictionary< Utf8GamePath, FullPath > redirections, SetId idFrom, SetId idTo, byte vfxId, MetaSwap imc )
    {
        if( vfxId != 0 )
        {
            var vfxPathFrom = GamePaths.Equipment.Avfx.Path( idFrom.Value, vfxId );
            var vfxPathTo   = GamePaths.Equipment.Avfx.Path( idTo.Value, vfxId );
            if( !FileSwap.CreateSwap( ResourceType.Avfx, redirections, vfxPathFrom, vfxPathTo, out var swap ) )
            {
                return false;
            }

            foreach( ref var filePath in swap.AsAvfx()!.Textures.AsSpan() )
            {
                if( !CreateAtex( redirections, ref filePath, ref swap.DataWasChanged, out var atex ) )
                {
                    return false;
                }

                swap.ChildSwaps.Add( atex );
            }

            imc.ChildSwaps.Add( swap );
        }

        return true;
    }

    public static bool CreateEqp( HashSet< MetaManipulation > manips, EquipSlot slot, SetId idFrom, SetId idTo, out MetaSwap? eqp )
    {
        if( slot.IsAccessory() )
        {
            eqp = null;
            return true;
        }

        var eqpValueFrom = ExpandedEqpFile.GetDefault( idFrom.Value );
        var eqpValueTo   = ExpandedEqpFile.GetDefault( idTo.Value );
        var eqpFrom      = new EqpManipulation( eqpValueFrom, slot, idFrom.Value );
        var eqpTo        = new EqpManipulation( eqpValueTo, slot, idFrom.Value );
        eqp = new MetaSwap( manips, eqpFrom, eqpTo );
        return true;
    }

    public static bool CreateMtrl( IReadOnlyDictionary< Utf8GamePath, FullPath > redirections, EquipSlot slot, SetId idFrom, SetId idTo, byte variantTo, ref string fileName,
        ref bool dataWasChanged, out FileSwap? mtrl )
    {
        var prefix = slot.IsAccessory() ? 'a' : 'e';
        if( !fileName.Contains( $"{prefix}{idTo.Value:D4}" ) )
        {
            mtrl = null;
            return true;
        }

        var folderTo = slot.IsAccessory() ? GamePaths.Accessory.Mtrl.FolderPath( idTo, variantTo ) : GamePaths.Equipment.Mtrl.FolderPath( idTo, variantTo );
        var pathTo   = $"{folderTo}{fileName}";

        var folderFrom  = slot.IsAccessory() ? GamePaths.Accessory.Mtrl.FolderPath( idFrom, variantTo ) : GamePaths.Equipment.Mtrl.FolderPath( idFrom, variantTo );
        var newFileName = ItemSwap.ReplaceId( fileName, prefix, idTo, idFrom );
        var pathFrom    = $"{folderFrom}{newFileName}";

        if( newFileName != fileName )
        {
            fileName       = newFileName;
            dataWasChanged = true;
        }

        if( !FileSwap.CreateSwap( ResourceType.Mtrl, redirections, pathFrom, pathTo, out mtrl ) )
        {
            return false;
        }

        if( !CreateShader( redirections, ref mtrl.AsMtrl()!.ShaderPackage.Name, ref mtrl.DataWasChanged, out var shader ) )
        {
            return false;
        }

        mtrl.ChildSwaps.Add( shader );

        foreach( ref var texture in mtrl.AsMtrl()!.Textures.AsSpan() )
        {
            if( !CreateTex( redirections, prefix, idFrom, idTo, ref texture, ref mtrl.DataWasChanged, out var swap ) )
            {
                return false;
            }

            mtrl.ChildSwaps.Add( swap );
        }

        return true;
    }

    public static bool CreateTex( IReadOnlyDictionary< Utf8GamePath, FullPath > redirections, char prefix, SetId idFrom, SetId idTo, ref MtrlFile.Texture texture,
        ref bool dataWasChanged,
        out FileSwap tex )
    {
        var path        = texture.Path;
        var addedDashes = false;
        if( texture.DX11 )
        {
            var fileName = Path.GetFileName( path );
            if( !fileName.StartsWith( "--" ) )
            {
                path        = path.Replace( fileName, $"--{fileName}" );
                addedDashes = true;
            }
        }

        var newPath = ItemSwap.ReplaceAnyId( path, prefix, idFrom );
        newPath = ItemSwap.AddSuffix( newPath, ".tex", $"_{Path.GetFileName( texture.Path ).GetStableHashCode():x8}", true );
        if( newPath != path )
        {
            texture.Path   = addedDashes ? newPath.Replace( "--", string.Empty ) : newPath;
            dataWasChanged = true;
        }

        return FileSwap.CreateSwap( ResourceType.Tex, redirections, newPath, path, out tex, path );
    }

    public static bool CreateShader( IReadOnlyDictionary< Utf8GamePath, FullPath > redirections, ref string shaderName, ref bool dataWasChanged, out FileSwap shpk )
    {
        var path = $"shader/sm5/shpk/{shaderName}";
        return FileSwap.CreateSwap( ResourceType.Shpk, redirections, path, path, out shpk );
    }

    public static bool CreateAtex( IReadOnlyDictionary< Utf8GamePath, FullPath > redirections, ref string filePath, ref bool dataWasChanged, out FileSwap atex )
    {
        var oldPath = filePath;
        filePath       = ItemSwap.AddSuffix( filePath, ".atex", $"_{Path.GetFileName( filePath ).GetStableHashCode():x8}", true );
        dataWasChanged = true;

        return FileSwap.CreateSwap( ResourceType.Atex, redirections, filePath, oldPath, out atex, oldPath );
    }
}