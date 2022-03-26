using System.Linq;
using Penumbra.Mod;

namespace Penumbra.Collections;

public sealed partial class ModCollection
{
    private static class Migration
    {
        public static void Migrate( ModCollection collection )
        {
            var changes = MigrateV0ToV1( collection );
            if( changes )
            {
                collection.Save();
            }
        }

        private static bool MigrateV0ToV1( ModCollection collection )
        {
            if( collection.Version > 0 )
            {
                return false;
            }

            collection.Version = 1;
            for( var i = 0; i < collection._settings.Count; ++i )
            {
                var setting = collection._settings[ i ];
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

        private static bool SettingIsDefaultV0( ModSettings? setting )
            => setting is { Enabled: false, Priority: 0 } && setting.Settings.Values.All( s => s == 0 );
    }
}