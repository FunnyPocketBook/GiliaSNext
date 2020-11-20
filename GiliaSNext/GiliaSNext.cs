using GiliaSNext.Config;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace GiliaSNext
{
    class GiliaSNext
    {
        private static ConfigBuilder Builder = ConfigBuilder.Instance();
        private static Ilias.Ilias Ilias;

        static void Main(string[] args)
        {
            Builder.Load();
            Ilias = new Ilias.Ilias(Builder);
            Test().Wait();
        }

        static async Task Test()
        {
            await Ilias.Login();
            var rssXml = await Ilias.GetRssXml();
            var rssFiles = Ilias.GetFilesFromXml(rssXml);
            await Ilias.DownloadRssFiles(Builder.Config.DownloadPath, rssFiles);
            await DownloadEx();
        }

        static async Task<object> DownloadEx()
        {
            List<Uri> exUrls = await Ilias.GetExerciseUrls();
            List<Uri> combinedExUrls = new List<Uri>();
            List<Uri> combinedFeedUrls = new List<Uri>();
            foreach (Uri url in exUrls)
            {
                List<Uri>[] exFileUrls = await Ilias.GetExerciseFileUrls(url);
                combinedExUrls.AddRange(exFileUrls[0]);
                combinedFeedUrls.AddRange(exFileUrls[1]);
            }
            await Ilias.DownloadFiles(Path.Combine(Builder.Config.DownloadPath, "exercises"), combinedExUrls);
            await Ilias.DownloadFiles(Path.Combine(Builder.Config.DownloadPath, "feedback"), combinedFeedUrls);
            return null;
        }
    }
}