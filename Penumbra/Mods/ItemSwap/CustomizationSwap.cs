using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Penumbra.GameData.Data;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Files;
using Penumbra.GameData.Structs;
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

    public static bool CreateMtrl( IReadOnlyDictionary< Utf8GamePath, FullPath > redirections, BodySlot slot, GenderRace race, SetId idFrom, SetId idTo, byte variant,
        ref string fileName, ref bool dataWasChanged, out FileSwap mtrl )
    {
        variant = slot is BodySlot.Face or BodySlot.Zear ? byte.MaxValue : variant;
        var mtrlFromPath = GamePaths.Character.Mtrl.Path( race, slot, idFrom, fileName, out var gameRaceFrom, out var gameSetIdFrom, variant );
        var mtrlToPath   = GamePaths.Character.Mtrl.Path( race, slot, idTo, fileName, out var gameRaceTo, out var gameSetIdTo, variant );

        var newFileName = fileName;
        newFileName = ItemSwap.ReplaceRace( newFileName, gameRaceTo, race, gameRaceTo     != race );
        newFileName = ItemSwap.ReplaceBody( newFileName, slot, idTo, idFrom, idFrom.Value != idTo.Value );
        newFileName = ItemSwap.AddSuffix( newFileName, ".mtrl", $"_c{race.ToRaceCode()}", gameRaceFrom != race || MaterialHandling.IsSpecialCase( race, idFrom ) );
        newFileName = ItemSwap.AddSuffix( newFileName, ".mtrl", $"_{slot.ToAbbreviation()}{idFrom.Value:D4}", gameSetIdFrom.Value != idFrom.Value );

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

        var newPath = ItemSwap.ReplaceAnyRace( path, race );
        newPath = ItemSwap.ReplaceAnyBody( newPath, slot, idFrom );
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
}