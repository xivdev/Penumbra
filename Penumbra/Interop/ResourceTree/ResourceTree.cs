using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Graphics.Render;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using FFXIVClientStructs.FFXIV.Client.System.Resource.Handle;
using Penumbra.GameData.Data;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Structs;
using Penumbra.Interop.Services;
using Penumbra.UI;
using CustomizeData = FFXIVClientStructs.FFXIV.Client.Game.Character.CustomizeData;
using CustomizeIndex = Dalamud.Game.ClientState.Objects.Enums.CustomizeIndex;

namespace Penumbra.Interop.ResourceTree;

public class ResourceTree
{
    public readonly string                Name;
    public readonly string                AnonymizedName;
    public readonly int                   GameObjectIndex;
    public readonly nint                  GameObjectAddress;
    public readonly nint                  DrawObjectAddress;
    public readonly bool                  LocalPlayerRelated;
    public readonly bool                  PlayerRelated;
    public readonly bool                  Networked;
    public readonly string                CollectionName;
    public readonly string                AnonymizedCollectionName;
    public readonly List<ResourceNode>    Nodes;
    public readonly HashSet<ResourceNode> FlatNodes;

    public int           ModelId;
    public CustomizeData CustomizeData;
    public GenderRace    RaceCode;

    public ResourceTree(string name, string anonymizedName, int gameObjectIndex, nint gameObjectAddress, nint drawObjectAddress, bool localPlayerRelated, bool playerRelated, bool networked, string collectionName, string anonymizedCollectionName)
    {
        Name                     = name;
        AnonymizedName           = anonymizedName;
        GameObjectIndex          = gameObjectIndex;
        GameObjectAddress        = gameObjectAddress;
        DrawObjectAddress        = drawObjectAddress;
        LocalPlayerRelated       = localPlayerRelated;
        Networked                = networked;
        PlayerRelated            = playerRelated;
        CollectionName           = collectionName;
        AnonymizedCollectionName = anonymizedCollectionName;
        Nodes                    = new List<ResourceNode>();
        FlatNodes                = new HashSet<ResourceNode>();
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
        var modelType = model->GetModelType();
        var human     = modelType == CharacterBase.ModelType.Human ? (Human*)model : null;
        var equipment = modelType switch
        {
            CharacterBase.ModelType.Human     => new ReadOnlySpan<CharacterArmor>(&human->Head, 10),
            CharacterBase.ModelType.DemiHuman => new ReadOnlySpan<CharacterArmor>(&character->DrawData.Head, 10),
            _                                 => ReadOnlySpan<CharacterArmor>.Empty,
        };
        ModelId       = character->CharacterData.ModelCharaId;
        CustomizeData = character->DrawData.CustomizeData;
        RaceCode      = human != null ? (GenderRace)human->RaceSexId : GenderRace.Unknown;

        var genericContext = globalContext.CreateContext(model);

        for (var i = 0; i < model->SlotCount; ++i)
        {
            var slotContext = i < equipment.Length
                ? globalContext.CreateContext(model, (uint)i, ((uint)i).ToEquipSlot(), equipment[i])
                : globalContext.CreateContext(model, (uint)i);

            var imc     = (ResourceHandle*)model->IMCArray[i];
            var imcNode = slotContext.CreateNodeFromImc(imc);
            if (imcNode != null)
            {
                if (globalContext.WithUiData)
                    imcNode.FallbackName = $"IMC #{i}";
                Nodes.Add(imcNode);
            }

            var mdl     = model->Models[i];
            var mdlNode = slotContext.CreateNodeFromModel(mdl, imc);
            if (mdlNode != null)
            {
                if (globalContext.WithUiData)
                    mdlNode.FallbackName = $"Model #{i}";
                Nodes.Add(mdlNode);
            }
        }

        AddSkeleton(Nodes, genericContext, model->EID, model->Skeleton);

        AddWeapons(globalContext, model);

        if (human != null)
            AddHumanResources(globalContext, human);
    }

    private unsafe void AddWeapons(GlobalResolveContext globalContext, CharacterBase* model)
    {
        var weaponIndex = 0;
        var weaponNodes = new List<ResourceNode>();
        foreach (var baseSubObject in model->DrawObject.Object.ChildObjects)
        {
            if (baseSubObject->GetObjectType() != FFXIVClientStructs.FFXIV.Client.Graphics.Scene.ObjectType.CharacterBase)
                continue;
            var subObject = (CharacterBase*)baseSubObject;

            if (subObject->GetModelType() != CharacterBase.ModelType.Weapon)
                continue;
            var weapon  = (Weapon*)subObject;

            // This way to tell apart MainHand and OffHand is not always accurate, but seems good enough for what we're doing with it.
            var slot       = weaponIndex > 0 ? EquipSlot.OffHand : EquipSlot.MainHand;
            var equipment  = new CharacterArmor(weapon->ModelSetId, (byte)weapon->Variant, (byte)weapon->ModelUnknown);
            var weaponType = weapon->SecondaryId;

            var genericContext = globalContext.CreateContext(subObject, 0xFFFFFFFFu, slot, equipment, weaponType);

            for (var i = 0; i < subObject->SlotCount; ++i)
            {
                var slotContext = globalContext.CreateContext(subObject, (uint)i, slot, equipment, weaponType);

                var imc     = (ResourceHandle*)subObject->IMCArray[i];
                var imcNode = slotContext.CreateNodeFromImc(imc);
                if (imcNode != null)
                {
                    if (globalContext.WithUiData)
                        imcNode.FallbackName = $"Weapon #{weaponIndex}, IMC #{i}";
                    weaponNodes.Add(imcNode);
                }

                var mdl     = subObject->Models[i];
                var mdlNode = slotContext.CreateNodeFromModel(mdl, imc);
                if (mdlNode != null)
                {
                    if (globalContext.WithUiData)
                        mdlNode.FallbackName = $"Weapon #{weaponIndex}, Model #{i}";
                    weaponNodes.Add(mdlNode);
                }
            }

            AddSkeleton(weaponNodes, genericContext, subObject->EID, subObject->Skeleton, $"Weapon #{weaponIndex}, ");

            ++weaponIndex;
        }
        Nodes.InsertRange(0, weaponNodes);
    }

    private unsafe void AddHumanResources(GlobalResolveContext globalContext, Human* human)
    {
        var genericContext = globalContext.CreateContext(&human->CharacterBase);

        var cache = globalContext.Collection._cache;
        if (cache != null && cache.CustomResources.TryGetValue(PreBoneDeformerReplacer.PreBoneDeformerPath, out var pbdHandle))
        {
            var pbdNode = genericContext.CreateNodeFromPbd(pbdHandle.ResourceHandle);
            if (pbdNode != null)
            {
                if (globalContext.WithUiData)
                {
                    pbdNode = pbdNode.Clone();
                    pbdNode.FallbackName = "Racial Deformer";
                    pbdNode.Icon = ChangedItemDrawer.ChangedItemIcon.Customization;
                }
                Nodes.Add(pbdNode);
            }
        }

        var decalId = (byte)(human->Customize[(int)CustomizeIndex.Facepaint] & 0x7F);
        var decalPath = decalId != 0
            ? GamePaths.Human.Decal.FaceDecalPath(decalId)
            : GamePaths.Tex.TransparentPath;
        var decalNode = genericContext.CreateNodeFromTex(human->Decal, decalPath);
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

        var hasLegacyDecal = (human->Customize[(int)CustomizeIndex.FaceFeatures] & 0x80) != 0;
        var legacyDecalPath = hasLegacyDecal
            ? GamePaths.Human.Decal.LegacyDecalPath
            : GamePaths.Tex.TransparentPath;
        var legacyDecalNode = genericContext.CreateNodeFromTex(human->LegacyBodyDecal, legacyDecalPath);
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

    private unsafe void AddSkeleton(List<ResourceNode> nodes, ResolveContext context, void* eid, Skeleton* skeleton, string prefix = "")
    {
        var eidNode = context.CreateNodeFromEid((ResourceHandle*)eid);
        if (eidNode != null)
        {
            if (context.Global.WithUiData)
                eidNode.FallbackName = $"{prefix}EID";
            Nodes.Add(eidNode);
        }

        if (skeleton == null)
            return;

        for (var i = 0; i < skeleton->PartialSkeletonCount; ++i)
        {
            var sklbNode = context.CreateNodeFromPartialSkeleton(&skeleton->PartialSkeletons[i], (uint)i);
            if (sklbNode != null)
            {
                if (context.Global.WithUiData)
                    sklbNode.FallbackName = $"{prefix}Skeleton #{i}";
                nodes.Add(sklbNode);
            }
        }
    }
}
