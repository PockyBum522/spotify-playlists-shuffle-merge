using Serilog;
using SpotifyAPI.Web;

namespace SpotifyPlaylistUtilities.Models;

public class ManagedPlaylist(ILogger logger, SpotifyClient spotifyClient, FullPlaylist nativePlaylist)
{
    public string Name { get; set; } = nativePlaylist.Name ?? "ERROR GETTING PLAYLIST NAME";
    public string Id { get; set; } = nativePlaylist.Id ?? "ERROR GETTING PLAYLIST ID";
    public List<ManagedPlaylistTrack> FetchedTracks => GetCachedTracks();
    
    private List<ManagedPlaylistTrack>? _fetchedTracks;
    
    private List<ManagedPlaylistTrack> GetCachedTracks()
    {
        if (_fetchedTracks is null) throw new Exception("You must call FetchAllTracks() before using this property.");

        return _fetchedTracks;
    }
    
    public async Task FetchAllTracks()
    {
        _fetchedTracks ??= new List<ManagedPlaylistTrack>();
        
        _fetchedTracks.Clear();
        
        var playlistId = nativePlaylist.Id ?? "ERROR GETTING PLAYLIST ID";
        
        var convertedPlaylist = await spotifyClient.Playlists.Get(playlistId);
    
        if (convertedPlaylist.Tracks is not null)
        {
            var allTracks = await spotifyClient.PaginateAll(convertedPlaylist.Tracks);
            
            var tracksCount = 0;
            
            foreach (var playlistTrack in allTracks)
            {
                if (playlistTrack.Track is not FullTrack track) continue;

                var convertedTrack = (FullTrack)playlistTrack.Track;
                
                // All FullTrack properties are available
                var artistString = track.Artists.First().Name;
            
                logger.Debug("OriginalTrack: #{TrackNumber}: {ArtistString} - {TrackName} | ID: {Id}", tracksCount++, artistString, track.Name, track.Id);
            
                _fetchedTracks.Add(
                    new ManagedPlaylistTrack(convertedTrack));
            }
            
            logger.Information("For playlist: {PlaylistName} got {TrackCount} tracks", nativePlaylist.Name, _fetchedTracks.Count);
    
            // Lazy rate-limiting (not really, but at least between tasks) 
            await Task.Delay(2000);
        }
        else
        {
            throw new Exception(
                $"Attempted to get all playlist tracks for playlist: {Name} (ID: {playlistId}) but got back null playlist");    
        }
    }
}