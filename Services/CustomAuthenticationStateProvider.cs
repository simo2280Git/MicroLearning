using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;

namespace MicroLearning.Services
{
    public class CustomAuthStateProvider : AuthenticationStateProvider
    {
        private readonly Supabase.Client _supabase;
        private readonly IJSRuntime _jsRuntime;
        private const string AccessTokenKey = "supabase_access_token";
        private const string RefreshTokenKey = "supabase_refresh_token";

        public CustomAuthStateProvider(Supabase.Client supabase, IJSRuntime jsRuntime)
        {
            _supabase = supabase;
            _jsRuntime = jsRuntime;
        }

        public override async Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            try
            {
                // Se non c'è una sessione in memoria, prova a leggerla da localStorage
                if (_supabase.Auth.CurrentSession == null)
                {
                    var accessToken = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", AccessTokenKey);
                    var refreshToken = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", RefreshTokenKey);

                    if (!string.IsNullOrEmpty(accessToken) && !string.IsNullOrEmpty(refreshToken))
                    {
                        await _supabase.Auth.SetSession(accessToken, refreshToken);
                    }
                }

                var user = _supabase.Auth.CurrentUser;
                if (user != null)
                {
                    var claims = new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, user.Id ?? string.Empty),
                        new Claim(ClaimTypes.Email, user.Email ?? string.Empty)
                    };

                    var identity = new ClaimsIdentity(claims, "SupabaseAuth");
                    return new AuthenticationState(new ClaimsPrincipal(identity));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Auth Provider Error]: {ex.Message}");
            }

            // Utente non autenticato
            return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
        }

        public void NotifyUserAuthenticationState()
        {
            NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
        }
    }
}