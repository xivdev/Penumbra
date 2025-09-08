using Dalamud.Interface;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Bindings.ImGui;
using Newtonsoft.Json.Linq;
using OtterGui.Extensions;
using OtterGui.Raii;
using OtterGui.Text;
using OtterGui.Widgets;
using Penumbra.Collections.Cache;
using Penumbra.GameData.Data;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Files;
using Penumbra.GameData.Files.AtchStructs;
using Penumbra.Meta;
using Penumbra.Meta.Manipulations;
using Penumbra.Mods.Editor;
using Penumbra.UI.Classes;
using Notification = OtterGui.Classes.Notification;

namespace Penumbra.UI.AdvancedWindow.Meta;

public sealed class AtchMetaDrawer : MetaDrawer<AtchIdentifier, AtchEntry>, Luna.IService
{
    public override ReadOnlySpan<byte> Label
        => "Attachment Points (ATCH)###ATCH"u8;

    public override int NumColumns
        => 10;

    public override float ColumnHeight
        => 2 * ImUtf8.FrameHeightSpacing;

    private          AtchFile?      _currentBaseAtchFile;
    private          AtchPoint?     _currentBaseAtchPoint;
    private readonly AtchPointCombo _combo;
    private          string         _fileImport = string.Empty;

    public AtchMetaDrawer(ModMetaEditor editor, MetaFileManager metaFiles)
        : base(editor, metaFiles)
    {
        _combo = new AtchPointCombo(() => _currentBaseAtchFile?.Points.Select(p => p.Type).ToList() ?? []);
    }

    private sealed class AtchPointCombo(Func<IReadOnlyList<AtchType>> generator)
        : FilterComboCache<AtchType>(generator, MouseWheelType.Control, Penumbra.Log)
    {
        protected override string ToString(AtchType obj)
            => obj.ToName();
    }

    private sealed class RaceCodeException(string filePath) : Exception($"Could not identify race code from path {filePath}.");

    public void ImportFile(string filePath)
    {
        try
        {
            if (filePath.Length == 0 || !File.Exists(filePath))
                throw new FileNotFoundException();

            var gr = Parser.ParseRaceCode(filePath);
            if (gr is GenderRace.Unknown)
                throw new RaceCodeException(filePath);

            var text = File.ReadAllBytes(filePath);
            var file = new AtchFile(text);
            foreach (var point in file.Points)
            {
                foreach (var (entry, index) in point.Entries.WithIndex())
                {
                    var identifier   = new AtchIdentifier(point.Type, gr, (ushort)index);
                    var defaultValue = AtchCache.GetDefault(MetaFiles, identifier);
                    if (defaultValue == null)
                        continue;

                    if (defaultValue.Value.Equals(entry))
                        Editor.Changes |= Editor.Remove(identifier);
                    else
                        Editor.Changes |= Editor.TryAdd(identifier, entry) || Editor.Update(identifier, entry);
                }
            }
        }
        catch (RaceCodeException ex)
        {
            Penumbra.Messager.AddMessage(new Notification(ex, "The imported .atch file does not contain a race code (cXXXX) in its name.",
                "Could not import .atch file:",
                NotificationType.Warning));
        }
        catch (Exception ex)
        {
            Penumbra.Messager.AddMessage(new Notification(ex, "Unable to import .atch file.", "Could not import .atch file:",
                NotificationType.Warning));
        }
    }


    protected override void DrawNew()
    {
        ImGui.TableNextColumn();
        CopyToClipboardButton("Copy all current ATCH manipulations to clipboard."u8,
            new Lazy<JToken?>(() => MetaDictionary.SerializeTo([], Editor.Atch)));

        ImGui.TableNextColumn();
        var canAdd = !Editor.Contains(Identifier);
        var tt     = canAdd ? "Stage this edit."u8 : "This entry is already edited."u8;
        if (ImUtf8.IconButton(FontAwesomeIcon.Plus, tt, disabled: !canAdd))
            Editor.Changes |= Editor.TryAdd(Identifier, Entry);

        if (DrawIdentifierInput(ref Identifier))
            UpdateEntry();

        var defaultEntry = AtchCache.GetDefault(MetaFiles, Identifier) ?? default;
        DrawEntry(defaultEntry, ref defaultEntry, true);
    }

    private void UpdateEntry()
        => Entry = _currentBaseAtchPoint!.Entries[Identifier.EntryIndex];

    protected override void Initialize()
    {
        _currentBaseAtchFile  = MetaFiles.AtchManager.AtchFileBase[GenderRace.MidlanderMale];
        _currentBaseAtchPoint = _currentBaseAtchFile.Points.First();
        Identifier            = new AtchIdentifier(_currentBaseAtchPoint.Type, GenderRace.MidlanderMale, 0);
        Entry                 = _currentBaseAtchPoint.Entries[0];
    }

    protected override void DrawEntry(AtchIdentifier identifier, AtchEntry entry)
    {
        DrawMetaButtons(identifier, entry);
        DrawIdentifier(identifier);

        var defaultEntry = AtchCache.GetDefault(MetaFiles, identifier) ?? default;
        if (DrawEntry(defaultEntry, ref entry, false))
            Editor.Changes |= Editor.Update(identifier, entry);
    }

    protected override IEnumerable<(AtchIdentifier, AtchEntry)> Enumerate()
        => Editor.Atch.Select(kvp => (kvp.Key, kvp.Value))
            .OrderBy(p => p.Key.GenderRace)
            .ThenBy(p => p.Key.Type)
            .ThenBy(p => p.Key.EntryIndex);

    protected override int Count
        => Editor.Atch.Count;

    private bool DrawIdentifierInput(ref AtchIdentifier identifier)
    {
        var changes = false;
        ImGui.TableNextColumn();
        changes |= DrawRace(ref identifier);
        ImGui.TableNextColumn();
        changes |= DrawGender(ref identifier, false);
        if (changes)
            UpdateFile();
        ImGui.TableNextColumn();
        if (DrawPointInput(ref identifier, _combo))
        {
            _currentBaseAtchPoint = _currentBaseAtchFile?.GetPoint(identifier.Type);
            changes               = true;
        }

        ImGui.TableNextColumn();
        changes |= DrawEntryIndexInput(ref identifier, _currentBaseAtchPoint!);

        return changes;
    }

    private void UpdateFile()
    {
        _currentBaseAtchFile  = MetaFiles.AtchManager.AtchFileBase[Identifier.GenderRace];
        _currentBaseAtchPoint = _currentBaseAtchFile.GetPoint(Identifier.Type);
        if (_currentBaseAtchPoint == null)
        {
            _currentBaseAtchPoint = _currentBaseAtchFile.Points.First();
            Identifier            = Identifier with { Type = _currentBaseAtchPoint.Type };
        }

        if (Identifier.EntryIndex >= _currentBaseAtchPoint.Entries.Length)
            Identifier = Identifier with { EntryIndex = 0 };
    }

    private static void DrawIdentifier(AtchIdentifier identifier)
    {
        ImGui.TableNextColumn();
        ImUtf8.TextFramed(identifier.Race.ToName(), FrameColor);
        ImUtf8.HoverTooltip("Model Race"u8);

        ImGui.TableNextColumn();
        DrawGender(ref identifier, true);

        ImGui.TableNextColumn();
        ImUtf8.TextFramed(identifier.Type.ToName(), FrameColor);
        ImUtf8.HoverTooltip("Attachment Point Type"u8);

        ImGui.TableNextColumn();
        ImUtf8.TextFramed(identifier.EntryIndex.ToString(), FrameColor);
        ImUtf8.HoverTooltip("State Entry Index"u8);
    }

    private static bool DrawEntry(in AtchEntry defaultEntry, ref AtchEntry entry, bool disabled)
    {
        var       changes = false;
        using var dis     = ImRaii.Disabled(disabled);
        if (defaultEntry.Bone.Length == 0)
            return false;

        ImGui.TableNextColumn();
        ImGui.SetNextItemWidth(200 * ImUtf8.GlobalScale);
        if (ImUtf8.InputText("##BoneName"u8, entry.FullSpan, out TerminatedByteString newBone))
        {
            entry.SetBoneName(newBone);
            changes = true;
        }

        ImUtf8.HoverTooltip(ImGuiHoveredFlags.AllowWhenDisabled, "Bone Name"u8);

        ImGui.SetNextItemWidth(200 * ImUtf8.GlobalScale);
        changes |= ImUtf8.InputScalar("##AtchScale"u8, ref entry.Scale);
        ImUtf8.HoverTooltip(ImGuiHoveredFlags.AllowWhenDisabled, "Scale"u8);

        ImGui.TableNextColumn();
        ImGui.SetNextItemWidth(120 * ImUtf8.GlobalScale);
        changes |= ImUtf8.InputScalar("##AtchOffsetX"u8, ref entry.OffsetX);
        ImUtf8.HoverTooltip(ImGuiHoveredFlags.AllowWhenDisabled, "Offset X-Coordinate"u8);
        ImGui.SetNextItemWidth(120 * ImUtf8.GlobalScale);
        changes |= ImUtf8.InputScalar("##AtchRotationX"u8, ref entry.RotationX);
        ImUtf8.HoverTooltip(ImGuiHoveredFlags.AllowWhenDisabled, "Rotation X-Axis"u8);

        ImGui.TableNextColumn();
        ImGui.SetNextItemWidth(120 * ImUtf8.GlobalScale);
        changes |= ImUtf8.InputScalar("##AtchOffsetY"u8, ref entry.OffsetY);
        ImUtf8.HoverTooltip(ImGuiHoveredFlags.AllowWhenDisabled, "Offset Y-Coordinate"u8);
        ImGui.SetNextItemWidth(120 * ImUtf8.GlobalScale);
        changes |= ImUtf8.InputScalar("##AtchRotationY"u8, ref entry.RotationY);
        ImUtf8.HoverTooltip(ImGuiHoveredFlags.AllowWhenDisabled, "Rotation Y-Axis"u8);

        ImGui.TableNextColumn();
        ImGui.SetNextItemWidth(120 * ImUtf8.GlobalScale);
        changes |= ImUtf8.InputScalar("##AtchOffsetZ"u8, ref entry.OffsetZ);
        ImUtf8.HoverTooltip("Offset Z-Coordinate"u8);
        ImGui.SetNextItemWidth(120 * ImUtf8.GlobalScale);
        changes |= ImUtf8.InputScalar("##AtchRotationZ"u8, ref entry.RotationZ);
        ImUtf8.HoverTooltip(ImGuiHoveredFlags.AllowWhenDisabled, "Rotation Z-Axis"u8);

        return changes;
    }

    private static bool DrawRace(ref AtchIdentifier identifier, float unscaledWidth = 100)
    {
        var ret = Combos.Race("##atchRace", identifier.Race, out var race, unscaledWidth);
        ImUtf8.HoverTooltip("Model Race"u8);
        if (ret)
            identifier = identifier with { GenderRace = Names.CombinedRace(identifier.Gender, race) };

        return ret;
    }

    private static bool DrawGender(ref AtchIdentifier identifier, bool disabled)
    {
        var isMale = identifier.Gender is Gender.Male;

        if (!ImUtf8.IconButton(isMale ? FontAwesomeIcon.Mars : FontAwesomeIcon.Venus, "Gender"u8, buttonColor: disabled ? 0x000F0000u : 0)
         || disabled)
            return false;

        identifier = identifier with { GenderRace = Names.CombinedRace(isMale ? Gender.Female : Gender.Male, identifier.Race) };
        return true;
    }

    private static bool DrawPointInput(ref AtchIdentifier identifier, AtchPointCombo combo)
    {
        if (!combo.Draw("##AtchPoint", identifier.Type.ToName(), "Attachment Point Type", 160 * ImUtf8.GlobalScale,
                ImGui.GetTextLineHeightWithSpacing()))
            return false;

        identifier = identifier with { Type = combo.CurrentSelection };
        return true;
    }

    private static bool DrawEntryIndexInput(ref AtchIdentifier identifier, AtchPoint currentAtchPoint)
    {
        var index = identifier.EntryIndex;
        ImGui.SetNextItemWidth(40 * ImUtf8.GlobalScale);
        var ret = ImUtf8.DragScalar("##AtchEntry"u8, ref index, 0, (ushort)(currentAtchPoint.Entries.Length - 1), 0.05f,
            ImGuiSliderFlags.AlwaysClamp);
        ImUtf8.HoverTooltip("State Entry Index"u8);
        if (!ret)
            return false;

        index      = Math.Clamp(index, (ushort)0, (ushort)(currentAtchPoint.Entries.Length - 1));
        identifier = identifier with { EntryIndex = index };
        return true;
    }
}
