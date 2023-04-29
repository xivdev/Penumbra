using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using OtterGui;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Files;

namespace Penumbra.Mods;

public partial class MdlMaterialEditor
{
    [GeneratedRegex(@"/mt_c(?'RaceCode'\d{4})b0001_(?'Suffix'.*?)\.mtrl", RegexOptions.ExplicitCapture | RegexOptions.NonBacktracking)]
    private static partial Regex MaterialRegex();

    private readonly ModFileCollection _files;

    private readonly List<ModelMaterialInfo> _modelFiles = new();

    public IReadOnlyList<ModelMaterialInfo> ModelFiles
        => _modelFiles;

    public MdlMaterialEditor(ModFileCollection files)
        => _files = files;

    public void SaveAllModels()
    {
        foreach (var info in _modelFiles)
            info.Save();
    }

    public void RestoreAllModels()
    {
        foreach (var info in _modelFiles)
            info.Restore();
    }

    public void Clear()
    {
        _modelFiles.Clear();
    }

    /// <summary>
    /// Go through the currently loaded files and replace all appropriate suffices.
    /// Does nothing if toSuffix is invalid.
    /// If raceCode is Unknown, apply to all raceCodes.
    /// If fromSuffix is empty, apply to all suffices.
    /// </summary>
    public void ReplaceAllMaterials(string toSuffix, string fromSuffix = "", GenderRace raceCode = GenderRace.Unknown)
    {
        if (!ValidString(toSuffix))
            return;

        foreach (var info in _modelFiles)
        {
            for (var i = 0; i < info.Count; ++i)
            {
                var (_, def) = info[i];
                var match = MaterialRegex().Match(def);
                if (match.Success
                 && (raceCode == GenderRace.Unknown || raceCode.ToRaceCode() == match.Groups["RaceCode"].Value)
                 && (fromSuffix.Length == 0 || fromSuffix == match.Groups["Suffix"].Value))
                    info.SetMaterial($"/mt_c{match.Groups["RaceCode"].Value}b0001_{toSuffix}.mtrl", i);
            }
        }
    }

    /// Non-ASCII encoding is not supported.
    public static bool ValidString(string to)
        => to.Length != 0
         && to.Length < 16
         && Encoding.UTF8.GetByteCount(to) == to.Length;

    /// <summary> Find all model files in the mod that contain skin materials. </summary>
    public void ScanModels(Mod mod)
    {
        _modelFiles.Clear();
        foreach (var file in _files.Mdl)
        {
            try
            {
                var bytes   = File.ReadAllBytes(file.File.FullName);
                var mdlFile = new MdlFile(bytes);
                var materials = mdlFile.Materials.WithIndex().Where(p => MaterialRegex().IsMatch((string)p.Item1))
                    .Select(p => p.Item2).ToArray();
                if (materials.Length > 0)
                    _modelFiles.Add(new ModelMaterialInfo(file.File, mdlFile, materials));
            }
            catch (Exception e)
            {
                Penumbra.Log.Error($"Unexpected error scanning {mod.Name}'s {file.File.FullName} for materials:\n{e}");
            }
        }
    }
}
