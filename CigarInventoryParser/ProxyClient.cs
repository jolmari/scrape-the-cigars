using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace CigarInventoryCrawler
{
    public class ProxyClient : IHttpClient
    {
        public string Uri { get; private set; }

        private HttpClient httpClient;  

        public ProxyClient(string proxyUri)
        {
            Uri = proxyUri;

            httpClient = new HttpClient(new HttpClientHandler
            {
                Proxy = new WebProxy(proxyUri)
            });

            httpClient.Timeout = new TimeSpan(0,0,30);
        }

        public async Task<bool> VerifyIpAddress()
        {   
            try
            {
                var responseIp = await GetAsync("https://api.ipify.org").Result.Content.ReadAsStringAsync();
                Console.WriteLine($"Proxy connection to {Uri} succeeded. Returned Ip: {responseIp}");
                return true;
            }
            catch(Exception ex)
            {
                Console.WriteLine($"Proxy connection to {Uri}  failed");
                Console.WriteLine(ex.Message, ex.InnerException);
                return false;
            }
        }

        public Task<HttpResponseMessage> GetAsync(string requestUri)
        {
            return httpClient.GetAsync(requestUri);
        }
    }
}
