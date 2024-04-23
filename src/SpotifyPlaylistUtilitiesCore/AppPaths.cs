namespace SpotifyPlaylistUtilities;

public static class AppPaths
{
    /// <summary>
    /// Full path to base folder for logs (the folder, not the log files themselves)
    /// </summary>
    public static string AppName => "Spotify-Playlist-Utility_";
    
    private static string ApplicationDataBasePath =>
        Path.Combine(
            GetAppRoot(), 
            "Data");
    
    /// <summary>
    /// Full path to a generic log filename, for Serilog
    /// </summary>
    public static string LogPath => 
        Path.Combine(
            ApplicationDataBasePath,
            "Logs",
            $"{AppName}.log");

    /// <summary>
    /// Full path to 
    /// </summary>
    public static string SavedTrackJsonPath => 
        Path.Combine(
            ApplicationDataBasePath,
            "TrackWeights",
            "KnownTrackWeights.json");

    /// <summary>
    /// Full path to the directory the app is running from, used for building log and settings directories
    /// </summary>
    private static string GetAppRoot()
    {
        return Path.GetDirectoryName(Environment.ProcessPath) ?? "ERROR_GETTING_APP_PATH";
    }
}