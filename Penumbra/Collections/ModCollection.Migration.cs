using Penumbra.Mods;
using System.Collections.Generic;
using System.Linq;
using Penumbra.Services;
using Penumbra.Util;

namespace Penumbra.Collections;

public sealed partial class ModCollection
{
    // Migration to convert ModCollections from older versions to newer.
    private static class Migration
    {
        public static void Migrate(SaveService saver, ModCollection collection )
        {
            var changes = MigrateV0ToV1( collection );
            if( changes )
            {
                saver.ImmediateSave(collection);
            }
        }

        private static bool MigrateV0ToV1( ModCollection collection )
        {
            if( collection.Version > 0 )
            {
                return false;
            }

            collection.Version = 1;

            // Remove all completely defaulted settings from active and inactive mods.
            for( var i = 0; i < collection._settings.Count; ++i )
            {
                if( SettingIsDefaultV0( collection._settings[ i ] ) )
                {
                    collection._settings[ i ] = null;
                }
            }

            foreach( var (key, _) in collection._unusedSettings.Where( kvp => SettingIsDefaultV0( kvp.Value ) ).ToList() )
            {
                collection._unusedSettings.Remove( key );
            }

            return true;
        }

        // We treat every completely defaulted setting as inheritance-ready.
        private static bool SettingIsDefaultV0( ModSettings.SavedSettings setting )
            => setting is { Enabled: false, Priority: 0 } && setting.Settings.Values.All( s => s == 0 );

        private static bool SettingIsDefaultV0( ModSettings? setting )
            => setting is { Enabled: false, Priority: 0 } && setting.Settings.All( s => s == 0 );
    }

    internal static ModCollection MigrateFromV0( string name, Dictionary< string, ModSettings.SavedSettings > allSettings )
        => new(name, 0, allSettings);
}