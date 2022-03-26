using System;
using Penumbra.Mods;

namespace Penumbra.Mod;

public partial class Mod
{
    public struct SortOrder : IComparable< SortOrder >
    {
        public ModFolder ParentFolder { get; set; }

        private string _sortOrderName;

        public string SortOrderName
        {
            get => _sortOrderName;
            set => _sortOrderName = value.Replace( '/', '\\' );
        }

        public string SortOrderPath
            => ParentFolder.FullName;

        public string FullName
        {
            get
            {
                var path = SortOrderPath;
                return path.Length > 0 ? $"{path}/{SortOrderName}" : SortOrderName;
            }
        }


        public SortOrder( ModFolder parentFolder, string name )
        {
            ParentFolder   = parentFolder;
            _sortOrderName = name.Replace( '/', '\\' );
        }

        public string FullPath
            => SortOrderPath.Length > 0 ? $"{SortOrderPath}/{SortOrderName}" : SortOrderName;

        public int CompareTo( SortOrder other )
            => string.Compare( FullPath, other.FullPath, StringComparison.InvariantCultureIgnoreCase );
    }
}