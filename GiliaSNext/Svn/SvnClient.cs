using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace GiliaSNext.Svn
{
    class SvnClient
    {
        private readonly SharpSvn.SvnUriTarget Url;
        private readonly string User;
        private readonly string Password;
        private readonly string ParentDir;
        private readonly string Email;
        private readonly SharpSvn.SvnClient Client;

        private const string SVN_TAG = "[SVN]";

        public SvnClient(string repository, string path, string user, string password, string email)
        {
            Url = new SharpSvn.SvnUriTarget(repository);
            ParentDir = path;
            User = user;
            Password = password;
            Email = email;
            Client = new SharpSvn.SvnClient();
            Client.Authentication.DefaultCredentials = new NetworkCredential(User, Password);
        }

        public string Name
        {
            get { return Url.Uri.LocalPath[1..].Replace("/", Path.DirectorySeparatorChar.ToString()); }
        }

        public string WorkingDir
        {
            get { return Path.Combine(ParentDir, Name); }
        }

        public void CheckoutUpdate()
        {
            var statusArgs = new SharpSvn.SvnStatusArgs()
            {
                RetrieveAllEntries = true
            };
            Collection<SharpSvn.SvnStatusEventArgs> statuses;
            try
            {
                Client.GetStatus(WorkingDir, statusArgs, out statuses);
                Console.WriteLine($"{SVN_TAG} Pulling repo {Name}.");
                Client.Update(WorkingDir);
                Console.WriteLine($"{SVN_TAG} Pulled repo {Name}.");
            }
            catch (SharpSvn.SvnInvalidNodeKindException)
            {
                Console.WriteLine($"{SVN_TAG} Checking out repo {Name}.");
                Client.CheckOut(Url, WorkingDir);
                Console.WriteLine($"{SVN_TAG} Checked out repo {Name}.");
            }
        }
    }
}
