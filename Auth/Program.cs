using Auth;
using Auth.DB;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations.Operations.Builders;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<MyDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.SameSite = SameSiteMode.None;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.Path = "/";
});

builder.Services.AddAuthentication("cookie")
    .AddCookie("cookie")
    .AddOAuth("github", options =>
    {
        options.SignInScheme = "cookie";
        options.ClientId = builder.Configuration["Github:ClientId"];
        options.ClientSecret = builder.Configuration["Github:ClientSecret"];
        options.AuthorizationEndpoint = "https://github.com/login/oauth/authorize";
        options.TokenEndpoint = "https://github.com/login/oauth/access_token";
        options.CallbackPath = "/oauth/github-cb";
        options.SaveTokens = true;

        options.UserInformationEndpoint = "https://api.github.com/user";

        options.ClaimActions.MapJsonKey("sub", "id");
        options.ClaimActions.MapJsonKey(ClaimTypes.Name, "login");
        options.ClaimActions.MapJsonKey(ClaimTypes.Email, "email");
        options.Scope.Add("user:email");

        options.Events.OnCreatingTicket = async ctx =>
        {
            HttpRequestMessage request;
            HttpResponseMessage result;
            JsonElement user;

            // Get user info
            request = new HttpRequestMessage(HttpMethod.Get, ctx.Options.UserInformationEndpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ctx.AccessToken);
            using (result = await ctx.Backchannel.SendAsync(request))
            {
                user = await result.Content.ReadFromJsonAsync<JsonElement>();
            }
            ctx.RunClaimActions(user);

            // Get user email
            request = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/user/emails");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ctx.AccessToken);
            List<GitHubEmail> emails;
            using (result = await ctx.Backchannel.SendAsync(request))
            {
                emails = await result.Content.ReadFromJsonAsync<List<GitHubEmail>>();
            }
            var email = emails.FirstOrDefault(e => e.Primary)?.Email;

            // Add email to claims
            if (email != null)
            {
                var claimsIdentity = (ClaimsIdentity)ctx.Principal.Identity;
                claimsIdentity.AddClaim(new Claim(ClaimTypes.Email, email));
            }


            // Save the user data to the db
            var db = ctx.HttpContext.RequestServices.GetRequiredService<MyDbContext>();
            var principal = new ClaimsPrincipal(ctx.Identity);
            var githubId = principal.FindFirstValue("sub");
            var name = principal.FindFirstValue(ClaimTypes.Name);

            var dbUser = await db.Users.FirstOrDefaultAsync(u => u.Id == int.Parse(githubId));
            if (dbUser == null)
            {
                dbUser = new User
                {
                    Id = int.Parse(githubId),
                    Name = name,
                    Email = email,
                };
                db.Users.Add(dbUser);
            }
            else
            {
                dbUser.Name = name;
                dbUser.Email = email;
            }

            await db.SaveChangesAsync();
        };

    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowSpecificOrigins",
        builder =>
        {
            builder.WithOrigins("https://localhost:7206",
                                "https://localhost:7166")
                   .AllowAnyHeader()
                   .AllowAnyMethod()
                   .AllowCredentials();
        });
});

builder.Services.AddControllers();
var app = builder.Build();
app.MapControllers();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "My API V1");
});

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseSession();

app.UseRouting();
app.UseCors("AllowSpecificOrigins");
app.UseAuthentication();
app.UseAuthorization();


app.Run();