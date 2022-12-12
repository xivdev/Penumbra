using System;
using System.Collections.Generic;
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

namespace Penumbra.Mods.ItemSwap;

public class EquipmentDataContainer
{
    public Item      Item;
    public EquipSlot Slot;
    public SetId     ModelId;
    public byte      Variant;

    public ImcManipulation ImcData;

    public EqpManipulation EqpData;
    public GmpManipulation GmpData;

    // Example: Abyssos Helm / Body
    public string AvfxPath = string.Empty;

    // Example: Dodore Doublet, but unknown what it does?
    public string SoundPath = string.Empty;

    // Example: Crimson Standard Bracelet
    public string DecalPath = string.Empty;

    // Example: The Howling Spirit and The Wailing Spirit, but unknown what it does.
    public string AnimationPath = string.Empty;

    public Dictionary< GenderRace, GenderRaceContainer > Files = new();

    public struct GenderRaceContainer
    {
        public EqdpManipulation Eqdp;
        public GenderRace       ModelRace;
        public GenderRace       MaterialRace;
        public EstManipulation  Est;
        public string           MdlPath;
        public MtrlContainer[]  MtrlPaths;
    }

    public struct MtrlContainer
    {
        public string   MtrlPath;
        public string[] Textures;
        public string   Shader;

        public MtrlContainer( string mtrlPath )
        {
            MtrlPath = mtrlPath;
            var file = Dalamud.GameData.GetFile( mtrlPath );
            if( file != null )
            {
                var mtrl = new MtrlFile( file.Data );
                Textures = mtrl.Textures.Select( t => t.Path ).ToArray();
                Shader   = $"shader/sm5/shpk/{mtrl.ShaderPackage.Name}";
            }
            else
            {
                Textures = Array.Empty< string >();
                Shader   = string.Empty;
            }
        }
    }


    private static EstManipulation GetEstEntry( GenderRace genderRace, SetId setId, EquipSlot slot )
    {
        if( slot == EquipSlot.Head )
        {
            var entry = EstFile.GetDefault( EstManipulation.EstType.Head, genderRace, setId.Value );
            return new EstManipulation( genderRace.Split().Item1, genderRace.Split().Item2, EstManipulation.EstType.Head, setId.Value, entry );
        }

        if( slot == EquipSlot.Body )
        {
            var entry = EstFile.GetDefault( EstManipulation.EstType.Body, genderRace, setId.Value );
            return new EstManipulation( genderRace.Split().Item1, genderRace.Split().Item2, EstManipulation.EstType.Body, setId.Value, entry );
        }

        return default;
    }

    private static GenderRaceContainer GetGenderRace( GenderRace genderRace, SetId modelId, EquipSlot slot, ushort materialId )
    {
        var ret = new GenderRaceContainer()
        {
            Eqdp = GetEqdpEntry( genderRace, modelId, slot ),
            Est  = GetEstEntry( genderRace, modelId, slot ),
        };
        ( ret.ModelRace, ret.MaterialRace ) = TraverseEqdpTree( genderRace, modelId, slot );
        ret.MdlPath                         = GamePaths.Equipment.Mdl.Path( modelId, ret.ModelRace, slot );
        ret.MtrlPaths                       = MtrlPaths( ret.MdlPath, ret.MaterialRace, modelId, materialId );
        return ret;
    }

    private static EqdpManipulation GetEqdpEntry( GenderRace genderRace, SetId modelId, EquipSlot slot )
    {
        var entry = ExpandedEqdpFile.GetDefault( genderRace, slot.IsAccessory(), modelId.Value );
        return new EqdpManipulation( entry, slot, genderRace.Split().Item1, genderRace.Split().Item2, modelId.Value );
    }

    private static MtrlContainer[] MtrlPaths( string mdlPath, GenderRace mtrlRace, SetId modelId, ushort materialId )
    {
        var file = Dalamud.GameData.GetFile( mdlPath );
        if( file == null )
        {
            return Array.Empty< MtrlContainer >();
        }

        var mdl       = new MdlFile( Dalamud.GameData.GetFile( mdlPath )!.Data );
        var basePath  = GamePaths.Equipment.Mtrl.FolderPath( modelId, ( byte )materialId );
        var equipPart = $"e{modelId.Value:D4}";
        var racePart  = $"c{mtrlRace.ToRaceCode()}";

        return mdl.Materials
           .Where( m => m.Contains( equipPart ) )
           .Select( m => new MtrlContainer( $"{basePath}{m.Replace( "c0101", racePart )}" ) )
           .ToArray();
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


    public EquipmentDataContainer( Item i )
    {
        Item = i;
        LookupItem( i, out Slot, out ModelId, out Variant );
        LookupImc( ModelId, Variant, Slot );
        EqpData = new EqpManipulation( ExpandedEqpFile.GetDefault( ModelId.Value ), Slot, ModelId.Value );
        GmpData = Slot == EquipSlot.Head ? new GmpManipulation( ExpandedGmpFile.GetDefault( ModelId.Value ), ModelId.Value ) : default;


        foreach( var genderRace in Enum.GetValues< GenderRace >() )
        {
            if( CharacterUtility.EqdpIdx( genderRace, Slot.IsAccessory() ) < 0 )
            {
                continue;
            }

            Files[ genderRace ] = GetGenderRace( genderRace, ModelId, Slot, ImcData.Entry.MaterialId );
        }
    }


    private static void LookupItem( Item i, out EquipSlot slot, out SetId modelId, out byte variant )
    {
        slot = ( ( EquipSlot )i.EquipSlotCategory.Row ).ToSlot();
        if( !slot.IsEquipment() )
        {
            throw new ItemSwap.InvalidItemTypeException();
        }

        modelId = ( ( Quad )i.ModelMain ).A;
        variant = ( byte )( ( Quad )i.ModelMain ).B;
    }



    private void LookupImc( SetId modelId, byte variant, EquipSlot slot )
    {
        var imc = ImcFile.GetDefault( GamePaths.Equipment.Imc.Path( modelId ), slot, variant, out var exists );
        if( !exists )
        {
            throw new ItemSwap.InvalidImcException();
        }

        ImcData = new ImcManipulation( slot, variant, modelId.Value, imc );
        if( imc.DecalId != 0 )
        {
            DecalPath = GamePaths.Equipment.Decal.Path( imc.DecalId );
        }

        // TODO: Figure out how this works.
        if( imc.SoundId != 0 )
        {
            SoundPath = string.Empty;
        }

        if( imc.VfxId != 0 )
        {
            AvfxPath = GamePaths.Equipment.Avfx.Path( modelId, imc.VfxId );
        }

        // TODO: Figure out how this works.
        if( imc.MaterialAnimationId != 0 )
        {
            AnimationPath = string.Empty;
        }
    }
}