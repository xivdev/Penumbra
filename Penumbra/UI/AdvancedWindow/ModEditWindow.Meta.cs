using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Interface;
using ImGuiNET;
using OtterGui;
using OtterGui.Raii;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Structs;
using Penumbra.Interop.Structs;
using Penumbra.Meta;
using Penumbra.Meta.Files;
using Penumbra.Meta.Manipulations;
using Penumbra.Mods;
using Penumbra.UI.Classes;

namespace Penumbra.UI.AdvancedWindow;

public partial class ModEditWindow
{
    private const string ModelSetIdTooltip =
        "Model Set ID - You can usually find this as the 'e####' part of an item path.\nThis should generally not be left <= 1 unless you explicitly want that.";

    private const string PrimaryIdTooltip =
        "Primary ID - You can usually find this as the 'x####' part of an item path.\nThis should generally not be left <= 1 unless you explicitly want that.";

    private const string ModelSetIdTooltipShort = "Model Set ID";
    private const string EquipSlotTooltip       = "Equip Slot";
    private const string ModelRaceTooltip       = "Model Race";
    private const string GenderTooltip          = "Gender";
    private const string ObjectTypeTooltip      = "Object Type";
    private const string SecondaryIdTooltip     = "Secondary ID";
    private const string VariantIdTooltip       = "Variant ID";
    private const string EstTypeTooltip         = "EST Type";
    private const string RacialTribeTooltip     = "Racial Tribe";
    private const string ScalingTypeTooltip     = "Scaling Type";

    private void DrawMetaTab()
    {
        using var tab = ImRaii.TabItem("Meta Manipulations");
        if (!tab)
            return;

        DrawOptionSelectHeader();

        var setsEqual = !_editor.MetaEditor.Changes;
        var tt        = setsEqual ? "No changes staged." : "Apply the currently staged changes to the option.";
        ImGui.NewLine();
        if (ImGuiUtil.DrawDisabledButton("Apply Changes", Vector2.Zero, tt, setsEqual))
            _editor.MetaEditor.Apply(_editor.Mod!, _editor.GroupIdx, _editor.OptionIdx);

        ImGui.SameLine();
        tt = setsEqual ? "No changes staged." : "Revert all currently staged changes.";
        if (ImGuiUtil.DrawDisabledButton("Revert Changes", Vector2.Zero, tt, setsEqual))
            _editor.MetaEditor.Load(_editor.Mod!, _editor.Option!);

        ImGui.SameLine();
        AddFromClipboardButton();
        ImGui.SameLine();
        SetFromClipboardButton();
        ImGui.SameLine();
        CopyToClipboardButton("Copy all current manipulations to clipboard.", _iconSize, _editor.MetaEditor.Recombine());
        ImGui.SameLine();
        if (ImGui.Button("Write as TexTools Files"))
            _metaFileManager.WriteAllTexToolsMeta(_mod!);

        using var child = ImRaii.Child("##meta", -Vector2.One, true);
        if (!child)
            return;

        DrawEditHeader(_editor.MetaEditor.Eqp,  "Equipment Parameter Edits (EQP)###EQP", 5,  EqpRow.Draw,  EqpRow.DrawNew , _editor.MetaEditor.OtherEqpCount);
        DrawEditHeader(_editor.MetaEditor.Eqdp, "Racial Model Edits (EQDP)###EQDP",      7,  EqdpRow.Draw, EqdpRow.DrawNew, _editor.MetaEditor.OtherEqdpCount);
        DrawEditHeader(_editor.MetaEditor.Imc,  "Variant Edits (IMC)###IMC",             10, ImcRow.Draw,  ImcRow.DrawNew , _editor.MetaEditor.OtherImcCount);
        DrawEditHeader(_editor.MetaEditor.Est,  "Extra Skeleton Parameters (EST)###EST", 7,  EstRow.Draw,  EstRow.DrawNew , _editor.MetaEditor.OtherEstCount);
        DrawEditHeader(_editor.MetaEditor.Gmp,  "Visor/Gimmick Edits (GMP)###GMP",       7,  GmpRow.Draw,  GmpRow.DrawNew , _editor.MetaEditor.OtherGmpCount);
        DrawEditHeader(_editor.MetaEditor.Rsp,  "Racial Scaling Edits (RSP)###RSP",      5,  RspRow.Draw,  RspRow.DrawNew , _editor.MetaEditor.OtherRspCount);
    }


    /// <summary> The headers for the different meta changes all have basically the same structure for different types.</summary>
    private void DrawEditHeader<T>(IReadOnlyCollection<T> items, string label, int numColumns, Action<MetaFileManager, T, ModEditor, Vector2> draw,
        Action<MetaFileManager, ModEditor, Vector2> drawNew, int otherCount)
    {
        const ImGuiTableFlags flags  = ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.BordersInnerV;

        var oldPos = ImGui.GetCursorPosY();
        var header = ImGui.CollapsingHeader($"{items.Count} {label}");
        var newPos = ImGui.GetCursorPos();
        if (otherCount > 0)
        {
            var text = $"{otherCount} Edits in other Options";
            var size = ImGui.CalcTextSize(text).X;
            ImGui.SetCursorPos(new Vector2(ImGui.GetContentRegionAvail().X - size, oldPos + ImGui.GetStyle().FramePadding.Y));
            ImGuiUtil.TextColored(ColorId.RedundantAssignment.Value() | 0xFF000000, text);
            ImGui.SetCursorPos(newPos);
        }
        if (!header)
            return;

        using (var table = ImRaii.Table(label, numColumns, flags))
        {
            if (table)
            {
                drawNew(_metaFileManager, _editor, _iconSize);
                foreach (var (item, index) in items.ToArray().WithIndex())
                {
                    using var id = ImRaii.PushId(index);
                    draw(_metaFileManager, item, _editor, _iconSize);
                }
            }
        }

        ImGui.NewLine();
    }

    private static class EqpRow
    {
        private static EqpManipulation _new = new(Eqp.DefaultEntry, EquipSlot.Head, 1);

        private static float IdWidth
            => 100 * UiHelpers.Scale;

        public static void DrawNew(MetaFileManager metaFileManager, ModEditor editor, Vector2 iconSize)
        {
            ImGui.TableNextColumn();
            CopyToClipboardButton("Copy all current EQP manipulations to clipboard.", iconSize,
                editor.MetaEditor.Eqp.Select(m => (MetaManipulation)m));
            ImGui.TableNextColumn();
            var canAdd       = editor.MetaEditor.CanAdd(_new);
            var tt           = canAdd ? "Stage this edit." : "This entry is already edited.";
            var defaultEntry = ExpandedEqpFile.GetDefault(metaFileManager, _new.SetId);
            if (ImGuiUtil.DrawDisabledButton(FontAwesomeIcon.Plus.ToIconString(), iconSize, tt, !canAdd, true))
                editor.MetaEditor.Add(_new.Copy(defaultEntry));

            // Identifier
            ImGui.TableNextColumn();
            if (IdInput("##eqpId", IdWidth, _new.SetId.Id, out var setId, 1, ExpandedEqpGmpBase.Count - 1, _new.SetId <= 1))
                _new = new EqpManipulation(ExpandedEqpFile.GetDefault(metaFileManager, setId), _new.Slot, setId);

            ImGuiUtil.HoverTooltip(ModelSetIdTooltip);

            ImGui.TableNextColumn();
            if (Combos.EqpEquipSlot("##eqpSlot", 100, _new.Slot, out var slot))
                _new = new EqpManipulation(ExpandedEqpFile.GetDefault(metaFileManager, setId), slot, _new.SetId);

            ImGuiUtil.HoverTooltip(EquipSlotTooltip);

            // Values
            using var disabled = ImRaii.Disabled();
            ImGui.TableNextColumn();
            using var style = ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing,
                new Vector2(3 * UiHelpers.Scale, ImGui.GetStyle().ItemSpacing.Y));
            foreach (var flag in Eqp.EqpAttributes[_new.Slot])
            {
                var value = defaultEntry.HasFlag(flag);
                Checkmark("##eqp", flag.ToLocalName(), value, value, out _);
                ImGui.SameLine();
            }

            ImGui.NewLine();
        }

        public static void Draw(MetaFileManager metaFileManager, EqpManipulation meta, ModEditor editor, Vector2 iconSize)
        {
            DrawMetaButtons(meta, editor, iconSize);

            // Identifier
            ImGui.TableNextColumn();
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetStyle().FramePadding.X);
            ImGui.TextUnformatted(meta.SetId.ToString());
            ImGuiUtil.HoverTooltip(ModelSetIdTooltipShort);
            var defaultEntry = ExpandedEqpFile.GetDefault(metaFileManager, meta.SetId);

            ImGui.TableNextColumn();
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetStyle().FramePadding.X);
            ImGui.TextUnformatted(meta.Slot.ToName());
            ImGuiUtil.HoverTooltip(EquipSlotTooltip);

            // Values
            ImGui.TableNextColumn();
            using var style = ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing,
                new Vector2(3 * UiHelpers.Scale, ImGui.GetStyle().ItemSpacing.Y));
            var idx = 0;
            foreach (var flag in Eqp.EqpAttributes[meta.Slot])
            {
                using var id           = ImRaii.PushId(idx++);
                var       defaultValue = defaultEntry.HasFlag(flag);
                var       currentValue = meta.Entry.HasFlag(flag);
                if (Checkmark("##eqp", flag.ToLocalName(), currentValue, defaultValue, out var value))
                    editor.MetaEditor.Change(meta.Copy(value ? meta.Entry | flag : meta.Entry & ~flag));

                ImGui.SameLine();
            }

            ImGui.NewLine();
        }
    }


    private static class EqdpRow
    {
        private static EqdpManipulation _new = new(EqdpEntry.Invalid, EquipSlot.Head, Gender.Male, ModelRace.Midlander, 1);

        private static float IdWidth
            => 100 * UiHelpers.Scale;

        public static void DrawNew(MetaFileManager metaFileManager, ModEditor editor, Vector2 iconSize)
        {
            ImGui.TableNextColumn();
            CopyToClipboardButton("Copy all current EQDP manipulations to clipboard.", iconSize,
                editor.MetaEditor.Eqdp.Select(m => (MetaManipulation)m));
            ImGui.TableNextColumn();
            var raceCode      = Names.CombinedRace(_new.Gender, _new.Race);
            var validRaceCode = CharacterUtilityData.EqdpIdx(raceCode, false) >= 0;
            var canAdd        = validRaceCode && editor.MetaEditor.CanAdd(_new);
            var tt = canAdd   ? "Stage this edit." :
                validRaceCode ? "This entry is already edited." : "This combination of race and gender can not be used.";
            var defaultEntry = validRaceCode
                ? ExpandedEqdpFile.GetDefault(metaFileManager, Names.CombinedRace(_new.Gender, _new.Race), _new.Slot.IsAccessory(), _new.SetId)
                : 0;
            if (ImGuiUtil.DrawDisabledButton(FontAwesomeIcon.Plus.ToIconString(), iconSize, tt, !canAdd, true))
                editor.MetaEditor.Add(_new.Copy(defaultEntry));

            // Identifier
            ImGui.TableNextColumn();
            if (IdInput("##eqdpId", IdWidth, _new.SetId.Id, out var setId, 0, ExpandedEqpGmpBase.Count - 1, _new.SetId <= 1))
            {
                var newDefaultEntry = ExpandedEqdpFile.GetDefault(metaFileManager, Names.CombinedRace(_new.Gender, _new.Race), _new.Slot.IsAccessory(), setId);
                _new = new EqdpManipulation(newDefaultEntry, _new.Slot, _new.Gender, _new.Race, setId);
            }

            ImGuiUtil.HoverTooltip(ModelSetIdTooltip);

            ImGui.TableNextColumn();
            if (Combos.Race("##eqdpRace", _new.Race, out var race))
            {
                var newDefaultEntry = ExpandedEqdpFile.GetDefault(metaFileManager, Names.CombinedRace(_new.Gender, race), _new.Slot.IsAccessory(), _new.SetId);
                _new = new EqdpManipulation(newDefaultEntry, _new.Slot, _new.Gender, race, _new.SetId);
            }

            ImGuiUtil.HoverTooltip(ModelRaceTooltip);

            ImGui.TableNextColumn();
            if (Combos.Gender("##eqdpGender", _new.Gender, out var gender))
            {
                var newDefaultEntry = ExpandedEqdpFile.GetDefault(metaFileManager, Names.CombinedRace(gender, _new.Race), _new.Slot.IsAccessory(), _new.SetId);
                _new = new EqdpManipulation(newDefaultEntry, _new.Slot, gender, _new.Race, _new.SetId);
            }

            ImGuiUtil.HoverTooltip(GenderTooltip);

            ImGui.TableNextColumn();
            if (Combos.EqdpEquipSlot("##eqdpSlot", _new.Slot, out var slot))
            {
                var newDefaultEntry = ExpandedEqdpFile.GetDefault(metaFileManager, Names.CombinedRace(_new.Gender, _new.Race), slot.IsAccessory(), _new.SetId);
                _new = new EqdpManipulation(newDefaultEntry, slot, _new.Gender, _new.Race, _new.SetId);
            }

            ImGuiUtil.HoverTooltip(EquipSlotTooltip);

            // Values
            using var disabled = ImRaii.Disabled();
            ImGui.TableNextColumn();
            var (bit1, bit2) = defaultEntry.ToBits(_new.Slot);
            Checkmark("Material##eqdpCheck1", string.Empty, bit1, bit1, out _);
            ImGui.SameLine();
            Checkmark("Model##eqdpCheck2", string.Empty, bit2, bit2, out _);
        }

        public static void Draw(MetaFileManager metaFileManager, EqdpManipulation meta, ModEditor editor, Vector2 iconSize)
        {
            DrawMetaButtons(meta, editor, iconSize);

            // Identifier
            ImGui.TableNextColumn();
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetStyle().FramePadding.X);
            ImGui.TextUnformatted(meta.SetId.ToString());
            ImGuiUtil.HoverTooltip(ModelSetIdTooltipShort);
            ImGui.TableNextColumn();
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetStyle().FramePadding.X);
            ImGui.TextUnformatted(meta.Race.ToName());
            ImGuiUtil.HoverTooltip(ModelRaceTooltip);
            ImGui.TableNextColumn();
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetStyle().FramePadding.X);
            ImGui.TextUnformatted(meta.Gender.ToName());
            ImGuiUtil.HoverTooltip(GenderTooltip);
            ImGui.TableNextColumn();
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetStyle().FramePadding.X);
            ImGui.TextUnformatted(meta.Slot.ToName());
            ImGuiUtil.HoverTooltip(EquipSlotTooltip);

            // Values
            var defaultEntry = ExpandedEqdpFile.GetDefault(metaFileManager, Names.CombinedRace(meta.Gender, meta.Race), meta.Slot.IsAccessory(), meta.SetId);
            var (defaultBit1, defaultBit2) = defaultEntry.ToBits(meta.Slot);
            var (bit1, bit2)               = meta.Entry.ToBits(meta.Slot);
            ImGui.TableNextColumn();
            if (Checkmark("Material##eqdpCheck1", string.Empty, bit1, defaultBit1, out var newBit1))
                editor.MetaEditor.Change(meta.Copy(Eqdp.FromSlotAndBits(meta.Slot, newBit1, bit2)));

            ImGui.SameLine();
            if (Checkmark("Model##eqdpCheck2", string.Empty, bit2, defaultBit2, out var newBit2))
                editor.MetaEditor.Change(meta.Copy(Eqdp.FromSlotAndBits(meta.Slot, bit1, newBit2)));
        }
    }

    private static class ImcRow
    {
        private static ImcManipulation _new = new(EquipSlot.Head, 1, 1, new ImcEntry());

        private static float IdWidth
            => 80 * UiHelpers.Scale;

        private static float SmallIdWidth
            => 45 * UiHelpers.Scale;

        /// <summary> Convert throwing to null-return if the file does not exist. </summary>
        private static ImcEntry? GetDefault(MetaFileManager metaFileManager, ImcManipulation imc)
        {
            try
            {
                return ImcFile.GetDefault(metaFileManager, imc.GamePath(), imc.EquipSlot, imc.Variant, out _);
            }
            catch
            {
                return null;
            }
        }

        public static void DrawNew(MetaFileManager metaFileManager, ModEditor editor, Vector2 iconSize)
        {
            ImGui.TableNextColumn();
            CopyToClipboardButton("Copy all current IMC manipulations to clipboard.", iconSize,
                editor.MetaEditor.Imc.Select(m => (MetaManipulation)m));
            ImGui.TableNextColumn();
            var defaultEntry = GetDefault(metaFileManager, _new);
            var canAdd = defaultEntry != null && editor.MetaEditor.CanAdd(_new);
            var tt = canAdd ? "Stage this edit." : defaultEntry == null ? "This IMC file does not exist." : "This entry is already edited.";
            defaultEntry ??= new ImcEntry();
            if (ImGuiUtil.DrawDisabledButton(FontAwesomeIcon.Plus.ToIconString(), iconSize, tt, !canAdd, true))
                editor.MetaEditor.Add(_new.Copy(defaultEntry.Value));

            // Identifier
            ImGui.TableNextColumn();
            if (Combos.ImcType("##imcType", _new.ObjectType, out var type))
            {
                var equipSlot = type switch
                {
                    ObjectType.Equipment => _new.EquipSlot.IsEquipment() ? _new.EquipSlot : EquipSlot.Head,
                    ObjectType.DemiHuman => _new.EquipSlot.IsEquipment() ? _new.EquipSlot : EquipSlot.Head,
                    ObjectType.Accessory => _new.EquipSlot.IsAccessory() ? _new.EquipSlot : EquipSlot.Ears,
                    _                    => EquipSlot.Unknown,
                };
                _new = new ImcManipulation(type, _new.BodySlot, _new.PrimaryId, _new.SecondaryId == 0 ? (ushort)1 : _new.SecondaryId,
                    _new.Variant.Id, equipSlot, _new.Entry);
            }

            ImGuiUtil.HoverTooltip(ObjectTypeTooltip);

            ImGui.TableNextColumn();
            if (IdInput("##imcId", IdWidth, _new.PrimaryId.Id, out var setId, 0, ushort.MaxValue, _new.PrimaryId <= 1))
                _new = new ImcManipulation(_new.ObjectType, _new.BodySlot, setId, _new.SecondaryId, _new.Variant.Id, _new.EquipSlot, _new.Entry)
                    .Copy(GetDefault(metaFileManager, _new)
                     ?? new ImcEntry());

            ImGuiUtil.HoverTooltip(PrimaryIdTooltip);

            using var style = ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing,
                new Vector2(3 * UiHelpers.Scale, ImGui.GetStyle().ItemSpacing.Y));

            ImGui.TableNextColumn();
            // Equipment and accessories are slightly different imcs than other types.
            if (_new.ObjectType is ObjectType.Equipment)
            {
                if (Combos.EqpEquipSlot("##imcSlot", 100, _new.EquipSlot, out var slot))
                    _new = new ImcManipulation(_new.ObjectType, _new.BodySlot, _new.PrimaryId, _new.SecondaryId, _new.Variant.Id, slot, _new.Entry)
                        .Copy(GetDefault(metaFileManager, _new)
                         ?? new ImcEntry());

                ImGuiUtil.HoverTooltip(EquipSlotTooltip);
            }
            else if (_new.ObjectType is ObjectType.Accessory)
            {
                if (Combos.AccessorySlot("##imcSlot", _new.EquipSlot, out var slot))
                    _new = new ImcManipulation(_new.ObjectType, _new.BodySlot, _new.PrimaryId, _new.SecondaryId, _new.Variant.Id, slot, _new.Entry)
                        .Copy(GetDefault(metaFileManager, _new)
                         ?? new ImcEntry());

                ImGuiUtil.HoverTooltip(EquipSlotTooltip);
            }
            else
            {
                if (IdInput("##imcId2", 100 * UiHelpers.Scale, _new.SecondaryId.Id, out var setId2, 0, ushort.MaxValue, false))
                    _new = new ImcManipulation(_new.ObjectType, _new.BodySlot, _new.PrimaryId, setId2, _new.Variant.Id, _new.EquipSlot, _new.Entry)
                        .Copy(GetDefault(metaFileManager, _new)
                         ?? new ImcEntry());

                ImGuiUtil.HoverTooltip(SecondaryIdTooltip);
            }

            ImGui.TableNextColumn();
            if (IdInput("##imcVariant", SmallIdWidth, _new.Variant.Id, out var variant, 0, byte.MaxValue, false))
                _new = new ImcManipulation(_new.ObjectType, _new.BodySlot, _new.PrimaryId, _new.SecondaryId, variant, _new.EquipSlot,
                    _new.Entry).Copy(GetDefault(metaFileManager, _new)
                 ?? new ImcEntry());

            ImGui.TableNextColumn();
            if (_new.ObjectType is ObjectType.DemiHuman)
            {
                if (Combos.EqpEquipSlot("##imcSlot", 70, _new.EquipSlot, out var slot))
                    _new = new ImcManipulation(_new.ObjectType, _new.BodySlot, _new.PrimaryId, _new.SecondaryId, _new.Variant.Id, slot, _new.Entry)
                        .Copy(GetDefault(metaFileManager, _new)
                         ?? new ImcEntry());

                ImGuiUtil.HoverTooltip(EquipSlotTooltip);
            }
            else
            {
                ImGui.Dummy(new Vector2(70 * UiHelpers.Scale, 0));
            }

            ImGuiUtil.HoverTooltip(VariantIdTooltip);

            // Values
            using var disabled = ImRaii.Disabled();
            ImGui.TableNextColumn();
            IntDragInput("##imcMaterialId", "Material ID", SmallIdWidth, defaultEntry.Value.MaterialId, defaultEntry.Value.MaterialId, out _,
                1,                          byte.MaxValue, 0f);
            ImGui.SameLine();
            IntDragInput("##imcMaterialAnimId",         "Material Animation ID", SmallIdWidth, defaultEntry.Value.MaterialAnimationId,
                defaultEntry.Value.MaterialAnimationId, out _,                   0,            byte.MaxValue, 0.01f);
            ImGui.TableNextColumn();
            IntDragInput("##imcDecalId", "Decal ID", SmallIdWidth, defaultEntry.Value.DecalId, defaultEntry.Value.DecalId, out _, 0,
                byte.MaxValue,           0f);
            ImGui.SameLine();
            IntDragInput("##imcVfxId", "VFX ID", SmallIdWidth, defaultEntry.Value.VfxId, defaultEntry.Value.VfxId, out _, 0, byte.MaxValue,
                0f);
            ImGui.SameLine();
            IntDragInput("##imcSoundId", "Sound ID", SmallIdWidth, defaultEntry.Value.SoundId, defaultEntry.Value.SoundId, out _, 0, 0b111111,
                0f);
            ImGui.TableNextColumn();
            for (var i = 0; i < 10; ++i)
            {
                using var id   = ImRaii.PushId(i);
                var       flag = 1 << i;
                Checkmark("##attribute",                            $"{(char)('A' + i)}", (defaultEntry.Value.AttributeMask & flag) != 0,
                    (defaultEntry.Value.AttributeMask & flag) != 0, out _);
                ImGui.SameLine();
            }

            ImGui.NewLine();
        }

        public static void Draw(MetaFileManager metaFileManager, ImcManipulation meta, ModEditor editor, Vector2 iconSize)
        {
            DrawMetaButtons(meta, editor, iconSize);

            // Identifier
            ImGui.TableNextColumn();
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetStyle().FramePadding.X);
            ImGui.TextUnformatted(meta.ObjectType.ToName());
            ImGuiUtil.HoverTooltip(ObjectTypeTooltip);
            ImGui.TableNextColumn();
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetStyle().FramePadding.X);
            ImGui.TextUnformatted(meta.PrimaryId.ToString());
            ImGuiUtil.HoverTooltip("Primary ID");

            ImGui.TableNextColumn();
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetStyle().FramePadding.X);
            if (meta.ObjectType is ObjectType.Equipment or ObjectType.Accessory)
            {
                ImGui.TextUnformatted(meta.EquipSlot.ToName());
                ImGuiUtil.HoverTooltip(EquipSlotTooltip);
            }
            else
            {
                ImGui.TextUnformatted(meta.SecondaryId.ToString());
                ImGuiUtil.HoverTooltip(SecondaryIdTooltip);
            }

            ImGui.TableNextColumn();
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetStyle().FramePadding.X);
            ImGui.TextUnformatted(meta.Variant.ToString());
            ImGuiUtil.HoverTooltip(VariantIdTooltip);

            ImGui.TableNextColumn();
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetStyle().FramePadding.X);
            if (meta.ObjectType is ObjectType.DemiHuman)
                ImGui.TextUnformatted(meta.EquipSlot.ToName());

            // Values
            using var style = ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing,
                new Vector2(3 * UiHelpers.Scale, ImGui.GetStyle().ItemSpacing.Y));
            ImGui.TableNextColumn();
            var defaultEntry = GetDefault(metaFileManager, meta) ?? new ImcEntry();
            if (IntDragInput("##imcMaterialId", $"Material ID\nDefault Value: {defaultEntry.MaterialId}", SmallIdWidth, meta.Entry.MaterialId,
                    defaultEntry.MaterialId,    out var materialId,                                       1,            byte.MaxValue, 0.01f))
                editor.MetaEditor.Change(meta.Copy(meta.Entry with { MaterialId = (byte)materialId }));

            ImGui.SameLine();
            if (IntDragInput("##imcMaterialAnimId", $"Material Animation ID\nDefault Value: {defaultEntry.MaterialAnimationId}", SmallIdWidth,
                    meta.Entry.MaterialAnimationId, defaultEntry.MaterialAnimationId, out var materialAnimId, 0, byte.MaxValue, 0.01f))
                editor.MetaEditor.Change(meta.Copy(meta.Entry with { MaterialAnimationId = (byte)materialAnimId }));

            ImGui.TableNextColumn();
            if (IntDragInput("##imcDecalId", $"Decal ID\nDefault Value: {defaultEntry.DecalId}", SmallIdWidth, meta.Entry.DecalId,
                    defaultEntry.DecalId,    out var decalId,                                    0,            byte.MaxValue, 0.01f))
                editor.MetaEditor.Change(meta.Copy(meta.Entry with { DecalId = (byte)decalId }));

            ImGui.SameLine();
            if (IntDragInput("##imcVfxId", $"VFX ID\nDefault Value: {defaultEntry.VfxId}", SmallIdWidth,  meta.Entry.VfxId, defaultEntry.VfxId,
                    out var vfxId,         0,                                              byte.MaxValue, 0.01f))
                editor.MetaEditor.Change(meta.Copy(meta.Entry with { VfxId = (byte)vfxId }));

            ImGui.SameLine();
            if (IntDragInput("##imcSoundId", $"Sound ID\nDefault Value: {defaultEntry.SoundId}", SmallIdWidth, meta.Entry.SoundId,
                    defaultEntry.SoundId,    out var soundId,                                    0,            0b111111, 0.01f))
                editor.MetaEditor.Change(meta.Copy(meta.Entry with { SoundId = (byte)soundId }));

            ImGui.TableNextColumn();
            for (var i = 0; i < 10; ++i)
            {
                using var id   = ImRaii.PushId(i);
                var       flag = 1 << i;
                if (Checkmark("##attribute",                      $"{(char)('A' + i)}", (meta.Entry.AttributeMask & flag) != 0,
                        (defaultEntry.AttributeMask & flag) != 0, out var val))
                {
                    var attributes = val ? meta.Entry.AttributeMask | flag : meta.Entry.AttributeMask & ~flag;
                    editor.MetaEditor.Change(meta.Copy(meta.Entry with { AttributeMask = (ushort)attributes }));
                }

                ImGui.SameLine();
            }

            ImGui.NewLine();
        }
    }

    private static class EstRow
    {
        private static EstManipulation _new = new(Gender.Male, ModelRace.Midlander, EstManipulation.EstType.Body, 1, 0);

        private static float IdWidth
            => 100 * UiHelpers.Scale;

        public static void DrawNew(MetaFileManager metaFileManager, ModEditor editor, Vector2 iconSize)
        {
            ImGui.TableNextColumn();
            CopyToClipboardButton("Copy all current EST manipulations to clipboard.", iconSize,
                editor.MetaEditor.Est.Select(m => (MetaManipulation)m));
            ImGui.TableNextColumn();
            var canAdd       = editor.MetaEditor.CanAdd(_new);
            var tt           = canAdd ? "Stage this edit." : "This entry is already edited.";
            var defaultEntry = EstFile.GetDefault(metaFileManager, _new.Slot, Names.CombinedRace(_new.Gender, _new.Race), _new.SetId);
            if (ImGuiUtil.DrawDisabledButton(FontAwesomeIcon.Plus.ToIconString(), iconSize, tt, !canAdd, true))
                editor.MetaEditor.Add(_new.Copy(defaultEntry));

            // Identifier
            ImGui.TableNextColumn();
            if (IdInput("##estId", IdWidth, _new.SetId.Id, out var setId, 0, ExpandedEqpGmpBase.Count - 1, _new.SetId <= 1))
            {
                var newDefaultEntry = EstFile.GetDefault(metaFileManager, _new.Slot, Names.CombinedRace(_new.Gender, _new.Race), setId);
                _new = new EstManipulation(_new.Gender, _new.Race, _new.Slot, setId, newDefaultEntry);
            }

            ImGuiUtil.HoverTooltip(ModelSetIdTooltip);

            ImGui.TableNextColumn();
            if (Combos.Race("##estRace", _new.Race, out var race))
            {
                var newDefaultEntry = EstFile.GetDefault(metaFileManager, _new.Slot, Names.CombinedRace(_new.Gender, race), _new.SetId);
                _new = new EstManipulation(_new.Gender, race, _new.Slot, _new.SetId, newDefaultEntry);
            }

            ImGuiUtil.HoverTooltip(ModelRaceTooltip);

            ImGui.TableNextColumn();
            if (Combos.Gender("##estGender", _new.Gender, out var gender))
            {
                var newDefaultEntry = EstFile.GetDefault(metaFileManager, _new.Slot, Names.CombinedRace(gender, _new.Race), _new.SetId);
                _new = new EstManipulation(gender, _new.Race, _new.Slot, _new.SetId, newDefaultEntry);
            }

            ImGuiUtil.HoverTooltip(GenderTooltip);

            ImGui.TableNextColumn();
            if (Combos.EstSlot("##estSlot", _new.Slot, out var slot))
            {
                var newDefaultEntry = EstFile.GetDefault(metaFileManager, slot, Names.CombinedRace(_new.Gender, _new.Race), _new.SetId);
                _new = new EstManipulation(_new.Gender, _new.Race, slot, _new.SetId, newDefaultEntry);
            }

            ImGuiUtil.HoverTooltip(EstTypeTooltip);

            // Values
            using var disabled = ImRaii.Disabled();
            ImGui.TableNextColumn();
            IntDragInput("##estSkeleton", "Skeleton Index", IdWidth, _new.Entry, defaultEntry, out _, 0, ushort.MaxValue, 0.05f);
        }

        public static void Draw(MetaFileManager metaFileManager, EstManipulation meta, ModEditor editor, Vector2 iconSize)
        {
            DrawMetaButtons(meta, editor, iconSize);

            // Identifier
            ImGui.TableNextColumn();
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetStyle().FramePadding.X);
            ImGui.TextUnformatted(meta.SetId.ToString());
            ImGuiUtil.HoverTooltip(ModelSetIdTooltipShort);
            ImGui.TableNextColumn();
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetStyle().FramePadding.X);
            ImGui.TextUnformatted(meta.Race.ToName());
            ImGuiUtil.HoverTooltip(ModelRaceTooltip);
            ImGui.TableNextColumn();
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetStyle().FramePadding.X);
            ImGui.TextUnformatted(meta.Gender.ToName());
            ImGuiUtil.HoverTooltip(GenderTooltip);
            ImGui.TableNextColumn();
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetStyle().FramePadding.X);
            ImGui.TextUnformatted(meta.Slot.ToString());
            ImGuiUtil.HoverTooltip(EstTypeTooltip);

            // Values
            var defaultEntry = EstFile.GetDefault(metaFileManager, meta.Slot, Names.CombinedRace(meta.Gender, meta.Race), meta.SetId);
            ImGui.TableNextColumn();
            if (IntDragInput("##estSkeleton", $"Skeleton Index\nDefault Value: {defaultEntry}", IdWidth,         meta.Entry, defaultEntry,
                    out var entry,            0,                                                ushort.MaxValue, 0.05f))
                editor.MetaEditor.Change(meta.Copy((ushort)entry));
        }
    }

    private static class GmpRow
    {
        private static GmpManipulation _new = new(GmpEntry.Default, 1);

        private static float RotationWidth
            => 75 * UiHelpers.Scale;

        private static float UnkWidth
            => 50 * UiHelpers.Scale;

        private static float IdWidth
            => 100 * UiHelpers.Scale;

        public static void DrawNew(MetaFileManager metaFileManager, ModEditor editor, Vector2 iconSize)
        {
            ImGui.TableNextColumn();
            CopyToClipboardButton("Copy all current GMP manipulations to clipboard.", iconSize,
                editor.MetaEditor.Gmp.Select(m => (MetaManipulation)m));
            ImGui.TableNextColumn();
            var canAdd       = editor.MetaEditor.CanAdd(_new);
            var tt           = canAdd ? "Stage this edit." : "This entry is already edited.";
            var defaultEntry = ExpandedGmpFile.GetDefault(metaFileManager, _new.SetId);
            if (ImGuiUtil.DrawDisabledButton(FontAwesomeIcon.Plus.ToIconString(), iconSize, tt, !canAdd, true))
                editor.MetaEditor.Add(_new.Copy(defaultEntry));

            // Identifier
            ImGui.TableNextColumn();
            if (IdInput("##gmpId", IdWidth, _new.SetId.Id, out var setId, 1, ExpandedEqpGmpBase.Count - 1, _new.SetId <= 1))
                _new = new GmpManipulation(ExpandedGmpFile.GetDefault(metaFileManager, setId), setId);

            ImGuiUtil.HoverTooltip(ModelSetIdTooltip);

            // Values
            using var disabled = ImRaii.Disabled();
            ImGui.TableNextColumn();
            Checkmark("##gmpEnabled", "Gimmick Enabled", defaultEntry.Enabled, defaultEntry.Enabled, out _);
            ImGui.TableNextColumn();
            Checkmark("##gmpAnimated", "Gimmick Animated", defaultEntry.Animated, defaultEntry.Animated, out _);
            ImGui.TableNextColumn();
            IntDragInput("##gmpRotationA", "Rotation A in Degrees", RotationWidth, defaultEntry.RotationA, defaultEntry.RotationA, out _, 0,
                360,                       0f);
            ImGui.SameLine();
            IntDragInput("##gmpRotationB", "Rotation B in Degrees", RotationWidth, defaultEntry.RotationB, defaultEntry.RotationB, out _, 0,
                360,                       0f);
            ImGui.SameLine();
            IntDragInput("##gmpRotationC", "Rotation C in Degrees", RotationWidth, defaultEntry.RotationC, defaultEntry.RotationC, out _, 0,
                360,                       0f);
            ImGui.TableNextColumn();
            IntDragInput("##gmpUnkA", "Animation Type A?", UnkWidth, defaultEntry.UnknownA, defaultEntry.UnknownA, out _, 0, 15, 0f);
            ImGui.SameLine();
            IntDragInput("##gmpUnkB", "Animation Type B?", UnkWidth, defaultEntry.UnknownB, defaultEntry.UnknownB, out _, 0, 15, 0f);
        }

        public static void Draw(MetaFileManager metaFileManager, GmpManipulation meta, ModEditor editor, Vector2 iconSize)
        {
            DrawMetaButtons(meta, editor, iconSize);

            // Identifier
            ImGui.TableNextColumn();
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetStyle().FramePadding.X);
            ImGui.TextUnformatted(meta.SetId.ToString());
            ImGuiUtil.HoverTooltip(ModelSetIdTooltipShort);

            // Values
            var defaultEntry = ExpandedGmpFile.GetDefault(metaFileManager, meta.SetId);
            ImGui.TableNextColumn();
            if (Checkmark("##gmpEnabled", "Gimmick Enabled", meta.Entry.Enabled, defaultEntry.Enabled, out var enabled))
                editor.MetaEditor.Change(meta.Copy(meta.Entry with { Enabled = enabled }));

            ImGui.TableNextColumn();
            if (Checkmark("##gmpAnimated", "Gimmick Animated", meta.Entry.Animated, defaultEntry.Animated, out var animated))
                editor.MetaEditor.Change(meta.Copy(meta.Entry with { Animated = animated }));

            ImGui.TableNextColumn();
            if (IntDragInput("##gmpRotationA", $"Rotation A in Degrees\nDefault Value: {defaultEntry.RotationA}", RotationWidth,
                    meta.Entry.RotationA,      defaultEntry.RotationA, out var rotationA, 0, 360, 0.05f))
                editor.MetaEditor.Change(meta.Copy(meta.Entry with { RotationA = (ushort)rotationA }));

            ImGui.SameLine();
            if (IntDragInput("##gmpRotationB", $"Rotation B in Degrees\nDefault Value: {defaultEntry.RotationB}", RotationWidth,
                    meta.Entry.RotationB,      defaultEntry.RotationB, out var rotationB, 0, 360, 0.05f))
                editor.MetaEditor.Change(meta.Copy(meta.Entry with { RotationB = (ushort)rotationB }));

            ImGui.SameLine();
            if (IntDragInput("##gmpRotationC", $"Rotation C in Degrees\nDefault Value: {defaultEntry.RotationC}", RotationWidth,
                    meta.Entry.RotationC,      defaultEntry.RotationC, out var rotationC, 0, 360, 0.05f))
                editor.MetaEditor.Change(meta.Copy(meta.Entry with { RotationC = (ushort)rotationC }));

            ImGui.TableNextColumn();
            if (IntDragInput("##gmpUnkA",  $"Animation Type A?\nDefault Value: {defaultEntry.UnknownA}", UnkWidth, meta.Entry.UnknownA,
                    defaultEntry.UnknownA, out var unkA,                                                 0,        15, 0.01f))
                editor.MetaEditor.Change(meta.Copy(meta.Entry with { UnknownA = (byte)unkA }));

            ImGui.SameLine();
            if (IntDragInput("##gmpUnkB",  $"Animation Type B?\nDefault Value: {defaultEntry.UnknownB}", UnkWidth, meta.Entry.UnknownB,
                    defaultEntry.UnknownB, out var unkB,                                                 0,        15, 0.01f))
                editor.MetaEditor.Change(meta.Copy(meta.Entry with { UnknownA = (byte)unkB }));
        }
    }

    private static class RspRow
    {
        private static RspManipulation _new = new(SubRace.Midlander, RspAttribute.MaleMinSize, 1f);

        private static float FloatWidth
            => 150 * UiHelpers.Scale;

        public static void DrawNew(MetaFileManager metaFileManager, ModEditor editor, Vector2 iconSize)
        {
            ImGui.TableNextColumn();
            CopyToClipboardButton("Copy all current RSP manipulations to clipboard.", iconSize,
                editor.MetaEditor.Rsp.Select(m => (MetaManipulation)m));
            ImGui.TableNextColumn();
            var canAdd       = editor.MetaEditor.CanAdd(_new);
            var tt           = canAdd ? "Stage this edit." : "This entry is already edited.";
            var defaultEntry = CmpFile.GetDefault(metaFileManager, _new.SubRace, _new.Attribute);
            if (ImGuiUtil.DrawDisabledButton(FontAwesomeIcon.Plus.ToIconString(), iconSize, tt, !canAdd, true))
                editor.MetaEditor.Add(_new.Copy(defaultEntry));

            // Identifier
            ImGui.TableNextColumn();
            if (Combos.SubRace("##rspSubRace", _new.SubRace, out var subRace))
                _new = new RspManipulation(subRace, _new.Attribute, CmpFile.GetDefault(metaFileManager, subRace, _new.Attribute));

            ImGuiUtil.HoverTooltip(RacialTribeTooltip);

            ImGui.TableNextColumn();
            if (Combos.RspAttribute("##rspAttribute", _new.Attribute, out var attribute))
                _new = new RspManipulation(_new.SubRace, attribute, CmpFile.GetDefault(metaFileManager, subRace, attribute));

            ImGuiUtil.HoverTooltip(ScalingTypeTooltip);

            // Values
            using var disabled = ImRaii.Disabled();
            ImGui.TableNextColumn();
            ImGui.SetNextItemWidth(FloatWidth);
            ImGui.DragFloat("##rspValue", ref defaultEntry, 0f);
        }

        public static void Draw(MetaFileManager metaFileManager, RspManipulation meta, ModEditor editor, Vector2 iconSize)
        {
            DrawMetaButtons(meta, editor, iconSize);

            // Identifier
            ImGui.TableNextColumn();
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetStyle().FramePadding.X);
            ImGui.TextUnformatted(meta.SubRace.ToName());
            ImGuiUtil.HoverTooltip(RacialTribeTooltip);
            ImGui.TableNextColumn();
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetStyle().FramePadding.X);
            ImGui.TextUnformatted(meta.Attribute.ToFullString());
            ImGuiUtil.HoverTooltip(ScalingTypeTooltip);
            ImGui.TableNextColumn();

            // Values
            var def   = CmpFile.GetDefault(metaFileManager, meta.SubRace, meta.Attribute);
            var value = meta.Entry;
            ImGui.SetNextItemWidth(FloatWidth);
            using var color = ImRaii.PushColor(ImGuiCol.FrameBg,
                def < value ? ColorId.IncreasedMetaValue.Value() : ColorId.DecreasedMetaValue.Value(),
                def != value);
            if (ImGui.DragFloat("##rspValue", ref value, 0.001f, RspManipulation.MinValue, RspManipulation.MaxValue) && value is >= RspManipulation.MinValue and <= RspManipulation.MaxValue)
                editor.MetaEditor.Change(meta.Copy(value));

            ImGuiUtil.HoverTooltip($"Default Value: {def:0.###}");
        }
    }

    // A number input for ids with a optional max id of given width.
    // Returns true if newId changed against currentId.
    private static bool IdInput(string label, float width, ushort currentId, out ushort newId, int minId, int maxId, bool border)
    {
        int tmp = currentId;
        ImGui.SetNextItemWidth(width);
        using var style = ImRaii.PushStyle(ImGuiStyleVar.FrameBorderSize, UiHelpers.Scale, border);
        using var color = ImRaii.PushColor(ImGuiCol.Border, Colors.RegexWarningBorder, border);
        if (ImGui.InputInt(label, ref tmp, 0))
            tmp = Math.Clamp(tmp, minId, maxId);

        newId = (ushort)tmp;
        return newId != currentId;
    }

    // A checkmark that compares against a default value and shows a tooltip.
    // Returns true if newValue is changed against currentValue.
    private static bool Checkmark(string label, string tooltip, bool currentValue, bool defaultValue, out bool newValue)
    {
        using var color = ImRaii.PushColor(ImGuiCol.FrameBg,
            defaultValue ? ColorId.DecreasedMetaValue.Value() : ColorId.IncreasedMetaValue.Value(),
            defaultValue != currentValue);
        newValue = currentValue;
        ImGui.Checkbox(label, ref newValue);
        ImGuiUtil.HoverTooltip(tooltip, ImGuiHoveredFlags.AllowWhenDisabled);
        return newValue != currentValue;
    }

    // A dragging int input of given width that compares against a default value, shows a tooltip and clamps against min and max.
    // Returns true if newValue changed against currentValue.
    private static bool IntDragInput(string label, string tooltip, float width, int currentValue, int defaultValue, out int newValue,
        int minValue, int maxValue, float speed)
    {
        newValue = currentValue;
        using var color = ImRaii.PushColor(ImGuiCol.FrameBg,
            defaultValue > currentValue ? ColorId.DecreasedMetaValue.Value() : ColorId.IncreasedMetaValue.Value(),
            defaultValue != currentValue);
        ImGui.SetNextItemWidth(width);
        if (ImGui.DragInt(label, ref newValue, speed, minValue, maxValue))
            newValue = Math.Clamp(newValue, minValue, maxValue);

        ImGuiUtil.HoverTooltip(tooltip, ImGuiHoveredFlags.AllowWhenDisabled);

        return newValue != currentValue;
    }

    private static void CopyToClipboardButton(string tooltip, Vector2 iconSize, IEnumerable<MetaManipulation> manipulations)
    {
        if (!ImGuiUtil.DrawDisabledButton(FontAwesomeIcon.Clipboard.ToIconString(), iconSize, tooltip, false, true))
            return;

        var text = Functions.ToCompressedBase64(manipulations, MetaManipulation.CurrentVersion);
        if (text.Length > 0)
            ImGui.SetClipboardText(text);
    }

    private void AddFromClipboardButton()
    {
        if (ImGui.Button("Add from Clipboard"))
        {
            var clipboard = ImGuiUtil.GetClipboardText();

            var version = Functions.FromCompressedBase64<MetaManipulation[]>(clipboard, out var manips);
            if (version == MetaManipulation.CurrentVersion && manips != null)
                foreach (var manip in manips.Where(m => m.ManipulationType != MetaManipulation.Type.Unknown))
                    _editor.MetaEditor.Set(manip);
        }

        ImGuiUtil.HoverTooltip(
            "Try to add meta manipulations currently stored in the clipboard to the current manipulations.\nOverwrites already existing manipulations.");
    }

    private void SetFromClipboardButton()
    {
        if (ImGui.Button("Set from Clipboard"))
        {
            var clipboard = ImGuiUtil.GetClipboardText();
            var version   = Functions.FromCompressedBase64<MetaManipulation[]>(clipboard, out var manips);
            if (version == MetaManipulation.CurrentVersion && manips != null)
            {
                _editor.MetaEditor.Clear();
                foreach (var manip in manips.Where(m => m.ManipulationType != MetaManipulation.Type.Unknown))
                    _editor.MetaEditor.Set(manip);
            }
        }

        ImGuiUtil.HoverTooltip(
            "Try to set the current meta manipulations to the set currently stored in the clipboard.\nRemoves all other manipulations.");
    }

    private static void DrawMetaButtons(MetaManipulation meta, ModEditor editor, Vector2 iconSize)
    {
        ImGui.TableNextColumn();
        CopyToClipboardButton("Copy this manipulation to clipboard.", iconSize, Array.Empty<MetaManipulation>().Append(meta));

        ImGui.TableNextColumn();
        if (ImGuiUtil.DrawDisabledButton(FontAwesomeIcon.Trash.ToIconString(), iconSize, "Delete this meta manipulation.", false, true))
            editor.MetaEditor.Delete(meta);
    }
}
