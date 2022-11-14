using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using Lumina.Extensions;
using Penumbra.GameData.Structs;

namespace Penumbra.GameData.Files;

public partial class StmFile
{
    public readonly struct StainingTemplateEntry
    {
        /// <summary>
        /// The number of stains is capped at 128 at the moment
        /// </summary>
        public const int NumElements = 128;

        // ColorSet row information for each stain.
        public readonly IReadOnlyList<(Half R, Half G, Half B)> DiffuseEntries;
        public readonly IReadOnlyList<(Half R, Half G, Half B)> SpecularEntries;
        public readonly IReadOnlyList<(Half R, Half G, Half B)> EmissiveEntries;
        public readonly IReadOnlyList<Half>                     GlossEntries;
        public readonly IReadOnlyList<Half>                     SpecularPowerEntries;

        public DyePack this[StainId idx]
            => this[(int)idx.Value];

        public DyePack this[int idx]
        {
            get
            {
                // The 0th index is skipped.
                if (idx is <= 0 or > NumElements)
                    return default;

                --idx;
                var (dr, dg, db) = DiffuseEntries[idx];
                var (sr, sg, sb) = SpecularEntries[idx];
                var (er, eg, eb) = EmissiveEntries[idx];
                var g  = GlossEntries[idx];
                var sp = SpecularPowerEntries[idx];
                // Convert to DyePack using floats.
                return new DyePack
                {
                    Diffuse       = new Vector3((float)dr, (float)dg, (float)db),
                    Specular      = new Vector3((float)sr, (float)sg, (float)sb),
                    Emissive      = new Vector3((float)er, (float)eg, (float)eb),
                    Gloss         = (float)g,
                    SpecularPower = (float)sp,
                };
            }
        }

        private static IReadOnlyList<T> ReadArray<T>(BinaryReader br, int offset, int size, Func<BinaryReader, T> read, int entrySize)
        {
            br.Seek(offset);
            var arraySize = size / entrySize;
            // The actual amount of entries informs which type of list we use.
            switch (arraySize)
            {
                case 0: return new RepeatingList<T>(default!, NumElements); // All default
                case 1: return new RepeatingList<T>(read(br), NumElements); // All single entry
                case NumElements:                                           // 1-to-1 entries
                    var ret = new T[NumElements];
                    for (var i = 0; i < NumElements; ++i)
                        ret[i] = read(br);
                    return ret;
                // Indexed access.
                case < NumElements: return new IndexedList<T>(br, arraySize - NumElements / entrySize, NumElements, read);
                // Should not happen.
                case > NumElements: throw new InvalidDataException($"Stain Template can not have more than {NumElements} elements.");
            }
        }

        // Read functions
        private static (Half, Half, Half) ReadTriple(BinaryReader br)
            => (br.ReadHalf(), br.ReadHalf(), br.ReadHalf());

        private static Half ReadSingle(BinaryReader br)
            => br.ReadHalf();

        // Actually parse an entry.
        public unsafe StainingTemplateEntry(BinaryReader br, int offset)
        {
            br.Seek(offset);
            // 5 different lists of values.
            Span<ushort> ends = stackalloc ushort[5];
            for (var i = 0; i < ends.Length; ++i)
                ends[i] = (ushort)(br.ReadUInt16() * 2); // because the ends are in terms of ushort.
            offset += ends.Length * 2;

            DiffuseEntries       = ReadArray(br, offset,           ends[0],           ReadTriple, 6);
            SpecularEntries      = ReadArray(br, offset + ends[0], ends[1] - ends[0], ReadTriple, 6);
            EmissiveEntries      = ReadArray(br, offset + ends[1], ends[2] - ends[1], ReadTriple, 6);
            GlossEntries         = ReadArray(br, offset + ends[2], ends[3] - ends[2], ReadSingle, 2);
            SpecularPowerEntries = ReadArray(br, offset + ends[3], ends[4] - ends[3], ReadSingle, 2);
        }

        /// <summary>
        /// Used if a single value is used for all entries of a list.
        /// </summary>
        private sealed class RepeatingList<T> : IReadOnlyList<T>
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

        /// <summary>
        /// Used if there is a small set of values for a bigger list, accessed via index information.
        /// </summary>
        private sealed class IndexedList<T> : IReadOnlyList<T>
        {
            private readonly T[]    _values;
            private readonly byte[] _indices;

            /// <summary>
            /// Reads <paramref name="count"/> values from <paramref name="br"/> via <paramref name="read"/>, then reads <paramref name="indexCount"/> byte indices.
            /// </summary>
            public IndexedList(BinaryReader br, int count, int indexCount, Func<BinaryReader, T> read)
            {
                _values    = new T[count + 1];
                _indices   = new byte[indexCount];
                _values[0] = default!;
                for (var i = 1; i < count + 1; ++i)
                    _values[i] = read(br);

                // Seems to be an unused 0xFF byte marker.
                // Necessary for correct offsets.
                br.ReadByte();
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
                => index >= 0 && index < Count ? _values[_indices[index]] : default!;
        }
    }
}
