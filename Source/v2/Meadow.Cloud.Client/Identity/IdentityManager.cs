using CredentialManagement;
using IdentityModel.Client;
using IdentityModel.OidcClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Logging;
using System.Diagnostics;
using System.Net;
using System.Runtime.InteropServices;

namespace Meadow.Cloud.Client.Identity;

public class IdentityManager
{
    public const string WlRefreshCredentialName = "WL:Identity:Refresh";
    private const string authority = "https://identity.wildernesslabs.co/oauth2/default";
    private const string redirectUri = "http://localhost:8877/";
    private const string clientId = "0oa3axsuyupb7J6E15d6";
    private readonly ILogger _logger;

    private static AccessToken? CachedAccessToken;
    private static readonly SemaphoreSlim AccessTokenLock = new(1);

    public IdentityManager(ILogger<IdentityManager>? logger = default)
    {
        _logger = logger ?? NullLogger<IdentityManager>.Instance;
    }

    /// <summary>
    /// Kick off login
    /// </summary>
    /// <returns></returns>
    public async Task<bool> Login(CancellationToken cancellationToken = default)
    {
        try
        {
            var client = GetOidcClient();

            using (var http = new HttpListener())
            {
                http.Prefixes.Add(redirectUri);
                http.Start();

                // generated login url with PKCE
                var state = await client.PrepareLoginAsync(cancellationToken: cancellationToken);

                OpenBrowser(state.StartUrl);

                var context = await http.GetContextAsync();
                var raw = context.Request.RawUrl;
                context.Response.StatusCode = 302;
                context.Response.AddHeader("Location", "https://wldrn.es/cliauthed");
                context.Response.Close();

                var result = await client.ProcessResponseAsync(raw, state, cancellationToken: cancellationToken);

                if (result.IsError)
                {
                    _logger.LogError(result.Error);
                }
                else
                {
                    var email = result.User.Claims.SingleOrDefault(x => x.Type == "email")?.Value;
                    if (string.IsNullOrWhiteSpace(email))
                    {
                        _logger.LogWarning("Unable to get email address");
                    }
                    else
                    {
                        // saving only the refresh token since the access token is too large
                        SaveCredential(WlRefreshCredentialName, email!, result.RefreshToken);
                        await AccessTokenLock.WaitAsync(cancellationToken);
                        try
                        {
                            CachedAccessToken = new AccessToken(result.AccessToken, DateTimeOffset.UtcNow.AddSeconds(result.TokenResponse.ExpiresIn), email!);
                        }
                        finally
                        {
                            AccessTokenLock.Release();
                        }
                    }
                }
                return !result.IsError;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "An error occurred");
            return false;
        }
    }

    public void Logout()
    {
        DeleteCredential(WlRefreshCredentialName);

        AccessTokenLock.Wait();
        try
        {
            CachedAccessToken = null;
        }
        finally
        {
            AccessTokenLock.Release();
        }
    }

    /// <summary>
    /// Get access token through a token refresh
    /// </summary>
    /// <returns></returns>
    public async Task<string> GetAccessToken(CancellationToken cancellationToken = default)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) &&
            !RuntimeInformation.IsOSPlatform(OSPlatform.OSX) &&
            !RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            _logger.LogWarning("Unsupported OS detected.");
            throw new NotSupportedException();
        }

        if (CachedAccessToken?.IsValid == true)
        {
            return CachedAccessToken.Token;
        }

        await AccessTokenLock.WaitAsync(cancellationToken);
        try
        {
            if (CachedAccessToken?.IsValid == true)
            {
                return CachedAccessToken.Token;
            }

            var (emailAddress, refreshToken) = GetCredentials(WlRefreshCredentialName);
            if (string.IsNullOrEmpty(refreshToken))
            {
                return string.Empty;
            }

            var client = GetOidcClient();
            var result = await client.RefreshTokenAsync(refreshToken, cancellationToken: cancellationToken);
            CachedAccessToken = new AccessToken(result.AccessToken, DateTimeOffset.UtcNow.AddSeconds(result.ExpiresIn), emailAddress);
            return CachedAccessToken.Token;
        }
        finally
        {
            AccessTokenLock.Release();
        }
    }

    /// <summary>
    /// Gets the email address of the current logged in user, null otherwise.
    /// </summary>
    /// <returns>The email address of the current logged in user, null otherwise.</returns>
    public string? GetEmailAddress()
    {
        return CachedAccessToken?.EmailAddress;
    }

    /// <summary>
    /// Get the stored credentials 
    /// </summary>
    /// <param name="credentialName"></param>
    /// <returns></returns>
    public (string username, string password) GetCredentials(string credentialName)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var cred = new Credential() { Target = credentialName };
            if (cred.Load())
            {
                return (cred.Username, cred.Password);
            }
            else
            {
                return (string.Empty, string.Empty);
            }
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            using (var libSecret = new LibSecret("WildernessLabs", credentialName))
            {
                //Username & Password delimited with a space. String split, and returned as a tuple.
                var secret = libSecret.GetSecret();
                if (!string.IsNullOrEmpty(secret))
                {
                    return secret!.Split(' ') switch { var a => (a[0], a[1]) };
                }
                else
                {
                    return (string.Empty, string.Empty);
                }
            }
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return Keychain.Query(credentialName);
        }
        else
        {
            _logger.LogWarning("Unsupported OS detected.");
            throw new NotSupportedException();
        }
    }

    private OidcClient GetOidcClient()
    {
        var options = new OidcClientOptions
        {
            Authority = authority,
            ClientId = clientId,
            RedirectUri = redirectUri,
            Policy = new Policy
            {
                Discovery = new DiscoveryPolicy
                {
                    ValidateEndpoints = false
                }
            },
            Scope = "openid email profile groups offline_access",
            Flow = OidcClientOptions.AuthenticationFlow.AuthorizationCode,
            ResponseMode = OidcClientOptions.AuthorizeResponseMode.Redirect,
        };
        IdentityModelEventSource.ShowPII = true;
        return new OidcClient(options);
    }

    public void DeleteCredential(string credentialName)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var cred = new Credential() { Target = credentialName };
            cred.Delete();
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            using (var libSecret = new LibSecret("WildernessLabs", credentialName))
            {
                libSecret.ClearSecret();
            }
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            Keychain.Delete(credentialName);
        }
        else
        {
            _logger?.LogWarning("Unsupported OS detected.");
            throw new NotSupportedException();
        }
    }

    public bool SaveCredential(string credentialName, string username, string password)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var credential = new Credential()
            {
                Password = password,
                Type = CredentialType.Generic,
                PersistanceType = PersistanceType.LocalComputer,
                Target = credentialName,
                Username = username,
            };
            return credential.Save();
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            using (var libSecret = new LibSecret("WildernessLabs", credentialName))
            {
                //Username & Password delimited with a space.
                libSecret.SetSecret($"{username} {password}");
                return true;
            }
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            // delete first in case it already exists
            Keychain.Delete(credentialName);
            return Keychain.Add(credentialName, username, password);
        }
        else
        {
            _logger.LogWarning("Unsupported OS detected.");
            throw new NotSupportedException();
        }
    }

    private void OpenBrowser(string url)
    {
        try
        {
            Process.Start(url);
        }
        catch
        {
            // hack because of this: https://github.com/dotnet/corefx/issues/10361
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                url = url.Replace("&", "^&");
                Process.Start(new ProcessStartInfo("cmd", $"/c start {url}") { CreateNoWindow = true });
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Process.Start("xdg-open", url);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Process.Start("open", url);
            }
            else
            {
                _logger?.LogWarning("Unsupported OS detected.");
                throw new NotSupportedException();
            }
        }
    }

    private class AccessToken
    {
        public AccessToken(string token, DateTimeOffset expiresAt, string emailAddress)
        {
            Token = token; 
            ExpiresAtUtc = expiresAt; 
            EmailAddress = emailAddress;
        }

        public string Token { get; }
        public DateTimeOffset ExpiresAtUtc { get; }
        public string EmailAddress { get; }

        public bool IsValid => !string.IsNullOrWhiteSpace(Token) && ExpiresAtUtc > DateTimeOffset.UtcNow.AddMinutes(30);
    }
}
