using AiMeetingAssistant.Core.Services;
using AiMeetingAssistant.Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AiMeetingAssistant.Tests.Integration;

/// <summary>
/// Boots the real Api host with an isolated in-memory database (unique per instance) and test-only
/// JWT/auth configuration, so integration tests never touch Postgres or the real Anthropic/Jira APIs
/// unless a test explicitly swaps in a fake via <see cref="ReplaceService{TService}"/>.
/// </summary>
public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = $"test-db-{Guid.NewGuid()}";
    private readonly List<Action<IServiceCollection>> _serviceOverrides = [];

    public void ReplaceService<TService>(TService instance) where TService : class
    {
        _serviceOverrides.Add(services =>
        {
            services.RemoveAll<TService>();
            services.AddSingleton(instance);
        });
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Issuer"] = "AiMeetingAssistant.Tests",
                ["Jwt:Audience"] = "AiMeetingAssistant.Tests.Client",
                ["Jwt:SigningKey"] = "test-signing-key-not-for-production-use-only-in-tests-0123456789",
                ["Jwt:ExpiryMinutes"] = "60",
            });
        });

        builder.ConfigureServices(services =>
        {
            // Program.cs's AddDbContext(UseNpgsql(...)) registers more than just DbContextOptions<T> —
            // EF Core also composes internal per-context configuration services that still reference
            // Npgsql unless removed too, which otherwise leaves two providers registered at once. Sweep
            // out anything generic over AppDbContext rather than naming each internal type individually.
            var toRemove = services
                .Where(d => d.ServiceType.IsGenericType && d.ServiceType.GetGenericArguments().Contains(typeof(AppDbContext)))
                .ToList();
            foreach (var descriptor in toRemove)
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(_dbName));

            foreach (var apply in _serviceOverrides)
            {
                apply(services);
            }
        });
    }
}
