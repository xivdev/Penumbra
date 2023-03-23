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
    public readonly string             CollectionName;
    public readonly List<ResourceNode> Nodes;

    public ResourceTree(string name, nint sourceAddress, string collectionName)
    {
        Name           = name;
        SourceAddress  = sourceAddress;
        CollectionName = collectionName;
        Nodes          = new List<ResourceNode>();
    }

    internal unsafe void LoadResources(GlobalResolveContext globalContext)
    {
        var character = (Character*)SourceAddress;
        var model     = (CharacterBase*) character->GameObject.GetDrawObject();
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

        if (character->GameObject.GetObjectKind() == (byte) ObjectKind.Pc)
            AddHumanResources(globalContext, (HumanExt*)model);
    }

    private unsafe void AddHumanResources(GlobalResolveContext globalContext, HumanExt* human)
    {
        var prependIndex = 0;

        var firstWeapon = (WeaponExt*)human->Human.CharacterBase.DrawObject.Object.ChildObject;
        if (firstWeapon != null)
        {
            var weapon      = firstWeapon;
            var weaponIndex = 0;
            do
            {
                var weaponContext = globalContext.CreateContext(
                    slot: EquipSlot.MainHand,
                    equipment: new CharacterArmor(weapon->Weapon.ModelSetId, (byte)weapon->Weapon.Variant, (byte)weapon->Weapon.ModelUnknown)
                );

                var weaponMdlNode = weaponContext.CreateNodeFromRenderModel(*weapon->WeaponRenderModel);
                if (weaponMdlNode != null)
                    Nodes.Insert(prependIndex++,
                        globalContext.WithNames ? weaponMdlNode.WithName(weaponMdlNode.Name ?? $"Weapon Model #{weaponIndex}") : weaponMdlNode);

                weapon = (WeaponExt*)weapon->Weapon.CharacterBase.DrawObject.Object.NextSiblingObject;
                ++weaponIndex;
            } while (weapon != null && weapon != firstWeapon);
        }

        var context = globalContext.CreateContext(
            EquipSlot.Unknown,
            default
        );

        var skeletonNode = context.CreateHumanSkeletonNode((GenderRace) human->Human.RaceSexId);
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
