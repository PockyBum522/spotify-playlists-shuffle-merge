using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Serilog;
using SpotifyAPI.Web;
using SpotifyPlaylistUtility.Models;

namespace SpotifyPlaylistUtility.Logic.Spotify.Playlists;

public class PlaylistShuffler
{
    private readonly List<string> _mergeShuffleAlreadyAddedTracks = new();

    //private int _numberOfTracksToPutIntoDestinationPlaylist = 0;

    private readonly ILogger _logger;
    private readonly SpotifyClient _spotifyClient;
    private readonly PlaylistManager _playlistManager;

    public PlaylistShuffler(ILogger logger, SpotifyClient spotifyClient)
    {
        _logger = logger;
        _spotifyClient = spotifyClient;

        _playlistManager = new PlaylistManager(_logger, _spotifyClient);
    }
    
    public async Task ShuffleAllIn(string playlistId)
    {
        var allTracks = 
            await _playlistManager.GetAllPlaylistTracks(playlistId);
        
        // Lazy rate-limiting
        await Task.Delay(5000);
        
        _playlistManager.BackupTracksToJsonFile(playlistId, allTracks);
        
        await _playlistManager.RemoveTracksFrom(playlistId, allTracks);
        
        await Task.Delay(5000);

        var allTracksShuffled = GetRandomTracksWithDuplicates(allTracks);
        
        await AddTracksToPlaylist(playlistId, allTracksShuffled);
    }
    
    private List<PlaylistTrack<IPlayableItem>> GetRandomTracksWithDuplicates(List<PlaylistTrack<IPlayableItem>> tracks)
    {
        var initialTracks = new List<ShuffleablePlaylistTrack>();

        foreach (var track in tracks)
        {
            initialTracks.Add(new ShuffleablePlaylistTrack(track));
        }
        
        var shuffledTracks = initialTracks.OrderBy(c => c.RandomShuffleNumber).ToList();

        var returnTracks = new List<PlaylistTrack<IPlayableItem>>();

        foreach (var shuffledTrack in shuffledTracks)
        {
            returnTracks.Add(shuffledTrack.Track);
        }

        return returnTracks;
    }
    
    private async Task AddTracksToPlaylist(string playlistId, List<PlaylistTrack<IPlayableItem>> tracks)
    {
        var itemsToAdd = new List<string>();
        
        foreach (var randomSelectedTrack in tracks)
        {
            var fullTrackConverted = randomSelectedTrack.Track as FullTrack;

            _logger.Information($"Attempting to add: {fullTrackConverted?.Name}");

            itemsToAdd.Add(fullTrackConverted?.Uri ?? "");

            if (itemsToAdd.Count < 100) continue;
            
            // If we hit capacity, add and clear
            await Add100ItemsToPlaylist(itemsToAdd, playlistId);
        }
        
        // Add any remaining
        if (itemsToAdd.Count > 0)
            await Add100ItemsToPlaylist(itemsToAdd, playlistId);
    }

    private async Task Add100ItemsToPlaylist(List<string> itemsToAdd, string weebletdaysSelectedPlaylistId)
    {
        // Otherwise, since we can only add 100 at a time:
        var itemsToAddRequest = new PlaylistAddItemsRequest(itemsToAdd);

        await _spotifyClient.Playlists.AddItems(weebletdaysSelectedPlaylistId, itemsToAddRequest);

        // More lazy rate-limiting
        await Task.Delay(1000);

        itemsToAdd.Clear();
    }
    
    
    
    
    
    
    
    
    // public async Task MergeShufflePlaylists(string[] sourcePlaylistIds, string destinationPlaylistId, int numberOfSongsToAddToDestination)
    // {
    //     var clinet = GetAuthenticatedSpotifyClient();
    //
    //     _mergeShuffleAlreadyAddedTracks.Clear();
    //     
    //     await ShowUserAuthenticatedMessage(spotifyClient);
    //
    //     var playlists = await spotifyClient.PaginateAll(await spotifyClient.Playlists.CurrentUsers().ConfigureAwait(false));
    //     ListAllPlaylistsWithId(playlists);
    //
    //     await Task.Delay(5000);
    //
    //     await RemoveTracksFrom(spotifyClient);
    //     
    //     var allCuratedWeebletdaysTracks = await GetAllPlaylistTracks(SECRETS.PLAYLIST_ID_CURATED_WEEBLETDAYS, spotifyClient);
    //     var allSelectSelectionsTracks = await GetAllPlaylistTracks(SECRETS.PLAYLIST_ID_SELECT_SELECTIONS, spotifyClient);
    //     
    //     var selectSelections150 = GetRandomUniqueTracks(allSelectSelectionsTracks, _numberOfTracksToPutIntoDestinationPlaylist / 2);
    //     var curatedWeebletdays150 = GetRandomUniqueTracks(allCuratedWeebletdaysTracks, _numberOfTracksToPutIntoDestinationPlaylist / 2);
    //     
    //     var allTracksTotal = new List<PlaylistTrack<IPlayableItem>>();
    //     
    //     allTracksTotal.AddRange(curatedWeebletdays150);
    //     allTracksTotal.AddRange(selectSelections150);
    //     
    //     await AddRandomTracksToNewWeebletdaysSelectPlaylist(spotifyClient, allTracksTotal);
    // }

    


    // private static async Task AddRandomTracksToNewWeebletdaysSelectPlaylist(SpotifyClient spotifyClient, List<PlaylistTrack<IPlayableItem>> randomSelectedTracks)
    // {
    //     //var shortDate = DateTimeOffset.Now.ToString("M/d");
    //
    //     // If you need to re-create weebletdays select daily playlist:
    //     // var weebletdaysSelectedPlaylist = await spotifyClient.Playlists.Create(
    //     //     (await spotifyClient.UserProfile.Current()).Id, new PlaylistCreateRequest("Weebletdays Select Daily"));
    //
    //     var itemsToAdd = new List<string>();
    //     
    //     foreach (var randomSelectedTrack in randomSelectedTracks)
    //     {
    //         var fullTrackConverted = randomSelectedTrack.Track as FullTrack;
    //
    //         _logger.Information($"Attempting to add: {fullTrackConverted?.Name}");
    //
    //         itemsToAdd.Add(fullTrackConverted?.Uri ?? "");
    //
    //         if (itemsToAdd.Count < 100) continue;
    //         
    //         // If we hit capacity, add and clear
    //         await Add100ItemsToPlaylist(spotifyClient, itemsToAdd, SECRETS.PLAYLIST_ID_SELECT_DAILY);
    //     }
    //     
    //     // Add any remaining
    //     if (itemsToAdd.Count > 0)
    //         await Add100ItemsToPlaylist(spotifyClient, itemsToAdd, SECRETS.PLAYLIST_ID_SELECT_DAILY);
    // }
    //
    // private static List<PlaylistTrack<IPlayableItem>> GetRandomUniqueTracks(List<PlaylistTrack<IPlayableItem>> tracks, int numberToGet)
    // {
    //     if (tracks.Count < numberToGet)
    //         numberToGet = tracks.Count;
    //     
    //     var returnTracks = new List<PlaylistTrack<IPlayableItem>>();
    //     
    //     var random = new Random();
    //
    //     var timeoutCounter = 20000; // Attempts to get unique tracks before we decide we're in an infinite loop and break
    //     
    //     while (numberToGet-- > 0)
    //     {
    //         // Get random track number
    //         var trackIndexToAdd = random.Next(tracks.Count);
    //
    //         var trackToAdd = tracks[trackIndexToAdd].Track as FullTrack;
    //
    //         // Make sure we haven't already used this track
    //         while (timeoutCounter-- > 0 &&
    //                _mergeShuffleAlreadyAddedTracks.Contains(trackToAdd?.Id ?? ""))
    //         {
    //             trackIndexToAdd = random.Next(tracks.Count);
    //             
    //             trackToAdd = tracks[trackIndexToAdd].Track as FullTrack;
    //         }
    //         
    //         _mergeShuffleAlreadyAddedTracks.Add(trackToAdd?.Id ?? "");
    //         
    //         returnTracks.Add(
    //             tracks[trackIndexToAdd]);
    //     }
    //
    //     return returnTracks;
    // }
    //
    //
    //
    //
    //
    // private static void ListAllPlaylistsWithId(IList<FullPlaylist> userPlaylists)
    // {
    //     _logger.Information($"Total Playlists in your Account: {userPlaylists.Count}");
    //
    //     var playlistIndex = 1;
    //     
    //     foreach (var playlist in userPlaylists)
    //     {
    //         _logger.Information();
    //         _logger.Information($"Playlist [ #{playlistIndex++} ]");
    //         _logger.Information($"{playlist.Name} | ID: {playlist.Id}");
    //     }
    //
    //     _logger.Information();
    // }
    
    
}