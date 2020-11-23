using GiliaSNext.Config;
using GiliaSNext.Ilias;
using System;
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
            Test().Wait();
            //TestSingleFile().Wait();
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

        static async Task TestSingleFile()
        {
            string[] subfolders = new string[] { "Data Visualization- Advanced Topics", "Literature", "lecture03" };
            Uri url = new Uri("https://ilias.uni-konstanz.de/ilias/goto_ilias_uni_file_1123455_download.html");
            IliasFile a = new IliasFile(subfolders, "Scatterplot-Splatterplot.pdf", 1123455, url, DateTime.Parse("2020-11-16T09:00:13+01:00"), DateTime.Parse("2020-11-16T09:38:14+01:00"));
            await Ilias.Login();
            await Ilias.DownloadFile(Builder.Config.DownloadPath, a);
        }

        static void OnProcessExit(object sender, EventArgs e)
        {
            Ilias.SaveJsonFile();
        }
    }
}