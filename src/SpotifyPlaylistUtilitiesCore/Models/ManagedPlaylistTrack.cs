using SpotifyAPI.Web;

namespace SpotifyPlaylistUtilities.Models;

public class ManagedPlaylistTrack
{
    public string Id => _originalTrack.Id;
    public string Uri => _originalTrack.Uri;
    public string Name => _originalTrack.Name;
    public double PickWeight { get; set; } 
    public int RandomShuffleNumber { get; private set; }
    
    private readonly FullTrack _originalTrack;

    public ManagedPlaylistTrack(FullTrack originalTrack)
    {
        _originalTrack = originalTrack;
        
        RerandomizeShuffleNumber();
    }

    public void RerandomizeShuffleNumber()
    {
        var random = new Random();
        
        RandomShuffleNumber = random.Next(int.MaxValue);
    }
}
