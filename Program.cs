using Lexicube.Components;
using Lexicube.Services;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.LoginPath = "/login";
        o.Cookie.Name = "lexicube.auth";
        o.Cookie.HttpOnly = true;
        o.Cookie.SameSite = SameSiteMode.Lax;
        o.ExpireTimeSpan = TimeSpan.FromDays(30);
        o.SlidingExpiration = true;
    });
builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();

builder.Services.AddSingleton<WordService>();
builder.Services.AddSingleton<DataStore>();
builder.Services.AddSingleton<AuthService>();
builder.Services.AddSingleton<GameService>();

var app = builder.Build();

// Fail fast if the word lists are missing or the data file is unreadable.
app.Services.GetRequiredService<WordService>();
app.Services.GetRequiredService<DataStore>();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

#if NET9_0_OR_GREATER
app.MapStaticAssets();
#else
app.UseStaticFiles();
#endif

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapPost("/account/logout", async (HttpContext ctx, IAntiforgery antiforgery) =>
{
    await antiforgery.ValidateRequestAsync(ctx);
    await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect("/login");
}).DisableAntiforgery(); // validated manually above

// Used when a cookie refers to an account that no longer exists.
app.MapGet("/account/reset", async (HttpContext ctx) =>
{
    await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect("/login");
});

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
