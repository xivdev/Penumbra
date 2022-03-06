using System.Collections.Generic;
using System.Linq;

namespace Penumbra.Mod;

// Contains settings with the option selections stored by names instead of index.
// This is meant to make them possibly more portable when we support importing collections from other users.
// Enabled does not exist, because disabled mods would not be exported in this way.
public class NamedModSettings
{
    public int Priority { get; set; }
    public Dictionary< string, HashSet< string > > Settings { get; set; } = new();

    public void AddFromModSetting( ModSettings s, ModMeta meta )
    {
        Priority = s.Priority;
        Settings = s.Settings.Keys.ToDictionary( k => k, _ => new HashSet< string >() );

        foreach( var kvp in Settings )
        {
            if( !meta.Groups.TryGetValue( kvp.Key, out var info ) )
            {
                continue;
            }

            var setting = s.Settings[ kvp.Key ];
            if( info.SelectionType == SelectType.Single )
            {
                var name = setting < info.Options.Count
                    ? info.Options[ setting ].OptionName
                    : info.Options[ 0 ].OptionName;
                kvp.Value.Add( name );
            }
            else
            {
                for( var i = 0; i < info.Options.Count; ++i )
                {
                    if( ( ( setting >> i ) & 1 ) != 0 )
                    {
                        kvp.Value.Add( info.Options[ i ].OptionName );
                    }
                }
            }
        }
    }
}