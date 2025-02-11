using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.ComponentModel.DataAnnotations;  // Add this for validation

namespace ImageGallery.Models
{
    public class UserModel
    {
        [BsonId]
        public ObjectId Id { get; set; }

        [Required(ErrorMessage = "Username is required")]
        public string? Username { get; set; }

        [Required(ErrorMessage = "Password is required")]
        public string? Password { get; set; } // In a real application, use hashed passwords

            public string? Role { get; set; } // This will store the role, e.g., Admin, NormalUser, or Guest
    }
}
