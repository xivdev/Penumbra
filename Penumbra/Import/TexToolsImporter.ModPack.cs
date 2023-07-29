using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Penumbra.Api.Enums;
using Penumbra.Import.Structs;
using Penumbra.Mods;
using Penumbra.Mods.Subclasses;
using Penumbra.Util;
using SharpCompress.Archives.Zip;

namespace Penumbra.Import;

public partial class TexToolsImporter
{
    private DirectoryInfo? _currentModDirectory;

    // Version 1 mod packs are a simple collection of files without much information.
    private DirectoryInfo ImportV1ModPack( FileInfo modPackFile, ZipArchive extractedModPack, string modRaw )
    {
        _currentOptionIdx  = 0;
        _currentNumOptions = 1;
        _currentModName    = modPackFile.Name.Length > 0 ? modPackFile.Name : DefaultTexToolsData.Name;
        _currentGroupName  = string.Empty;
        _currentOptionName = DefaultTexToolsData.DefaultOption;

        Penumbra.Log.Information( "    -> Importing V1 ModPack" );

        var modListRaw = modRaw.Split(
            new[] { "\r\n", "\r", "\n" },
            StringSplitOptions.RemoveEmptyEntries
        );

        var modList = modListRaw.Select( m => JsonConvert.DeserializeObject< SimpleMod >( m, JsonSettings )! ).ToList();

        _currentModDirectory = ModCreator.CreateModFolder( _baseDirectory, Path.GetFileNameWithoutExtension( modPackFile.Name ) );
        // Create a new ModMeta from the TTMP mod list info
        _modManager.DataEditor.CreateMeta( _currentModDirectory, _currentModName, DefaultTexToolsData.Author, DefaultTexToolsData.Description, null, null );

        // Open the mod data file from the mod pack as a SqPackStream
        _streamDisposer = GetSqPackStreamStream( extractedModPack, "TTMPD.mpd" );
        ExtractSimpleModList( _currentModDirectory, modList );
        _modManager.Creator.CreateDefaultFiles( _currentModDirectory );
        ResetStreamDisposer();
        return _currentModDirectory;
    }

    // Version 2 mod packs can either be simple or extended, import accordingly.
    private DirectoryInfo ImportV2ModPack( FileInfo _, ZipArchive extractedModPack, string modRaw )
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
            Penumbra.Log.Warning( $"Unknown TTMPVersion <{modList.TtmpVersion}> given, trying to export as simple mod pack." );
            return ImportSimpleV2ModPack( extractedModPack, modList );
        }
        catch( Exception e1 )
        {
            Penumbra.Log.Warning( $"Exporting as simple mod pack failed with following error, retrying as extended mod pack:\n{e1}" );
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
    private DirectoryInfo ImportSimpleV2ModPack( ZipArchive extractedModPack, SimpleModPack modList )
    {
        _currentOptionIdx  = 0;
        _currentNumOptions = 1;
        _currentModName    = modList.Name;
        _currentGroupName  = string.Empty;
        _currentOptionName = DefaultTexToolsData.DefaultOption;
        Penumbra.Log.Information( "    -> Importing Simple V2 ModPack" );

        _currentModDirectory = ModCreator.CreateModFolder( _baseDirectory, _currentModName );
        _modManager.DataEditor.CreateMeta( _currentModDirectory, _currentModName, modList.Author, string.IsNullOrEmpty( modList.Description )
            ? "Mod imported from TexTools mod pack"
            : modList.Description, modList.Version, modList.Url );

        // Open the mod data file from the mod pack as a SqPackStream
        _streamDisposer = GetSqPackStreamStream( extractedModPack, "TTMPD.mpd" );
        ExtractSimpleModList( _currentModDirectory, modList.SimpleModsList );
        _modManager.Creator.CreateDefaultFiles( _currentModDirectory );
        ResetStreamDisposer();
        return _currentModDirectory;
    }

    // Obtain the number of relevant options to extract.
    private static int GetOptionCount( ExtendedModPack pack )
        => ( pack.SimpleModsList.Length > 0 ? 1 : 0 )
          + pack.ModPackPages
               .Sum( page => page.ModGroups
                   .Where( g => g.GroupName.Length > 0 && g.OptionList.Length > 0 )
                   .Sum( group => group.OptionList
                           .Count( o => o.Name.Length > 0 && o.ModsJsons.Length > 0 )
                      + ( group.OptionList.Any( o => o.Name.Length > 0 && o.ModsJsons.Length == 0 ) ? 1 : 0 ) ) );

    private static string GetGroupName( string groupName, ISet< string > names )
    {
        var baseName = groupName;
        var i        = 2;
        while( !names.Add( groupName ) )
        {
            groupName = $"{baseName} ({i++})";
        }

        return groupName;
    }

    // Extended V2 mod packs contain multiple options that need to be handled separately.
    private DirectoryInfo ImportExtendedV2ModPack( ZipArchive extractedModPack, string modRaw )
    {
        _currentOptionIdx = 0;
        Penumbra.Log.Information( "    -> Importing Extended V2 ModPack" );

        var modList = JsonConvert.DeserializeObject< ExtendedModPack >( modRaw, JsonSettings )!;
        _currentNumOptions = GetOptionCount( modList );
        _currentModName    = modList.Name;

        _currentModDirectory = ModCreator.CreateModFolder( _baseDirectory, _currentModName );
        _modManager.DataEditor.CreateMeta( _currentModDirectory, _currentModName, modList.Author, modList.Description, modList.Version, modList.Url );

        if( _currentNumOptions == 0 )
        {
            return _currentModDirectory;
        }

        // Open the mod data file from the mod pack as a SqPackStream
        _streamDisposer = GetSqPackStreamStream( extractedModPack, "TTMPD.mpd" );

        // It can contain a simple list, still.
        if( modList.SimpleModsList.Length > 0 )
        {
            _currentGroupName  = string.Empty;
            _currentOptionName = "Default";
            ExtractSimpleModList( _currentModDirectory, modList.SimpleModsList );
        }

        // Iterate through all pages
        var options       = new List< ISubMod >();
        var groupPriority = 0;
        var groupNames    = new HashSet< string >();
        foreach( var page in modList.ModPackPages )
        {
            foreach( var group in page.ModGroups.Where( group => group.GroupName.Length > 0 && group.OptionList.Length > 0 ) )
            {
                var allOptions = group.OptionList.Where( option => option.Name.Length > 0 && option.ModsJsons.Length > 0 ).ToList();
                var (numGroups, maxOptions) = group.SelectionType == GroupType.Single
                    ? ( 1, allOptions.Count )
                    : ( 1 + allOptions.Count / IModGroup.MaxMultiOptions, IModGroup.MaxMultiOptions );
                _currentGroupName = GetGroupName( group.GroupName, groupNames );

                var optionIdx       = 0;
                for( var groupId = 0; groupId < numGroups; ++groupId )
                {
                    var name           = numGroups == 1 ? _currentGroupName : $"{_currentGroupName}, Part {groupId + 1}";
                    options.Clear();
                    var groupFolder = ModCreator.NewSubFolderName( _currentModDirectory, name )
                     ?? new DirectoryInfo( Path.Combine( _currentModDirectory.FullName,
                            numGroups == 1 ? $"Group {groupPriority + 1}" : $"Group {groupPriority + 1}, Part {groupId + 1}" ) );

                    uint? defaultSettings = group.SelectionType == GroupType.Multi ? 0u : null;
                    for( var i = 0; i + optionIdx < allOptions.Count && i < maxOptions; ++i )
                    {
                        var option = allOptions[ i + optionIdx ];
                        _token.ThrowIfCancellationRequested();
                        _currentOptionName = option.Name;
                        var optionFolder = ModCreator.NewSubFolderName( groupFolder, option.Name )
                         ?? new DirectoryInfo( Path.Combine( groupFolder.FullName, $"Option {i + optionIdx + 1}" ) );
                        ExtractSimpleModList( optionFolder, option.ModsJsons );
                        options.Add( _modManager.Creator.CreateSubMod( _currentModDirectory, optionFolder, option ) );
                        if( option.IsChecked )
                        {
                            defaultSettings = group.SelectionType == GroupType.Multi
                                ? ( defaultSettings!.Value | ( 1u << i ) )
                                : ( uint )i;
                        }

                        ++_currentOptionIdx;
                    }

                    optionIdx += maxOptions;

                    // Handle empty options for single select groups without creating a folder for them.
                    // We only want one of those at most, and it should usually be the first option.
                    if( group.SelectionType == GroupType.Single )
                    {
                        var empty = group.OptionList.FirstOrDefault( o => o.Name.Length > 0 && o.ModsJsons.Length == 0 );
                        if( empty != null )
                        {
                            _currentOptionName = empty.Name;
                            options.Insert( 0, ModCreator.CreateEmptySubMod( empty.Name ) );
                            defaultSettings = defaultSettings == null ? 0 : defaultSettings.Value + 1;
                        }
                    }

                    _modManager.Creator.CreateOptionGroup( _currentModDirectory, group.SelectionType, name, groupPriority, groupPriority,
                        defaultSettings ?? 0, group.Description, options );
                    ++groupPriority;
                }
            }
        }

        ResetStreamDisposer();
        _modManager.Creator.CreateDefaultFiles( _currentModDirectory );
        return _currentModDirectory;
    }

    private void ExtractSimpleModList( DirectoryInfo outDirectory, ICollection< SimpleMod > mods )
    {
        State = ImporterState.ExtractingModFiles;

        _currentFileIdx  = 0;
        _currentNumFiles = mods.Count(m => m.FullPath.Length > 0);

        // Extract each SimpleMod into the new mod folder
        foreach( var simpleMod in mods.Where(m => m.FullPath.Length > 0 ) )
        {
            ExtractMod( outDirectory, simpleMod );
            ++_currentFileIdx;
        }
    }

    private void ExtractMod( DirectoryInfo outDirectory, SimpleMod mod )
    {
        if( _streamDisposer is not PenumbraSqPackStream stream )
        {
            return;
        }

        Penumbra.Log.Information( $"        -> Extracting {mod.FullPath} at {mod.ModOffset:X}" );

        _token.ThrowIfCancellationRequested();
        var data = stream.ReadFile< PenumbraSqPackStream.PenumbraFileResource >( mod.ModOffset );

        _currentFileName = mod.FullPath;
        var extractedFile = new FileInfo( Path.Combine( outDirectory.FullName, mod.FullPath ) );

        extractedFile.Directory?.Create();

        if( extractedFile.FullName.EndsWith( ".mdl" ) )
        {
            ProcessMdl( data.Data );
        }

        File.WriteAllBytes( extractedFile.FullName, data.Data );
    }

    private static void ProcessMdl( byte[] mdl )
    {
        const int modelHeaderLodOffset = 22;

        // Model file header LOD num
        mdl[ 64 ] = 1;

        // Model header LOD num
        var stackSize           = BitConverter.ToUInt32( mdl, 4 );
        var runtimeBegin        = stackSize    + 0x44;
        var stringsLengthOffset = runtimeBegin + 4;
        var stringsLength       = BitConverter.ToUInt32( mdl, ( int )stringsLengthOffset );
        var modelHeaderStart    = stringsLengthOffset + stringsLength + 4;
        mdl[ modelHeaderStart + modelHeaderLodOffset ] = 1;
    }
}