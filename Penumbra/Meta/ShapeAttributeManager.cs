using OtterGui.Services;
using Penumbra.Collections;
using Penumbra.Collections.Cache;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Interop;
using Penumbra.GameData.Structs;
using Penumbra.Interop.Hooks.PostProcessing;
using Penumbra.Meta.Manipulations;

namespace Penumbra.Meta;

public unsafe class ShapeAttributeManager : IRequiredService, IDisposable
{
    public const     int           NumSlots      = 14;
    public const     int           ModelSlotSize = 18;
    private readonly AttributeHook _attributeHook;

    public static ReadOnlySpan<HumanSlot> UsedModels
        =>
        [
            HumanSlot.Head, HumanSlot.Body, HumanSlot.Hands, HumanSlot.Legs, HumanSlot.Feet, HumanSlot.Ears, HumanSlot.Neck, HumanSlot.Wrists,
            HumanSlot.RFinger, HumanSlot.LFinger, HumanSlot.Glasses, HumanSlot.Hair, HumanSlot.Face, HumanSlot.Ear,
        ];

    public ShapeAttributeManager(AttributeHook attributeHook)
    {
        _attributeHook = attributeHook;
        _attributeHook.Subscribe(OnAttributeComputed, AttributeHook.Priority.ShapeAttributeManager);
    }

    private readonly Dictionary<ShapeAttributeString, short>[] _temporaryShapes =
        Enumerable.Range(0, NumSlots).Select(_ => new Dictionary<ShapeAttributeString, short>()).ToArray();

    private readonly PrimaryId[] _ids = new PrimaryId[ModelSlotSize];

    private HumanSlot  _modelIndex;
    private int        _slotIndex;
    private GenderRace _genderRace;

    private FFXIVClientStructs.FFXIV.Client.Graphics.Render.Model* _model;

    public void Dispose()
        => _attributeHook.Unsubscribe(OnAttributeComputed);

    private void OnAttributeComputed(Actor actor, Model model, ModCollection collection)
    {
        if (!collection.HasCache)
            return;

        _genderRace = (GenderRace)model.AsHuman->RaceSexId;
        for (_slotIndex = 0; _slotIndex < NumSlots; ++_slotIndex)
        {
            _modelIndex = UsedModels[_slotIndex];
            _model      = model.AsHuman->Models[_modelIndex.ToIndex()];
            if (_model is null || _model->ModelResourceHandle is null)
                continue;

            _ids[(int)_modelIndex] = model.GetModelId(_modelIndex);
            CheckShapes(collection.MetaCache!.Shp);
            CheckAttributes(collection.MetaCache!.Atr);
            if (_modelIndex is <= HumanSlot.LFinger and >= HumanSlot.Ears)
                AccessoryImcCheck(model);
        }

        UpdateDefaultMasks(model, collection.MetaCache!.Shp);
    }

    private void AccessoryImcCheck(Model model)
    {
        var imcMask = (ushort)(0x03FF & *(ushort*)(model.Address + 0xAAC + 6 * (int)_modelIndex));

        Span<byte> attr =
        [
            (byte)'a',
            (byte)'t',
            (byte)'r',
            (byte)'_',
            AccessoryByte(_modelIndex),
            (byte)'v',
            (byte)'_',
            (byte)'a',
            0,
        ];
        for (var i = 1; i < 10; ++i)
        {
            var flag = (ushort)(1 << i);
            if ((imcMask & flag) is not 0)
                continue;

            attr[^2] = (byte)('a' + i);

            foreach (var (attribute, index) in _model->ModelResourceHandle->Attributes)
            {
                if (!EqualAttribute(attr, attribute.Value))
                    continue;

                _model->EnabledAttributeIndexMask &= ~(1u << index);
                break;
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.AggressiveInlining)]
    private static bool EqualAttribute(Span<byte> needle, byte* haystack)
    {
        foreach (var character in needle)
        {
            if (*haystack++ != character)
                return false;
        }

        return true;
    }

    private static byte AccessoryByte(HumanSlot slot)
        => slot switch
        {
            HumanSlot.Head    => (byte)'m',
            HumanSlot.Ears    => (byte)'e',
            HumanSlot.Neck    => (byte)'n',
            HumanSlot.Wrists  => (byte)'w',
            HumanSlot.RFinger => (byte)'r',
            HumanSlot.LFinger => (byte)'r',
            _                 => 0,
        };

    private void CheckAttributes(AtrCache attributeCache)
    {
        if (attributeCache.DisabledCount is 0)
            return;

        ref var attributes = ref _model->ModelResourceHandle->Attributes;
        foreach (var (attribute, index) in attributes.Where(kvp => ShapeAttributeString.ValidateCustomAttributeString(kvp.Key.Value)))
        {
            if (ShapeAttributeString.TryRead(attribute.Value, out var attributeString))
            {
                // Mask out custom attributes if they are disabled. Attributes are enabled by default.
                if (attributeCache.ShouldBeDisabled(attributeString, _modelIndex, _ids[_modelIndex.ToIndex()], _genderRace))
                    _model->EnabledAttributeIndexMask &= ~(1u << index);
            }
            else
            {
                Penumbra.Log.Warning($"Trying to read a attribute string that is too long: {attribute}.");
            }
        }
    }

    private void CheckShapes(ShpCache shapeCache)
    {
        _temporaryShapes[_slotIndex].Clear();
        ref var shapes = ref _model->ModelResourceHandle->Shapes;
        foreach (var (shape, index) in shapes.Where(kvp => ShapeAttributeString.ValidateCustomShapeString(kvp.Key.Value)))
        {
            if (ShapeAttributeString.TryRead(shape.Value, out var shapeString))
            {
                _temporaryShapes[_slotIndex].TryAdd(shapeString, index);
                // Add custom shapes if they are enabled. Shapes are disabled by default.
                if (shapeCache.ShouldBeEnabled(shapeString, _modelIndex, _ids[_modelIndex.ToIndex()], _genderRace))
                    _model->EnabledShapeKeyIndexMask |= 1u << index;
            }
            else
            {
                Penumbra.Log.Warning($"Trying to read a shape string that is too long: {shape}.");
            }
        }
    }

    private void UpdateDefaultMasks(Model human, ShpCache cache)
    {
        var genderRace = (GenderRace)human.AsHuman->RaceSexId;
        foreach (var (shape, topIndex) in _temporaryShapes[1])
        {
            if (shape.IsWrist()
             && _temporaryShapes[2].TryGetValue(shape, out var handIndex)
             && !cache.ShouldBeDisabled(shape, HumanSlot.Body,  _ids[1], genderRace)
             && !cache.ShouldBeDisabled(shape, HumanSlot.Hands, _ids[2], genderRace)
             && human.AsHuman->Models[1] is not null
             && human.AsHuman->Models[2] is not null)
            {
                human.AsHuman->Models[1]->EnabledShapeKeyIndexMask |= 1u << topIndex;
                human.AsHuman->Models[2]->EnabledShapeKeyIndexMask |= 1u << handIndex;
                CheckCondition(cache.State(ShapeConnectorCondition.Wrists), genderRace, HumanSlot.Body, HumanSlot.Hands, 1, 2);
            }

            if (shape.IsWaist()
             && _temporaryShapes[3].TryGetValue(shape, out var legIndex)
             && !cache.ShouldBeDisabled(shape, HumanSlot.Body, _ids[1], genderRace)
             && !cache.ShouldBeDisabled(shape, HumanSlot.Legs, _ids[3], genderRace)
             && human.AsHuman->Models[1] is not null
             && human.AsHuman->Models[3] is not null)
            {
                human.AsHuman->Models[1]->EnabledShapeKeyIndexMask |= 1u << topIndex;
                human.AsHuman->Models[3]->EnabledShapeKeyIndexMask |= 1u << legIndex;
                CheckCondition(cache.State(ShapeConnectorCondition.Waist), genderRace, HumanSlot.Body, HumanSlot.Legs, 1, 3);
            }
        }

        foreach (var (shape, bottomIndex) in _temporaryShapes[3])
        {
            if (shape.IsAnkle()
             && _temporaryShapes[4].TryGetValue(shape, out var footIndex)
             && !cache.ShouldBeDisabled(shape, HumanSlot.Legs, _ids[3], genderRace)
             && !cache.ShouldBeDisabled(shape, HumanSlot.Feet, _ids[4], genderRace)
             && human.AsHuman->Models[3] is not null
             && human.AsHuman->Models[4] is not null)
            {
                human.AsHuman->Models[3]->EnabledShapeKeyIndexMask |= 1u << bottomIndex;
                human.AsHuman->Models[4]->EnabledShapeKeyIndexMask |= 1u << footIndex;
                CheckCondition(cache.State(ShapeConnectorCondition.Ankles), genderRace, HumanSlot.Legs, HumanSlot.Feet, 3, 4);
            }
        }

        return;

        void CheckCondition(IReadOnlyDictionary<ShapeAttributeString, ShapeAttributeHashSet> dict, GenderRace genderRace, HumanSlot slot1,
            HumanSlot slot2, int idx1, int idx2)
        {
            foreach (var (shape, set) in dict)
            {
                if (set.CheckEntry(slot1, _ids[idx1], genderRace) is true && _temporaryShapes[idx1].TryGetValue(shape, out var index1))
                    human.AsHuman->Models[idx1]->EnabledShapeKeyIndexMask |= 1u << index1;
                if (set.CheckEntry(slot2, _ids[idx2], genderRace) is true && _temporaryShapes[idx2].TryGetValue(shape, out var index2))
                    human.AsHuman->Models[idx2]->EnabledShapeKeyIndexMask |= 1u << index2;
            }
        }
    }
}
