using Newtonsoft.Json;
using SpotifyAPI.Web;
using SpotifyAPI.Web.Auth;

namespace SpotifyPlaylistUtilities;

/// <summary>
///   This is a basic example how to get user access using the Auth package and a CLI Program
///   Your spotify app needs to have http://localhost:5543 as redirect uri whitelisted
/// </summary>
public static class Program
{
    private const string CredentialsPath = "credentials.json";

    private static readonly EmbedIOAuthServer Server = new(new Uri("http://localhost:5543/callback"), 5543);

    private static readonly List<string> AlreadyAddedTracks = new();

    private static void Exiting()
    {
        Console.CursorVisible = true;
    }

    public static async Task<int> Main()
    {
        var runTodayFlag = false;
        
        while (true)
        {
            Console.WriteLine($"Checking time. Is currently hour: {DateTimeOffset.Now.Hour} and runTodayFlag is: {runTodayFlag}");
            
            // Check at 3am if we've run yet today
            if (DateTimeOffset.Now.Hour == 3 &&
                !runTodayFlag)
            {
                runTodayFlag = true;
                
                await CheckAuthenticationAndRun();
            }

            // Reset run yet flag at 4am
            if (DateTimeOffset.Now.Hour == 4 &&
                runTodayFlag)
            {
                runTodayFlag = false;
            }

            await Task.Delay(
                TimeSpan.FromMinutes(20));
        }
        
        // ReSharper disable once FunctionNeverReturns because it's not supposed to
    }

    private static async Task CheckAuthenticationAndRun()
    {
        // This is a bug in the SWAN Logging library, need this hack to bring back the cursor
        AppDomain.CurrentDomain.ProcessExit += (_, _) => Exiting();

        if (string.IsNullOrEmpty(SECRETS.SPOTIFY_CLIENT_ID))
        {
            throw new NullReferenceException(
                "Please set SPOTIFY_CLIENT_ID via SECRETS.cs before starting the program"
            );
        }

        if (File.Exists(CredentialsPath))
        {
            await RefreshSelectDailyPlaylist();
        }
        else
        {
            await StartAuthentication();
        }
    }

    private static async Task RefreshSelectDailyPlaylist()
    {
        var json = await File.ReadAllTextAsync(CredentialsPath);
        var token = JsonConvert.DeserializeObject<PKCETokenResponse>(json);

        var authenticator = new PKCEAuthenticator(SECRETS.SPOTIFY_CLIENT_ID!, token!);
        authenticator.TokenRefreshed += (_, refreshedToken) => File.WriteAllText(CredentialsPath, JsonConvert.SerializeObject(refreshedToken));

        var config = SpotifyClientConfig.CreateDefault()
            .WithAuthenticator(authenticator);

        var spotifyClient = new SpotifyClient(config);

        AlreadyAddedTracks.Clear();
        
        await ShowUserAuthenticatedMessage(spotifyClient);

        var playlists = await spotifyClient.PaginateAll(await spotifyClient.Playlists.CurrentUsers().ConfigureAwait(false));
        ListAllPlaylistsWithId(playlists);

        await RemoveAllTracksFromWeebletdaysSelectDaily(spotifyClient);
        
        var allCuratedWeebletdaysTracks = await GetAllPlaylistTracks(SECRETS.PLAYLIST_ID_CURATED_WEEBLETDAYS, spotifyClient);
        var allSelectSelectionsTracks = await GetAllPlaylistTracks(SECRETS.PLAYLIST_ID_SELECT_SELECTIONS, spotifyClient);
        
        var curatedWeebletdays150 = GetRandomTracks(allCuratedWeebletdaysTracks, 150);
        var selectSelections150 = GetRandomTracks(allSelectSelectionsTracks, 150);
        
        var allTracksTotal = new List<PlaylistTrack<IPlayableItem>>();
        
        allTracksTotal.AddRange(curatedWeebletdays150);
        allTracksTotal.AddRange(selectSelections150);
        
        await AddRandomTracksToNewWeebletdaysSelectPlaylist(spotifyClient, allTracksTotal);
    }

    private static async Task RemoveAllTracksFromWeebletdaysSelectDaily(SpotifyClient spotifyClient)
    {
        // Weebletdays Select Daily | ID: 6QQUbYHQyaiaYbK7BC56Il   
        var allTracks = await GetAllPlaylistTracks("", spotifyClient);
      
        var itemsToRemove = new List<string>();
        
        foreach (var trackToRemove in allTracks)
        {
            var fullTrackConverted = trackToRemove.Track as FullTrack;

            Console.WriteLine($"Attempting to remove: {fullTrackConverted?.Name}");

            itemsToRemove.Add(fullTrackConverted?.Uri ?? "");

            if (itemsToRemove.Count < 100) continue;
            
            // If we hit capacity, add and clear
            await Remove100ItemsFromWeebletdaysSelectDaily(spotifyClient, itemsToRemove);
        }
         
        if (itemsToRemove.Count > 0)
            await Remove100ItemsFromWeebletdaysSelectDaily(spotifyClient, itemsToRemove);
    }

    private static async Task Remove100ItemsFromWeebletdaysSelectDaily(SpotifyClient spotifyClient, List<string> itemsToRemove)
    {
        // Otherwise, since we can only add 100 at a time:
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

        await spotifyClient.Playlists.RemoveItems(SECRETS.PLAYLIST_ID_SELECT_DAILY, itemsToRemoveRequest);

        itemsToRemove.Clear();
    }

    private static async Task AddRandomTracksToNewWeebletdaysSelectPlaylist(SpotifyClient spotifyClient, List<PlaylistTrack<IPlayableItem>> randomSelectedTracks)
    {
        //var shortDate = DateTimeOffset.Now.ToString("M/d");

        // If you need to re-create weebletdays select daily playlist:
        // var weebletdaysSelectedPlaylist = await spotifyClient.Playlists.Create(
        //     (await spotifyClient.UserProfile.Current()).Id, new PlaylistCreateRequest("Weebletdays Select Daily"));

        var itemsToAdd = new List<string>();
        
        foreach (var randomSelectedTrack in randomSelectedTracks)
        {
            var fullTrackConverted = randomSelectedTrack.Track as FullTrack;

            Console.WriteLine($"Attempting to add: {fullTrackConverted?.Name}");

            itemsToAdd.Add(fullTrackConverted?.Uri ?? "");

            if (itemsToAdd.Count < 100) continue;
            
            // If we hit capacity, add and clear
            await Add100ItemsToPlaylist(spotifyClient, itemsToAdd, SECRETS.PLAYLIST_ID_SELECT_DAILY);
        }
        
        // Add any remaining
        if (itemsToAdd.Count > 0)
            await Add100ItemsToPlaylist(spotifyClient, itemsToAdd, SECRETS.PLAYLIST_ID_SELECT_DAILY);
    }

    private static async Task Add100ItemsToPlaylist(SpotifyClient spotifyClient, List<string> itemsToAdd, string weebletdaysSelectedPlaylistId)
    {
        // Otherwise, since we can only add 100 at a time:
        var itemsToAddRequest = new PlaylistAddItemsRequest(itemsToAdd);

        await spotifyClient.Playlists.AddItems(weebletdaysSelectedPlaylistId, itemsToAddRequest);

        itemsToAdd.Clear();
    }

    private static List<PlaylistTrack<IPlayableItem>> GetRandomTracks(List<PlaylistTrack<IPlayableItem>> allWeebletdaysTracks, int numberToGet)
    {
        if (allWeebletdaysTracks.Count < numberToGet)
            throw new Exception($"Cannot select {numberToGet} tracks when supplied playlist only has {allWeebletdaysTracks.Count} tracks! Reduce numberToGet");
        
        var returnTracks = new List<PlaylistTrack<IPlayableItem>>();
        
        var random = new Random();

        var timeoutCounter = 4000; // Attempts to get unique tracks before we decide we're in an infinite loop and break
        
        while (numberToGet-- > 0)
        {
            // Get random track number
            var trackIndexToAdd = random.Next(allWeebletdaysTracks.Count);

            var trackToAdd = allWeebletdaysTracks[trackIndexToAdd].Track as FullTrack;

            // Make sure we haven't already used this track
            while (timeoutCounter-- > 0 &&
                   AlreadyAddedTracks.Contains(trackToAdd?.Id ?? ""))
            {
                trackIndexToAdd = random.Next(allWeebletdaysTracks.Count);
                
                trackToAdd = allWeebletdaysTracks[trackIndexToAdd].Track as FullTrack;
            }
            
            AlreadyAddedTracks.Add(trackToAdd?.Id ?? "");
            
            returnTracks.Add(
                allWeebletdaysTracks[trackIndexToAdd]);
        }

        return returnTracks;
    }
    
    private static async Task<List<PlaylistTrack<IPlayableItem>>> GetAllPlaylistTracks(string playlistId, SpotifyClient spotifyClient)
    {
        var convertedPlaylist = await spotifyClient.Playlists.Get(playlistId);

        if (convertedPlaylist.Tracks is not null)
        {
            var allWeebletdaysTracks = await spotifyClient.PaginateAll(convertedPlaylist.Tracks);

            var returnTracks = new List<PlaylistTrack<IPlayableItem>>();
        
            var tracksCount = 0;
            foreach (var playlistTrack in allWeebletdaysTracks)
            {
                if (playlistTrack.Track is not FullTrack track) continue;

                // All FullTrack properties are available
                var artistString = track.Artists.First().Name;
            
                Console.WriteLine($"Curated Weebletdays Track: #{tracksCount++}: {artistString} - {track.Name} | ID: {track.Id}");
            
                returnTracks.Add(playlistTrack);
            }

            return returnTracks;
        }
        else
        {
            throw new Exception(
                $"Attempted to get all playlist tracks for playlist ID: {playlistId} but got back null playlist");
        }
    }

    // private static FullPlaylist GetCuratedWeebletdaysPlaylist(IList<FullPlaylist> userPlaylists)
    // {
    //     foreach (var playlist in userPlaylists)
    //     {
    //         if (playlist.Id == SECRETS.PLAYLIST_ID_TO_SEARCH_FOR)
    //             return playlist;
    //     }
    //
    //     throw new Exception("Couldn't find playlist with ID matching Curated Weebletdays ID");
    // }

    private static async Task ShowUserAuthenticatedMessage(SpotifyClient spotify)
    {
        var me = await spotify.UserProfile.Current();
        
        Console.WriteLine($"Welcome {me.DisplayName} ({me.Id}), you're authenticated!");
    }

    private static void ListAllPlaylistsWithId(IList<FullPlaylist> userPlaylists)
    {
        Console.WriteLine($"Total Playlists in your Account: {userPlaylists.Count}");

        var playlistIndex = 1;
        
        foreach (var playlist in userPlaylists)
        {
            Console.WriteLine();
            Console.WriteLine($"Playlist [ #{playlistIndex++} ]");
            Console.WriteLine($"{playlist.Name} | ID: {playlist.Id}");
        }

        Console.WriteLine();
    }

    private static async Task StartAuthentication()
    {
        var (verifier, challenge) = PKCEUtil.GenerateCodes();

        await Server.Start();
        Server.AuthorizationCodeReceived += async (sender, response) =>
        {
            await Server.Stop();
            var token = await new OAuthClient().RequestToken(
                new PKCETokenRequest(SECRETS.SPOTIFY_CLIENT_ID!, response.Code, Server.BaseUri, verifier)
            );

            await File.WriteAllTextAsync(CredentialsPath, JsonConvert.SerializeObject(token));
            await RefreshSelectDailyPlaylist();
        };

        var request = new LoginRequest(Server.BaseUri, SECRETS.SPOTIFY_CLIENT_ID!, LoginRequest.ResponseType.Code)
        {
            CodeChallenge = challenge,
            CodeChallengeMethod = "S256",
            Scope = new List<string> { "user-read-email", "user-read-private", "playlist-read-private", "playlist-read-collaborative", "playlist-modify-private", "playlist-modify-public" }
        };

        var uri = request.ToUri();
        try
        {
            BrowserUtil.Open(uri);
        }
        catch (Exception)
        {
            Console.WriteLine("Unable to open URL, manually open: {0}", uri);
        }
    }
}