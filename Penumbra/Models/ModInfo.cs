using Newtonsoft.Json;
using Penumbra.Mods;

namespace Penumbra.Models
{
    public class ModInfo : ModSettings
    {
        public ModInfo( ResourceMod mod )
            => Mod = mod;

        public string FolderName { get; set; } = "";
        public bool Enabled { get; set; }

        [JsonIgnore]
        public ResourceMod Mod { get; set; }

        public bool FixSpecificSetting( string name )
            => FixSpecificSetting( Mod.Meta, name );

        public bool FixInvalidSettings()
            => FixInvalidSettings( Mod.Meta );
    }
}