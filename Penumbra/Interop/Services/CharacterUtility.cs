using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game;
using Dalamud.Utility.Signatures;
using Penumbra.GameData;
using Penumbra.Interop.Structs;

namespace Penumbra.Interop.Services;

public unsafe partial class CharacterUtility : IDisposable
{
    public record struct InternalIndex(int Value);

    /// <summary> A static pointer to the CharacterUtility address. </summary>
    [Signature(Sigs.CharacterUtility, ScanType = ScanType.StaticAddress)]
    private readonly CharacterUtilityData** _characterUtilityAddress = null;

    /// <summary> Only required for migration anymore. </summary>
    public delegate void LoadResources(CharacterUtilityData* address);

    [Signature(Sigs.LoadCharacterResources)]
    public readonly LoadResources LoadCharacterResourcesFunc = null!;

    public void LoadCharacterResources()
        => LoadCharacterResourcesFunc.Invoke(Address);

    public CharacterUtilityData* Address
        => *_characterUtilityAddress;

    public bool         Ready { get; private set; }
    public event Action LoadingFinished;
    public nint         DefaultTransparentResource { get; private set; }
    public nint         DefaultDecalResource       { get; private set; }

    /// <summary>
    /// The relevant indices depend on which meta manipulations we allow for.
    /// The defines are set in the project configuration.
    /// </summary>
    public static readonly MetaIndex[]
        RelevantIndices = Enum.GetValues<MetaIndex>();

    public static readonly InternalIndex[] ReverseIndices
        = Enumerable.Range(0, CharacterUtilityData.TotalNumResources)
            .Select(i => new InternalIndex(Array.IndexOf(RelevantIndices, (MetaIndex)i)))
            .ToArray();

    private readonly MetaList[] _lists = Enumerable.Range(0, RelevantIndices.Length)
        .Select(idx => new MetaList(new InternalIndex(idx)))
        .ToArray();

    public IReadOnlyList<MetaList> Lists
        => _lists;

    public (nint Address, int Size) DefaultResource(InternalIndex idx)
        => _lists[idx.Value].DefaultResource;

    private readonly Framework _framework;

    public CharacterUtility(Framework framework)
    {
        SignatureHelper.Initialise(this);
        _framework      =  framework;
        LoadingFinished += () => Penumbra.Log.Debug("Loading of CharacterUtility finished.");
        LoadDefaultResources(null!);
        if (!Ready)
            _framework.Update += LoadDefaultResources;
    }

    /// <summary> We store the default data of the resources so we can always restore them. </summary>
    private void LoadDefaultResources(object _)
    {
        if (Address == null)
            return;

        var anyMissing = false;
        for (var i = 0; i < RelevantIndices.Length; ++i)
        {
            var list = _lists[i];
            if (list.Ready)
                continue;

            var resource = Address->Resource(RelevantIndices[i]);
            var (data, length) = resource->GetData();
            list.SetDefaultResource(data, length);
            anyMissing |= !_lists[i].Ready;
        }

        if (DefaultTransparentResource == nint.Zero)
        {
            DefaultTransparentResource =  (nint)Address->TransparentTexResource;
            anyMissing                 |= DefaultTransparentResource == nint.Zero;
        }

        if (DefaultDecalResource == nint.Zero)
        {
            DefaultDecalResource =  (nint)Address->DecalTexResource;
            anyMissing           |= DefaultDecalResource == nint.Zero;
        }

        if (anyMissing)
            return;

        Ready             =  true;
        _framework.Update -= LoadDefaultResources;
        LoadingFinished.Invoke();
    }

    public void SetResource(MetaIndex resourceIdx, nint data, int length)
    {
        var idx  = ReverseIndices[(int)resourceIdx];
        var list = _lists[idx.Value];
        list.SetResource(data, length);
    }

    public void ResetResource(MetaIndex resourceIdx)
    {
        var idx  = ReverseIndices[(int)resourceIdx];
        var list = _lists[idx.Value];
        list.ResetResource();
    }

    public MetaList.MetaReverter TemporarilySetResource(MetaIndex resourceIdx, nint data, int length)
    {
        var idx  = ReverseIndices[(int)resourceIdx];
        var list = _lists[idx.Value];
        return list.TemporarilySetResource(data, length);
    }

    public MetaList.MetaReverter TemporarilyResetResource(MetaIndex resourceIdx)
    {
        var idx  = ReverseIndices[(int)resourceIdx];
        var list = _lists[idx.Value];
        return list.TemporarilyResetResource();
    }

    /// <summary> Return all relevant resources to the default resource. </summary>
    public void ResetAll()
    {
        foreach (var list in _lists)
            list.Dispose();

        Address->TransparentTexResource = (TextureResourceHandle*)DefaultTransparentResource;
        Address->DecalTexResource       = (TextureResourceHandle*)DefaultDecalResource;
    }

    public void Dispose()
        => ResetAll();
}
