using Serilog;
using Serilog.Core;

namespace SpotifyPlaylistUtilities.Logging;

public static class LoggerSetup
{
    public static Logger? Logger { get; set; }
    
    public static LoggerConfiguration ConfigureLogger()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(AppPaths.LogPath) ?? "");

        return new LoggerConfiguration()
            .Enrich.WithProperty("Application", "SerilogTestContext")
            .WriteTo.Console()
            .WriteTo.Debug()
            .WriteTo.File(AppPaths.LogPath, rollingInterval: RollingInterval.Day);
    }
}