using System.Collections.Generic;
using Newtonsoft.Json;

namespace Penumbra.Models
{
    public class ModMeta
    {
        public uint FileVersion { get; set; }
        public string Name { get; set; }
        public string Author { get; set; }
        public string Description { get; set; }

        public string Version { get; set; }

        public string Website { get; set; }

        public List< string > ChangedItems { get; set; } = new();

        public Dictionary< string, string > FileSwaps { get; } = new();

        public Dictionary<string, InstallerInfo> Groups { get; set; } = new();

        [JsonIgnore]
        public bool HasGroupWithConfig { get; set; } = false;
    }
}