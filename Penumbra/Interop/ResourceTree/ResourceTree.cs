using System;
using System.Collections.Generic;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Structs;
using Penumbra.Interop.Structs;

namespace Penumbra.Interop.ResourceTree;

public class ResourceTree
{
    public readonly string             Name;
    public readonly nint               SourceAddress;
    public readonly bool               PlayerRelated;
    public readonly string             CollectionName;
    public readonly List<ResourceNode> Nodes;

    public ResourceTree(string name, nint sourceAddress, bool playerRelated, string collectionName)
    {
        Name           = name;
        SourceAddress  = sourceAddress;
        PlayerRelated  = playerRelated;
        CollectionName = collectionName;
        Nodes          = new List<ResourceNode>();
    }

    internal unsafe void LoadResources(GlobalResolveContext globalContext)
    {
        var character = (Character*)SourceAddress;
        var model     = (CharacterBase*)character->GameObject.GetDrawObject();
        var equipment = new ReadOnlySpan<CharacterArmor>(character->EquipSlotData, 10);
        // var customize = new ReadOnlySpan<byte>( character->CustomizeData, 26 );

        for (var i = 0; i < model->SlotCount; ++i)
        {
            var context = globalContext.CreateContext(
                i < equipment.Length ? ((uint)i).ToEquipSlot() : EquipSlot.Unknown,
                i < equipment.Length ? equipment[i] : default
            );

            var imc     = (ResourceHandle*)model->IMCArray[i];
            var imcNode = context.CreateNodeFromImc(imc);
            if (imcNode != null)
                Nodes.Add(globalContext.WithNames ? imcNode.WithName(imcNode.Name ?? $"IMC #{i}") : imcNode);

            var mdl     = (RenderModel*)model->ModelArray[i];
            var mdlNode = context.CreateNodeFromRenderModel(mdl);
            if (mdlNode != null)
                Nodes.Add(globalContext.WithNames ? mdlNode.WithName(mdlNode.Name ?? $"Model #{i}") : mdlNode);
        }

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
                        subObjectNodes.Add(globalContext.WithNames
                            ? imcNode.WithName(imcNode.Name ?? $"{subObjectNamePrefix} #{subObjectIndex}, IMC #{i}")
                            : imcNode);

                    var mdl     = (RenderModel*)subObject->ModelArray[i];
                    var mdlNode = subObjectContext.CreateNodeFromRenderModel(mdl);
                    if (mdlNode != null)
                        subObjectNodes.Add(globalContext.WithNames
                            ? mdlNode.WithName(mdlNode.Name ?? $"{subObjectNamePrefix} #{subObjectIndex}, Model #{i}")
                            : mdlNode);
                }

                subObject = (CharacterBase*)subObject->DrawObject.Object.NextSiblingObject;
                ++subObjectIndex;
            } while (subObject != null && subObject != firstSubObject);

            Nodes.InsertRange(0, subObjectNodes);
        }

        var context = globalContext.CreateContext(EquipSlot.Unknown, default);

        var skeletonNode = context.CreateHumanSkeletonNode((GenderRace)human->Human.RaceSexId);
        if (skeletonNode != null)
            Nodes.Add(globalContext.WithNames ? skeletonNode.WithName(skeletonNode.Name ?? "Skeleton") : skeletonNode);

        var decalNode = context.CreateNodeFromTex(human->Decal);
        if (decalNode != null)
            Nodes.Add(globalContext.WithNames ? decalNode.WithName(decalNode.Name ?? "Face Decal") : decalNode);

        var legacyDecalNode = context.CreateNodeFromTex(human->LegacyBodyDecal);
        if (legacyDecalNode != null)
            Nodes.Add(globalContext.WithNames ? legacyDecalNode.WithName(legacyDecalNode.Name ?? "Legacy Body Decal") : legacyDecalNode);
    }
}
