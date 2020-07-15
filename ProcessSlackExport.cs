using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.IO.Compression;
using System.Linq;

namespace ProcessSlackExport
{
    public static class ProcessSlackExport
    {
        [FunctionName("ProcessSlackExport")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var data = JsonConvert.DeserializeObject<Data>(requestBody);
            using var ms = new MemoryStream(data.File);
            using var zipfile = new ZipArchive(ms);

            var reader = new SlackReader(zipfile);
            var channel = reader.Channels.FirstOrDefault(c => c.Name.ToLower() == data.Channel.ToLower());
            if (channel == null)
                return new NotFoundObjectResult($"Channel {data.Channel} not found");
            var msgs = reader.Read(channel);

            return new JsonResult(msgs.OrderBy(m => m.Date).Select(m => m.FormatAsHtml()).ToArray());
        }

        public class Data 
        {
            public byte[] File { get; set; }
            public string Channel { get; set; }
        }
    }
}
