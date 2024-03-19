using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Graphics.Render;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using Penumbra.GameData.Interop;
using Penumbra.GameData.Structs;
using Penumbra.Interop.ResourceTree;
using Penumbra.String;
using Model = Penumbra.GameData.Interop.Model;

namespace Penumbra.Interop.MaterialPreview;

public enum DrawObjectType
{
    Character,
    Mainhand,
    Offhand,
    Vfx,
};

public readonly record struct MaterialInfo(ObjectIndex ObjectIndex, DrawObjectType Type, int ModelSlot, int MaterialSlot)
{
    public Actor GetCharacter(ObjectManager objects)
        => objects[ObjectIndex];

    public nint GetDrawObject(Actor address)
        => GetDrawObject(Type, address);

    public unsafe Material* GetDrawObjectMaterial(ObjectManager objects)
        => GetDrawObjectMaterial((CharacterBase*)GetDrawObject(GetCharacter(objects)));

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

    public static unsafe List<MaterialInfo> FindMaterials(IEnumerable<nint> gameObjects, string materialPath)
    {
        var needle = ByteString.FromString(materialPath.Replace('\\', '/'), out var m, true) ? m : ByteString.Empty;

        var result = new List<MaterialInfo>(Enum.GetValues<DrawObjectType>().Length);
        foreach (var objectPtr in gameObjects)
        {
            var gameObject = (Character*)objectPtr;
            if (gameObject == null)
                continue;

            var index = (ObjectIndex)gameObject->GameObject.ObjectIndex;

            foreach (var type in Enum.GetValues<DrawObjectType>())
            {
                var drawObject = GetDrawObject(type, objectPtr);
                if (!drawObject.Valid)
                    continue;

                for (var i = 0; i < drawObject.AsCharacterBase->SlotCount; ++i)
                {
                    var model = drawObject.AsCharacterBase->Models[i];
                    if (model == null)
                        continue;

                    for (var j = 0; j < model->MaterialCount; ++j)
                    {
                        var material = model->Materials[j];
                        if (material == null)
                            continue;

                        var mtrlHandle = material->MaterialResourceHandle;
                        var path       = ResolveContext.GetResourceHandlePath(&mtrlHandle->ResourceHandle);
                        if (path == needle)
                            result.Add(new MaterialInfo(index, type, i, j));
                    }
                }
            }
        }

        return result;
    }

    private static unsafe Model GetDrawObject(DrawObjectType type, Actor address)
    {
        if (!address.Valid)
            return Model.Null;

        return type switch
        {
            DrawObjectType.Character => address.Model,
            DrawObjectType.Mainhand  => address.AsCharacter->DrawData.Weapon(DrawDataContainer.WeaponSlot.MainHand).DrawObject,
            DrawObjectType.Offhand   => address.AsCharacter->DrawData.Weapon(DrawDataContainer.WeaponSlot.OffHand).DrawObject,
            DrawObjectType.Vfx       => address.AsCharacter->DrawData.Weapon(DrawDataContainer.WeaponSlot.Unk).DrawObject,
            _                        => Model.Null,
        };
    }
}
