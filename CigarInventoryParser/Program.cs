using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using HtmlAgilityPack;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace CigarInventoryCrawler
{
    public class Program
    {
        static void Main(string[] args)
        {
            var crawler = new Crawler();
            crawler.GetCigarsList();
            Console.ReadKey();
        }
    }

    public class Crawler
    {
        private List<string> ProxyAddresses = new List<string>{
            "134.60.51.152:3128",
            "54.183.3.46:80",
            "191.96.50.197:8080",
            "94.177.234.22:3128"
        };

        public async Task GetCigarsList()
        {
            var config = File.ReadAllText("config.json");
            var scrapeConfig = JsonConvert.DeserializeObject<Config>(config);

            var unverifiedProxies = ProxyAddresses.Select(uri => new ProxyClient(uri));
            var verifiedProxies = new List<ProxyClient>();

            foreach(var proxy in unverifiedProxies)
            {
                if(await proxy.VerifyIpAddress())
                {
                    verifiedProxies.Add(proxy);
                }
            }

            var urls = GenerateCigarListingPageUris(5, scrapeConfig.SearchResultUrl);

            var result = new List<HttpResponseMessage>();

            foreach (var url in urls)
            {
                try
                {
                    var proxy = verifiedProxies.PickRandomElement();
                    result.Add(await proxy.GetAsync(url));
                    Console.WriteLine($"Response from {url}");
                    await Task.Delay(3000);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message, ex.InnerException);
                }
            }

            Console.WriteLine("DONE!");

            //var listContents = SendRequestParallel(proxies, urls);



            //proxies.ToList().ForEach(async x => Console.WriteLine("Created proxy:", await x.VerifyIpAddress()));

            //var response = await proxies.First().GetAsync($"{scrapeConfig.CigarDbUrl}");
            //response.EnsureSuccessStatusCode();

            //var content = await response.Content.ReadAsStringAsync();

            //using (var stringReader = new StringReader(content))
            //{
            //    var document = new HtmlDocument();
            //    document.Load(stringReader);

            //    //var result = ReadCigarInfoListFromDocument(document);
            //    //var pages = ReadTotalPageNumberFromDocument(document);
            //    var urls = GenerateCigarListingPageUris(10, scrapeConfig.CigarDbUrl);

            //    var listContents = SendRequestParallel(proxies, urls);
            //    //File.WriteAllText($"./cigars-output-{DateTime.Now.Ticks}.json", JsonConvert.SerializeObject(result));
            //}
        }

        private async Task<IEnumerable<HttpResponseMessage>> SendRequestParallel(IEnumerable<ProxyClient> proxies, IEnumerable<string> urlList)
        {
            var results = new List<HttpResponseMessage>();

            foreach (var chunk in urlList.Chunkify(5))
            {
                var tasks = chunk.Select(url =>
                {
                    var proxy = proxies.PickRandomElement();
                    return proxy.GetAsync(url);
                });

                Console.WriteLine("Processing next batch...");
                results.AddRange(await Task.WhenAll(tasks));
                Console.WriteLine("Chunk processed, waiting 5s...");
                await Task.Delay(5000);
                Console.WriteLine("Delay completed, next round.");
            }

            return results;
            // Jako chunkkeihin
            // Jokaisesta taski
            // Delay
            // Looppi

            //var options = new ParallelOptions { MaxDegreeOfParallelism = 5 };
            //var results = new ConcurrentStack<string>();

            //foreach(var urlsChunked in urlList.)

            //return await Task.WhenAll(urlList.AsParallel().WithDegreeOfParallelism(1).Select(async url => {
            //    await Task.Delay(new Random().Next(2000, 5000));
            //    var proxy = proxies.PickRandomElement();
            //    Console.WriteLine($"Connecting with proxy {proxy.Uri} to {url}");
            //    var pageResult = await proxy.GetAsync(url);
            //    Console.WriteLine($"Response received from {url}");
            //    return pageResult;
            //}));
        }

        private async Task<IEnumerable<ProxyClient>> CreateProxyClients()
        {
            var proxyUris = await GetHttpsProxyUrls();
            var validProxies = new Queue<ProxyClient>();

            foreach (var uri in proxyUris)
            {
                var proxy = await TryCreateProxy(uri);

                if (proxy != null)
                {
                    validProxies.Enqueue(proxy);
                }

                if (validProxies.Count == 10)
                {
                    break;
                }
            }

            return validProxies;
        }

        private async Task<ProxyClient> TryCreateProxy(string proxyUri)
        {
            try
            {
                var proxyClient = new ProxyClient(proxyUri);

                var result = await proxyClient.GetAsync("https://api.ipify.org");

                if (result.IsSuccessStatusCode)
                {
                    Debug.WriteLine(await result.Content.ReadAsStringAsync());
                    return proxyClient;
                }

                return null;
            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
                return null;
            }
        }

        private async Task<IEnumerable<string>> GetHttpsProxyUrls()
        {
            Console.WriteLine("Fetching proxy list file...");

            var client = new HttpClient();
            var response = await client.GetStringAsync("http://txt.proxyspy.net/proxy.txt");

            Console.WriteLine("Proxy list file fetched. Processing...");

            return response
                .Split('\n')
                .Select(line => line.Split(' '))
                .Where(cells => cells.Count() == 4 && cells.ElementAt(1).Contains("-S"))
                .Select(proxy => proxy.ElementAt(0));
        }

        private IEnumerable<string> GenerateCigarListingPageUris(int totalPages, string baseUri)
        {
            return Enumerable
                .Range(1, totalPages)
                .Select(page => $"{baseUri}/{page}");
        }

        private int ReadTotalPageNumberFromDocument(HtmlDocument document)
        {
            // Total amount of pages is contained in the 'Last' link on the bottom of the list page.
            return int.Parse(document
                .DocumentNode
                .SelectSingleNode(@"//a[contains(text(), 'Last &gt;&gt;')]")
                .GetAttributeValue("href", "undefined")
                .Split('&')[1]
                .Remove(0, 5));
        }

        private IEnumerable<CigarInfo> ReadCigarInfoListFromDocument(HtmlDocument document)
        {
            // 50 results per page. Document format is a mess so we have to:
            // 1. Find the table for the list
            // 2. Separate Id from the link to cigar details
            // 3. Read rest of the info from each row's column in order
            return document
                .DocumentNode
                .SelectNodes("//table[@class='bbstable']/tr[position() > 2]")
                .Select(row => new CigarInfo
                {
                    Id = int.Parse(row
                        .SelectSingleNode("./td/a")
                        .GetAttributeValue("href", "Undefined")
                        .Split('&')[1]
                        .Remove(0, 9)),
                    Name = row.SelectSingleNode("./td/a").InnerText,
                    LengthInches = FormatNodeContentToDecimal(row.SelectSingleNode("(./td)[2]")),
                    RingGauge = int.Parse(row.SelectSingleNode("(./td)[3]").InnerText),
                    Country = row.SelectSingleNode("(./td)[4]").InnerText,
                    FillerCountry = row.SelectSingleNode("(./td)[5]").InnerText,
                    WrapperCountry = row.SelectSingleNode("(./td)[6]").InnerText,
                    Color = row.SelectSingleNode("(./td)[7]").InnerText,
                    Strength = row.SelectSingleNode("(./td)[8]").InnerText
                });
        }

        private decimal FormatNodeContentToDecimal(HtmlNode row)
        {
            return decimal.Parse(row.InnerText.Replace('.', ','));
        }

        public class Config
        {
            public string SearchResultUrl { get; set; }
        }

        public class CigarInfo
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public decimal LengthInches { get; set; }
            public int RingGauge { get; set; }
            public string Country { get; set; }
            public string FillerCountry { get; set; }
            public string WrapperCountry { get; set; }
            public string Color { get; set; }
            public string Strength { get; set; }
        }
    }
}