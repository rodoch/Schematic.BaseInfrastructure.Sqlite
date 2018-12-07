using Schematic.Identity;

namespace Schematic.BaseInfrastructure.Sqlite
{
    public class UserCacheKeyService : IUserCacheKeyService
    {
        public string GetUserRoleListCacheKey() => "Schematic__UserRoleList";
    }
}