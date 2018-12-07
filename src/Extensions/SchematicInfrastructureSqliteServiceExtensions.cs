using Microsoft.Extensions.DependencyInjection;
using Schematic.Identity;

namespace Schematic.BaseInfrastructure.Sqlite
{
    public static class SchematicInfrastructureSqliteServiceExtensions
    {
        public static IServiceCollection AddSchematic(this IServiceCollection services)
        {
            services.AddScoped<IUserCacheKeyService, UserCacheKeyService>();
            return services;
        }
    }
}