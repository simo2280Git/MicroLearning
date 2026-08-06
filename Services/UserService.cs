using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;
using Supabase.Gotrue;

namespace MicroLearning.Services
{
    public class UserService
    {
        private readonly Supabase.Client _supabase;
        private readonly IJSRuntime _jsRuntime;
        private readonly AuthenticationStateProvider _authStateProvider;

        private const string AccessTokenKey = "supabase_access_token";
        private const string RefreshTokenKey = "supabase_refresh_token";

        public UserService(
            Supabase.Client supabase,
            IJSRuntime jsRuntime,
            AuthenticationStateProvider authStateProvider)
        {
            _supabase = supabase;
            _jsRuntime = jsRuntime;
            _authStateProvider = authStateProvider;
        }

        /// <summary>
        /// Restituisce l'utente attualmente registrato nella sessione di Supabase.
        /// </summary>
        public User? CurrentUser => _supabase.Auth.CurrentUser;

        /// <summary>
        /// Indica se è presente una sessione attiva.
        /// </summary>
        public bool IsLoggedIn => _supabase.Auth.CurrentSession != null;

        /// <summary>
        /// Effettua il login con email e password, salvando i token nel localStorage.
        /// </summary>
        public async Task<bool> LoginAsync(string email, string password)
        {
            try
            {
                var response = await _supabase.Auth.SignIn(email, password);
                if (response != null)
                {
                    await SaveTokensAsync(response.AccessToken, response.RefreshToken);

                    // Notifica il CustomAuthStateProvider del cambio di stato
                    if (_authStateProvider is CustomAuthStateProvider customProvider)
                    {
                        customProvider.NotifyUserAuthenticationState();
                    }

                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Login Error]: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Registra un nuovo utente e, se la sessione viene aperta subito, salva i token nel localStorage.
        /// </summary>
        public async Task<bool> RegisterAsync(string email, string password)
        {
            try
            {
                var response = await _supabase.Auth.SignUp(email, password);
                if (response != null)
                {
                    await SaveTokensAsync(response.AccessToken, response.RefreshToken);

                    if (_authStateProvider is CustomAuthStateProvider customProvider)
                    {
                        customProvider.NotifyUserAuthenticationState();
                    }
                }
                return response != null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Register Error]: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Effettua il logout da Supabase e rimuove i token memorizzati nel browser.
        /// </summary>
        public async Task LogoutAsync()
        {
            try
            {
                await _supabase.Auth.SignOut();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SignOut Error]: {ex.Message}");
            }
            finally
            {
                await ClearTokensAsync();

                if (_authStateProvider is CustomAuthStateProvider customProvider)
                {
                    customProvider.NotifyUserAuthenticationState();
                }
            }
        }

        /// <summary>
        /// Ottiene l'utente corrente garantendo che lo stato di autenticazione sia stato risolto dal Provider.
        /// </summary>
        public async Task<User?> GetCurrentUserAsync()
        {
            await _authStateProvider.GetAuthenticationStateAsync();
            return _supabase.Auth.CurrentUser;
        }

        private async Task SaveTokensAsync(string? accessToken, string? refreshToken)
        {
            if (!string.IsNullOrEmpty(accessToken) && !string.IsNullOrEmpty(refreshToken))
            {
                await _jsRuntime.InvokeVoidAsync("localStorage.setItem", AccessTokenKey, accessToken);
                await _jsRuntime.InvokeVoidAsync("localStorage.setItem", RefreshTokenKey, refreshToken);
            }
        }

        private async Task ClearTokensAsync()
        {
            await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", AccessTokenKey);
            await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", RefreshTokenKey);
        }
    }
}