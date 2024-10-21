using Dalamud.Interface.GameFonts;
using Dalamud.Interface.ManagedFontAtlas;
using Dalamud.Plugin;
using ImGuiNET;
using OtterGui;
using OtterGui.Raii;
using Penumbra.Communication;
using Penumbra.Mods;
using Penumbra.Mods.Manager;
using Penumbra.Services;
using Penumbra.UI.Classes;

namespace Penumbra.UI.ModsTab;

public class ModPanelHeader : IDisposable
{
    /// <summary> We use a big, nice game font for the title. </summary>
    private readonly IFontHandle _nameFont;

    private readonly CommunicatorService _communicator;
    private          float               _lastPreSettingsHeight;
    private          bool                _dirty                 = true;

    public ModPanelHeader(IDalamudPluginInterface pi, CommunicatorService communicator)
    {
        _communicator = communicator;
        _nameFont     = pi.UiBuilder.FontAtlas.NewGameFontHandle(new GameFontStyle(GameFontFamilyAndSize.Jupiter23));
        _communicator.ModDataChanged.Subscribe(OnModDataChange, ModDataChanged.Priority.ModPanelHeader);
    }

    /// <summary>
    /// Draw the header for the current mod,
    /// consisting of its name, version, author and website, if they exist.
    /// </summary>
    public void Draw()
    {
        UpdateModData();
        var height     = ImGui.GetContentRegionAvail().Y;
        var maxHeight = 3 * height / 4;
        using var child = _lastPreSettingsHeight > maxHeight && _communicator.PreSettingsTabBarDraw.HasSubscribers
            ? ImRaii.Child("HeaderChild", new Vector2(ImGui.GetContentRegionAvail().X, maxHeight), false)
            : null;
        using (ImRaii.Group())
        {
            var offset = DrawModName();
            DrawVersion(offset);
            DrawSecondRow(offset);
        }

        _communicator.PreSettingsTabBarDraw.Invoke(_mod.Identifier, ImGui.GetItemRectSize().X, _nameWidth);
        _lastPreSettingsHeight = ImGui.GetCursorPosY();
    }

    public void ChangeMod(Mod mod)
    {
        _mod   = mod;
        _dirty = true;
    }

    /// <summary>
    /// Update all mod header data. Should someone change frame padding or item spacing,
    /// or his default font, this will break, but he will just have to select a different mod to restore.
    /// </summary>
    private void UpdateModData()
    {
        if (!_dirty)
            return;

        _dirty                 = false;
        _lastPreSettingsHeight = 0;
        // Name
        var name = $" {_mod.Name} ";
        if (name != _modName)
        {
            using var f = _nameFont.Push();
            _modName      = name;
            _modNameWidth = ImGui.CalcTextSize(name).X + 2 * (ImGui.GetStyle().FramePadding.X + 2 * UiHelpers.Scale);
        }

        // Author
        if (_mod.Author != _modAuthor)
        {
            var author = _mod.Author.IsEmpty ? string.Empty : $"by  {_mod.Author}";
            _modAuthor      = _mod.Author.Text;
            _modAuthorWidth = ImGui.CalcTextSize(author).X;
            _secondRowWidth = _modAuthorWidth + _modWebsiteButtonWidth + ImGui.GetStyle().ItemSpacing.X;
        }

        // Version
        var version = _mod.Version.Length > 0 ? $"({_mod.Version})" : string.Empty;
        if (version != _modVersion)
        {
            _modVersion      = version;
            _modVersionWidth = ImGui.CalcTextSize(version).X;
        }

        // Website
        if (_modWebsite != _mod.Website)
        {
            _modWebsite = _mod.Website;
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
        _communicator.ModDataChanged.Unsubscribe(OnModDataChange);
    }

    // Header data.
    private Mod    _mod              = null!;
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

    private float _nameWidth;

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
        using var f     = _nameFont.Push();
        ImGuiUtil.DrawTextButton(_modName, Vector2.Zero, 0);
        _nameWidth = ImGui.GetItemRectSize().X;
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

    /// <summary> Just update the data when any relevant field changes. </summary>
    private void OnModDataChange(ModDataChangeType changeType, Mod mod, string? _2)
    {
        const ModDataChangeType relevantChanges =
            ModDataChangeType.Author | ModDataChangeType.Name | ModDataChangeType.Website | ModDataChangeType.Version;
        _dirty     = (changeType & relevantChanges) != 0;
    }
}
