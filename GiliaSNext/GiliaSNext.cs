using GiliaSNext.Config;
using GiliaSNext.Ilias;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace GiliaSNext
{
    class GiliaSNext
    {
        private static readonly ConfigBuilder Builder = ConfigBuilder.Instance();
        private static Ilias.Ilias Ilias;

        static void Main(string[] args)
        {
            AppDomain.CurrentDomain.ProcessExit += new EventHandler(OnProcessExit);
            Builder.Load();
            Ilias = new Ilias.Ilias(Builder);
            var iliasTask = Test();
            var gitTask = GitRepos();
            iliasTask.Wait();
            gitTask.Wait();
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
                var repo = new Git.Git(url, Builder.Config.DownloadPath, Builder.Config.GitUser, Builder.Config.GitPassword, Builder.Config.GitEmail);
                Task repoTask = new Task(() => repo.ClonePull());
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