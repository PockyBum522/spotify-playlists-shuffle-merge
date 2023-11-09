# spotify-playlists-shuffle-merge

Takes two large spotify playlists, pulls 150 songs from each (customizable) and then refreshes a destination playlist with those 300 random songs. 

Good for running nightly to always have a shuffled selection of two people's playlists, or expand it to more playlists and always have a shuffled daily selection if you have a very large set of playlists for your library.

Names are extremely weird since they refer to specific playlists I'm doing this with.

## Improvements

Needs a better paginator with a delay between requests, but works fine for now. However, this can drain your request limit quite fast, so consider using a custom paginator with delays. You can add one in each call to spotifyClient.PaginateAll()

# SECRETS.cs format

```
// ReSharper disable InconsistentNaming because I like having secret constants be capitalized 
namespace SpotifyPlaylistUtilities;

public class SECRETS
{
    /// <summary>
    /// Client ID for application in spotify dev dashboard
    /// </summary>
    public const string SPOTIFY_CLIENT_ID = "7362c858a9824a73a112ba818bbb2abc";
    
    /// <summary>
    /// First playlist to pull 150 random songs from
    /// </summary>
    public const string PLAYLIST_ID_CURATED_WEEBLETDAYS = "B3FDhsBiS6usFoFecEzVJnXt";
    
    /// <summary>
    /// Second playlist to pull 150 random songs from
    /// </summary>
    public const string PLAYLIST_ID_SELECT_SELECTIONS = "3wAL6yR7Dz48Gpp4c9RHdXh5";
    
    /// <summary>
    /// Destination playlist to drop 300 random songs into daily
    /// </summary>
    public const string PLAYLIST_ID_SELECT_DAILY = "A364q9WwQ36hQK8D4tGj5v8S";
}
```
