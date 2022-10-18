using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OtterGui.Classes;

namespace Penumbra.Mods;

[Flags]
public enum ModDataChangeType : ushort
{
    None        = 0x0000,
    Name        = 0x0001,
    Author      = 0x0002,
    Description = 0x0004,
    Version     = 0x0008,
    Website     = 0x0010,
    Deletion    = 0x0020,
    Migration   = 0x0040,
    ModTags     = 0x0080,
    ImportDate  = 0x0100,
    Favorite    = 0x0200,
    LocalTags   = 0x0400,
    Note        = 0x0800,
}

public sealed partial class Mod
{
    public static readonly TemporaryMod ForcedFiles = new()
    {
        Name     = "Forced Files",
        Index    = -1,
        Priority = int.MaxValue,
    };

    public const uint CurrentFileVersion = 3;
    public uint FileVersion { get; private set; } = CurrentFileVersion;
    public LowerString Name { get; private set; } = "New Mod";
    public LowerString Author { get; private set; } = LowerString.Empty;
    public string Description { get; private set; } = string.Empty;
    public string Version { get; private set; } = string.Empty;
    public string Website { get; private set; } = string.Empty;
    public IReadOnlyList< string > ModTags { get; private set; } = Array.Empty< string >();

    internal FileInfo MetaFile
        => new(Path.Combine( ModPath.FullName, "meta.json" ));

    private ModDataChangeType LoadMeta()
    {
        var metaFile = MetaFile;
        if( !File.Exists( metaFile.FullName ) )
        {
            Penumbra.Log.Debug( $"No mod meta found for {ModPath.Name}." );
            return ModDataChangeType.Deletion;
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
            var importDate     = json[ nameof( ImportDate ) ]?.Value< long >();
            var modTags        = json[ nameof( ModTags ) ]?.Values< string >().OfType< string >();

            ModDataChangeType changes = 0;
            if( Name != newName )
            {
                changes |= ModDataChangeType.Name;
                Name    =  newName;
            }

            if( Author != newAuthor )
            {
                changes |= ModDataChangeType.Author;
                Author  =  newAuthor;
            }

            if( Description != newDescription )
            {
                changes     |= ModDataChangeType.Description;
                Description =  newDescription;
            }

            if( Version != newVersion )
            {
                changes |= ModDataChangeType.Version;
                Version =  newVersion;
            }

            if( Website != newWebsite )
            {
                changes |= ModDataChangeType.Website;
                Website =  newWebsite;
            }

            if( FileVersion != newFileVersion )
            {
                FileVersion = newFileVersion;
                if( Migration.Migrate( this, json ) )
                {
                    changes |= ModDataChangeType.Migration;
                }
            }

            if( importDate != null && ImportDate != importDate.Value )
            {
                ImportDate =  importDate.Value;
                changes    |= ModDataChangeType.ImportDate;
            }

            changes |= UpdateTags( modTags, null );

            return changes;
        }
        catch( Exception e )
        {
            Penumbra.Log.Error( $"Could not load mod meta:\n{e}" );
            return ModDataChangeType.Deletion;
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
                { nameof( ModTags ), JToken.FromObject( ModTags ) },
            };
            File.WriteAllText( metaFile.FullName, jObject.ToString( Formatting.Indented ) );
        }
        catch( Exception e )
        {
            Penumbra.Log.Error( $"Could not write meta file for mod {Name} to {metaFile.FullName}:\n{e}" );
        }
    }

    public override string ToString()
        => Name.Text;
}