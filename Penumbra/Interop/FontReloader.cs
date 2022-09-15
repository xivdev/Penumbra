using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace Penumbra.Interop;

// Handle font reloading via game functions.
// May cause a interface flicker while reloading.
public static unsafe class FontReloader
{
    private static readonly AtkModule*                                        AtkModule       = null;
    private static readonly delegate* unmanaged<AtkModule*, bool, bool, void> ReloadFontsFunc = null;

    public static bool Valid
        => ReloadFontsFunc != null;

    public static void Reload()
    {
        if( Valid )
        {
            ReloadFontsFunc( AtkModule, false, true ); 
        }
        else
        {
            Penumbra.Log.Error( "Could not reload fonts, function could not be found." );
        }
    }

    static FontReloader()
    {
        if( ReloadFontsFunc != null )
        {
            return;
        }

        var framework = Framework.Instance();
        if( framework == null )
        {
            return;
        }

        var uiModule = framework->GetUiModule();
        if( uiModule == null )
        {
            return;
        }
        
        var atkModule = uiModule->GetRaptureAtkModule();
        if( atkModule == null )
        {
            return;
        }

        AtkModule       = &atkModule->AtkModule;
        ReloadFontsFunc = ( ( delegate* unmanaged< AtkModule*, bool, bool, void >* )AtkModule->vtbl )[ 43 ];
    }
}