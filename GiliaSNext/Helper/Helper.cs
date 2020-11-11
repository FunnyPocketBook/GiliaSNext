using System;
using System.Collections;
using System.Net;
using System.Reflection;

namespace GiliaSNext.Helper
{
    class Cookies
    {
        public static void PrintCookies(CookieContainer cookieJar)
        {
            Hashtable table = (Hashtable)cookieJar.GetType().InvokeMember(
                "m_domainTable",
                BindingFlags.NonPublic |
                BindingFlags.GetField |
                BindingFlags.Instance,
                null,
                cookieJar,
                new object[] { }
            );
            foreach (var key in table.Keys)
            {
                Uri uri = new Uri(string.Format("https://{0}/", key));

                foreach (Cookie cookie in cookieJar.GetCookies(uri))
                {
                    Console.WriteLine("Name = {0} ; Value = {1} ; Domain = {2}",
                        cookie.Name, cookie.Value, cookie.Domain);
                }
            }
        }
    }
}
