using System;
using System.Threading.Tasks;
using Bus_ticket.Models;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Bus_ticket.Helpers;

public static class BusClassIndexInitializer
{
    public static async Task EnsureIndexesAsync(IMongoCollection<BusClass> collection)
    {
        await BackfillClassNameKeysAsync(collection);

        try
        {
            var indexKeys = Builders<BusClass>.IndexKeys.Ascending(bc => bc.ClassNameKey);
            
            // SỬA LẠI ĐOẠN NÀY: Dùng Eq(bc => bc.DeletedAt, BsonNull.Value) thay cho BusClassFilters.NotDeleted
            var indexOptions = new CreateIndexOptions<BusClass>
            {
                Name = "ux_busclass_classNameKey_active",
                Unique = true,
                PartialFilterExpression = Builders<BusClass>.Filter.And(
                    Builders<BusClass>.Filter.Eq<DateTime?>(bc => bc.DeletedAt, null),
                    Builders<BusClass>.Filter.Type(bc => bc.ClassNameKey, BsonType.String) // Đảm bảo trường ClassNameKey là kiểu String
                )
            };

            await collection.Indexes.CreateOneAsync(new CreateIndexModel<BusClass>(indexKeys, indexOptions));
        }
        catch (MongoCommandException ex) when (ex.CodeName is "IndexOptionsConflict" or "IndexKeySpecsConflict")
        {
            // Index đã tồn tại với cấu hình tương thích, bỏ qua.
        }
        catch (MongoCommandException ex) when (ex.Code == 11000)
        {
            Console.WriteLine(
                "BusClasses: không tạo được unique index vì đang có tên loại xe trùng. Xóa bản ghi trùng rồi khởi động lại.");
        }
    }

    private static async Task BackfillClassNameKeysAsync(IMongoCollection<BusClass> collection)
    {
        // Khi backfill dữ liệu cũ chưa có key, bạn vẫn có thể giữ nguyên lọc này vì đây là Query thông thường, không phải Partial Index
        var missingKeyFilter = Builders<BusClass>.Filter.And(
            BusClassFilters.NotDeleted,
            Builders<BusClass>.Filter.Or(
                Builders<BusClass>.Filter.Eq(bc => bc.ClassNameKey, null),
                Builders<BusClass>.Filter.Exists(bc => bc.ClassNameKey, false)));

        var cursor = await collection.Find(missingKeyFilter).ToCursorAsync();
        while (await cursor.MoveNextAsync())
        {
            foreach (var busClass in cursor.Current)
            {
                if (string.IsNullOrWhiteSpace(busClass.ClassName))
                {
                    continue;
                }

                var key = BusClassNameHelper.NormalizeKey(busClass.ClassName);
                await collection.UpdateOneAsync(
                    Builders<BusClass>.Filter.Eq(bc => bc.Id, busClass.Id),
                    Builders<BusClass>.Update.Set(bc => bc.ClassNameKey, key));
            }
        }
    }
}