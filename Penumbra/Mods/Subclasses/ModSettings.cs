using System;
using System.Collections.Generic;
using System.Linq;

namespace Penumbra.Mods;


// Contains the settings for a given mod.
public class ModSettings2
{
    public static readonly ModSettings2 Empty = new();
    public List< uint > Settings { get; init; } = new();
    public int Priority { get; set; }
    public bool Enabled { get; set; }

    public ModSettings2 DeepCopy()
        => new()
        {
            Enabled  = Enabled,
            Priority = Priority,
            Settings = Settings.ToList(),
        };

    public static ModSettings2 DefaultSettings( Mod2 mod )
        => new()
        {
            Enabled  = false,
            Priority = 0,
            Settings = Enumerable.Repeat( 0u, mod.Groups.Count ).ToList(),
        };



    public void HandleChanges( ModOptionChangeType type, Mod2 mod, int groupIdx, int optionIdx )
    {
        switch( type )
        {
            case ModOptionChangeType.GroupAdded:
                Settings.Insert( groupIdx, 0 );
                break;
            case ModOptionChangeType.GroupDeleted:
                Settings.RemoveAt( groupIdx );
                break;
            case ModOptionChangeType.OptionDeleted:
                var group  = mod.Groups[ groupIdx ];
                var config = Settings[ groupIdx ];
                Settings[ groupIdx ] = group.Type switch
                {
                    SelectType.Single => config >= optionIdx ? Math.Max( 0, config - 1 ) : config,
                    SelectType.Multi  => RemoveBit( config, optionIdx ),
                    _                 => config,
                };
                break;
        }
    }

    public void SetValue( Mod2 mod, int groupIdx, uint newValue )
    {
        AddMissingSettings( groupIdx + 1 );
        var group = mod.Groups[ groupIdx ];
        Settings[ groupIdx ] = group.Type switch
        {
            SelectType.Single => ( uint )Math.Max( newValue, group.Count ),
            SelectType.Multi  => ( ( 1u << group.Count ) - 1 ) & newValue,
            _                 => newValue,
        };
    }

    private static uint RemoveBit( uint config, int bit )
    {
        var lowMask  = ( 1u << bit ) - 1u;
        var highMask = ~( ( 1u << ( bit + 1 ) ) - 1u );
        var low      = config & lowMask;
        var high     = ( config & highMask ) >> 1;
        return low | high;
    }

    internal bool AddMissingSettings( int totalCount )
    {
        if( totalCount <= Settings.Count )
        {
            return false;
        }

        Settings.AddRange( Enumerable.Repeat( 0u, totalCount - Settings.Count ) );
        return true;
    }

    public struct SavedSettings
    {
        public Dictionary< string, uint > Settings;
        public int                        Priority;
        public bool                       Enabled;

        public SavedSettings DeepCopy()
            => new()
            {
                Enabled  = Enabled,
                Priority = Priority,
                Settings = Settings.ToDictionary( kvp => kvp.Key, kvp => kvp.Value ),
            };

        public SavedSettings( ModSettings2 settings, Mod2 mod )
        {
            Priority = settings.Priority;
            Enabled  = settings.Enabled;
            Settings = new Dictionary< string, uint >( mod.Groups.Count );
            settings.AddMissingSettings( mod.Groups.Count );

            foreach( var (group, setting) in mod.Groups.Zip( settings.Settings ) )
            {
                Settings.Add( group.Name, setting );
            }
        }

        public bool ToSettings( Mod2 mod, out ModSettings2 settings )
        {
            var list    = new List< uint >( mod.Groups.Count );
            var changes = Settings.Count != mod.Groups.Count;
            foreach( var group in mod.Groups )
            {
                if( Settings.TryGetValue( group.Name, out var config ) )
                {
                    list.Add( config );
                }
                else
                {
                    list.Add( 0 );
                    changes = true;
                }
            }

            settings = new ModSettings2
            {
                Enabled  = Enabled,
                Priority = Priority,
                Settings = list,
            };

            return changes;
        }
    }
}