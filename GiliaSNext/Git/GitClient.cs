using LibGit2Sharp;
using LibGit2Sharp.Handlers;
using System;
using System.IO;

namespace GiliaSNext.Git
{
    class GitClient
    {
        private readonly string Url;
        private readonly string User;
        private readonly string Password;
        private readonly string ParentDir;
        private readonly string Email;

        private const string GIT_TAG = "[Git]";
        public GitClient(string repository, string path, string user, string password, string email)
        {
            Url = repository;
            ParentDir = path;
            User = user;
            Password = password;
            Email = email;
        }

        public string Name
        {
            get { return Path.GetFileNameWithoutExtension(Url); }
        }

        public string WorkingDir
        {
            get { return Path.Combine(ParentDir, Name); }
        }

        public void Clone()
        {
            Console.WriteLine($"{GIT_TAG} Cloning repo {Name}.");
            var cloneOptions = new CloneOptions()
            {
                CredentialsProvider = (_url, _user, _cred) => new UsernamePasswordCredentials
                {
                    Username = User,
                    Password = Password
                }
            };
            try
            {
                Repository.Clone(Url, WorkingDir, cloneOptions);
                Console.WriteLine($"{GIT_TAG} Cloned repo {Name}.");
            }
            catch (RecurseSubmodulesException)
            {
                Console.WriteLine($"{GIT_TAG} Couldn't clone repo {Name}.");
                throw;
            }
            catch (UserCancelledException)
            {
                Console.WriteLine($"{GIT_TAG} Couldn't clone repo {Name}.");
                throw;
            }
        }

        private void Pull(Repository repo)
        {
            Console.WriteLine($"{GIT_TAG} Pulling repo {Name}.");
            PullOptions options = new PullOptions
            {
                FetchOptions = new FetchOptions
                {
                    CredentialsProvider = new CredentialsHandler(
                        (url, usernameFromUrl, types) =>
                        new UsernamePasswordCredentials()
                        {
                            Username = User,
                            Password = Password
                        })
                }
            };

            // User information to create a merge commit
            var signature = new Signature(
                new Identity(User, Email), DateTimeOffset.Now);

            // Pull
            Commands.Pull(repo, signature, options);
            Console.WriteLine($"{GIT_TAG} Pulled repo {Name}.");
        }

        public void ClonePull()
        {
            if (Repository.IsValid(WorkingDir))
            {
                Pull(new Repository(WorkingDir));
            }
            else
            {
                Clone();
            }
        }
    }
}
