using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Data;
using Dalamud.Plugin;
using OtterGui.Widgets;
using Penumbra.GameData.Data;
using Penumbra.GameData.Files;

namespace Penumbra.Util;

public class StainManager : IDisposable
{
    public sealed class StainTemplateCombo : FilterComboCache< ushort >
    {
        public StainTemplateCombo( IEnumerable< ushort > items )
            : base( items )
        { }
    }

    public readonly StainData          StainData;
    public readonly FilterComboColors  StainCombo;
    public readonly StmFile            StmFile;
    public readonly StainTemplateCombo TemplateCombo;

    public StainManager( DalamudPluginInterface pluginInterface, DataManager dataManager )
    {
        StainData     = new StainData( pluginInterface, dataManager, dataManager.Language );
        StainCombo    = new FilterComboColors( 140, StainData.Data.Prepend( new KeyValuePair< byte, (string Name, uint Dye, bool Gloss) >( 0, ( "None", 0, false ) ) ) );
        StmFile       = new StmFile( dataManager );
        TemplateCombo = new StainTemplateCombo( StmFile.Entries.Keys.Prepend( ( ushort )0 ) );
    }

    public void Dispose()
        => StainData.Dispose();
}