using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dalamud.Data;
using Dalamud.Plugin;
using Lumina.Excel.GeneratedSheets;
using OtterGui.Classes;
using Penumbra.GameData;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Structs;
using Penumbra.Util;
using Action = System.Action;

namespace Penumbra.Services;

public sealed class ObjectIdentifier : IObjectIdentifier
{
    private const string Prefix = $"[{nameof(ObjectIdentifier)}]";

    public IObjectIdentifier? Identifier { get; private set; }

    public bool IsDisposed { get; private set; }

    public bool Ready
        => Identifier != null && !IsDisposed;

    public event Action? FinishedCreation;

    public ObjectIdentifier(StartTimeTracker<StartTimeType> tracker, DalamudPluginInterface pi, DataManager data)
    {
        Task.Run(() =>
        {
            using var timer = tracker.Measure(StartTimeType.Identifier);
            var identifier = GameData.GameData.GetIdentifier(pi, data);
            if (IsDisposed)
            {
                identifier.Dispose();
            }
            else
            {
                Identifier = identifier;
                Penumbra.Log.Verbose($"{Prefix} Created.");
                FinishedCreation?.Invoke();
            }
        });
    }

    public void Dispose()
    {
        Identifier?.Dispose();
        IsDisposed = true;
        Penumbra.Log.Verbose($"{Prefix} Disposed.");
    }

    public IGamePathParser GamePathParser
        => Identifier?.GamePathParser ?? throw new Exception($"{Prefix} Not yet ready.");

    public void Identify(IDictionary<string, object?> set, string path)
        => Identifier?.Identify(set, path);

    public Dictionary<string, object?> Identify(string path)
        => Identifier?.Identify(path) ?? new Dictionary<string, object?>();

    public IEnumerable<Item> Identify(SetId setId, WeaponType weaponType, ushort variant, EquipSlot slot)
        => Identifier?.Identify(setId, weaponType, variant, slot) ?? Array.Empty<Item>();
}
