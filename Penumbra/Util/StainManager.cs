using System;
using Dalamud.Data;
using Dalamud.Plugin;
using OtterGui.Widgets;
using Penumbra.GameData.Data;
using Penumbra.GameData.Files;

namespace Penumbra.Util;

public class StainManager : IDisposable
{
    public readonly StainData         StainData;
    public readonly FilterComboColors Combo;
    public readonly StmFile           StmFile;

    public StainManager(DalamudPluginInterface pluginInterface, DataManager dataManager)
    {
        StainData = new StainData( pluginInterface, dataManager, dataManager.Language );
        Combo     = new FilterComboColors( 140, StainData.Data );
        StmFile   = new StmFile( dataManager );
    }

    public void Dispose()
        => StainData.Dispose();
}