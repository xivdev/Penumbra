using System.Collections.Generic;

namespace Penumbra.Importer.Models
{
    internal class OptionList
    {
        public string Name { get; set; }
        public object Description { get; set; }
        public string ImagePath { get; set; }
        public List< SimpleMod > ModsJsons { get; set; }
        public string GroupName { get; set; }
        public string SelectionType { get; set; }
        public bool IsChecked { get; set; }
    }

    internal class ModGroup
    {
        public string GroupName { get; set; }
        public string SelectionType { get; set; }
        public List< OptionList > OptionList { get; set; }
    }

    internal class ModPackPage
    {
        public int PageIndex { get; set; }
        public List< ModGroup > ModGroups { get; set; }
    }

    internal class ExtendedModPack
    {
        public string TTMPVersion { get; set; }
        public string Name { get; set; }
        public string Author { get; set; }
        public string Version { get; set; }
        public string Description { get; set; }
        public List< ModPackPage > ModPackPages { get; set; }
        public List< SimpleMod > SimpleModsList { get; set; }
    }
}