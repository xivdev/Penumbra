using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dalamud.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Penumbra.GameData.ByteString;
using Penumbra.Util;

namespace Penumbra.Mods;

// Contains descriptive data about the mod as well as possible settings and fileswaps.
public class ModMeta
{
    public const uint CurrentFileVersion = 1;

    [Flags]
    public enum ChangeType : byte
    {
        Name        = 0x01,
        Author      = 0x02,
        Description = 0x04,
        Version     = 0x08,
        Website     = 0x10,
        Deletion    = 0x20,
    }

    public uint FileVersion { get; set; } = CurrentFileVersion;
    public LowerString Name { get; set; } = "Mod";
    public LowerString Author { get; set; } = LowerString.Empty;
    public string Description { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Website { get; set; } = string.Empty;

    public bool HasGroupsWithConfig = false;

    public bool RefreshHasGroupsWithConfig()
    {
        var oldValue = HasGroupsWithConfig;
        HasGroupsWithConfig = Groups.Values.Any( g => g.Options.Count > 1 || g.SelectionType == SelectType.Multi && g.Options.Count == 1 );
        return oldValue != HasGroupsWithConfig;
    }

    public ChangeType RefreshFromFile( FileInfo filePath )
    {
        var newMeta = LoadFromFile( filePath );
        if( newMeta == null )
        {
            return ChangeType.Deletion;
        }

        ChangeType changes = 0;

        if( Name != newMeta.Name )
        {
            changes |= ChangeType.Name;
            Name    =  newMeta.Name;
        }

        if( Author != newMeta.Author )
        {
            changes |= ChangeType.Author;
            Author  =  newMeta.Author;
        }

        if( Description != newMeta.Description )
        {
            changes     |= ChangeType.Description;
            Description =  newMeta.Description;
        }

        if( Version != newMeta.Version )
        {
            changes |= ChangeType.Version;
            Version =  newMeta.Version;
        }

        if( Website != newMeta.Website )
        {
            changes |= ChangeType.Website;
            Website =  newMeta.Website;
        }

        return changes;
    }

    [JsonProperty( ItemConverterType = typeof( FullPath.FullPathConverter ) )]
    public Dictionary< Utf8GamePath, FullPath > FileSwaps { get; set; } = new();

    public Dictionary< string, OptionGroup > Groups { get; set; } = new();

    public static ModMeta? LoadFromFile( FileInfo filePath )
    {
        try
        {
            var text = File.ReadAllText( filePath.FullName );

            var meta = JsonConvert.DeserializeObject< ModMeta >( text,
                new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore } );
            if( meta != null )
            {
                meta.RefreshHasGroupsWithConfig();
                Migration.Migrate( meta, text );
            }

            return meta;
        }
        catch( Exception e )
        {
            PluginLog.Error( $"Could not load mod meta:\n{e}" );
            return null;
        }
    }


    public void SaveToFile( FileInfo filePath )
    {
        try
        {
            var text = JsonConvert.SerializeObject( this, Formatting.Indented );
            File.WriteAllText( filePath.FullName, text );
        }
        catch( Exception e )
        {
            PluginLog.Error( $"Could not write meta file for mod {Name} to {filePath.FullName}:\n{e}" );
        }
    }

    private static class Migration
    {
        public static void Migrate( ModMeta meta, string text )
        {
            MigrateV0ToV1( meta, text );
        }

        private static void MigrateV0ToV1( ModMeta meta, string text )
        {
            if( meta.FileVersion > 0 )
            {
                return;
            }

            var data = JObject.Parse( text );
            var swaps = data[ "FileSwaps" ]?.ToObject< Dictionary< Utf8GamePath, FullPath > >()
             ?? new Dictionary< Utf8GamePath, FullPath >();
            var groups = data[ "Groups" ]?.ToObject< Dictionary< string, OptionGroupV0 > >() ?? new Dictionary< string, OptionGroupV0 >();
            foreach( var group in groups.Values )
            { }

            foreach( var swap in swaps )
            { }

            //var meta = 
        }


        private struct OptionV0
        {
            public string OptionName = string.Empty;
            public string OptionDesc = string.Empty;

            [JsonProperty( ItemConverterType = typeof( SingleOrArrayConverter< Utf8GamePath > ) )]
            public Dictionary< Utf8RelPath, HashSet< Utf8GamePath > > OptionFiles = new();

            public OptionV0()
            { }
        }

        private struct OptionGroupV0
        {
            public string GroupName = string.Empty;

            [JsonConverter( typeof( Newtonsoft.Json.Converters.StringEnumConverter ) )]
            public SelectType SelectionType = SelectType.Single;

            public List< OptionV0 > Options = new();

            public OptionGroupV0()
            { }
        }
    }
}