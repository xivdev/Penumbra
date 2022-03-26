using System;
using Penumbra.Mod;

namespace Penumbra.Collections;

public enum ModSettingChange
{
    Inheritance,
    EnableState,
    Priority,
    Setting,
}

public partial class ModCollection
{
    public delegate void ModSettingChangeDelegate( ModSettingChange type, int modIdx, int oldValue, string? optionName, bool inherited );
    public event ModSettingChangeDelegate ModSettingChanged;

    // Enable or disable the mod inheritance of mod idx.
    public void SetModInheritance( int idx, bool inherit )
    {
        if( FixInheritance( idx, inherit ) )
        {
            ModSettingChanged.Invoke( ModSettingChange.Inheritance, idx, inherit ? 0 : 1, null, false );
        }
    }

    // Set the enabled state mod idx to newValue if it differs from the current priority.
    // If mod idx is currently inherited, stop the inheritance.
    public void SetModState( int idx, bool newValue )
    {
        var oldValue = _settings[ idx ]?.Enabled ?? this[ idx ].Settings?.Enabled ?? false;
        if( newValue != oldValue )
        {
            var inheritance = FixInheritance( idx, false );
            _settings[ idx ]!.Enabled = newValue;
            ModSettingChanged.Invoke( ModSettingChange.EnableState, idx, inheritance ? -1 : newValue ? 0 : 1, null, false );
        }
    }

    // Set the priority of mod idx to newValue if it differs from the current priority.
    // If mod idx is currently inherited, stop the inheritance.
    public void SetModPriority( int idx, int newValue )
    {
        var oldValue = _settings[ idx ]?.Priority ?? this[ idx ].Settings?.Priority ?? 0;
        if( newValue != oldValue )
        {
            var inheritance = FixInheritance( idx, false );
            _settings[ idx ]!.Priority = newValue;
            ModSettingChanged.Invoke( ModSettingChange.Priority, idx, inheritance ? -1 : oldValue, null, false );
        }
    }

    // Set a given setting group settingName of mod idx to newValue if it differs from the current value and fix it if necessary.
    // If mod idx is currently inherited, stop the inheritance.
    public void SetModSetting( int idx, string settingName, int newValue )
    {
        var settings = _settings[ idx ] != null ? _settings[ idx ]!.Settings : this[ idx ].Settings?.Settings;
        var oldValue = settings != null
            ? settings.TryGetValue( settingName, out var v ) ? v : newValue
            : Penumbra.ModManager.Mods[ idx ].Meta.Groups.ContainsKey( settingName )
                ? 0
                : newValue;
        if( oldValue != newValue )
        {
            var inheritance = FixInheritance( idx, false );
            _settings[ idx ]!.Settings[ settingName ] = newValue;
            _settings[ idx ]!.FixSpecificSetting( settingName, Penumbra.ModManager.Mods[ idx ].Meta );
            ModSettingChanged.Invoke( ModSettingChange.Setting, idx, inheritance ? -1 : oldValue, settingName, false );
        }
    }

    // Change one of the available mod settings for mod idx discerned by type.
    // If type == Setting, settingName should be a valid setting for that mod, otherwise it will be ignored.
    // The setting will also be automatically fixed if it is invalid for that setting group.
    // For boolean parameters, newValue == 0 will be treated as false and != 0 as true.
    public void ChangeModSetting( ModSettingChange type, int idx, int newValue, string? settingName = null )
    {
        switch( type )
        {
            case ModSettingChange.Inheritance:
                SetModInheritance( idx, newValue != 0 );
                break;
            case ModSettingChange.EnableState:
                SetModState( idx, newValue != 0 );
                break;
            case ModSettingChange.Priority:
                SetModPriority( idx, newValue );
                break;
            case ModSettingChange.Setting:
                SetModSetting( idx, settingName ?? string.Empty, newValue );
                break;
            default: throw new ArgumentOutOfRangeException( nameof( type ), type, null );
        }
    }

    // Set inheritance of a mod without saving,
    // to be used as an intermediary.
    private bool FixInheritance( int idx, bool inherit )
    {
        var settings = _settings[ idx ];
        if( inherit != ( settings == null ) )
        {
            _settings[ idx ] = inherit ? null : this[ idx ].Settings ?? ModSettings.DefaultSettings( Penumbra.ModManager.Mods[ idx ].Meta );
            return true;
        }

        return false;
    }

    private void SaveOnChange( ModSettingChange _1, int _2, int _3, string? _4, bool inherited )
        => SaveOnChange( inherited );

    private void SaveOnChange( bool inherited )
    {
        if( !inherited )
        {
            Save();
        }
    }
}