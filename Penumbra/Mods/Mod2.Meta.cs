using System;
using System.IO;
using Dalamud.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OtterGui;

namespace Penumbra.Mods;

[Flags]
public enum MetaChangeType : byte
{
    None        = 0x00,
    Name        = 0x01,
    Author      = 0x02,
    Description = 0x04,
    Version     = 0x08,
    Website     = 0x10,
    Deletion    = 0x20,
}

public sealed partial class Mod2
{
    public const uint CurrentFileVersion = 1;
    public uint FileVersion { get; private set; } = CurrentFileVersion;
    public LowerString Name { get; private set; } = "Mod";
    public LowerString Author { get; private set; } = LowerString.Empty;
    public string Description { get; private set; } = string.Empty;
    public string Version { get; private set; } = string.Empty;
    public string Website { get; private set; } = string.Empty;

    private void SaveMeta()
        => SaveToFile( MetaFile );

    private MetaChangeType LoadMetaFromFile( FileInfo filePath )
    {
        if( !File.Exists( filePath.FullName ) )
        {
            return MetaChangeType.Deletion;
        }

        try
        {
            var text = File.ReadAllText( filePath.FullName );
            var json = JObject.Parse( text );

            var newName        = json[ nameof( Name ) ]?.Value< string >()        ?? string.Empty;
            var newAuthor      = json[ nameof( Author ) ]?.Value< string >()      ?? string.Empty;
            var newDescription = json[ nameof( Description ) ]?.Value< string >() ?? string.Empty;
            var newVersion     = json[ nameof( Version ) ]?.Value< string >()     ?? string.Empty;
            var newWebsite     = json[ nameof( Website ) ]?.Value< string >()     ?? string.Empty;
            var newFileVersion = json[ nameof( FileVersion ) ]?.Value< uint >()   ?? 0;

            MetaChangeType changes = 0;
            if( newFileVersion < CurrentFileVersion )
            {
                Migration.Migrate( this, text );
                FileVersion = newFileVersion;
            }

            if( Name != newName )
            {
                changes |= MetaChangeType.Name;
                Name    =  newName;
            }

            if( Author != newAuthor )
            {
                changes |= MetaChangeType.Author;
                Author  =  newAuthor;
            }

            if( Description != newDescription )
            {
                changes     |= MetaChangeType.Description;
                Description =  newDescription;
            }

            if( Version != newVersion )
            {
                changes |= MetaChangeType.Version;
                Version =  newVersion;
            }

            if( Website != newWebsite )
            {
                changes |= MetaChangeType.Website;
                Website =  newWebsite;
            }


            return changes;
        }
        catch( Exception e )
        {
            PluginLog.Error( $"Could not load mod meta:\n{e}" );
            return MetaChangeType.Deletion;
        }
    }

    private void SaveToFile( FileInfo filePath )
    {
        try
        {
            var jObject = new JObject
            {
                { nameof( FileVersion ), JToken.FromObject( FileVersion ) },
                { nameof( Name ), JToken.FromObject( Name ) },
                { nameof( Author ), JToken.FromObject( Author ) },
                { nameof( Description ), JToken.FromObject( Description ) },
                { nameof( Version ), JToken.FromObject( Version ) },
                { nameof( Website ), JToken.FromObject( Website ) },
            };
            File.WriteAllText( filePath.FullName, jObject.ToString( Formatting.Indented ) );
        }
        catch( Exception e )
        {
            PluginLog.Error( $"Could not write meta file for mod {Name} to {filePath.FullName}:\n{e}" );
        }
    }
}