using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using OtterGui.Filesystem;

namespace Penumbra.Mods;

// Contains the settings for a given mod.
public class ModSettings
{
    public static readonly ModSettings Empty = new();
    public List< uint > Settings { get; init; } = new();
    public int Priority { get; set; }
    public bool Enabled { get; set; }

    public ModSettings DeepCopy()
        => new()
        {
            Enabled  = Enabled,
            Priority = Priority,
            Settings = Settings.ToList(),
        };

    public static ModSettings DefaultSettings( Mod mod )
        => new()
        {
            Enabled  = false,
            Priority = 0,
            Settings = Enumerable.Repeat( 0u, mod.Groups.Count ).ToList(),
        };

    public bool HandleChanges( ModOptionChangeType type, Mod mod, int groupIdx, int optionIdx, int movedToIdx )
    {
        switch( type )
        {
            case ModOptionChangeType.GroupRenamed: return true;
            case ModOptionChangeType.GroupAdded:
                Settings.Insert( groupIdx, 0 );
                return true;
            case ModOptionChangeType.GroupDeleted:
                Settings.RemoveAt( groupIdx );
                return true;
            case ModOptionChangeType.GroupTypeChanged:
            {
                var group  = mod.Groups[ groupIdx ];
                var config = Settings[ groupIdx ];
                Settings[ groupIdx ] = group.Type switch
                {
                    SelectType.Single => ( uint )Math.Min( group.Count - 1, BitOperations.TrailingZeroCount( config ) ),
                    SelectType.Multi  => 1u << ( int )config,
                    _                 => config,
                };
                return config != Settings[ groupIdx ];
            }
            case ModOptionChangeType.OptionDeleted:
            {
                var group  = mod.Groups[ groupIdx ];
                var config = Settings[ groupIdx ];
                Settings[ groupIdx ] = group.Type switch
                {
                    SelectType.Single => config >= optionIdx ? Math.Max( 0, config - 1 ) : config,
                    SelectType.Multi  => RemoveBit( config, optionIdx ),
                    _                 => config,
                };
                return config != Settings[ groupIdx ];
            }
            case ModOptionChangeType.GroupMoved: return Settings.Move( groupIdx, movedToIdx );
            case ModOptionChangeType.OptionMoved:
            {
                var group  = mod.Groups[ groupIdx ];
                var config = Settings[ groupIdx ];
                Settings[ groupIdx ] = group.Type switch
                {
                    SelectType.Single => config == optionIdx ? ( uint )movedToIdx : config,
                    SelectType.Multi  => MoveBit( config, optionIdx, movedToIdx ),
                    _                 => config,
                };
                return config != Settings[ groupIdx ];
            }
            default: return false;
        }
    }

    private static uint FixSetting( IModGroup group, uint value )
        => group.Type switch
        {
            SelectType.Single => ( uint )Math.Min( value, group.Count     - 1 ),
            SelectType.Multi  => ( uint )( value & ( ( 1 << group.Count ) - 1 ) ),
            _                 => value,
        };

    public void SetValue( Mod mod, int groupIdx, uint newValue )
    {
        AddMissingSettings( groupIdx + 1 );
        var group = mod.Groups[ groupIdx ];
        Settings[ groupIdx ] = FixSetting( group, newValue );
    }

    private static uint RemoveBit( uint config, int bit )
    {
        var lowMask  = ( 1u << bit ) - 1u;
        var highMask = ~( ( 1u << ( bit + 1 ) ) - 1u );
        var low      = config & lowMask;
        var high     = ( config & highMask ) >> 1;
        return low | high;
    }

    private static uint MoveBit( uint config, int bit1, int bit2 )
    {
        var enabled = ( config & ( 1 << bit1 ) ) != 0 ? 1u << bit2 : 0u;
        config = RemoveBit( config, bit1 );
        var lowMask = ( 1u << bit2 ) - 1u;
        var low     = config & lowMask;
        var high    = ( config & ~lowMask ) << 1;
        return low | enabled | high;
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

        public SavedSettings( ModSettings settings, Mod mod )
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

        public bool ToSettings( Mod mod, out ModSettings settings )
        {
            var list    = new List< uint >( mod.Groups.Count );
            var changes = Settings.Count != mod.Groups.Count;
            foreach( var group in mod.Groups )
            {
                if( Settings.TryGetValue( group.Name, out var config ) )
                {
                    var actualConfig = FixSetting( group, config );
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
}