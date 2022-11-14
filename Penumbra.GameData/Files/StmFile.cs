using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using Dalamud.Data;
using Penumbra.GameData.Structs;

namespace Penumbra.GameData.Files;

public partial class StmFile
{
    public const string Path = "chara/base_material/stainingtemplate.stm";

    /// <summary>
    /// All dye-able color set information for a row.
    /// </summary>
    public record struct DyePack
    {
        public Vector3 Diffuse;
        public Vector3 Specular;
        public Vector3 Emissive;
        public float   Gloss;
        public float   SpecularPower;
    }

    /// <summary>
    /// All currently available dyeing templates with their IDs.
    /// </summary>
    public readonly IReadOnlyDictionary<ushort, StainingTemplateEntry> Entries;

    /// <summary>
    /// Access a specific dye pack.
    /// </summary>
    /// <param name="template">The ID of the accessed template.</param>
    /// <param name="idx">The ID of the Stain.</param>
    /// <returns>The corresponding color set information or a defaulted DyePack of 0-entries.</returns>
    public DyePack this[ushort template, int idx]
        => Entries.TryGetValue(template, out var entry) ? entry[idx] : default;

    /// <inheritdoc cref="this[ushort, StainId]"/>
    public DyePack this[ushort template, StainId idx]
        => this[template, (int)idx.Value];

    /// <summary>
    /// Try to access a specific dye pack.
    /// </summary>
    /// <param name="template">The ID of the accessed template.</param>
    /// <param name="idx">The ID of the Stain.</param>
    /// <param name="dyes">On success, the corresponding color set information, otherwise a defaulted DyePack.</param>
    /// <returns>True on success, false otherwise.</returns>
    public bool TryGetValue(ushort template, StainId idx, out DyePack dyes)
    {
        if (idx.Value is > 0 and <= StainingTemplateEntry.NumElements && Entries.TryGetValue(template, out var entry))
        {
            dyes = entry[idx];
            return true;
        }

        dyes = default;
        return false;
    }

    /// <summary>
    /// Create a STM file from the given data array.
    /// </summary>
    public StmFile(byte[] data)
    {
        using var stream = new MemoryStream(data);
        using var br     = new BinaryReader(stream);
        br.ReadUInt32();
        var numEntries = br.ReadInt32();

        var keys    = new ushort[numEntries];
        var offsets = new ushort[numEntries];

        for (var i = 0; i < numEntries; ++i)
            keys[i] = br.ReadUInt16();

        for (var i = 0; i < numEntries; ++i)
            offsets[i] = br.ReadUInt16();

        var entries = new Dictionary<ushort, StainingTemplateEntry>(numEntries);
        Entries = entries;

        for (var i = 0; i < numEntries; ++i)
        {
            var offset = offsets[i] * 2 + 8 + 4 * numEntries;
            entries.Add(keys[i], new StainingTemplateEntry(br, offset));
        }
    }

    /// <summary>
    /// Try to read and parse the default STM file given by Lumina.
    /// </summary>
    public StmFile(DataManager gameData)
        : this(gameData.GetFile(Path)?.Data ?? Array.Empty<byte>())
    { }
}
