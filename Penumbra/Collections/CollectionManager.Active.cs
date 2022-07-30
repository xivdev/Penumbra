using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dalamud.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OtterGui;
using Penumbra.Mods;

namespace Penumbra.Collections;

public partial class ModCollection
{
    public sealed partial class Manager
    {
        // Is invoked after the collections actually changed.
        public event CollectionChangeDelegate CollectionChanged;

        // The collection currently selected for changing settings.
        public ModCollection Current { get; private set; } = Empty;

        // The collection currently selected is in use either as an active collection or through inheritance.
        public bool CurrentCollectionInUse { get; private set; }

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

        // Special Collections
        private readonly ModCollection?[] _specialCollections = new ModCollection?[Enum.GetValues< CollectionType >().Length - 4];

        // Return the configured collection for the given type or null.
        public ModCollection? ByType( CollectionType type, string? name = null )
        {
            if( type.IsSpecial() )
            {
                return _specialCollections[ ( int )type ];
            }

            return type switch
            {
                CollectionType.Default   => Default,
                CollectionType.Current   => Current,
                CollectionType.Character => name != null ? _characters.TryGetValue( name, out var c ) ? c : null : null,
                CollectionType.Inactive  => name != null ? ByName( name, out var c ) ? c : null : null,
                _                        => null,
            };
        }

        // Set a active collection, can be used to set Default, Current or Character collections.
        private void SetCollection( int newIdx, CollectionType collectionType, string? characterName = null )
        {
            var oldCollectionIdx = collectionType switch
            {
                CollectionType.Default => Default.Index,
                CollectionType.Current => Current.Index,
                CollectionType.Character => characterName?.Length > 0
                    ? _characters.TryGetValue( characterName, out var c )
                        ? c.Index
                        : Default.Index
                    : -1,
                _ when collectionType.IsSpecial() => _specialCollections[ ( int )collectionType ]?.Index ?? Default.Index,
                _                                 => -1,
            };

            if( oldCollectionIdx == -1 || newIdx == oldCollectionIdx )
            {
                return;
            }

            var newCollection = this[ newIdx ];
            if( newIdx > Empty.Index )
            {
                newCollection.CreateCache();
            }

            switch( collectionType )
            {
                case CollectionType.Default:
                    Default = newCollection;
                    if( Penumbra.CharacterUtility.Ready )
                    {
                        Penumbra.ResidentResources.Reload();
                        Default.SetFiles();
                    }

                    break;
                case CollectionType.Current:
                    Current = newCollection;
                    break;
                case CollectionType.Character:
                    _characters[ characterName! ] = newCollection;
                    break;
                default:
                    _specialCollections[ ( int )collectionType ] = newCollection;
                    break;
            }

            RemoveCache( oldCollectionIdx );

            UpdateCurrentCollectionInUse();
            CollectionChanged.Invoke( collectionType, this[ oldCollectionIdx ], newCollection, characterName );
        }

        private void UpdateCurrentCollectionInUse()
            => CurrentCollectionInUse = _specialCollections
               .OfType< ModCollection >()
               .Prepend( Default )
               .Concat( Characters.Values )
               .SelectMany( c => c.GetFlattenedInheritance() ).Contains( Current );

        public void SetCollection( ModCollection collection, CollectionType collectionType, string? characterName = null )
            => SetCollection( collection.Index, collectionType, characterName );

        // Create a special collection if it does not exist and set it to Empty.
        public bool CreateSpecialCollection( CollectionType collectionType )
        {
            if( !collectionType.IsSpecial() || _specialCollections[ ( int )collectionType ] != null )
            {
                return false;
            }

            _specialCollections[ ( int )collectionType ] = Empty;
            CollectionChanged.Invoke( collectionType, null, Empty, null );
            return true;
        }

        // Remove a special collection if it exists
        public void RemoveSpecialCollection( CollectionType collectionType )
        {
            if( !collectionType.IsSpecial() )
            {
                return;
            }

            var old = _specialCollections[ ( int )collectionType ];
            if( old != null )
            {
                _specialCollections[ ( int )collectionType ] = null;
                CollectionChanged.Invoke( collectionType, old, null, null );
            }
        }

        // Create a new character collection. Returns false if the character name already has a collection.
        public bool CreateCharacterCollection( string characterName )
        {
            if( _characters.ContainsKey( characterName ) )
            {
                return false;
            }

            _characters[ characterName ] = Empty;
            CollectionChanged.Invoke( CollectionType.Character, null, Empty, characterName );
            return true;
        }

        // Remove a character collection if it exists.
        public void RemoveCharacterCollection( string characterName )
        {
            if( _characters.TryGetValue( characterName, out var collection ) )
            {
                RemoveCache( collection.Index );
                _characters.Remove( characterName );
                CollectionChanged.Invoke( CollectionType.Character, collection, null, characterName );
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
            var defaultName = jObject[ nameof( Default ) ]?.ToObject< string >() ?? ( configChanged ? DefaultCollection : Empty.Name );
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

            // Load special collections.
            foreach( var type in CollectionTypeExtensions.Special )
            {
                var typeName = jObject[ type.ToString() ]?.ToObject< string >();
                if( typeName != null )
                {
                    var idx = GetIndexForCollectionName( typeName );
                    if( idx < 0 )
                    {
                        PluginLog.Error( $"Last choice of {type.ToName()} Collection {typeName} is not available, removed." );
                        configChanged = true;
                    }
                    else
                    {
                        _specialCollections[ ( int )type ] = this[ idx ];
                    }
                }
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
                () => SaveActiveCollections( Default.Name, Current.Name, Characters.Select( kvp => ( kvp.Key, kvp.Value.Name ) ),
                    _specialCollections.WithIndex()
                       .Where( c => c.Item1 != null )
                       .Select( c => ( ( CollectionType )c.Item2, c.Item1!.Name ) ) ) );
        }

        internal static void SaveActiveCollections( string def, string current, IEnumerable< (string, string) > characters,
            IEnumerable< (CollectionType, string) > special )
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
                foreach( var (type, collection) in special )
                {
                    j.WritePropertyName( type.ToString() );
                    j.WriteValue( collection );
                }

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
        private void SaveOnChange( CollectionType collectionType, ModCollection? _1, ModCollection? _2, string? _3 )
        {
            if( collectionType != CollectionType.Inactive )
            {
                SaveActiveCollections();
            }
        }


        // Cache handling.
        private void CreateNecessaryCaches()
        {
            Default.CreateCache();
            Current.CreateCache();

            foreach( var collection in _specialCollections.OfType< ModCollection >().Concat( _characters.Values ) )
            {
                collection.CreateCache();
            }
        }

        private void RemoveCache( int idx )
        {
            if( idx != Empty.Index
            && idx  != Default.Index
            && idx  != Current.Index
            && _specialCollections.All( c => c == null || c.Index != idx )
            && _characters.Values.All( c => c.Index != idx ) )
            {
                _collections[ idx ].ClearCache();
            }
        }

        // Recalculate effective files for active collections on events.
        private void OnModAddedActive( Mod mod )
        {
            foreach( var collection in this.Where( c => c.HasCache && c[ mod.Index ].Settings?.Enabled == true ) )
            {
                collection._cache!.AddMod( mod, true );
            }
        }

        private void OnModRemovedActive( Mod mod )
        {
            foreach( var collection in this.Where( c => c.HasCache && c[ mod.Index ].Settings?.Enabled == true ) )
            {
                collection._cache!.RemoveMod( mod, true );
            }
        }

        private void OnModMovedActive( Mod mod )
        {
            foreach( var collection in this.Where( c => c.HasCache && c[ mod.Index ].Settings?.Enabled == true ) )
            {
                collection._cache!.ReloadMod( mod, true );
            }
        }
    }
}