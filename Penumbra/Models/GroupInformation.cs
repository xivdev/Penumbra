using System.Collections.Generic;

namespace Penumbra.Models
{
    public enum SelectType {
        Single, Multi
    }
    public struct Option {
        public string OptionName;
        public string OptionDesc;
        public Dictionary<string, string> OptionFiles;
    }
    public struct InstallerInfo {
        public string GroupName;
        public SelectType SelectionType;
        public List<Option> Options;
    }
}