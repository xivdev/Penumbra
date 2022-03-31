using System.Collections.Generic;
using Newtonsoft.Json;
using Penumbra.GameData.ByteString;
using Penumbra.Meta.Manipulations;

namespace Penumbra.Mods;

public partial class Mod2
{
    private sealed class SubMod : ISubMod
    {
        public string Name { get; set; } = "Default";

        [JsonProperty( ItemConverterType = typeof( FullPath.FullPathConverter ) )]
        public readonly Dictionary< Utf8GamePath, FullPath > FileData = new();

        [JsonProperty( ItemConverterType = typeof( FullPath.FullPathConverter ) )]
        public readonly Dictionary< Utf8GamePath, FullPath > FileSwapData = new();

        public readonly List< MetaManipulation > ManipulationData = new();

        public IReadOnlyDictionary< Utf8GamePath, FullPath > Files
            => FileData;

        public IReadOnlyDictionary< Utf8GamePath, FullPath > FileSwaps
            => FileSwapData;

        public IReadOnlyList< MetaManipulation > Manipulations
            => ManipulationData;
    }
}