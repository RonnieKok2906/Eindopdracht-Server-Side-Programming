using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using BeerFunction;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace QueueTriggerFunction
{
    /// <summary>
    /// Queue trigger function.
    /// </summary>
    public static class QueueTriggerFunction
    {
        /// <summary>
        /// Method for creating new beer rapport.
        /// </summary>
        /// <param name="myQueueItem">My queue item.</param>
        /// <param name="log">Trace Writer.</param>
        [FunctionName("QueueTrigger")]
        public static async Task RunAsync([QueueTrigger("queue", Connection = "")]string myQueueItem, TraceWriter log)
        {
            log.Info($"C# Queue trigger function processed: {myQueueItem}");

            QueueMessage queueItem = JsonConvert.DeserializeObject<QueueMessage>(myQueueItem);

            log.Info("Deserialized object");

            if (queueItem.PlaceName != null)
            {
                // get weather info
                string weatherUrl = string.Format("https://api.openweathermap.org/data/2.5/weather?q={0},nl&appid=" + Environment.GetEnvironmentVariable("WeatherKey") + "&units=metric", queueItem.PlaceName);
                string output = await GetStringAsync(weatherUrl);

                if (output != null)
                {
                    log.Info("Got weather info");

                    // deserialize object and get values
                    dynamic results = JsonConvert.DeserializeObject<dynamic>(output);

                    double temp = results.main.temp;
                    double windSpeed = results.wind.speed;

                    JArray weatherArray = results.weather;
                    string weatherDescription = weatherArray.FirstOrDefault()?["description"]?.Value<string>();

                    string lon = results.coord.lon;
                    string lat = results.coord.lat;

                    // generate beer advice
                    string beerAdvice = CreateBeerAdvice(temp, windSpeed);

                    // get map image
                    string mapUri = string.Format("https://atlas.microsoft.com/map/static/png?subscription-key=" + Environment.GetEnvironmentVariable("MapKey") + "&api-version=1.0&center={0},{1}&zoom=12&height=512&width=512&language=nl-NL&format=png", lon, lat);

                    Task<MemoryStream> streamTask = GetMemoryStreamAsync(mapUri);
                    MemoryStream stream = streamTask.Result;

                    log.Info("Map obtained");

                    if (stream != null)
                    {
                        // add text to image
                        ImageHelper helper = new ImageHelper();
                        MemoryStream modifiedStream = helper.AddTextToImage(stream, beerAdvice, temp.ToString(), weatherDescription, windSpeed.ToString());

                        // add memorystream to blob storage
                        CloudStorageAccount storageAccount = CloudStorageAccount.Parse(Environment.GetEnvironmentVariable("AzureWebJobsStorage") + ";EndpointSuffix=core.windows.net");
                        CloudBlobClient cloudBlobClient = storageAccount.CreateCloudBlobClient();

                        CloudBlobContainer cloudBlobContainer = new CloudBlobContainer(new Uri(queueItem.SasUri));

                        Task<bool> succes = SendBlobAsync(cloudBlobContainer, modifiedStream, queueItem.ImageName);

                        if (!await succes)
                        {
                            log.Info("Problem with uploading image");
                        }

                        log.Info("End of queue trigger reached");
                    }
                    else
                    {
                        log.Info("Problem with creating memory stream");
                    }
                }
                else
                {
                    log.Info("Cannot retrieve weather info");
                }
            }
            else
            {
                log.Info("No place name given");
            }
        }

        /// <summary>
        /// Sends image to blob container.
        /// </summary>
        /// <param name="container">The blob container.</param>
        /// <param name="memoryStream">The memory stream.</param>
        /// <param name="imageName">The image name.</param>
        private static async Task<bool> SendBlobAsync(CloudBlobContainer container, MemoryStream memoryStream, string imageName)
        {
            try
            {
                CloudBlockBlob blob = container.GetBlockBlobReference(imageName);
                await blob.UploadFromStreamAsync(memoryStream);

                return true;
            }
            catch (StorageException e)
            {
                return false;
            }
        }

        /// <summary>
        /// Gets a string from a given url.
        /// </summary>
        /// <param name="url">The url.</param>
        /// <returns>A string.</returns>
        public async static Task<string> GetStringAsync(string url)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);

            try
            {
                HttpWebResponse response = (HttpWebResponse)await request.GetResponseAsync();

                Stream stream = response.GetResponseStream();
                StreamReader reader = new StreamReader(stream);

                return await reader.ReadToEndAsync();
            }
            catch
            {

                return null;
            }
        }

        /// <summary>
        /// Gets a memory stream from a given url.
        /// </summary>
        /// <param name="url">The url.</param>
        /// <returns>A memory stream.</returns>
        public async static Task<MemoryStream> GetMemoryStreamAsync(string url)
        {
            HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("image/png"));

            HttpResponseMessage response = await client.GetAsync(url);

            MemoryStream stream = new MemoryStream(response.Content.ReadAsByteArrayAsync().Result);

            return stream;
        }

        /// <summary>
        /// Creates a beer advice.
        /// </summary>
        /// <param name="temp">The temperature.</param>
        /// <param name="windSpeed">The windspeed.</param>
        /// <returns>A beer advice.</returns>
        public static string CreateBeerAdvice(double temp, double windSpeed)
        {
            string beerAdvice;
            if (temp > 18 & temp < 25)
            {
                if (windSpeed > 16)
                {
                    beerAdvice = "Temperatuur is prima, maar sterke wind";
                }
                else
                {
                    beerAdvice = "Mooi weer om bier te drinken";
                }
            }
            else
            {
                if (windSpeed > 16)
                {
                    beerAdvice = "Temperatuur is niet optimaal met sterke wind";
                }
                else
                {
                    beerAdvice = "Temperatuur is niet optimaal";
                }
            }

            return beerAdvice;
        }
    }
}
