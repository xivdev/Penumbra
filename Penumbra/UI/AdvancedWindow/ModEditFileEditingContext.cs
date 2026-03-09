using Luna;
using Penumbra.Api.Enums;
using Penumbra.Collections;
using Penumbra.Collections.Manager;
using Penumbra.Mods;
using Penumbra.Mods.Editor;
using Penumbra.Mods.SubMods;
using Penumbra.String.Classes;
using Penumbra.UI.FileEditing;

namespace Penumbra.UI.AdvancedWindow;

public sealed class ModEditFileEditingContext(ActiveCollections activeCollections, ModEditor? editor, Mod? boundToMod) : FileEditingContext
{
    protected override ModCollection Collection
        => activeCollections.Current;

    public override ModEditor? Editor
        => editor;

    public override Mod? Mod
        => boundToMod ?? editor?.Mod;

    public override IModDataContainer? Option
        => boundToMod is null || editor?.Mod == boundToMod ? editor?.Option : null;

    public override FileRegistry? TryFindFileRegistry(Mod mod, Utf8RelPath relPath)
        => editor is not null
         && editor.Mod == mod
         && editor.Files.GetByType(ResourceType.FromPath(relPath.Path.Span))
                .FindFirst(r => relPath.Equals(r.RelPath), out var registry)
                ? registry
                : null;
}
