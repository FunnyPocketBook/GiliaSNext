using Newtonsoft.Json;
using System;
using System.IO;
using System.Linq;

namespace GiliaSNext.Ilias
{
    class IliasFile : IEquatable<IliasFile>, IComparable<IliasFile>
    {
        public string[] Subfolders { get; set; }
        public string Name { get; set; }
        public int Id { get; set; }
        public DateTime Date { get; set; }
        public DateTime LastModified { get; set; }

        public IliasFile(string[] subfolders, string name, int id, DateTime date)
        {
            Subfolders = subfolders;
            Name = name;
            Id = id;
            Date = date;
        }

        public IliasFile()
        {

        }

        public string Course
        {
            get { return Subfolders[0]; }
        }

        public string Extension
        {
            get { return Path.GetExtension(Name); }
        }

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this, Formatting.Indented);
        }

        public bool Equals(IliasFile other)
        {
            if (other == null)
                return false;
            return this.Subfolders.SequenceEqual(other.Subfolders) &&
                this.Name == other.Name && this.Id == other.Id &&
                this.Date.CompareTo(other.Date) == 0 && 
                this.LastModified.CompareTo(other.LastModified) == 0;
        }

        public int CompareTo(IliasFile other)
        {
            return Id.CompareTo(other.Id);
        }
    }
}
