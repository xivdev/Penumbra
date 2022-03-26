using System;
using System.Collections.Generic;
using System.Linq;
using Penumbra.Mod;

namespace Penumbra.Mods
{
    public partial class ModFolder
    {
        public ModFolder? Parent;

        public string FullName
        {
            get
            {
                var parentPath = Parent?.FullName ?? string.Empty;
                return parentPath.Any() ? $"{parentPath}/{Name}" : Name;
            }
        }

        private string _name = string.Empty;

        public string Name
        {
            get => _name;
            set => _name = value.Replace( '/', '\\' );
        }

        public List< ModFolder > SubFolders { get; } = new();
        public List< Mod.Mod > Mods { get; } = new();

        public ModFolder( ModFolder parent, string name )
        {
            Parent = parent;
            Name   = name;
        }

        public override string ToString()
            => FullName;

        public int TotalDescendantMods()
            => Mods.Count + SubFolders.Sum( f => f.TotalDescendantMods() );

        public int TotalDescendantFolders()
            => SubFolders.Sum( f => f.TotalDescendantFolders() );

        // Return all descendant mods in the specified order.
        public IEnumerable< Mod.Mod > AllMods( bool foldersFirst )
        {
            if( foldersFirst )
            {
                return SubFolders.SelectMany( f => f.AllMods( foldersFirst ) ).Concat( Mods );
            }

            return GetSortedEnumerator().SelectMany( f =>
            {
                if( f is ModFolder folder )
                {
                    return folder.AllMods( false );
                }

                return new[] { ( Mod.Mod )f };
            } );
        }

        // Return all descendant subfolders.
        public IEnumerable< ModFolder > AllFolders()
            => SubFolders.SelectMany( f => f.AllFolders() ).Prepend( this );

        // Iterate through all descendants in the specified order, returning subfolders as well as mods.
        public IEnumerable< object > GetItems( bool foldersFirst )
            => foldersFirst ? SubFolders.Cast< object >().Concat( Mods ) : GetSortedEnumerator();

        // Find a subfolder by name. Returns true and sets folder to it if it exists.
        public bool FindSubFolder( string name, out ModFolder folder )
        {
            var subFolder = new ModFolder( this, name );
            var idx       = SubFolders.BinarySearch( subFolder, FolderComparer );
            folder = idx >= 0 ? SubFolders[ idx ] : this;
            return idx >= 0;
        }

        // Checks if an equivalent subfolder as folder already exists and returns its index.
        // If it does not exist, inserts folder as a subfolder and returns the new index.
        // Also sets this as folders parent.
        public int FindOrAddSubFolder( ModFolder folder )
        {
            var idx = SubFolders.BinarySearch( folder, FolderComparer );
            if( idx >= 0 )
            {
                return idx;
            }

            idx = ~idx;
            SubFolders.Insert( idx, folder );
            folder.Parent = this;
            return idx;
        }

        // Checks if a subfolder with the given name already exists and returns it and its index.
        // If it does not exists, creates and inserts it and returns the new subfolder and its index.
        public (ModFolder, int) FindOrCreateSubFolder( string name )
        {
            var subFolder = new ModFolder( this, name );
            var idx       = FindOrAddSubFolder( subFolder );
            return ( SubFolders[ idx ], idx );
        }

        // Remove folder as a subfolder if it exists.
        // If this folder is empty afterwards, remove it from its parent.
        public void RemoveSubFolder( ModFolder folder )
        {
            RemoveFolderIgnoreEmpty( folder );
            CheckEmpty();
        }

        // Add the given mod as a child, if it is not already a child.
        // Returns the index of the found or inserted mod.
        public int AddMod( Mod.Mod mod )
        {
            var idx = Mods.BinarySearch( mod, ModComparer );
            if( idx >= 0 )
            {
                return idx;
            }

            idx = ~idx;
            Mods.Insert( idx, mod );

            return idx;
        }

        // Remove mod as a child if it exists.
        // If this folder is empty afterwards, remove it from its parent.
        public void RemoveMod( Mod.Mod mod )
        {
            RemoveModIgnoreEmpty( mod );
            CheckEmpty();
        }
    }

    // Internals
    public partial class ModFolder
    {
        // Create a Root folder without parent.
        internal static ModFolder CreateRoot()
            => new( null!, string.Empty );

        internal class ModFolderComparer : IComparer< ModFolder >
        {
            public StringComparison CompareType = StringComparison.InvariantCultureIgnoreCase;

            // Compare only the direct folder names since this is only used inside an enumeration of subfolders of one folder.
            public int Compare( ModFolder? x, ModFolder? y )
                => ReferenceEquals( x, y )
                    ? 0
                    : string.Compare( x?.Name ?? string.Empty, y?.Name ?? string.Empty, CompareType );
        }

        internal class ModDataComparer : IComparer< Mod.Mod >
        {
            public StringComparison CompareType = StringComparison.InvariantCultureIgnoreCase;

            // Compare only the direct SortOrderNames since this is only used inside an enumeration of direct mod children of one folder.
            // Since mod SortOrderNames do not have to be unique inside a folder, also compare their BasePaths (and thus their identity) if necessary.
            public int Compare( Mod.Mod? x, Mod.Mod? y )
            {
                if( ReferenceEquals( x, y ) )
                {
                    return 0;
                }

                var cmp = string.Compare( x?.Order.SortOrderName, y?.Order.SortOrderName, CompareType );
                if( cmp != 0 )
                {
                    return cmp;
                }

                return string.Compare( x?.BasePath.Name, y?.BasePath.Name, StringComparison.InvariantCulture );
            }
        }

        internal static readonly ModFolderComparer FolderComparer = new();
        internal static readonly ModDataComparer   ModComparer    = new();

        // Get an enumerator for actually sorted objects instead of folder-first objects.
        private IEnumerable< object > GetSortedEnumerator()
        {
            var modIdx = 0;
            foreach( var folder in SubFolders )
            {
                var folderString = folder.Name;
                for( ; modIdx < Mods.Count; ++modIdx )
                {
                    var mod       = Mods[ modIdx ];
                    var modString = mod.Order.SortOrderName;
                    if( string.Compare( folderString, modString, StringComparison.InvariantCultureIgnoreCase ) > 0 )
                    {
                        yield return mod;
                    }
                    else
                    {
                        break;
                    }
                }

                yield return folder;
            }

            for( ; modIdx < Mods.Count; ++modIdx )
            {
                yield return Mods[ modIdx ];
            }
        }

        private void CheckEmpty()
        {
            if( Mods.Count == 0 && SubFolders.Count == 0 )
            {
                Parent?.RemoveSubFolder( this );
            }
        }

        // Remove a subfolder but do not remove this folder from its parent if it is empty afterwards.
        internal void RemoveFolderIgnoreEmpty( ModFolder folder )
        {
            var idx = SubFolders.BinarySearch( folder, FolderComparer );
            if( idx < 0 )
            {
                return;
            }

            SubFolders[ idx ].Parent = null;
            SubFolders.RemoveAt( idx );
        }

        // Remove a mod, but do not remove this folder from its parent if it is empty afterwards.
        internal void RemoveModIgnoreEmpty( Mod.Mod mod )
        {
            var idx = Mods.BinarySearch( mod, ModComparer );
            if( idx >= 0 )
            {
                Mods.RemoveAt( idx );
            }
        }
    }
}