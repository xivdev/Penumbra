using System;
using System.Threading;
using Dalamud.Game;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Graphics.Kernel;
using FFXIVClientStructs.FFXIV.Client.Graphics.Render;
using Penumbra.GameData.Files;
using Penumbra.Interop.SafeHandles;

namespace Penumbra.Interop.MaterialPreview;

public sealed unsafe class LiveColorSetPreviewer : LiveMaterialPreviewerBase
{
    public const int TextureWidth  = 4;
    public const int TextureHeight = MtrlFile.ColorSet.RowArray.NumRows;
    public const int TextureLength = TextureWidth * TextureHeight * 4;

    private readonly Framework _framework;

    private readonly Texture**         _colorSetTexture;
    private readonly SafeTextureHandle _originalColorSetTexture;

    private Half[] _colorSet;
    private bool   _updatePending;

    public Half[] ColorSet
        => _colorSet;

    public LiveColorSetPreviewer(IObjectTable objects, Framework framework, MaterialInfo materialInfo)
        : base(objects, materialInfo)
    {
        _framework = framework;

        var mtrlHandle = Material->MaterialResourceHandle;
        if (mtrlHandle == null)
            throw new InvalidOperationException("Material doesn't have a resource handle");

        var colorSetTextures = ((Structs.CharacterBaseExt*)DrawObject)->ColorSetTextures;
        if (colorSetTextures == null)
            throw new InvalidOperationException("Draw object doesn't have color set textures");

        _colorSetTexture = colorSetTextures + (MaterialInfo.ModelSlot * 4 + MaterialInfo.MaterialSlot);

        _originalColorSetTexture = new SafeTextureHandle(*_colorSetTexture, true);
        if (_originalColorSetTexture == null)
            throw new InvalidOperationException("Material doesn't have a color set");

        _colorSet      = new Half[TextureLength];
        _updatePending = true;

        framework.Update += OnFrameworkUpdate;
    }

    protected override void Clear(bool disposing, bool reset)
    {
        _framework.Update -= OnFrameworkUpdate;

        base.Clear(disposing, reset);

        if (reset)
            _originalColorSetTexture.Exchange(ref *(nint*)_colorSetTexture);

        _originalColorSetTexture.Dispose();
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
        lock (_colorSet)
        {
            fixed (Half* colorSet = _colorSet)
            {
                success = Structs.TextureUtility.InitializeContents(texture.Texture, colorSet);
            }
        }

        if (success)
            texture.Exchange(ref *(nint*)_colorSetTexture);
    }

    protected override bool IsStillValid()
    {
        if (!base.IsStillValid())
            return false;

        var colorSetTextures = ((Structs.CharacterBaseExt*)DrawObject)->ColorSetTextures;
        if (colorSetTextures == null)
            return false;

        if (_colorSetTexture != colorSetTextures + (MaterialInfo.ModelSlot * 4 + MaterialInfo.MaterialSlot))
            return false;

        return true;
    }
}
