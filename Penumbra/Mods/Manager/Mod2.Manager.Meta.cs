using System;

namespace Penumbra.Mods;

public sealed partial class Mod2
{
    public partial class Manager
    {
        public delegate void ModMetaChangeDelegate( MetaChangeType type, Mod2 mod );
        public event ModMetaChangeDelegate? ModMetaChanged;

        public void ChangeModName( Index idx, string newName )
        {
            var mod = this[ idx ];
            if( mod.Name != newName )
            {
                mod.Name = newName;
                mod.SaveMeta();
                ModMetaChanged?.Invoke( MetaChangeType.Name, mod );
            }
        }

        public void ChangeModAuthor( Index idx, string newAuthor )
        {
            var mod = this[ idx ];
            if( mod.Author != newAuthor )
            {
                mod.Author = newAuthor;
                mod.SaveMeta();
                ModMetaChanged?.Invoke( MetaChangeType.Author, mod );
            }
        }

        public void ChangeModDescription( Index idx, string newDescription )
        {
            var mod = this[ idx ];
            if( mod.Description != newDescription )
            {
                mod.Description = newDescription;
                mod.SaveMeta();
                ModMetaChanged?.Invoke( MetaChangeType.Description, mod );
            }
        }

        public void ChangeModVersion( Index idx, string newVersion )
        {
            var mod = this[ idx ];
            if( mod.Version != newVersion )
            {
                mod.Version = newVersion;
                mod.SaveMeta();
                ModMetaChanged?.Invoke( MetaChangeType.Version, mod );
            }
        }

        public void ChangeModWebsite( Index idx, string newWebsite )
        {
            var mod = this[ idx ];
            if( mod.Website != newWebsite )
            {
                mod.Website = newWebsite;
                mod.SaveMeta();
                ModMetaChanged?.Invoke( MetaChangeType.Website, mod );
            }
        }
    }
}