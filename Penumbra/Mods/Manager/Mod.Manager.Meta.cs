using System;

namespace Penumbra.Mods;

public sealed partial class Mod
{
    public partial class Manager
    {
        public delegate void ModDataChangeDelegate( ModDataChangeType type, Mod mod, string? oldName );
        public event ModDataChangeDelegate? ModDataChanged;

        public void ChangeModName( Index idx, string newName )
        {
            var mod = this[ idx ];
            if( mod.Name.Text != newName )
            {
                var oldName = mod.Name;
                mod.Name = newName;
                mod.SaveMeta();
                ModDataChanged?.Invoke( ModDataChangeType.Name, mod, oldName.Text );
            }
        }

        public void ChangeModAuthor( Index idx, string newAuthor )
        {
            var mod = this[ idx ];
            if( mod.Author != newAuthor )
            {
                mod.Author = newAuthor;
                mod.SaveMeta();
                ModDataChanged?.Invoke( ModDataChangeType.Author, mod, null );
            }
        }

        public void ChangeModDescription( Index idx, string newDescription )
        {
            var mod = this[ idx ];
            if( mod.Description != newDescription )
            {
                mod.Description = newDescription;
                mod.SaveMeta();
                ModDataChanged?.Invoke( ModDataChangeType.Description, mod, null );
            }
        }

        public void ChangeModVersion( Index idx, string newVersion )
        {
            var mod = this[ idx ];
            if( mod.Version != newVersion )
            {
                mod.Version = newVersion;
                mod.SaveMeta();
                ModDataChanged?.Invoke( ModDataChangeType.Version, mod, null );
            }
        }

        public void ChangeModWebsite( Index idx, string newWebsite )
        {
            var mod = this[ idx ];
            if( mod.Website != newWebsite )
            {
                mod.Website = newWebsite;
                mod.SaveMeta();
                ModDataChanged?.Invoke( ModDataChangeType.Website, mod, null );
            }
        }

        public void ChangeModTag( Index idx, int tagIdx, string newTag )
            => ChangeTag( idx, tagIdx, newTag, false );
    }
}