using System;
using System.IO;
using Serilog;

namespace SpotifyPlaylistUtility;

public class LoggerSetup
{
    /// <summary>
    /// Full path to base folder for logs (the folder, not the log files themselves)
    /// </summary>
    private static string AppName => "Spotify-Playlist-Utility_";
    
    /// <summary>
    /// Full path to base folder for logs (the folder, not the log files themselves)
    /// </summary>
    private static string LogAppBasePath =>
        Path.Combine(
            GetAppRoot(), 
            "Logs");
    
    /// <summary>
    /// Full path to a generic log filename, for Serilog
    /// </summary>
    private static string LogPath => 
        Path.Combine(
            LogAppBasePath,
            $"{AppName}.log");
    
    public static LoggerConfiguration ConfigureLogger()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(LogPath) ?? "");

        return new LoggerConfiguration()
            .Enrich.WithProperty("Application", "SerilogTestContext")
            .WriteTo.Console()
            .WriteTo.Debug()
            .WriteTo.File(LogPath, rollingInterval: RollingInterval.Day);
    }
    
    /// <summary>
    /// Full path to the directory the app is running from, used for building log and settings directories
    /// </summary>
    private static string GetAppRoot()
    {
        return Path.GetDirectoryName(Environment.ProcessPath) ?? "ERROR_GETTING_APP_PATH";
    }
}