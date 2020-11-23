

namespace GiliaSNext.Config
{
    class Config
    {
        public string User { get; set; } = "first.last";
        public string PasswordIlias { get; set; } = "hunter12";
        public string PasswordRss { get; set; } = "hunter12";
        public string RssUrl { get; set; } = "https://first.last:-password-@ilias.uni-konstanz.de/ilias/privfeed.php?client_id=ilias_uni&user_id=000000&hash=0123456789abcdefghjklmnopqrst";
        public string FileListPath { get; set; } = @"C:\Users\user\Documents\GiliaSVN\";
        public string DownloadPath { get; set; } = @"C:\Users\user\Documents\GiliaSVN\";
        public string[] IgnoreFiles { get; set; } = { };
        public string[] IgnoreExtensions { get; set; } = { };
        public string GitUser { get; set; } = "first.last";
        public string GitEmail { get; set; } = "first.last@email.com";
        public string GitPassword { get; set; } = "hunter12";
        public string[] GitReposHttp { get; set; } = { };
        public string SvnUser { get; set; } = "first.last";
        public string SvnEmail { get; set; } = "first.last@email.com";
        public string SvnPassword { get; set; } = "hunter12";
        public string[] SvnReposHttp { get; set; } = { };
    }
}
