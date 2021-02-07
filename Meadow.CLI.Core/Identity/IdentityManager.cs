using CredentialManagement;
using IdentityModel.OidcClient;
using Microsoft.IdentityModel.Logging;
using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Meadow.CLI.Core.Auth
{
    public class IdentityManager
    {
        public readonly string WLRefreshCredentialName = "WL:Identity:Refresh";
        readonly string authority = "https://identity.wildernesslabs.co";
        readonly string redirectUri = "http://localhost:8877/";
        readonly string postAuthRedirectUri = "https://www.wildernesslabs.co";
        readonly string clientId = "0oa3axsuyupb7J6E15d6";

        /// <summary>
        /// Kick off login
        /// </summary>
        /// <returns></returns>
        public async Task<bool> LoginAsync()
        {
            try
            {
                var client = GetOidcClient();

                using (var http = new HttpListener())
                {
                    http.Prefixes.Add(redirectUri);
                    http.Start();

                    // generated login url with PKCE
                    var state = await client.PrepareLoginAsync();

                    OpenBrowser(state.StartUrl);

                    var context = await http.GetContextAsync();
                    var raw = context.Request.RawUrl;
                    context.Response.StatusCode = 302;
                    context.Response.AddHeader("Location", postAuthRedirectUri);
                    context.Response.Close();

                    var result = await client.ProcessResponseAsync(raw, state);

                    if (result.IsError)
                    {
                        Console.WriteLine(result.Error);
                    }
                    else
                    {
                        var email = result.User.Claims.SingleOrDefault(x => x.Type == "email")?.Value;
                        if (string.IsNullOrEmpty(email))
                        {
                            Console.WriteLine("Unable to get email address");
                        }
                        else
                        {
                            // saving only the refresh token since the access token is too large
                            SaveCredential(WLRefreshCredentialName, email, result.RefreshToken);
                        }
                    }
                    return !result.IsError;
                }
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.Message);
                return false;
            }
        }

        public void Logout()
        {
            DeleteCredential(WLRefreshCredentialName);
        }

        /// <summary>
        /// Get access token through a token refresh
        /// </summary>
        /// <returns></returns>
        public async Task<string> GetAccessToken()
        {
            string refreshToken = string.Empty;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                refreshToken = GetCredentials(WLRefreshCredentialName).password;
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                throw new NotSupportedException();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                throw new NotSupportedException();
            }
            else
            {
                Console.WriteLine("Unsupported OS detected.");
                throw new NotSupportedException();
            }

            if (!string.IsNullOrEmpty(refreshToken))
            {
                var client = GetOidcClient();
                var result = await client.RefreshTokenAsync(refreshToken);
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
                throw new NotSupportedException();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                throw new NotSupportedException();
            }
            else
            {
                Console.WriteLine("Unsupported OS detected.");
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
                Scope = "openid email offline_access",
                Flow = OidcClientOptions.AuthenticationFlow.AuthorizationCode,
                ResponseMode = OidcClientOptions.AuthorizeResponseMode.Redirect
            };
            IdentityModelEventSource.ShowPII = true;
            return new OidcClient(options);
        }

        private void DeleteCredential(string credentialName)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var cred = new Credential() { Target = credentialName };
                cred.Delete();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                throw new NotSupportedException();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                throw new NotSupportedException();
            }
            else
            {
                Console.WriteLine("Unsupported OS detected.");
                throw new NotSupportedException();
            }
        }

        private bool SaveCredential(string credentialName, string username, string password)
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
                throw new NotSupportedException();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                throw new NotSupportedException();
            }
            else
            {
                Console.WriteLine("Unsupported OS detected.");
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
                    Console.WriteLine("Unsupported OS detected.");
                    throw new NotSupportedException();
                }
            }
        }
    }
}
