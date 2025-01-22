using Dalamud.Plugin.Services;
using Dalamud.Utility.Signatures;
using OtterGui.Services;
using Penumbra.Communication;
using Penumbra.GameData;
using Penumbra.Interop.Structs;

namespace Penumbra.Interop.Services;

public unsafe class CharacterUtility : IDisposable, IRequiredService
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

    public bool Ready { get; private set; }

    public readonly CharacterUtilityFinished LoadingFinished = new();

    public nint DefaultHumanPbdResource               { get; private set; }
    public nint DefaultTransparentResource            { get; private set; }
    public nint DefaultDecalResource                  { get; private set; }
    public nint DefaultSkinShpkResource               { get; private set; }
    public nint DefaultCharacterStockingsShpkResource { get; private set; }
    public nint DefaultCharacterLegacyShpkResource    { get; private set; }

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

    private readonly MetaList[] _lists;

    public (nint Address, int Size) DefaultResource(InternalIndex idx)
        => _lists[idx.Value].DefaultResource;

    private readonly IFramework _framework;

    public CharacterUtility(IFramework framework, IGameInteropProvider interop)
    {
        interop.InitializeFromAttributes(this);
        _lists = Enumerable.Range(0, RelevantIndices.Length)
            .Select(idx => new MetaList(new InternalIndex(idx)))
            .ToArray();
        _framework      =  framework;
        LoadingFinished.Subscribe(() => Penumbra.Log.Debug("Loading of CharacterUtility finished."), CharacterUtilityFinished.Priority.OnFinishedLoading);
        LoadDefaultResources(null!);
        if (!Ready)
            _framework.Update += LoadDefaultResources;
    }

    /// <summary> We store the default data of the resources, so we can always restore them. </summary>
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

        if (DefaultHumanPbdResource == nint.Zero)
        {
            DefaultHumanPbdResource =  (nint)Address->HumanPbdResource;
            anyMissing              |= DefaultHumanPbdResource == nint.Zero;
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

        if (DefaultSkinShpkResource == nint.Zero)
        {
            DefaultSkinShpkResource =  (nint)Address->SkinShpkResource;
            anyMissing              |= DefaultSkinShpkResource == nint.Zero;
        }

        if (DefaultCharacterStockingsShpkResource == nint.Zero)
        {
            DefaultCharacterStockingsShpkResource =  (nint)Address->CharacterStockingsShpkResource;
            anyMissing                            |= DefaultCharacterStockingsShpkResource == nint.Zero;
        }

        if (DefaultCharacterLegacyShpkResource == nint.Zero)
        {
            DefaultCharacterLegacyShpkResource =  (nint)Address->CharacterLegacyShpkResource;
            anyMissing                         |= DefaultCharacterLegacyShpkResource == nint.Zero;
        }

        if (anyMissing)
            return;

        Ready             =  true;
        _framework.Update -= LoadDefaultResources;
        LoadingFinished.Invoke();
    }

    /// <summary> Return all relevant resources to the default resource. </summary>
    public void ResetAll()
    {
        if (!Ready)
            return;

        Address->HumanPbdResource               = (ResourceHandle*)DefaultHumanPbdResource;
        Address->TransparentTexResource         = (TextureResourceHandle*)DefaultTransparentResource;
        Address->DecalTexResource               = (TextureResourceHandle*)DefaultDecalResource;
        Address->SkinShpkResource               = (ResourceHandle*)DefaultSkinShpkResource;
        Address->CharacterStockingsShpkResource = (ResourceHandle*)DefaultCharacterStockingsShpkResource;
        Address->CharacterLegacyShpkResource    = (ResourceHandle*)DefaultCharacterLegacyShpkResource;
    }

    public void Dispose()
        => ResetAll();
}
