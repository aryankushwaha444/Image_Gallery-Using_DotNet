using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace ImageGallery.Models
{
    public class ImageModel
    {
        [BsonId]
        public required ObjectId Id { get; set; }
        public required string? Title { get; set; }
        public required string? Url { get; set; }
        public required string? Description { get; set; }
    }
}
