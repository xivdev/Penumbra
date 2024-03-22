using System.Diagnostics;
using System.Text.Json;

namespace Penumbra.CrashHandler;

public class CrashHandler
{
    public static void Main(string[] args)
    {
        if (args.Length < 4 || !int.TryParse(args[1], out var pid))
            return;

        try
        {
            using var reader = new GameEventLogReader(pid);
            var       parent = Process.GetProcessById(pid);
            using var handle = parent.SafeHandle;
            parent.WaitForExit();
            int exitCode;
            try
            {
                exitCode = parent.ExitCode;
            }
            catch
            {
                exitCode = -1;
            }

            var       obj = reader.Dump("Crash", pid, exitCode, args[2], args[3]);
            using var fs  = File.Open(args[0], FileMode.Create);
            using var w   = new Utf8JsonWriter(fs, new JsonWriterOptions { Indented = true });
            obj.WriteTo(w, new JsonSerializerOptions() { WriteIndented = true });
        }
        catch (Exception ex)
        {
            File.WriteAllText(args[0], $"{DateTime.UtcNow} {pid} {ex}");
        }
    }
}
