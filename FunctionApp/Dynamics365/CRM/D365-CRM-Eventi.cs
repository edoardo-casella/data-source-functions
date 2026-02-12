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
                
                // *** LOGICA PER LA LISTA (TUTTI I RECORD) ***
                if (!id.HasValue)
                {
                    // NOTA: cr6ef_Evento con la 'E' maiuscola nell'expand
                    var query = "cr6ef_calendariovenues" +
                        "?$filter=cr6ef_tipologia eq 848780001" +
                        "&$expand=cr6ef_Evento($select=cr6ef_eventoid,cr6ef_nomeevento,cr6ef_status)" +
                        "&$select=cr6ef_calendariovenueid,cr6ef_inizio,cr6ef_venue,cr6ef_tipologia";
                    
                    var calendarioVenueJson = await client.GetStringAsync(query);
                    var calendarioVenue = JsonNode.Parse(calendarioVenueJson);
                    
                    var value = calendarioVenue?["value"]?.AsArray();
                    if (value != null)
                    {
                        var transformed = new JsonArray();
                        
                        foreach (var item in value)
                        {
                            // NOTA: cr6ef_Evento con la 'E' maiuscola per leggere il JSON
                            var evento = item?["cr6ef_Evento"];
                            
                            // Se non c'è l'evento collegato, saltiamo il record o gestiamo come null
                            if (evento == null) continue;
                            
                            var transformedItem = new JsonObject
                            {
                                ["calendariovenueid"] = item?["cr6ef_calendariovenueid"]?.DeepClone(),
                                ["dataEvento"] = item?["cr6ef_inizio"]?.DeepClone(),
                                ["venue"] = item?["cr6ef_venue"]?.DeepClone(),
                                ["tipologia"] = item?["cr6ef_tipologia"]?.DeepClone(),
                                ["eventoId"] = evento?["cr6ef_eventoid"]?.DeepClone(),
                                ["nomeEvento"] = evento?["cr6ef_nomeevento"]?.DeepClone(),
                                ["status"] = evento?["cr6ef_status"]?.DeepClone()
                            };
                            
                            transformed.Add(transformedItem);
                        }
                        
                        return new OkObjectResult(transformed);
                    }
                    
                    return new OkObjectResult(new JsonArray());
                }
                
                // *** LOGICA PER IL SINGOLO RECORD ***
                
                // NOTA: cr6ef_Evento con la 'E' maiuscola nell'expand
                var singleQuery = $"cr6ef_calendariovenues({id})" +
                    "?$expand=cr6ef_Evento($select=cr6ef_eventoid,cr6ef_nomeevento,cr6ef_status)" +
                    "&$select=cr6ef_calendariovenueid,cr6ef_inizio,cr6ef_venue,cr6ef_tipologia";
                
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
                var calendario = JsonNode.Parse(calendarioJson);
                
                // NOTA: cr6ef_Evento con la 'E' maiuscola per leggere il JSON
                var eventoData = calendario?["cr6ef_Evento"];
                
                var transformedSingle = new JsonObject
                {
                    ["calendariovenueid"] = calendario?["cr6ef_calendariovenueid"]?.DeepClone(),
                    ["dataEvento"] = calendario?["cr6ef_inizio"]?.DeepClone(),
                    ["venue"] = calendario?["cr6ef_venue"]?.DeepClone(),
                    ["tipologia"] = calendario?["cr6ef_tipologia"]?.DeepClone(),
                    ["eventoId"] = eventoData?["cr6ef_eventoid"]?.DeepClone(),
                    ["nomeEvento"] = eventoData?["cr6ef_nomeevento"]?.DeepClone(),
                    ["status"] = eventoData?["cr6ef_status"]?.DeepClone()
                };
                
                return new OkObjectResult(transformedSingle);
            }
            catch (HttpRequestException ex)
            {
                logger.LogError(ex, "An error has occurred while processing Dynamics365-CRM-Eventi request.");
                return new StatusCodeResult(ex.StatusCode.HasValue ? (int)ex.StatusCode.Value : StatusCodes.Status500InternalServerError);
            }
        }
    }
}
