using OtterGui.Services;
using Penumbra.Collections;
using Penumbra.Communication;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Interop;
using Penumbra.Services;

namespace Penumbra.Meta;

public class ShapeManager : IRequiredService, IDisposable
{
    public const     int                 NumSlots = 4;
    private readonly CommunicatorService _communicator;

    private static ReadOnlySpan<byte> UsedModels
        => [1, 2, 3, 4];

    public ShapeManager(CommunicatorService communicator)
    {
        _communicator = communicator;
        _communicator.ModelAttributeComputed.Subscribe(OnAttributeComputed, ModelAttributeComputed.Priority.ShapeManager);
    }

    private readonly Dictionary<ShapeString, short>[] _temporaryIndices =
        Enumerable.Range(0, NumSlots).Select(_ => new Dictionary<ShapeString, short>()).ToArray();

    private readonly uint[] _temporaryMasks  = new uint[NumSlots];
    private readonly uint[] _temporaryValues = new uint[NumSlots];

    private unsafe void OnAttributeComputed(Actor actor, Model model, ModCollection collection, HumanSlot slot)
    {
        int index;
        switch (slot)
        {
            case HumanSlot.Unknown:
                ResetCache(model);
                return;
            case HumanSlot.Body:  index = 0; break;
            case HumanSlot.Hands: index = 1; break;
            case HumanSlot.Legs:  index = 2; break;
            case HumanSlot.Feet:  index = 3; break;
            default:              return;
        }

        if (_temporaryMasks[index] is 0)
            return;

        var modelIndex  = UsedModels[index];
        var currentMask = model.AsHuman->Models[modelIndex]->EnabledShapeKeyIndexMask;
        var newMask     = (currentMask & ~_temporaryMasks[index]) | _temporaryValues[index];
        Penumbra.Log.Excessive($"Changed Model Mask from {currentMask:X} to {newMask:X}.");
        model.AsHuman->Models[modelIndex]->EnabledShapeKeyIndexMask = newMask;
    }

    public void Dispose()
    {
        _communicator.ModelAttributeComputed.Unsubscribe(OnAttributeComputed);
    }

    private unsafe void ResetCache(Model human)
    {
        for (var i = 0; i < NumSlots; ++i)
        {
            _temporaryMasks[i]  = 0;
            _temporaryValues[i] = 0;
            _temporaryIndices[i].Clear();

            var modelIndex = UsedModels[i];
            var model      = human.AsHuman->Models[modelIndex];
            if (model is null || model->ModelResourceHandle is null)
                continue;

            ref var shapes = ref model->ModelResourceHandle->Shapes;
            foreach (var (shape, index) in shapes.Where(kvp => CheckShapes(kvp.Key.AsSpan(), modelIndex)))
            {
                if (ShapeString.TryRead(shape.Value, out var shapeString))
                {
                    _temporaryIndices[i].TryAdd(shapeString, index);
                    _temporaryMasks[i] |= (ushort)(1 << index);
                }
                else
                {
                    Penumbra.Log.Warning($"Trying to read a shape string that is too long: {shape}.");
                }
            }
        }

        UpdateMasks();
    }

    private static bool CheckShapes(ReadOnlySpan<byte> shape, byte index)
        => index switch
        {
            1 => shape.StartsWith("shp_wa_"u8) || shape.StartsWith("shp_wr_"u8),
            2 => shape.StartsWith("shp_wr_"u8),
            3 => shape.StartsWith("shp_wa_"u8) || shape.StartsWith("shp_an"u8),
            4 => shape.StartsWith("shp_an"u8),
            _ => false,
        };

    private void UpdateMasks()
    {
        foreach (var (shape, topIndex) in _temporaryIndices[0])
        {
            if (_temporaryIndices[1].TryGetValue(shape, out var handIndex))
            {
                _temporaryValues[0] |= 1u << topIndex;
                _temporaryValues[1] |= 1u << handIndex;
            }

            if (_temporaryIndices[2].TryGetValue(shape, out var legIndex))
            {
                _temporaryValues[0] |= 1u << topIndex;
                _temporaryValues[2] |= 1u << legIndex;
            }
        }

        foreach (var (shape, bottomIndex) in _temporaryIndices[2])
        {
            if (_temporaryIndices[3].TryGetValue(shape, out var footIndex))
            {
                _temporaryValues[2] |= 1u << bottomIndex;
                _temporaryValues[3] |= 1u << footIndex;
            }
        }
    }
}
