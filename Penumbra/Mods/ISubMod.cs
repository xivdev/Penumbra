using System.Collections.Generic;
using Penumbra.GameData.ByteString;
using Penumbra.Meta.Manipulations;

namespace Penumbra.Mods;

public interface ISubMod
{
    public string Name { get; }

    public IReadOnlyDictionary< Utf8GamePath, FullPath > Files { get; }
    public IReadOnlyDictionary< Utf8GamePath, FullPath > FileSwaps { get; }
    public IReadOnlyList< MetaManipulation > Manipulations { get; }
}