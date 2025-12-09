using ImSharp;
using Luna;
using Newtonsoft.Json.Linq;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Structs;
using Penumbra.Meta;
using Penumbra.Meta.Manipulations;
using Penumbra.Mods.Editor;

namespace Penumbra.UI.AdvancedWindow.Meta;

public sealed class ImcMetaDrawer(ModMetaEditor editor, MetaFileManager metaFiles)
    : MetaDrawer<ImcIdentifier, ImcEntry>(editor, metaFiles)
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
        => (Entry, _fileExists, _) = ImcChecker.GetDefaultEntry(Identifier, true);

    protected override void DrawNew()
    {
        Im.Table.NextColumn();
        CopyToClipboardButton("Copy all current IMC manipulations to clipboard."u8,
            new Lazy<JToken?>(() => MetaDictionary.SerializeTo([], Editor.Imc)));
        Im.Table.NextColumn();
        var canAdd = _fileExists && !Editor.Contains(Identifier);
        var tt     = canAdd ? "Stage this edit."u8 : !_fileExists ? "This IMC file does not exist."u8 : "This entry is already edited."u8;
        if (ImEx.Icon.Button(LunaStyle.AddObjectIcon, tt, !canAdd))
            Editor.Changes |= Editor.TryAdd(Identifier, Entry);

        if (DrawIdentifierInput(ref Identifier))
            UpdateEntry();

        using var disabled = Im.Disabled();
        DrawEntry(Entry, ref Entry, false);
    }

    protected override void DrawEntry(ImcIdentifier identifier, ImcEntry entry)
    {
        DrawMetaButtons(identifier, entry);
        DrawIdentifier(identifier);

        var defaultEntry = ImcChecker.GetDefaultEntry(identifier, true).Entry;
        if (DrawEntry(defaultEntry, ref entry, true))
            Editor.Changes |= Editor.Update(identifier, entry);
    }

    private static bool DrawIdentifierInput(ref ImcIdentifier identifier)
    {
        Im.Table.NextColumn();
        var change = DrawObjectType(ref identifier);

        Im.Table.NextColumn();
        change |= DrawPrimaryId(ref identifier);

        Im.Table.NextColumn();
        if (identifier.ObjectType is ObjectType.Equipment or ObjectType.Accessory)
            change |= DrawSlot(ref identifier);
        else
            change |= DrawSecondaryId(ref identifier);

        Im.Table.NextColumn();
        change |= DrawVariant(ref identifier);

        Im.Table.NextColumn();
        if (identifier.ObjectType is ObjectType.DemiHuman)
            change |= DrawSlot(ref identifier, 70f);
        else
            Im.ScaledDummy(70f);
        return change;
    }

    private static void DrawIdentifier(ImcIdentifier identifier)
    {
        Im.Table.NextColumn();
        ImEx.TextFramed(identifier.ObjectType.ToName(), default, FrameColor);
        Im.Tooltip.OnHover("Object Type"u8);

        Im.Table.NextColumn();
        ImEx.TextFramed($"{identifier.PrimaryId.Id}", default, FrameColor);
        Im.Tooltip.OnHover("Primary ID");

        Im.Table.NextColumn();
        if (identifier.ObjectType is ObjectType.Equipment or ObjectType.Accessory)
        {
            ImEx.TextFramed(identifier.EquipSlot.ToNameU8(), default, FrameColor);
            Im.Tooltip.OnHover("Equip Slot"u8);
        }
        else
        {
            ImEx.TextFramed($"{identifier.SecondaryId.Id}", default, FrameColor);
            Im.Tooltip.OnHover("Secondary ID"u8);
        }

        Im.Table.NextColumn();
        ImEx.TextFramed($"{identifier.Variant.Id}", default, FrameColor);
        Im.Tooltip.OnHover("Variant"u8);

        Im.Table.NextColumn();
        if (identifier.ObjectType is ObjectType.DemiHuman)
        {
            ImEx.TextFramed(identifier.EquipSlot.ToNameU8(), default, FrameColor);
            Im.Tooltip.OnHover("Equip Slot"u8);
        }
    }

    private static bool DrawEntry(ImcEntry defaultEntry, ref ImcEntry entry, bool addDefault)
    {
        Im.Table.NextColumn();
        var change = DrawMaterialId(defaultEntry, ref entry, addDefault);
        Im.Line.SameInner();
        change |= DrawMaterialAnimationId(defaultEntry, ref entry, addDefault);

        Im.Table.NextColumn();
        change |= DrawDecalId(defaultEntry, ref entry, addDefault);
        Im.Line.SameInner();
        change |= DrawVfxId(defaultEntry, ref entry, addDefault);
        Im.Line.SameInner();
        change |= DrawSoundId(defaultEntry, ref entry, addDefault);

        Im.Table.NextColumn();
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
        var ret = Combos.Combos.ImcType("##imcType", identifier.ObjectType, out var type, width);
        Im.Tooltip.OnHover("Object Type"u8);

        if (ret)
        {
            var (equipSlot, secondaryId) = type switch
            {
                ObjectType.Equipment => (identifier.EquipSlot.IsEquipment() ? identifier.EquipSlot : EquipSlot.Head, SecondaryId.Zero),
                ObjectType.DemiHuman => (identifier.EquipSlot.IsEquipment() ? identifier.EquipSlot : EquipSlot.Head,
                    identifier.SecondaryId == 0 ? (SecondaryId)1 : identifier.SecondaryId),
                ObjectType.Accessory => (identifier.EquipSlot.IsAccessory() ? identifier.EquipSlot : EquipSlot.Ears, SecondaryId.Zero),
                _                    => (EquipSlot.Unknown, identifier.SecondaryId == 0 ? (SecondaryId)1 : identifier.SecondaryId),
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
        Im.Tooltip.OnHover("Primary ID - You can usually find this as the 'x####' part of an item path.\n"u8
          + "This should generally not be left <= 1 unless you explicitly want that."u8);
        if (ret)
            identifier = identifier with { PrimaryId = newId };
        return ret;
    }

    public static bool DrawSecondaryId(ref ImcIdentifier identifier, float unscaledWidth = 100)
    {
        var ret = IdInput("##imcSecondaryId"u8, unscaledWidth, identifier.SecondaryId.Id, out var newId, 0, ushort.MaxValue, false);
        Im.Tooltip.OnHover("Secondary ID"u8);
        if (ret)
            identifier = identifier with { SecondaryId = newId };
        return ret;
    }

    public static bool DrawVariant(ref ImcIdentifier identifier, float unscaledWidth = 45)
    {
        var ret = IdInput("##imcVariant"u8, unscaledWidth, identifier.Variant.Id, out var newId, 0, byte.MaxValue, false);
        Im.Tooltip.OnHover("Variant ID"u8);
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
                ret = Combos.Combos.EqpEquipSlot("##slot", identifier.EquipSlot, out slot, unscaledWidth);
                break;
            case ObjectType.Accessory: ret = Combos.Combos.AccessorySlot("##slot", identifier.EquipSlot, out slot, unscaledWidth); break;
            default:                   return false;
        }

        Im.Tooltip.OnHover("Equip Slot"u8);
        if (ret)
            identifier = identifier with { EquipSlot = slot };
        return ret;
    }

    public static bool DrawMaterialId(ImcEntry defaultEntry, ref ImcEntry entry, bool addDefault, float unscaledWidth = 45)
    {
        if (!DragInput("##materialId"u8, "Material ID"u8, unscaledWidth * Im.Style.GlobalScale, entry.MaterialId, defaultEntry.MaterialId,
                out var newValue,        (byte)1,         byte.MaxValue,                        0.01f,            addDefault))
            return false;

        entry = entry with { MaterialId = newValue };
        return true;
    }

    public static bool DrawMaterialAnimationId(ImcEntry defaultEntry, ref ImcEntry entry, bool addDefault, float unscaledWidth = 45)
    {
        if (!DragInput("##mAnimId"u8,             "Material Animation ID"u8, unscaledWidth * Im.Style.GlobalScale, entry.MaterialAnimationId,
                defaultEntry.MaterialAnimationId, out var newValue,          (byte)0, byte.MaxValue, 0.01f, addDefault))
            return false;

        entry = entry with { MaterialAnimationId = newValue };
        return true;
    }

    public static bool DrawDecalId(ImcEntry defaultEntry, ref ImcEntry entry, bool addDefault, float unscaledWidth = 45)
    {
        if (!DragInput("##decalId"u8, "Decal ID"u8, unscaledWidth * Im.Style.GlobalScale, entry.DecalId, defaultEntry.DecalId, out var newValue,
                (byte)0,              byte.MaxValue, 0.01f, addDefault))
            return false;

        entry = entry with { DecalId = newValue };
        return true;
    }

    public static bool DrawVfxId(ImcEntry defaultEntry, ref ImcEntry entry, bool addDefault, float unscaledWidth = 45)
    {
        if (!DragInput("##vfxId"u8, "VFX ID"u8, unscaledWidth * Im.Style.GlobalScale, entry.VfxId, defaultEntry.VfxId, out var newValue,
                (byte)0,
                byte.MaxValue, 0.01f, addDefault))
            return false;

        entry = entry with { VfxId = newValue };
        return true;
    }

    public static bool DrawSoundId(ImcEntry defaultEntry, ref ImcEntry entry, bool addDefault, float unscaledWidth = 45)
    {
        if (!DragInput("##soundId"u8, "Sound ID"u8, unscaledWidth * Im.Style.GlobalScale, entry.SoundId, defaultEntry.SoundId, out var newValue,
                (byte)0,              byte.MaxValue, 0.01f, addDefault))
            return false;

        entry = entry with { SoundId = newValue };
        return true;
    }

    private static bool DrawAttributes(ImcEntry defaultEntry, ref ImcEntry entry)
    {
        var changes = false;
        for (var i = 0; i < ImcEntry.NumAttributes; ++i)
        {
            using var id    = Im.Id.Push(i);
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
                Im.Line.SameInner();
        }

        return changes;
    }
}
