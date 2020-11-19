using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml;
using System.ServiceModel.Syndication;
using HtmlAgilityPack;
using System.Text;
using System.Diagnostics;
using System.IO;

namespace GiliaSNext
{
    class Ilias
    {
        private HttpClientHandler HttpHandler;
        private HttpClient Client;
        private Uri LoginUrl;
        private string User;
        private string Password;
        
        public Ilias(string user, string password)
        {
            User = user;
            Password = password;
            HttpHandler = new HttpClientHandler();
            Client = new HttpClient(HttpHandler);
            HttpHandler.AllowAutoRedirect = true;
            Client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/86.0.4240.183 Safari/537.36");
            Client.DefaultRequestHeaders.Add("Upgrade-Insecure-Requests", "1");
        }

        public async Task<int> Login()
        {
            LoginUrl = await GetLoginLink();
            var postParamsDict = new Dictionary<string, string>
            {
                { "username", User },
                { "password", Password },
                { "cmd[doStandardAuthentication]", "Anmelden" }
            };
            FormUrlEncodedContent postParams = new FormUrlEncodedContent(postParamsDict);

            Console.WriteLine("[Ilias] Logging in...");
            try
            {
                HttpResponseMessage response = await Client.PostAsync(LoginUrl, postParams);
                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine("[Ilias] Status Code {0}", response.StatusCode);
                    return -1;
                }
                Console.WriteLine("[Ilias] Logged in");
                return 0;
            }
            catch (TaskCanceledException e)
            {
                Console.WriteLine("[Ilias] Login timed out.");
                Console.WriteLine(e.InnerException.Message);
                return -1;
            }
        }

        public async Task<Uri> GetLoginLink()
        {
            try
            {
                HttpResponseMessage response = await Client.GetAsync("https://ilias.uni-konstanz.de/ilias/login.php");
                string pageContents = await response.Content.ReadAsStringAsync();
                var pageDocument = new HtmlDocument();
                pageDocument.LoadHtml(pageContents);
                HtmlNode form = pageDocument.DocumentNode.SelectSingleNode("//form[@id='form_']");
                return new Uri("https://ilias.uni-konstanz.de/ilias/" + WebUtility.HtmlDecode(form.Attributes["action"].Value) + "&client_id=ilias_uni");
            }
            catch (TaskCanceledException e)
            {
                Console.WriteLine("[Ilias] Fetching login URL timed out.");
                Console.WriteLine(e.InnerException.Message);
                return null;
            }
        }

        public async Task<XmlDocument> GetRssFeed(string rssString, string user, string password)
        {
            byte[] auth = Encoding.ASCII.GetBytes(user + ":" + password);
            Client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", Convert.ToBase64String(auth));
            try
            {
                Console.WriteLine("[Ilias] Loading RSS feed...");
                var watch = new Stopwatch();
                watch.Start();
                HttpResponseMessage response = await Client.GetAsync(rssString);
                Client.DefaultRequestHeaders.Authorization = null;
                watch.Stop();
                Console.WriteLine($"[Ilias] It took {watch.ElapsedMilliseconds / 1000.0} seconds to load the feed.");
                string pageContents = await response.Content.ReadAsStringAsync();
                var xml = new XmlDocument();
                xml.LoadXml(pageContents);
                return xml;
            }
            catch (TaskCanceledException e)
            {
                Client.DefaultRequestHeaders.Authorization = null;
                Console.WriteLine("[Ilias] Fetching login URL timed out.");
                Console.WriteLine(e.InnerException.Message);
                return null;
            }
        }

        public async Task<int> DownloadFile(string id, string path)
        {
            string fileUrl = "https://ilias.uni-konstanz.de/ilias/goto_ilias_uni_file_" + id + "_download.html";
            Console.WriteLine("Downloading file");
            HttpResponseMessage response = await Client.GetAsync(fileUrl);
            if (response.IsSuccessStatusCode)
            {
                byte[] fileArray = await response.Content.ReadAsByteArrayAsync();
                await File.WriteAllBytesAsync(path, fileArray);
                Console.WriteLine("Wrote file");
                return 0;
            }
            return -1;
        }
    }
}
