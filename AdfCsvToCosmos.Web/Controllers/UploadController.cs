using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Management.DataFactory;
using Microsoft.Rest;
using Microsoft.Identity.Client;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;
using Azure.Storage.Blobs.Models;

namespace AdfCsvToCosmos.Web.Controllers;

[ApiController]
[Route("[controller]")]
public class UploadController : ControllerBase
{
    private readonly ILogger<UploadController> _logger;
    private readonly IConfiguration _configuration;
    private BlobContainerClient _blobContainerClient;

    public UploadController(ILogger<UploadController> logger,
                            IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
        var blobServiceBaseUrl = configuration.GetValue<string>("BlobStorageEndpoint");
        var credential = new DefaultAzureCredential();
        _blobContainerClient = new BlobContainerClient(new Uri(Path.Combine(blobServiceBaseUrl, "uploaded")), credential);
    }

    [HttpGet("healthcheck")]
    public async Task<IActionResult> HealthCheck()
    {
        await _blobContainerClient.ExistsAsync();

        return Ok("Connection to Blob Okay.");
    }

    [HttpPost]
    public async Task<IActionResult> Post([FromForm]int chunkNumber,
                                          [FromForm]int totalChunks,
                                          [FromForm]string identifier,
                                          [FromForm]string fileName)
    {
        var blobClient = _blobContainerClient.GetBlockBlobClient($"{identifier}.csv");

        var blockId = GetBlockId(identifier, chunkNumber);

        await blobClient.StageBlockAsync(blockId, Request.Form.Files.First().OpenReadStream());

        // When we are in the final chunk, commit the blob
        if (chunkNumber == totalChunks)
        {
            var chunkNames = Enumerable.Range(1, chunkNumber).Select(chunk => GetBlockId(identifier, chunk));
            await blobClient.CommitBlockListAsync(chunkNames, new CommitBlockListOptions()
            {
                HttpHeaders = new BlobHttpHeaders()
                {
                    ContentEncoding = "utf-8",
                    ContentType = "text/csv"
                }
            });

            var pipelineRunId = await RunPipelineAsync(identifier);

            return new JsonResult(new { pipelineRunId = pipelineRunId });
        }

        return Ok();
    }

    [HttpGet("pipeline-status/{piplineRunId}")]
    public async Task<IActionResult> PipelineStatus(string piplineRunId)
    {
        var client = await CreateDataFactoryClientAsync();
        var adfConfig = _configuration.GetSection("AzureDataFactory");
        var resourceGroup = adfConfig.GetValue<string>("ResourceGroup");
        var factoryName = adfConfig.GetValue<string>("DataFactoryName");
        var pipelineName = adfConfig.GetValue<string>("PipelineName");
        
        var response = await client.PipelineRuns.GetAsync(resourceGroup, factoryName, piplineRunId);
        return new JsonResult(new { Status = response.Status });
    }

    private static string GetBlockId(string fileName, int chunkNumber)
    {
        // Block Ids must have the same length
        return EncodeToBase64($"{fileName}_{chunkNumber,0:000000}");
    }

    private static string EncodeToBase64(string rawText)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(rawText);
        return System.Convert.ToBase64String(bytes);
    }

    private async Task<string> RunPipelineAsync(string identifier)
    {
        // Reference:
        // https://docs.microsoft.com/en-us/azure/data-factory/quickstart-create-data-factory-dot-net#create-a-pipeline-run
        var client = await CreateDataFactoryClientAsync();
        var adfConfig = _configuration.GetSection("AzureDataFactory");
        var resourceGroup = adfConfig.GetValue<string>("ResourceGroup");
        var factoryName = adfConfig.GetValue<string>("DataFactoryName");
        var pipelineName = adfConfig.GetValue<string>("PipelineName");

        var parameters = new Dictionary<string, object>()
        {
            { "identifier", identifier }
        };

        var runResponse = await client.Pipelines.CreateRunWithHttpMessagesAsync(
            resourceGroup,
            factoryName,
            "ImportCsvToCosmos",
            parameters: parameters
        );

        _logger.LogInformation($"Pipeline Run Id: {runResponse.Body.RunId}");

        return runResponse.Body.RunId;
    }

    private async Task<DataFactoryManagementClient> CreateDataFactoryClientAsync()
    {
        // Reference:
        // https://docs.microsoft.com/en-us/azure/data-factory/quickstart-create-data-factory-dot-net#create-a-data-factory-client
        var tenantId = _configuration.GetSection("AzureClientCredentials").GetValue<string>("TenantId");
        var applicationId = _configuration.GetSection("AzureClientCredentials").GetValue<string>("ApplicationId");
        var clientSecret = _configuration.GetSection("AzureClientCredentials").GetValue<string>("ClientSecret");
        var app = ConfidentialClientApplicationBuilder
                    .Create(applicationId)
                    .WithAuthority("https://login.microsoftonline.com/" + tenantId)
                    .WithClientSecret(clientSecret)
                    .WithLegacyCacheCompatibility(false)
                    .WithCacheOptions(CacheOptions.EnableSharedCacheOptions)
                    .Build();
        var result = await app.AcquireTokenForClient(new string[] { "https://management.azure.com//.default" })
                        .ExecuteAsync();

        var credential = new TokenCredentials(result.AccessToken);
        var dataFactoryClient = new DataFactoryManagementClient(credential)
        {
            SubscriptionId = _configuration.GetValue<string>("AzureSubscriptionId")
        };

        return dataFactoryClient;
    }
}
