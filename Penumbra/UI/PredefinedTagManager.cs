using Dalamud.Interface;
using Dalamud.Interface.ImGuiNotification;
using ImGuiNET;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OtterGui;
using OtterGui.Classes;
using OtterGui.Raii;
using OtterGui.Services;
using Penumbra.Mods.Manager;
using Penumbra.Services;
using Penumbra.UI.Classes;

namespace Penumbra.UI;

public sealed class PredefinedTagManager : ISavable, IReadOnlyList<string>, IService
{
    public const int Version = 1;

    public record struct TagData
    { }

    private readonly ModManager  _modManager;
    private readonly SaveService _saveService;

    private bool _isListOpen = false;
    private uint _enabledColor;
    private uint _disabledColor;

    private readonly SortedList<string, TagData> _predefinedTags = [];

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
        var jObj = new JObject()
        {
            ["Version"] = Version,
            ["Tags"]    = JObject.FromObject(_predefinedTags),
        };
        jObj.WriteTo(jWriter);
    }

    public void Save()
        => _saveService.DelaySave(this, TimeSpan.FromSeconds(5));

    private void Load()
    {
        if (!File.Exists(_saveService.FileNames.PredefinedTagFile))
            return;

        try
        {
            var text    = File.ReadAllText(_saveService.FileNames.PredefinedTagFile);
            var jObj    = JObject.Parse(text);
            var version = jObj["Version"]?.ToObject<int>() ?? 0;
            switch (version)
            {
                case 1:
                    var tags = jObj["Tags"]?.ToObject<Dictionary<string, TagData>>() ?? [];
                    foreach (var (tag, data) in tags)
                        _predefinedTags.TryAdd(tag, data);
                    break;
                default: throw new Exception($"Invalid version {version}.");
            }
        }
        catch (Exception ex)
        {
            Penumbra.Messager.NotificationMessage(ex,
                "Error reading predefined tags Configuration, reverting to default.",
                "Error reading predefined tags Configuration", NotificationType.Error);
        }
    }

    public void ChangeSharedTag(int tagIdx, string tag)
    {
        if (tagIdx < 0 || tagIdx > _predefinedTags.Count)
            return;

        if (tagIdx != _predefinedTags.Count)
            _predefinedTags.RemoveAt(tagIdx);

        if (!string.IsNullOrEmpty(tag))
            _predefinedTags.TryAdd(tag, default);

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
        foreach (var (tag, idx) in _predefinedTags.Keys.WithIndex())
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

    public IEnumerator<string> GetEnumerator()
        => _predefinedTags.Keys.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator()
        => GetEnumerator();

    public int Count
        => _predefinedTags.Count;

    public string this[int index]
        => _predefinedTags.Keys[index];
}
