using ImSharp;
using Penumbra.GameData.Data;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Files;

namespace Penumbra.UI.FileEditing.Skeletons;

public partial class DeformerEditor(Configuration configuration, PbdFile file, string filePath, bool writable) : IFileEditor
{
    public readonly string FilePath = filePath;
    public readonly bool   Writable = writable;

    public readonly PbdFile File = file;
    
    bool IFileEditor.Writable
        => Writable;

    event Action? IFileEditor.SaveRequested
    {
        add { }
        remove { }
    }

    void IDisposable.Dispose()
    {
    }

    public bool Valid
        => File.Valid;

    public byte[] Write()
        => File.Write();
    
    public GenderRace      SelectedRaceCode = GenderRace.Unknown;
    public RacialDeformer? SelectedDeformer;
    public string?         SelectedBone;
    public TextFilter      BoneFilter     = new();
    public string          NewBoneName    = string.Empty;
    public string          RaceCodeFilter = string.Empty;

    public TransformMatrix? CopiedMatrix;

    public void Update(PbdFile file)
    {
        if (SelectedRaceCode is GenderRace.Unknown)
        {
            SelectedDeformer = null;
        }
        else
        {
            SelectedDeformer = file.Deformers.FirstOrDefault(p => p.GenderRace == SelectedRaceCode).RacialDeformer;
            if (SelectedDeformer is null)
                SelectedRaceCode = GenderRace.Unknown;
        }
    }
}
