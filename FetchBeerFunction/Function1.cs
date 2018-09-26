using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace FetchBeerFunction
{
    /// <summary>
    /// Http trigger for fetching finished beer rapport.
    /// </summary>
    public static class Function1
    {
        /// <summary>
        /// Method for fetching beer rapport.
        /// </summary>
        /// <param name="req">The http request message.</param>
        /// <param name="log">The trace writer.</param>
        /// <returns>HttpRequestMessage.</returns>
        [FunctionName("FetchRapport")]
        public static async Task<HttpResponseMessage> Run([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)]HttpRequestMessage req, TraceWriter log)
        {
            log.Info("C# HTTP trigger function processed a request.");

            // parse query parameter
            string imageName = req.GetQueryNameValuePairs()
                .FirstOrDefault(q => string.Compare(q.Key, "imageName", true) == 0)
                .Value;

            string sasUri = req.GetQueryNameValuePairs()
                .FirstOrDefault(q => string.Compare(q.Key, "sasUri", true) == 0)
                .Value;

            if (imageName == null)
            {
                // Get request body
                dynamic data = await req.Content.ReadAsAsync<object>();
                imageName = data?.imageName;
                sasUri = data?.sasUr;
            }

            if (imageName != null & sasUri != null)
            {
                if (!imageName.EndsWith(".png"))
                {
                    imageName = imageName + ".png";
                }

                CloudStorageAccount storageAccount = CloudStorageAccount.Parse(Environment.GetEnvironmentVariable("AzureWebJobsStorage") + ";EndpointSuffix=core.windows.net");

                CloudBlobContainer cloudBlobContainer = new CloudBlobContainer(new Uri(sasUri));

                string uri = cloudBlobContainer.StorageUri.PrimaryUri.AbsoluteUri;
                string url = uri + "/" + imageName;

                log.Info("URL:" + url);

                MemoryStream stream = await GetMemoryStream(url);

                if (stream != null)
                {
                    log.Info("Succes");

                    var response = new HttpResponseMessage(HttpStatusCode.OK);
                    response.Headers.AcceptRanges.Add("bytes");
                    response.Content = new StreamContent(stream);
                    response.Content.Headers.ContentDisposition = new ContentDispositionHeaderValue("render");
                    response.Content.Headers.ContentDisposition.FileName = imageName;
                    response.Content.Headers.ContentType = new MediaTypeHeaderValue("image/png");
                    response.Content.Headers.ContentLength = stream.Length;

                    return response;
                }
                else
                {
                    log.Info("Image not found");
                    return req.CreateResponse(HttpStatusCode.NotFound, "Cannot find image");
                }
            }
            else
            {
                log.Info("No image name or sas");
                return req.CreateResponse(HttpStatusCode.NotFound, "No image name or sas found");
            }
        }

        /// <summary>
        /// Gets a memory stream from a given url.
        /// </summary>
        /// <param name="url">The url.</param>
        /// <returns>A memory stream.</returns>
        public async static Task<MemoryStream> GetMemoryStream(string url)
        {
            HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("image/png"));

            HttpResponseMessage response = await client.GetAsync(url);

            if (response.IsSuccessStatusCode)
            {
                MemoryStream stream = new MemoryStream(response.Content.ReadAsByteArrayAsync().Result);

                return stream;
            }
            else
            {
                return null;
            }
        }
    }
}
