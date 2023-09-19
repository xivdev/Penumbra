using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Graphics.Render;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Structs;
using Penumbra.Interop.Structs;
using Penumbra.UI;
using CustomizeData = FFXIVClientStructs.FFXIV.Client.Game.Character.CustomizeData;

namespace Penumbra.Interop.ResourceTree;

public class ResourceTree
{
    public readonly string                Name;
    public readonly int                   GameObjectIndex;
    public readonly nint                  GameObjectAddress;
    public readonly nint                  DrawObjectAddress;
    public readonly bool                  LocalPlayerRelated;
    public readonly bool                  PlayerRelated;
    public readonly bool                  Networked;
    public readonly string                CollectionName;
    public readonly List<ResourceNode>    Nodes;
    public readonly HashSet<ResourceNode> FlatNodes;

    public int           ModelId;
    public CustomizeData CustomizeData;
    public GenderRace    RaceCode;

    public ResourceTree(string name, int gameObjectIndex, nint gameObjectAddress, nint drawObjectAddress, bool localPlayerRelated, bool playerRelated, bool networked, string collectionName)
    {
        Name               = name;
        GameObjectIndex    = gameObjectIndex;
        GameObjectAddress  = gameObjectAddress;
        DrawObjectAddress  = drawObjectAddress;
        LocalPlayerRelated = localPlayerRelated;
        Networked          = networked;
        PlayerRelated      = playerRelated;
        CollectionName     = collectionName;
        Nodes              = new List<ResourceNode>();
        FlatNodes          = new HashSet<ResourceNode>();
    }

    public void ProcessPostfix(Action<ResourceNode, ResourceNode?> action)
    {
        foreach (var node in Nodes)
            node.ProcessPostfix(action, null);
    }

    internal unsafe void LoadResources(GlobalResolveContext globalContext)
    {
        var character = (Character*)GameObjectAddress;
        var model     = (CharacterBase*)DrawObjectAddress;
        var equipment = new ReadOnlySpan<CharacterArmor>(&character->DrawData.Head, 10);
        // var customize = new ReadOnlySpan<byte>( character->CustomizeData, 26 );
        ModelId       = character->CharacterData.ModelCharaId;
        CustomizeData = character->DrawData.CustomizeData;
        RaceCode      = model->GetModelType() == CharacterBase.ModelType.Human ? (GenderRace)((Human*)model)->RaceSexId : GenderRace.Unknown;

        for (var i = 0; i < model->SlotCount; ++i)
        {
            var context = globalContext.CreateContext(
                i < equipment.Length ? ((uint)i).ToEquipSlot() : EquipSlot.Unknown,
                i < equipment.Length ? equipment[i] : default
            );

            var imc     = (ResourceHandle*)model->IMCArray[i];
            var imcNode = context.CreateNodeFromImc(imc);
            if (imcNode != null)
            {
                if (globalContext.WithUiData)
                    imcNode.FallbackName = $"IMC #{i}";
                Nodes.Add(imcNode);
            }

            var mdl     = (RenderModel*)model->Models[i];
            var mdlNode = context.CreateNodeFromRenderModel(mdl);
            if (mdlNode != null)
            {
                if (globalContext.WithUiData)
                    mdlNode.FallbackName = $"Model #{i}";
                Nodes.Add(mdlNode);
            }
        }

        AddSkeleton(Nodes, globalContext.CreateContext(EquipSlot.Unknown, default), model->Skeleton);

        if (model->GetModelType() == CharacterBase.ModelType.Human)
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
                    {
                        if (globalContext.WithUiData)
                            imcNode.FallbackName = $"{subObjectNamePrefix} #{subObjectIndex}, IMC #{i}";
                        subObjectNodes.Add(imcNode);
                    }

                    var mdl     = (RenderModel*)subObject->Models[i];
                    var mdlNode = subObjectContext.CreateNodeFromRenderModel(mdl);
                    if (mdlNode != null)
                    {
                        if (globalContext.WithUiData)
                            mdlNode.FallbackName = $"{subObjectNamePrefix} #{subObjectIndex}, Model #{i}";
                        subObjectNodes.Add(mdlNode);
                    }
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
        {
            if (globalContext.WithUiData)
            {
                decalNode = decalNode.Clone();
                decalNode.FallbackName = "Face Decal";
                decalNode.Icon         = ChangedItemDrawer.ChangedItemIcon.Customization;
            }
            Nodes.Add(decalNode);
        }

        var legacyDecalNode = context.CreateNodeFromTex((TextureResourceHandle*)human->LegacyBodyDecal);
        if (legacyDecalNode != null)
        {
            if (globalContext.WithUiData)
            {
                legacyDecalNode = legacyDecalNode.Clone();
                legacyDecalNode.FallbackName = "Legacy Body Decal";
                legacyDecalNode.Icon         = ChangedItemDrawer.ChangedItemIcon.Customization;
            }
            Nodes.Add(legacyDecalNode);
        }
    }

    private unsafe void AddSkeleton(List<ResourceNode> nodes, ResolveContext context, Skeleton* skeleton, string prefix = "")
    {
        if (skeleton == null)
            return;

        for (var i = 0; i < skeleton->PartialSkeletonCount; ++i)
        {
            var sklbNode = context.CreateNodeFromPartialSkeleton(&skeleton->PartialSkeletons[i]);
            if (sklbNode != null)
            {
                if (context.WithUiData)
                    sklbNode.FallbackName = $"{prefix}Skeleton #{i}";
                nodes.Add(sklbNode);
            }
        }
    }
}
