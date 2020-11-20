using HtmlAgilityPack;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace GiliaSNext
{
    class Util
    {
        public static async Task<HtmlDocument> LoadHtmlDocument(HttpResponseMessage response)
        {
            string pageContents = await response.Content.ReadAsStringAsync();
            var pageDocument = new HtmlDocument();
            pageDocument.LoadHtml(pageContents);
            return pageDocument;
        }

        public static string ReplaceInvalidChars(string filename)
        {
            return string.Join("-", filename.Split(Path.GetInvalidFileNameChars()));
        }
    }
}
