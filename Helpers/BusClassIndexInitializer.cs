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
            var indexOptions = new CreateIndexOptions<BusClass>
            {
                Name = "ux_busclass_classNameKey_active",
                Unique = true,
                PartialFilterExpression = Builders<BusClass>.Filter.And(
                    BusClassFilters.NotDeleted,
                    Builders<BusClass>.Filter.Type(bc => bc.ClassNameKey, BsonType.String))
            };

            await collection.Indexes.CreateOneAsync(new CreateIndexModel<BusClass>(indexKeys, indexOptions));
        }
        catch (MongoCommandException ex) when (ex.CodeName is "IndexOptionsConflict" or "IndexKeySpecsConflict")
        {
            // Index already exists with compatible definition.
        }
        catch (MongoCommandException ex) when (ex.Code == 11000)
        {
            Console.WriteLine(
                "BusClasses: không tạo được unique index vì đang có tên loại xe trùng. Xóa bản ghi trùng rồi khởi động lại.");
        }
    }

    private static async Task BackfillClassNameKeysAsync(IMongoCollection<BusClass> collection)
    {
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
