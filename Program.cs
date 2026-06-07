using GameWiki.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

builder.Services.AddDbContext<GameWikiDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath       = "/Account/Login";
        options.LogoutPath      = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan  = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
    });

builder.Services.AddAuthorization();

builder.Services.AddHttpClient<RawgService>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<ArticleService>();
builder.Services.AddScoped<ReviewService>();
builder.Services.AddScoped<AccountService>();
builder.Services.AddScoped<FavoriteService>();
builder.Services.AddScoped<GameService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.Use(async (context, next) =>
{
    if (context.User.Identity?.IsAuthenticated == true)
    {
        var userIdClaim = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
        if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
        {
            var db = context.RequestServices.GetRequiredService<GameWikiDbContext>();
            context.Items["UnreadNotificationsCount"] =
                await db.UserNotifications.CountAsync(n => n.UserId == userId && !n.IsRead);
        }
    }
    await next();
});

app.MapStaticAssets();
app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}")
   .WithStaticAssets();

app.Run();