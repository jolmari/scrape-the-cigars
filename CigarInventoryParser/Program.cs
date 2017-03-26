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
        public class Config
        {
            public string CigarDbUrl { get; set; }
        }

        //public async Task<HttpWebResponse> GetThroughProxy()
        //{
        //    var proxyUriList = await GetHttpsProxyUrls();
        //    var proxy = new WebProxy(proxyUriList.First());

        //    using (var httpClientHandler = new HttpClientHandler
        //    {
        //        Proxy = proxy
        //    })
        //    {
        //        using (var httpClient = new HttpClient(httpClientHandler))
        //        {
        //            var response = await httpClient.GetStringAsync("https://api.ipify.org/");
        //            Console.WriteLine($"Using Proxy IP address: {response}");
        //        }
        //    }
        //}

        private async Task<IEnumerable<string>> GetHttpsProxyUrls()
        {
            using(var httpClient = new HttpClient())
            {
                Console.WriteLine("Fetching proxy list file...");

                var response = await httpClient.GetStringAsync("http://txt.proxyspy.net/proxy.txt");

                Console.WriteLine("Proxy list file fetched. Processing...");

                return response
                    .Split('\n')
                    .Select(line => line.Split(' '))
                    .Where(cells => cells.Count() == 4 && cells.ElementAt(1).Contains("-S"))
                    .Select(proxy => proxy.ElementAt(0));
            }
        }

        public async void GetCigarsList()
        {
            var config = File.ReadAllText("config.json");
            var scrapeConfig = JsonConvert.DeserializeObject<Config>(config);
            var proxyUris = await GetHttpsProxyUrls();

            using (var httpClientHandler = new HttpClientHandler
            {
                Proxy = new WebProxy(proxyUris.First())
            })
            {
                using (var httpClient = new HttpClient())
                {
                    var response = await httpClient.GetAsync($"{scrapeConfig.CigarDbUrl}");
                    response.EnsureSuccessStatusCode();

                    var content = await response.Content.ReadAsStringAsync();

                    using(var stringReader = new StringReader(content))
                    {
                        var document = new HtmlDocument();
                        document.Load(stringReader);

                        // 50 results per page
                        var tableNodes = document
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

                        // Total amount of pages
                        var lastPageNumber = int.Parse(document
                            .DocumentNode
                            .SelectSingleNode(@"//a[contains(text(), 'Last &gt;&gt;')]")
                            .GetAttributeValue("href", "undefined")
                            .Split('&')[1]
                            .Remove(0, 5));

                        // Build ConcurrentBag of traversable URLs
                        var cigarPages = new ConcurrentBag<string>();

                        Enumerable
                            .Range(1, lastPageNumber)
                            .Select(page => $"{scrapeConfig.CigarDbUrl}&page={page}")
                            .ToList()
                            .ForEach(url => cigarPages.Add(url));
                    }
                }
            }

            //File.WriteAllText($"./cigars-output-{DateTime.Now.Ticks}.json", JsonConvert.SerializeObject(tableNodes));
        }


        private decimal FormatNodeContentToDecimal(HtmlNode row)
        {
            return decimal.Parse(row.InnerText.Replace('.', ','));
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