using Dalamud.Interface;
using Dalamud.Bindings.ImGui;
using Penumbra.UI.Classes;
using OtterGui.Raii;
using OtterGui.Text;

namespace Penumbra.UI;

public class IncognitoService(TutorialService tutorial, Configuration config) : Luna.IService
{
    public bool IncognitoMode
        => config.Ephemeral.IncognitoMode;

    public void DrawToggle(float width)
    {
        var hold  = config.IncognitoModifier.IsActive();
        var color = ColorId.FolderExpanded.Value();
        using (ImRaii.PushFrameBorder(ImUtf8.GlobalScale, color))
        {
            var tt   = IncognitoMode ? "Toggle incognito mode off."u8 : "Toggle incognito mode on."u8;
            var icon = IncognitoMode ? FontAwesomeIcon.EyeSlash : FontAwesomeIcon.Eye;
            if (ImUtf8.IconButton(icon, tt, new Vector2(width, ImUtf8.FrameHeight), false, color) && hold)
            {
                config.Ephemeral.IncognitoMode = !IncognitoMode;
                config.Ephemeral.Save();
            }

            if (!hold)
                ImUtf8.HoverTooltip(ImGuiHoveredFlags.AllowWhenDisabled, $"\nHold {config.IncognitoModifier} while clicking to toggle.");
        }

        tutorial.OpenTutorial(BasicTutorialSteps.Incognito);
    }
}
