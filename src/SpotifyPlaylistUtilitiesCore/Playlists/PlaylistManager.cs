using Newtonsoft.Json;
using Serilog;
using SpotifyAPI.Web;

namespace SpotifyPlaylistUtilities.Playlists;

public class PlaylistManager
{
    private readonly ILogger _logger;
    private readonly SpotifyClient _spotifyClient;

    public PlaylistManager(ILogger logger, SpotifyClient spotifyClient)
    {
        _logger = logger;
        _spotifyClient = spotifyClient;
    }
    
    public async Task<FullPlaylist> FindPlaylistById(string playlistId)
    {
        var playlists = await _spotifyClient.PaginateAll(await _spotifyClient.Playlists.CurrentUsers().ConfigureAwait(false));
        
        foreach (var playlist in playlists)
        {
            if (playlist.Id == playlistId)
                return playlist;
        }
    
        throw new Exception("Couldn't find playlist with ID matching Curated Weebletdays ID");
    }
    
    public void BackupTracksToJsonFile(string playlistId, List<PlaylistTrack<IPlayableItem>> tracks)
    {
        // JsonConvert to file with playlist ID
        var jsonString = JsonConvert.SerializeObject(tracks);
        
        var filePath = Path.GetDirectoryName(Environment.ProcessPath) ?? "ERROR_GETTING_APP_PATH";

        var backupsPath = Path.Join(filePath, "Backups");

        Directory.CreateDirectory(backupsPath);

        var filename = 
            playlistId + "_" +
            DateTimeOffset.Now.ToString("s")
            .Replace("T", "_T")
            .Replace(":", "-")
            + "_backup.json";

        var fullFilePath = Path.Join(backupsPath, filename);
        
        File.WriteAllText(fullFilePath, jsonString);
    }

    public async Task RemoveTracksFrom(string playlistId, List<PlaylistTrack<IPlayableItem>> playlistTracks)
    {
        var itemsToRemove = new List<string>();
        
        foreach (var trackToRemove in playlistTracks)
        {
            var fullTrackConverted = trackToRemove.Track as FullTrack;

            _logger.Information($"Attempting to remove: {fullTrackConverted?.Name}");

            itemsToRemove.Add(fullTrackConverted?.Uri ?? "");

            if (itemsToRemove.Count < 100) continue;
            
            // If we hit capacity, add and clear
            await Remove100ItemsFrom(playlistId, itemsToRemove);
        }
         
        if (itemsToRemove.Count > 0)
            await Remove100ItemsFrom(playlistId, itemsToRemove);
    }
    
    public async Task<List<PlaylistTrack<IPlayableItem>>> GetAllPlaylistTracks(string playlistId)
    {
        var convertedPlaylist = await _spotifyClient.Playlists.Get(playlistId);

        if (convertedPlaylist.Tracks is not null)
        {
            var allTracks = await _spotifyClient.PaginateAll(convertedPlaylist.Tracks);

            var returnTracks = new List<PlaylistTrack<IPlayableItem>>();
        
            var tracksCount = 0;
            foreach (var playlistTrack in allTracks)
            {
                if (playlistTrack.Track is not FullTrack track) continue;

                // All FullTrack properties are available
                var artistString = track.Artists.First().Name;
            
                _logger.Information($"Track: #{tracksCount++}: {artistString} - {track.Name} | ID: {track.Id}");
            
                returnTracks.Add(playlistTrack);
            }

            // Lazy rate-limiting (not really, but at least between tasks) 
            await Task.Delay(10000);

            return returnTracks;
        }
        
        throw new Exception(
            $"Attempted to get all playlist tracks for playlist ID: {playlistId} but got back null playlist");
    }
    
    public async Task Remove100ItemsFrom(string playlistId, List<string> itemsToRemove)
    {
        await Task.Delay(1000);
            
        // Otherwise, since we can only remove 100 at a time:
        var itemsToRemoveRequest = new PlaylistRemoveItemsRequest();

        itemsToRemoveRequest.Tracks = new List<PlaylistRemoveItemsRequest.Item>();
        
        foreach (var itemToRemove in itemsToRemove)
        {
            itemsToRemoveRequest.Tracks.Add(
                new PlaylistRemoveItemsRequest.Item()
                {
                    Uri = itemToRemove
                });
        }

        await _spotifyClient.Playlists.RemoveItems(playlistId, itemsToRemoveRequest);

        itemsToRemove.Clear();
    }
}