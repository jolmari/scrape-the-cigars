using Microsoft.AspNetCore.Mvc;
using System;

namespace ScraperTestServer.Controllers
{
    [Route("api")]
    public class ValuesController : Controller
    {
        // GET api/5
        [HttpGet("{index}")]
        public ViewResult Get(int index)
        {
            Console.WriteLine(HttpContext.Connection.RemoteIpAddress);
            return View("~/Templates/listing-page.cshtml", index);
        }
    }
}
