using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson;
using System;

namespace Bkl.Models.MongoEntity
{
    public class MdEntityBase
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        [BsonElement("Createtime")]
        public DateTime Createtime { get; set; }

        public MdEntityBase()
        {
            Createtime = DateTime.UtcNow;
        }

    }
}
