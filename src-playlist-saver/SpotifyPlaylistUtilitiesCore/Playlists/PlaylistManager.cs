using Newtonsoft.Json;
using Serilog;
using SpotifyAPI.Web;
using SpotifyPlaylistUtilities.Models;

namespace SpotifyPlaylistUtilities.Playlists;

public class PlaylistManager(ILogger logger, SpotifyClient spotifyClient)
{
    public async Task PrintAllPlaylistData()
    {
        var playlists = await spotifyClient.PaginateAll(await spotifyClient.Playlists.CurrentUsers().ConfigureAwait(false));
        
        foreach (var playlist in playlists)
        {
            logger.Information("In all playlists - Got: {PlaylistName} with ID: {PlaylistId}", playlist.Name, playlist.Id);
        }
    }
    
    public async Task<ManagedPlaylist> GetPlaylistByName(string playlistName)
    {
        var playlists = await spotifyClient.PaginateAll(await spotifyClient.Playlists.CurrentUsers().ConfigureAwait(false));
        
        foreach (var playlist in playlists)
        {
            logger.Debug("In all playlists - Got: {PlaylistName} with ID: {PlaylistId} [Trying to match: {MatchName}]", playlist.Name, playlist.Id, playlistName);
        }
        
        foreach (var playlist in playlists)
        {
            if (playlist.Name != playlistName) continue;

            var returnManagedPlaylist = new ManagedPlaylist(logger, spotifyClient, playlist);

            await returnManagedPlaylist.FetchAllTracks();
            
            return returnManagedPlaylist;
        }
        
        throw new ArgumentException("Couldn't find playlist with name of: {SuppliedName}", playlistName);
    }
    
    public async Task<ManagedPlaylist> GetPlaylistById(string playlistId)
    {
        var playlists = await spotifyClient.PaginateAll(await spotifyClient.Playlists.CurrentUsers().ConfigureAwait(false));
        
        foreach (var playlist in playlists)
        {
            logger.Debug("In all playlists - Got: {PlaylistName} with ID: {PlaylistId} [Trying to match: {MatchId}]", playlist.Name, playlist.Id, playlistId);
        }
        
        foreach (var playlist in playlists)
        {
            if (playlist.Id != playlistId) continue;

            var returnManagedPlaylist = new ManagedPlaylist(logger, spotifyClient, playlist);

            await returnManagedPlaylist.FetchAllTracks();
            
            return returnManagedPlaylist;
        }

        // Lazy rate limiting, sort of
        await Task.Delay(2000);
        
        throw new ArgumentException("Couldn't find playlist with name of: {SuppliedName}", playlistId);
    }
    
    public void BackupTracksToJsonFile(ManagedPlaylist playlistToBackup)
    {
        // JsonConvert to file with playlist ID
        var jsonString = JsonConvert.SerializeObject(playlistToBackup);
        
        var filePath = Path.GetDirectoryName(Environment.ProcessPath) ?? "ERROR_GETTING_APP_PATH";
    
        var safePlaylistName = playlistToBackup.Name;

        foreach (var invalidFileNameChar in Path.GetInvalidFileNameChars())
        {
            safePlaylistName = safePlaylistName.Replace(invalidFileNameChar.ToString(), "_").Trim();
        }
        
        var backupsPath = Path.Join(filePath, "Backups", "Playlists",  safePlaylistName);
    
        Directory.CreateDirectory(backupsPath);
        
        var filename =
            DateTimeOffset.Now.ToString("s")
                .Replace("T", "_T")
                .Replace(":", "-")
                + $"_{playlistToBackup.Id}.json";
    
        var fullFilePath = Path.Join(backupsPath, filename);
        
        File.WriteAllText(fullFilePath, jsonString);
    }
    
    public async Task DeleteAllPlaylistTracksOnSpotify(ManagedPlaylist playlistToClear)
    {
        var itemsToRemove = new List<string>();
        
        foreach (var trackToRemove in playlistToClear.FetchedTracks)
        {
            logger.Debug("Attempting to remove track: {TrackName} from playlist: {PlaylistName}", trackToRemove.Name, playlistToClear.Name);

            if (string.IsNullOrWhiteSpace(trackToRemove.Uri))
                throw new ArgumentException("Track URI was empty");
            
            itemsToRemove.Add(trackToRemove.Uri);
    
            if (itemsToRemove.Count < 100) continue;
            
            // If we hit capacity, add and clear
            await Remove100ItemsFrom(playlistToClear, itemsToRemove);
        }
         
        if (itemsToRemove.Count > 0)
            await Remove100ItemsFrom(playlistToClear, itemsToRemove);
    }

    public async Task AddTracksToPlaylistOnSpotify(ManagedPlaylist playlist, List<ManagedPlaylistTrack> tracks)
    {
        var itemsToAdd = new List<string>();
        
        foreach (var track in tracks)
        {
            logger.Debug("Attempting to add: {TrackName}", track.Name);
    
            itemsToAdd.Add(track.Uri);
    
            if (itemsToAdd.Count < 100) continue;
            
            // If we hit capacity, add and clear
            await Add100ItemsToPlaylist(itemsToAdd, playlist.Id);
        }
        
        // Add any remaining
        if (itemsToAdd.Count > 0)
            await Add100ItemsToPlaylist(itemsToAdd, playlist.Id);
    }
    
    private async Task Add100ItemsToPlaylist(List<string> itemsToAdd, string weebletdaysSelectedPlaylistId)
    {
        // More lazy rate-limiting
        await Task.Delay(2000);
        
        // Otherwise, since we can only add 100 at a time:
        var itemsToAddRequest = new PlaylistAddItemsRequest(itemsToAdd);
    
        await spotifyClient.Playlists.AddItems(weebletdaysSelectedPlaylistId, itemsToAddRequest);
    
        itemsToAdd.Clear();
    }
    
    private async Task Remove100ItemsFrom(ManagedPlaylist playlist, List<string> itemsToRemove)
    {
        // More lazy rate-limiting
        await Task.Delay(2000);
            
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
        
        await spotifyClient.Playlists.RemoveItems(playlist.Id, itemsToRemoveRequest);
    
        itemsToRemove.Clear();
    }
}