using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Dalamud.Plugin;

namespace Penumbra.Models
{
    public class Deduplicator
    {
        private readonly DirectoryInfo _baseDir;
        private readonly int           _baseDirLength;
        private readonly ModMeta       _mod;
        private SHA256                 _hasher = null;

        private readonly Dictionary<long, List<FileInfo>> _filesBySize = new();

        private ref SHA256 Sha()
        {
            if (_hasher == null)
                _hasher = SHA256.Create();
            return ref _hasher;
        }

        public Deduplicator(DirectoryInfo baseDir, ModMeta mod)
        {
            _baseDir       = baseDir;
            _baseDirLength = baseDir.FullName.Length;
            _mod           = mod;

            BuildDict();
        }

        private void BuildDict()
        {
            foreach( var file in _baseDir.EnumerateFiles( "*.*", SearchOption.AllDirectories ) )
            {
                var fileLength = file.Length;
                if (_filesBySize.TryGetValue(fileLength, out var files))
                    files.Add(file);
                else
                    _filesBySize[fileLength] = new(){ file };
            }
        }

        public void Run()
        {
            foreach (var pair in _filesBySize)
            {
                if (pair.Value.Count < 2)
                    continue;

                if (pair.Value.Count == 2)
                {
                    if (CompareFilesDirectly(pair.Value[0], pair.Value[1]))
                        ReplaceFile(pair.Value[0], pair.Value[1]);
                }
                else
                {
                    var deleted = Enumerable.Repeat(false, pair.Value.Count).ToArray();
                    var hashes  = pair.Value.Select( F => ComputeHash(F)).ToArray();

                    for (var i = 0; i < pair.Value.Count; ++i)
                    {
                        if (deleted[i])
                            continue;

                        for (var j = i + 1; j < pair.Value.Count; ++j)
                        {
                            if (deleted[j])
                                continue;

                            if (!CompareHashes(hashes[i], hashes[j]))
                                continue;

                            ReplaceFile(pair.Value[i], pair.Value[j]);
                            deleted[j] = true;
                        }
                    }
                }
            }
            ClearEmptySubDirectories(_baseDir);
        }

        private void ReplaceFile(FileInfo f1, FileInfo f2)
        {
            var relName1 = f1.FullName.Substring(_baseDirLength).TrimStart('\\');
            var relName2 = f2.FullName.Substring(_baseDirLength).TrimStart('\\');

            var inOption = false;
            foreach (var group in _mod.Groups.Select( g => g.Value.Options))
            {
                foreach (var option in group)
                {
                    if (option.OptionFiles.TryGetValue(relName2, out var values))
                    {
                        inOption = true;
                        foreach (var value in values)
                            option.AddFile(relName1, value);
                        option.OptionFiles.Remove(relName2);
                    }
                }
            }
            if (!inOption)
            {
                const string duplicates = "Duplicates";
                if (!_mod.Groups.ContainsKey(duplicates))
                {
                    InstallerInfo info = new()
                    {
                        GroupName = duplicates,
                        SelectionType = SelectType.Single,
                        Options = new()
                        {
                            new()
                            {
                                OptionName  = "Required",
                                OptionDesc  = "",
                                OptionFiles = new()
                            }
                        }
                    };
                    _mod.Groups.Add(duplicates, info);
                }
                _mod.Groups[duplicates].Options[0].AddFile(relName1, relName2.Replace('\\', '/'));
                _mod.Groups[duplicates].Options[0].AddFile(relName1, relName1.Replace('\\', '/'));
            }
            PluginLog.Information($"File {relName1} and {relName2} are identical. Deleting the second.");
            f2.Delete();
        }

        public static bool CompareFilesDirectly(FileInfo f1, FileInfo f2)
        {
            return File.ReadAllBytes(f1.FullName).SequenceEqual(File.ReadAllBytes(f2.FullName));
        }

        public static bool CompareHashes(byte[] f1, byte[] f2)
        {
            return StructuralComparisons.StructuralEqualityComparer.Equals(f1, f2);
        }

        public byte[] ComputeHash(FileInfo f)
        {
            var stream = File.OpenRead( f.FullName );
            var ret = Sha().ComputeHash(stream);
            stream.Dispose();
            return ret;
        }

        // Does not delete the base directory itself even if it is completely empty at the end.
        public static void ClearEmptySubDirectories(DirectoryInfo baseDir)
        {
            foreach (var subDir in baseDir.GetDirectories())
            {
                ClearEmptySubDirectories(subDir);
                if (subDir.GetFiles().Length == 0 && subDir.GetDirectories().Length == 0)
                    subDir.Delete();
            }
        }
    }
}