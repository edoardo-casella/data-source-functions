using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk.Query;
using System;

namespace FunctionApp.Dynamics365.CRM
{
    public class Eventi
    {
        [Function("D365-CRM-Eventi")]
        public IActionResult Run(
            [HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequest req,
            FunctionContext executionContext)
        {
            var logger = executionContext.GetLogger("D365-CRM-Eventi");

            try
            {
                var serviceClient = new ServiceClient(
                    Environment.GetEnvironmentVariable("Dynamics365.CRM__AzureApp__DynamicsUrl"),
                    Environment.GetEnvironmentVariable("Dynamics365.CRM__AzureApp__ClientId"),
                    Environment.GetEnvironmentVariable("Dynamics365.CRM__AzureApp__ClientSecret"),
                    false);

                var fetchXml = @"<fetch top='50'>
                    <entity name='cr6ef_evento'>
                        <attribute name='cr6ef_eventoid' />
                        <attribute name='cr6ef_name' />
                    </entity>
                </fetch>";

                var result = serviceClient.RetrieveMultiple(new FetchExpression(fetchXml));

                return new OkObjectResult(result.Entities);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error retrieving eventi");
                return new StatusCodeResult(500);
            }
        }
    }
}
