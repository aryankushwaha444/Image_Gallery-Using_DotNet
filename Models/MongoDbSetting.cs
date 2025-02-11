using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ImageGallery.Models
{
    public class MongoDbSetting
    {
     public string ConnectionString { get; set; } = null!;
        public string DatabaseName { get; set; } = null!;
    }
}