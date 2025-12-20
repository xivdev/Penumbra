using Dalamud.Interface;
using Dalamud.Interface.ImGuiNotification;
using ImSharp;
using Luna;
using Newtonsoft.Json.Linq;
using Penumbra.Collections.Cache;
using Penumbra.GameData.Data;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Files;
using Penumbra.GameData.Files.AtchStructs;
using Penumbra.Meta;
using Penumbra.Meta.Manipulations;
using Penumbra.Mods.Editor;
using Notification = Luna.Notification;

namespace Penumbra.UI.AdvancedWindow.Meta;

public sealed class AtchMetaDrawer : MetaDrawer<AtchIdentifier, AtchEntry>
{
    public override ReadOnlySpan<byte> Label
        => "Attachment Points (ATCH)###ATCH"u8;

    public override int NumColumns
        => 10;

    public override float ColumnHeight
        => 2 * Im.Style.FrameHeightWithSpacing;

    private          AtchFile?      _currentBaseAtchFile;
    private          AtchPoint?     _currentBaseAtchPoint;
    private readonly AtchPointCombo _combo;

    public AtchMetaDrawer(ModMetaEditor editor, MetaFileManager metaFiles)
        : base(editor, metaFiles)
    {
        _combo = new AtchPointCombo(this);
    }

    public IEnumerable<AtchType> GetPoints()
        => _currentBaseAtchFile?.Points.Select(p => p.Type) ?? [];

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
                foreach (var (index, entry) in point.Entries.Index())
                {
                    var identifier   = new AtchIdentifier(point.Type, gr, (ushort)index);
                    var defaultValue = AtchCache.GetDefault(MetaFiles, identifier);
                    if (defaultValue is null)
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
                "Could not import .atch file:", NotificationType.Warning));
        }
        catch (Exception ex)
        {
            Penumbra.Messager.AddMessage(new Notification(ex, "Unable to import .atch file.", "Could not import .atch file:",
                NotificationType.Warning));
        }
    }


    protected override void DrawNew()
    {
        Im.Table.NextColumn();
        CopyToClipboardButton("Copy all current ATCH manipulations to clipboard."u8,
            new Lazy<JToken?>(() => MetaDictionary.SerializeTo([], Editor.Atch)));

        Im.Table.NextColumn();
        var canAdd = !Editor.Contains(Identifier);
        var tt     = canAdd ? "Stage this edit."u8 : "This entry is already edited."u8;
        if (ImEx.Icon.Button(LunaStyle.AddObjectIcon, tt, !canAdd))
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
        Im.Table.NextColumn();
        changes |= DrawRace(ref identifier);
        Im.Table.NextColumn();
        changes |= DrawGender(ref identifier, false);
        if (changes)
            UpdateFile();
        Im.Table.NextColumn();
        if (DrawPointInput(ref identifier, _combo))
        {
            _currentBaseAtchPoint = _currentBaseAtchFile?.GetPoint(identifier.Type);
            changes               = true;
        }

        Im.Table.NextColumn();
        changes |= DrawEntryIndexInput(ref identifier, _currentBaseAtchPoint!);

        return changes;
    }

    private void UpdateFile()
    {
        _currentBaseAtchFile  = MetaFiles.AtchManager.AtchFileBase[Identifier.GenderRace];
        _currentBaseAtchPoint = _currentBaseAtchFile.GetPoint(Identifier.Type);
        if (_currentBaseAtchPoint is null)
        {
            _currentBaseAtchPoint = _currentBaseAtchFile.Points.First();
            Identifier            = Identifier with { Type = _currentBaseAtchPoint.Type };
        }

        if (Identifier.EntryIndex >= _currentBaseAtchPoint.Entries.Length)
            Identifier = Identifier with { EntryIndex = 0 };
    }

    private static void DrawIdentifier(AtchIdentifier identifier)
    {
        Im.Table.NextColumn();
        ImEx.TextFramed(identifier.Race.ToNameU8(), default, FrameColor);
        Im.Tooltip.OnHover("Model Race"u8);

        Im.Table.NextColumn();
        DrawGender(ref identifier, true);

        Im.Table.NextColumn();
        ImEx.TextFramed(identifier.Type.ToName(), default, FrameColor);
        Im.Tooltip.OnHover("Attachment Point Type"u8);

        Im.Table.NextColumn();
        ImEx.TextFramed($"{identifier.EntryIndex}", default, FrameColor);
        Im.Tooltip.OnHover("State Entry Index"u8);
    }

    private static bool DrawEntry(in AtchEntry defaultEntry, ref AtchEntry entry, bool disabled)
    {
        var       changes = false;
        using var dis     = Im.Disabled(disabled);
        if (defaultEntry.Bone.Length == 0)
            return false;

        Im.Table.NextColumn();
        Im.Item.SetNextWidthScaled(200);
        if (Im.Input.Text("##BoneName"u8, entry.FullSpan, out StringU8 newBone))
        {
            entry.SetBoneName(newBone);
            changes = true;
        }

        Im.Tooltip.OnHover(HoveredFlags.AllowWhenDisabled, "Bone Name"u8);

        Im.Item.SetNextWidthScaled(200);
        changes |= Im.Input.Scalar("##AtchScale"u8, ref entry.Scale);
        Im.Tooltip.OnHover(HoveredFlags.AllowWhenDisabled, "Scale"u8);

        Im.Table.NextColumn();
        Im.Item.SetNextWidthScaled(120);
        changes |= Im.Input.Scalar("##AtchOffsetX"u8, ref entry.OffsetX);
        Im.Tooltip.OnHover(HoveredFlags.AllowWhenDisabled, "Offset X-Coordinate"u8);
        Im.Item.SetNextWidthScaled(120);
        changes |= Im.Input.Scalar("##AtchRotationX"u8, ref entry.RotationX);
        Im.Tooltip.OnHover(HoveredFlags.AllowWhenDisabled, "Rotation X-Axis"u8);

        Im.Table.NextColumn();
        Im.Item.SetNextWidthScaled(120);
        changes |= Im.Input.Scalar("##AtchOffsetY"u8, ref entry.OffsetY);
        Im.Tooltip.OnHover(HoveredFlags.AllowWhenDisabled, "Offset Y-Coordinate"u8);
        Im.Item.SetNextWidthScaled(120);
        changes |= Im.Input.Scalar("##AtchRotationY"u8, ref entry.RotationY);
        Im.Tooltip.OnHover(HoveredFlags.AllowWhenDisabled, "Rotation Y-Axis"u8);

        Im.Table.NextColumn();
        Im.Item.SetNextWidthScaled(120);
        changes |= Im.Input.Scalar("##AtchOffsetZ"u8, ref entry.OffsetZ);
        Im.Tooltip.OnHover("Offset Z-Coordinate"u8);
        Im.Item.SetNextWidthScaled(120);
        changes |= Im.Input.Scalar("##AtchRotationZ"u8, ref entry.RotationZ);
        Im.Tooltip.OnHover(HoveredFlags.AllowWhenDisabled, "Rotation Z-Axis"u8);

        return changes;
    }

    private static bool DrawRace(ref AtchIdentifier identifier, float unscaledWidth = 100)
    {
        var ret = Combos.ModelRace.Draw("##atchRace"u8, identifier.Race, StringU8.Empty, unscaledWidth * Im.Style.GlobalScale, out var race);
        Im.Tooltip.OnHover("Model Race"u8);
        if (ret)
            identifier = identifier with { GenderRace = Names.CombinedRace(identifier.Gender, race) };

        return ret;
    }

    private static bool DrawGender(ref AtchIdentifier identifier, bool disabled)
    {
        var isMale = identifier.Gender is Gender.Male;

        if (!ImEx.Icon.Button(isMale ? FontAwesomeIcon.Mars.Icon() : FontAwesomeIcon.Venus.Icon(), "Gender"u8,
                buttonColor: disabled ? 0x000F0000u : 0)
         || disabled)
            return false;

        identifier = identifier with { GenderRace = Names.CombinedRace(isMale ? Gender.Female : Gender.Male, identifier.Race) };
        return true;
    }

    private static bool DrawPointInput(ref AtchIdentifier identifier, AtchPointCombo combo)
    {
        if (!combo.Draw("##AtchPoint"u8, identifier.Type, "Attachment Point Type"u8, 160 * Im.Style.GlobalScale, out var newType))
            return false;

        identifier = identifier with { Type = newType };
        return true;
    }

    private static bool DrawEntryIndexInput(ref AtchIdentifier identifier, AtchPoint currentAtchPoint)
    {
        var index = identifier.EntryIndex;
        Im.Item.SetNextWidth(40 * Im.Style.GlobalScale);
        var ret = Im.Drag("##AtchEntry"u8, ref index, 0, (ushort)(currentAtchPoint.Entries.Length - 1), 0.05f,
            SliderFlags.AlwaysClamp);
        Im.Tooltip.OnHover("State Entry Index"u8);
        if (!ret)
            return false;

        index      = Math.Clamp(index, (ushort)0, (ushort)(currentAtchPoint.Entries.Length - 1));
        identifier = identifier with { EntryIndex = index };
        return true;
    }
}
