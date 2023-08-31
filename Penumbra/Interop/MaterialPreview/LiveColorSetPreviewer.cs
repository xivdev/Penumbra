using System;
using System.Threading;
using Dalamud.Game;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Graphics.Kernel;
using FFXIVClientStructs.FFXIV.Client.Graphics.Render;
using Penumbra.GameData.Files;

namespace Penumbra.Interop.MaterialPreview;

public sealed unsafe class LiveColorSetPreviewer : LiveMaterialPreviewerBase
{
    public const int TextureWidth  = 4;
    public const int TextureHeight = MtrlFile.ColorSet.RowArray.NumRows;
    public const int TextureLength = TextureWidth * TextureHeight * 4;

    private readonly Framework _framework;

    private readonly Texture** _colorSetTexture;
    private readonly Texture*  _originalColorSetTexture;

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

        _originalColorSetTexture = *_colorSetTexture;
        if (_originalColorSetTexture == null)
            throw new InvalidOperationException("Material doesn't have a color set");

        Structs.TextureUtility.IncRef(_originalColorSetTexture);

        _colorSet      = new Half[TextureLength];
        _updatePending = true;

        framework.Update += OnFrameworkUpdate;
    }

    protected override void Clear(bool disposing, bool reset)
    {
        _framework.Update -= OnFrameworkUpdate;

        base.Clear(disposing, reset);

        if (reset)
        {
            var oldTexture = (Texture*)Interlocked.Exchange(ref *(nint*)_colorSetTexture, (nint)_originalColorSetTexture);
            if (oldTexture != null)
                Structs.TextureUtility.DecRef(oldTexture);
        }
        else
        {
            Structs.TextureUtility.DecRef(_originalColorSetTexture);
        }
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

        var newTexture = Structs.TextureUtility.Create2D(Device.Instance(), textureSize, 1, 0x2460, 0x80000804, 7);
        if (newTexture == null)
            return;

        bool success;
        lock (_colorSet)
        {
            fixed (Half* colorSet = _colorSet)
            {
                success = Structs.TextureUtility.InitializeContents(newTexture, colorSet);
            }
        }

        if (success)
        {
            var oldTexture = (Texture*)Interlocked.Exchange(ref *(nint*)_colorSetTexture, (nint)newTexture);
            if (oldTexture != null)
                Structs.TextureUtility.DecRef(oldTexture);
        }
        else
        {
            Structs.TextureUtility.DecRef(newTexture);
        }
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
