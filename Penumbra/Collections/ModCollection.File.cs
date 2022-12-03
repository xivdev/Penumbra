using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OtterGui.Filesystem;
using Penumbra.Mods;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Penumbra.Collections;

// File operations like saving, loading and deleting for a collection.
public partial class ModCollection
{
    public static string CollectionDirectory
        => Path.Combine( Dalamud.PluginInterface.GetPluginConfigDirectory(), "collections" );

    // We need to remove all invalid path symbols from the collection name to be able to save it to file.
    public FileInfo FileName
        => new(Path.Combine( CollectionDirectory, $"{Name.RemoveInvalidPathSymbols()}.json" ));

    // Custom serialization due to shared mod information across managers.
    private void SaveCollection()
    {
        try
        {
            Penumbra.Log.Debug( $"Saving collection {AnonymizedName}..." );
            var file = FileName;
            file.Directory?.Create();
            using var s = file.Exists ? file.Open( FileMode.Truncate ) : file.Open( FileMode.CreateNew );
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

            // Write all used and unused settings by mod directory name.
            j.WriteStartObject();
            for( var i = 0; i < _settings.Count; ++i )
            {
                var settings = _settings[ i ];
                if( settings != null )
                {
                    j.WritePropertyName( Penumbra.ModManager[ i ].ModPath.Name );
                    x.Serialize( j, new ModSettings.SavedSettings( settings, Penumbra.ModManager[ i ] ) );
                }
            }

            foreach( var (modDir, settings) in _unusedSettings )
            {
                j.WritePropertyName( modDir );
                x.Serialize( j, settings );
            }

            j.WriteEndObject();

            // Inherit by collection name.
            j.WritePropertyName( nameof( Inheritance ) );
            x.Serialize( j, Inheritance.Select( c => c.Name ) );
            j.WriteEndObject();
        }
        catch( Exception e )
        {
            Penumbra.Log.Error( $"Could not save collection {AnonymizedName}:\n{e}" );
        }
    }

    public void Save()
        => Penumbra.Framework.RegisterDelayed( nameof( SaveCollection ) + Name, SaveCollection );

    public void Delete()
    {
        if( Index == 0 )
        {
            return;
        }

        var file = FileName;
        if( !file.Exists )
        {
            return;
        }

        try
        {
            file.Delete();
            Penumbra.Log.Information( $"Deleted collection file for {AnonymizedName}." );
        }
        catch( Exception e )
        {
            Penumbra.Log.Error( $"Could not delete collection file for {AnonymizedName}:\n{e}" );
        }
    }

    // Since inheritances depend on other collections existing,
    // we return them as a list to be applied after reading all collections.
    private static ModCollection? LoadFromFile( FileInfo file, out IReadOnlyList< string > inheritance )
    {
        inheritance = Array.Empty< string >();
        if( !file.Exists )
        {
            Penumbra.Log.Error( "Could not read collection because file does not exist." );
            return null;
        }

        try
        {
            var obj     = JObject.Parse( File.ReadAllText( file.FullName ) );
            var name    = obj[ nameof( Name ) ]?.ToObject< string >() ?? string.Empty;
            var version = obj[ nameof( Version ) ]?.ToObject< int >() ?? 0;
            // Custom deserialization that is converted with the constructor. 
            var settings = obj[ nameof( Settings ) ]?.ToObject< Dictionary< string, ModSettings.SavedSettings > >()
             ?? new Dictionary< string, ModSettings.SavedSettings >();
            inheritance = obj[ nameof( Inheritance ) ]?.ToObject< List< string > >() ?? ( IReadOnlyList< string > )Array.Empty< string >();

            return new ModCollection( name, version, settings );
        }
        catch( Exception e )
        {
            Penumbra.Log.Error( $"Could not read collection information from file:\n{e}" );
        }

        return null;
    }
}