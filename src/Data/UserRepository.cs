using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Dapper;
using Schematic.Core;
using Schematic.Identity;

namespace Schematic.BaseInfrastructure.Sqlite
{
    public class UserRepository : IUserRepository<User, UserFilter, UserSpecification>
    {
        private readonly IOptionsMonitor<SchematicSettings> _settings;
        private readonly IUserRoleRepository<UserRole> _roleRepository;
        private readonly string _connectionString;

        public UserRepository(
            IOptionsMonitor<SchematicSettings> settings,
            IUserRoleRepository<UserRole> roleRepository)
        {
            _settings = settings;
            _roleRepository = roleRepository;
            _connectionString = _settings.CurrentValue.DataStore.ConnectionString;
        }

        public async Task<int> CreateAsync(User resource, string token, int userID)
        {
            using (var db = new SqliteConnection(_connectionString))
            {
                db.Open();

                using (var transaction = db.BeginTransaction())
                {
                    var dateCreated = DateTime.UtcNow;

                    var createUser = await db.QueryAsync<int>(@"INSERT INTO Users (Forenames, Surnames, Email, DateCreated, CreatedBy)
                            VALUES (@Forenames, @Surnames, @Email, @DateCreated, @CreatedBy);" +
                            "SELECT last_insert_rowid();", 
                        new { Forenames = resource.Forenames, Surnames = resource.Surnames, Email = resource.Email, 
                            PassHash = resource.PassHash, DateCreated = dateCreated, CreatedBy = userID });
                            
                    int userId = createUser.FirstOrDefault();

                    await db.ExecuteAsync(@"INSERT INTO UsersPasswordToken (UserID, Email, Token, DateCreated) 
                            VALUES (@UserID, @Email, @Token, @DateCreated)",
                        new { UserID = userId, Email = resource.Email, Token = token, DateCreated = dateCreated });

                    foreach (var role in resource.Roles.Where(r => r.HasRole))
                    {
                        await db.ExecuteAsync(@"INSERT INTO UsersUserRole (UserID, RoleID, DateCreated, CreatedBy) 
                                VALUES (@UserID, @RoleID, @DateCreated, @CreatedBy)", 
                            new { UserID = userId, RoleID = role.ID, DateCreated = dateCreated, CreatedBy = userID });
                    }

                    transaction.Commit();
                    return userId;
                }
            }
        }

        public async Task<User> ReadAsync(UserSpecification userSpecification)
        {
            var builder = new SqlBuilder();
            var template = builder.AddTemplate(@"SELECT * FROM Users /**where**/;");

            if (userSpecification.Email.HasValue())
            {
                builder.Where("Email = @Email", new { userSpecification.Email });
            }
            else
            {
                builder.Where("ID = @ID", new { userSpecification.ID });
            }

            const string roleSql = @"SELECT * FROM UserRoles ORDER BY DisplayTitle, Name;
                SELECT r.ID FROM UserRoles AS r 
                LEFT JOIN UsersUserRole AS ur ON ur.RoleID = r.ID 
                WHERE ur.UserID = @ID;";

            using (var db = new SqliteConnection(_connectionString))
            {   
                var readUser = await db.QueryAsync<User>(template.RawSql, template.Parameters);
                var user = readUser.FirstOrDefault();

                if (user != null)
                {
                    using (var multi = await db.QueryMultipleAsync(roleSql, new { ID = user.ID }))
                    {
                        var roles = multi.Read<UserRole>().ToList();
                        var userRoleIDs = multi.Read<int>();

                        foreach (var role in roles)
                        {
                            role.HasRole = false;
                        }

                        foreach (int roleID in userRoleIDs)
                        {
                            roles.Find(r => r.ID == roleID).HasRole = true;
                        }

                        user.Roles = roles;
                    }
                }

                return user;
            }
        }

        public async Task<int> UpdateAsync(User resource, int userID)
        {
            const string sql = @"UPDATE Users 
                SET Forenames = @Forenames, Surnames = @Surnames, Email = @Email, PassHash = @PassHash WHERE ID = @ID;
                DELETE FROM UsersUserRole WHERE UserID = @ID;";

            using (var db = new SqliteConnection(_connectionString))
            {
                db.Open();

                using (var transaction = db.BeginTransaction())
                {
                    int updatedUser = await db.ExecuteAsync(sql, new { ID = resource.ID, Forenames = resource.Forenames, 
                            Surnames = resource.Surnames, Email = resource.Email, PassHash = resource.PassHash });

                    foreach (var role in resource.Roles.Where(r => r.HasRole))
                    {
                        await db.ExecuteAsync(@"INSERT INTO UsersUserRole (UserID, RoleID, DateCreated, CreatedBy) 
                                VALUES (@UserID, @RoleID, @DateCreated, @CreatedBy)", 
                            new { UserID = resource.ID, RoleID = role.ID, DateCreated = DateTime.UtcNow, CreatedBy = userID });
                    }

                    transaction.Commit();
                    return updatedUser;
                }
            }
        }

        public async Task<int> DeleteAsync(int id, int userID)
        {
            const string sql = @"DELETE FROM Users WHERE ID = @ID";

            using (var db = new SqliteConnection(_connectionString))
            {
                return await db.ExecuteAsync(sql, new { ID = id });
            }
        }

        public async Task<List<User>> ListAsync(UserFilter filter)
        {
            var builder = new SqlBuilder();

            var template = builder.AddTemplate(@"SELECT * FROM Users
                /**where**/
                ORDER BY Forenames, Surnames, ID;");

            if (filter.SearchQuery.HasValue())
            {
                builder.OrWhere("Forenames LIKE @Query");
                builder.OrWhere("Surnames LIKE @Query");
            }

            using (var db = new SqliteConnection(_connectionString))
            {
                var users = await db.QueryAsync<User>(template.RawSql, 
                    new { Query = filter.SearchQuery + "%" });
                return users.ToList();
            }
        }

        public async Task<bool> SaveTokenAsync(User resource, string token)
        {
            const string sql = @"INSERT INTO UsersPasswordToken (UserID, Email, Token, NotificationsSent, DateCreated) 
                VALUES (@UserID, @Email, @Token, @NotificationsSent, @DateCreated)";

            using (var db = new SqliteConnection(_connectionString))
            {
                var tokenSaved = await db.ExecuteAsync(sql, new { UserID = resource.ID, Email = resource.Email, Token = token, 
                    NotificationsSent = 0, DateCreated = DateTime.UtcNow });
                return (tokenSaved > 0) ? true : false; 
            }
        }

        public async Task<TokenVerificationResult> ValidateTokenAsync(string email, string token)
        {
            const string sql = @"SELECT ID, DateCreated FROM UsersPasswordToken 
                WHERE Email = @Email AND Token = @Token LIMIT 1";

            using (var db = new SqliteConnection(_connectionString))
            {
                var result = await db.QueryAsync(sql, new { Email = email, Token = token });
                var tokenResult = result.FirstOrDefault();

                if (tokenResult == null || tokenResult.Count() == 0)
                {
                    return TokenVerificationResult.Invalid;
                }

                var timeCreated = DateTime.Parse(tokenResult.DateCreated);
                var timeLimit = _settings.CurrentValue.SetPasswordTimeLimitHours;

                if (timeCreated < DateTime.UtcNow.Subtract(TimeSpan.FromHours(timeLimit)))
                {
                    return TokenVerificationResult.Expired;
                }
                else
                {
                    return TokenVerificationResult.Success;
                }
            }
        }

        public async Task<bool> SetPasswordAsync(User resource, string passHash)
        {
            const string sql = @"UPDATE Users SET PassHash = @PassHash WHERE ID = @ID";

            using (var db = new SqliteConnection(_connectionString))
            {
                var passwordSet = await db.ExecuteAsync(sql, new { ID = resource.ID, PassHash = passHash });
                return (passwordSet > 0) ? true : false; 
            }
        }
    }
}