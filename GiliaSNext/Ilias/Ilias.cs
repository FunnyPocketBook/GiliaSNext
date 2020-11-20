using GiliaSNext.Config;
using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;

namespace GiliaSNext.Ilias
{
    class Ilias
    {
        private HttpClientHandler HttpHandler;
        private HttpClient Client;
        private Uri LoginUrl;
        private ConfigBuilder Builder;
        private IliasFile[] Files;
        private int DlCounter;

        private static string ILIAS_BASE_URL = "https://ilias.uni-konstanz.de/ilias/";


        public Ilias(ConfigBuilder builder)
        {
            Builder = builder;
            HttpHandler = new HttpClientHandler
            {
                AllowAutoRedirect = true
            };
            Client = new HttpClient(HttpHandler);
            DlCounter = 0;
            Client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/86.0.4240.183 Safari/537.36");
            Client.DefaultRequestHeaders.Add("Upgrade-Insecure-Requests", "1");
            try
            {
                string json = File.ReadAllText(Path.Combine(Builder.Config.FileListPath, "files.json"));
                Files = JsonSerializer.Deserialize<IliasFile[]>(json);
                Array.Sort(Files);

            }
            catch (FileNotFoundException e)
            {
                Files = new IliasFile[] { };
            }
        }

        public async Task<int> Login()
        {
            LoginUrl = await GetLoginLink();
            var postParamsDict = new Dictionary<string, string>
            {
                { "username", Builder.Config.User },
                { "password", Builder.Config.PasswordIlias },
                { "cmd[doStandardAuthentication]", "Anmelden" }
            };
            FormUrlEncodedContent postParams = new FormUrlEncodedContent(postParamsDict);

            Console.WriteLine("[Ilias] Logging in...");
            try
            {
                HttpResponseMessage response = await Client.PostAsync(LoginUrl, postParams);
                if (!response.IsSuccessStatusCode)
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
                var loginScreenUrl = new Uri("https://ilias.uni-konstanz.de/ilias/login.php");
                HttpResponseMessage response = await Client.GetAsync(loginScreenUrl);
                HtmlDocument pageDocument = await Util.LoadHtmlDocument(response);
                HtmlNode form = pageDocument.DocumentNode.SelectSingleNode("//form[@id='form_']");
                return new Uri($"{ ILIAS_BASE_URL }{ WebUtility.HtmlDecode(form.Attributes["action"].Value) }");
            }
            catch (TaskCanceledException e)
            {
                Console.WriteLine("[Ilias] Fetching login URL timed out.");
                Console.WriteLine(e.InnerException.Message);
                return null;
            }
        }

        public async Task<XmlDocument> GetRssXml()
        {
            byte[] auth = Encoding.ASCII.GetBytes(Builder.Config.User + ":" + Builder.Config.PasswordRss);
            Client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", Convert.ToBase64String(auth));
            try
            {
                Console.WriteLine("[Ilias] Loading RSS feed...");
                var watch = new Stopwatch();
                watch.Start();
                HttpResponseMessage response = await Client.GetAsync(Builder.Config.RssUrl);
                Client.DefaultRequestHeaders.Authorization = null;
                watch.Stop();
                Console.WriteLine($"[Ilias] It took {watch.ElapsedMilliseconds / 1000.0} seconds to load the feed.");
                string pageContents = await response.Content.ReadAsStringAsync();
                var xmlDoc = new XmlDocument();
                xmlDoc.LoadXml(pageContents);
                return xmlDoc;
            }
            catch (TaskCanceledException e)
            {
                Client.DefaultRequestHeaders.Authorization = null;
                Console.WriteLine("[Ilias] Fetching login URL timed out.");
                Console.WriteLine(e.InnerException.Message);
                return null;
            }
        }

        public List<IliasFile> GetFilesFromXml(XmlDocument xmlDoc)
        {
            var rssFiles = new List<IliasFile>();
            XmlNodeList items = xmlDoc.SelectNodes("//item");
            string subfolderPattern = @"\[(.*?)\]";
            string fileNamePattern = @"]\s(.*): Die Datei";
            string fileIdPattern = @"file_(\d*)";
            foreach (XmlNode item in items)
            {
                if (item.SelectSingleNode(".//link") != null)
                {
                    string link = item.SelectSingleNode(".//link").InnerText;
                    if (link.Contains("target=file"))
                    {
                        string title = item.SelectSingleNode(".//title").InnerText;
                        var subfoldersMatch = Regex.Match(title, subfolderPattern);
                        var subfolders = subfoldersMatch.Groups[1].Value.Split(" > ");
                        subfolders = subfolders.Select(sub => Util.ReplaceInvalidChars(sub)).ToArray();
                        string fileName = Util.ReplaceInvalidChars(Regex.Match(title, fileNamePattern).Groups[1].Value);
                        int fileId = int.Parse(Regex.Match(link, fileIdPattern).Groups[1].Value);
                        DateTime date = DateTime.Parse(item.SelectSingleNode(".//pubDate").InnerText);
                        IliasFile fileToDownload = new IliasFile(subfolders, fileName, fileId, date);
                        if (Array.Exists(Builder.Config.IgnoreFiles, e => e == fileName) || Array.Exists(Builder.Config.IgnoreExtensions, e => e == fileToDownload.Extension) ||
                            Array.Exists(Files, f => f.Equals(fileToDownload)))
                        {
                            continue;
                        }
                        rssFiles.Add(fileToDownload);
                    }
                }
            }
            var jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true
            };
            var newFilesJson = new List<IliasFile>(Files);
            newFilesJson.AddRange(rssFiles);
            newFilesJson.Sort();
            string json = JsonSerializer.Serialize(newFilesJson, jsonOptions);
            File.WriteAllText(Path.Combine(Builder.Config.FileListPath, "files.json"), json);
            return rssFiles;
        }

        public async Task<int> DownloadRssFiles(string path, List<IliasFile> rssFiles)
        {
            if (rssFiles.Count == 0)
                return -1;
            var taskList = new List<Task>();
            int counter = 0;
            foreach (IliasFile file in rssFiles)
            {
                taskList.Add(DownloadFile(Path.Combine(path, String.Join(Path.DirectorySeparatorChar, file.Subfolders)), file.Id, file.Name, ++counter, rssFiles.Count));
            }
            DlCounter = 0;
            await Task.WhenAll(taskList.ToArray());
            return 0;
        }

        public async Task<List<Uri>> GetExerciseUrls()
        {
            var exerciseUrls = new List<Uri>();
            try
            {
                var desktopUrl = new Uri("https://ilias.uni-konstanz.de/ilias/ilias.php?baseClass=ilPersonalDesktopGUI&cmd=jumpToSelectedItems");
                HttpResponseMessage response = await Client.GetAsync(desktopUrl);
                HtmlDocument pageDocument = await Util.LoadHtmlDocument(response);
                HtmlNodeCollection rows = pageDocument.DocumentNode.SelectNodes("//div[@class='ilObjListRow']");
                foreach (HtmlNode row in rows)
                {
                    string href = row.SelectSingleNode(".//a[@class='il_ContainerItemTitle']").Attributes["href"].Value;
                    if (href.Contains("goto_ilias_uni_exc"))
                    {
                        exerciseUrls.Add(new Uri(href));
                    }
                }
                return exerciseUrls;
            }
            catch (TaskCanceledException e)
            {
                Client.DefaultRequestHeaders.Authorization = null;
                Console.WriteLine("[Ilias] Fetching exercise URLs timed out.");
                Console.WriteLine(e.InnerException.Message);
                return null;
            }
        }

        // TODO: 
        // - Add exercise and feedback files to files.json and skip those that have already been downloaded
        // - Assign files to the correct subfolder. Often the files of different courses have the same name (e.g. assignment01.pdf),
        //   so those files are just overwritten. One way to do it is to track the course name in GetExerciseUrls() and have a separate class
        public async Task<List<Uri>[]> GetExerciseFileUrls(Uri exercisePageUrl)
        {
            try
            {
                var exerciseFileUrls = new List<Uri>[2];
                exerciseFileUrls[0] = new List<Uri>();
                exerciseFileUrls[1] = new List<Uri>();
                HttpResponseMessage response = await Client.GetAsync(exercisePageUrl);
                HtmlDocument pageDocument = await Util.LoadHtmlDocument(response);
                HtmlNodeCollection rows = pageDocument.DocumentNode.SelectNodes("//div[@class='ilInfoScreenSec form-horizontal']");
                foreach (HtmlNode row in rows)
                {
                    HtmlNodeCollection links = row.SelectNodes(".//a");
                    if (links != null)
                    {
                        foreach (HtmlNode link in links)
                        {
                            HtmlAttribute hrefAttribute = link.Attributes["href"];
                            if (hrefAttribute != null)
                            {
                                string href = WebUtility.HtmlDecode(hrefAttribute.Value);
                                if (href.Contains("file="))
                                {
                                    if (href.Contains("cmd=downloadFile"))
                                    {
                                        exerciseFileUrls[0].Add(new Uri(ILIAS_BASE_URL + href));
                                    }
                                    else if (href.Contains("cmd=downloadFeedbackFile"))
                                    {
                                        exerciseFileUrls[1].Add(new Uri(ILIAS_BASE_URL + href));
                                    }
                                }
                            }
                        }
                    }
                }
                return exerciseFileUrls;
            }
            catch (TaskCanceledException e)
            {
                Client.DefaultRequestHeaders.Authorization = null;
                Console.WriteLine("[Ilias] Fetching exercise file URLs timed out.");
                Console.WriteLine(e.InnerException.Message);
                return null;
            }
        }

        public async Task<int> DownloadFile(string path, int id, string fileName = "", int count = -1, int total = -1)
        {
            var fileUrl = new Uri($"https://ilias.uni-konstanz.de/ilias/goto_ilias_uni_file_{id}_download.html");
            await DownloadFile(path, fileUrl, fileName, count, total);
            return 0;
        }

        public async Task<int> DownloadFile(string path, Uri fileUrl, string fileName = "", int count = -1, int total = -1, bool retrying = true)
        {
            if (fileName == "")
                fileName = fileUrl.ParseQueryString()["file"];

            if (count > 0 && total > 0)
                Console.WriteLine($"[{ count }/{ total }] Downloading { fileName }");
            else
                Console.WriteLine($"Downloading { fileName }");
            try
            {
                HttpResponseMessage response = await Client.GetAsync(fileUrl);
                string lastModified = response.Content.Headers.LastModified.ToString();
                if (response.IsSuccessStatusCode)
                {
                    byte[] fileArray = await response.Content.ReadAsByteArrayAsync();
                    Directory.CreateDirectory(path);
                    try
                    {
                        await File.WriteAllBytesAsync(Path.Combine(path, fileName), fileArray);
                        if (count > 0 && total > 0)
                            Console.WriteLine($"[{ ++DlCounter }/{ total }] Downloaded { fileName }");
                        else
                            Console.WriteLine($"Downloaded { fileName }");
                        return 0;
                    }
                    catch (AggregateException e)
                    {
                        if (!retrying)
                        {
                            Console.WriteLine($"An error occurred while writing { fileName }. Please check manually if the file is correct.");
                            Console.WriteLine(e);
                            return -1;
                        }
                        Console.WriteLine($"An error occurred while writing { fileName }. Retrying.");
                        Console.WriteLine(e);
                        return await DownloadFile(path, fileUrl, fileName, count, total, false);
                    }
                }
                return -1;
            }
            catch (TaskCanceledException e)
            {
                Client.DefaultRequestHeaders.Authorization = null;
                Console.WriteLine($"[Ilias] Downloading { fileName } failed.");
                Console.WriteLine(e.InnerException.Message);
                return -1;
            }
        }

        public Task DownloadFiles(string path, List<Uri> urls)
        {
            var taskList = new List<Task>();
            int counter = 0;
            foreach (Uri fileUrl in urls)
            {
                taskList.Add(DownloadFile(path, fileUrl, count: ++counter, total: urls.Count));
            }
            DlCounter = 0;
            return Task.WhenAll(taskList.ToArray());
        }
    }
}
