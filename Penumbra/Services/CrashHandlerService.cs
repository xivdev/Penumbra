using System.Text.Json;
using System.Text.Json.Nodes;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using OtterGui.Services;
using Penumbra.Collections;
using Penumbra.Communication;
using Penumbra.CrashHandler;
using Penumbra.CrashHandler.Buffers;
using Penumbra.GameData.Actors;
using Penumbra.Interop.Hooks.ResourceLoading;
using Penumbra.Interop.PathResolving;
using Penumbra.Interop.Structs;
using Penumbra.String;
using Penumbra.String.Classes;
using FileMode = System.IO.FileMode;

namespace Penumbra.Services;

public sealed class CrashHandlerService : IDisposable, IService
{
    private readonly FilenameService     _files;
    private readonly CommunicatorService _communicator;
    private readonly ActorManager        _actors;
    private readonly ResourceLoader      _resourceLoader;
    private readonly Configuration       _config;
    private readonly ValidityChecker     _validityChecker;

    private string _tempExecutableDirectory = string.Empty;

    public CrashHandlerService(FilenameService files, CommunicatorService communicator, ActorManager actors, ResourceLoader resourceLoader,
        Configuration config, ValidityChecker validityChecker)
    {
        _files           = files;
        _communicator    = communicator;
        _actors          = actors;
        _resourceLoader  = resourceLoader;
        _config          = config;
        _validityChecker = validityChecker;

        if (!(_config.UseCrashHandler ?? false))
            return;

        OpenEventWriter();
        LaunchCrashHandler();
        if (_eventWriter != null)
            Subscribe();
    }

    public void Dispose()
    {
        CloseEventWriter();
        _eventWriter?.Dispose();
        if (_child != null)
        {
            _child.Kill();
            Penumbra.Log.Debug($"Killed crash handler child process {_child.Id}.");
        }

        Unsubscribe();
        CleanExecutables();
    }

    private Process?            _child;
    private GameEventLogWriter? _eventWriter;

    public string CopiedExe = string.Empty;

    public string OriginalExe
        => _files.CrashHandlerExe;

    public string LogPath
        => _files.LogFileName;

    public int ChildProcessId
        => _child?.Id ?? -1;

    public int ProcessId
        => Environment.ProcessId;

    public bool IsRunning
        => _eventWriter != null && _child is { HasExited: false };

    public int ChildExitCode
        => IsRunning ? 0 : _child?.ExitCode ?? 0;

    public void Enable()
    {
        if (_config.UseCrashHandler ?? false)
            return;

        _config.UseCrashHandler = true;
        _config.Save();
        OpenEventWriter();
        LaunchCrashHandler();
        if (_eventWriter != null)
            Subscribe();
    }

    public void Disable()
    {
        if (!(_config.UseCrashHandler ?? false))
            return;

        _config.UseCrashHandler = false;
        _config.Save();
        CloseEventWriter();
        CloseCrashHandler();
        Unsubscribe();
    }

    public JsonObject? Load(string fileName)
    {
        if (!File.Exists(fileName))
            return null;

        try
        {
            var data = File.ReadAllText(fileName);
            return JsonNode.Parse(data) as JsonObject;
        }
        catch (Exception ex)
        {
            Penumbra.Log.Error($"Could not parse crash dump at {fileName}:\n{ex}");
            return null;
        }
    }

    public void CloseCrashHandler()
    {
        if (_child == null)
            return;

        try
        {
            if (_child.HasExited)
                return;

            _child.Kill();
            Penumbra.Log.Debug($"Closed Crash Handler at {CopiedExe}.");
        }
        catch (Exception ex)
        {
            _child = null;
            Penumbra.Log.Debug($"Closed not close Crash Handler at {CopiedExe}:\n{ex}.");
        }
    }

    public void LaunchCrashHandler()
    {
        try
        {
            CloseCrashHandler();
            CopiedExe = CopyExecutables();
            var info = new ProcessStartInfo()
            {
                CreateNoWindow = true,
                FileName       = CopiedExe,
            };
            info.ArgumentList.Add(_files.LogFileName);
            info.ArgumentList.Add(Environment.ProcessId.ToString());
            info.ArgumentList.Add($"{_validityChecker.Version} ({_validityChecker.CommitHash})");
            info.ArgumentList.Add(_validityChecker.GameVersion);
            _child = Process.Start(info);
            if (_child == null)
                throw new Exception("Child Process could not be created.");

            Penumbra.Log.Information($"Opened Crash Handler at {CopiedExe}, PID {_child.Id}.");
        }
        catch (Exception ex)
        {
            Penumbra.Log.Error($"Could not launch crash handler process:\n{ex}");
            CloseCrashHandler();
            _child = null;
        }
    }

    public JsonObject? Dump()
    {
        if (_eventWriter == null)
            return null;

        try
        {
            using var  reader = new GameEventLogReader(Environment.ProcessId);
            JsonObject jObj;
            lock (_eventWriter)
            {
                jObj = reader.Dump("Manual Dump", Environment.ProcessId, 0, $"{_validityChecker.Version} ({_validityChecker.CommitHash})",
                    _validityChecker.GameVersion);
            }

            var       logFile = _files.LogFileName;
            using var s       = File.Open(logFile, FileMode.Create);
            using var jw      = new Utf8JsonWriter(s, new JsonWriterOptions() { Indented = true });
            jObj.WriteTo(jw);
            Penumbra.Log.Information($"Dumped crash handler memory to {logFile}.");
            return jObj;
        }
        catch (Exception ex)
        {
            Penumbra.Log.Error($"Error dumping crash handler memory to file:\n{ex}");
            return null;
        }
    }

    private void CleanExecutables()
    {
        var parent = Path.GetDirectoryName(_files.CrashHandlerExe)!;
        foreach (var dir in Directory.EnumerateDirectories(parent, "temp_*"))
        {
            try
            {
                Directory.Delete(dir, true);
            }
            catch (Exception ex)
            {
                Penumbra.Log.Verbose($"Could not delete {dir}. This is generally not an error:\n{ex}");
            }
        }
    }

    private string CopyExecutables()
    {
        CleanExecutables();
        var parent = Path.GetDirectoryName(_files.CrashHandlerExe)!;
        _tempExecutableDirectory = Path.Combine(parent, $"temp_{Environment.ProcessId}");
        Directory.CreateDirectory(_tempExecutableDirectory);
        foreach (var file in Directory.EnumerateFiles(parent, "Penumbra.CrashHandler.*"))
            File.Copy(file, Path.Combine(_tempExecutableDirectory, Path.GetFileName(file)), true);
        return Path.Combine(_tempExecutableDirectory, Path.GetFileName(_files.CrashHandlerExe));
    }

    public void LogAnimation(nint character, ModCollection collection, AnimationInvocationType type)
    {
        if (_eventWriter == null)
            return;

        try
        {
            var name = GetActorName(character);
            lock (_eventWriter)
            {
                _eventWriter?.AnimationFuncInvoked.WriteLine(character, name.Span, collection.Identity.Id, type);
            }
        }
        catch (Exception ex)
        {
            Penumbra.Log.Warning($"Error logging animation function {type} to crash handler:\n{ex}");
        }
    }

    private void OnCreatingCharacterBase(nint address, Guid collection, nint _1, nint _2, nint _3)
    {
        if (_eventWriter == null)
            return;

        try
        {
            var name = GetActorName(address);

            lock (_eventWriter)
            {
                _eventWriter?.CharacterBase.WriteLine(address, name.Span, collection);
            }
        }
        catch (Exception ex)
        {
            Penumbra.Log.Warning($"Error logging character creation to crash handler:\n{ex}");
        }
    }

    private unsafe ByteString GetActorName(nint address)
    {
        var obj = (GameObject*)address;
        if (obj == null)
            return ByteString.FromSpanUnsafe("Unknown"u8, true, false, true);

        var id = _actors.FromObject(obj, out _, false, true, false);
        return id.IsValid     ? ByteString.FromStringUnsafe(id.Incognito(null),                                       false) :
            obj->Name[0] != 0 ? new ByteString(obj->Name) : ByteString.FromStringUnsafe($"Actor #{obj->ObjectIndex}", false);
    }

    private unsafe void OnResourceLoaded(ResourceHandle* handle, Utf8GamePath originalPath, FullPath? manipulatedPath, ResolveData resolveData)
    {
        if (manipulatedPath == null || _eventWriter == null)
            return;

        try
        {
            if (PathDataHandler.Split(manipulatedPath.Value.FullName, out var actualPath, out _) && !Path.IsPathRooted(actualPath))
                return;

            var name = GetActorName(resolveData.AssociatedGameObject);
            lock (_eventWriter)
            {
                _eventWriter!.FileLoaded.WriteLine(resolveData.AssociatedGameObject, name.Span, resolveData.ModCollection.Identity.Id,
                    manipulatedPath.Value.InternalName.Span, originalPath.Path.Span);
            }
        }
        catch (Exception ex)
        {
            Penumbra.Log.Warning($"Error logging resource to crash handler:\n{ex}");
        }
    }

    private void CloseEventWriter()
    {
        if (_eventWriter == null)
            return;

        _eventWriter.Dispose();
        _eventWriter = null;
        Penumbra.Log.Debug("Closed Event Writer for crash handler.");
    }

    private void OpenEventWriter()
    {
        try
        {
            CloseEventWriter();
            _eventWriter = new GameEventLogWriter(Environment.ProcessId);
            Penumbra.Log.Debug("Opened new Event Writer for crash handler.");
        }
        catch (Exception ex)
        {
            Penumbra.Log.Error($"Could not open Event Writer:\n{ex}");
            CloseEventWriter();
        }
    }

    private unsafe void Subscribe()
    {
        _communicator.CreatingCharacterBase.Subscribe(OnCreatingCharacterBase, CreatingCharacterBase.Priority.CrashHandler);
        _resourceLoader.ResourceLoaded += OnResourceLoaded;
    }

    private unsafe void Unsubscribe()
    {
        _communicator.CreatingCharacterBase.Unsubscribe(OnCreatingCharacterBase);
        _resourceLoader.ResourceLoaded -= OnResourceLoaded;
    }
}
