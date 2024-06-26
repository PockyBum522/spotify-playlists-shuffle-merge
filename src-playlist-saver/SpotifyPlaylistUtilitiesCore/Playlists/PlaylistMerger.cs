﻿using Serilog;
using SpotifyAPI.Web;

namespace SpotifyPlaylistUtilities.Playlists;

public class PlaylistMerger
{
    private readonly List<string> _mergeShuffleAlreadyAddedTracks = new();

    private readonly ILogger _logger;
    private readonly SpotifyClient _spotifyClient;
    private readonly PlaylistManager _playlistManager;

    public PlaylistMerger(ILogger logger, SpotifyClient spotifyClient)
    {
        _logger = logger;
        _spotifyClient = spotifyClient;

        _playlistManager = new PlaylistManager(_logger, spotifyClient: _spotifyClient);
    }
    
    // /// <summary>
    // /// Randomly pulls destinationPlaylistTracksCount / sourcePlaylistIds.Count tracks from each source playlist.
    // ///
    // /// Drops destinationPlaylistTracksCount tracks into 
    // /// </summary>
    // /// <param name="sourcePlaylistIds"></param>
    // /// <param name="destinationPlaylistId"></param>
    // /// <param name="destinationPlaylistTracksCount"></param>
    // public async Task Merge(List<string> sourcePlaylistIds, string destinationPlaylistId, int destinationPlaylistTracksCount)
    // {
    //     var playlists = new List<List<PlaylistTrack<IPlayableItem>>>();
    //     
    //     foreach (var playlistId in sourcePlaylistIds)
    //     {
    //         playlists.Add(await _playlistManager.GetAllPlaylistTracks(playlistId));
    //
    //         await Task.Delay(5000);
    //     }
    //     
    //     playlists = playlists.OrderBy(p => p.Count).ToList();
    //
    //     var songsPerPlaylist = destinationPlaylistTracksCount / playlists.Count;
    //
    //     throw new NotImplementedException();
    //
    //     // foreach (var playlist in playlists)
    //     // {
    //     //     _mergeShuffleAlreadyAddedTracks.Add();
    //     //
    //     //
    //     //     // Lazy rate-limiting
    //     //     await Task.Delay(5000);
    //     //
    //     //     _playlistManager.BackupTracksToJsonFile(playlistId, allTracks);
    //     // }
    //     //
    //     // await _playlistManager.RemoveTracksFrom(destinationPlaylistId, allTracks);
    //     //
    //     // await Task.Delay(5000);
    //     //
    //     // var allTracksShuffled = GetRandomUniqueTracks(allTracks);
    //     //
    //     // await AddTracksToPlaylist(playlistId, allTracksShuffled);
    // }
    //
    // private List<PlaylistTrack<IPlayableItem>> GetRandomUniqueTracks(List<PlaylistTrack<IPlayableItem>> tracks)
    // {
    //     throw new NotImplementedException("Not implemented to get unique tracks");
    //     
    //     var initialTracks = new List<ManagedPlaylistTrack>();
    //
    //     foreach (var track in tracks)
    //     {
    //         initialTracks.Add(new ManagedPlaylistTrack(track));
    //     }
    //     
    //     var shuffledTracks = initialTracks.OrderBy(c => c.RandomShuffleNumber).ToList();
    //
    //     var returnTracks = new List<PlaylistTrack<IPlayableItem>>();
    //
    //     foreach (var shuffledTrack in shuffledTracks)
    //     {
    //         returnTracks.Add(shuffledTrack.OriginalTrack);
    //     }
    //
    //     return returnTracks;
    // }
    //
    // private async Task AddTracksToPlaylist(string playlistId, List<PlaylistTrack<IPlayableItem>> tracks)
    // {
    //     var itemsToAdd = new List<string>();
    //     
    //     foreach (var randomSelectedTrack in tracks)
    //     {
    //         var fullTrackConverted = randomSelectedTrack.OriginalTrack as FullTrack;
    //
    //         _logger.Information($"Attempting to add: {fullTrackConverted?.Name}");
    //
    //         itemsToAdd.Add(fullTrackConverted?.Uri ?? "");
    //
    //         if (itemsToAdd.Count < 100) continue;
    //         
    //         // If we hit capacity, add and clear
    //         await Add100ItemsToPlaylist(itemsToAdd, playlistId);
    //     }
    //     
    //     // Add any remaining
    //     if (itemsToAdd.Count > 0)
    //         await Add100ItemsToPlaylist(itemsToAdd, playlistId);
    // }
    //
    // private async Task Add100ItemsToPlaylist(List<string> itemsToAdd, string weebletdaysSelectedPlaylistId)
    // {
    //     // Otherwise, since we can only add 100 at a time:
    //     var itemsToAddRequest = new PlaylistAddItemsRequest(itemsToAdd);
    //
    //     await spotifyClient.Playlists.AddItems(weebletdaysSelectedPlaylistId, itemsToAddRequest);
    //
    //     // More lazy rate-limiting
    //     await Task.Delay(1000);
    //
    //     itemsToAdd.Clear();
    // }
    //
    //
    //
    //
    //
    //
    //
    //
    // // public async Task MergeShufflePlaylists(string[] sourcePlaylistIds, string destinationPlaylistId, int numberOfSongsToAddToDestination)
    // // {
    // //     var clinet = GetAuthenticatedSpotifyClient();
    // //
    // //     _mergeShuffleAlreadyAddedTracks.Clear();
    // //     
    // //     await ShowUserAuthenticatedMessage(spotifyClient);
    // //
    // //     var playlists = await spotifyClient.PaginateAll(await spotifyClient.Playlists.CurrentUsers().ConfigureAwait(false));
    // //     ListAllPlaylistsWithId(playlists);
    // //
    // //     await Task.Delay(5000);
    // //
    // //     await RemoveTracksFrom(spotifyClient);
    // //     
    // //     var allCuratedWeebletdaysTracks = await GetAllPlaylistTracks(SECRETS.PLAYLIST_ID_CURATED_WEEBLETDAYS, spotifyClient);
    // //     var allSelectSelectionsTracks = await GetAllPlaylistTracks(SECRETS.PLAYLIST_ID_SELECT_SELECTIONS, spotifyClient);
    // //     
    // //     var selectSelections150 = GetRandomUniqueTracks(allSelectSelectionsTracks, _numberOfTracksToPutIntoDestinationPlaylist / 2);
    // //     var curatedWeebletdays150 = GetRandomUniqueTracks(allCuratedWeebletdaysTracks, _numberOfTracksToPutIntoDestinationPlaylist / 2);
    // //     
    // //     var allTracksTotal = new List<PlaylistTrack<IPlayableItem>>();
    // //     
    // //     allTracksTotal.AddRange(curatedWeebletdays150);
    // //     allTracksTotal.AddRange(selectSelections150);
    // //     
    // //     await AddRandomTracksToNewWeebletdaysSelectPlaylist(spotifyClient, allTracksTotal);
    // // }
    //
    //
    //
    //
    // // private static async Task AddRandomTracksToNewWeebletdaysSelectPlaylist(SpotifyClient spotifyClient, List<PlaylistTrack<IPlayableItem>> randomSelectedTracks)
    // // {
    // //     //var shortDate = DateTimeOffset.Now.ToString("M/d");
    // //
    // //     // If you need to re-create weebletdays select daily playlist:
    // //     // var weebletdaysSelectedPlaylist = await spotifyClient.Playlists.Create(
    // //     //     (await spotifyClient.UserProfile.Current()).Id, new PlaylistCreateRequest("Weebletdays Select Daily"));
    // //
    // //     var itemsToAdd = new List<string>();
    // //     
    // //     foreach (var randomSelectedTrack in randomSelectedTracks)
    // //     {
    // //         var fullTrackConverted = randomSelectedTrack.OriginalTrack as FullTrack;
    // //
    // //         _logger.Information($"Attempting to add: {fullTrackConverted?.Name}");
    // //
    // //         itemsToAdd.Add(fullTrackConverted?.Uri ?? "");
    // //
    // //         if (itemsToAdd.Count < 100) continue;
    // //         
    // //         // If we hit capacity, add and clear
    // //         await Add100ItemsToPlaylist(spotifyClient, itemsToAdd, SECRETS.PLAYLIST_ID_SELECT_DAILY);
    // //     }
    // //     
    // //     // Add any remaining
    // //     if (itemsToAdd.Count > 0)
    // //         await Add100ItemsToPlaylist(spotifyClient, itemsToAdd, SECRETS.PLAYLIST_ID_SELECT_DAILY);
    // // }
    // //
    // // private static List<PlaylistTrack<IPlayableItem>> GetRandomUniqueTracks(List<PlaylistTrack<IPlayableItem>> tracks, int numberToGet)
    // // {
    // //     if (tracks.Count < numberToGet)
    // //         numberToGet = tracks.Count;
    // //     
    // //     var returnTracks = new List<PlaylistTrack<IPlayableItem>>();
    // //     
    // //     var random = new Random();
    // //
    // //     var timeoutCounter = 20000; // Attempts to get unique tracks before we decide we're in an infinite loop and break
    // //     
    // //     while (numberToGet-- > 0)
    // //     {
    // //         // Get random track number
    // //         var trackIndexToAdd = random.Next(tracks.Count);
    // //
    // //         var trackToAdd = tracks[trackIndexToAdd].OriginalTrack as FullTrack;
    // //
    // //         // Make sure we haven't already used this track
    // //         while (timeoutCounter-- > 0 &&
    // //                _mergeShuffleAlreadyAddedTracks.Contains(trackToAdd?.Id ?? ""))
    // //         {
    // //             trackIndexToAdd = random.Next(tracks.Count);
    // //             
    // //             trackToAdd = tracks[trackIndexToAdd].OriginalTrack as FullTrack;
    // //         }
    // //         
    // //         _mergeShuffleAlreadyAddedTracks.Add(trackToAdd?.Id ?? "");
    // //         
    // //         returnTracks.Add(
    // //             tracks[trackIndexToAdd]);
    // //     }
    // //
    // //     return returnTracks;
    // // }
    // //
    // //
    // //
    // //
    // //
    // // private static void ListAllPlaylistsWithId(IList<FullPlaylist> userPlaylists)
    // // {
    // //     _logger.Information($"Total Playlists in your Account: {userPlaylists.Count}");
    // //
    // //     var playlistIndex = 1;
    // //     
    // //     foreach (var playlist in userPlaylists)
    // //     {
    // //         _logger.Information();
    // //         _logger.Information($"Playlist [ #{playlistIndex++} ]");
    // //         _logger.Information($"{playlist.Name} | ID: {playlist.Id}");
    // //     }
    // //
    // //     _logger.Information();
    // // }
    //
    
}