using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Authentication;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Serilog;
using SpotifyAPI.Web;
using SpotifyAPI.Web.Auth;

namespace SpotifyPlaylistUtility.Logic.Spotify;

public class AuthenticationManager
{
    private readonly ILogger _logger;
    private string CredentialsPath => Path.Join(Path.GetDirectoryName(Environment.ProcessPath) ?? "ERROR_GETTING_APP_PATH", "credentials.json");

    private static readonly EmbedIOAuthServer Server = new(new Uri("http://localhost:5543/callback"), 5543);

    private SpotifyClient? _spotifyClient;

    public AuthenticationManager(ILogger logger)
    {
        _logger = logger;
    }
    
    public async Task<SpotifyClient> GetAuthenticatedSpotifyClient()
    {
        // This is a bug in the SWAN Logging library, need this hack to bring back the cursor in a console app
        // AppDomain.CurrentDomain.ProcessExit += (_, _) => Exiting();

        if (string.IsNullOrEmpty(SECRETS.SPOTIFY_CLIENT_ID))
        {
            throw new NullReferenceException(
                "Please set SPOTIFY_CLIENT_ID via SECRETS.cs before starting the program"
            );
        }

        if (_spotifyClient is not null) return _spotifyClient;

        if (!File.Exists(CredentialsPath))
        {
            await StartAuthentication();
        }
        
        if (!File.Exists(CredentialsPath))
        {
            throw new AuthenticationException(
                $"Could not find credentials file after authentication: {CredentialsPath}");
        }
        
        // Configure spotify client now that we've authed
        var json = await File.ReadAllTextAsync(CredentialsPath);
        var token = JsonConvert.DeserializeObject<PKCETokenResponse>(json);

        var authenticator = new PKCEAuthenticator(SECRETS.SPOTIFY_CLIENT_ID!, token!);
        authenticator.TokenRefreshed += (_, refreshedToken) => File.WriteAllText(CredentialsPath, JsonConvert.SerializeObject(refreshedToken));

        var config = SpotifyClientConfig.CreateDefault().WithAuthenticator(authenticator);

        _spotifyClient ??= new SpotifyClient(config);
        
        if (_spotifyClient is null)
            throw new NullReferenceException(
                $"_spotifyClient was not set up in {nameof(AuthenticationManager)} and is null");
        
        // Quick check
        var me = await _spotifyClient.UserProfile.Current();
        _logger.Information($"Welcome {me.DisplayName} ({me.Id}), you're authenticated!");

        return _spotifyClient;
    }
    
    private async Task StartAuthentication()
    {
        var completedAuth = false;
        
        var (verifier, challenge) = PKCEUtil.GenerateCodes();

        await Server.Start();
        Server.AuthorizationCodeReceived += async (sender, response) =>
        {
            await Server.Stop();
            var token = await new OAuthClient().RequestToken(
                new PKCETokenRequest(SECRETS.SPOTIFY_CLIENT_ID!, response.Code, Server.BaseUri, verifier)
            );

            await File.WriteAllTextAsync(CredentialsPath, JsonConvert.SerializeObject(token));
            
            completedAuth = true;
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
            _logger.Information("Unable to open URL, manually open: {0}", uri);
        }

        // Wait until auth is done so program doesn't try to continue without auth
        while (!completedAuth)
        {
            await Task.Delay(500);
        }
    }
}