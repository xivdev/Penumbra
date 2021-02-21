using System.Collections.Generic;
using Newtonsoft.Json;
using Penumbra.Mods;

namespace Penumbra.Models
{
    public class ModInfo
    {
        public ModInfo( ResourceMod mod )
            => Mod = mod;

        public string FolderName { get; set; } = "";
        public bool Enabled { get; set; }
        public int Priority { get; set; }
        public Dictionary< string, int > Conf { get; set; } = new();

        [JsonIgnore]
        public ResourceMod Mod { get; set; }
    }
}