using GarageManagementSystem.Data;
using GarageManagementSystem.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using GroqSharp;
using GarageManagementSystem.Services;

namespace GarageManagementSystem
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            // Apikey: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
            var builder = WebApplication.CreateBuilder(args);

            // Add controllers
            builder.Services.AddControllers();

            // Logging
            builder.Logging.AddConsole();

            // Database connection
            var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
            builder.Services.AddDbContext<AppDbContext>(options =>
                options.UseSqlServer(connectionString));

            builder.Services.AddScoped<IAlertService, AlertService>();
            builder.Services.AddScoped<NotificationService>();

            builder.Services.AddTransient<IEmailService, SendGridEmailService>();

            // Identity setup
            builder.Services.AddIdentity<Users, IdentityRole<int>>(options =>
            {
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequiredLength = 8;
                options.User.RequireUniqueEmail = true;

                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.AllowedForNewUsers = true;

                // Add this line to require email confirmation before login
                options.SignIn.RequireConfirmedEmail = !builder.Environment.IsDevelopment();
            })
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders();

            builder.Services.AddControllersWithViews();

            // Add HTTP client for embeddings
            builder.Services.AddHttpClient("EmbeddingClient", client => {
                client.Timeout = TimeSpan.FromSeconds(30);
            });

            // Add CORS policy for testing
            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowTestClient", builder =>
                {
                    builder.AllowAnyOrigin()
                           .AllowAnyMethod()
                           .AllowAnyHeader();
                });
            });

            // Add memory cache for the enhanced chatbot
            builder.Services.AddMemoryCache();

            // Configure Groq client
            var apiKey = builder.Configuration["ApiKey"];
            var apiModel = "llama-3.1-70b-online";

            builder.Services.AddSingleton<IGroqClient>(sp =>
                new GroqClient(apiKey, apiModel)
                    .SetTemperature(0.5)
                    .SetMaxTokens(512)
                    .SetTopP(1)
                    .SetStop("NONE")
                    .SetStructuredRetryPolicy(5));

            // Register other services
            builder.Services.AddTransient<PhoneCallService>();
            builder.Services.AddHttpClient<ChatbotController>();
            builder.Services.AddHttpClient();

            var app = builder.Build();

            // Middleware Configuration
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Home/Error");
                app.UseHsts();
            }

            // IMPORTANT: Add CORS early in the pipeline, before routing
            app.UseCors("AllowTestClient");

            app.UseHttpsRedirection();
            app.UseStaticFiles();
            app.UseRouting();
            app.UseAuthentication();
            app.UseAuthorization();

            // Map Controllers
            app.MapControllers();

            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Account}/{action=Login}/{id?}");

            // In Program.cs (for .NET 6+)
            app.MapControllerRoute(
                name: "enhanced-chatbot",
                pattern: "api/enhanced-chatbot/{action}",
                defaults: new { controller = "EnhancedChatbot" });

            // Seed Roles and Admin User
            using (var scope = app.Services.CreateScope())
            {
                var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<int>>>();
                var userManager = scope.ServiceProvider.GetRequiredService<UserManager<Users>>();
                var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

                var roles = new[] { "Admin", "Mechanic", "Customer" };
                foreach (var role in roles)
                {
                    if (!await roleManager.RoleExistsAsync(role))
                    {
                        await roleManager.CreateAsync(new IdentityRole<int>(role));
                    }
                }

                // Seed Admin User
                string adminEmail = "admin@admin.com";
                string profilePicture = "/images/spotify.jpg";

                if (await userManager.FindByEmailAsync(adminEmail) == null)
                {
                    var adminUser = new Users
                    {
                        ProfilePicture = profilePicture,
                        UserName = adminEmail,
                        Email = adminEmail,
                        EmailConfirmed = true,
                        FullName = "Admin User",
                        PhoneNumber = "3232414"
                    };

                    var passwordHasher = new PasswordHasher<Users>();
                    adminUser.PasswordHash = passwordHasher.HashPassword(adminUser, "admin1234");

                    var result = await userManager.CreateAsync(adminUser);
                    if (result.Succeeded)
                    {
                        await userManager.AddToRoleAsync(adminUser, "Admin");
                        await userManager.SetLockoutEnabledAsync(adminUser, false);
                        logger.LogInformation("Admin user created successfully.");
                    }
                    else
                    {
                        foreach (var error in result.Errors)
                        {
                            logger.LogError($"Error: {error.Description}");
                        }
                    }
                }
            }

            app.MapGet("/routes", (IEnumerable<EndpointDataSource> endpointSources) =>
                string.Join("\n", endpointSources.SelectMany(source => source.Endpoints)));

            app.Run();
        }
    }
}