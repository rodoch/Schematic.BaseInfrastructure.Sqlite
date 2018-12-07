using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Schematic.Core;
using Schematic.Identity;

namespace Schematic.BaseInfrastructure.Sqlite
{
    public class CachedUserRoleRepository : UserRoleRepository, IUserRoleRepository<UserRole>
    {
        private readonly IMemoryCache _cache;
        private readonly IUserCacheKeyService _cacheKeys;

        public CachedUserRoleRepository(
            IConfiguration configuration,
            IMemoryCache cache,
            IUserCacheKeyService cacheKeys) : base(configuration)
        {
            _cache = cache;
            _cacheKeys = cacheKeys;
        }

        public new async Task<List<UserRole>> ListAsync()
        {
            string cacheKey = _cacheKeys.GetUserRoleListCacheKey();

            if (!_cache.TryGetValue(cacheKey, out List<UserRole> cacheEntry))
            {
                var cacheEntryOptions = new MemoryCacheEntryOptions()
                    .SetSize(1)
                    .SetSlidingExpiration(TimeSpan.FromMinutes(5));

                cacheEntry = await base.ListAsync();
                
                _cache.Set(cacheKey, cacheEntry, cacheEntryOptions);
            }

            return cacheEntry;
        }
    }
}