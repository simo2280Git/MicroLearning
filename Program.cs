using MicroLearning;
using MicroLearning.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Supabase;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

// REGISTRAZIONE SUPABASE CLIENT (Singleton / Scoped)
builder.Services.AddScoped(sp =>
{
    var configuration = sp.GetRequiredService<IConfiguration>();
    var url = configuration["SupabaseUrl"] ?? "";
    var key = configuration["SupabaseKey"] ?? "";

    var options = new SupabaseOptions { AutoConnectRealtime = false };
    return new Supabase.Client(url, key, options);
});

// REGISTRAZIONE SERVICES
builder.Services.AddScoped<CardService>();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<TopicService>();
builder.Services.AddAuthorizationCore();
builder.Services.AddScoped<AuthenticationStateProvider, CustomAuthStateProvider>();

var host = builder.Build();

// INIZIALIZZAZIONE OBBLIGATORIA DI SUPABASE
var supabaseClient = host.Services.GetRequiredService<Supabase.Client>();
await supabaseClient.InitializeAsync();

await host.RunAsync();