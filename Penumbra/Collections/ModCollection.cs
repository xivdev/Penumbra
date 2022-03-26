using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Dalamud.Logging;
using Newtonsoft.Json.Linq;
using Penumbra.Mod;
using Penumbra.Util;

namespace Penumbra.Collections;

// A ModCollection is a named set of ModSettings to all of the users' installed mods.
// It is meant to be local only, and thus should always contain settings for every mod, not just the enabled ones.
// Settings to mods that are not installed anymore are kept as long as no call to CleanUnavailableSettings is made.
// Active ModCollections build a cache of currently relevant data.
public partial class ModCollection
{
    public const int    CurrentVersion    = 1;
    public const string DefaultCollection = "Default";
    public const string EmptyCollection   = "None";

    public static readonly ModCollection Empty = CreateEmpty();

    private static ModCollection CreateEmpty()
    {
        var collection = CreateNewEmpty( EmptyCollection );
        collection.Index = 0;
        collection._settings.Clear();
        return collection;
    }

    public string Name { get; private init; }
    public int Version { get; private set; }
    public int Index { get; private set; } = -1;

    private readonly List< ModSettings? > _settings;

    public IReadOnlyList< ModSettings? > Settings
        => _settings;

    public IEnumerable< ModSettings? > ActualSettings
        => Enumerable.Range( 0, _settings.Count ).Select( i => this[ i ].Settings );

    private readonly Dictionary< string, ModSettings > _unusedSettings;


    private ModCollection( string name, ModCollection duplicate )
    {
        Name               =  name;
        Version            =  duplicate.Version;
        _settings          =  duplicate._settings.ConvertAll( s => s?.DeepCopy() );
        _unusedSettings    =  duplicate._unusedSettings.ToDictionary( kvp => kvp.Key, kvp => kvp.Value.DeepCopy() );
        _inheritance       =  duplicate._inheritance.ToList();
        ModSettingChanged  += SaveOnChange;
        InheritanceChanged += SaveOnChange;
    }

    private ModCollection( string name, int version, Dictionary< string, ModSettings > allSettings )
    {
        Name            = name;
        Version         = version;
        _unusedSettings = allSettings;
        _settings       = Enumerable.Repeat( ( ModSettings? )null, Penumbra.ModManager.Count ).ToList();
        for( var i = 0; i < Penumbra.ModManager.Count; ++i )
        {
            var modName = Penumbra.ModManager[ i ].BasePath.Name;
            if( _unusedSettings.TryGetValue( Penumbra.ModManager[ i ].BasePath.Name, out var settings ) )
            {
                _unusedSettings.Remove( modName );
                _settings[ i ] = settings;
            }
        }

        Migration.Migrate( this );
        ModSettingChanged  += SaveOnChange;
        InheritanceChanged += SaveOnChange;
    }

    public static ModCollection CreateNewEmpty( string name )
        => new(name, CurrentVersion, new Dictionary< string, ModSettings >());

    public ModCollection Duplicate( string name )
        => new(name, this);

    internal static ModCollection MigrateFromV0( string name, Dictionary< string, ModSettings > allSettings )
        => new(name, 0, allSettings);

    public void CleanUnavailableSettings()
    {
        var any = _unusedSettings.Count > 0;
        _unusedSettings.Clear();
        if( any )
        {
            Save();
        }
    }

    public void AddMod( Mod.Mod mod )
    {
        if( _unusedSettings.TryGetValue( mod.BasePath.Name, out var settings ) )
        {
            _settings.Add( settings );
            _unusedSettings.Remove( mod.BasePath.Name );
        }
        else
        {
            _settings.Add( null );
        }
    }

    public void RemoveMod( Mod.Mod mod, int idx )
    {
        var settings = _settings[ idx ];
        if( settings != null )
        {
            _unusedSettings.Add( mod.BasePath.Name, settings );
        }

        _settings.RemoveAt( idx );
    }

    public static string CollectionDirectory
        => Path.Combine( Dalamud.PluginInterface.GetPluginConfigDirectory(), "collections" );

    public FileInfo FileName
        => new(Path.Combine( CollectionDirectory, $"{Name.RemoveInvalidPathSymbols()}.json" ));

    public void Save()
    {
        try
        {
            var file = FileName;
            file.Directory?.Create();
            using var s = file.Open( FileMode.Truncate );
            using var w = new StreamWriter( s, Encoding.UTF8 );
            using var j = new JsonTextWriter( w );
            j.Formatting = Formatting.Indented;
            var x = JsonSerializer.Create( new JsonSerializerSettings { Formatting = Formatting.Indented } );
            j.WriteStartObject();
            j.WritePropertyName( nameof( Version ) );
            j.WriteValue( Version );
            j.WritePropertyName( nameof( Name ) );
            j.WriteValue( Name );
            j.WritePropertyName( nameof( Settings ) );
            j.WriteStartObject();
            for( var i = 0; i < _settings.Count; ++i )
            {
                var settings = _settings[ i ];
                if( settings != null )
                {
                    j.WritePropertyName( Penumbra.ModManager[ i ].BasePath.Name );
                    x.Serialize( j, settings );
                }
            }

            foreach( var settings in _unusedSettings )
            {
                j.WritePropertyName( settings.Key );
                x.Serialize( j, settings.Value );
            }

            j.WriteEndObject();
            j.WritePropertyName( nameof( Inheritance ) );
            x.Serialize( j, Inheritance );
            j.WriteEndObject();
        }
        catch( Exception e )
        {
            PluginLog.Error( $"Could not save collection {Name}:\n{e}" );
        }
    }

    public void Delete()
    {
        if( Index == 0 )
        {
            return;
        }

        var file = FileName;
        if( file.Exists )
        {
            try
            {
                file.Delete();
            }
            catch( Exception e )
            {
                PluginLog.Error( $"Could not delete collection file {file.FullName} for {Name}:\n{e}" );
            }
        }
    }

    public static ModCollection? LoadFromFile( FileInfo file, out IReadOnlyList< string > inheritance )
    {
        inheritance = Array.Empty< string >();
        if( !file.Exists )
        {
            PluginLog.Error( $"Could not read collection because {file.FullName} does not exist." );
            return null;
        }

        try
        {
            var obj     = JObject.Parse( File.ReadAllText( file.FullName ) );
            var name    = obj[ nameof( Name ) ]?.ToObject< string >() ?? string.Empty;
            var version = obj[ nameof( Version ) ]?.ToObject< int >() ?? 0;
            var settings = obj[ nameof( Settings ) ]?.ToObject< Dictionary< string, ModSettings > >()
             ?? new Dictionary< string, ModSettings >();
            inheritance = obj[ nameof( Inheritance ) ]?.ToObject< List< string > >() ?? ( IReadOnlyList< string > )Array.Empty< string >();

            return new ModCollection( name, version, settings );
        }
        catch( Exception e )
        {
            PluginLog.Error( $"Could not read collection information from {file.FullName}:\n{e}" );
        }

        return null;
    }
}