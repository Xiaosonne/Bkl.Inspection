using System;

namespace Bkl.Models.MongoEntity
{
    public class MdCollectionName : Attribute
    {
        public MdCollectionName(string name)
        {
            Name = name;
        }

        public string Name { get; set; }
    }
}
