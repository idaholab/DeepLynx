using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using deeplynx.datalayer.Models;

namespace deeplynx.datalayer.MigrationRunner
{
    public static class MigrationRunner
    {
        public static async Task ApplyMigrations(string connectionString)
        {
            try
            {
                var serviceCollection = new ServiceCollection();
                ConfigureServices(serviceCollection, connectionString);

                var serviceProvider = serviceCollection.BuildServiceProvider();

                using (var scope = serviceProvider.CreateScope())
                {
                    var dbContext = scope.ServiceProvider.GetRequiredService<DeeplynxContext>();
                    await dbContext.Database.MigrateAsync();

                    var stagingContext = scope.ServiceProvider.GetRequiredService<StagingContext>();
                    await stagingContext.Database.MigrateAsync();
                }

                Console.WriteLine("Migrations applied successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred while applying migrations: {ex.Message}");
                Console.WriteLine("Are the database connection credentials correct?");
                Console.WriteLine("Migrations were NOT applied.");
            }
        }

        private static void ConfigureServices(IServiceCollection services, string connectionString)
        {
            services.AddDbContext<DeeplynxContext>(options =>
                options.UseNpgsql(connectionString));
            services.AddDbContext<StagingContext>(options =>
                options.UseNpgsql(connectionString));
        }
    }
}
