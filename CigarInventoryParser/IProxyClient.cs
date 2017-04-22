using System.Net.Http;
using System.Threading.Tasks;

namespace CigarInventoryCrawler
{
    public interface IHttpClient
    {
        Task<HttpResponseMessage> GetAsync(string requestUri);
    }
}