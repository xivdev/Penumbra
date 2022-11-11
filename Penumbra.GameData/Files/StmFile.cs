using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using Dalamud.Data;
using Lumina.Extensions;
using Penumbra.GameData.Structs;

namespace Penumbra.GameData.Files;

public partial class StmFile
{
    public const string Path = "chara/base_material/stainingtemplate.stm";

    public record struct DyePack
    {
        public Vector3 Diffuse;
        public Vector3 Specular;
        public Vector3 Emissive;
        public float   SpecularPower;
        public float   Gloss;
    }

    public readonly struct StainingTemplateEntry
    {
        public const int NumElements = 128;

        public readonly IReadOnlyList<(Half R, Half G, Half B)> DiffuseEntries;
        public readonly IReadOnlyList<(Half R, Half G, Half B)> SpecularEntries;
        public readonly IReadOnlyList<(Half R, Half G, Half B)> EmissiveEntries;
        public readonly IReadOnlyList<Half>                     SpecularPowerEntries;
        public readonly IReadOnlyList<Half>                     GlossEntries;

        public DyePack this[StainId idx]
            => this[(int)idx.Value];

        public DyePack this[int idx]
        {
            get
            {
                if (idx is <= 0 or > NumElements)
                    return default;

                --idx;
                var (dr, dg, db) = DiffuseEntries[idx];
                var (sr, sg, sb) = SpecularEntries[idx];
                var (er, eg, eb) = EmissiveEntries[idx];
                var sp = SpecularPowerEntries[idx];
                var g  = GlossEntries[idx];
                return new DyePack
                {
                    Diffuse       = new Vector3((float)dr, (float)dg, (float)db),
                    Emissive      = new Vector3((float)sr, (float)sg, (float)sb),
                    Specular      = new Vector3((float)er, (float)eg, (float)eb),
                    SpecularPower = (float)sp,
                    Gloss         = (float)g,
                };
            }
        }

        private class RepeatingList<T> : IReadOnlyList<T>
        {
            private readonly T   _value;
            public           int Count { get; }

            public RepeatingList(T value, int size)
            {
                _value = value;
                Count  = size;
            }

            public IEnumerator<T> GetEnumerator()
            {
                for (var i = 0; i < Count; ++i)
                    yield return _value;
            }

            IEnumerator IEnumerable.GetEnumerator()
                => GetEnumerator();

            public T this[int index]
                => index >= 0 && index < Count ? _value : throw new IndexOutOfRangeException();
        }

        private class IndexedList<T> : IReadOnlyList<T>
        {
            private readonly T[]    _values;
            private readonly byte[] _indices;

            public IndexedList(BinaryReader br, int count, int indexCount, Func<BinaryReader, T> read, int entrySize)
            {
                _values    = new T[count + 1];
                _indices   = new byte[indexCount];
                _values[0] = default!;
                for (var i = 1; i <= count; ++i)
                    _values[i] = read(br);
                for (var i = 0; i < indexCount; ++i)
                {
                    _indices[i] = br.ReadByte();
                    if (_indices[i] > count)
                        _indices[i] = 0;
                }
            }

            public IEnumerator<T> GetEnumerator()
            {
                for (var i = 0; i < NumElements; ++i)
                    yield return _values[_indices[i]];
            }

            IEnumerator IEnumerable.GetEnumerator()
                => GetEnumerator();

            public int Count
                => _indices.Length;

            public T this[int index]
                => index >= 0 && index < Count ? _values[_indices[index]] : throw new IndexOutOfRangeException();
        }

        private static IReadOnlyList<T> ReadArray<T>(BinaryReader br, int offset, int size, Func<BinaryReader, T> read, int entrySize)
        {
            br.Seek(offset);
            var arraySize = size / entrySize;
            switch (arraySize)
            {
                case 0: return new RepeatingList<T>(default!, NumElements);
                case 1: return new RepeatingList<T>(read(br), NumElements);
                case NumElements:
                    var ret = new T[NumElements];
                    for (var i = 0; i < NumElements; ++i)
                        ret[i] = read(br);
                    return ret;
                case < NumElements: return new IndexedList<T>(br, arraySize - NumElements / entrySize / 2, NumElements, read, entrySize);
                case > NumElements: throw new InvalidDataException($"Stain Template can not have more than {NumElements} elements.");
            }
        }

        private static (Half, Half, Half) ReadTriple(BinaryReader br)
            => (br.ReadHalf(), br.ReadHalf(), br.ReadHalf());

        private static Half ReadSingle(BinaryReader br)
            => br.ReadHalf();

        public unsafe StainingTemplateEntry(BinaryReader br, int offset)
        {
            br.Seek(offset);
            Span<ushort> ends = stackalloc ushort[5];
            for (var i = 0; i < ends.Length; ++i)
                ends[i] = br.ReadUInt16();

            offset               += ends.Length * 2;
            DiffuseEntries       =  ReadArray(br, offset,           ends[0],           ReadTriple, 3);
            SpecularEntries      =  ReadArray(br, offset + ends[0], ends[1] - ends[0], ReadTriple, 3);
            EmissiveEntries      =  ReadArray(br, offset + ends[1], ends[2] - ends[1], ReadTriple, 3);
            SpecularPowerEntries =  ReadArray(br, offset + ends[2], ends[3] - ends[2], ReadSingle, 1);
            GlossEntries         =  ReadArray(br, offset + ends[3], ends[4] - ends[3], ReadSingle, 1);
        }
    }

    public readonly IReadOnlyDictionary<ushort, StainingTemplateEntry> Entries;

    public DyePack this[ushort template, int idx]
        => Entries.TryGetValue(template, out var entry) ? entry[idx] : default;

    public DyePack this[ushort template, StainId idx]
        => this[template, (int)idx.Value];

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

    public StmFile(DataManager gameData)
        : this(gameData.GetFile(Path)?.Data ?? Array.Empty<byte>())
    { }
}
