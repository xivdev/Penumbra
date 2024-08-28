using Dalamud.Interface;
using ImGuiNET;
using Newtonsoft.Json.Linq;
using OtterGui;
using OtterGui.Raii;
using OtterGui.Text;
using Penumbra.Api.Api;
using Penumbra.Meta;
using Penumbra.Meta.Manipulations;
using Penumbra.Mods.Editor;
using Penumbra.UI.Classes;

namespace Penumbra.UI.AdvancedWindow.Meta;

public interface IMetaDrawer
{
    public ReadOnlySpan<byte> Label      { get; }
    public int                NumColumns { get; }
    public void               Draw();
}

public abstract class MetaDrawer<TIdentifier, TEntry>(ModMetaEditor editor, MetaFileManager metaFiles) : IMetaDrawer
    where TIdentifier : unmanaged, IMetaIdentifier
    where TEntry : unmanaged
{
    protected const uint FrameColor = 0;

    protected readonly ModMetaEditor   Editor    = editor;
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

        using var id = ImUtf8.PushId((int)Identifier.Type);
        DrawNew();

        var height    = ImUtf8.FrameHeightSpacing;
        var skips     = ImGuiClip.GetNecessarySkipsAtPos(height, ImGui.GetCursorPosY());
        var remainder = ImGuiClip.ClippedTableDraw(Enumerate(), skips, DrawLine, Count);
        ImGuiClip.DrawEndDummy(remainder, height);

        void DrawLine((TIdentifier Identifier, TEntry Value) pair)
            => DrawEntry(pair.Identifier, pair.Value);
    }

    public abstract ReadOnlySpan<byte> Label      { get; }
    public abstract int                NumColumns { get; }

    protected abstract void DrawNew();
    protected abstract void Initialize();
    protected abstract void DrawEntry(TIdentifier identifier, TEntry entry);

    protected abstract IEnumerable<(TIdentifier, TEntry)> Enumerate();
    protected abstract int                                Count { get; }


    /// <summary>
    /// A number input for ids with an optional max id of given width.
    /// Returns true if newId changed against currentId.
    /// </summary>
    protected static bool IdInput(ReadOnlySpan<byte> label, float unscaledWidth, ushort currentId, out ushort newId, int minId, int maxId,
        bool border)
    {
        int tmp = currentId;
        ImGui.SetNextItemWidth(unscaledWidth * ImUtf8.GlobalScale);
        using var style = ImRaii.PushStyle(ImGuiStyleVar.FrameBorderSize, UiHelpers.Scale, border);
        using var color = ImRaii.PushColor(ImGuiCol.Border, Colors.RegexWarningBorder, border);
        if (ImUtf8.InputScalar(label, ref tmp))
            tmp = Math.Clamp(tmp, minId, maxId);

        newId = (ushort)tmp;
        return newId != currentId;
    }

    /// <summary>
    /// A dragging int input of given width that compares against a default value, shows a tooltip and clamps against min and max.
    /// Returns true if newValue changed against currentValue.
    /// </summary>
    protected static bool DragInput<T>(ReadOnlySpan<byte> label, ReadOnlySpan<byte> tooltip, float width, T currentValue, T defaultValue,
        out T newValue, T minValue, T maxValue, float speed, bool addDefault) where T : unmanaged, INumber<T>
    {
        newValue = currentValue;
        using var color = ImRaii.PushColor(ImGuiCol.FrameBg,
            defaultValue > currentValue ? ColorId.DecreasedMetaValue.Value() : ColorId.IncreasedMetaValue.Value(),
            defaultValue != currentValue);
        ImGui.SetNextItemWidth(width);
        if (ImUtf8.DragScalar(label, ref newValue, minValue, maxValue, speed))
            newValue = newValue <= minValue ? minValue : newValue >= maxValue ? maxValue : newValue;

        if (addDefault)
            ImUtf8.HoverTooltip($"{tooltip}\nDefault Value: {defaultValue}");
        else
            ImUtf8.HoverTooltip(ImGuiHoveredFlags.AllowWhenDisabled, tooltip);

        return newValue != currentValue;
    }

    /// <summary>
    /// A checkmark that compares against a default value and shows a tooltip.
    /// Returns true if newValue is changed against currentValue.
    /// </summary>
    protected static bool Checkmark(ReadOnlySpan<byte> label, ReadOnlySpan<byte> tooltip, bool currentValue, bool defaultValue,
        out bool newValue)
    {
        using var color = ImRaii.PushColor(ImGuiCol.FrameBg,
            defaultValue ? ColorId.DecreasedMetaValue.Value() : ColorId.IncreasedMetaValue.Value(),
            defaultValue != currentValue);
        newValue = currentValue;
        ImUtf8.Checkbox(label, ref newValue);
        ImUtf8.HoverTooltip(ImGuiHoveredFlags.AllowWhenDisabled, tooltip);
        return newValue != currentValue;
    }

    /// <summary>
    /// A checkmark that compares against a default value and shows a tooltip.
    /// Returns true if newValue is changed against currentValue.
    /// </summary>
    protected static bool Checkmark(ReadOnlySpan<byte> label, ReadOnlySpan<char> tooltip, bool currentValue, bool defaultValue,
        out bool newValue)
    {
        using var color = ImRaii.PushColor(ImGuiCol.FrameBg,
            defaultValue ? ColorId.DecreasedMetaValue.Value() : ColorId.IncreasedMetaValue.Value(),
            defaultValue != currentValue);
        newValue = currentValue;
        ImUtf8.Checkbox(label, ref newValue);
        ImUtf8.HoverTooltip(ImGuiHoveredFlags.AllowWhenDisabled, tooltip);
        return newValue != currentValue;
    }

    protected void DrawMetaButtons(TIdentifier identifier, TEntry entry)
    {
        ImGui.TableNextColumn();
        CopyToClipboardButton("Copy this manipulation to clipboard."u8, new JArray { MetaDictionary.Serialize(identifier, entry)! });

        ImGui.TableNextColumn();
        if (ImUtf8.IconButton(FontAwesomeIcon.Trash, "Delete this meta manipulation."u8))
            Editor.Changes |= Editor.Remove(identifier);
    }

    protected void CopyToClipboardButton(ReadOnlySpan<byte> tooltip, JToken? manipulations)
    {
        if (!ImUtf8.IconButton(FontAwesomeIcon.Clipboard, tooltip))
            return;

        var text = Functions.ToCompressedBase64(manipulations, MetaApi.CurrentVersion);
        if (text.Length > 0)
            ImGui.SetClipboardText(text);
    }
}
