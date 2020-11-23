using GiliaSNext.Config;
using GiliaSNext.Git;
using GiliaSNext.Ilias;
using GiliaSNext.Svn;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace GiliaSNext
{
    class GiliaSNext
    {
        private static readonly ConfigBuilder Builder = ConfigBuilder.Instance();
        private static IliasClient Ilias;

        static void Main(string[] args)
        {
            AppDomain.CurrentDomain.ProcessExit += new EventHandler(OnProcessExit);
            Builder.Load();
            Ilias = new IliasClient(Builder);
            var iliasTask = Test();
            var gitTask = GitRepos();
            var svnTask = SvnRepos();
            iliasTask.Wait();
            gitTask.Wait();
            svnTask.Wait();
        }

        static async Task Test()
        {
            int login = await Ilias.Login();
            if (login == 0)
            {
                var rssDownload = Ilias.RssDownload(Builder.Config.DownloadPath);
                var exDownload = Ilias.ExerciseDownload(Builder.Config.DownloadPath);
                await Task.WhenAll(rssDownload, exDownload);
            }
        }

        static async Task GitRepos()
        {
            var taskList = new List<Task>();
            foreach (string url in Builder.Config.GitReposHttp)
            {
                var repo = new GitClient(url, Builder.Config.DownloadPath, Builder.Config.GitUser, Builder.Config.GitPassword, Builder.Config.GitEmail);
                Task repoTask = new Task(() => repo.ClonePull());
                repoTask.Start();
                taskList.Add(repoTask);
            }
            foreach (Task task in taskList)
            {
                await task;
            }
        }

        static async Task SvnRepos()
        {
            var taskList = new List<Task>();
            foreach (string url in Builder.Config.SvnReposHttp)
            {
                var repo = new SvnClient(url, Builder.Config.DownloadPath, Builder.Config.SvnUser, Builder.Config.GitPassword, Builder.Config.SvnEmail);
                Task repoTask = new Task(() => repo.CheckoutUpdate());
                repoTask.Start();
                taskList.Add(repoTask);
            }
            foreach (Task task in taskList)
            {
                await task;
            }
        }

        static void OnProcessExit(object sender, EventArgs e)
        {
            Ilias.SaveJsonFile();
        }
    }
}