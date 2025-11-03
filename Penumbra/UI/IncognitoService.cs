using ImSharp;
using Luna;
using Penumbra.UI.Classes;

namespace Penumbra.UI;

public class IncognitoService(TutorialService tutorial, Configuration config) : IUiService
{
    public bool IncognitoMode
        => config.Ephemeral.IncognitoMode;

    public void DrawToggle(float width)
    {
        var hold  = config.IncognitoModifier.IsActive();
        var color = ColorId.FolderExpanded.Value();
        using (ImStyleBorder.Frame.Push(color))
        {
            var       tt    = IncognitoMode ? "Toggle incognito mode off."u8 : "Toggle incognito mode on."u8;
            var       icon  = IncognitoMode ? LunaStyle.IncognitoOn : LunaStyle.IncognitoOff;
            if (ImEx.Icon.Button(icon, tt, size: new Vector2(width, Im.Style.FrameHeight), textColor: color) && hold)
            {
                config.Ephemeral.IncognitoMode = !IncognitoMode;
                config.Ephemeral.Save();
            }
        }

        if (!hold)
            Im.Tooltip.OnHover(HoveredFlags.AllowWhenDisabled, $"\nHold {config.IncognitoModifier} while clicking to toggle.");

        tutorial.OpenTutorial(BasicTutorialSteps.Incognito);
    }
}
