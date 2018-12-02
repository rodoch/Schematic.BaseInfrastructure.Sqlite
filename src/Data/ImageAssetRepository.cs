using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Dapper;
using Schematic.Core;

namespace Schematic.BaseInfrastructure.Sqlite
{
    public class ImageAssetRepository : IImageAssetRepository
    {
        private readonly string _connectionString;

        public ImageAssetRepository(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("Sqlite");
        }

        public async Task<int> Create(ImageAsset asset, int userID)
        {
            const string sql = @"INSERT INTO ImageAssets (FileName, ContentType, Height, Width, AltText, Title, DateCreated, CreatedBy) 
                VALUES (@FileName, @ContentType, @Width, @Height, @AltText, @Title, @DateCreated, @CreatedBy);
                SELECT last_insert_rowid();";

            using (IDbConnection db = new SqliteConnection(_connectionString))
            {
                var imageID = await db.QueryAsync<int>(sql, new { FileName = asset.FileName, ContentType = asset.ContentType, 
                   Height = asset.Height, Width = asset.Width, AltText = asset.AltText, Title = asset.Title, 
                   DateCreated = asset.DateCreated, CreatedBy = asset.CreatedBy });
                return imageID.FirstOrDefault();
            }
        }

        public async Task<int> Delete(int id, int userID)
        {
            const string sql = @"DELETE FROM ImageAssets WHERE ID = @ID";

            using (IDbConnection db = new SqliteConnection(_connectionString))
            {
                return await db.ExecuteAsync(sql, new { ID = id });
            }
        }

        public async Task<ImageAsset> Read(int id)
        {
            const string sql = @"SELECT * FROM ImageAssets WHERE FileName = @FileName LIMIT 1";

            using (IDbConnection db = new SqliteConnection(_connectionString))
            {
                var results = await db.QueryAsync<ImageAsset>(sql, new { ID = id });
                return results.FirstOrDefault();
            }
        }

        public async Task<int> Update(ImageAsset asset, int userID)
        {
            const string sql = @"UPDATE SET FileName = @FileName, ContentType = @ContentType, 
                    Height = @Height, Width = @Width, AltText = @AltText, Title = @Title WHERE ID = @ID";

            using (IDbConnection db = new SqliteConnection(_connectionString))
            {
                return await db.ExecuteAsync(sql, new { ID = asset.ID, FileName = asset.FileName, ContentType = asset.ContentType,
                   Height = asset.Height, Width = asset.Width, AltText = asset.AltText, Title = asset.Title });
            }
        }
    }
}