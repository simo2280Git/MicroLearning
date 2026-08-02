using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Supabase.Gotrue;

namespace MicroLearning.Services
{
    public class CustomAuthStateProvider : AuthenticationStateProvider
    {
        private readonly Supabase.Client _supabase;

        public CustomAuthStateProvider(Supabase.Client supabase)
        {
            _supabase = supabase;

            // Ascolta i cambiamenti di stato di Supabase (Login, Logout, Token Refresh)
            _supabase.Auth.AddStateChangedListener((sender, state) =>
            {
                NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
            });
        }

        public override Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            var currentUser = _supabase.Auth.CurrentUser;

            if (currentUser == null)
            {
                // Utente anonimo / Non autenticato
                var anonymous = new ClaimsPrincipal(new ClaimsIdentity());
                return Task.FromResult(new AuthenticationState(anonymous));
            }

            // Utente autenticato: creiamo le Identity Claims per Blazor
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, currentUser.Id ?? string.Empty),
                new Claim(ClaimTypes.Email, currentUser.Email ?? string.Empty)
            };

            var identity = new ClaimsIdentity(claims, "SupabaseAuth");
            var user = new ClaimsPrincipal(identity);

            return Task.FromResult(new AuthenticationState(user));
        }
    }
}