using System;
using System.Net;

namespace GiliaSNext.Helper
{
    class Helper
    {
        public static void PrintCookies(CookieContainer cookieJar, Uri uri)
        {
            foreach (Cookie cookie in cookieJar.GetCookies(uri))
            {
                Console.WriteLine("Name = {0} ; Value = {1} ; Domain = {2}",
                    cookie.Name, cookie.Value, cookie.Domain);
            }
        }
    }
}
