using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Penumbra.GameData;

namespace Penumbra.Interop.Services;

/// <summary>
/// Handle font reloading via game functions.
/// May cause a interface flicker while reloading.
/// </summary>
public unsafe class FontReloader
{
    public bool Valid
        => _reloadFontsFunc != null;

    public void Reload()
    {
        if (Valid)
            _reloadFontsFunc(_atkModule, false, true);
        else
            Penumbra.Log.Error("Could not reload fonts, function could not be found.");
    }

    private readonly AtkModule* _atkModule = null!;
    private readonly delegate* unmanaged<AtkModule*, bool, bool, void> _reloadFontsFunc = null!;

    public FontReloader()
    {
        var framework = Framework.Instance();
        if (framework == null)
            return;

        var uiModule = framework->GetUiModule();
        if (uiModule == null)
            return;

        var atkModule = uiModule->GetRaptureAtkModule();
        if (atkModule == null)
            return;

        _atkModule = &atkModule->AtkModule;
        _reloadFontsFunc = ((delegate* unmanaged<AtkModule*, bool, bool, void>*)_atkModule->vtbl)[Offsets.ReloadFontsVfunc];
    }
}
