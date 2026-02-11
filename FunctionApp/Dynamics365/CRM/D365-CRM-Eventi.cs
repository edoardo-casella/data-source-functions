using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.Text.Json.Nodes;

namespace Plumsail.DataSource.Dynamics365.CRM
{
    public class Eventi(HttpClientProvider httpClientProvider, ILogger<Eventi> logger)
    {
        [Function("D365-CRM-Eventi")]
        public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "get", Route = "crm/eventi/{id?}")] HttpRequest req, Guid? id)
        {
            logger.LogInformation("Dynamics365-CRM-Eventi is requested.");
            try
            {
                var client = httpClientProvider.Create();
                if (!id.HasValue)
                {
                    var eventiJson = await client.GetStringAsync("cr6ef_evento?$select=cr6ef_eventoid,cr6ef_nomeevento,cr6ef_status,cr6ef_venue,cr6ef_dataevento");
                    var eventi = JsonValue.Parse(eventiJson);
                    return new OkObjectResult(eventi?["value"]);
                }
                var eventoResponse = await client.GetAsync($"cr6ef_evento({id})");
                if (!eventoResponse.IsSuccessStatusCode)
                {
                    if (eventoResponse.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        return new NotFoundResult();
                    }
                    eventoResponse.EnsureSuccessStatusCode();
                }
                var eventoJson = await eventoResponse.Content.ReadAsStringAsync();
                return new ContentResult()
                {
                    Content = eventoJson,
                    ContentType = "application/json"
                };
            }
            catch (HttpRequestException ex)
            {
                logger.LogError(ex, "An error has occured while processing Dynamics365-CRM-Eventi request.");
                return new StatusCodeResult(ex.StatusCode.HasValue ? (int)ex.StatusCode.Value : StatusCodes.Status500InternalServerError);
            }
        }
    }
}
