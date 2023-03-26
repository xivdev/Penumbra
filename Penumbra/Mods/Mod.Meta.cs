using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OtterGui.Classes;
using Penumbra.Services;
using Penumbra.Util;

namespace Penumbra.Mods;

public sealed partial class Mod : IMod
{
    public static readonly TemporaryMod ForcedFiles = new()
    {
        Name     = "Forced Files",
        Index    = -1,
        Priority = int.MaxValue,
    };

    public       LowerString           Name        { get; internal set; } = "New Mod";
    public       LowerString           Author      { get; internal set; } = LowerString.Empty;
    public       string                Description { get; internal set; } = string.Empty;
    public       string                Version     { get; internal set; } = string.Empty;
    public       string                Website     { get; internal set; } = string.Empty;
    public       IReadOnlyList<string> ModTags     { get; internal set; } = Array.Empty<string>();

    public override string ToString()
        => Name.Text;

    internal readonly struct ModMeta : ISavable
    {
        public const uint FileVersion = 3;

        private readonly Mod _mod;

        public ModMeta(Mod mod)
            => _mod = mod;

        public string ToFilename(FilenameService fileNames)
            => fileNames.ModMetaPath(_mod);

        public void Save(StreamWriter writer)
        {
            var jObject = new JObject
            {
                { nameof(FileVersion), JToken.FromObject(FileVersion) },
                { nameof(Name), JToken.FromObject(_mod.Name) },
                { nameof(Author), JToken.FromObject(_mod.Author) },
                { nameof(Description), JToken.FromObject(_mod.Description) },
                { nameof(Version), JToken.FromObject(_mod.Version) },
                { nameof(Website), JToken.FromObject(_mod.Website) },
                { nameof(ModTags), JToken.FromObject(_mod.ModTags) },
            };
            using var jWriter = new JsonTextWriter(writer) { Formatting = Formatting.Indented };
            jObject.WriteTo(jWriter);
        }
    }
}
