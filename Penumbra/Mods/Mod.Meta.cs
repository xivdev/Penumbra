using System;
using System.IO;
using Dalamud.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OtterGui;
using OtterGui.Classes;

namespace Penumbra.Mods;

[Flags]
public enum MetaChangeType : ushort
{
    None        = 0x00,
    Name        = 0x01,
    Author      = 0x02,
    Description = 0x04,
    Version     = 0x08,
    Website     = 0x10,
    Deletion    = 0x20,
    Migration   = 0x40,
    ImportDate  = 0x80,
}

public sealed partial class Mod
{
    public static readonly TemporaryMod ForcedFiles = new()
    {
        Name     = "Forced Files",
        Index    = -1,
        Priority = int.MaxValue,
    };

    public const uint CurrentFileVersion = 1;
    public uint FileVersion { get; private set; } = CurrentFileVersion;
    public LowerString Name { get; private set; } = "New Mod";
    public LowerString Author { get; private set; } = LowerString.Empty;
    public string Description { get; private set; } = string.Empty;
    public string Version { get; private set; } = string.Empty;
    public string Website { get; private set; } = string.Empty;
    public long ImportDate { get; private set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    internal FileInfo MetaFile
        => new(Path.Combine( ModPath.FullName, "meta.json" ));

    private MetaChangeType LoadMeta()
    {
        var metaFile = MetaFile;
        if( !File.Exists( metaFile.FullName ) )
        {
            PluginLog.Debug( "No mod meta found for {ModLocation}.", ModPath.Name );
            return MetaChangeType.Deletion;
        }

        try
        {
            var text = File.ReadAllText( metaFile.FullName );
            var json = JObject.Parse( text );

            var newName        = json[ nameof( Name ) ]?.Value< string >()        ?? string.Empty;
            var newAuthor      = json[ nameof( Author ) ]?.Value< string >()      ?? string.Empty;
            var newDescription = json[ nameof( Description ) ]?.Value< string >() ?? string.Empty;
            var newVersion     = json[ nameof( Version ) ]?.Value< string >()     ?? string.Empty;
            var newWebsite     = json[ nameof( Website ) ]?.Value< string >()     ?? string.Empty;
            var newFileVersion = json[ nameof( FileVersion ) ]?.Value< uint >()   ?? 0;
            var importDate     = json[ nameof( ImportDate ) ]?.Value< long >()    ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            MetaChangeType changes = 0;
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

            if( FileVersion != newFileVersion )
            {
                FileVersion = newFileVersion;
                if( Migration.Migrate( this, json ) )
                {
                    changes |= MetaChangeType.Migration;
                }
            }

            if( ImportDate != importDate )
            {
                ImportDate =  importDate;
                changes    |= MetaChangeType.ImportDate;
            }

            return changes;
        }
        catch( Exception e )
        {
            PluginLog.Error( $"Could not load mod meta:\n{e}" );
            return MetaChangeType.Deletion;
        }
    }

    private void SaveMeta()
        => Penumbra.Framework.RegisterDelayed( nameof( SaveMetaFile ) + ModPath.Name, SaveMetaFile );

    private void SaveMetaFile()
    {
        var metaFile = MetaFile;
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
                { nameof( ImportDate ), JToken.FromObject( ImportDate ) },
            };
            File.WriteAllText( metaFile.FullName, jObject.ToString( Formatting.Indented ) );
        }
        catch( Exception e )
        {
            PluginLog.Error( $"Could not write meta file for mod {Name} to {metaFile.FullName}:\n{e}" );
        }
    }

    public override string ToString()
        => Name.Text;
}