using Supabase.Gotrue;

namespace MicroLearning.Services
{
    public class UserService
    {
        private readonly Supabase.Client _supabase;

        public UserService(Supabase.Client supabase)
        {
            _supabase = supabase;
        }

        public User? CurrentUser => _supabase.Auth.CurrentUser;
        public bool IsLoggedIn => _supabase.Auth.CurrentSession != null;

        public async Task<bool> LoginAsync(string email, string password)
        {
            var response = await _supabase.Auth.SignIn(email, password);
            return response?.User != null;
        }

        public async Task<bool> RegisterAsync(string email, string password)
        {
            try
            {
                var session = await _supabase.Auth.SignUp(email, password);
                return session != null;
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
        }

        public async Task<User?> GetCurrentUserAsync()
        {
            var session = _supabase.Auth.CurrentSession;
            return session?.User;
        }
    }
}