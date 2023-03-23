using System;

namespace Penumbra.Mods;

[Flags]
public enum ModDataChangeType : ushort
{
    None        = 0x0000,
    Name        = 0x0001,
    Author      = 0x0002,
    Description = 0x0004,
    Version     = 0x0008,
    Website     = 0x0010,
    Deletion    = 0x0020,
    Migration   = 0x0040,
    ModTags     = 0x0080,
    ImportDate  = 0x0100,
    Favorite    = 0x0200,
    LocalTags   = 0x0400,
    Note        = 0x0800,
}
