using Dalamud.Interface;
using ImGuiNET;
using OtterGui.Raii;
using OtterGui.Services;
using OtterGui.Text;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Structs;
using Penumbra.Meta;
using Penumbra.Meta.Manipulations;
using Penumbra.Mods.Editor;
using Penumbra.UI.Classes;

namespace Penumbra.UI.AdvancedWindow.Meta;

public sealed class ImcMetaDrawer(ModMetaEditor editor, MetaFileManager metaFiles)
    : MetaDrawer<ImcIdentifier, ImcEntry>(editor, metaFiles), IService
{
    public override ReadOnlySpan<byte> Label
        => "Variant Edits (IMC)###IMC"u8;

    public override int NumColumns
        => 10;

    private bool _fileExists;

    protected override void Initialize()
    {
        Identifier = ImcIdentifier.Default;
        UpdateEntry();
    }

    private void UpdateEntry()
        => (Entry, _fileExists, _) = MetaFiles.ImcChecker.GetDefaultEntry(Identifier, true);

    protected override void DrawNew()
    {
        ImGui.TableNextColumn();
        CopyToClipboardButton("Copy all current IMC manipulations to clipboard."u8, MetaDictionary.SerializeTo([], Editor.Imc));
        ImGui.TableNextColumn();
        var canAdd = _fileExists && !Editor.Contains(Identifier);
        var tt     = canAdd ? "Stage this edit."u8 : !_fileExists ? "This IMC file does not exist."u8 : "This entry is already edited."u8;
        if (ImUtf8.IconButton(FontAwesomeIcon.Plus, tt, disabled: !canAdd))
            Editor.Changes |= Editor.TryAdd(Identifier, Entry);

        if (DrawIdentifierInput(ref Identifier))
            UpdateEntry();

        using var disabled = ImRaii.Disabled();
        DrawEntry(Entry, ref Entry, false);
    }

    protected override void DrawEntry(ImcIdentifier identifier, ImcEntry entry)
    {
        DrawMetaButtons(identifier, entry);
        DrawIdentifier(identifier);

        var defaultEntry = MetaFiles.ImcChecker.GetDefaultEntry(identifier, true).Entry;
        if (DrawEntry(defaultEntry, ref entry, true))
            Editor.Changes |= Editor.Update(identifier, entry);
    }

    private static bool DrawIdentifierInput(ref ImcIdentifier identifier)
    {
        ImGui.TableNextColumn();
        var change = DrawObjectType(ref identifier);

        ImGui.TableNextColumn();
        change |= DrawPrimaryId(ref identifier);

        ImGui.TableNextColumn();
        if (identifier.ObjectType is ObjectType.Equipment or ObjectType.Accessory)
            change |= DrawSlot(ref identifier);
        else
            change |= DrawSecondaryId(ref identifier);

        ImGui.TableNextColumn();
        change |= DrawVariant(ref identifier);

        ImGui.TableNextColumn();
        if (identifier.ObjectType is ObjectType.DemiHuman)
            change |= DrawSlot(ref identifier, 70f);
        else
            ImUtf8.ScaledDummy(70f);
        return change;
    }

    private static void DrawIdentifier(ImcIdentifier identifier)
    {
        ImGui.TableNextColumn();
        ImUtf8.TextFramed(identifier.ObjectType.ToName(), FrameColor);
        ImUtf8.HoverTooltip("Object Type"u8);

        ImGui.TableNextColumn();
        ImUtf8.TextFramed($"{identifier.PrimaryId.Id}", FrameColor);
        ImUtf8.HoverTooltip("Primary ID");

        ImGui.TableNextColumn();
        if (identifier.ObjectType is ObjectType.Equipment or ObjectType.Accessory)
        {
            ImUtf8.TextFramed(identifier.EquipSlot.ToName(), FrameColor);
            ImUtf8.HoverTooltip("Equip Slot"u8);
        }
        else
        {
            ImUtf8.TextFramed($"{identifier.SecondaryId.Id}", FrameColor);
            ImUtf8.HoverTooltip("Secondary ID"u8);
        }

        ImGui.TableNextColumn();
        ImUtf8.TextFramed($"{identifier.Variant.Id}", FrameColor);
        ImUtf8.HoverTooltip("Variant"u8);

        ImGui.TableNextColumn();
        if (identifier.ObjectType is ObjectType.DemiHuman)
        {
            ImUtf8.TextFramed(identifier.EquipSlot.ToName(), FrameColor);
            ImUtf8.HoverTooltip("Equip Slot"u8);
        }

    }

    private static bool DrawEntry(ImcEntry defaultEntry, ref ImcEntry entry, bool addDefault)
    {
        ImGui.TableNextColumn();
        var change = DrawMaterialId(defaultEntry, ref entry, addDefault);
        ImUtf8.SameLineInner();
        change |= DrawMaterialAnimationId(defaultEntry, ref entry, addDefault);

        ImGui.TableNextColumn();
        change |= DrawDecalId(defaultEntry, ref entry, addDefault);
        ImUtf8.SameLineInner();
        change |= DrawVfxId(defaultEntry, ref entry, addDefault);
        ImUtf8.SameLineInner();
        change |= DrawSoundId(defaultEntry, ref entry, addDefault);

        ImGui.TableNextColumn();
        change |= DrawAttributes(defaultEntry, ref entry);
        return change;
    }


    protected override IEnumerable<(ImcIdentifier, ImcEntry)> Enumerate()
        => Editor.Imc
            .OrderBy(kvp => kvp.Key.ObjectType)
            .ThenBy(kvp => kvp.Key.PrimaryId.Id)
            .ThenBy(kvp => kvp.Key.EquipSlot)
            .ThenBy(kvp => kvp.Key.BodySlot)
            .ThenBy(kvp => kvp.Key.SecondaryId.Id)
            .ThenBy(kvp => kvp.Key.Variant.Id)
            .Select(kvp => (kvp.Key, kvp.Value));

    protected override int Count
        => Editor.Imc.Count;

    public static bool DrawObjectType(ref ImcIdentifier identifier, float width = 110)
    {
        var ret = Combos.ImcType("##imcType", identifier.ObjectType, out var type, width);
        ImUtf8.HoverTooltip("Object Type"u8);

        if (ret)
        {
            var (equipSlot, secondaryId) = type switch
            {
                ObjectType.Equipment => (identifier.EquipSlot.IsEquipment() ? identifier.EquipSlot : EquipSlot.Head, (SecondaryId) 0),
                ObjectType.DemiHuman => (identifier.EquipSlot.IsEquipment() ? identifier.EquipSlot : EquipSlot.Head, identifier.SecondaryId == 0 ? 1 : identifier.SecondaryId),
                ObjectType.Accessory => (identifier.EquipSlot.IsAccessory() ? identifier.EquipSlot : EquipSlot.Ears, (SecondaryId)0),
                _                    => (EquipSlot.Unknown, identifier.SecondaryId == 0 ? 1 : identifier.SecondaryId),
            };
            identifier = identifier with
            {
                ObjectType = type,
                EquipSlot = equipSlot,
                SecondaryId = secondaryId,
            };
        }

        return ret;
    }

    public static bool DrawPrimaryId(ref ImcIdentifier identifier, float unscaledWidth = 80)
    {
        var ret = IdInput("##imcPrimaryId"u8, unscaledWidth, identifier.PrimaryId.Id, out var newId, 0, ushort.MaxValue,
            identifier.PrimaryId.Id <= 1);
        ImUtf8.HoverTooltip("Primary ID - You can usually find this as the 'x####' part of an item path.\n"u8
          + "This should generally not be left <= 1 unless you explicitly want that."u8);
        if (ret)
            identifier = identifier with { PrimaryId = newId };
        return ret;
    }

    public static bool DrawSecondaryId(ref ImcIdentifier identifier, float unscaledWidth = 100)
    {
        var ret = IdInput("##imcSecondaryId"u8, unscaledWidth, identifier.SecondaryId.Id, out var newId, 0, ushort.MaxValue, false);
        ImUtf8.HoverTooltip("Secondary ID"u8);
        if (ret)
            identifier = identifier with { SecondaryId = newId };
        return ret;
    }

    public static bool DrawVariant(ref ImcIdentifier identifier, float unscaledWidth = 45)
    {
        var ret = IdInput("##imcVariant"u8, unscaledWidth, identifier.Variant.Id, out var newId, 0, byte.MaxValue, false);
        ImUtf8.HoverTooltip("Variant ID"u8);
        if (ret)
            identifier = identifier with { Variant = (byte)newId };
        return ret;
    }

    public static bool DrawSlot(ref ImcIdentifier identifier, float unscaledWidth = 100)
    {
        bool      ret;
        EquipSlot slot;
        switch (identifier.ObjectType)
        {
            case ObjectType.Equipment:
            case ObjectType.DemiHuman:
                ret = Combos.EqpEquipSlot("##slot", identifier.EquipSlot, out slot, unscaledWidth);
                break;
            case ObjectType.Accessory:
                ret = Combos.AccessorySlot("##slot", identifier.EquipSlot, out slot, unscaledWidth);
                break;
            default: return false;
        }

        ImUtf8.HoverTooltip("Equip Slot"u8);
        if (ret)
            identifier = identifier with { EquipSlot = slot };
        return ret;
    }

    public static bool DrawMaterialId(ImcEntry defaultEntry, ref ImcEntry entry, bool addDefault, float unscaledWidth = 45)
    {
        if (!DragInput("##materialId"u8, "Material ID"u8, unscaledWidth * ImUtf8.GlobalScale, entry.MaterialId, defaultEntry.MaterialId,
                out var newValue,        (byte)1,         byte.MaxValue,                      0.01f,            addDefault))
            return false;

        entry = entry with { MaterialId = newValue };
        return true;
    }

    public static bool DrawMaterialAnimationId(ImcEntry defaultEntry, ref ImcEntry entry, bool addDefault, float unscaledWidth = 45)
    {
        if (!DragInput("##mAnimId"u8,             "Material Animation ID"u8, unscaledWidth * ImUtf8.GlobalScale, entry.MaterialAnimationId,
                defaultEntry.MaterialAnimationId, out var newValue,          (byte)0, byte.MaxValue, 0.01f, addDefault))
            return false;

        entry = entry with { MaterialAnimationId = newValue };
        return true;
    }

    public static bool DrawDecalId(ImcEntry defaultEntry, ref ImcEntry entry, bool addDefault, float unscaledWidth = 45)
    {
        if (!DragInput("##decalId"u8, "Decal ID"u8,  unscaledWidth * ImUtf8.GlobalScale, entry.DecalId, defaultEntry.DecalId, out var newValue,
                (byte)0,              byte.MaxValue, 0.01f,                              addDefault))
            return false;

        entry = entry with { DecalId = newValue };
        return true;
    }

    public static bool DrawVfxId(ImcEntry defaultEntry, ref ImcEntry entry, bool addDefault, float unscaledWidth = 45)
    {
        if (!DragInput("##vfxId"u8, "VFX ID"u8, unscaledWidth * ImUtf8.GlobalScale, entry.VfxId, defaultEntry.VfxId, out var newValue, (byte)0,
                byte.MaxValue,      0.01f,      addDefault))
            return false;

        entry = entry with { VfxId = newValue };
        return true;
    }

    public static bool DrawSoundId(ImcEntry defaultEntry, ref ImcEntry entry, bool addDefault, float unscaledWidth = 45)
    {
        if (!DragInput("##soundId"u8, "Sound ID"u8,  unscaledWidth * ImUtf8.GlobalScale, entry.SoundId, defaultEntry.SoundId, out var newValue,
                (byte)0,              byte.MaxValue, 0.01f,                              addDefault))
            return false;

        entry = entry with { SoundId = newValue };
        return true;
    }

    private static bool DrawAttributes(ImcEntry defaultEntry, ref ImcEntry entry)
    {
        var changes = false;
        for (var i = 0; i < ImcEntry.NumAttributes; ++i)
        {
            using var id    = ImRaii.PushId(i);
            var       flag  = 1 << i;
            var       value = (entry.AttributeMask & flag) != 0;
            var       def   = (defaultEntry.AttributeMask & flag) != 0;
            if (Checkmark("##attribute"u8, "ABCDEFGHIJ"u8.Slice(i, 1), value, def, out var newValue))
            {
                var newMask = (ushort)(newValue ? entry.AttributeMask | flag : entry.AttributeMask & ~flag);
                entry   = entry with { AttributeMask = newMask };
                changes = true;
            }

            if (i < ImcEntry.NumAttributes - 1)
                ImUtf8.SameLineInner();
        }

        return changes;
    }
}
