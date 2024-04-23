using Newtonsoft.Json;
using Serilog;
using SpotifyAPI.Web;
using SpotifyPlaylistUtilities.Models;

namespace SpotifyPlaylistUtilities.Playlists;

public class PlaylistShuffler
{
    private Random _random = new();
    
    private readonly ILogger _logger;
    private readonly SpotifyClient _spotifyClient;
    private readonly PlaylistManager _playlistManager;

    public PlaylistShuffler(ILogger logger, SpotifyClient spotifyClient)
    {
        _logger = logger;
        _spotifyClient = spotifyClient;

        _playlistManager = new PlaylistManager(_logger, spotifyClient: _spotifyClient);
    }

    /// <summary>
    /// Gets all tracks in a playlist, backs them up, removes them, then re-adds them all in a random order
    /// </summary>
    /// <param name="playlist">ManagedPlaylist to shuffle all tracks in</param>
    /// <param name="allowDuplicates">Whether to allow duplicate tracks in the shuffled list of tracks, if the original playlist had duplicates</param>
    /// <param name="backupPlaylist">Whether to backup the playlist tracks to disk, true by default, highly recommended</param>
    public async Task ShuffleAllIn(ManagedPlaylist playlist, bool allowDuplicates, bool backupPlaylist = true)
    {
        if (backupPlaylist)
            _playlistManager.BackupTracksToJsonFile(playlist);
        
        await _playlistManager.DeleteAllPlaylistTracksOnSpotify(playlist);
        
        // Lazy rate-limiting
        await Task.Delay(2000);
        
        var allTracksShuffled = RandomizeTracksOrder(playlist.FetchedTracks, allowDuplicates);
        
        await _playlistManager.AddTracksToPlaylistOnSpotify(playlist, allTracksShuffled);
    }
    
    /// <summary>
    /// Get random tracks from a playlist without taking weights of picked tracks into account
    /// </summary>
    /// <param name="playlist">ManagedPlaylist to get random tracks from</param>
    /// <param name="numberOfTracksToGet">How many random tracks to get</param>
    /// <param name="allowDuplicates">If the playlist has duplicates, should method be able to return duplicates</param>
    /// <param name="backupPlaylist">Backup the playlist to JSON, highly recommended, true by default</param>
    /// <returns></returns>
    public async Task<List<ManagedPlaylistTrack>> GetRandomTracksFrom(ManagedPlaylist playlist, int numberOfTracksToGet, bool allowDuplicates = false, bool backupPlaylist = true)
    {
        if (backupPlaylist)
            _playlistManager.BackupTracksToJsonFile(playlist);
        
        // Lazy rate-limiting
        await Task.Delay(2000);
        
        var allTracksShuffled = RandomizeTracksOrder(playlist.FetchedTracks, allowDuplicates);

        if (numberOfTracksToGet >= allTracksShuffled.Count)
            return allTracksShuffled;
        
        var returnTracks = new List<ManagedPlaylistTrack>();

        for (var i = 0; i < numberOfTracksToGet; i++)
        {
            returnTracks.Add(allTracksShuffled[i]);
        }

        return returnTracks;
    }
    
    /// <summary>
    /// Get random tracks from a playlist, taking weights of past picked tracks into account
    /// </summary>
    /// <param name="playlist">ManagedPlaylist to get random tracks from</param>
    /// <param name="numberOfTracksToGet">How many random tracks to get</param>
    /// <param name="allowDuplicates">If the playlist has duplicates, should method be able to return duplicates</param>
    /// <param name="backupPlaylist">Backup the playlist to JSON, highly recommended, true by default</param>
    /// <returns></returns>
    public async Task<List<ManagedPlaylistTrack>> GetRandomTracksWithPickWeightsFrom(ManagedPlaylist playlist, int numberOfTracksToGet, bool allowDuplicates = false, bool backupPlaylist = true)
    {
        if (backupPlaylist)
            _playlistManager.BackupTracksToJsonFile(playlist);
        
        // Lazy rate-limiting
        await Task.Delay(2000);

        var existingSavedPickWeightsList = LoadSavedTrackWeights();
        
        var allTracksShuffled = RandomizeTracksOrder(playlist.FetchedTracks, allowDuplicates);

        if (numberOfTracksToGet >= allTracksShuffled.Count)
            return allTracksShuffled;
        
        var returnTracks = new List<ManagedPlaylistTrack>();

        var currentTrackIndex = -1;
        
        while (returnTracks.Count < numberOfTracksToGet)
        {
            currentTrackIndex++;
            
            var currentTrack = allTracksShuffled[currentTrackIndex];
            
            if (!TrackWeightDataExistsIn(existingSavedPickWeightsList, currentTrack.Id))
            {
                _logger.Debug("Data for {CurrentTrackName} doesn't exist in track weight data JSON file, so adding {StillCurrentTrackName} without rolling",currentTrack.Name, currentTrack.Name );
                
                returnTracks.Add(currentTrack);
                
                continue;
            }
            
            // Otherwise, if we have existing data, roll die:
            var trackWeight = GetExistingTrackWeight(existingSavedPickWeightsList, currentTrack);

            var roll = _random.NextDouble();
            
            _logger.Debug("Data for {CurrentTrackName} exists with weight of {TrackWeight}, so rolling die: {DieRoll}", currentTrack.Name, trackWeight, roll);
            
            // Only add if roll > pickweight
            if (roll > trackWeight)
            {
                _logger.Debug("Roll: {DieRoll} > {TrackWeight}, so adding track {TrackName}", roll, trackWeight, currentTrack.Name);
                
                returnTracks.Add(currentTrack);
            }
        }

        return returnTracks;
    }
    
    public void DecrementPickWeightsForAllSavedTracks()
    {
        var existingSavedTrackWeights = LoadSavedTrackWeights();

        for (var i = 0; i < existingSavedTrackWeights.Count; i++)
        {
            var trackToDecrement = existingSavedTrackWeights[i];
            
            trackToDecrement.PickWeight -= 0.02;

            _logger.Debug(
                "Decrementing track weight of {TrackId}, is now: {NewWeight}", trackToDecrement.Id, trackToDecrement.PickWeight);
        }
        
        var json = JsonConvert.SerializeObject(existingSavedTrackWeights);
        
        File.WriteAllText(AppPaths.SavedTrackJsonPath, json);
    }
    
    private double GetExistingTrackWeight(List<SerializableWeightedTrack> existingSavedPickWeightsList, ManagedPlaylistTrack queryTrack)
    {
        foreach (var existingTrackData in existingSavedPickWeightsList)
        {
            if (existingTrackData.Id == queryTrack.Id)
                return existingTrackData.PickWeight;
        }

        return 0.0;
    }

    private bool TrackWeightDataExistsIn(List<SerializableWeightedTrack> existingSavedPickWeightsList, string managedPlaylistTrackId)
    {
        foreach (var track in existingSavedPickWeightsList)
        {
            if (track.Id == managedPlaylistTrackId)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Increments PickWeights of tracks in the supplied list, and then saves them to disk
    /// </summary>
    /// <param name="tracksToIncrementWeightOf">List of ManagedTracks to increment PickWeights for</param>
    public void IncrementPickWeightsForTracks(List<ManagedPlaylistTrack> tracksToIncrementWeightOf)
    {
        var existingSavedTrackWeights = LoadSavedTrackWeights();

        foreach (var trackToIncrement in tracksToIncrementWeightOf)
        {
            if (TrackWeightDataExistsIn(existingSavedTrackWeights, trackToIncrement.Id))
            {
                IncrementTrackWeightIn(existingSavedTrackWeights, trackToIncrement.Id);
                
                continue;
            }
            
            // Otherwise, if not already in existing tracks list:
            trackToIncrement.PickWeight += 0.2;

            var trackToAdd = new SerializableWeightedTrack();

            trackToAdd.PickWeight = 0.2;
            trackToAdd.Id = trackToIncrement.Id;
            
            existingSavedTrackWeights.Add(trackToAdd);
            
            _logger.Debug("No existing track weight data for ID: {TrackId}, created new SerializableWeightedTrack and adding to full list", trackToAdd.Id);
        }
        
        _logger.Debug("About to write full list of track Ids and PickWeights to {Path}", AppPaths.SavedTrackJsonPath);
        
        var json = JsonConvert.SerializeObject(existingSavedTrackWeights);
        
        File.WriteAllText(AppPaths.SavedTrackJsonPath, json);
    }
    
    private List<SerializableWeightedTrack> LoadSavedTrackWeights()
    {
        _logger.Debug("Loading existing track weights data");
        
        Directory.CreateDirectory(
            Path.GetDirectoryName(AppPaths.SavedTrackJsonPath) ?? throw new ArgumentException("Could not get AppPaths.SavedTrackJsonPath"));

        if (!File.Exists(AppPaths.SavedTrackJsonPath))
            return new List<SerializableWeightedTrack>();
        
        var fileJson = File.ReadAllText(AppPaths.SavedTrackJsonPath);
        
        var weightedTracks = JsonConvert.DeserializeObject<List<SerializableWeightedTrack>>(fileJson) ?? new List<SerializableWeightedTrack>();

        foreach (var weightedTrackData in weightedTracks)
        {
            _logger.Debug("Existing track weight data - ID: {TrackId}, PickWeight: {TrackWeight}", weightedTrackData.Id, weightedTrackData.PickWeight);   
        }
        
        return weightedTracks;
    }
    
    private void IncrementTrackWeightIn(List<SerializableWeightedTrack> tracksList, string trackIdToIncrement)
    {
        for (var i = 0; i < tracksList.Count; i++)
        {
            if (tracksList[i].Id == trackIdToIncrement)
            {
                tracksList[i].PickWeight += 0.2;
                
                _logger.Debug("Incremented track weight for ID: {TrackId} and new weight after increment: {NewWeight}", tracksList[i].Id, tracksList[i].PickWeight );
            }
        }
    }

    // private bool TrackExistsIn(List<ManagedPlaylistTrack> tracksToCheck, string trackIdToMatch)
    // {
    //     _logger.Debug("Checking if track with ID: {TrackId} has existing weight data", trackIdToMatch );
    //
    //     if (tracksToCheck.Count < 1)
    //         return false;
    //     
    //     foreach (var track in tracksToCheck)
    //     {
    //         if (track.Id == trackIdToMatch)
    //             return true;
    //     }
    //
    //     return false;
    // }

    private List<ManagedPlaylistTrack> RandomizeTracksOrder(List<ManagedPlaylistTrack> tracks, bool allowDuplicates)
    {
        var returnTracks = new List<ManagedPlaylistTrack>();

        foreach (var track in tracks)
        {
            returnTracks.Add(track);
        }
        
        if (!allowDuplicates)
            returnTracks = returnTracks
                .GroupBy(x => new {Id = x.Id})
                .Select(grp => grp.First())
                .ToList();
        
        foreach (var track in returnTracks)
        {
            track.RerandomizeShuffleNumber();
        }
        
        returnTracks = returnTracks.OrderBy(c => c.RandomShuffleNumber).ToList();
    
        return returnTracks;
    }
    
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
}