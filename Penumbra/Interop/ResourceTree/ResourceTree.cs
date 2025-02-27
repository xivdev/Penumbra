using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Graphics.Physics;
using FFXIVClientStructs.FFXIV.Client.Graphics.Render;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using FFXIVClientStructs.FFXIV.Client.System.Resource.Handle;
using FFXIVClientStructs.Interop;
using Penumbra.GameData.Data;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Structs;
using Penumbra.Interop.Hooks.PostProcessing;
using Penumbra.UI;
using CustomizeData = FFXIVClientStructs.FFXIV.Client.Game.Character.CustomizeData;
using CustomizeIndex = Dalamud.Game.ClientState.Objects.Enums.CustomizeIndex;
using ModelType = FFXIVClientStructs.FFXIV.Client.Graphics.Scene.CharacterBase.ModelType;
using ResourceHandle = FFXIVClientStructs.FFXIV.Client.System.Resource.Handle.ResourceHandle;

namespace Penumbra.Interop.ResourceTree;

public class ResourceTree(
    string name,
    string anonymizedName,
    int gameObjectIndex,
    nint gameObjectAddress,
    nint drawObjectAddress,
    bool localPlayerRelated,
    bool playerRelated,
    bool networked,
    string collectionName,
    string anonymizedCollectionName)
{
    public readonly string                Name                     = name;
    public readonly string                AnonymizedName           = anonymizedName;
    public readonly int                   GameObjectIndex          = gameObjectIndex;
    public readonly nint                  GameObjectAddress        = gameObjectAddress;
    public readonly nint                  DrawObjectAddress        = drawObjectAddress;
    public readonly bool                  LocalPlayerRelated       = localPlayerRelated;
    public readonly bool                  PlayerRelated            = playerRelated;
    public readonly bool                  Networked                = networked;
    public readonly string                CollectionName           = collectionName;
    public readonly string                AnonymizedCollectionName = anonymizedCollectionName;
    public readonly List<ResourceNode>    Nodes                    = [];
    public readonly HashSet<ResourceNode> FlatNodes                = [];

    public int           ModelId;
    public CustomizeData CustomizeData;
    public GenderRace    RaceCode;

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
        var human     = modelType == ModelType.Human ? (Human*)model : null;
        var equipment = modelType switch
        {
            ModelType.Human => new ReadOnlySpan<CharacterArmor>(&human->Head, 12),
            ModelType.DemiHuman => new ReadOnlySpan<CharacterArmor>(
                Unsafe.AsPointer(ref character->DrawData.EquipmentModelIds[0]), 10),
            _ => [],
        };
        ModelId       = character->ModelContainer.ModelCharaId;
        CustomizeData = character->DrawData.CustomizeData;
        RaceCode      = human is not null ? (GenderRace)human->RaceSexId : GenderRace.Unknown;

        var genericContext = globalContext.CreateContext(model);

        // TODO ClientStructs-ify (aers/FFXIVClientStructs#1312)
        var mpapArrayPtr = *(ResourceHandle***)((nint)model + 0x948);
        var mpapArray    = mpapArrayPtr is not null ? new ReadOnlySpan<Pointer<ResourceHandle>>(mpapArrayPtr, model->SlotCount) : [];
        var decalArray = modelType switch
        {
            ModelType.Human     => human->SlotDecalsSpan,
            ModelType.DemiHuman => ((Demihuman*)model)->SlotDecals,
            ModelType.Weapon    => [((Weapon*)model)->Decal],
            ModelType.Monster   => [((Monster*)model)->Decal],
            _                   => [],
        };

        for (var i = 0u; i < model->SlotCount; ++i)
        {
            var slotContext = modelType switch
            {
                ModelType.Human => i switch
                {
                    < 10 => globalContext.CreateContext(model, i, i.ToEquipSlot().ToEquipType(), equipment[(int)i]),
                    16   => globalContext.CreateContext(model, i, FullEquipType.Glasses,         equipment[10]),
                    17   => globalContext.CreateContext(model, i, FullEquipType.Unknown,         equipment[11]),
                    _    => globalContext.CreateContext(model, i),
                },
                _ => i < equipment.Length
                    ? globalContext.CreateContext(model, i, i.ToEquipSlot().ToEquipType(), equipment[(int)i])
                    : globalContext.CreateContext(model, i),
            };

            var imc = (ResourceHandle*)model->IMCArray[i];
            if (slotContext.CreateNodeFromImc(imc) is { } imcNode)
            {
                if (globalContext.WithUiData)
                    imcNode.FallbackName = $"IMC #{i}";
                Nodes.Add(imcNode);
            }

            var mdl = model->Models[i];
            if (slotContext.CreateNodeFromModel(mdl, imc, i < decalArray.Length ? decalArray[(int)i].Value : null,
                    i < mpapArray.Length ? mpapArray[(int)i].Value : null) is { } mdlNode)
            {
                if (globalContext.WithUiData)
                    mdlNode.FallbackName = $"Model #{i}";
                Nodes.Add(mdlNode);
            }
        }

        AddSkeleton(Nodes, genericContext, model->EID, model->Skeleton, model->BonePhysicsModule);
        // TODO ClientStructs-ify (aers/FFXIVClientStructs#1312)
        AddMaterialAnimationSkeleton(Nodes, genericContext, *(SkeletonResourceHandle**)((nint)model + 0x940));

        AddWeapons(globalContext, model);

        if (human is not null)
            AddHumanResources(globalContext, human);
    }

    private unsafe void AddWeapons(GlobalResolveContext globalContext, CharacterBase* model)
    {
        var weaponIndex = 0;
        var weaponNodes = new List<ResourceNode>();
        foreach (var baseSubObject in model->DrawObject.Object.ChildObjects)
        {
            if (baseSubObject->GetObjectType() is not FFXIVClientStructs.FFXIV.Client.Graphics.Scene.ObjectType.CharacterBase)
                continue;

            var subObject = (CharacterBase*)baseSubObject;

            if (subObject->GetModelType() is not ModelType.Weapon)
                continue;

            var weapon = (Weapon*)subObject;

            // This way to tell apart MainHand and OffHand is not always accurate, but seems good enough for what we're doing with it.
            var slot       = weaponIndex > 0 ? FullEquipType.UnknownOffhand : FullEquipType.UnknownMainhand;
            var equipment  = new CharacterArmor(weapon->ModelSetId, (byte)weapon->Variant, new StainIds(weapon->Stain0, weapon->Stain1));
            var weaponType = weapon->SecondaryId;

            var genericContext = globalContext.CreateContext(subObject, 0xFFFFFFFFu, slot, equipment, weaponType);

            // TODO ClientStructs-ify (aers/FFXIVClientStructs#1312)
            var mpapArrayPtr = *(ResourceHandle***)((nint)subObject + 0x948);
            var mpapArray    = mpapArrayPtr is not null ? new ReadOnlySpan<Pointer<ResourceHandle>>(mpapArrayPtr, subObject->SlotCount) : [];

            for (var i = 0; i < subObject->SlotCount; ++i)
            {
                var slotContext = globalContext.CreateContext(subObject, (uint)i, slot, equipment, weaponType);

                var imc = (ResourceHandle*)subObject->IMCArray[i];
                if (slotContext.CreateNodeFromImc(imc) is { } imcNode)
                {
                    if (globalContext.WithUiData)
                        imcNode.FallbackName = $"Weapon #{weaponIndex}, IMC #{i}";
                    weaponNodes.Add(imcNode);
                }

                var mdl = subObject->Models[i];
                if (slotContext.CreateNodeFromModel(mdl, imc, weapon->Decal, i < mpapArray.Length ? mpapArray[i].Value : null) is { } mdlNode)
                {
                    if (globalContext.WithUiData)
                        mdlNode.FallbackName = $"Weapon #{weaponIndex}, Model #{i}";
                    weaponNodes.Add(mdlNode);
                }
            }

            AddSkeleton(weaponNodes, genericContext, subObject->EID, subObject->Skeleton, subObject->BonePhysicsModule,
                $"Weapon #{weaponIndex}, ");
            // TODO ClientStructs-ify (aers/FFXIVClientStructs#1312)
            AddMaterialAnimationSkeleton(weaponNodes, genericContext, *(SkeletonResourceHandle**)((nint)subObject + 0x940),
                $"Weapon #{weaponIndex}, ");

            ++weaponIndex;
        }

        Nodes.InsertRange(0, weaponNodes);
    }

    private unsafe void AddHumanResources(GlobalResolveContext globalContext, Human* human)
    {
        var genericContext = globalContext.CreateContext(&human->CharacterBase);

        var cache = globalContext.Collection._cache;
        if (cache is not null
         && cache.CustomResources.TryGetValue(PreBoneDeformerReplacer.PreBoneDeformerPath, out var pbdHandle)
         && genericContext.CreateNodeFromPbd(pbdHandle.ResourceHandle) is { } pbdNode)
        {
            if (globalContext.WithUiData)
            {
                pbdNode              = pbdNode.Clone();
                pbdNode.FallbackName = "Racial Deformer";
                pbdNode.IconFlag     = ChangedItemIconFlag.Customization;
            }

            Nodes.Add(pbdNode);
        }

        var decalId = (byte)(human->Customize[(int)CustomizeIndex.Facepaint] & 0x7F);
        var decalPath = decalId is not 0
            ? GamePaths.Tex.FaceDecal(decalId)
            : GamePaths.Tex.Transparent;
        if (genericContext.CreateNodeFromTex(human->Decal, decalPath) is { } decalNode)
        {
            if (globalContext.WithUiData)
            {
                decalNode              = decalNode.Clone();
                decalNode.FallbackName = "Face Decal";
                decalNode.IconFlag     = ChangedItemIconFlag.Customization;
            }

            Nodes.Add(decalNode);
        }

        var hasLegacyDecal = (human->Customize[(int)CustomizeIndex.FaceFeatures] & 0x80) != 0;
        var legacyDecalPath = hasLegacyDecal
            ? GamePaths.Tex.LegacyDecal
            : GamePaths.Tex.Transparent;
        if (genericContext.CreateNodeFromTex(human->LegacyBodyDecal, legacyDecalPath) is { } legacyDecalNode)
        {
            legacyDecalNode.ForceProtected = !hasLegacyDecal;
            if (globalContext.WithUiData)
            {
                legacyDecalNode              = legacyDecalNode.Clone();
                legacyDecalNode.FallbackName = "Legacy Body Decal";
                legacyDecalNode.IconFlag     = ChangedItemIconFlag.Customization;
            }

            Nodes.Add(legacyDecalNode);
        }
    }

    private unsafe void AddSkeleton(List<ResourceNode> nodes, ResolveContext context, void* eid, Skeleton* skeleton, BonePhysicsModule* physics,
        string prefix = "")
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
            // TODO ClientStructs-ify (aers/FFXIVClientStructs#1312)
            var phybHandle = physics != null ? ((ResourceHandle**)((nint)physics + 0x190))[i] : null;
            if (context.CreateNodeFromPartialSkeleton(&skeleton->PartialSkeletons[i], phybHandle, (uint)i) is { } sklbNode)
            {
                if (context.Global.WithUiData)
                    sklbNode.FallbackName = $"{prefix}Skeleton #{i}";
                nodes.Add(sklbNode);
            }
        }
    }

    private unsafe void AddMaterialAnimationSkeleton(List<ResourceNode> nodes, ResolveContext context, SkeletonResourceHandle* sklbHandle,
        string prefix = "")
    {
        var sklbNode = context.CreateNodeFromMaterialSklb(sklbHandle);
        if (sklbNode is null)
            return;

        if (context.Global.WithUiData)
            sklbNode.FallbackName = $"{prefix}Material Animation Skeleton";
        nodes.Add(sklbNode);
    }
}
