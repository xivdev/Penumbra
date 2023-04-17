using System.Collections.Generic;
using System.IO;
using System.Linq;
using Penumbra.Mods.Manager;

namespace Penumbra.Mods;

public enum ModPathChangeType
{
    Added,
    Deleted,
    Moved,
    Reloaded,
    StartingReload,
}

public partial class Mod
{
    public DirectoryInfo ModPath { get; internal set; }
    public string Identifier
        => Index >= 0 ? ModPath.Name : Name;
    public int Index { get; internal set; } = -1;

    public bool IsTemporary
        => Index < 0;

    // Unused if Index < 0 but used for special temporary mods.
    public int Priority
        => 0;

    internal Mod( DirectoryInfo modPath )
    {
        ModPath  = modPath;
        Default = new SubMod( this );
    }
}