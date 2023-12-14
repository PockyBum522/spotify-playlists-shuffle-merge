using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using SpotifyAPI.Web;
using SpotifyPlaylistUtility.Logic;
using SpotifyPlaylistUtility.Logic.Spotify;
using SpotifyPlaylistUtility.Logic.Spotify.Playlists;

namespace SpotifyPlaylistUtility.ViewModels;

public partial class MainViewModel : ObservableObject
{
    [ObservableProperty] private string _sourcePlaylistId = "";

    private SpotifyClient? _spotifyClient;
    private PlaylistShuffler? _spotifyPlaylistShuffler;
    
    private readonly ILogger _logger;
    private readonly LoggerConfiguration _loggerConfiguration;
    private AuthenticationManager? _spotifyAuthenticationManager;
    
    public MainViewModel(LoggerConfiguration loggerConfiguration)
    {
        _loggerConfiguration = loggerConfiguration;

        _logger = loggerConfiguration
            .MinimumLevel.Debug()
            .CreateLogger();
    }

    [RelayCommand]
    private async Task ShufflePlaylistInPlace()
    {
        await EnsureSpotifyClientIsSetup();
        
        if (string.IsNullOrWhiteSpace(SourcePlaylistId))
            SourcePlaylistId = "Please input playlist ID here first";

        if (_spotifyPlaylistShuffler is null)
        {
            throw new NullReferenceException(
                $"_spotifyPlaylistShuffler was not set up in {nameof(MainViewModel)} and is null");
        }
        
        await _spotifyPlaylistShuffler.ShuffleAllIn(SourcePlaylistId);
    }

    private async Task EnsureSpotifyClientIsSetup()
    {
        _spotifyAuthenticationManager = new AuthenticationManager(_logger);
        
        _spotifyClient ??= await _spotifyAuthenticationManager.GetAuthenticatedSpotifyClient();
        
        _spotifyPlaylistShuffler ??= new PlaylistShuffler(_logger, _spotifyClient);
    }
}
