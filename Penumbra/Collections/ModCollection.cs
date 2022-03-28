using System.Collections.Generic;
using System.Linq;
using Penumbra.Mods;

namespace Penumbra.Collections;

public partial class ModCollection
{
    // Create the always available Empty Collection that will always sit at index 0,
    // can not be deleted and does never create a cache.
    private static ModCollection CreateEmpty()
    {
        var collection = CreateNewEmpty( EmptyCollection );
        collection.Index = 0;
        collection._settings.Clear();
        return collection;
    }
}

// A ModCollection is a named set of ModSettings to all of the users' installed mods.
// Settings to mods that are not installed anymore are kept as long as no call to CleanUnavailableSettings is made.
// Invariants:
//    - Index is the collections index in the ModCollection.Manager
//    - Settings has the same size as ModManager.Mods.
//    - any change in settings or inheritance of the collection causes a Save.
public partial class ModCollection
{
    public const int    CurrentVersion    = 1;
    public const string DefaultCollection = "Default";
    public const string EmptyCollection   = "None";

    public static readonly ModCollection Empty = CreateEmpty();

    // The collection name can contain invalid path characters,
    // but after removing those and going to lower case it has to be unique.
    public string Name { get; private init; }
    public int Version { get; private set; }
    public int Index { get; private set; } = -1;

    // If a ModSetting is null, it can be inherited from other collections.
    // If no collection provides a setting for the mod, it is just disabled.
    private readonly List< ModSettings? > _settings;

    public IReadOnlyList< ModSettings? > Settings
        => _settings;

    // Evaluates the settings along the whole inheritance tree.
    public IEnumerable< ModSettings? > ActualSettings
        => Enumerable.Range( 0, _settings.Count ).Select( i => this[ i ].Settings );

    // Settings for deleted mods will be kept via directory name.
    private readonly Dictionary< string, ModSettings > _unusedSettings;


    // Constructor for duplication.
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

    // Constructor for reading from files.
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

    // Create a new, unique empty collection of a given name.
    public static ModCollection CreateNewEmpty( string name )
        => new(name, CurrentVersion, new Dictionary< string, ModSettings >());

    // Duplicate the calling collection to a new, unique collection of a given name.
    public ModCollection Duplicate( string name )
        => new(name, this);

    // Remove all settings for not currently-installed mods.
    public void CleanUnavailableSettings()
    {
        var any = _unusedSettings.Count > 0;
        _unusedSettings.Clear();
        if( any )
        {
            Save();
        }
    }

    // Add settings for a new appended mod, by checking if the mod had settings from a previous deletion.
    private void AddMod( Mods.Mod mod )
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

    // Move settings from the current mod list to the unused mod settings.
    private void RemoveMod( Mods.Mod mod, int idx )
    {
        var settings = _settings[ idx ];
        if( settings != null )
        {
            _unusedSettings.Add( mod.BasePath.Name, settings );
        }

        _settings.RemoveAt( idx );
    }
}