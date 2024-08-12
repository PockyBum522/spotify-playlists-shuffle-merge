using System.Security.Authentication;
using Newtonsoft.Json;
using Serilog;
using SpotifyAPI.Web;
using SpotifyAPI.Web.Auth;

namespace SpotifyPlaylistUtilities;

// ReSharper disable once InconsistentNaming because _logger is going to behave like a private field
public class SpotifyAuthenticationManager(ILogger _logger)
{
    private string CredentialsPath => Path.Join(Path.GetDirectoryName(Environment.ProcessPath) ?? "ERROR_GETTING_APP_PATH", "credentials.json");

    private static readonly EmbedIOAuthServer Server = new(new Uri("http://localhost:5543/callback"), 5543);

    private SpotifyClient? _spotifyClient;

    /// <summary>
    /// Checks that SECRETS.SPOTIFY_CLIENT_ID is not empty
    /// Checks whether token needs to be refreshed
    /// Saves updated credentials to json file
    /// </summary>
    /// <returns>Task with an authenticated spotify client</returns>
    /// <exception cref="NullReferenceException">If the spotify client stays null after creation</exception>
    /// <exception cref="AuthenticationException">If generated credentials.json cannot be created</exception>
    public async Task<SpotifyClient> GetAuthenticatedSpotifyClient()
    {
        // Just set up a new token every time. Nothing we're doing needs anything to be fast anyways and this might fix the invalid_grant problems
        _spotifyClient = null;
        File.Delete(CredentialsPath);
        
        if (string.IsNullOrEmpty(SECRETS.SPOTIFY_CLIENT_ID))
        {
            throw new NullReferenceException(
                "Please set SPOTIFY_CLIENT_ID via SECRETS.cs before starting the program"
            );
        }
        
        //if (_spotifyClient is not null) return _spotifyClient;

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

        var authenticator = new PKCEAuthenticator(SECRETS.SPOTIFY_CLIENT_ID, token!);
        authenticator.TokenRefreshed += (_, refreshedToken) => File.WriteAllText(CredentialsPath, JsonConvert.SerializeObject(refreshedToken));

        var config = SpotifyClientConfig.CreateDefault().WithAuthenticator(authenticator);

        _spotifyClient = new SpotifyClient(config);
        
        if (_spotifyClient is null)
            throw new NullReferenceException(
                $"_spotifyClient was not set up in {nameof(SpotifyAuthenticationManager)} and is null");
        
        // Quick check
        var me = await _spotifyClient.UserProfile.Current();
        _logger.Information("Welcome {DisplayName} with User ID:({Id}), you're authenticated!", me.DisplayName, me.Id);

        return _spotifyClient;
    }
    
    private async Task StartAuthentication()
    {
        var completedAuth = false;
        
        var (verifier, challenge) = PKCEUtil.GenerateCodes();

        await Server.Start();
        Server.AuthorizationCodeReceived += async (_, response) =>
        {
            await Server.Stop();
            var token = await new OAuthClient().RequestToken(
                new PKCETokenRequest(SECRETS.SPOTIFY_CLIENT_ID, response.Code, Server.BaseUri, verifier)
            );

            await File.WriteAllTextAsync(CredentialsPath, JsonConvert.SerializeObject(token));
            
            completedAuth = true;
        };

        var request = new LoginRequest(Server.BaseUri, SECRETS.SPOTIFY_CLIENT_ID, LoginRequest.ResponseType.Code)
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
            _logger.Information("Unable to open URL, manually open: {Uri}", uri);
        }

        // Wait until auth is done so program doesn't try to continue without auth
        while (!completedAuth)
        {
            await Task.Delay(500);
        }
    }
}