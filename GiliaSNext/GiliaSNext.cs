using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using GiliaSNext.Config;
using GiliaSNext.Helper;
using HtmlAgilityPack;

namespace GiliaSNext
{
    class GiliaSNext
    {
        private static ConfigBuilder builder = ConfigBuilder.Instance();
        private static HttpClientHandler handler = new HttpClientHandler();
        private static HttpClient client = new HttpClient(handler);
        private static Uri iliasLoginUrl;
        private static readonly Uri ILIAS_URI = new Uri("https://ilias.uni-konstanz.de/");

        static void Main(string[] args)
        {
            builder.Load();
            client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/86.0.4240.183 Safari/537.36");
            client.DefaultRequestHeaders.Add("Upgrade-Insecure-Requests", "1");
            GetLoginLink().Wait();
            IliasLogin().Wait();
            TestLogin().Wait();
        }

        static async Task<int> IliasLogin()
        {
            Dictionary<string, string> postParamsDict = new Dictionary<string, string>
            {
                { "username", builder.Config.User },
                { "password", builder.Config.PasswordIlias }
            };
            FormUrlEncodedContent postParams = new FormUrlEncodedContent(postParamsDict);

            Console.WriteLine("Logging in...");
            try
            {
                HttpResponseMessage response = await client.PostAsync(iliasLoginUrl, postParams);
                if (response.StatusCode.CompareTo(HttpStatusCode.OK) != 0)
                {
                    Console.WriteLine("[Ilias] Status Code {0}", response.StatusCode);
                    return -1;
                }
                Console.WriteLine("Logged in");
                return 0;
            }
            catch (TaskCanceledException e)
            {
                Console.WriteLine("[Ilias] Login timed out.");
                Console.WriteLine(e.InnerException.Message);
                return -1;
            }
        }

        static async Task<int> TestLogin()
        {
            Console.WriteLine("Test");
            try
            {
                HttpResponseMessage response = await client.GetAsync("https://ilias.uni-konstanz.de/ilias/goto_ilias_uni_file_1082633_download.html");
                string pageContents = await response.Content.ReadAsStringAsync();
                HtmlDocument pageDocument = new HtmlDocument();
                pageDocument.LoadHtml(pageContents);
                Console.WriteLine(pageDocument.DocumentNode.InnerText);
                return 0;
            }
            catch (TaskCanceledException e)
            {
                Console.WriteLine("[Ilias] Fetching login URL timed out.");
                Console.WriteLine(e.InnerException.Message);
                return -1;
            }
        }

        static async Task<int> GetLoginLink()
        {
            HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 6.3; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/38.0.2125.111 Safari/537.36");
            try
            {
                HttpResponseMessage response = await client.GetAsync("https://ilias.uni-konstanz.de/ilias/login.php");
                string pageContents = await response.Content.ReadAsStringAsync();
                HtmlDocument pageDocument = new HtmlDocument();
                pageDocument.LoadHtml(pageContents);
                HtmlNode form = pageDocument.DocumentNode.SelectSingleNode("//form[@id='form_']");
                iliasLoginUrl = new Uri("https://ilias.uni-konstanz.de/ilias/" + WebUtility.HtmlDecode(form.Attributes["action"].Value));
                Console.WriteLine(iliasLoginUrl);
                return 0;
            }
            catch (TaskCanceledException e)
            {
                Console.WriteLine("[Ilias] Fetching login URL timed out.");
                Console.WriteLine(e.InnerException.Message);
                return -1;
            }
        }
        public static CookieContainer ReadCookies(HttpResponseMessage response)
        {
            Uri pageUri = response.RequestMessage.RequestUri;
            var cookieContainer = new CookieContainer();
            IEnumerable<string> cookies;
            if (response.Headers.TryGetValues("set-cookie", out cookies))
            {
                int counter = 0;
                foreach (var c in cookies)
                {
                    if (counter == 2)
                    {
                        break;
                    }
                    cookieContainer.SetCookies(ILIAS_URI, c.Replace("/ilias/", "/"));
                    counter++;
                    /*var tempContainer = new CookieContainer();
                    tempContainer.SetCookies(ILIAS_URI, c.Replace("/ilias/", "/"));
                    CookieCollection cColl = tempContainer.GetCookies(ILIAS_URI);
                    cookieContainer.Add(cColl[0]);*/
                }
            }
            Cookies.PrintCookies(cookieContainer);
            return cookieContainer;
        }

    }
}