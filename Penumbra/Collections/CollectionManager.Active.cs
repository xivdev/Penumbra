using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dalamud.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Penumbra.Mods;
using Penumbra.Util;

namespace Penumbra.Collections;

public partial class ModCollection
{
    public sealed partial class Manager
    {
        // Is invoked after the collections actually changed.
        public event CollectionChangeDelegate CollectionChanged;

        // The collection currently selected for changing settings.
        public ModCollection Current { get; private set; } = Empty;

        // The collection used for general file redirections and all characters not specifically named.
        public ModCollection Default { get; private set; } = Empty;

        // A single collection that can not be deleted as a fallback for the current collection.
        private ModCollection DefaultName { get; set; } = Empty;

        // The list of character collections.
        private readonly Dictionary< string, ModCollection > _characters = new();

        public IReadOnlyDictionary< string, ModCollection > Characters
            => _characters;

        // If a name does not correspond to a character, return the default collection instead.
        public ModCollection Character( string name )
            => _characters.TryGetValue( name, out var c ) ? c : Default;

        // Set a active collection, can be used to set Default, Current or Character collections.
        private void SetCollection( int newIdx, Type type, string? characterName = null )
        {
            var oldCollectionIdx = type switch
            {
                Type.Default => Default.Index,
                Type.Current => Current.Index,
                Type.Character => characterName?.Length > 0
                    ? _characters.TryGetValue( characterName, out var c )
                        ? c.Index
                        : Default.Index
                    : -1,
                _ => -1,
            };

            if( oldCollectionIdx == -1 || newIdx == oldCollectionIdx )
            {
                return;
            }

            var newCollection = this[ newIdx ];
            if( newIdx > Empty.Index )
            {
                newCollection.CreateCache( false );
            }

            RemoveCache( oldCollectionIdx );
            switch( type )
            {
                case Type.Default:
                    Default = newCollection;
                    Penumbra.ResidentResources.Reload();
                    Default.SetFiles();
                    break;
                case Type.Current:
                    Current = newCollection;
                    break;
                case Type.Character:
                    _characters[ characterName! ] = newCollection;
                    break;
            }

            CollectionChanged.Invoke( type, this[ oldCollectionIdx ], newCollection, characterName );
        }

        public void SetCollection( ModCollection collection, Type type, string? characterName = null )
            => SetCollection( collection.Index, type, characterName );

        // Create a new character collection. Returns false if the character name already has a collection.
        public bool CreateCharacterCollection( string characterName )
        {
            if( _characters.ContainsKey( characterName ) )
            {
                return false;
            }

            _characters[ characterName ] = Empty;
            CollectionChanged.Invoke( Type.Character, null, Empty, characterName );
            return true;
        }

        // Remove a character collection if it exists.
        public void RemoveCharacterCollection( string characterName )
        {
            if( _characters.TryGetValue( characterName, out var collection ) )
            {
                RemoveCache( collection.Index );
                _characters.Remove( characterName );
                CollectionChanged.Invoke( Type.Character, collection, null, characterName );
            }
        }

        // Obtain the index of a collection by name.
        private int GetIndexForCollectionName( string name )
            => name.Length == 0 ? Empty.Index : _collections.IndexOf( c => c.Name == name );

        public static string ActiveCollectionFile
            => Path.Combine( Dalamud.PluginInterface.ConfigDirectory.FullName, "active_collections.json" );

        // Load default, current and character collections from config.
        // Then create caches. If a collection does not exist anymore, reset it to an appropriate default.
        private void LoadCollections()
        {
            var configChanged = !ReadActiveCollections( out var jObject );

            // Load the default collection.
            var defaultName = jObject[ nameof( Default ) ]?.ToObject< string >() ?? Empty.Name;
            var defaultIdx  = GetIndexForCollectionName( defaultName );
            if( defaultIdx < 0 )
            {
                PluginLog.Error( $"Last choice of Default Collection {defaultName} is not available, reset to {Empty.Name}." );
                Default       = Empty;
                configChanged = true;
            }
            else
            {
                Default = this[ defaultIdx ];
            }

            // Load the current collection.
            var currentName = jObject[ nameof( Current ) ]?.ToObject< string >() ?? DefaultCollection;
            var currentIdx  = GetIndexForCollectionName( currentName );
            if( currentIdx < 0 )
            {
                PluginLog.Error( $"Last choice of Current Collection {currentName} is not available, reset to {DefaultCollection}." );
                Current       = DefaultName;
                configChanged = true;
            }
            else
            {
                Current = this[ currentIdx ];
            }

            // Load character collections. If a player name comes up multiple times, the last one is applied.
            var characters = jObject[ nameof( Characters ) ]?.ToObject< Dictionary< string, string > >() ?? new Dictionary< string, string >();
            foreach( var (player, collectionName) in characters )
            {
                var idx = GetIndexForCollectionName( collectionName );
                if( idx < 0 )
                {
                    PluginLog.Error( $"Last choice of <{player}>'s Collection {collectionName} is not available, reset to {Empty.Name}." );
                    _characters.Add( player, Empty );
                    configChanged = true;
                }
                else
                {
                    _characters.Add( player, this[ idx ] );
                }
            }

            // Save any changes and create all required caches.
            if( configChanged )
            {
                SaveActiveCollections();
            }

            CreateNecessaryCaches();
        }


        public void SaveActiveCollections()
        {
            Penumbra.Framework.RegisterDelayed( nameof( SaveActiveCollections ),
                () => SaveActiveCollections( Default.Name, Current.Name, Characters.Select( kvp => ( kvp.Key, kvp.Value.Name ) ) ) );
        }

        internal static void SaveActiveCollections( string def, string current, IEnumerable< (string, string) > characters )
        {
            var file = ActiveCollectionFile;
            try
            {
                using var stream = File.Open( file, File.Exists( file ) ? FileMode.Truncate : FileMode.CreateNew );
                using var writer = new StreamWriter( stream );
                using var j      = new JsonTextWriter( writer );
                j.Formatting = Formatting.Indented;
                j.WriteStartObject();
                j.WritePropertyName( nameof( Default ) );
                j.WriteValue( def );
                j.WritePropertyName( nameof( Current ) );
                j.WriteValue( current );
                j.WritePropertyName( nameof( Characters ) );
                j.WriteStartObject();
                foreach( var (character, collection) in characters )
                {
                    j.WritePropertyName( character, true );
                    j.WriteValue( collection );
                }

                j.WriteEndObject();
                j.WriteEndObject();
                PluginLog.Verbose( "Active Collections saved." );
            }
            catch( Exception e )
            {
                PluginLog.Error( $"Could not save active collections to file {file}:\n{e}" );
            }
        }

        // Read the active collection file into a jObject.
        // Returns true if this is successful, false if the file does not exist or it is unsuccessful.
        private static bool ReadActiveCollections( out JObject ret )
        {
            var file = ActiveCollectionFile;
            if( File.Exists( file ) )
            {
                try
                {
                    ret = JObject.Parse( File.ReadAllText( file ) );
                    return true;
                }
                catch( Exception e )
                {
                    PluginLog.Error( $"Could not read active collections from file {file}:\n{e}" );
                }
            }

            ret = new JObject();
            return false;
        }


        // Save if any of the active collections is changed.
        private void SaveOnChange( Type type, ModCollection? _1, ModCollection? _2, string? _3 )
        {
            if( type != Type.Inactive )
            {
                SaveActiveCollections();
            }
        }


        // Cache handling.
        private void CreateNecessaryCaches()
        {
            Default.CreateCache( true );
            Current.CreateCache( false );

            foreach( var collection in _characters.Values )
            {
                collection.CreateCache( false );
            }
        }

        private void RemoveCache( int idx )
        {
            if( idx != Default.Index && idx != Current.Index && _characters.Values.All( c => c.Index != idx ) )
            {
                _collections[ idx ].ClearCache();
            }
        }

        // Recalculate effective files for active collections on events.
        private void OnModAddedActive( bool meta )
        {
            foreach( var collection in this.Where( c => c.HasCache && c[ ^1 ].Settings?.Enabled == true ) )
            {
                collection.CalculateEffectiveFileList( meta, collection == Penumbra.CollectionManager.Default );
            }
        }

        private void OnModRemovedActive( bool meta, IEnumerable< ModSettings? > settings )
        {
            foreach( var (collection, _) in this.Zip( settings ).Where( c => c.First.HasCache && c.Second?.Enabled == true ) )
            {
                collection.CalculateEffectiveFileList( meta, collection == Penumbra.CollectionManager.Default );
            }
        }

        private void OnModChangedActive( bool meta, int modIdx )
        {
            foreach( var collection in this.Where( c => c.HasCache && c[ modIdx ].Settings?.Enabled == true ) )
            {
                collection.CalculateEffectiveFileList( meta, collection == Penumbra.CollectionManager.Default );
            }
        }
    }
}