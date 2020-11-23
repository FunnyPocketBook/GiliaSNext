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
using System.Web;

// TODO: Error handling
namespace GiliaSNext.Ilias
{
    class Ilias
    {
        private readonly HttpClientHandler HttpHandler;
        private readonly HttpClient Client;
        private Uri LoginUrl;
        private readonly ConfigBuilder Builder;
        private readonly List<IliasFile> Files;

        private static readonly string ILIAS_BASE_URL = "https://ilias.uni-konstanz.de/ilias/";

        /// <summary>
        /// Create an instance of Ilias. For each user, create a new instance
        /// </summary>
        /// <param name="builder">User configuration</param>
        public Ilias(ConfigBuilder builder)
        {
            Builder = builder;
            HttpHandler = new HttpClientHandler
            {
                AllowAutoRedirect = true
            };
            Client = new HttpClient(HttpHandler);
            Client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/86.0.4240.183 Safari/537.36");
            Client.DefaultRequestHeaders.Add("Upgrade-Insecure-Requests", "1");
            try
            {
                string json = File.ReadAllText(Path.Combine(Builder.Config.FileListPath, "files.json"));
                Files = JsonSerializer.Deserialize<List<IliasFile>>(json);
            }
            catch (FileNotFoundException)
            {
                Console.WriteLine($"No files.json found, creating a new one at {Path.Combine(Builder.Config.FileListPath, "files.json")}.");
                Files = new List<IliasFile>();
            }
        }

        /// <summary>
        /// Logs into ILIAS and stores the cookies
        /// </summary>
        /// <returns>The task object representing integer 0 on success and -1 on failure</returns>
        public async Task<int> Login()
        {
            LoginUrl = await GetLoginUrl();
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

        /// <summary>
        /// Gets the login URL for ILIAS since the URL changes with every update
        /// </summary>
        /// <returns>The task object representing the login URL</returns>
        public async Task<Uri> GetLoginUrl()
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

        /// <summary>
        /// Get the RSS feed
        /// </summary>
        /// <returns>The task object representing the XmlDocument of the RSS feed</returns>
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


        public async Task RssDownload(string path)
        {
            var rssXml = await GetRssXml();
            var rssFiles = GetFilesFromXml(rssXml);
            await DownloadIliasFiles(path, rssFiles);
            return;
        }

        /// <summary>
        /// Downloads all exercises and feedback
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public async Task ExerciseDownload(string path)
        {
            Console.WriteLine("[Ilias] Checking exercise files.");
            List<Uri> exUrls = await GetExerciseUrls();
            var combinedExFiles = new List<IliasFile>();
            var combinedFeedFiles = new List<IliasFile>();
            var exFileUrls = new List<Task<List<IliasFile>[]>>();
            foreach (Uri url in exUrls)
            {
                exFileUrls.Add(GetExerciseFileUrls(url));
            }
            var resultExFileUrls = await Task.WhenAll(exFileUrls);
            foreach (var r in resultExFileUrls)
            {
                combinedExFiles.AddRange(r[0]);
                combinedFeedFiles.AddRange(r[1]);
            }
            Task exFileTask = DownloadIliasFiles(path, combinedExFiles);
            Task feedFileTask = DownloadIliasFiles(path, combinedFeedFiles);
            await Task.WhenAll(exFileTask, feedFileTask);
            return;
        }

        /// <summary>
        /// Gets files to download from RSS feed
        /// </summary>
        /// <param name="xmlDoc">RSS feed</param>
        /// <returns>List of files to download</returns>
        public List<IliasFile> GetFilesFromXml(XmlDocument xmlDoc)
        {
            var rssFiles = new List<IliasFile>();
            XmlNodeList items = xmlDoc.SelectNodes("//item");
            string subfolderPattern = @"\[(.*?)\]";
            string fileNamePattern = @"]\s(.*): Die Datei";
            string fileIdPattern = @"file_(\d*)";
            IliasFile[] filesArr = Files.ToArray();
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
                        var fileUrl = new Uri($"https://ilias.uni-konstanz.de/ilias/goto_ilias_uni_file_{fileId}_download.html");
                        DateTime date = DateTime.Parse(item.SelectSingleNode(".//pubDate").InnerText);
                        IliasFile fileToDownload = new IliasFile(subfolders, fileName, fileId, fileUrl, date);
                        if (Array.Exists(Builder.Config.IgnoreFiles, e => e == fileName) || Array.Exists(Builder.Config.IgnoreExtensions, e => e == fileToDownload.Extension) ||
                            Array.Exists(filesArr, f => f.Equals(fileToDownload)))
                        {
                            continue;
                        }
                        rssFiles.Add(fileToDownload);
                    }
                }
            }
            return rssFiles;
        }

        /// <summary>
        /// Gets exercise URLs from the ILIAS desktop
        /// </summary>
        /// <returns>The task object representing the list of URIs to the exercises on success and <c>null</c> on failure</returns>
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
                Console.WriteLine("[Ilias] Fetching exercise URLs timed out.");
                Console.WriteLine(e.InnerException.Message);
                return null;
            }
        }
        
        /// <summary>
        /// Gets URL to single exercises from <c>exercisePageUrl</c>
        /// </summary>
        /// <param name="exercisePageUrl">URL to page where the exercises are listed</param>
        /// <returns>The task object representing an array of lists where each element is an IliasFile of a single exercise file.
        /// The index 0 of the array contains exercise files and index 1 contains feedback files. Returns <c>null</c> on failure</returns>
        public async Task<List<IliasFile>[]> GetExerciseFileUrls(Uri exercisePageUrl)
        {
            try
            {
                var exerciseFiles = new List<IliasFile>[2];
                exerciseFiles[0] = new List<IliasFile>();
                exerciseFiles[1] = new List<IliasFile>();
                HttpResponseMessage response = await Client.GetAsync(exercisePageUrl);
                HtmlDocument pageDocument = await Util.LoadHtmlDocument(response);
                HtmlNodeCollection rows = pageDocument.DocumentNode.SelectNodes("//div[@class='ilInfoScreenSec form-horizontal']");
                HtmlNodeCollection navigation = pageDocument.DocumentNode.SelectSingleNode("//ol[@class='breadcrumb hidden-print']").SelectNodes(".//li");
                var navigationSplit = new List<string>();
                /*
                 * TODO: Determine the exercise subfolder in a better way
                 * The trigger word determines when to start adding the categories/subfolders from Ilias to the list.
                 * This is used to determine which subfolder the exercise files belong to.
                 * An example of navigation is:
                 * Magazin, Mathematisch-Naturwissenschaftliche Sektion, Informatik und Informationswissenschaft, Lehrveranstaltungen WS 20/21, Data Visualization: Advanced Topics, Assignments, Assignment 01
                 */
                string triggerWord = "Lehrveranstaltungen";
                var foundTriggerWord = false;
                foreach (HtmlNode li in navigation)
                {
                    string text = li.InnerText;
                    if (foundTriggerWord)
                    {
                        navigationSplit.Add(text);
                    }
                    else if (text.StartsWith(triggerWord))
                    {
                        foundTriggerWord = true;
                    }
                }
                var tempSubfolders = navigationSplit.Select(sub => Util.ReplaceInvalidChars(sub)).ToArray();
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
                                    var url = new Uri(ILIAS_BASE_URL + href);
                                    var query = HttpUtility.ParseQueryString(url.Query);
                                    string fileName = query["file"];
                                    int fileId = int.Parse(query["ref_id"]);
                                    var subfolderList = tempSubfolders.ToList();
                                    if (href.Contains("cmd=downloadFile"))
                                    {
                                        var file = new IliasFile(tempSubfolders, fileName, fileId, url);
                                        exerciseFiles[0].Add(file);
                                    }
                                    else if (href.Contains("cmd=downloadFeedbackFile"))
                                    {
                                        var file = new IliasFile(tempSubfolders, "feedback_"+fileName, fileId, url);
                                        exerciseFiles[1].Add(file);
                                    }
                                }
                            }
                        }
                    }
                }
                return exerciseFiles;
            }
            catch (TaskCanceledException e)
            {
                Console.WriteLine("[Ilias] Fetching exercise file URLs timed out.");
                Console.WriteLine(e.InnerException.Message);
                return null;
            }
        }

        /// <summary>
        /// Downloads file from <c>fileUrl</c> to <c>path</c>
        /// </summary>
        /// <param name="path">Path to download directory</param>
        /// <param name="file">File to download</param>
        /// <param name="retrying">Retry download on failure</param>
        /// <returns>The task object representing integer 0 on success and -1 on failure</returns>
        public async Task<int> DownloadFile(string path, IliasFile file, bool retrying = true)
        {
            try
            {
                HttpResponseMessage response = await Client.GetAsync(file.Url);
                if (response.IsSuccessStatusCode)
                {
                    var lastModified = DateTime.Parse(response.Content.Headers.LastModified.ToString());
                    file.LastModified = lastModified;
                    bool updated = false;
                    if (Files.Count > 0)
                    {
                        IliasFile currentFile = Files.Find(f => f.Matches(file));
                        var idx = Files.FindIndex(f => f.Matches(file));
                        if (currentFile != null)
                        {
                            if (currentFile.LastModified.CompareTo(file.LastModified) >= 0 && currentFile.Date.CompareTo(file.Date) >= 0)
                                return 0;
                            updated = true;
                            Files[idx] = file;
                        }
                    }
                    Task<byte[]> fileArray = response.Content.ReadAsByteArrayAsync();
                    Directory.CreateDirectory(path);
                    try
                    {
                        File.WriteAllBytes(Path.Combine(path, file.Name), await fileArray);
                        if (!updated)
                            Files.Add(file);
                        Console.WriteLine($"[Ilias] Downloaded { file.Name }");
                        return 0;
                    }
                    catch (IOException)
                    {
                        if (!retrying)
                        {
                            Console.WriteLine($"[Ilias] An error occurred while writing { file.Name }. Please check manually if the file is correct.");
                            return -1;
                        }
                        Console.WriteLine($"[Ilias] An error occurred while writing { file.Name }. Retrying.");
                        return await DownloadFile(path, file, false);
                    }
                }
                return -1;
            }
            catch (TaskCanceledException e)
            {
                if (!retrying)
                {
                    Console.WriteLine($"[Ilias] Downloading { file.Name } failed.");
                    Console.WriteLine(e.InnerException.Message);
                    return -1;
                }
                Console.WriteLine($"[Ilias] An error occurred while downloading { file.Name }. Retrying.");
                Console.WriteLine(e);
                return await DownloadFile(path, file, false);
            }
        }

        /// <summary>
        /// Downloads files taken from RSS feed and track downloaded files
        /// </summary>
        /// <param name="path">Path to download directory</param>
        /// <param name="rssFiles">List of files to download</param>
        public async Task DownloadIliasFiles(string path, List<IliasFile> rssFiles)
        {
            if (rssFiles.Count == 0)
                return;
            var taskList = new List<Task<int>>();
            foreach (IliasFile file in rssFiles)
            {
                taskList.Add(DownloadFile(Path.Combine(path, string.Join(Path.DirectorySeparatorChar, file.Subfolders)), file));
            }
            await Task.WhenAll(taskList);
            return;
        }

        public void SaveJsonFile()
        {
            var jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true
            };
            string json = JsonSerializer.Serialize(Files, jsonOptions);
            File.WriteAllText(Path.Combine(Builder.Config.FileListPath, "files.json"), json);
        }
    }
}
