using System.Collections.Generic;

namespace Penumbra.Importer.Models
{
    internal class SimpleModPack
    {
        public string TTMPVersion { get; set; }
        public string Name { get; set; }
        public string Author { get; set; }
        public string Version { get; set; }
        public string Description { get; set; }
        public List< SimpleMod > SimpleModsList { get; set; }
    }

    internal class SimpleMod
    {
        public string Name { get; set; }
        public string Category { get; set; }
        public string FullPath { get; set; }
        public long ModOffset { get; set; }
        public long ModSize { get; set; }
        public string DatFile { get; set; }
        public object ModPackEntry { get; set; }
    }
}