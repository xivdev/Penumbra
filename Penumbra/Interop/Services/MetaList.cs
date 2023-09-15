using Penumbra.Interop.Structs;

namespace Penumbra.Interop.Services;

public unsafe class MetaList : IDisposable
{
    private readonly CharacterUtility               _utility;
    private readonly LinkedList<MetaReverter>       _entries = new();
    public readonly  CharacterUtility.InternalIndex Index;
    public readonly  MetaIndex                      GlobalMetaIndex;

    public IReadOnlyCollection<MetaReverter> Entries
        => _entries;

    private nint _defaultResourceData = nint.Zero;
    private int  _defaultResourceSize = 0;
    public  bool Ready { get; private set; } = false;

    public MetaList(CharacterUtility utility, CharacterUtility.InternalIndex index)
    {
        _utility        = utility;
        Index           = index;
        GlobalMetaIndex = CharacterUtility.RelevantIndices[index.Value];
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
        Penumbra.Log.Excessive($"Temporarily set resource {GlobalMetaIndex} to 0x{(ulong)data:X} ({length} bytes).");
        var reverter = new MetaReverter(this, data, length);
        _entries.AddFirst(reverter);
        SetResourceInternal(data, length);
        return reverter;
    }

    public MetaReverter TemporarilyResetResource()
    {
        Penumbra.Log.Excessive(
            $"Temporarily reset resource {GlobalMetaIndex} to default at 0x{_defaultResourceData:X} ({_defaultResourceSize} bytes).");
        var reverter = new MetaReverter(this);
        _entries.AddFirst(reverter);
        ResetResourceInternal();
        return reverter;
    }

    public void SetResource(nint data, int length)
    {
        Penumbra.Log.Excessive($"Set resource {GlobalMetaIndex} to 0x{(ulong)data:X} ({length} bytes).");
        SetResourceInternal(data, length);
    }

    public void ResetResource()
    {
        Penumbra.Log.Excessive($"Reset resource {GlobalMetaIndex} to default at 0x{_defaultResourceData:X} ({_defaultResourceSize} bytes).");
        ResetResourceInternal();
    }

    /// <summary> Set the currently stored data of this resource to new values. </summary>
    private void SetResourceInternal(nint data, int length)
    {
        if (!Ready)
            return;

        var resource = _utility.Address->Resource(GlobalMetaIndex);
        resource->SetData(data, length);
    }

    /// <summary> Reset the currently stored data of this resource to its default values. </summary>
    private void ResetResourceInternal()
        => SetResourceInternal(_defaultResourceData, _defaultResourceSize);

    private void SetResourceToDefaultCollection()
        => _utility.Active.Default.SetMetaFile(_utility, GlobalMetaIndex);

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
        public static readonly MetaReverter Disabled = new(null!) { Disposed = true };

        public readonly MetaList MetaList;
        public readonly nint     Data;
        public readonly int      Length;
        public readonly bool     Resetter;
        public          bool     Disposed;

        public MetaReverter(MetaList metaList, nint data, int length)
        {
            MetaList = metaList;
            Data     = data;
            Length   = length;
        }

        public MetaReverter(MetaList metaList)
        {
            MetaList = metaList;
            Data     = nint.Zero;
            Length   = 0;
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
