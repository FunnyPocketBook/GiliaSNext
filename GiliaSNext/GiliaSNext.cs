using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using GiliaSNext.Config;
using HtmlAgilityPack;

namespace GiliaSNext
{
    class GiliaSNext
    {
        private static ConfigBuilder Builder = ConfigBuilder.Instance();
        private static Ilias Ilias;

        static void Main(string[] args)
        {
            Builder.Load();
            Ilias = new Ilias(Builder.Config.User, Builder.Config.PasswordIlias);
            Test().Wait();
        }

        static async Task Test()
        {
            await Ilias.Login();
            await Ilias.GetRssFeed(Builder.Config.RssUrl, Builder.Config.User, Builder.Config.PasswordRss);
            await Ilias.DownloadFile("1126607", "C:\\Users\\dangy\\Desktop\\bla.pdf");
        }
    }
}