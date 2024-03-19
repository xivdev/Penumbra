using Dalamud.Interface;
using Dalamud.Interface.Internal.Notifications;
using ImGuiNET;
using Newtonsoft.Json;
using OtterGui;
using OtterGui.Classes;
using OtterGui.Raii;
using Penumbra.Mods.Manager;
using Penumbra.Services;
using Penumbra.UI.Classes;
using ErrorEventArgs = Newtonsoft.Json.Serialization.ErrorEventArgs;

namespace Penumbra.UI;

public sealed class PredefinedTagManager : ISavable
{
    private readonly ModManager  _modManager;
    private readonly SaveService _saveService;

    private bool _isListOpen = false;
    private uint _enabledColor;
    private uint _disabledColor;


    // Operations on this list assume that it is sorted and will keep it sorted if that is the case.
    // The list also gets re-sorted when first loaded from config in case the config was modified.
    [JsonRequired]
    private readonly List<string> _predefinedTags = [];

    [JsonIgnore]
    public IReadOnlyList<string> PredefinedTags
        => _predefinedTags;

    public int ConfigVersion = 1;

    public PredefinedTagManager(ModManager modManager, SaveService saveService)
    {
        _modManager  = modManager;
        _saveService = saveService;
        Load();
    }

    public string ToFilename(FilenameService fileNames)
        => fileNames.PredefinedTagFile;

    public void Save(StreamWriter writer)
    {
        using var jWriter = new JsonTextWriter(writer);
        jWriter.Formatting = Formatting.Indented;
        var serializer = new JsonSerializer { Formatting = Formatting.Indented };
        serializer.Serialize(jWriter, this);
    }

    public void Save()
        => _saveService.DelaySave(this, TimeSpan.FromSeconds(5));

    private void Load()
    {
        if (!File.Exists(_saveService.FileNames.PredefinedTagFile))
            return;

        try
        {
            var text = File.ReadAllText(_saveService.FileNames.PredefinedTagFile);
            JsonConvert.PopulateObject(text, this, new JsonSerializerSettings
            {
                Error = HandleDeserializationError,
            });

            // Any changes to this within this class should keep it sorted, but in case someone went in and manually changed the JSON, run a sort on initial load.
            _predefinedTags.Sort();
        }
        catch (Exception ex)
        {
            Penumbra.Messager.NotificationMessage(ex,
                "Error reading shared tags Configuration, reverting to default.",
                "Error reading shared tags Configuration", NotificationType.Error);
        }

        return;

        static void HandleDeserializationError(object? sender, ErrorEventArgs errorArgs)
        {
            Penumbra.Log.Error(
                $"Error parsing shared tags Configuration at {errorArgs.ErrorContext.Path}, using default or migrating:\n{errorArgs.ErrorContext.Error}");
            errorArgs.ErrorContext.Handled = true;
        }
    }

    public void ChangeSharedTag(int tagIdx, string tag)
    {
        if (tagIdx < 0 || tagIdx > PredefinedTags.Count)
            return;

        // In the case of editing a tag, remove what's there prior to doing an insert.
        if (tagIdx != PredefinedTags.Count)
            _predefinedTags.RemoveAt(tagIdx);

        if (!string.IsNullOrEmpty(tag))
        {
            // Taking advantage of the fact that BinarySearch returns the complement of the correct sorted position for the tag.
            var existingIdx = _predefinedTags.BinarySearch(tag);
            if (existingIdx < 0)
                _predefinedTags.Insert(~existingIdx, tag);
        }

        Save();
    }

    public void DrawAddFromSharedTagsAndUpdateTags(IReadOnlyCollection<string> localTags, IReadOnlyCollection<string> modTags, bool editLocal,
        Mods.Mod mod)
    {
        DrawToggleButton();
        if (!DrawList(localTags, modTags, editLocal, out var changedTag, out var index))
            return;

        if (editLocal)
            _modManager.DataEditor.ChangeLocalTag(mod, index, changedTag);
        else
            _modManager.DataEditor.ChangeModTag(mod, index, changedTag);
    }

    private void DrawToggleButton()
    {
        ImGui.SameLine(ImGui.GetContentRegionMax().X
          - ImGui.GetFrameHeight()
          - (ImGui.GetScrollMaxY() > 0 ? ImGui.GetStyle().ItemInnerSpacing.X : 0));
        using var color = ImRaii.PushColor(ImGuiCol.Button, ImGui.GetColorU32(ImGuiCol.ButtonActive), _isListOpen);
        if (ImGuiUtil.DrawDisabledButton(FontAwesomeIcon.Tags.ToIconString(), new Vector2(ImGui.GetFrameHeight()),
                "Add Predefined Tags...", false, true))
            _isListOpen = !_isListOpen;
    }

    private bool DrawList(IReadOnlyCollection<string> localTags, IReadOnlyCollection<string> modTags, bool editLocal, out string changedTag,
        out int changedIndex)
    {
        changedTag   = string.Empty;
        changedIndex = -1;

        if (!_isListOpen)
            return false;

        ImGui.TextUnformatted("Predefined Tags");
        ImGui.Separator();

        var ret = false;
        _enabledColor        = ColorId.PredefinedTagAdd.Value();
        _disabledColor       = ColorId.PredefinedTagRemove.Value();
        var (edited, others) = editLocal ? (localTags, modTags) : (modTags, localTags);
        foreach (var (tag, idx) in PredefinedTags.WithIndex())
        {
            var tagIdx  = edited.IndexOf(tag);
            var inOther = tagIdx < 0 && others.IndexOf(tag) >= 0;
            if (DrawColoredButton(tag, idx, tagIdx, inOther))
            {
                (changedTag, changedIndex) = tagIdx >= 0 ? (string.Empty, tagIdx) : (tag, edited.Count);
                ret                        = true;
            }

            ImGui.SameLine();
        }

        ImGui.NewLine();
        ImGui.Separator();
        return ret;
    }

    private bool DrawColoredButton(string buttonLabel, int index, int tagIdx, bool inOther)
    {
        using var id          = ImRaii.PushId(index);
        var       buttonWidth = CalcTextButtonWidth(buttonLabel);
        // Prevent adding a new tag past the right edge of the popup
        if (buttonWidth + ImGui.GetStyle().ItemSpacing.X >= ImGui.GetContentRegionAvail().X)
            ImGui.NewLine();

        bool ret;
        using (ImRaii.Disabled(inOther))
        {
            using var color = ImRaii.PushColor(ImGuiCol.Button, tagIdx >= 0 || inOther ? _disabledColor : _enabledColor);
            ret = ImGui.Button(buttonLabel);
        }

        if (inOther && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip("This tag is already present in the other set of tags.");


        return ret;
    }

    private static float CalcTextButtonWidth(string text)
        => ImGui.CalcTextSize(text).X + 2 * ImGui.GetStyle().FramePadding.X;
}
