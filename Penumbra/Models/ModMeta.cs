using System.Collections.Generic;

namespace Penumbra.Models
{
    public class ModMeta
    {
        public string Name { get; set; }
        public string Author { get; set; }
        public string Description { get; set; }
        
        public string Version { get; set; }
        
        public string Website { get; set; }

        public Dictionary< string, string > FileSwaps { get; } = new();

        public List<string> ChangedItems { get; set; } = new();
        
    }
}