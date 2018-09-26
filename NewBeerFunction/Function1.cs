using BeerFunction;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Queue;
using Newtonsoft.Json;
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace EindopdrachtFunctions
{
    /// <summary>
    /// Http trigger for creating new beer rapport.
    /// </summary>
    public static class NewBeerFunction
    {
        /// <summary>
        /// Method for creating new beer rapport request.
        /// </summary>
        /// <param name="req">The Http request message.</param>
        /// <param name="log">The trace writer.</param>
        /// <returns>HttpRequestMessage.</returns>
        [FunctionName("CreateRapport")]
        public static async Task<HttpResponseMessage> Run([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)]HttpRequestMessage req, TraceWriter log)
        {
            log.Info("C# HTTP trigger function processed a request.");

            // parse query parameter
            string placeName = req.GetQueryNameValuePairs()
                .FirstOrDefault(q => string.Compare(q.Key, "placeName", true) == 0)
                .Value;

            if (placeName == null)
            {
                // Get request body
                dynamic data = await req.Content.ReadAsAsync<object>();
                placeName = data?.placeName;

            }

            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(Environment.GetEnvironmentVariable("AzureWebJobsStorage") + ";EndpointSuffix=core.windows.net");

            CloudBlobClient cloudBlobClient = storageAccount.CreateCloudBlobClient();
            CloudBlobContainer cloudBlobContainer = cloudBlobClient.GetContainerReference("blobcontainer");
            await cloudBlobContainer.CreateIfNotExistsAsync();

            // create sas uri
            string policyName = "newPolicy";
            CreateSharedAccessPolicy(cloudBlobClient, cloudBlobContainer, policyName);
            string sasUri = GetContainerSasUri(cloudBlobContainer, policyName);
            log.Info("Sas uri created");

            // create queue
            CloudQueueClient queueClient = storageAccount.CreateCloudQueueClient();

            CloudQueue queue = queueClient.GetQueueReference("queue");
            queue.CreateIfNotExists();

            log.Info("Cloud queue created");

            QueueMessage queueMessage = new QueueMessage
            {
                PlaceName = placeName,
                ImageName = $"{Guid.NewGuid().ToString()}.png",
                SasUri = sasUri
            };

            string message = JsonConvert.SerializeObject(queueMessage);
            queue.AddMessage(new CloudQueueMessage(message));

            log.Info("Message placed on queue");

            return req.CreateResponse(HttpStatusCode.OK, queueMessage);
        }

        /// <summary>
        /// Creates a new shared access policy on the container.
        /// </summary>
        /// <param name="blobClient">The blob client.</param>
        /// <param name="container">The cloud blob container.</param>
        /// <param name="policyName">The policy name.</param>
        private static void CreateSharedAccessPolicy(CloudBlobClient blobClient, CloudBlobContainer container, string policyName)
        {
            // remove existing policies
            BlobContainerPermissions perms = container.GetPermissions();
            perms.SharedAccessPolicies.Clear();
            container.SetPermissions(perms);

            BlobContainerPermissions permissions = container.GetPermissions();

            SharedAccessBlobPolicy sharedPolicy = new SharedAccessBlobPolicy()
            {
                SharedAccessExpiryTime = DateTimeOffset.UtcNow.AddHours(12),
                Permissions = SharedAccessBlobPermissions.Write | SharedAccessBlobPermissions.List | SharedAccessBlobPermissions.Read
            };

            permissions.SharedAccessPolicies.Add(policyName, sharedPolicy);

            container.SetPermissions(permissions);
        }

        /// <summary>
        /// Creates a new sas uri for the container.
        /// </summary>
        /// <param name="container">The cloud blob container.</param>
        /// <param name="policyName">The policy name.</param>
        /// <returns></returns>
        private static string GetContainerSasUri(CloudBlobContainer container, string policyName)
        {
            string sasContainerToken = container.GetSharedAccessSignature(null, policyName);

            return container.Uri + sasContainerToken;
        }
    }
}
