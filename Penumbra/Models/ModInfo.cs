using Newtonsoft.Json;
using Penumbra.Mods;

namespace Penumbra.Models
{
    public class ModInfo
    {
        public string FolderName { get; set; }
        public bool Enabled      { get; set; }
        public int Priority      { get; set; }
        public int CurrentTop    { get; set; } = 0;
        public int CurrentBottom { get; set; } = 0;
        public int CurrentGroup  { get; set; } = 0;

        [JsonIgnore]
        public ResourceMod Mod { get; set; }
    }
}