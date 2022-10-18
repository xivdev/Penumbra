using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace Penumbra.Mods;

public sealed partial class Mod
{
    public static DirectoryInfo LocalDataDirectory
        => new(Path.Combine( Dalamud.PluginInterface.ConfigDirectory.FullName, "mod_data" ));

    public long ImportDate { get; private set; } = DateTimeOffset.UnixEpoch.ToUnixTimeMilliseconds();

    public IReadOnlyList< string > LocalTags { get; private set; } = Array.Empty< string >();

    public string AllTagsLower { get; private set; } = string.Empty;
    public string Note { get; private set; } = string.Empty;
    public bool Favorite { get; private set; } = false;

    private FileInfo LocalDataFile
        => new(Path.Combine( Dalamud.PluginInterface.ConfigDirectory.FullName, "mod_data", $"{ModPath.Name}.json" ));

    private ModDataChangeType LoadLocalData()
    {
        var dataFile = LocalDataFile;

        var importDate = 0L;
        var localTags  = Enumerable.Empty< string >();
        var favorite   = false;
        var note       = string.Empty;

        var save = true;
        if( File.Exists( dataFile.FullName ) )
        {
            save = false;
            try
            {
                var text = File.ReadAllText( dataFile.FullName );
                var json = JObject.Parse( text );

                importDate = json[ nameof( ImportDate ) ]?.Value< long >()                      ?? importDate;
                favorite   = json[ nameof( Favorite ) ]?.Value< bool >()                        ?? favorite;
                note       = json[ nameof( Note ) ]?.Value< string >()                          ?? note;
                localTags  = json[ nameof( LocalTags ) ]?.Values< string >().OfType< string >() ?? localTags;
            }
            catch( Exception e )
            {
                Penumbra.Log.Error( $"Could not load local mod data:\n{e}" );
            }
        }

        if( importDate == 0 )
        {
            importDate = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }

        ModDataChangeType changes = 0;
        if( ImportDate != importDate )
        {
            ImportDate =  importDate;
            changes    |= ModDataChangeType.ImportDate;
        }

        changes |= UpdateTags( null, localTags );

        if( Favorite != favorite )
        {
            Favorite =  favorite;
            changes  |= ModDataChangeType.Favorite;
        }

        if( Note != note )
        {
            Note    =  note;
            changes |= ModDataChangeType.Note;
        }

        if( save )
        {
            SaveLocalDataFile();
        }

        return changes;
    }

    private void SaveLocalData()
        => Penumbra.Framework.RegisterDelayed( nameof( SaveLocalData ) + ModPath.Name, SaveLocalDataFile );

    private void SaveLocalDataFile()
    {
        var dataFile = LocalDataFile;
        try
        {
            var jObject = new JObject
            {
                { nameof( FileVersion ), JToken.FromObject( FileVersion ) },
                { nameof( ImportDate ), JToken.FromObject( ImportDate ) },
                { nameof( LocalTags ), JToken.FromObject( LocalTags ) },
                { nameof( Note ), JToken.FromObject( Note ) },
                { nameof( Favorite ), JToken.FromObject( Favorite ) },
            };
            dataFile.Directory!.Create();
            File.WriteAllText( dataFile.FullName, jObject.ToString( Formatting.Indented ) );
        }
        catch( Exception e )
        {
            Penumbra.Log.Error( $"Could not write local data file for mod {Name} to {dataFile.FullName}:\n{e}" );
        }
    }

    private static void MoveDataFile( DirectoryInfo oldMod, DirectoryInfo newMod )
    {
        var oldFile = Path.Combine( Dalamud.PluginInterface.ConfigDirectory.FullName, "mod_data", $"{oldMod.Name}.json" );
        var newFile = Path.Combine( Dalamud.PluginInterface.ConfigDirectory.FullName, "mod_data", $"{newMod.Name}.json" );
        if( File.Exists( oldFile ) )
        {
            try
            {
                File.Move( oldFile, newFile, true );
            }
            catch( Exception e )
            {
                Penumbra.Log.Error( $"Could not move local data file {oldFile} to {newFile}:\n{e}" );
            }
        }
    }

    private ModDataChangeType UpdateTags( IEnumerable< string >? newModTags, IEnumerable< string >? newLocalTags )
    {
        if( newModTags == null && newLocalTags == null )
        {
            return 0;
        }

        ModDataChangeType type = 0;
        if( newModTags != null )
        {
            var modTags = newModTags.Where( t => t.Length > 0 ).Distinct().ToArray();
            if( !modTags.SequenceEqual( ModTags ) )
            {
                newLocalTags ??= LocalTags;
                ModTags      =   modTags;
                type         |=  ModDataChangeType.ModTags;
            }
        }

        if( newLocalTags != null )
        {
            var localTags = newLocalTags!.Where( t => t.Length > 0 && !ModTags.Contains( t ) ).Distinct().ToArray();
            if( !localTags.SequenceEqual( LocalTags ) )
            {
                LocalTags =  localTags;
                type      |= ModDataChangeType.LocalTags;
            }
        }

        if( type != 0 )
        {
            AllTagsLower = string.Join( '\0', ModTags.Concat( LocalTags ).Select( s => s.ToLowerInvariant() ) );
        }

        return type;
    }
}