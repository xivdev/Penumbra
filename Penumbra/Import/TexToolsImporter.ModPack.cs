using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Dalamud.Logging;
using ICSharpCode.SharpZipLib.Zip;
using Newtonsoft.Json;
using Penumbra.Mods;
using Penumbra.Util;

namespace Penumbra.Import;

public partial class TexToolsImporter
{
    // Version 1 mod packs are a simple collection of files without much information.
    private DirectoryInfo ImportV1ModPack( FileInfo modPackFile, ZipFile extractedModPack, string modRaw )
    {
        _currentOptionIdx  = 0;
        _currentNumOptions = 1;
        _currentModName    = modPackFile.Name.Length > 0 ? modPackFile.Name : DefaultTexToolsData.Name;
        _currentGroupName  = string.Empty;
        _currentOptionName = DefaultTexToolsData.DefaultOption;

        PluginLog.Log( "    -> Importing V1 ModPack" );

        var modListRaw = modRaw.Split(
            new[] { "\r\n", "\r", "\n" },
            StringSplitOptions.RemoveEmptyEntries
        );

        var modList = modListRaw.Select( m => JsonConvert.DeserializeObject< SimpleMod >( m, JsonSettings )! ).ToList();

        // Open the mod data file from the mod pack as a SqPackStream
        using var modData = GetSqPackStreamStream( extractedModPack, "TTMPD.mpd" );

        var ret = Mod.CreateModFolder( _baseDirectory, Path.GetFileNameWithoutExtension( modPackFile.Name ) );
        // Create a new ModMeta from the TTMP mod list info
        Mod.CreateMeta( ret, _currentModName, DefaultTexToolsData.Author, DefaultTexToolsData.Description, null, null );

        ExtractSimpleModList( ret, modList, modData );
        Mod.CreateDefaultFiles( ret );
        return ret;
    }

    // Version 2 mod packs can either be simple or extended, import accordingly.
    private DirectoryInfo ImportV2ModPack( FileInfo _, ZipFile extractedModPack, string modRaw )
    {
        var modList = JsonConvert.DeserializeObject< SimpleModPack >( modRaw, JsonSettings )!;

        if( modList.TtmpVersion.EndsWith( "s" ) )
        {
            return ImportSimpleV2ModPack( extractedModPack, modList );
        }

        if( modList.TtmpVersion.EndsWith( "w" ) )
        {
            return ImportExtendedV2ModPack( extractedModPack, modRaw );
        }

        try
        {
            PluginLog.Warning( $"Unknown TTMPVersion <{modList.TtmpVersion}> given, trying to export as simple mod pack." );
            return ImportSimpleV2ModPack( extractedModPack, modList );
        }
        catch( Exception e1 )
        {
            PluginLog.Warning( $"Exporting as simple mod pack failed with following error, retrying as extended mod pack:\n{e1}" );
            try
            {
                return ImportExtendedV2ModPack( extractedModPack, modRaw );
            }
            catch( Exception e2 )
            {
                throw new IOException( "Exporting as extended mod pack failed, too. Version unsupported or file defect.", e2 );
            }
        }
    }

    // Simple V2 mod packs are basically the same as V1 mod packs.
    private DirectoryInfo ImportSimpleV2ModPack( ZipFile extractedModPack, SimpleModPack modList )
    {
        _currentOptionIdx  = 0;
        _currentNumOptions = 1;
        _currentModName    = modList.Name;
        _currentGroupName  = string.Empty;
        _currentOptionName = DefaultTexToolsData.DefaultOption;
        PluginLog.Log( "    -> Importing Simple V2 ModPack" );

        // Open the mod data file from the mod pack as a SqPackStream
        using var modData = GetSqPackStreamStream( extractedModPack, "TTMPD.mpd" );

        var ret = Mod.CreateModFolder( _baseDirectory, _currentModName );
        Mod.CreateMeta( ret, _currentModName, modList.Author, string.IsNullOrEmpty( modList.Description )
            ? "Mod imported from TexTools mod pack"
            : modList.Description, null, null );

        ExtractSimpleModList( ret, modList.SimpleModsList, modData );
        Mod.CreateDefaultFiles( ret );
        return ret;
    }

    // Obtain the number of relevant options to extract.
    private static int GetOptionCount( ExtendedModPack pack )
        => ( pack.SimpleModsList.Length > 0 ? 1 : 0 )
          + pack.ModPackPages
               .Sum( page => page.ModGroups
                   .Where( g => g.GroupName.Length > 0 && g.OptionList.Length > 0 )
                   .Sum( group => group.OptionList
                       .Count( o => o.Name.Length > 0 && o.ModsJsons.Length > 0 ) ) );

    // Extended V2 mod packs contain multiple options that need to be handled separately.
    private DirectoryInfo ImportExtendedV2ModPack( ZipFile extractedModPack, string modRaw )
    {
        _currentOptionIdx = 0;
        PluginLog.Log( "    -> Importing Extended V2 ModPack" );

        var modList = JsonConvert.DeserializeObject< ExtendedModPack >( modRaw, JsonSettings )!;
        _currentNumOptions = GetOptionCount( modList );
        _currentModName    = modList.Name;
        // Open the mod data file from the mod pack as a SqPackStream
        using var modData = GetSqPackStreamStream( extractedModPack, "TTMPD.mpd" );

        var ret = Mod.CreateModFolder( _baseDirectory, _currentModName );
        Mod.CreateMeta( ret, _currentModName, modList.Author, modList.Description, modList.Version, null );

        if( _currentNumOptions == 0 )
        {
            return ret;
        }

        // It can contain a simple list, still.
        if( modList.SimpleModsList.Length > 0 )
        {
            _currentGroupName  = string.Empty;
            _currentOptionName = "Default";
            ExtractSimpleModList( ret, modList.SimpleModsList, modData );
        }

        // Iterate through all pages
        var options       = new List< ISubMod >();
        var groupPriority = 0;
        foreach( var page in modList.ModPackPages )
        {
            foreach( var group in page.ModGroups.Where( group => group.GroupName.Length > 0 && group.OptionList.Length > 0 ) )
            {
                _currentGroupName = group.GroupName;
                options.Clear();
                var description = new StringBuilder();
                var groupFolder = Mod.NewSubFolderName( ret, group.GroupName )
                 ?? new DirectoryInfo( Path.Combine( ret.FullName, $"Group {groupPriority + 1}" ) );

                var optionIdx = 1;

                foreach( var option in group.OptionList.Where( option => option.Name.Length > 0 && option.ModsJsons.Length > 0 ) )
                {
                    _currentOptionName = option.Name;
                    var optionFolder = Mod.NewSubFolderName( groupFolder, option.Name )
                     ?? new DirectoryInfo( Path.Combine( groupFolder.FullName, $"Option {optionIdx}" ) );
                    ExtractSimpleModList( optionFolder, option.ModsJsons, modData );
                    options.Add( Mod.CreateSubMod( ret, optionFolder, option ) );
                    description.Append( option.Description );
                    if( !string.IsNullOrEmpty( option.Description ) )
                    {
                        description.Append( '\n' );
                    }

                    ++optionIdx;
                    ++_currentOptionIdx;
                }

                Mod.CreateOptionGroup( ret, group, groupPriority++, description.ToString(), options );
            }
        }

        Mod.CreateDefaultFiles( ret );
        return ret;
    }

    private void ExtractSimpleModList( DirectoryInfo outDirectory, ICollection< SimpleMod > mods, PenumbraSqPackStream dataStream )
    {
        State = ImporterState.ExtractingModFiles;

        _currentFileIdx  = 0;
        _currentNumFiles = mods.Count;

        // Extract each SimpleMod into the new mod folder
        foreach( var simpleMod in mods )
        {
            ExtractMod( outDirectory, simpleMod, dataStream );
            ++_currentFileIdx;
        }
    }

    private void ExtractMod( DirectoryInfo outDirectory, SimpleMod mod, PenumbraSqPackStream dataStream )
    {
        PluginLog.Log( "        -> Extracting {0} at {1}", mod.FullPath, mod.ModOffset.ToString( "X" ) );

        try
        {
            var data = dataStream.ReadFile< PenumbraSqPackStream.PenumbraFileResource >( mod.ModOffset );

            _currentFileName = mod.FullPath;
            var extractedFile = new FileInfo( Path.Combine( outDirectory.FullName, mod.FullPath ) );

            extractedFile.Directory?.Create();

            if( extractedFile.FullName.EndsWith( ".mdl" ) )
            {
                ProcessMdl( data.Data );
            }

            File.WriteAllBytes( extractedFile.FullName, data.Data );
        }
        catch( Exception ex )
        {
            PluginLog.LogError( ex, "Could not extract mod." );
        }
    }

    private static void ProcessMdl( byte[] mdl )
    {
        const int modelHeaderLodOffset = 22;

        // Model file header LOD num
        mdl[ 64 ] = 1;

        // Model header LOD num
        var       stackSize            = BitConverter.ToUInt32( mdl, 4 );
        var       runtimeBegin         = stackSize    + 0x44;
        var       stringsLengthOffset  = runtimeBegin + 4;
        var       stringsLength        = BitConverter.ToUInt32( mdl, ( int )stringsLengthOffset );
        var       modelHeaderStart     = stringsLengthOffset + stringsLength + 4;
        mdl[ modelHeaderStart + modelHeaderLodOffset ] = 1;
    }
}