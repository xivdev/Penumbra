using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Graphics.Render;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using Penumbra.Interop.ResourceTree;
using Penumbra.String;

namespace Penumbra.Interop.MaterialPreview;

public enum DrawObjectType
{
    PlayerCharacter,
    PlayerMainhand,
    PlayerOffhand,
    PlayerVfx,
    MinionCharacter,
    MinionUnk1,
    MinionUnk2,
    MinionUnk3,
};

public readonly record struct MaterialInfo(DrawObjectType Type, int ModelSlot, int MaterialSlot)
{
    public nint GetCharacter(IObjectTable objects)
        => GetCharacter(Type, objects);

    public static nint GetCharacter(DrawObjectType type, IObjectTable objects)
        => type switch
        {
            DrawObjectType.PlayerCharacter => objects.GetObjectAddress(0),
            DrawObjectType.PlayerMainhand  => objects.GetObjectAddress(0),
            DrawObjectType.PlayerOffhand   => objects.GetObjectAddress(0),
            DrawObjectType.PlayerVfx       => objects.GetObjectAddress(0),
            DrawObjectType.MinionCharacter => objects.GetObjectAddress(1),
            DrawObjectType.MinionUnk1      => objects.GetObjectAddress(1),
            DrawObjectType.MinionUnk2      => objects.GetObjectAddress(1),
            DrawObjectType.MinionUnk3      => objects.GetObjectAddress(1),
            _                              => nint.Zero,
        };

    public nint GetDrawObject(nint address)
        => GetDrawObject(Type, address);

    public static nint GetDrawObject(DrawObjectType type, IObjectTable objects)
        => GetDrawObject(type, GetCharacter(type, objects));

    public static unsafe nint GetDrawObject(DrawObjectType type, nint address)
    {
        var gameObject = (Character*)address;
        if (gameObject == null)
            return nint.Zero;

        return type switch
        {
            DrawObjectType.PlayerCharacter => (nint)gameObject->GameObject.GetDrawObject(),
            DrawObjectType.PlayerMainhand  => *((nint*)&gameObject->DrawData.MainHand + 1),
            DrawObjectType.PlayerOffhand   => *((nint*)&gameObject->DrawData.OffHand + 1),
            DrawObjectType.PlayerVfx       => *((nint*)&gameObject->DrawData.UnkF0 + 1),
            DrawObjectType.MinionCharacter => (nint)gameObject->GameObject.GetDrawObject(),
            DrawObjectType.MinionUnk1      => *((nint*)&gameObject->DrawData.MainHand + 1),
            DrawObjectType.MinionUnk2      => *((nint*)&gameObject->DrawData.OffHand + 1),
            DrawObjectType.MinionUnk3      => *((nint*)&gameObject->DrawData.UnkF0 + 1),
            _                              => nint.Zero,
        };
    }

    public unsafe Material* GetDrawObjectMaterial(CharacterBase* drawObject)
    {
        if (drawObject == null)
            return null;

        if (ModelSlot < 0 || ModelSlot >= drawObject->SlotCount)
            return null;

        var model = drawObject->Models[ModelSlot];
        if (model == null)
            return null;

        if (MaterialSlot < 0 || MaterialSlot >= model->MaterialCount)
            return null;

        return model->Materials[MaterialSlot];
    }

    public static unsafe List<MaterialInfo> FindMaterials(IObjectTable objects, string materialPath)
    {
        var needle = ByteString.FromString(materialPath.Replace('\\', '/'), out var m, true) ? m : ByteString.Empty;

        var result = new List<MaterialInfo>(Enum.GetValues<DrawObjectType>().Length);
        foreach (var type in Enum.GetValues<DrawObjectType>())
        {
            var drawObject = (CharacterBase*)GetDrawObject(type, objects);
            if (drawObject == null)
                continue;

            for (var i = 0; i < drawObject->SlotCount; ++i)
            {
                var model = drawObject->Models[i];
                if (model == null)
                    continue;

                for (var j = 0; j < model->MaterialCount; ++j)
                {
                    var material = model->Materials[j];
                    if (material == null)
                        continue;

                    var mtrlHandle = material->MaterialResourceHandle;
                    var path       = ResolveContext.GetResourceHandlePath((Structs.ResourceHandle*)mtrlHandle);
                    if (path == needle)
                        result.Add(new MaterialInfo(type, i, j));
                }
            }
        }

        return result;
    }
}
