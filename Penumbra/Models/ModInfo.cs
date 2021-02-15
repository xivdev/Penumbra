using System.Collections.Generic;
using Newtonsoft.Json;
using Penumbra.Mods;

namespace Penumbra.Models
{
    public class ModInfo
    {
        public string FolderName { get; set; }
        public bool Enabled      { get; set; }
        public int Priority      { get; set; }
        public Dictionary<string, int> Conf {get;set;}

        [JsonIgnore]
        public ResourceMod Mod { get; set; }
    }
}