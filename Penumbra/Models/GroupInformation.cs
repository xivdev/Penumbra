using System.Collections.Generic;

namespace Penumbra.Models
{
    public struct Option {
        public string OptionName;
        public string OptionDesc;
        public Dictionary<string, string> OptionFiles;
    }
    public struct InstallerInfo {
        public string GroupName;
        public string SelectionType;
        public List<Option> Options;
    }
}