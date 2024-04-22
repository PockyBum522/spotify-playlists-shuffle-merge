using System;
using SpotifyAPI.Web;

namespace SpotifyPlaylistUtility.Models;

public class ShuffleablePlaylistTrack
{
    public ShuffleablePlaylistTrack(PlaylistTrack<IPlayableItem> track)
    {
        Track = track;
        
        var random = new Random();
        
        RandomShuffleNumber = random.Next(int.MaxValue);
    }
    
    public PlaylistTrack<IPlayableItem> Track { get; }
    
    public int RandomShuffleNumber { get; }
}