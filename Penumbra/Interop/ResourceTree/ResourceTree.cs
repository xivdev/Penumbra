using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.Graphics.Render;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Structs;
using Penumbra.Interop.Structs;
using CustomizeData = FFXIVClientStructs.FFXIV.Client.Game.Character.CustomizeData;

namespace Penumbra.Interop.ResourceTree;

public class ResourceTree
{
    public readonly string                Name;
    public readonly nint                  GameObjectAddress;
    public readonly nint                  DrawObjectAddress;
    public readonly bool                  PlayerRelated;
    public readonly string                CollectionName;
    public readonly List<ResourceNode>    Nodes;
    public readonly HashSet<ResourceNode> FlatNodes;

    public int           ModelId;
    public CustomizeData CustomizeData;
    public GenderRace    RaceCode;

    public ResourceTree(string name, nint gameObjectAddress, nint drawObjectAddress, bool playerRelated, string collectionName)
    {
        Name              = name;
        GameObjectAddress = gameObjectAddress;
        DrawObjectAddress = drawObjectAddress;
        PlayerRelated     = playerRelated;
        CollectionName    = collectionName;
        Nodes             = new List<ResourceNode>();
        FlatNodes         = new HashSet<ResourceNode>();
    }

    internal unsafe void LoadResources(GlobalResolveContext globalContext)
    {
        var character = (Character*)GameObjectAddress;
        var model     = (CharacterBase*)DrawObjectAddress;
        var equipment = new ReadOnlySpan<CharacterArmor>(&character->DrawData.Head, 10);
        // var customize = new ReadOnlySpan<byte>( character->CustomizeData, 26 );
        ModelId       = character->CharacterData.ModelCharaId;
        CustomizeData = character->DrawData.CustomizeData;
        RaceCode      = model->GetModelType() == CharacterBase.ModelType.Human ? (GenderRace) ((Human*)model)->RaceSexId : GenderRace.Unknown;

        for (var i = 0; i < model->SlotCount; ++i)
        {
            var context = globalContext.CreateContext(
                i < equipment.Length ? ((uint)i).ToEquipSlot() : EquipSlot.Unknown,
                i < equipment.Length ? equipment[i] : default
            );

            var imc     = (ResourceHandle*)model->IMCArray[i];
            var imcNode = context.CreateNodeFromImc(imc);
            if (imcNode != null)
                Nodes.Add(globalContext.WithUiData ? imcNode.WithUIData(imcNode.Name ?? $"IMC #{i}", imcNode.Icon) : imcNode);

            var mdl     = (RenderModel*)model->Models[i];
            var mdlNode = context.CreateNodeFromRenderModel(mdl);
            if (mdlNode != null)
                Nodes.Add(globalContext.WithUiData ? mdlNode.WithUIData(mdlNode.Name ?? $"Model #{i}", mdlNode.Icon) : mdlNode);
        }

        AddSkeleton(Nodes, globalContext.CreateContext(EquipSlot.Unknown, default), model->Skeleton);

        if (character->GameObject.GetObjectKind() == (byte)ObjectKind.Pc)
            AddHumanResources(globalContext, (HumanExt*)model);
    }

    private unsafe void AddHumanResources(GlobalResolveContext globalContext, HumanExt* human)
    {
        var firstSubObject = (CharacterBase*)human->Human.CharacterBase.DrawObject.Object.ChildObject;
        if (firstSubObject != null)
        {
            var subObjectNodes = new List<ResourceNode>();
            var subObject      = firstSubObject;
            var subObjectIndex = 0;
            do
            {
                var weapon              = subObject->GetModelType() == CharacterBase.ModelType.Weapon ? (Weapon*)subObject : null;
                var subObjectNamePrefix = weapon != null ? "Weapon" : "Fashion Acc.";
                var subObjectContext = globalContext.CreateContext(
                    weapon != null ? EquipSlot.MainHand : EquipSlot.Unknown,
                    weapon != null ? new CharacterArmor(weapon->ModelSetId, (byte)weapon->Variant, (byte)weapon->ModelUnknown) : default
                );

                for (var i = 0; i < subObject->SlotCount; ++i)
                {
                    var imc     = (ResourceHandle*)subObject->IMCArray[i];
                    var imcNode = subObjectContext.CreateNodeFromImc(imc);
                    if (imcNode != null)
                        subObjectNodes.Add(globalContext.WithUiData
                            ? imcNode.WithUIData(imcNode.Name ?? $"{subObjectNamePrefix} #{subObjectIndex}, IMC #{i}", imcNode.Icon)
                            : imcNode);

                    var mdl     = (RenderModel*)subObject->Models[i];
                    var mdlNode = subObjectContext.CreateNodeFromRenderModel(mdl);
                    if (mdlNode != null)
                        subObjectNodes.Add(globalContext.WithUiData
                            ? mdlNode.WithUIData(mdlNode.Name ?? $"{subObjectNamePrefix} #{subObjectIndex}, Model #{i}", mdlNode.Icon)
                            : mdlNode);
                }

                AddSkeleton(subObjectNodes, subObjectContext, subObject->Skeleton, $"{subObjectNamePrefix} #{subObjectIndex}, ");

                subObject = (CharacterBase*)subObject->DrawObject.Object.NextSiblingObject;
                ++subObjectIndex;
            } while (subObject != null && subObject != firstSubObject);

            Nodes.InsertRange(0, subObjectNodes);
        }

        var context = globalContext.CreateContext(EquipSlot.Unknown, default);

        var decalNode = context.CreateNodeFromTex((TextureResourceHandle*)human->Decal);
        if (decalNode != null)
            Nodes.Add(globalContext.WithUiData ? decalNode.WithUIData(decalNode.Name ?? "Face Decal", decalNode.Icon) : decalNode);

        var legacyDecalNode = context.CreateNodeFromTex((TextureResourceHandle*)human->LegacyBodyDecal);
        if (legacyDecalNode != null)
            Nodes.Add(globalContext.WithUiData ? legacyDecalNode.WithUIData(legacyDecalNode.Name ?? "Legacy Body Decal", legacyDecalNode.Icon) : legacyDecalNode);
    }

    private unsafe void AddSkeleton(List<ResourceNode> nodes, ResolveContext context, Skeleton* skeleton, string prefix = "")
    {
        if (skeleton == null)
            return;

        for (var i = 0; i < skeleton->PartialSkeletonCount; ++i)
        {
            var sklbNode = context.CreateNodeFromPartialSkeleton(&skeleton->PartialSkeletons[i]);
            if (sklbNode != null)
                nodes.Add(context.WithUiData ? sklbNode.WithUIData($"{prefix}Skeleton #{i}", sklbNode.Icon) : sklbNode);
        }
    }
}
