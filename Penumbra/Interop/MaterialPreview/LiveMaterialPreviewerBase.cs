using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Graphics.Render;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;

namespace Penumbra.Interop.MaterialPreview;

public abstract unsafe class LiveMaterialPreviewerBase : IDisposable
{
    private readonly IObjectTable _objects;

    public readonly    MaterialInfo   MaterialInfo;
    public readonly    CharacterBase* DrawObject;
    protected readonly Material*      Material;

    protected bool Valid;

    public LiveMaterialPreviewerBase(IObjectTable objects, MaterialInfo materialInfo)
    {
        _objects = objects;

        MaterialInfo = materialInfo;
        var gameObject = MaterialInfo.GetCharacter(objects);
        if (gameObject == nint.Zero)
            throw new InvalidOperationException("Cannot retrieve game object.");

        DrawObject = (CharacterBase*)MaterialInfo.GetDrawObject(gameObject);
        if (DrawObject == null)
            throw new InvalidOperationException("Cannot retrieve draw object.");

        Material = MaterialInfo.GetDrawObjectMaterial(DrawObject);
        if (Material == null)
            throw new InvalidOperationException("Cannot retrieve material.");

        Valid = true;
    }

    public void Dispose()
    {
        if (Valid)
            Clear(true, IsStillValid());
    }

    public bool CheckValidity()
    {
        if (Valid && !IsStillValid())
            Clear(false, false);
        return Valid;
    }

    protected virtual void Clear(bool disposing, bool reset)
    {
        Valid = false;
    }

    protected virtual bool IsStillValid()
    {
        var gameObject = MaterialInfo.GetCharacter(_objects);
        if (gameObject == nint.Zero)
            return false;

        if ((nint)DrawObject != MaterialInfo.GetDrawObject(gameObject))
            return false;

        if (Material != MaterialInfo.GetDrawObjectMaterial(DrawObject))
            return false;

        return true;
    }
}
