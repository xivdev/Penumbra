using System;
using System.Diagnostics;
using System.Numerics;
using Dalamud.Interface.GameFonts;
using Dalamud.Plugin;
using ImGuiNET;
using OtterGui;
using OtterGui.Raii;
using Penumbra.Mods;
using Penumbra.UI.Classes;

namespace Penumbra.UI.ModsTab;

public class ModPanelHeader : IDisposable
{
    /// <summary> We use a big, nice game font for the title. </summary>
    private readonly GameFontHandle _nameFont;

    public ModPanelHeader(DalamudPluginInterface pi)
        => _nameFont = pi.UiBuilder.GetGameFontHandle(new GameFontStyle(GameFontFamilyAndSize.Jupiter23));

    /// <summary>
    /// Draw the header for the current mod,
    /// consisting of its name, version, author and website, if they exist.
    /// </summary>
    public void Draw()
    {
        var offset = DrawModName();
        DrawVersion(offset);
        DrawSecondRow(offset);
    }

    /// <summary>
    /// Update all mod header data. Should someone change frame padding or item spacing,
    /// or his default font, this will break, but he will just have to select a different mod to restore.
    /// </summary>
    public void UpdateModData(Mod mod)
    {
        // Name
        var name = $" {mod.Name} ";
        if (name != _modName)
        {
            using var font = ImRaii.PushFont(_nameFont.ImFont, _nameFont.Available);
            _modName      = name;
            _modNameWidth = ImGui.CalcTextSize(name).X + 2 * (ImGui.GetStyle().FramePadding.X + 2 * UiHelpers.Scale);
        }

        // Author
        var author = mod.Author.IsEmpty ? string.Empty : $"by  {mod.Author}";
        if (author != _modAuthor)
        {
            _modAuthor      = author;
            _modAuthorWidth = ImGui.CalcTextSize(author).X;
            _secondRowWidth = _modAuthorWidth + _modWebsiteButtonWidth + ImGui.GetStyle().ItemSpacing.X;
        }

        // Version
        var version = mod.Version.Length > 0 ? $"({mod.Version})" : string.Empty;
        if (version != _modVersion)
        {
            _modVersion      = version;
            _modVersionWidth = ImGui.CalcTextSize(version).X;
        }

        // Website
        if (_modWebsite != mod.Website)
        {
            _modWebsite = mod.Website;
            _websiteValid = Uri.TryCreate(_modWebsite, UriKind.Absolute, out var uriResult)
             && (uriResult.Scheme == Uri.UriSchemeHttps || uriResult.Scheme == Uri.UriSchemeHttp);
            _modWebsiteButton = _websiteValid ? "Open Website" : _modWebsite.Length == 0 ? string.Empty : $"from  {_modWebsite}";
            _modWebsiteButtonWidth = _websiteValid
                ? ImGui.CalcTextSize(_modWebsiteButton).X + 2 * ImGui.GetStyle().FramePadding.X
                : ImGui.CalcTextSize(_modWebsiteButton).X;
            _secondRowWidth = _modAuthorWidth + _modWebsiteButtonWidth + ImGui.GetStyle().ItemSpacing.X;
        }
    }

    public void Dispose()
    {
        _nameFont.Dispose();
    }

    // Header data.
    private string _modName          = string.Empty;
    private string _modAuthor        = string.Empty;
    private string _modVersion       = string.Empty;
    private string _modWebsite       = string.Empty;
    private string _modWebsiteButton = string.Empty;
    private bool   _websiteValid;

    private float _modNameWidth;
    private float _modAuthorWidth;
    private float _modVersionWidth;
    private float _modWebsiteButtonWidth;
    private float _secondRowWidth;

    /// <summary>
    /// Draw the mod name in the game font with a 2px border, centered,
    /// with at least the width of the version space to each side.
    /// </summary>
    private float DrawModName()
    {
        var decidingWidth = Math.Max(_secondRowWidth, ImGui.GetWindowWidth());
        var offsetWidth   = (decidingWidth - _modNameWidth) / 2;
        var offsetVersion = _modVersion.Length > 0
            ? _modVersionWidth + ImGui.GetStyle().ItemSpacing.X + ImGui.GetStyle().WindowPadding.X
            : 0;
        var offset = Math.Max(offsetWidth, offsetVersion);
        if (offset > 0)
        {
            ImGui.SetCursorPosX(offset);
        }

        using var color = ImRaii.PushColor(ImGuiCol.Border, Colors.MetaInfoText);
        using var style = ImRaii.PushStyle(ImGuiStyleVar.FrameBorderSize, 2 * UiHelpers.Scale);
        using var font  = ImRaii.PushFont(_nameFont.ImFont, _nameFont.Available);
        ImGuiUtil.DrawTextButton(_modName, Vector2.Zero, 0);
        return offset;
    }

    /// <summary> Draw the version in the top-right corner. </summary>
    private void DrawVersion(float offset)
    {
        var oldPos = ImGui.GetCursorPos();
        ImGui.SetCursorPos(new Vector2(2 * offset + _modNameWidth - _modVersionWidth - ImGui.GetStyle().WindowPadding.X,
            ImGui.GetStyle().FramePadding.Y));
        ImGuiUtil.TextColored(Colors.MetaInfoText, _modVersion);
        ImGui.SetCursorPos(oldPos);
    }

    /// <summary>
    /// Draw author and website if they exist. The website is a button if it is valid.
    /// Usually, author begins at the left boundary of the name,
    /// and website ends at the right boundary of the name.
    /// If their combined width is larger than the name, they are combined-centered. 
    /// </summary>
    private void DrawSecondRow(float offset)
    {
        if (_modAuthor.Length == 0)
        {
            if (_modWebsiteButton.Length == 0)
            {
                ImGui.NewLine();
                return;
            }

            offset += (_modNameWidth - _modWebsiteButtonWidth) / 2;
            ImGui.SetCursorPosX(offset);
            DrawWebsite();
        }
        else if (_modWebsiteButton.Length == 0)
        {
            offset += (_modNameWidth - _modAuthorWidth) / 2;
            ImGui.SetCursorPosX(offset);
            DrawAuthor();
        }
        else if (_secondRowWidth < _modNameWidth)
        {
            ImGui.SetCursorPosX(offset);
            DrawAuthor();
            ImGui.SameLine(offset + _modNameWidth - _modWebsiteButtonWidth);
            DrawWebsite();
        }
        else
        {
            offset -= (_secondRowWidth - _modNameWidth) / 2;
            if (offset > 0)
            {
                ImGui.SetCursorPosX(offset);
            }

            DrawAuthor();
            ImGui.SameLine();
            DrawWebsite();
        }
    }

    /// <summary> Draw the author text. </summary>
    private void DrawAuthor()
    {
        using var style = ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, Vector2.Zero);
        ImGuiUtil.TextColored(Colors.MetaInfoText, "by ");
        ImGui.SameLine();
        style.Pop();
        ImGui.TextUnformatted(_modAuthor);
    }

    /// <summary>
    /// Draw either a website button if the source is a valid website address,
    /// or a source text if it is not.
    /// </summary>
    private void DrawWebsite()
    {
        if (_websiteValid)
        {
            if (ImGui.SmallButton(_modWebsiteButton))
            {
                try
                {
                    var process = new ProcessStartInfo(_modWebsite)
                    {
                        UseShellExecute = true,
                    };
                    Process.Start(process);
                }
                catch
                {
                    // ignored
                }
            }

            ImGuiUtil.HoverTooltip(_modWebsite);
        }
        else
        {
            using var style = ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, Vector2.Zero);
            ImGuiUtil.TextColored(Colors.MetaInfoText, "from ");
            ImGui.SameLine();
            style.Pop();
            ImGui.TextUnformatted(_modWebsite);
        }
    }
}
