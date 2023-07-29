using System;
using System.IO;
using System.Linq;
using Penumbra.GameData.Data;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Files;
using Penumbra.GameData.Structs;
using Penumbra.Meta;
using Penumbra.String.Classes;

namespace Penumbra.Mods.ItemSwap;

public static class CustomizationSwap
{
    /// The .mdl file for customizations is unique per racecode, slot and id, thus the .mdl redirection itself is independent of the mode.
    public static FileSwap CreateMdl( MetaFileManager manager, Func< Utf8GamePath, FullPath > redirections, BodySlot slot, GenderRace race, SetId idFrom, SetId idTo )
    {
        if( idFrom.Id > byte.MaxValue )
        {
            throw new Exception( $"The Customization ID {idFrom} is too large for {slot}." );
        }

        var mdlPathFrom = GamePaths.Character.Mdl.Path( race, slot, idFrom, slot.ToCustomizationType() );
        var mdlPathTo   = GamePaths.Character.Mdl.Path( race, slot, idTo, slot.ToCustomizationType() );

        var mdl   = FileSwap.CreateSwap( manager, ResourceType.Mdl, redirections, mdlPathFrom, mdlPathTo );
        var range = slot == BodySlot.Tail && race is GenderRace.HrothgarMale or GenderRace.HrothgarFemale or GenderRace.HrothgarMaleNpc or GenderRace.HrothgarMaleNpc ? 5 : 1;

        foreach( ref var materialFileName in mdl.AsMdl()!.Materials.AsSpan() )
        {
            var name = materialFileName;
            foreach( var variant in Enumerable.Range( 1, range ) )
            {
                name = materialFileName;
                var mtrl = CreateMtrl( manager, redirections, slot, race, idFrom, idTo, ( byte )variant, ref name, ref mdl.DataWasChanged );
                mdl.ChildSwaps.Add( mtrl );
            }

            materialFileName = name;
        }

        return mdl;
    }

    public static FileSwap CreateMtrl( MetaFileManager manager, Func< Utf8GamePath, FullPath > redirections, BodySlot slot, GenderRace race, SetId idFrom, SetId idTo, byte variant,
        ref string fileName, ref bool dataWasChanged )
    {
        variant = slot is BodySlot.Face or BodySlot.Zear ? byte.MaxValue : variant;
        var mtrlFromPath = GamePaths.Character.Mtrl.Path( race, slot, idFrom, fileName, out var gameRaceFrom, out var gameSetIdFrom, variant );
        var mtrlToPath   = GamePaths.Character.Mtrl.Path( race, slot, idTo, fileName, out var gameRaceTo, out var gameSetIdTo, variant );

        var newFileName = fileName;
        newFileName = ItemSwap.ReplaceRace( newFileName, gameRaceTo, race, gameRaceTo     != race );
        newFileName = ItemSwap.ReplaceBody( newFileName, slot, idTo, idFrom, idFrom != idTo );
        newFileName = ItemSwap.AddSuffix( newFileName, ".mtrl", $"_c{race.ToRaceCode()}", gameRaceFrom != race || MaterialHandling.IsSpecialCase( race, idFrom ) );
        newFileName = ItemSwap.AddSuffix( newFileName, ".mtrl", $"_{slot.ToAbbreviation()}{idFrom.Id:D4}", gameSetIdFrom != idFrom );

        var actualMtrlFromPath = mtrlFromPath;
        if( newFileName != fileName )
        {
            actualMtrlFromPath = GamePaths.Character.Mtrl.Path( race, slot, idFrom, newFileName, out _, out _, variant );
            fileName           = newFileName;
            dataWasChanged     = true;
        }

        var mtrl = FileSwap.CreateSwap( manager, ResourceType.Mtrl, redirections, actualMtrlFromPath, mtrlToPath, actualMtrlFromPath );
        var shpk = CreateShader( manager, redirections, ref mtrl.AsMtrl()!.ShaderPackage.Name, ref mtrl.DataWasChanged );
        mtrl.ChildSwaps.Add( shpk );

        foreach( ref var texture in mtrl.AsMtrl()!.Textures.AsSpan() )
        {
            var tex = CreateTex( manager, redirections, slot, race, idFrom, ref texture, ref mtrl.DataWasChanged );
            mtrl.ChildSwaps.Add( tex );
        }

        return mtrl;
    }

    public static FileSwap CreateTex( MetaFileManager manager, Func< Utf8GamePath, FullPath > redirections, BodySlot slot, GenderRace race, SetId idFrom, ref MtrlFile.Texture texture,
        ref bool dataWasChanged )
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

        return FileSwap.CreateSwap( manager, ResourceType.Tex, redirections, newPath, path, path );
    }


    public static FileSwap CreateShader( MetaFileManager manager, Func< Utf8GamePath, FullPath > redirections, ref string shaderName, ref bool dataWasChanged )
    {
        var path = $"shader/sm5/shpk/{shaderName}";
        return FileSwap.CreateSwap( manager, ResourceType.Shpk, redirections, path, path );
    }
}