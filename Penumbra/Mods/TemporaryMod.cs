using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OtterGui.Classes;
using Penumbra.Collections;
using Penumbra.Meta.Manipulations;
using Penumbra.String.Classes;

namespace Penumbra.Mods;

public class TemporaryMod : IMod
{
    public LowerString Name     { get; init; } = LowerString.Empty;
    public int         Index    { get; init; } = -2;
    public int         Priority { get; init; } = int.MaxValue;

    public int TotalManipulations
        => Default.Manipulations.Count;

    public ISubMod Default
        => _default;

    public IReadOnlyList< IModGroup > Groups
        => Array.Empty< IModGroup >();

    public IEnumerable< ISubMod > AllSubMods
        => new[] { Default };

    private readonly SubMod _default;

    public TemporaryMod()
        => _default = new SubMod( this );

    public void SetFile( Utf8GamePath gamePath, FullPath fullPath )
        => _default.FileData[ gamePath ] = fullPath;

    public bool SetManipulation( MetaManipulation manip )
        => _default.ManipulationData.Remove( manip ) | _default.ManipulationData.Add( manip );

    public void SetAll( Dictionary< Utf8GamePath, FullPath > dict, HashSet< MetaManipulation > manips )
    {
        _default.FileData         = dict;
        _default.ManipulationData = manips;
    }

    public static void SaveTempCollection( ModManager modManager, ModCollection collection, string? character = null )
    {
        DirectoryInfo? dir = null;
        try
        {
            dir = Mod.Creator.CreateModFolder( Penumbra.ModManager.BasePath, collection.Name );
            var fileDir = Directory.CreateDirectory( Path.Combine( dir.FullName, "files" ) );
            modManager.DataEditor.CreateMeta( dir, collection.Name, character ?? Penumbra.Config.DefaultModAuthor,
                $"Mod generated from temporary collection {collection.Name} for {character ?? "Unknown Character"}.", null, null );
            var mod        = new Mod( dir );
            var defaultMod = (SubMod) mod.Default;
            foreach( var (gamePath, fullPath) in collection.ResolvedFiles )
            {
                if( gamePath.Path.EndsWith( ".imc"u8 ) )
                {
                    continue;
                }

                var targetPath = fullPath.Path.FullName;
                if( fullPath.Path.Name.StartsWith( '|' ) )
                {
                    targetPath = targetPath.Split( '|', 3, StringSplitOptions.RemoveEmptyEntries ).Last();
                }

                if( Path.IsPathRooted(targetPath) )
                {
                    var target = Path.Combine( fileDir.FullName, Path.GetFileName(targetPath) );
                    File.Copy( targetPath, target, true );
                    defaultMod.FileData[ gamePath ] = new FullPath( target );
                }
                else
                {
                    defaultMod.FileSwapData[ gamePath ] = new FullPath(targetPath);
                }
            }

            foreach( var manip in collection.MetaCache?.Manipulations ?? Array.Empty< MetaManipulation >() )
                defaultMod.ManipulationData.Add( manip );

            Penumbra.SaveService.ImmediateSave(new ModSaveGroup(dir, defaultMod));
            modManager.AddMod( dir );
            Penumbra.Log.Information( $"Successfully generated mod {mod.Name} at {mod.ModPath.FullName} for collection {collection.Name}." );
        }
        catch( Exception e )
        {
            Penumbra.Log.Error( $"Could not save temporary collection {collection.Name} to permanent Mod:\n{e}" );
            if( dir != null && Directory.Exists( dir.FullName ) )
            {
                try
                {
                    Directory.Delete( dir.FullName, true );
                }
                catch
                {
                    // ignored
                }
            }
        }
    }
}