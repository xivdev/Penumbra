using FFXIVClientStructs.Havok;

namespace Penumbra.Import.Models;

// TODO: where should this live? interop i guess, in penum? or game data?
public unsafe class HavokConverter
{
    /// <summary>Creates a temporary file and returns its path.</summary>
    /// <returns>Path to a temporary file.</returns>
    private string CreateTempFile()
    {
        var s = File.Create(Path.GetTempFileName());
        s.Close();
        return s.Name;
    }

    /// <summary>Converts a .hkx file to a .xml file.</summary>
    /// <param name="hkx">A byte array representing the .hkx file.</param>
    /// <returns>A string representing the .xml file.</returns>
    /// <exception cref="Exceptions.HavokReadException">Thrown if parsing the .hkx file fails.</exception>
    /// <exception cref="Exceptions.HavokWriteException">Thrown if writing the .xml file fails.</exception>
    public string HkxToXml(byte[] hkx)
    {
        var tempHkx = CreateTempFile();
        File.WriteAllBytes(tempHkx, hkx);

        var resource = Read(tempHkx);
        File.Delete(tempHkx);

        if (resource == null) throw new Exception("HavokReadException");

        var options = hkSerializeUtil.SaveOptionBits.SerializeIgnoredMembers
            | hkSerializeUtil.SaveOptionBits.TextFormat
            | hkSerializeUtil.SaveOptionBits.WriteAttributes;

        var file = Write(resource, options);
        file.Close();

        var bytes = File.ReadAllText(file.Name);
        File.Delete(file.Name);

        return bytes;
    }

    /// <summary>Converts a .xml file to a .hkx file.</summary>
    /// <param name="xml">A string representing the .xml file.</param>
    /// <returns>A byte array representing the .hkx file.</returns>
    /// <exception cref="Exceptions.HavokReadException">Thrown if parsing the .xml file fails.</exception>
    /// <exception cref="Exceptions.HavokWriteException">Thrown if writing the .hkx file fails.</exception>
    public byte[] XmlToHkx(string xml)
    {
        var tempXml = CreateTempFile();
        File.WriteAllText(tempXml, xml);

        var resource = Read(tempXml);
        File.Delete(tempXml);

        if (resource == null) throw new Exception("HavokReadException");

        var options = hkSerializeUtil.SaveOptionBits.SerializeIgnoredMembers
            | hkSerializeUtil.SaveOptionBits.WriteAttributes;

        var file = Write(resource, options);
        file.Close();

        var bytes = File.ReadAllBytes(file.Name);
        File.Delete(file.Name);

        return bytes;
    }

    /// <summary>
    /// Parses a serialized file into an hkResource*.
    /// The type is guessed automatically by Havok.
    /// This pointer might be null - you should check for that.
    /// </summary>
    /// <param name="filePath">Path to a file on the filesystem.</param>
    /// <returns>A (potentially null) pointer to an hkResource.</returns>
    private hkResource* Read(string filePath)
    {
        var path = Marshal.StringToHGlobalAnsi(filePath);

        var builtinTypeRegistry = hkBuiltinTypeRegistry.Instance();

        var loadOptions = stackalloc hkSerializeUtil.LoadOptions[1];
        loadOptions->Flags = new() { Storage = (int)hkSerializeUtil.LoadOptionBits.Default };
        loadOptions->ClassNameRegistry = builtinTypeRegistry->GetClassNameRegistry();
        loadOptions->TypeInfoRegistry = builtinTypeRegistry->GetTypeInfoRegistry();

        // TODO: probably can loadfrombuffer this
        var resource = hkSerializeUtil.LoadFromFile((byte*)path, null, loadOptions);
        return resource;
    }

    /// <summary>Serializes an hkResource* to a temporary file.</summary>
    /// <param name="resource">A pointer to the hkResource, opened through Read().</param>
    /// <param name="optionBits">Flags representing how to serialize the file.</param>
    /// <returns>An opened FileStream of a temporary file. You are expected to read the file and delete it.</returns>
    /// <exception cref="Exceptions.HavokWriteException">Thrown if accessing the root level container fails.</exception>
    /// <exception cref="Exceptions.HavokFailureException">Thrown if an unknown failure in writing occurs.</exception>
    private FileStream Write(
        hkResource* resource,
        hkSerializeUtil.SaveOptionBits optionBits
    )
    {
        var tempFile = CreateTempFile();
        var path = Marshal.StringToHGlobalAnsi(tempFile);
        var oStream = new hkOstream();
        oStream.Ctor((byte*)path);

        var result = stackalloc hkResult[1];

        var saveOptions = new hkSerializeUtil.SaveOptions()
        {
            Flags = new() { Storage = (int)optionBits }
        };


        var builtinTypeRegistry = hkBuiltinTypeRegistry.Instance();
        var classNameRegistry = builtinTypeRegistry->GetClassNameRegistry();
        var typeInfoRegistry = builtinTypeRegistry->GetTypeInfoRegistry();

        try
        {
            var name = "hkRootLevelContainer";

            var resourcePtr = (hkRootLevelContainer*)resource->GetContentsPointer(name, typeInfoRegistry);
            if (resourcePtr == null) throw new Exception("HavokWriteException");

            var hkRootLevelContainerClass = classNameRegistry->GetClassByName(name);
            if (hkRootLevelContainerClass == null) throw new Exception("HavokWriteException");

            hkSerializeUtil.Save(result, resourcePtr, hkRootLevelContainerClass, oStream.StreamWriter.ptr, saveOptions);
        }
        finally { oStream.Dtor(); }

        if (result->Result == hkResult.hkResultEnum.Failure) throw new Exception("HavokFailureException");

        return new FileStream(tempFile, FileMode.Open);
    }
}
