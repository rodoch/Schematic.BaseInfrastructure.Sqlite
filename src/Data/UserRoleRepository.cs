using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Caching.Memory;
using Dapper;
using Schematic.Core;
using Schematic.Identity;

namespace Schematic.BaseInfrastructure.Sqlite
{
    public class UserRoleRepository : IUserRoleRepository<UserRole>
    {
        private readonly string ConnectionString;
        private static readonly string UserRolesCacheKey = CacheKeys.UserRolesCacheKey;
        
        private readonly IConfiguration Configuration;
        private readonly IMemoryCache MemoryCache;

        public UserRoleRepository(
            IConfiguration configuration,
            IMemoryCache memoryCache)
        {
            Configuration = configuration;
            MemoryCache = memoryCache;
            ConnectionString = Configuration.GetConnectionString("Sqlite");
        }

        public async Task<List<UserRole>> List()
        {
            if (!MemoryCache.TryGetValue(UserRolesCacheKey, out List<UserRole> cacheEntry))
            {
                var cacheEntryOptions = new MemoryCacheEntryOptions() 
                    .SetSize(1) 
                    .SetSlidingExpiration(TimeSpan.FromMinutes(3));

                const string sql = @"SELECT * FROM UserRoles ORDER BY DisplayTitle, Name";

                using (IDbConnection db = new SqliteConnection(ConnectionString))
                {
                    var roles = await db.QueryAsync<UserRole>(sql);
                    cacheEntry = roles.ToList();
                    MemoryCache.Set(UserRolesCacheKey, cacheEntry, cacheEntryOptions);
                }
            }

            return cacheEntry;
        }
    }
}