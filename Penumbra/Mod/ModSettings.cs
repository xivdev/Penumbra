using System;
using System.Collections.Generic;
using System.Linq;

namespace Penumbra.Mod;

// Contains the settings for a given mod.
public class ModSettings
{
    public bool Enabled { get; set; }
    public int Priority { get; set; }
    public Dictionary< string, int > Settings { get; set; } = new();

    // For backwards compatibility
    private Dictionary< string, int > Conf
    {
        set => Settings = value;
    }

    public ModSettings DeepCopy()
    {
        var settings = new ModSettings
        {
            Enabled  = Enabled,
            Priority = Priority,
            Settings = Settings.ToDictionary( kvp => kvp.Key, kvp => kvp.Value ),
        };
        return settings;
    }

    public static ModSettings DefaultSettings( ModMeta meta )
    {
        return new ModSettings
        {
            Enabled  = false,
            Priority = 0,
            Settings = meta.Groups.ToDictionary( kvp => kvp.Key, _ => 0 ),
        };
    }

    public bool FixSpecificSetting( string name, ModMeta meta )
    {
        if( !meta.Groups.TryGetValue( name, out var group ) )
        {
            return Settings.Remove( name );
        }

        if( Settings.TryGetValue( name, out var oldSetting ) )
        {
            Settings[ name ] = group.SelectionType switch
            {
                SelectType.Single => Math.Min( Math.Max( oldSetting, 0 ), group.Options.Count          - 1 ),
                SelectType.Multi  => Math.Min( Math.Max( oldSetting, 0 ), ( 1 << group.Options.Count ) - 1 ),
                _                 => Settings[ group.GroupName ],
            };
            return oldSetting != Settings[ group.GroupName ];
        }

        Settings[ name ] = 0;
        return true;
    }

    public bool FixInvalidSettings( ModMeta meta )
    {
        if( meta.Groups.Count == 0 )
        {
            return false;
        }

        return Settings.Keys.ToArray().Union( meta.Groups.Keys )
           .Aggregate( false, ( current, name ) => current | FixSpecificSetting( name, meta ) );
    }
}