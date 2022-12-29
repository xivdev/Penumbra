using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Penumbra.GameData.Data;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Files;
using Penumbra.GameData.Structs;
using Penumbra.Meta.Files;
using Penumbra.Meta.Manipulations;
using Penumbra.String.Classes;

namespace Penumbra.Mods.ItemSwap;

public static class CustomizationSwap
{
    /// The .mdl file for customizations is unique per racecode, slot and id, thus the .mdl redirection itself is independent of the mode.
    public static bool CreateMdl( IReadOnlyDictionary< Utf8GamePath, FullPath > redirections, BodySlot slot, GenderRace race, SetId idFrom, SetId idTo, out FileSwap mdl )
    {
        if( idFrom.Value > byte.MaxValue )
        {
            mdl = new FileSwap();
            return false;
        }

        var mdlPathFrom = GamePaths.Character.Mdl.Path( race, slot, idFrom, slot.ToCustomizationType() );
        var mdlPathTo   = GamePaths.Character.Mdl.Path( race, slot, idTo, slot.ToCustomizationType() );

        if( !FileSwap.CreateSwap( ResourceType.Mdl, redirections, mdlPathFrom, mdlPathTo, out mdl ) )
        {
            return false;
        }

        var range = slot == BodySlot.Tail && race is GenderRace.HrothgarMale or GenderRace.HrothgarFemale or GenderRace.HrothgarMaleNpc or GenderRace.HrothgarMaleNpc ? 5 : 1;

        foreach( ref var materialFileName in mdl.AsMdl()!.Materials.AsSpan() )
        {
            var name = materialFileName;
            foreach( var variant in Enumerable.Range( 1, range ) )
            {
                name = materialFileName;
                if( !CreateMtrl( redirections, slot, race, idFrom, idTo, ( byte )variant, ref name, ref mdl.DataWasChanged, out var mtrl ) )
                {
                    return false;
                }

                mdl.ChildSwaps.Add( mtrl );
            }

            materialFileName = name;
        }

        return true;
    }

    public static string ReplaceAnyId( string path, char idType, SetId id, bool condition = true )
        => condition
            ? Regex.Replace( path, $"{idType}\\d{{4}}", $"{idType}{id.Value:D4}" )
            : path;

    public static string ReplaceAnyRace( string path, GenderRace to, bool condition = true )
        => ReplaceAnyId( path, 'c', ( ushort )to, condition );

    public static string ReplaceAnyBody( string path, BodySlot slot, SetId to, bool condition = true )
        => ReplaceAnyId( path, slot.ToAbbreviation(), to, condition );

    public static string ReplaceId( string path, char type, SetId idFrom, SetId idTo, bool condition = true )
        => condition
            ? path.Replace( $"{type}{idFrom.Value:D4}", $"{type}{idTo.Value:D4}" )
            : path;

    public static string ReplaceRace( string path, GenderRace from, GenderRace to, bool condition = true )
        => ReplaceId( path, 'c', ( ushort )from, ( ushort )to, condition );

    public static string ReplaceBody( string path, BodySlot slot, SetId idFrom, SetId idTo, bool condition = true )
        => ReplaceId( path, slot.ToAbbreviation(), idFrom, idTo, condition );

    public static string AddSuffix( string path, string ext, string suffix, bool condition = true )
        => condition
            ? path.Replace( ext, suffix + ext )
            : path;

    public static bool CreateMtrl( IReadOnlyDictionary< Utf8GamePath, FullPath > redirections, BodySlot slot, GenderRace race, SetId idFrom, SetId idTo, byte variant,
        ref string fileName, ref bool dataWasChanged, out FileSwap mtrl )
    {
        variant = slot is BodySlot.Face or BodySlot.Zear ? byte.MaxValue : variant;
        var mtrlFromPath = GamePaths.Character.Mtrl.Path( race, slot, idFrom, fileName, out var gameRaceFrom, out var gameSetIdFrom, variant );
        var mtrlToPath   = GamePaths.Character.Mtrl.Path( race, slot, idTo, fileName, out var gameRaceTo, out var gameSetIdTo, variant );

        var newFileName = fileName;
        newFileName = ReplaceRace( newFileName, gameRaceTo, race, gameRaceTo                                             != race );
        newFileName = ReplaceBody( newFileName, slot, idTo, idFrom, idFrom.Value                                         != idTo.Value );
        newFileName = AddSuffix( newFileName, ".mtrl", $"_c{race.ToRaceCode()}", gameRaceFrom                            != race );
        newFileName = AddSuffix( newFileName, ".mtrl", $"_{slot.ToAbbreviation()}{idFrom.Value:D4}", gameSetIdFrom.Value != idFrom.Value );

        var actualMtrlFromPath = mtrlFromPath;
        if( newFileName != fileName )
        {
            actualMtrlFromPath = GamePaths.Character.Mtrl.Path( race, slot, idFrom, newFileName, out _, out _, variant );
            fileName           = newFileName;
            dataWasChanged     = true;
        }

        if( !FileSwap.CreateSwap( ResourceType.Mtrl, redirections, actualMtrlFromPath, mtrlToPath, out mtrl, actualMtrlFromPath ) )
        {
            return false;
        }

        if( !CreateShader( redirections, ref mtrl.AsMtrl()!.ShaderPackage.Name, ref mtrl.DataWasChanged, out var shpk ) )
        {
            return false;
        }

        mtrl.ChildSwaps.Add( shpk );

        foreach( ref var texture in mtrl.AsMtrl()!.Textures.AsSpan() )
        {
            if( !CreateTex( redirections, slot, race, idFrom, ref texture, ref mtrl.DataWasChanged, out var tex ) )
            {
                return false;
            }

            mtrl.ChildSwaps.Add( tex );
        }

        return true;
    }

    public static bool CreateTex( IReadOnlyDictionary< Utf8GamePath, FullPath > redirections, BodySlot slot, GenderRace race, SetId idFrom, ref MtrlFile.Texture texture,
        ref bool dataWasChanged, out FileSwap tex )
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

        var newPath = ReplaceAnyRace( path, race );
        newPath = ReplaceAnyBody( newPath, slot, idFrom );
        newPath = AddSuffix( newPath, ".tex", $"_{Path.GetFileName(texture.Path).GetStableHashCode():x8}", true );
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

    /// <remarks> metaChanges is not manipulated, but IReadOnlySet does not support TryGetValue. </remarks>
    public static bool CreateEst( IReadOnlyDictionary< Utf8GamePath, FullPath > redirections, HashSet< MetaManipulation > metaChanges, BodySlot slot, GenderRace gr, SetId idFrom,
        SetId idTo, out MetaSwap? est )
    {
        var (gender, race) = gr.Split();
        var estSlot = slot switch
        {
            BodySlot.Hair => EstManipulation.EstType.Hair,
            BodySlot.Body => EstManipulation.EstType.Body,
            _             => ( EstManipulation.EstType )0,
        };
        if( estSlot == 0 )
        {
            est = null;
            return true;
        }

        var fromDefault = new EstManipulation( gender, race, estSlot, idFrom.Value, EstFile.GetDefault( estSlot, gr, idFrom.Value ) );
        var toDefault   = new EstManipulation( gender, race, estSlot, idTo.Value, EstFile.GetDefault( estSlot, gr, idTo.Value ) );
        est = new MetaSwap( metaChanges, fromDefault, toDefault );

        if( est.SwapApplied.Est.Entry >= 2 )
        {
            if( !CreatePhyb( redirections, slot, gr, est.SwapApplied.Est.Entry, out var phyb ) )
            {
                return false;
            }

            if( !CreateSklb( redirections, slot, gr, est.SwapApplied.Est.Entry, out var sklb ) )
            {
                return false;
            }

            est.ChildSwaps.Add( phyb );
            est.ChildSwaps.Add( sklb );
        }

        return true;
    }

    public static bool CreatePhyb( IReadOnlyDictionary< Utf8GamePath, FullPath > redirections, BodySlot slot, GenderRace race, ushort estEntry, out FileSwap phyb )
    {
        var phybPath = GamePaths.Character.Phyb.Path( race, slot, estEntry );
        return FileSwap.CreateSwap( ResourceType.Phyb, redirections, phybPath, phybPath, out phyb );
    }

    public static bool CreateSklb( IReadOnlyDictionary< Utf8GamePath, FullPath > redirections, BodySlot slot, GenderRace race, ushort estEntry, out FileSwap sklb )
    {
        var sklbPath = GamePaths.Character.Sklb.Path( race, slot, estEntry );
        return FileSwap.CreateSwap( ResourceType.Sklb, redirections, sklbPath, sklbPath, out sklb );
    }
}