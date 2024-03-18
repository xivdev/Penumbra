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

    private static uint _tagButtonAddColor    = ColorId.PredefinedTagAdd.Value();
    private static uint _tagButtonRemoveColor = ColorId.PredefinedTagRemove.Value();

    private static float _minTagButtonWidth = 15;

    private const string PopupContext = "SharedTagsPopup";
    private       bool   _isPopupOpen = false;

    // Operations on this list assume that it is sorted and will keep it sorted if that is the case.
    // The list also gets re-sorted when first loaded from config in case the config was modified.
    [JsonRequired]
    private readonly List<string> _sharedTags = [];

    [JsonIgnore]
    public IReadOnlyList<string> SharedTags
        => _sharedTags;

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
        using var jWriter    = new JsonTextWriter(writer) { Formatting = Formatting.Indented };
        var       serializer = new JsonSerializer { Formatting         = Formatting.Indented };
        serializer.Serialize(jWriter, this);
    }

    public void Save()
        => _saveService.DelaySave(this, TimeSpan.FromSeconds(5));

    private void Load()
    {
        static void HandleDeserializationError(object? sender, ErrorEventArgs errorArgs)
        {
            Penumbra.Log.Error(
                $"Error parsing shared tags Configuration at {errorArgs.ErrorContext.Path}, using default or migrating:\n{errorArgs.ErrorContext.Error}");
            errorArgs.ErrorContext.Handled = true;
        }

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
            _sharedTags.Sort();
        }
        catch (Exception ex)
        {
            Penumbra.Messager.NotificationMessage(ex,
                "Error reading shared tags Configuration, reverting to default.",
                "Error reading shared tags Configuration", NotificationType.Error);
        }
    }

    public void ChangeSharedTag(int tagIdx, string tag)
    {
        if (tagIdx < 0 || tagIdx > SharedTags.Count)
            return;

        // In the case of editing a tag, remove what's there prior to doing an insert.
        if (tagIdx != SharedTags.Count)
            _sharedTags.RemoveAt(tagIdx);

        if (!string.IsNullOrEmpty(tag))
        {
            // Taking advantage of the fact that BinarySearch returns the complement of the correct sorted position for the tag.
            var existingIdx = _sharedTags.BinarySearch(tag);
            if (existingIdx < 0)
                _sharedTags.Insert(~existingIdx, tag);
        }

        Save();
    }

    public void DrawAddFromSharedTagsAndUpdateTags(IReadOnlyCollection<string> localTags, IReadOnlyCollection<string> modTags, bool editLocal,
        Mods.Mod mod)
    {
        ImGui.SameLine(ImGui.GetContentRegionMax().X - ImGui.GetFrameHeight() - ImGui.GetStyle().WindowPadding.X);
        var sharedTag = DrawAddFromSharedTags(localTags, modTags, editLocal);

        if (sharedTag.Length > 0)
        {
            var index = editLocal ? mod.LocalTags.IndexOf(sharedTag) : mod.ModTags.IndexOf(sharedTag);

            if (editLocal)
            {
                if (index < 0)
                {
                    index = mod.LocalTags.Count;
                    _modManager.DataEditor.ChangeLocalTag(mod, index, sharedTag);
                }
                else
                {
                    _modManager.DataEditor.ChangeLocalTag(mod, index, string.Empty);
                }
            }
            else
            {
                if (index < 0)
                {
                    index = mod.ModTags.Count;
                    _modManager.DataEditor.ChangeModTag(mod, index, sharedTag);
                }
                else
                {
                    _modManager.DataEditor.ChangeModTag(mod, index, string.Empty);
                }
            }
        }
    }

    public string DrawAddFromSharedTags(IReadOnlyCollection<string> localTags, IReadOnlyCollection<string> modTags, bool editLocal)
    {
        var tagToAdd = string.Empty;
        if (ImGuiUtil.DrawDisabledButton(FontAwesomeIcon.Tags.ToIconString(), new Vector2(ImGui.GetFrameHeight()),
                "Add Shared Tag... (Right-click to close popup)",
                false, true)
         || _isPopupOpen)
            return DrawSharedTagsPopup(localTags, modTags, editLocal);

        return tagToAdd;
    }

    private string DrawSharedTagsPopup(IReadOnlyCollection<string> localTags, IReadOnlyCollection<string> modTags, bool editLocal)
    {
        var selected = string.Empty;
        if (!ImGui.IsPopupOpen(PopupContext))
        {
            ImGui.OpenPopup(PopupContext);
            _isPopupOpen = true;
        }

        var display = ImGui.GetIO().DisplaySize;
        var height  = Math.Min(display.Y / 4, 10 * ImGui.GetFrameHeightWithSpacing());
        var width   = display.X / 6;
        var size    = new Vector2(width, height);
        ImGui.SetNextWindowSize(size);
        using var popup = ImRaii.Popup(PopupContext);
        if (!popup)
            return selected;

        ImGui.TextUnformatted("Shared Tags");
        ImGuiUtil.HoverTooltip("Right-click to close popup");
        ImGui.Separator();

        foreach (var (tag, idx) in SharedTags.WithIndex())
        {
            if (DrawColoredButton(localTags, modTags, tag, editLocal, idx))
                selected = tag;
            ImGui.SameLine();
        }

        if (ImGui.IsMouseClicked(ImGuiMouseButton.Right))
            _isPopupOpen = false;

        return selected;
    }

    private static bool DrawColoredButton(IReadOnlyCollection<string> localTags, IReadOnlyCollection<string> modTags, string buttonLabel,
        bool editLocal, int index)
    {
        var ret = false;

        var isLocalTagPresent = localTags.Contains(buttonLabel);
        var isModTagPresent   = modTags.Contains(buttonLabel);

        var buttonWidth = CalcTextButtonWidth(buttonLabel);
        // Would prefer to be able to fit at least 2 buttons per line so the popup doesn't look sparse with lots of long tags. Thus long tags will be trimmed.
        var maxButtonWidth = (ImGui.GetContentRegionMax().X - ImGui.GetWindowContentRegionMin().X) * 0.5f - ImGui.GetStyle().ItemSpacing.X;
        var displayedLabel = buttonLabel;
        if (buttonWidth >= maxButtonWidth)
        {
            displayedLabel = TrimButtonTextToWidth(buttonLabel, maxButtonWidth);
            buttonWidth    = CalcTextButtonWidth(displayedLabel);
        }

        // Prevent adding a new tag past the right edge of the popup
        if (buttonWidth + ImGui.GetStyle().ItemSpacing.X >= ImGui.GetContentRegionAvail().X)
            ImGui.NewLine();

        // Trimmed tag names can collide, and while tag names are currently distinct this may not always be the case. As such use the index to avoid an ImGui moment.
        using var id = ImRaii.PushId(index);

        if (editLocal && isModTagPresent || !editLocal && isLocalTagPresent)
        {
            using var alpha = ImRaii.PushStyle(ImGuiStyleVar.Alpha, 0.5f);
            ImGui.Button(displayedLabel);
        }
        else
        {
            using (ImRaii.PushColor(ImGuiCol.Button, isLocalTagPresent || isModTagPresent ? _tagButtonRemoveColor : _tagButtonAddColor))
            {
                if (ImGui.Button(displayedLabel))
                    ret = true;
            }
        }

        return ret;
    }

    private static string TrimButtonTextToWidth(string fullText, float maxWidth)
    {
        var trimmedText = fullText;

        while (trimmedText.Length > _minTagButtonWidth)
        {
            var nextTrim = trimmedText.Substring(0, Math.Max(trimmedText.Length - 1, 0));

            // An ellipsis will be used to indicate trimmed tags
            if (CalcTextButtonWidth(nextTrim + "...") < maxWidth)
                return nextTrim + "...";

            trimmedText = nextTrim;
        }

        return trimmedText;
    }

    private static float CalcTextButtonWidth(string text)
        => ImGui.CalcTextSize(text).X + 2 * ImGui.GetStyle().FramePadding.X;
}
