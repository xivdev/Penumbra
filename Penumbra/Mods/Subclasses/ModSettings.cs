using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using OtterGui;
using OtterGui.Filesystem;
using Penumbra.Api.Enums;
using Penumbra.Meta.Manipulations;
using Penumbra.String.Classes;

namespace Penumbra.Mods;

/// <summary> Contains the settings for a given mod. </summary>
public class ModSettings
{
    public static readonly ModSettings Empty = new();
    public List< uint > Settings { get; private init; } = new();
    public int Priority { get; set; }
    public bool Enabled { get; set; }

    // Create an independent copy of the current settings.
    public ModSettings DeepCopy()
        => new()
        {
            Enabled  = Enabled,
            Priority = Priority,
            Settings = Settings.ToList(),
        };

    // Create default settings for a given mod.
    public static ModSettings DefaultSettings( Mod mod )
        => new()
        {
            Enabled  = false,
            Priority = 0,
            Settings = mod.Groups.Select( g => g.DefaultSettings ).ToList(),
        };

    // Return everything required to resolve things for a single mod with given settings (which can be null, in which case the default is used.
    public static (Dictionary< Utf8GamePath, FullPath >, HashSet< MetaManipulation >) GetResolveData( Mod mod, ModSettings? settings )
    {
        if( settings == null )
        {
            settings = DefaultSettings( mod );
        }
        else
        {
            settings.AddMissingSettings( mod );
        }

        var dict = new Dictionary< Utf8GamePath, FullPath >();
        var set  = new HashSet< MetaManipulation >();

        void AddOption( ISubMod option )
        {
            foreach( var (path, file) in option.Files.Concat( option.FileSwaps ) )
            {
                dict.TryAdd( path, file );
            }

            foreach( var manip in option.Manipulations )
            {
                set.Add( manip );
            }
        }

        foreach( var (group, index) in mod.Groups.WithIndex().OrderByDescending( g => g.Value.Priority ) )
        {
            if( group.Type is GroupType.Single )
            {
                AddOption( group[ ( int )settings.Settings[ index ] ] );
            }
            else
            {
                foreach( var (option, optionIdx) in group.WithIndex().OrderByDescending( o => group.OptionPriority( o.Index ) ) )
                {
                    if( ( ( settings.Settings[ index ] >> optionIdx ) & 1 ) == 1 )
                    {
                        AddOption( option );
                    }
                }
            }
        }

        AddOption( mod.Default );
        return ( dict, set );
    }

    // Automatically react to changes in a mods available options.
    public bool HandleChanges( ModOptionChangeType type, Mod mod, int groupIdx, int optionIdx, int movedToIdx )
    {
        switch( type )
        {
            case ModOptionChangeType.GroupRenamed: return true;
            case ModOptionChangeType.GroupAdded:
                // Add new empty setting for new mod.
                Settings.Insert( groupIdx, mod.Groups[ groupIdx ].DefaultSettings );
                return true;
            case ModOptionChangeType.GroupDeleted:
                // Remove setting for deleted mod.
                Settings.RemoveAt( groupIdx );
                return true;
            case ModOptionChangeType.GroupTypeChanged:
            {
                // Fix settings for a changed group type.
                // Single -> Multi: set single as enabled, rest as disabled
                // Multi -> Single: set the first enabled option or 0.
                var group  = mod.Groups[ groupIdx ];
                var config = Settings[ groupIdx ];
                Settings[ groupIdx ] = group.Type switch
                {
                    GroupType.Single => ( uint )Math.Max( Math.Min( group.Count - 1, BitOperations.TrailingZeroCount( config ) ), 0 ),
                    GroupType.Multi  => 1u << ( int )config,
                    _                => config,
                };
                return config != Settings[ groupIdx ];
            }
            case ModOptionChangeType.OptionDeleted:
            {
                // Single -> select the previous option if any.
                // Multi -> excise the corresponding bit.
                var group  = mod.Groups[ groupIdx ];
                var config = Settings[ groupIdx ];
                Settings[ groupIdx ] = group.Type switch
                {
                    GroupType.Single => config >= optionIdx ? config > 1 ? config - 1 : 0 : config,
                    GroupType.Multi  => Functions.RemoveBit( config, optionIdx ),
                    _                => config,
                };
                return config != Settings[ groupIdx ];
            }
            case ModOptionChangeType.GroupMoved:
                // Move the group the same way.
                return Settings.Move( groupIdx, movedToIdx );
            case ModOptionChangeType.OptionMoved:
            {
                // Single -> select the moved option if it was currently selected
                // Multi -> move the corresponding bit
                var group  = mod.Groups[ groupIdx ];
                var config = Settings[ groupIdx ];
                Settings[ groupIdx ] = group.Type switch
                {
                    GroupType.Single => config == optionIdx ? ( uint )movedToIdx : config,
                    GroupType.Multi  => Functions.MoveBit( config, optionIdx, movedToIdx ),
                    _                => config,
                };
                return config != Settings[ groupIdx ];
            }
            default: return false;
        }
    }

    // Ensure that a value is valid for a group.
    private static uint FixSetting( IModGroup group, uint value )
        => group.Type switch
        {
            GroupType.Single => ( uint )Math.Min( value, group.Count       - 1 ),
            GroupType.Multi  => ( uint )( value & ( ( 1ul << group.Count ) - 1 ) ),
            _                => value,
        };

    // Set a setting. Ensures that there are enough settings and fixes the setting beforehand.
    public void SetValue( Mod mod, int groupIdx, uint newValue )
    {
        AddMissingSettings( mod );
        var group = mod.Groups[ groupIdx ];
        Settings[ groupIdx ] = FixSetting( group, newValue );
    }

    // Add defaulted settings up to the required count.
    private bool AddMissingSettings( Mod mod )
    {
        var changes = false;
        for( var i = Settings.Count; i < mod.Groups.Count; ++i )
        {
            Settings.Add( mod.Groups[ i ].DefaultSettings );
            changes = true;
        }

        return changes;
    }

    // A simple struct conversion to easily save settings by name instead of value.
    public struct SavedSettings
    {
        public Dictionary< string, long > Settings;
        public int                        Priority;
        public bool                       Enabled;

        public SavedSettings DeepCopy()
            => new()
            {
                Enabled  = Enabled,
                Priority = Priority,
                Settings = Settings.ToDictionary( kvp => kvp.Key, kvp => kvp.Value ),
            };

        public SavedSettings( ModSettings settings, Mod mod )
        {
            Priority = settings.Priority;
            Enabled  = settings.Enabled;
            Settings = new Dictionary< string, long >( mod.Groups.Count );
            settings.AddMissingSettings( mod );

            foreach( var (group, setting) in mod.Groups.Zip( settings.Settings ) )
            {
                Settings.Add( group.Name, setting );
            }
        }

        // Convert and fix.
        public bool ToSettings( Mod mod, out ModSettings settings )
        {
            var list    = new List< uint >( mod.Groups.Count );
            var changes = Settings.Count != mod.Groups.Count;
            foreach( var group in mod.Groups )
            {
                if( Settings.TryGetValue( group.Name, out var config ) )
                {
                    var castConfig   = ( uint )Math.Clamp( config, 0, uint.MaxValue );
                    var actualConfig = FixSetting( group, castConfig );
                    list.Add( actualConfig );
                    if( actualConfig != config )
                    {
                        changes = true;
                    }
                }
                else
                {
                    list.Add( 0 );
                    changes = true;
                }
            }

            settings = new ModSettings
            {
                Enabled  = Enabled,
                Priority = Priority,
                Settings = list,
            };

            return changes;
        }
    }

    // Return the settings for a given mod in a shareable format, using the names of groups and options instead of indices.
    // Does not repair settings but ignores settings not fitting to the given mod.
    public (bool Enabled, int Priority, Dictionary< string, IList< string > > Settings) ConvertToShareable( Mod mod )
    {
        var dict = new Dictionary< string, IList< string > >( Settings.Count );
        foreach( var (setting, idx) in Settings.WithIndex() )
        {
            if( idx >= mod.Groups.Count )
            {
                break;
            }

            var group = mod.Groups[ idx ];
            if( group.Type == GroupType.Single && setting < group.Count )
            {
                dict.Add( group.Name, new[] { group[ ( int )setting ].Name } );
            }
            else
            {
                var list = group.Where( ( _, optionIdx ) => ( setting & ( 1 << optionIdx ) ) != 0 ).Select( o => o.Name ).ToList();
                dict.Add( group.Name, list );
            }
        }

        return ( Enabled, Priority, dict );
    }
}