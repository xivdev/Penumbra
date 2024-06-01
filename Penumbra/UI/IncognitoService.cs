using Dalamud.Interface;
using Penumbra.UI.Classes;
using OtterGui.Raii;
using OtterGui.Services;
using OtterGui.Text;

namespace Penumbra.UI;

public class IncognitoService(TutorialService tutorial) : IService
{
    public bool IncognitoMode;

    public void DrawToggle(float width)
    {
        var color = ColorId.FolderExpanded.Value();
        using (ImRaii.PushFrameBorder(ImUtf8.GlobalScale, color))
        {
            var tt   = IncognitoMode ? "Toggle incognito mode off."u8 : "Toggle incognito mode on."u8;
            var icon = IncognitoMode ? FontAwesomeIcon.EyeSlash : FontAwesomeIcon.Eye;
            if (ImUtf8.IconButton(icon, tt, new Vector2(width, ImUtf8.FrameHeight), false, color))
                IncognitoMode = !IncognitoMode;
        }

        tutorial.OpenTutorial(BasicTutorialSteps.Incognito);
    }
}
