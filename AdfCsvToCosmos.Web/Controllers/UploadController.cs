using Microsoft.AspNetCore.Mvc;
using Azure.Storage.Blobs;

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
        var blobServiceClient = new BlobServiceClient(configuration.GetConnectionString("AzureStorage"));
        _blobContainerClient =  blobServiceClient.GetBlobContainerClient("uploaded");
    }

    [HttpGet]
    public async Task<IActionResult> Get(int chunkNumber, long chunkSize, long currentChunkSize, long totalSize, string identifier, string filename, string relativePath, int totalChunks)
    {
        var blobClient = _blobContainerClient.GetBlobClient($"{identifier}/{chunkNumber,0:00000000}");

        if (await blobClient.ExistsAsync())
        {
            return Ok();
        }
        return StatusCode(204);
    }

    [HttpPost]
    public async Task<IActionResult> Post([FromForm]int chunkNumber, [FromForm]string identifier)
    {
        var blobClient = _blobContainerClient.GetBlobClient($"{identifier}/{chunkNumber,0:00000000}");
        await blobClient.UploadAsync(Request.Form.Files.First().OpenReadStream());
        return Ok();
    }
}
