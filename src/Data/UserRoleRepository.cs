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
        protected readonly IConfiguration Configuration;
        protected readonly string ConnectionString;

        public UserRoleRepository(IConfiguration configuration)
        {
            Configuration = configuration;
            ConnectionString = Configuration.GetConnectionString("Sqlite");
        }

        public async Task<List<UserRole>> ListAsync()
        {
            const string sql = @"SELECT * FROM UserRoles ORDER BY DisplayTitle, Name";

            using (IDbConnection db = new SqliteConnection(ConnectionString))
            {
                var roles = await db.QueryAsync<UserRole>(sql);
                return roles.ToList();
            }
        }
    }
}