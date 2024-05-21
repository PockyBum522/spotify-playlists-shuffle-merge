namespace SpotifyPlaylistUtilities.Models;

public class SerializableWeightedTrack
{
    public string Id { get; set; } = "";
    
    public double PickWeight { get; set; }
}