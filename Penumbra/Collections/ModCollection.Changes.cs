using Penumbra.Mods;
using System;
using System.Collections.Generic;
using System.Linq;
using Penumbra.Api.Enums;

namespace Penumbra.Collections;

public partial class ModCollection
{
    // If the change type is a bool, oldValue will be 1 for true and 0 for false.
    // optionName will only be set for type == Setting.
    public delegate void ModSettingChangeDelegate( ModSettingChange type, int modIdx, int oldValue, int groupIdx, bool inherited );
    public event ModSettingChangeDelegate ModSettingChanged;

    // Enable or disable the mod inheritance of mod idx.
    public bool SetModInheritance( int idx, bool inherit )
    {
        if (!FixInheritance(idx, inherit))
            return false;

        ModSettingChanged.Invoke( ModSettingChange.Inheritance, idx, inherit ? 0 : 1, 0, false );
        return true;

    }

    // Set the enabled state mod idx to newValue if it differs from the current enabled state.
    // If mod idx is currently inherited, stop the inheritance.
    public bool SetModState( int idx, bool newValue )
    {
        var oldValue = _settings[ idx ]?.Enabled ?? this[ idx ].Settings?.Enabled ?? false;
        if (newValue == oldValue)
            return false;

        var inheritance = FixInheritance( idx, false );
        _settings[ idx ]!.Enabled = newValue;
        ModSettingChanged.Invoke( ModSettingChange.EnableState, idx, inheritance ? -1 : newValue ? 0 : 1, 0, false );
        return true;

    }

    // Enable or disable the mod inheritance of every mod in mods.
    public void SetMultipleModInheritances( IEnumerable< Mod > mods, bool inherit )
    {
        if( mods.Aggregate( false, ( current, mod ) => current | FixInheritance( mod.Index, inherit ) ) )
            ModSettingChanged.Invoke( ModSettingChange.MultiInheritance, -1, -1, 0, false );
    }

    // Set the enabled state of every mod in mods to the new value.
    // If the mod is currently inherited, stop the inheritance.
    public void SetMultipleModStates( IEnumerable< Mod > mods, bool newValue )
    {
        var changes = false;
        foreach( var mod in mods )
        {
            var oldValue = _settings[ mod.Index ]?.Enabled;
            if (newValue == oldValue)
                continue;

            FixInheritance( mod.Index, false );
            _settings[ mod.Index ]!.Enabled = newValue;
            changes                         = true;
        }

        if( changes )
        {
            ModSettingChanged.Invoke( ModSettingChange.MultiEnableState, -1, -1, 0, false );
        }
    }

    // Set the priority of mod idx to newValue if it differs from the current priority.
    // If mod idx is currently inherited, stop the inheritance.
    public bool SetModPriority( int idx, int newValue )
    {
        var oldValue = _settings[ idx ]?.Priority ?? this[ idx ].Settings?.Priority ?? 0;
        if (newValue == oldValue)
            return false;

        var inheritance = FixInheritance( idx, false );
        _settings[ idx ]!.Priority = newValue;
        ModSettingChanged.Invoke( ModSettingChange.Priority, idx, inheritance ? -1 : oldValue, 0, false );
        return true;

    }

    // Set a given setting group settingName of mod idx to newValue if it differs from the current value and fix it if necessary.
    // If mod idx is currently inherited, stop the inheritance.
    public bool SetModSetting( int idx, int groupIdx, uint newValue )
    {
        var settings = _settings[ idx ] != null ? _settings[ idx ]!.Settings : this[ idx ].Settings?.Settings;
        var oldValue = settings?[ groupIdx ] ?? Penumbra.ModManager[idx].Groups[groupIdx].DefaultSettings;
        if (oldValue == newValue)
            return false;

        var inheritance = FixInheritance( idx, false );
        _settings[ idx ]!.SetValue( Penumbra.ModManager[ idx ], groupIdx, newValue );
        ModSettingChanged.Invoke( ModSettingChange.Setting, idx, inheritance ? -1 : ( int )oldValue, groupIdx, false );
        return true;

    }

    // Change one of the available mod settings for mod idx discerned by type.
    // If type == Setting, settingName should be a valid setting for that mod, otherwise it will be ignored.
    // The setting will also be automatically fixed if it is invalid for that setting group.
    // For boolean parameters, newValue == 0 will be treated as false and != 0 as true.
    public bool ChangeModSetting( ModSettingChange type, int idx, int newValue, int groupIdx )
    {
        return type switch
        {
            ModSettingChange.Inheritance => SetModInheritance( idx, newValue != 0 ),
            ModSettingChange.EnableState => SetModState( idx, newValue       != 0 ),
            ModSettingChange.Priority    => SetModPriority( idx, newValue ),
            ModSettingChange.Setting     => SetModSetting( idx, groupIdx, ( uint )newValue ),
            _                            => throw new ArgumentOutOfRangeException( nameof( type ), type, null ),
        };
    }

    // Set inheritance of a mod without saving,
    // to be used as an intermediary.
    private bool FixInheritance( int idx, bool inherit )
    {
        var settings = _settings[ idx ];
        if( inherit == ( settings == null ) )
            return false;

        _settings[ idx ] = inherit ? null : this[ idx ].Settings?.DeepCopy() ?? ModSettings.DefaultSettings( Penumbra.ModManager[ idx ] );
        return true;
    }

    private void SaveOnChange( ModSettingChange _1, int _2, int _3, int _4, bool inherited )
        => SaveOnChange( inherited );

    private void SaveOnChange( bool inherited )
    {
        if( !inherited )
            Penumbra.SaveService.QueueSave(this);
    }
}