using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dalamud.Interface.Internal.Notifications;
using OtterGui;
using Penumbra.String.Classes;
using Penumbra.Util;

namespace Penumbra.Mods;

public partial class Mod
{
    public void Normalize( Manager manager )
        => ModNormalizer.Normalize( manager, this );

    private struct ModNormalizer
    {
        private readonly Mod                                       _mod;
        private readonly string                                    _normalizationDirName;
        private readonly string                                    _oldDirName;
        private          Dictionary< Utf8GamePath, FullPath >[][]? _redirections = null;

        private ModNormalizer( Mod mod )
        {
            _mod                  = mod;
            _normalizationDirName = Path.Combine( _mod.ModPath.FullName, "TmpNormalization" );
            _oldDirName           = Path.Combine( _mod.ModPath.FullName, "TmpNormalizationOld" );
        }

        public static void Normalize( Manager manager, Mod mod )
        {
            var normalizer = new ModNormalizer( mod );
            try
            {
                Penumbra.Log.Debug( $"[Normalization] Starting Normalization of {mod.ModPath.Name}..." );
                if( !normalizer.CheckDirectories() )
                {
                    return;
                }

                Penumbra.Log.Debug( "[Normalization] Copying files to temporary directory structure..." );
                if( !normalizer.CopyNewFiles() )
                {
                    return;
                }

                Penumbra.Log.Debug( "[Normalization] Moving old files out of the way..." );
                if( !normalizer.MoveOldFiles() )
                {
                    return;
                }

                Penumbra.Log.Debug( "[Normalization] Moving new directory structure in place..." );
                if( !normalizer.MoveNewFiles() )
                {
                    return;
                }

                Penumbra.Log.Debug( "[Normalization] Applying new redirections..." );
                normalizer.ApplyRedirections( manager );
            }
            catch( Exception e )
            {
                ChatUtil.NotificationMessage( $"Could not normalize mod:\n{e}", "Failure", NotificationType.Error );
            }
            finally
            {
                Penumbra.Log.Debug( "[Normalization] Cleaning up remaining directories..." );
                normalizer.Cleanup();
            }
        }

        private bool CheckDirectories()
        {
            if( Directory.Exists( _normalizationDirName ) )
            {
                ChatUtil.NotificationMessage( "Could not normalize mod:\n"
                  + "The directory TmpNormalization may not already exist when normalizing a mod.", "Failure",
                    NotificationType.Error );
                return false;
            }

            if( Directory.Exists( _oldDirName ) )
            {
                ChatUtil.NotificationMessage( "Could not normalize mod:\n"
                  + "The directory TmpNormalizationOld may not already exist when normalizing a mod.", "Failure",
                    NotificationType.Error );
                return false;
            }

            return true;
        }

        private void Cleanup()
        {
            if( Directory.Exists( _normalizationDirName ) )
            {
                try
                {
                    Directory.Delete( _normalizationDirName, true );
                }
                catch
                {
                    // ignored
                }
            }

            if( Directory.Exists( _oldDirName ) )
            {
                try
                {
                    foreach( var dir in new DirectoryInfo( _oldDirName ).EnumerateDirectories() )
                    {
                        dir.MoveTo( Path.Combine( _mod.ModPath.FullName, dir.Name ) );
                    }

                    Directory.Delete( _oldDirName, true );
                }
                catch
                {
                    // ignored
                }
            }
        }

        private bool CopyNewFiles()
        {
            // We copy all files to a temporary folder to ensure that we can revert the operation on failure.
            try
            {
                var directory = Directory.CreateDirectory( _normalizationDirName );
                _redirections      = new Dictionary< Utf8GamePath, FullPath >[_mod.Groups.Count + 1][];
                _redirections[ 0 ] = new Dictionary< Utf8GamePath, FullPath >[] { new(_mod.Default.Files.Count) };

                // Normalize the default option.
                var newDict = new Dictionary< Utf8GamePath, FullPath >( _mod.Default.Files.Count );
                _redirections[ 0 ][ 0 ] = newDict;
                foreach( var (gamePath, fullPath) in _mod._default.FileData )
                {
                    var relPath      = new Utf8RelPath( gamePath ).ToString();
                    var newFullPath  = Path.Combine( directory.FullName, relPath );
                    var redirectPath = new FullPath( Path.Combine( _mod.ModPath.FullName, relPath ) );
                    Directory.CreateDirectory( Path.GetDirectoryName( newFullPath )! );
                    File.Copy( fullPath.FullName, newFullPath, true );
                    newDict.Add( gamePath, redirectPath );
                }

                // Normalize all other options.
                foreach( var (group, groupIdx) in _mod.Groups.WithIndex() )
                {
                    _redirections[ groupIdx + 1 ] = new Dictionary< Utf8GamePath, FullPath >[group.Count];
                    var groupDir = CreateModFolder( directory, group.Name );

                    foreach( var option in group.OfType< SubMod >() )
                    {
                        var optionDir = CreateModFolder( groupDir, option.Name );
                        newDict                                           = new Dictionary< Utf8GamePath, FullPath >( option.FileData.Count );
                        _redirections[ groupIdx + 1 ][ option.OptionIdx ] = newDict;
                        foreach( var (gamePath, fullPath) in option.FileData )
                        {
                            var relPath      = new Utf8RelPath( gamePath ).ToString();
                            var newFullPath  = Path.Combine( optionDir.FullName, relPath );
                            var redirectPath = new FullPath( Path.Combine( _mod.ModPath.FullName, groupDir.Name, optionDir.Name, relPath ) );
                            Directory.CreateDirectory( Path.GetDirectoryName( newFullPath )! );
                            File.Copy( fullPath.FullName, newFullPath, true );
                            newDict.Add( gamePath, redirectPath );
                        }
                    }
                }

                return true;
            }
            catch( Exception e )
            {
                ChatUtil.NotificationMessage( $"Could not normalize mod:\n{e}", "Failure", NotificationType.Error );
                _redirections = null;
            }

            return false;
        }

        private bool MoveOldFiles()
        {
            try
            {
                // Clean old directories and files.
                var oldDirectory = Directory.CreateDirectory( _oldDirName );
                foreach( var dir in _mod.ModPath.EnumerateDirectories() )
                {
                    if( dir.FullName.Equals( _oldDirName, StringComparison.OrdinalIgnoreCase )
                    || dir.FullName.Equals( _normalizationDirName, StringComparison.OrdinalIgnoreCase ) )
                    {
                        continue;
                    }

                    dir.MoveTo( Path.Combine( oldDirectory.FullName, dir.Name ) );
                }

                return true;
            }
            catch( Exception e )
            {
                ChatUtil.NotificationMessage( $"Could not move old files out of the way while normalizing mod mod:\n{e}", "Failure", NotificationType.Error );
            }

            return false;
        }

        private bool MoveNewFiles()
        {
            try
            {
                var mainDir = new DirectoryInfo( _normalizationDirName );
                foreach( var dir in mainDir.EnumerateDirectories() )
                {
                    dir.MoveTo( Path.Combine( _mod.ModPath.FullName, dir.Name ) );
                }

                mainDir.Delete();
                Directory.Delete( _oldDirName, true );
                return true;
            }
            catch( Exception e )
            {
                ChatUtil.NotificationMessage( $"Could not move new files into the mod while normalizing mod mod:\n{e}", "Failure", NotificationType.Error );
                foreach( var dir in _mod.ModPath.EnumerateDirectories() )
                {
                    if( dir.FullName.Equals( _oldDirName, StringComparison.OrdinalIgnoreCase )
                    || dir.FullName.Equals( _normalizationDirName, StringComparison.OrdinalIgnoreCase ) )
                    {
                        continue;
                    }

                    try
                    {
                        dir.Delete( true );
                    }
                    catch
                    {
                        // ignored
                    }
                }
            }

            return false;
        }

        private void ApplyRedirections( Manager manager )
        {
            if( _redirections == null )
            {
                return;
            }

            foreach( var option in _mod.AllSubMods.OfType< SubMod >() )
            {
                manager.OptionSetFiles( _mod, option.GroupIdx, option.OptionIdx, _redirections[ option.GroupIdx + 1 ][ option.OptionIdx ] );
            }
        }
    }
}