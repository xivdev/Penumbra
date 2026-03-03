using System.Collections.Frozen;
using Dalamud.Plugin.Services;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Files;

namespace Penumbra.Meta;

public sealed unsafe class AtchManager : Luna.IService
{
    private static readonly IReadOnlyList<GenderRace> GenderRaces =
    [
        GenderRace.MidlanderMale, GenderRace.MidlanderFemale, GenderRace.HighlanderMale, GenderRace.HighlanderFemale, GenderRace.ElezenMale,
        GenderRace.ElezenFemale, GenderRace.MiqoteMale, GenderRace.MiqoteFemale, GenderRace.RoegadynMale, GenderRace.RoegadynFemale,
        GenderRace.LalafellMale, GenderRace.LalafellFemale, GenderRace.AuRaMale, GenderRace.AuRaFemale, GenderRace.HrothgarMale,
        GenderRace.HrothgarFemale, GenderRace.VieraMale, GenderRace.VieraFemale,
    ];

    public readonly IReadOnlyDictionary<GenderRace, AtchFile> AtchFileBase;

    public AtchManager(IDataManager manager)
    {
        AtchFileBase = GenderRaces.ToFrozenDictionary(gr => gr,
            gr => new AtchFile(manager.GetFile($"chara/xls/attachOffset/c{gr.ToRaceCode()}.atch")!.DataSpan));
    }
}
