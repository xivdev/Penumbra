using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dalamud.Interface;
using ImGuiNET;
using OtterGui;
using OtterGui.Raii;
using OtterGui.Widgets;
using Penumbra.Mods;
using Penumbra.UI.Classes;

namespace Penumbra.UI;
public sealed class SharedTagManager
{
    private static uint _tagButtonAddColor = ColorId.SharedTagAdd.Value();
    private static uint _tagButtonRemoveColor = ColorId.SharedTagRemove.Value();

    private static float _minTagButtonWidth = 15;

    private const string PopupContext = "SharedTagsPopup";
    private bool _isPopupOpen = false;


    public IReadOnlyList<string> SharedTags { get; internal set; } = Array.Empty<string>();

    public SharedTagManager()
    {
    }

    public void ChangeSharedTag(int tagIdx, string tag)
    {
        if (tagIdx < 0 || tagIdx > SharedTags.Count)
            return;

        if (tagIdx == SharedTags.Count) // Adding a new tag
        {
            SharedTags = SharedTags.Append(tag).Distinct().Where(tag => tag.Length > 0).OrderBy(a => a).ToArray();
        }
        else // Editing an existing tag
        {
            var tmpTags = SharedTags.ToArray();
            tmpTags[tagIdx] = tag;
            SharedTags = tmpTags.Distinct().Where(tag => tag.Length > 0).OrderBy(a => a).ToArray();
        }
    }

    public string DrawAddFromSharedTags(IReadOnlyCollection<string> localTags, IReadOnlyCollection<string> modTags, bool editLocal)
    {
        var tagToAdd = "";
        if (ImGuiUtil.DrawDisabledButton(FontAwesomeIcon.Tags.ToIconString(), new Vector2(ImGui.GetFrameHeight()), "Add Shared Tag... (Right-click to close popup)",
            false, true) || _isPopupOpen)
            return DrawSharedTagsPopup(localTags, modTags, editLocal);


        return tagToAdd;
    }

    private string DrawSharedTagsPopup(IReadOnlyCollection<string> localTags, IReadOnlyCollection<string> modTags, bool editLocal)
    {
        var selected = "";
        if (!ImGui.IsPopupOpen(PopupContext))
        {
            ImGui.OpenPopup(PopupContext);
            _isPopupOpen = true;
        }

        var display = ImGui.GetIO().DisplaySize;
        var height = Math.Min(display.Y / 4, 10 * ImGui.GetFrameHeightWithSpacing());
        var width = display.X / 6;
        var size = new Vector2(width, height);
        ImGui.SetNextWindowSize(size);
        using var popup = ImRaii.Popup(PopupContext);
        if (!popup)
            return selected;

        ImGui.Text("Shared Tags");
        ImGuiUtil.HoverTooltip("Right-click to close popup");
        ImGui.Separator();

        foreach (var tag in SharedTags)
        {
            if (DrawColoredButton(localTags, modTags, tag, editLocal))
            {
                selected = tag;
                return selected;
            }
            ImGui.SameLine();
        }

        if (ImGui.IsMouseClicked(ImGuiMouseButton.Right))
        {
            _isPopupOpen = false;
        }

        return selected;
    }

    private static bool DrawColoredButton(IReadOnlyCollection<string> localTags, IReadOnlyCollection<string> modTags, string buttonLabel, bool editLocal)
    {
        var isLocalTagPresent = localTags.Contains(buttonLabel);
        var isModTagPresent = modTags.Contains(buttonLabel);

        var buttonWidth = CalcTextButtonWidth(buttonLabel);
        // Would prefer to be able to fit at least 2 buttons per line so the popup doesn't look sparse with lots of long tags. Thus long tags will be trimmed.
        var maxButtonWidth = (ImGui.GetContentRegionMax().X - ImGui.GetWindowContentRegionMin().X) * 0.5f - ImGui.GetStyle().ItemSpacing.X;
        var displayedLabel = buttonLabel;
        if (buttonWidth >= maxButtonWidth)
        {
            displayedLabel = TrimButtonTextToWidth(buttonLabel, maxButtonWidth);
            buttonWidth = CalcTextButtonWidth(displayedLabel);
        }

        // Prevent adding a new tag past the right edge of the popup
        if (buttonWidth + ImGui.GetStyle().ItemSpacing.X >= ImGui.GetContentRegionAvail().X)
            ImGui.NewLine();

        // Trimmed tag names can collide, but the full tags are guaranteed distinct so use the full tag as the ID to avoid an ImGui moment.
        ImRaii.PushId(buttonLabel);

        if (editLocal && isModTagPresent || !editLocal && isLocalTagPresent)
        {
            using var alpha = ImRaii.PushStyle(ImGuiStyleVar.Alpha, 0.5f);
            ImGui.Button(displayedLabel);
            alpha.Pop();
            return false;
        }

        using (ImRaii.PushColor(ImGuiCol.Button, isLocalTagPresent || isModTagPresent ? _tagButtonRemoveColor : _tagButtonAddColor))
        {
            return ImGui.Button(displayedLabel);
        }
    }

    private static string TrimButtonTextToWidth(string fullText, float maxWidth)
    {
        var trimmedText = fullText;

        while (trimmedText.Length > _minTagButtonWidth)
        {
            var nextTrim = trimmedText.Substring(0, Math.Max(trimmedText.Length - 1, 0));

            // An ellipsis will be used to indicate trimmed tags
            if (CalcTextButtonWidth(nextTrim + "...") < maxWidth)
            {
                return nextTrim + "...";
            }
            trimmedText = nextTrim;
        }

        return trimmedText;
    }

    private static float CalcTextButtonWidth(string text)
    {
        return ImGui.CalcTextSize(text).X + 2 * ImGui.GetStyle().FramePadding.X;
    }

}
