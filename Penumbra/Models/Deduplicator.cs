using System.Collections.Generic;
using System.Security.Cryptography;
using System.IO;
using System.Linq;
using System.Collections;
using Dalamud.Plugin;

namespace Penumbra.Models
{
    public class Deduplicator
    {
        private DirectoryInfo baseDir;
        private int           baseDirLength;
        private ModMeta       mod;
        private SHA256        hasher = null;

        private Dictionary<long, List<FileInfo>> filesBySize;

        private ref SHA256 Sha()
        {
            if (hasher == null)
                hasher = SHA256.Create();
            return ref hasher;
        }

        public Deduplicator(DirectoryInfo baseDir, ModMeta mod)
        {
            this.baseDir       = baseDir;
            this.baseDirLength = baseDir.FullName.Length;
            this.mod           = mod;
            filesBySize        = new();

            BuildDict();  
        }

        private void BuildDict()
        {
            foreach( var file in baseDir.EnumerateFiles( "*.*", SearchOption.AllDirectories ) )
            {
                var fileLength = file.Length;
                if (filesBySize.TryGetValue(fileLength, out var files))
                    files.Add(file);
                else
                    filesBySize[fileLength] = new(){ file };
            }
        }

        public void Run()
        {
            foreach (var pair in filesBySize)
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
            ClearEmptySubDirectories(baseDir);
        }

        private void ReplaceFile(FileInfo f1, FileInfo f2)
        {
            var relName1 = f1.FullName.Substring(baseDirLength).TrimStart('\\');
            var relName2 = f2.FullName.Substring(baseDirLength).TrimStart('\\');

            var inOption = false;
            foreach (var group in mod.Groups.Select( g => g.Value.Options))
            {
                foreach (var option in group)
                {
                    if (option.OptionFiles.TryGetValue(relName2, out var values))
                    {
                        inOption = true;
                        foreach (var value in values)
                            option.AddFile(relName1, value);
                    }
                }
            }
            if (!inOption)
            {
                const string duplicates = "Duplicates";
                if (!mod.Groups.ContainsKey(duplicates))
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
                    mod.Groups.Add(duplicates, info);
                }
                mod.Groups[duplicates].Options[0].AddFile(relName1, relName2.Replace('\\', '/'));
                mod.Groups[duplicates].Options[0].AddFile(relName1, relName1.Replace('\\', '/'));
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