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
    public readonly string WlRefreshCredentialName = "WL:Identity:Refresh";
    private readonly string authority = "https://identity.wildernesslabs.co/oauth2/default";
    private readonly string redirectUri = "http://localhost:8877/";
    private readonly string clientId = "0oa3axsuyupb7J6E15d6";
    private readonly ILogger _logger;

    public IdentityManager(ILoggerFactory? loggerFactory = null)
        : this(loggerFactory?.CreateLogger("IdentityManager") ?? null)
    {
    }

    private IdentityManager(ILogger? logger = null)
    {
        _logger = logger ?? NullLogger.Instance;
    }

    /// <summary>
    /// Kick off login
    /// </summary>
    /// <returns></returns>
    public async Task<bool> Login(string host, CancellationToken? cancellationToken = default)
    {
        try
        {
            var client = await GetOidcClient();

            using (var http = new HttpListener())
            {
                http.Prefixes.Add(redirectUri);
                http.Start();

                // generated login url with PKCE
                var state = await client.PrepareLoginAsync(cancellationToken: cancellationToken ?? CancellationToken.None);

                OpenBrowser(state.StartUrl);

                var context = await http.GetContextAsync();
                var raw = context.Request.RawUrl;
                context.Response.StatusCode = 302;
                context.Response.AddHeader("Location", host);
                context.Response.Close();

                var result = await client.ProcessResponseAsync(raw, state, cancellationToken: cancellationToken ?? CancellationToken.None);

                if (result.IsError)
                {
                    _logger?.LogError(result.Error);
                }
                else
                {
                    var email = result.User.Claims.SingleOrDefault(x => x.Type == "email")?.Value;
                    if (string.IsNullOrWhiteSpace(email))
                    {
                        _logger?.LogWarning("Unable to get email address");
                    }
                    else
                    {
                        // saving only the refresh token since the access token is too large
                        SaveCredential(WlRefreshCredentialName, email!, result.RefreshToken);
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
    }

    /// <summary>
    /// Get access token through a token refresh
    /// </summary>
    /// <returns></returns>
    public async Task<string> GetAccessToken(CancellationToken? cancellationToken = null)
    {
        string refreshToken = string.Empty;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX) || RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            refreshToken = GetCredentials(WlRefreshCredentialName).password;
        }
        else
        {
            _logger?.LogWarning("Unsupported OS detected.");
            throw new NotSupportedException();
        }

        if (!string.IsNullOrEmpty(refreshToken))
        {
            var client = await GetOidcClient();
            var result = await client.RefreshTokenAsync(refreshToken, cancellationToken: cancellationToken ?? CancellationToken.None);
            return result.AccessToken;
        }
        else
        {
            return string.Empty;
        }
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
                    return secret.Split(' ') switch { var a => (a[0], a[1]) };
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
            _logger?.LogWarning("Unsupported OS detected.");
            throw new NotSupportedException();
        }
    }

    private Task<OidcClient> GetOidcClient()
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
        return Task.FromResult(new OidcClient(options));
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
            _logger?.LogWarning("Unsupported OS detected.");
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
}
