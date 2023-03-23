using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Penumbra.Services;

namespace Penumbra.Mods;

public sealed partial class Mod
{
    public long ImportDate { get; internal set; } = DateTimeOffset.UnixEpoch.ToUnixTimeMilliseconds();

    public IReadOnlyList<string> LocalTags { get; private set; } = Array.Empty<string>();

    public string AllTagsLower { get; private set; }  = string.Empty;
    public string Note         { get; internal set; } = string.Empty;
    public bool   Favorite     { get; internal set; } = false;

    internal ModDataChangeType UpdateTags(IEnumerable<string>? newModTags, IEnumerable<string>? newLocalTags)
    {
        if (newModTags == null && newLocalTags == null)
            return 0;

        ModDataChangeType type = 0;
        if (newModTags != null)
        {
            var modTags = newModTags.Where(t => t.Length > 0).Distinct().ToArray();
            if (!modTags.SequenceEqual(ModTags))
            {
                newLocalTags ??= LocalTags;
                ModTags      =   modTags;
                type         |=  ModDataChangeType.ModTags;
            }
        }

        if (newLocalTags != null)
        {
            var localTags = newLocalTags!.Where(t => t.Length > 0 && !ModTags.Contains(t)).Distinct().ToArray();
            if (!localTags.SequenceEqual(LocalTags))
            {
                LocalTags =  localTags;
                type      |= ModDataChangeType.LocalTags;
            }
        }

        if (type != 0)
            AllTagsLower = string.Join('\0', ModTags.Concat(LocalTags).Select(s => s.ToLowerInvariant()));

        return type;
    }
}
