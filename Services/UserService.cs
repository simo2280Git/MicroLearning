using Microsoft.JSInterop;
using Supabase.Gotrue;

namespace MicroLearning.Services
{
    public class UserService
    {
        private readonly Supabase.Client _supabase;
        private readonly IJSRuntime _jsRuntime;

        private const string AccessTokenKey = "supabase_access_token";
        private const string RefreshTokenKey = "supabase_refresh_token";

        public UserService(Supabase.Client supabase, IJSRuntime jsRuntime)
        {
            _supabase = supabase;
            _jsRuntime = jsRuntime;
        }

        public User? CurrentUser => _supabase.Auth.CurrentUser;
        public bool IsLoggedIn => _supabase.Auth.CurrentSession != null;

        /// <summary>
        /// Tenta di ripristinare la sessione salvata nel localStorage all'avvio dell'app.
        /// </summary>
        public async Task InitializeAsync()
        {
            try
            {
                // Se c'è già una sessione attiva in memoria, non occorre ripristinare
                if (_supabase.Auth.CurrentSession != null) return;

                var accessToken = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", AccessTokenKey);
                var refreshToken = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", RefreshTokenKey);

                if (!string.IsNullOrEmpty(accessToken) && !string.IsNullOrEmpty(refreshToken))
                {
                    // Ripristina la sessione nell'SDK di Supabase
                    await _supabase.Auth.SetSession(accessToken, refreshToken);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Auth Restore Error]: {ex.Message}");
                await ClearTokensAsync();
            }
        }

        public async Task<bool> LoginAsync(string email, string password)
        {
            try
            {
                var response = await _supabase.Auth.SignIn(email, password);
                if (response != null)
                {
                    await SaveTokensAsync(response.AccessToken, response.RefreshToken);
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

        public async Task<bool> RegisterAsync(string email, string password)
        {
            try
            {
                var response = await _supabase.Auth.SignUp(email, password);
                if (response != null)
                {
                    await SaveTokensAsync(response.AccessToken, response.RefreshToken);
                }
                return response != null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Register Error]: {ex.Message}");
                return false;
            }
        }

        public async Task LogoutAsync()
        {
            await _supabase.Auth.SignOut();
            await ClearTokensAsync();
        }

        public async Task<User?> GetCurrentUserAsync()
        {
            if (_supabase.Auth.CurrentSession == null)
            {
                await InitializeAsync();
            }
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