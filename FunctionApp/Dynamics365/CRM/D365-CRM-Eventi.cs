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
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "crm/eventi/{id?}")] HttpRequest req, 
            Guid? id)
        {
            logger.LogInformation("Dynamics365-CRM-Eventi is requested.");
            
            try
            {
                var client = httpClientProvider.Create();
                
                if (!id.HasValue)
                {
                    // Query Calendar Venue con expand su Evento
                    var query = "cr6ef_calendariovenueSet" +
                        "?$filter=cr6ef_tipologia eq 848780001" +
                        "&$expand=cr6ef_evento($select=cr6ef_eventoid,cr6ef_nomeevento,cr6ef_status)" +
                        "&$select=cr6ef_calendariove nueid,cr6ef_data,cr6ef_venue,cr6ef_tipologia";
                    
                    var calendarioVenueJson = await client.GetStringAsync(query);
                    var calendarioVenue = JsonValue.Parse(calendarioVenueJson);
                    
                    // Trasforma i dati per appiattire la struttura
                    var value = calendarioVenue?["value"]?.AsArray();
                    if (value != null)
                    {
                        var transformed = new JsonArray();
                        
                        foreach (var item in value)
                        {
                            var evento = item?["cr6ef_evento"];
                            
                            // Salta se evento non presente (lookup vuoto)
                            if (evento == null) continue;
                            
                            var transformedItem = new JsonObject
                            {
                                ["calendariovenueId"] = item?["cr6ef_calendariove nueid"],
                                ["dataEvento"] = item?["cr6ef_data"],
                                ["venue"] = item?["cr6ef_venue"],
                                ["tipologia"] = item?["cr6ef_tipologia"],
                                ["eventoId"] = evento?["cr6ef_eventoid"],
                                ["nomeEvento"] = evento?["cr6ef_nomeevento"],
                                ["status"] = evento?["cr6ef_status"]
                            };
                            
                            transformed.Add(transformedItem);
                        }
                        
                        return new OkObjectResult(transformed);
                    }
                    
                    return new OkObjectResult(new JsonArray());
                }
                
                // Query singolo calendar venue con expand
                var singleQuery = $"cr6ef_calendariovenueSet({id})" +
                    "?$expand=cr6ef_evento($select=cr6ef_eventoid,cr6ef_nomeevento,cr6ef_status)" +
                    "&$select=cr6ef_calendariove nueid,cr6ef_data,cr6ef_venue,cr6ef_tipologia";
                
                var calendarioResponse = await client.GetAsync(singleQuery);
                
                if (!calendarioResponse.IsSuccessStatusCode)
                {
                    if (calendarioResponse.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        return new NotFoundResult();
                    }
                    calendarioResponse.EnsureSuccessStatusCode();
                }
                
                var calendarioJson = await calendarioResponse.Content.ReadAsStringAsync();
                var calendario = JsonValue.Parse(calendarioJson);
                var eventoData = calendario?["cr6ef_evento"];
                
                // Trasforma anche il singolo item
                var transformedSingle = new JsonObject
                {
                    ["calendariovenueId"] = calendario?["cr6ef_calendariove nueid"],
                    ["dataEvento"] = calendario?["cr6ef_data"],
                    ["venue"] = calendario?["cr6ef_venue"],
                    ["tipologia"] = calendario?["cr6ef_tipologia"],
                    ["eventoId"] = eventoData?["cr6ef_eventoid"],
                    ["nomeEvento"] = eventoData?["cr6ef_nomeevento"],
                    ["status"] = eventoData?["cr6ef_status"]
                };
                
                return new ContentResult()
                {
                    Content = transformedSingle.ToJsonString(),
                    ContentType = "application/json"
                };
            }
            catch (HttpRequestException ex)
            {
                logger.LogError(ex, "An error has occurred while processing Dynamics365-CRM-Eventi request.");
                return new StatusCodeResult(ex.StatusCode.HasValue ? (int)ex.StatusCode.Value : StatusCodes.Status500InternalServerError);
            }
        }
    }
}
