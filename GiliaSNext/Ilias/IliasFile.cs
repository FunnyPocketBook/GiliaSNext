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
        public Uri Url { get; set; }
        public DateTime Date { get; set; }
        public DateTime LastModified { get; set; }

        public IliasFile(string[] subfolders, string name, int id, Uri url, DateTime date = new DateTime(), DateTime lastModified = new DateTime())
        {
            Subfolders = subfolders;
            Name = name;
            Id = id;
            Url = url;
            Date = date;
            LastModified = lastModified;
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
            return Subfolders.SequenceEqual(other.Subfolders) &&
                Name == other.Name && Id == other.Id &&
                Url == other.Url && Date.CompareTo(other.Date) == 0 &&
                LastModified.CompareTo(other.LastModified) == 0;
        }

        public bool Matches(IliasFile other)
        {
            if (other == null)
                return false;
            return Subfolders.SequenceEqual(other.Subfolders) &&
                Name == other.Name && Id == other.Id &&
                Url == other.Url;
        }

        public int CompareTo(IliasFile other)
        {
            return Id.CompareTo(other.Id);
        }
    }
}
