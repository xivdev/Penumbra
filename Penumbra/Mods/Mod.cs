using System;
using System.Collections.Generic;
using OtterGui.Classes;

namespace Penumbra.Mods;

public sealed partial class Mod : IMod
{
    public static readonly TemporaryMod ForcedFiles = new()
    {
        Name     = "Forced Files",
        Index    = -1,
        Priority = int.MaxValue,
    };

    // Meta Data
    public LowerString           Name        { get; internal set; } = "New Mod";
    public LowerString           Author      { get; internal set; } = LowerString.Empty;
    public string                Description { get; internal set; } = string.Empty;
    public string                Version     { get; internal set; } = string.Empty;
    public string                Website     { get; internal set; } = string.Empty;
    public IReadOnlyList<string> ModTags     { get; internal set; } = Array.Empty<string>();


    // Local Data
    public long                  ImportDate { get; internal set; } = DateTimeOffset.UnixEpoch.ToUnixTimeMilliseconds();
    public IReadOnlyList<string> LocalTags  { get; internal set; } = Array.Empty<string>();
    public string                Note       { get; internal set; } = string.Empty;
    public bool                  Favorite   { get; internal set; } = false;


    // Access
    public override string ToString()
        => Name.Text;
}
