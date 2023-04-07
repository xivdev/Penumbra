using Dalamud.Game.ClientState.Keys;
using OtterGui.Filesystem;
using OtterGui.FileSystem.Selector;
using Penumbra.Mods;
using Penumbra.UI.Classes;
using Penumbra.UI.ModsTab;
using System;
using System.IO;
using System.Linq;

namespace Penumbra.Api {
    public class ExternalModImporter {
        private static ModFileSystemSelector modFileSystemSelectorInstance;

        public static ModFileSystemSelector ModFileSystemSelectorInstance { get => modFileSystemSelectorInstance; set => modFileSystemSelectorInstance = value; }

        public static void UnpackMod(string modPackagePath)
        {
            modFileSystemSelectorInstance.ImportStandaloneModPackage(modPackagePath);
        }
    }
}
