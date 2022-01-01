# Azure Data Factory - CSV to Cosmos DB Examle

An example application to demonstrate uploading a CSV file to Azure Storage Blob container and triggering an Azure Data Factory pipeline to import it to Azure Cosmos DB database.

## Related Repositories
* UI - https://github.com/randalvance/adf-csv-to-cosmos-ui
* Infra - https://github.com/randalvance/adf-csv-to-cosmos-infra

## Running Locally

Create an `appsettings.Development.json` file with the following contents:
```json
{
    "BlobStorageEndpoint": "https://mystorage.blob.core.windows.net/"
}
```
Replace `mystorage` with the name of your storage account. The storage account is expected to have a container named `uploaded`.

You can point to a local Storage emulator. If you point to a real azure storage container, you need to setup your credentials properly. Please follow the instructions in this page.
https://docs.microsoft.com/en-us/dotnet/api/overview/azure/identity-readme#environment-variables

Finally, run the app.

```
cd AdfCsvToCosmos.Web
dotnet watch run
```