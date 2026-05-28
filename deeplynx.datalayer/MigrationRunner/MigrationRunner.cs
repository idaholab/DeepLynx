using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using deeplynx.datalayer.Models;
using Npgsql;
using Pgvector.EntityFrameworkCore;
using Pgvector.Npgsql;

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

                    var latticeContext = scope.ServiceProvider.GetRequiredService<LatticeContext>();
                    await latticeContext.Database.MigrateAsync();
                }

                Console.WriteLine("Migrations applied successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred while applying migrations: {ex.Message}");
                Console.WriteLine("Are the database connection credentials correct?");
                Console.WriteLine("Migrations were NOT applied.");
                throw;
            }
        }

        private static void ConfigureServices(IServiceCollection services, string connectionString)
        {
            var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
            dataSourceBuilder.UseVector();
            var dataSource = dataSourceBuilder.Build();

            services.AddDbContext<DeeplynxContext>(options =>
                options.UseNpgsql(dataSource));
            services.AddDbContext<LatticeContext>(options =>
                options.UseNpgsql(connectionString));
        }
    }
}
