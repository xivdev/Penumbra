using Dalamud.Game;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Graphics.Kernel;
using FFXIVClientStructs.FFXIV.Client.Graphics.Render;
using Penumbra.GameData.Files;
using Penumbra.Interop.SafeHandles;

namespace Penumbra.Interop.MaterialPreview;

public sealed unsafe class LiveColorTablePreviewer : LiveMaterialPreviewerBase
{
    public const int TextureWidth  = 4;
    public const int TextureHeight = MtrlFile.ColorTable.NumRows;
    public const int TextureLength = TextureWidth * TextureHeight * 4;

    private readonly Framework _framework;

    private readonly Texture**         _colorTableTexture;
    private readonly SafeTextureHandle _originalColorTableTexture;

    private Half[] _colorTable;
    private bool   _updatePending;

    public Half[] ColorTable
        => _colorTable;

    public LiveColorTablePreviewer(IObjectTable objects, Framework framework, MaterialInfo materialInfo)
        : base(objects, materialInfo)
    {
        _framework = framework;

        var mtrlHandle = Material->MaterialResourceHandle;
        if (mtrlHandle == null)
            throw new InvalidOperationException("Material doesn't have a resource handle");

        var colorSetTextures = ((Structs.CharacterBaseExt*)DrawObject)->ColorTableTextures;
        if (colorSetTextures == null)
            throw new InvalidOperationException("Draw object doesn't have color table textures");

        _colorTableTexture = colorSetTextures + (MaterialInfo.ModelSlot * 4 + MaterialInfo.MaterialSlot);

        _originalColorTableTexture = new SafeTextureHandle(*_colorTableTexture, true);
        if (_originalColorTableTexture == null)
            throw new InvalidOperationException("Material doesn't have a color table");

        _colorTable    = new Half[TextureLength];
        _updatePending = true;

        framework.Update += OnFrameworkUpdate;
    }

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

    private void OnFrameworkUpdate(Framework _)
    {
        if (!_updatePending)
            return;

        _updatePending = false;

        if (!CheckValidity())
            return;

        var textureSize = stackalloc int[2];
        textureSize[0] = TextureWidth;
        textureSize[1] = TextureHeight;

        using var texture = new SafeTextureHandle(Structs.TextureUtility.Create2D(Device.Instance(), textureSize, 1, 0x2460, 0x80000804, 7), false);
        if (texture.IsInvalid)
            return;

        bool success;
        lock (_colorTable)
        {
            fixed (Half* colorTable = _colorTable)
            {
                success = Structs.TextureUtility.InitializeContents(texture.Texture, colorTable);
            }
        }

        if (success)
            texture.Exchange(ref *(nint*)_colorTableTexture);
    }

    protected override bool IsStillValid()
    {
        if (!base.IsStillValid())
            return false;

        var colorSetTextures = ((Structs.CharacterBaseExt*)DrawObject)->ColorTableTextures;
        if (colorSetTextures == null)
            return false;

        if (_colorTableTexture != colorSetTextures + (MaterialInfo.ModelSlot * 4 + MaterialInfo.MaterialSlot))
            return false;

        return true;
    }
}
