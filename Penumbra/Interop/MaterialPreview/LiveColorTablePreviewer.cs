using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Graphics.Kernel;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using Penumbra.GameData.Interop;
using Penumbra.Interop.SafeHandles;

namespace Penumbra.Interop.MaterialPreview;

public sealed unsafe class LiveColorTablePreviewer : LiveMaterialPreviewerBase
{
    private readonly IFramework _framework;

    private readonly Texture**         _colorTableTexture;
    private readonly SafeTextureHandle _originalColorTableTexture;

    private bool _updatePending;

    public int Width  { get; }
    public int Height { get; }

    public Half[] ColorTable { get; }

    public LiveColorTablePreviewer(ObjectManager objects, IFramework framework, MaterialInfo materialInfo)
        : base(objects, materialInfo)
    {
        _framework = framework;

        var mtrlHandle = Material->MaterialResourceHandle;
        if (mtrlHandle == null)
            throw new InvalidOperationException("Material doesn't have a resource handle");

        var colorSetTextures = DrawObject->ColorTableTextures;
        if (colorSetTextures == null)
            throw new InvalidOperationException("Draw object doesn't have color table textures");

        _colorTableTexture = colorSetTextures + (MaterialInfo.ModelSlot * CharacterBase.MaterialsPerSlot + MaterialInfo.MaterialSlot);
        

        _originalColorTableTexture = new SafeTextureHandle(*_colorTableTexture, true);
        if (_originalColorTableTexture.Texture == null)
            throw new InvalidOperationException("Material doesn't have a color table");

        Width          = (int)_originalColorTableTexture.Texture->Width;
        Height         = (int)_originalColorTableTexture.Texture->Height;
        ColorTable     = new Half[Width * Height * 4];
        _updatePending = true;

        framework.Update += OnFrameworkUpdate;
    }

    public Span<Half> GetColorRow(int i)
        => ColorTable.AsSpan().Slice(Width * 4 * i, Width * 4);

    protected override void Clear(bool disposing, bool reset)
    {
        _framework.Update -= OnFrameworkUpdate;

        base.Clear(disposing, reset);

        if (reset)
            _originalColorTableTexture.Exchange(ref *(nint*)_colorTableTexture);

        _originalColorTableTexture.Dispose();
    }

    public void ScheduleUpdate()
    {
        _updatePending = true;
    }

    [SkipLocalsInit]
    private void OnFrameworkUpdate(IFramework _)
    {
        if (!_updatePending)
            return;

        _updatePending = false;

        if (!CheckValidity())
            return;

        var textureSize = stackalloc int[2];
        textureSize[0] = Width;
        textureSize[1] = Height;

        using var texture =
            new SafeTextureHandle(Device.Instance()->CreateTexture2D(textureSize, 1, 0x2460, 0x80000804, 7), false);
        if (texture.IsInvalid)
            return;

        bool success;
        lock (ColorTable)
        {
            fixed (Half* colorTable = ColorTable)
            {
                success = texture.Texture->InitializeContents(colorTable);
            }
        }

        if (success)
            texture.Exchange(ref *(nint*)_colorTableTexture);
    }

    protected override bool IsStillValid()
    {
        if (!base.IsStillValid())
            return false;

        var colorSetTextures = DrawObject->ColorTableTextures;
        if (colorSetTextures == null)
            return false;

        return _colorTableTexture == colorSetTextures + (MaterialInfo.ModelSlot * CharacterBase.MaterialsPerSlot + MaterialInfo.MaterialSlot);
    }
}
