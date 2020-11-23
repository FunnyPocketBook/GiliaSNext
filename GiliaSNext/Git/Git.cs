using LibGit2Sharp;
using LibGit2Sharp.Handlers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Text;
using System.Threading.Tasks;

namespace GiliaSNext.Git
{
    class Git
    {
        private readonly string Url;
        private readonly string User;
        private readonly string Password;
        private readonly string PathToRepo;
        private readonly string Email;

        public Git(string repository, string path, string user, string password, string email)
        {
            Url = repository;
            PathToRepo = path;
            User = user;
            Password = password;
            Email = email;
        }

        public string Clone()
        {
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
                return Repository.Clone(Url, PathToRepo, cloneOptions);
            }
            catch (RecurseSubmodulesException)
            {
                return "";
            }
            catch (UserCancelledException)
            {
                return "";
            }
        }

        private void Pull(Repository repo)
        {
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
        }

        public void ClonePull(string path)
        {
            if (Repository.IsValid(path))
            {
                Pull(new Repository(path));
            }
            else
            {
                Clone();
            }
        }
    }
}
