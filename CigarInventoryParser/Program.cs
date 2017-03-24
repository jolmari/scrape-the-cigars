using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using HtmlAgilityPack;
using Newtonsoft.Json;

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
            public string BaseUrl { get; set; }
        }

        public async Task ProxyTest()
        {
            var proxy = new WebProxy("198.50.235.188", 8080);

            using (var httpClientHandler = new HttpClientHandler
            {
                Proxy = proxy,
                //ClientCertificateOptions = ClientCertificateOption.Automatic
            })
            {
                using (var httpClient = new HttpClient(httpClientHandler))
                {
                    var response = await httpClient.GetStringAsync("http://www.google.fi");
                }
            }
        }

        public async void GetCigarsList()
        {
            var config = File.ReadAllText("config.json");
            var scrapeConfig = JsonConvert.DeserializeObject<Config>(config);

            await ProxyTest();

            //var response = await httpClient.GetAsync("");
            //response.EnsureSuccessStatusCode();

            //var content = await response.Content.ReadAsStringAsync();

            //var stream = new MemoryStream();
            //var writer = new StreamWriter(stream);

            //writer.Write(content);
            //writer.Flush();
            //stream.Position = 0;

            var document = new HtmlDocument();
            document.Load("./cigars.txt");

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
                .Select(page => $"{scrapeConfig.BaseUrl}/default.asp?action=srchrslt&amp;page={page}")
                .ToList()
                .ForEach(url => cigarPages.Add(url));


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