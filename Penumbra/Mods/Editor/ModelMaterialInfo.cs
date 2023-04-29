using System;
using System.Collections.Generic;
using System.Linq;
using OtterGui;
using Penumbra.GameData.Files;
using Penumbra.String.Classes;

namespace Penumbra.Mods;

/// <summary> A class that collects information about skin materials in a model file and handle changes on them. </summary>
public class ModelMaterialInfo
{
    public readonly  FullPath           Path;
    public readonly  MdlFile            File;
    private readonly string[]           _currentMaterials;
    private readonly IReadOnlyList<int> _materialIndices;
    public           bool               Changed { get; private set; }

    public IReadOnlyList<string> CurrentMaterials
        => _currentMaterials;

    private IEnumerable<string> DefaultMaterials
        => _materialIndices.Select(i => File.Materials[i]);

    public (string Current, string Default) this[int idx]
        => (_currentMaterials[idx], File.Materials[_materialIndices[idx]]);

    public int Count
        => _materialIndices.Count;

    // Set the skin material to a new value and flag changes appropriately.
    public void SetMaterial(string value, int materialIdx)
    {
        var mat = File.Materials[_materialIndices[materialIdx]];
        _currentMaterials[materialIdx] = value;
        if (mat != value)
            Changed = true;
        else
            Changed = !_currentMaterials.SequenceEqual(DefaultMaterials);
    }

    // Save a changed .mdl file.
    public void Save()
    {
        if (!Changed)
            return;

        foreach (var (idx, i) in _materialIndices.WithIndex())
            File.Materials[idx] = _currentMaterials[i];

        try
        {
            System.IO.File.WriteAllBytes(Path.FullName, File.Write());
            Changed = false;
        }
        catch (Exception e)
        {
            Restore();
            Penumbra.Log.Error($"Could not write manipulated .mdl file {Path.FullName}:\n{e}");
        }
    }

    // Revert all current changes.
    public void Restore()
    {
        if (!Changed)
            return;

        foreach (var (idx, i) in _materialIndices.WithIndex())
            _currentMaterials[i] = File.Materials[idx];

        Changed = false;
    }

    public ModelMaterialInfo(FullPath path, MdlFile file, IReadOnlyList<int> indices)
    {
        Path              = path;
        File              = file;
        _materialIndices  = indices;
        _currentMaterials = DefaultMaterials.ToArray();
    }
}
