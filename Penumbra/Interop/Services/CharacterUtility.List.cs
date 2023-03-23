using System;
using System.Collections.Generic;
using Penumbra.Interop.Structs;

namespace Penumbra.Interop.Services;

public unsafe partial class CharacterUtility
{
    public class MetaList : IDisposable
    {
        private readonly LinkedList<MetaReverter> _entries = new();
        public readonly InternalIndex Index;
        public readonly MetaIndex GlobalMetaIndex;

        public IReadOnlyCollection<MetaReverter> Entries
            => _entries;

        private nint _defaultResourceData = nint.Zero;
        private int _defaultResourceSize = 0;
        public bool Ready { get; private set; } = false;

        public MetaList(InternalIndex index)
        {
            Index = index;
            GlobalMetaIndex = RelevantIndices[index.Value];
        }

        public void SetDefaultResource(nint data, int size)
        {
            if (Ready)
                return;

            _defaultResourceData = data;
            _defaultResourceSize = size;
            Ready                = _defaultResourceData != nint.Zero && size != 0;
            if (_entries.Count <= 0)
                return;

            var first = _entries.First!.Value;
            SetResource(first.Data, first.Length);
        }

        public (nint Address, int Size) DefaultResource
            => (_defaultResourceData, _defaultResourceSize);

        public MetaReverter TemporarilySetResource(nint data, int length)
        {
#if false
            Penumbra.Log.Verbose($"Temporarily set resource {GlobalMetaIndex} to 0x{(ulong)data:X} ({length} bytes).");
#endif
            var reverter = new MetaReverter(this, data, length);
            _entries.AddFirst(reverter);
            SetResourceInternal(data, length);
            return reverter;
        }

        public MetaReverter TemporarilyResetResource()
        {
#if false
            Penumbra.Log.Verbose(
                $"Temporarily reset resource {GlobalMetaIndex} to default at 0x{_defaultResourceData:X} ({_defaultResourceSize} bytes).");
#endif
            var reverter = new MetaReverter(this);
            _entries.AddFirst(reverter);
            ResetResourceInternal();
            return reverter;
        }

        public void SetResource(nint data, int length)
        {
#if false
            Penumbra.Log.Verbose($"Set resource {GlobalMetaIndex} to 0x{(ulong)data:X} ({length} bytes).");
#endif
            SetResourceInternal(data, length);
        }

        public void ResetResource()
        {
#if false
            Penumbra.Log.Verbose($"Reset resource {GlobalMetaIndex} to default at 0x{_defaultResourceData:X} ({_defaultResourceSize} bytes).");
#endif
            ResetResourceInternal();
        }

        /// <summary> Set the currently stored data of this resource to new values. </summary>
        private void SetResourceInternal(nint data, int length)
        {
            if (!Ready)
                return;

            var resource = Penumbra.CharacterUtility.Address->Resource(GlobalMetaIndex);
            resource->SetData(data, length);
        }

        /// <summary> Reset the currently stored data of this resource to its default values. </summary>
        private void ResetResourceInternal()
            => SetResourceInternal(_defaultResourceData, _defaultResourceSize);

        private void SetResourceToDefaultCollection()
            => Penumbra.CollectionManager.Default.SetMetaFile(GlobalMetaIndex);

        public void Dispose()
        {
            if (_entries.Count > 0)
            {
                foreach (var entry in _entries)
                    entry.Disposed = true;

                _entries.Clear();
            }

            ResetResourceInternal();
        }

        public sealed class MetaReverter : IDisposable
        {
            public readonly MetaList MetaList;
            public readonly nint Data;
            public readonly int Length;
            public readonly bool Resetter;
            public bool Disposed;

            public MetaReverter(MetaList metaList, nint data, int length)
            {
                MetaList = metaList;
                Data = data;
                Length = length;
            }

            public MetaReverter(MetaList metaList)
            {
                MetaList = metaList;
                Data = nint.Zero;
                Length = 0;
                Resetter = true;
            }

            public void Dispose()
            {
                if (Disposed)
                    return;

                var list       = MetaList._entries;
                var wasCurrent = ReferenceEquals(this, list.First?.Value);
                list.Remove(this);
                if (!wasCurrent)
                    return;

                if (list.Count == 0)
                {
                    MetaList.SetResourceToDefaultCollection();
                }
                else
                {
                    var next = list.First!.Value;
                    if (next.Resetter)
                        MetaList.ResetResourceInternal();
                    else
                        MetaList.SetResourceInternal(next.Data, next.Length);
                }

                Disposed = true;
            }
        }
    }
}