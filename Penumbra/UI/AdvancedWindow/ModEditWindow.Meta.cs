using System.Reflection.Emit;
using Dalamud.Interface;
using ImGuiNET;
using OtterGui;
using OtterGui.Raii;
using OtterGui.Text;
using OtterGui.Text.EndObjects;
using Penumbra.GameData.Enums;
using Penumbra.Interop.Structs;
using Penumbra.Meta;
using Penumbra.Meta.Files;
using Penumbra.Meta.Manipulations;
using Penumbra.Mods.Editor;
using Penumbra.UI.Classes;
using Penumbra.UI.ModsTab;

namespace Penumbra.UI.AdvancedWindow;

public partial class ModEditWindow
{
    private const string ModelSetIdTooltip =
        "Model Set ID - You can usually find this as the 'e####' part of an item path.\nThis should generally not be left <= 1 unless you explicitly want that.";



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
            _editor.MetaEditor.Apply(_editor.Option!);

        ImGui.SameLine();
        tt = setsEqual ? "No changes staged." : "Revert all currently staged changes.";
        if (ImGuiUtil.DrawDisabledButton("Revert Changes", Vector2.Zero, tt, setsEqual))
            _editor.MetaEditor.Load(_editor.Mod!, _editor.Option!);

        ImGui.SameLine();
        AddFromClipboardButton();
        ImGui.SameLine();
        SetFromClipboardButton();
        ImGui.SameLine();
        CopyToClipboardButton("Copy all current manipulations to clipboard.", _iconSize, _editor.MetaEditor);
        ImGui.SameLine();
        if (ImGui.Button("Write as TexTools Files"))
            _metaFileManager.WriteAllTexToolsMeta(Mod!);

        using var child = ImRaii.Child("##meta", -Vector2.One, true);
        if (!child)
            return;

        DrawEditHeader(MetaManipulation.Type.Eqp);
        DrawEditHeader(MetaManipulation.Type.Eqdp);
        DrawEditHeader(MetaManipulation.Type.Imc);
        DrawEditHeader(MetaManipulation.Type.Est);
        DrawEditHeader(MetaManipulation.Type.Gmp);
        DrawEditHeader(MetaManipulation.Type.Rsp);
        DrawEditHeader(MetaManipulation.Type.GlobalEqp);
    }

    private static ReadOnlySpan<byte> Label(MetaManipulation.Type type)
        => type switch
        {
            MetaManipulation.Type.Imc       => "Variant Edits (IMC)###IMC"u8,
            MetaManipulation.Type.Eqdp      => "Racial Model Edits (EQDP)###EQDP"u8,
            MetaManipulation.Type.Eqp       => "Equipment Parameter Edits (EQP)###EQP"u8,
            MetaManipulation.Type.Est       => "Extra Skeleton Parameters (EST)###EST"u8,
            MetaManipulation.Type.Gmp       => "Visor/Gimmick Edits (GMP)###GMP"u8,
            MetaManipulation.Type.Rsp       => "Racial Scaling Edits (RSP)###RSP"u8,
            MetaManipulation.Type.GlobalEqp => "Global Equipment Parameter Edits (Global EQP)###GEQP"u8,
            _                               => "\0"u8,
        };

    private static int ColumnCount(MetaManipulation.Type type)
        => type switch
        {
            MetaManipulation.Type.Imc       => 10,
            MetaManipulation.Type.Eqdp      => 7,
            MetaManipulation.Type.Eqp       => 5,
            MetaManipulation.Type.Est       => 7,
            MetaManipulation.Type.Gmp       => 7,
            MetaManipulation.Type.Rsp       => 5,
            MetaManipulation.Type.GlobalEqp => 4,
            _                               => 0,
        };

    private void DrawEditHeader(MetaManipulation.Type type)
    {
        var oldPos = ImGui.GetCursorPosY();
        var header = ImUtf8.CollapsingHeader($"{_editor.MetaEditor.GetCount(type)} {Label(type)}");
        DrawOtherOptionData(type, oldPos, ImGui.GetCursorPos());
        if (!header)
            return;

        DrawTable(type);
    }

    private IMetaDrawer? Drawer(MetaManipulation.Type type)
        => type switch
        {
            //MetaManipulation.Type.Imc       => expr,
            //MetaManipulation.Type.Eqdp      => expr,
            //MetaManipulation.Type.Eqp       => expr,
            //MetaManipulation.Type.Est       => expr,
            //MetaManipulation.Type.Gmp       => expr,
            //MetaManipulation.Type.Rsp       => expr,
            //MetaManipulation.Type.GlobalEqp => expr,
            _ => null,
        };

    private void DrawTable(MetaManipulation.Type type)
    {
        const ImGuiTableFlags flags = ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.BordersInnerV;
        using var             table = ImUtf8.Table(Label(type), ColumnCount(type), flags);
        if (!table)
            return;

        if (Drawer(type) is not { } drawer)
            return;

        drawer.Draw();
        ImGui.NewLine();
    }

    private void DrawOtherOptionData(MetaManipulation.Type type, float oldPos, Vector2 newPos)
    {
        var otherOptionData = _editor.MetaEditor.OtherData[type];
        if (otherOptionData.TotalCount <= 0)
            return;

        var text = $"{otherOptionData.TotalCount} Edits in other Options";
        var size = ImGui.CalcTextSize(text).X;
        ImGui.SetCursorPos(new Vector2(ImGui.GetContentRegionAvail().X - size, oldPos + ImGui.GetStyle().FramePadding.Y));
        ImGuiUtil.TextColored(ColorId.RedundantAssignment.Value() | 0xFF000000, text);
        if (ImGui.IsItemHovered())
        {
            using var tt = ImUtf8.Tooltip();
            foreach (var name in otherOptionData)
                ImUtf8.Text(name);
        }

        ImGui.SetCursorPos(newPos);
    }

#if false
    private static class EqpRow
    {
        private static EqpIdentifier _newIdentifier = new(1, EquipSlot.Body);

        private static float IdWidth
            => 100 * UiHelpers.Scale;

        public static void DrawNew(MetaFileManager metaFileManager, ModEditor editor, Vector2 iconSize)
        {
            ImGui.TableNextColumn();
            CopyToClipboardButton("Copy all current EQP manipulations to clipboard.", iconSize,
                editor.MetaEditor.Eqp.Select(m => (MetaManipulation)m));
            ImGui.TableNextColumn();
            var canAdd = editor.MetaEditor.CanAdd(_new);
            var tt = canAdd ? "Stage this edit." : "This entry is already edited.";
            var defaultEntry = ExpandedEqpFile.GetDefault(metaFileManager, _new.SetId);
            if (ImGuiUtil.DrawDisabledButton(FontAwesomeIcon.Plus.ToIconString(), iconSize, tt, !canAdd, true))
                editor.MetaEditor.Add(_new.Copy(defaultEntry));

            // Identifier
            ImGui.TableNextColumn();
            if (IdInput("##eqpId", IdWidth, _new.SetId.Id, out var setId, 1, ExpandedEqpGmpBase.Count - 1, _new.SetId <= 1))
                _new = new EqpManipulation(ExpandedEqpFile.GetDefault(metaFileManager, setId), _new.Slot, setId);

            ImGuiUtil.HoverTooltip(ModelSetIdTooltip);

            ImGui.TableNextColumn();
            if (Combos.EqpEquipSlot("##eqpSlot", _new.Slot, out var slot))
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
                using var id = ImRaii.PushId(idx++);
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
            var raceCode = Names.CombinedRace(_new.Gender, _new.Race);
            var validRaceCode = CharacterUtilityData.EqdpIdx(raceCode, false) >= 0;
            var canAdd = validRaceCode && editor.MetaEditor.CanAdd(_new);
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
                var newDefaultEntry = ExpandedEqdpFile.GetDefault(metaFileManager, Names.CombinedRace(_new.Gender, _new.Race),
                    _new.Slot.IsAccessory(), setId);
                _new = new EqdpManipulation(newDefaultEntry, _new.Slot, _new.Gender, _new.Race, setId);
            }

            ImGuiUtil.HoverTooltip(ModelSetIdTooltip);

            ImGui.TableNextColumn();
            if (Combos.Race("##eqdpRace", _new.Race, out var race))
            {
                var newDefaultEntry = ExpandedEqdpFile.GetDefault(metaFileManager, Names.CombinedRace(_new.Gender, race),
                    _new.Slot.IsAccessory(), _new.SetId);
                _new = new EqdpManipulation(newDefaultEntry, _new.Slot, _new.Gender, race, _new.SetId);
            }

            ImGuiUtil.HoverTooltip(ModelRaceTooltip);

            ImGui.TableNextColumn();
            if (Combos.Gender("##eqdpGender", _new.Gender, out var gender))
            {
                var newDefaultEntry = ExpandedEqdpFile.GetDefault(metaFileManager, Names.CombinedRace(gender, _new.Race),
                    _new.Slot.IsAccessory(), _new.SetId);
                _new = new EqdpManipulation(newDefaultEntry, _new.Slot, gender, _new.Race, _new.SetId);
            }

            ImGuiUtil.HoverTooltip(GenderTooltip);

            ImGui.TableNextColumn();
            if (Combos.EqdpEquipSlot("##eqdpSlot", _new.Slot, out var slot))
            {
                var newDefaultEntry = ExpandedEqdpFile.GetDefault(metaFileManager, Names.CombinedRace(_new.Gender, _new.Race),
                    slot.IsAccessory(), _new.SetId);
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
            var defaultEntry = ExpandedEqdpFile.GetDefault(metaFileManager, Names.CombinedRace(meta.Gender, meta.Race), meta.Slot.IsAccessory(),
                meta.SetId);
            var (defaultBit1, defaultBit2) = defaultEntry.ToBits(meta.Slot);
            var (bit1, bit2) = meta.Entry.ToBits(meta.Slot);
            ImGui.TableNextColumn();
            if (Checkmark("Material##eqdpCheck1", string.Empty, bit1, defaultBit1, out var newBit1))
                editor.MetaEditor.Change(meta.Copy(Eqdp.FromSlotAndBits(meta.Slot, newBit1, bit2)));

            ImGui.SameLine();
            if (Checkmark("Model##eqdpCheck2", string.Empty, bit2, defaultBit2, out var newBit2))
                editor.MetaEditor.Change(meta.Copy(Eqdp.FromSlotAndBits(meta.Slot, bit1, newBit2)));
        }
    }
    
    private static class EstRow
    {
        private static EstManipulation _new = new(Gender.Male, ModelRace.Midlander, EstType.Body, 1, EstEntry.Zero);

        private static float IdWidth
            => 100 * UiHelpers.Scale;

        public static void DrawNew(MetaFileManager metaFileManager, ModEditor editor, Vector2 iconSize)
        {
            ImGui.TableNextColumn();
            CopyToClipboardButton("Copy all current EST manipulations to clipboard.", iconSize,
                editor.MetaEditor.Est.Select(m => (MetaManipulation)m));
            ImGui.TableNextColumn();
            var canAdd = editor.MetaEditor.CanAdd(_new);
            var tt = canAdd ? "Stage this edit." : "This entry is already edited.";
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
            IntDragInput("##estSkeleton", "Skeleton Index", IdWidth, _new.Entry.Value, defaultEntry.Value, out _, 0, ushort.MaxValue, 0.05f);
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
            if (IntDragInput("##estSkeleton", $"Skeleton Index\nDefault Value: {defaultEntry}", IdWidth, meta.Entry.Value, defaultEntry.Value,
                    out var entry,            0,                                                ushort.MaxValue, 0.05f))
                editor.MetaEditor.Change(meta.Copy(new EstEntry((ushort)entry)));
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
            var canAdd = editor.MetaEditor.CanAdd(_new);
            var tt = canAdd ? "Stage this edit." : "This entry is already edited.";
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
                editor.MetaEditor.Change(meta.Copy(meta.Entry with { UnknownB = (byte)unkB }));
        }
    }
    private static class RspRow
    {
        private static RspManipulation _new = new(SubRace.Midlander, RspAttribute.MaleMinSize, RspEntry.One);

        private static float FloatWidth
            => 150 * UiHelpers.Scale;

        public static void DrawNew(MetaFileManager metaFileManager, ModEditor editor, Vector2 iconSize)
        {
            ImGui.TableNextColumn();
            CopyToClipboardButton("Copy all current RSP manipulations to clipboard.", iconSize,
                editor.MetaEditor.Rsp.Select(m => (MetaManipulation)m));
            ImGui.TableNextColumn();
            var canAdd = editor.MetaEditor.CanAdd(_new);
            var tt = canAdd ? "Stage this edit." : "This entry is already edited.";
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
            var value = defaultEntry.Value;
            ImGui.DragFloat("##rspValue", ref value, 0f);
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
            var def = CmpFile.GetDefault(metaFileManager, meta.SubRace, meta.Attribute).Value;
            var value = meta.Entry.Value;
            ImGui.SetNextItemWidth(FloatWidth);
            using var color = ImRaii.PushColor(ImGuiCol.FrameBg,
                def < value ? ColorId.IncreasedMetaValue.Value() : ColorId.DecreasedMetaValue.Value(),
                def != value);
            if (ImGui.DragFloat("##rspValue", ref value, 0.001f, RspEntry.MinValue, RspEntry.MaxValue)
             && value is >= RspEntry.MinValue and <= RspEntry.MaxValue)
                editor.MetaEditor.Change(meta.Copy(new RspEntry(value)));

            ImGuiUtil.HoverTooltip($"Default Value: {def:0.###}");
        }
    }
    private static class GlobalEqpRow
    {
        private static GlobalEqpManipulation _new = new()
        {
            Type = GlobalEqpType.DoNotHideEarrings,
            Condition = 1,
        };

        public static void DrawNew(MetaFileManager metaFileManager, ModEditor editor, Vector2 iconSize)
        {
            ImGui.TableNextColumn();
            CopyToClipboardButton("Copy all current global EQP manipulations to clipboard.", iconSize,
                editor.MetaEditor.GlobalEqp.Select(m => (MetaManipulation)m));
            ImGui.TableNextColumn();
            var canAdd = editor.MetaEditor.CanAdd(_new);
            var tt = canAdd ? "Stage this edit." : "This entry is already manipulated.";
            if (ImGuiUtil.DrawDisabledButton(FontAwesomeIcon.Plus.ToIconString(), iconSize, tt, !canAdd, true))
                editor.MetaEditor.Add(_new);

            // Identifier
            ImGui.TableNextColumn();
            ImGui.SetNextItemWidth(250 * ImUtf8.GlobalScale);
            using (var combo = ImUtf8.Combo("##geqpType"u8, _new.Type.ToName()))
            {
                if (combo)
                    foreach (var type in Enum.GetValues<GlobalEqpType>())
                    {
                        if (ImUtf8.Selectable(type.ToName(), type == _new.Type))
                            _new = new GlobalEqpManipulation
                            {
                                Type = type,
                                Condition = type.HasCondition() ? _new.Type.HasCondition() ? _new.Condition : 1 : 0,
                            };
                        ImUtf8.HoverTooltip(type.ToDescription());
                    }
            }

            ImUtf8.HoverTooltip(_new.Type.ToDescription());

            ImGui.TableNextColumn();
            if (!_new.Type.HasCondition())
                return;

            if (IdInput("##geqpCond", 100 * ImUtf8.GlobalScale, _new.Condition.Id, out var newId, 1, ushort.MaxValue, _new.Condition.Id <= 1))
                _new = _new with { Condition = newId };
            ImUtf8.HoverTooltip("The Model ID for the item that should not be hidden."u8);
        }

        public static void Draw(MetaFileManager metaFileManager, GlobalEqpManipulation meta, ModEditor editor, Vector2 iconSize)
        {
            DrawMetaButtons(meta, editor, iconSize);
            ImGui.TableNextColumn();
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetStyle().FramePadding.X);
            ImUtf8.Text(meta.Type.ToName());
            ImUtf8.HoverTooltip(meta.Type.ToDescription());
            ImGui.TableNextColumn();
            if (meta.Type.HasCondition())
            {
                ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetStyle().FramePadding.X);
                ImUtf8.Text($"{meta.Condition.Id}");
            }
        }
    }
#endif

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

    private static void CopyToClipboardButton(string tooltip, Vector2 iconSize, MetaDictionary manipulations)
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

            var version = Functions.FromCompressedBase64<MetaDictionary>(clipboard, out var manips);
            if (version == MetaManipulation.CurrentVersion && manips != null)
                _editor.MetaEditor.UpdateTo(manips);
        }

        ImGuiUtil.HoverTooltip(
            "Try to add meta manipulations currently stored in the clipboard to the current manipulations.\nOverwrites already existing manipulations.");
    }

    private void SetFromClipboardButton()
    {
        if (ImGui.Button("Set from Clipboard"))
        {
            var clipboard = ImGuiUtil.GetClipboardText();
            var version   = Functions.FromCompressedBase64<MetaDictionary>(clipboard, out var manips);
            if (version == MetaManipulation.CurrentVersion && manips != null)
                _editor.MetaEditor.SetTo(manips);
        }

        ImGuiUtil.HoverTooltip(
            "Try to set the current meta manipulations to the set currently stored in the clipboard.\nRemoves all other manipulations.");
    }

    private static void DrawMetaButtons(MetaManipulation meta, ModEditor editor, Vector2 iconSize)
    {
        //ImGui.TableNextColumn();
        //CopyToClipboardButton("Copy this manipulation to clipboard.", iconSize, Array.Empty<MetaManipulation>().Append(meta));
        //
        //ImGui.TableNextColumn();
        //if (ImGuiUtil.DrawDisabledButton(FontAwesomeIcon.Trash.ToIconString(), iconSize, "Delete this meta manipulation.", false, true))
        //    editor.MetaEditor.Delete(meta);
    }
}


public interface IMetaDrawer
{
    public void Draw();
}




public abstract class MetaDrawer<TIdentifier, TEntry>(ModEditor editor, MetaFileManager metaFiles) : IMetaDrawer
    where TIdentifier : unmanaged, IMetaIdentifier
    where TEntry : unmanaged
{
    protected readonly ModEditor       Editor = editor;
    protected readonly MetaFileManager MetaFiles = metaFiles;
    protected          TIdentifier     Identifier;
    protected          TEntry          Entry;
    private            bool            _initialized;

    public void Draw()
    {
        if (!_initialized)
        {
            Initialize();
            _initialized = true;
        }

        DrawNew();
        foreach (var ((identifier, entry), idx) in Enumerate().WithIndex())
        {
            using var id = ImUtf8.PushId(idx);
            DrawEntry(identifier, entry);
        }
    }

    protected abstract void DrawNew();
    protected abstract void Initialize();
    protected abstract void DrawEntry(TIdentifier identifier, TEntry entry);

    protected abstract IEnumerable<(TIdentifier, TEntry)> Enumerate();
}


#if false
public sealed class GmpMetaDrawer(ModEditor editor) : MetaDrawer<GmpIdentifier, GmpEntry>, IService
{
    protected override void Initialize()
    {
        Identifier = new GmpIdentifier(1, EquipSlot.Body);
        UpdateEntry();
    }

    private void UpdateEntry()
        => Entry = ExpandedEqpFile.GetDefault(metaManager, Identifier.SetId);

    protected override void DrawNew()
    { }

    protected override void DrawEntry(GmpIdentifier identifier, GmpEntry entry)
    { }

    protected override IEnumerable<(GmpIdentifier, GmpEntry)> Enumerate()
        => editor.MetaEditor.Eqp.Select(kvp => (kvp.Key, kvp.Value.ToEntry(kvp.Key.Slot)));
}

public sealed class EstMetaDrawer(ModEditor editor) : MetaDrawer<EstIdentifier, EstEntry>, IService
{
    protected override void Initialize()
    {
        Identifier = new EqpIdentifier(1, EquipSlot.Body);
        UpdateEntry();
    }

    private void UpdateEntry()
        => Entry = ExpandedEqpFile.GetDefault(metaManager, Identifier.SetId);

    protected override void DrawNew()
    { }

    protected override void DrawEntry(EstIdentifier identifier, EstEntry entry)
    { }

    protected override IEnumerable<(EstIdentifier, EstEntry)> Enumerate()
        => editor.MetaEditor.Eqp.Select(kvp => (kvp.Key, kvp.Value.ToEntry(kvp.Key.Slot)));
}

public sealed class EqdpMetaDrawer(ModEditor editor) : MetaDrawer<EqdpIdentifier, EqdpEntry>, IService
{
    protected override void Initialize()
    {
        Identifier = new EqdpIdentifier(1, EquipSlot.Body);
        UpdateEntry();
    }

    private void UpdateEntry()
        => Entry = ExpandedEqpFile.GetDefault(metaManager, Identifier.SetId);

    protected override void DrawNew()
    { }

    protected override void DrawEntry(EqdpIdentifier identifier, EqdpEntry entry)
    { }

    protected override IEnumerable<(EqdpIdentifier, EqdpEntry)> Enumerate()
        => editor.MetaEditor.Eqp.Select(kvp => (kvp.Key, kvp.Value.ToEntry(kvp.Key.Slot)));
}

public sealed class EqpMetaDrawer(ModEditor editor, MetaFileManager metaManager) : MetaDrawer<EqpIdentifier, EqpEntry>, IService
{
    protected override void Initialize()
    {
        Identifier = new EqpIdentifier(1, EquipSlot.Body);
        UpdateEntry();
    }

    private void UpdateEntry()
        => Entry = ExpandedEqpFile.GetDefault(metaManager, Identifier.SetId);

    protected override void DrawNew()
    { }

    protected override void DrawEntry(EqpIdentifier identifier, EqpEntry entry)
    { }

    protected override IEnumerable<(EqpIdentifier, EqpEntry)> Enumerate()
        => editor.MetaEditor.Eqp.Select(kvp => (kvp.Key, kvp.Value.ToEntry(kvp.Key.Slot)));
}

public sealed class RspMetaDrawer(ModEditor editor) : MetaDrawer<RspIdentifier, RspEntry>, IService
{
    protected override void Initialize()
    {
        Identifier = new RspIdentifier(1, EquipSlot.Body);
        UpdateEntry();
    }

    private void UpdateEntry()
        => Entry = ExpandedEqpFile.GetDefault(metaManager, Identifier.SetId);

    protected override void DrawNew()
    { }

    protected override void DrawEntry(RspIdentifier identifier, RspEntry entry)
    { }

    protected override IEnumerable<(RspIdentifier, RspEntry)> Enumerate()
        => editor.MetaEditor.Eqp.Select(kvp => (kvp.Key, kvp.Value.ToEntry(kvp.Key.Slot)));
}



public sealed class GlobalEqpMetaDrawer(ModEditor editor) : MetaDrawer<GlobalEqpManipulation, byte>, IService
{
    protected override void Initialize()
    {
        Identifier = new EqpIdentifier(1, EquipSlot.Body);
        UpdateEntry();
    }

    private void UpdateEntry()
        => Entry = ExpandedEqpFile.GetDefault(metaManager, Identifier.SetId);

    protected override void DrawNew()
    { }

    protected override void DrawEntry(GlobalEqpManipulation identifier, byte _)
    { }

    protected override IEnumerable<(GlobalEqpManipulation, byte)> Enumerate()
        => editor.MetaEditor.Eqp.Select(kvp => (kvp.Key, kvp.Value.ToEntry(kvp.Key.Slot)));
}
#endif
