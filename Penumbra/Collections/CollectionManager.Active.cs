using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OtterGui;
using Penumbra.Mods;
using Penumbra.UI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Penumbra.Collections;

public partial class ModCollection
{
    public sealed partial class Manager
    {
        public const int Version = 1;

        // Is invoked after the collections actually changed.
        public event CollectionChangeDelegate CollectionChanged;

        // The collection currently selected for changing settings.
        public ModCollection Current { get; private set; } = Empty;

        // The collection currently selected is in use either as an active collection or through inheritance.
        public bool CurrentCollectionInUse { get; private set; }

        // The collection used for general file redirections and all characters not specifically named.
        public ModCollection Default { get; private set; } = Empty;

        // The collection used for all files categorized as UI files.
        public ModCollection Interface { get; private set; } = Empty;

        // A single collection that can not be deleted as a fallback for the current collection.
        private ModCollection DefaultName { get; set; } = Empty;

        // The list of character collections.
        private readonly Dictionary< string, ModCollection > _characters = new();
        public readonly  IndividualCollections               Individuals = new(Penumbra.Actors);

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
                CollectionType.Interface => Interface,
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
                CollectionType.Default   => Default.Index,
                CollectionType.Interface => Interface.Index,
                CollectionType.Current   => Current.Index,
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
                    if( Penumbra.CharacterUtility.Ready && Penumbra.Config.EnableMods )
                    {
                        Penumbra.ResidentResources.Reload();
                        Default.SetFiles();
                    }

                    break;
                case CollectionType.Interface:
                    Interface = newCollection;
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
               .Prepend( Interface )
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

            _specialCollections[ ( int )collectionType ] = Default;
            CollectionChanged.Invoke( collectionType, null, Default, null );
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

            _characters[ characterName ] = Default;
            CollectionChanged.Invoke( CollectionType.Character, null, Default, characterName );
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

        // Load default, current, special, and character collections from config.
        // Then create caches. If a collection does not exist anymore, reset it to an appropriate default.
        private void LoadCollections()
        {
            var configChanged = !ReadActiveCollections( out var jObject );

            // Load the default collection.
            var defaultName = jObject[ nameof( Default ) ]?.ToObject< string >() ?? ( configChanged ? DefaultCollection : Empty.Name );
            var defaultIdx  = GetIndexForCollectionName( defaultName );
            if( defaultIdx < 0 )
            {
                Penumbra.Log.Error( $"Last choice of {ConfigWindow.DefaultCollection} {defaultName} is not available, reset to {Empty.Name}." );
                Default       = Empty;
                configChanged = true;
            }
            else
            {
                Default = this[ defaultIdx ];
            }

            // Load the interface collection.
            var interfaceName = jObject[ nameof( Interface ) ]?.ToObject< string >() ?? Default.Name;
            var interfaceIdx  = GetIndexForCollectionName( interfaceName );
            if( interfaceIdx < 0 )
            {
                Penumbra.Log.Error(
                    $"Last choice of {ConfigWindow.InterfaceCollection} {interfaceName} is not available, reset to {Empty.Name}." );
                Interface     = Empty;
                configChanged = true;
            }
            else
            {
                Interface = this[ interfaceIdx ];
            }

            // Load the current collection.
            var currentName = jObject[ nameof( Current ) ]?.ToObject< string >() ?? DefaultCollection;
            var currentIdx  = GetIndexForCollectionName( currentName );
            if( currentIdx < 0 )
            {
                Penumbra.Log.Error(
                    $"Last choice of {ConfigWindow.SelectedCollection} {currentName} is not available, reset to {DefaultCollection}." );
                Current       = DefaultName;
                configChanged = true;
            }
            else
            {
                Current = this[ currentIdx ];
            }

            // Load special collections.
            foreach( var (type, name, _) in CollectionTypeExtensions.Special )
            {
                var typeName = jObject[ type.ToString() ]?.ToObject< string >();
                if( typeName != null )
                {
                    var idx = GetIndexForCollectionName( typeName );
                    if( idx < 0 )
                    {
                        Penumbra.Log.Error( $"Last choice of {name} Collection {typeName} is not available, removed." );
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
                    Penumbra.Log.Error( $"Last choice of <{player}>'s Collection {collectionName} is not available, reset to {Empty.Name}." );
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

            MigrateIndividualCollections( jObject );
        }

        // Migrate ungendered collections to Male and Female for 0.5.9.0.
        public static void MigrateUngenderedCollections()
        {
            if( !ReadActiveCollections( out var jObject ) )
            {
                return;
            }

            foreach( var (type, _, _) in CollectionTypeExtensions.Special.Where( t => t.Item2.StartsWith( "Male " ) ) )
            {
                var oldName = type.ToString()[ 4.. ];
                var value   = jObject[ oldName ];
                if( value == null )
                {
                    continue;
                }

                jObject.Remove( oldName );
                jObject.Add( "Male"   + oldName, value );
                jObject.Add( "Female" + oldName, value );
            }

            using var stream = File.Open( ActiveCollectionFile, FileMode.Truncate );
            using var writer = new StreamWriter( stream );
            using var j      = new JsonTextWriter( writer );
            j.Formatting = Formatting.Indented;
            jObject.WriteTo( j );
        }

        // Migrate individual collections to Identifiers for 0.6.0.
        private bool MigrateIndividualCollections(JObject jObject)
        {
            var version = jObject[ nameof( Version ) ]?.Value< int >() ?? 0;
            if( version > 0 )
                return false;
            
            // Load character collections. If a player name comes up multiple times, the last one is applied.
            var characters    = jObject[nameof( Characters )]?.ToObject<Dictionary<string, string>>() ?? new Dictionary<string, string>();
            var dict          = new Dictionary< string, ModCollection >( characters.Count );
            foreach( var (player, collectionName) in characters )
            {
                var idx = GetIndexForCollectionName( collectionName );
                if( idx < 0 )
                {
                    Penumbra.Log.Error( $"Last choice of <{player}>'s Collection {collectionName} is not available, reset to {Empty.Name}." );
                    dict.Add( player, Empty );
                }
                else
                {
                    dict.Add( player, this[ idx ] );
                }
            }
            
            Individuals.Migrate0To1( dict );
            return true;
        }

        public void SaveActiveCollections()
        {
            Penumbra.Framework.RegisterDelayed( nameof( SaveActiveCollections ),
                () => SaveActiveCollections( Default.Name, Interface.Name, Current.Name,
                    Characters.Select( kvp => ( kvp.Key, kvp.Value.Name ) ),
                    _specialCollections.WithIndex()
                       .Where( c => c.Item1 != null )
                       .Select( c => ( ( CollectionType )c.Item2, c.Item1!.Name ) ) ) );
        }

        internal static void SaveActiveCollections( string def, string ui, string current, IEnumerable< (string, string) > characters,
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
                j.WritePropertyName( nameof( Interface ) );
                j.WriteValue( ui );
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
                Penumbra.Log.Verbose( "Active Collections saved." );
            }
            catch( Exception e )
            {
                Penumbra.Log.Error( $"Could not save active collections to file {file}:\n{e}" );
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
                    Penumbra.Log.Error( $"Could not read active collections from file {file}:\n{e}" );
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

        // Cache handling. Usually recreate caches on the next framework tick,
        // but at launch create all of them at once.
        public void CreateNecessaryCaches()
        {
            var tasks = _specialCollections.OfType< ModCollection >()
               .Concat( _characters.Values )
               .Prepend( Current )
               .Prepend( Default )
               .Prepend( Interface )
               .Distinct()
               .Select( c => Task.Run( c.CalculateEffectiveFileListInternal ) )
               .ToArray();

            Task.WaitAll( tasks );
        }

        private void RemoveCache( int idx )
        {
            if( idx != Empty.Index
            && idx  != Default.Index
            && idx  != Interface.Index
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