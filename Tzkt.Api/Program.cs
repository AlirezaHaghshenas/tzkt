using System;
using System.Linq;
using System.Threading;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Tzkt.Data;

namespace Tzkt.Api
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Host.CreateDefaultBuilder(args).ConfigureApi().Build().Init().Run();
        }
    }

    public static class IHostBuilderExt
    {
        public static IHostBuilder ConfigureApi(this IHostBuilder host) => host
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.UseStartup<Startup>();
            });
    }

    static class IHostExt
    {
        public static IHost Init(this IHost host, int attempt = 0)
        {
            using var scope = host.Services.CreateScope();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
            var db = scope.ServiceProvider.GetRequiredService<TzktContext>();

            try
            {
                logger.LogInformation("Initialize database");

                var migrations = db.Database.GetPendingMigrations();
                if (migrations.Any())
                    throw new Exception($"{migrations.Count()} database migrations are pending");

                logger.LogInformation("Database initialized");
                return host;
            }
            catch (Exception ex)
            {
                logger.LogCritical($"Failed to initialize database: {ex.Message}. Try again...");
                if (attempt >= 10) throw;
                Thread.Sleep(1000);

                return host.Init(++attempt);
            }
        }
    }
}
