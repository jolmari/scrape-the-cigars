using Microsoft.AspNetCore.Mvc;
using System;

namespace ScraperTestServer.Controllers
{
    [Route("/search-result")]
    public class ValuesController : Controller
    {
        [HttpGet("{index}")]
        public ViewResult Get(int index)
        {
            Console.WriteLine(HttpContext.Connection.RemoteIpAddress);
            return View("~/Templates/listing-page.cshtml", index);
        }
    }
}
