using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;

namespace latencymicroservice.Controllers
{
    [Route("api/[controller]")]
    public class TestLatencyController : Controller
    {
        private const string serviceHostName = "testlatency";
        private const string targetUrl = "https://jsonplaceholder.typicode.com/posts/1";

        // GET api/testlatency/5
        [HttpGet("{countdown}")]
        public async Task<LatencyResult> Get(int countdown)
        {
            LatencyResult result = new LatencyResult();
            
            var httpConnectionFeature = HttpContext.Features.Get<IHttpConnectionFeature>();
            var localIpAddress = httpConnectionFeature?.LocalIpAddress;
        
            LatencyReport report = new LatencyReport();
            report.CallerInstance = $"Test Latency {countdown}";
            report.IpAddress = localIpAddress.ToString();
            Stopwatch w = Stopwatch.StartNew();
            if(countdown <= 0)
            {
                await CallExternalService<string>(targetUrl);
                report.AwaitedMilliseconds = w.ElapsedMilliseconds;
                report.ExternalServiceCalled = true;
                report.CalledUrl = targetUrl;
                result.Hops.Add(report);
            }
            else
            {
                var childResult = await CallExternalService<LatencyResult>($"http://testlatency:5577/api/testlatency/{(countdown-1)}");
                report.CalledUrl = $"http://testlatency:5577/api/testlatency/{(countdown-1)}";
                report.AwaitedMilliseconds = w.ElapsedMilliseconds;
                report.ExternalServiceCalled = false;
                result.Hops.AddRange(childResult.Hops);
                result.Hops.Add(report);
                LatencyReport externalHop = result.Hops.FirstOrDefault(x=>x.ExternalServiceCalled);
                if(externalHop != null){
                    result.FinalLatency = report.AwaitedMilliseconds - externalHop.AwaitedMilliseconds;
                    report.PartialLatency = result.FinalLatency - childResult.FinalLatency;
                    result.AverageLatency = result.FinalLatency / result.Hops.Count;
                }  
            }
            result.TotalMilliseconds = w.ElapsedMilliseconds;


            return result;
        }
        private async Task<T> CallExternalService<T>(string url) where T : class
        {
            HttpClient client = new HttpClient();

            Console.WriteLine($"Calling service @ {url}");
            var response = await client.GetAsync(url);

            if (response.StatusCode == HttpStatusCode.OK)
            {
                try
                {
                    string message = await response.Content.ReadAsStringAsync();
                    if(typeof(T)  == typeof(string))
                    {
                        return (T)(object) message;
                    }
                    return Newtonsoft.Json.JsonConvert.DeserializeObject<T>(message);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                    throw;
                }
            }
            else
            {
                Console.WriteLine("Unable to contact endpoint");
                throw new InvalidOperationException("Unable to contact endpoint");
            }
        }
    }

    public class LatencyResult
    {
        public double TotalMilliseconds { get; set; }
        public double FinalLatency { get; set; }
        
        public double AverageLatency { get; set; }

        public List<LatencyReport> Hops {get; set;} = new List<LatencyReport>();
    }
    public class LatencyReport
    {
        public double PartialLatency { get; set; }
        public string CallerInstance {get; set; }
        public double AwaitedMilliseconds {get; set; }
        public string CalledUrl {get; set; }
        public string IpAddress {get; set; }
        public bool ExternalServiceCalled {get; set; }
    }
}
