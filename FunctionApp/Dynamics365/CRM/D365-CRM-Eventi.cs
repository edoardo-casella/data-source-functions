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
                
                // Header per ottenere le etichette testuali (es. "Unipol Arena", "In Vendita")
                if (!client.DefaultRequestHeaders.Contains("Prefer"))
                {
                    client.DefaultRequestHeaders.Add("Prefer", "odata.include-annotations=\"OData.Community.Display.V1.FormattedValue\"");
                }

                // *** LOGICA PER LA LISTA (TUTTI I RECORD) ***
                if (!id.HasValue)
                {
                    // Codici Filtro
                    int tipoEventoPrincipale = 848780001;
                    int statoAttivo = 0; // 0 = Active, 1 = Inactive
                    int statusInVendita = 848780004; // Recuperato dai tuoi metadati

                    var query = "cr6ef_calendariovenues" +
                        // 1. Filtro Tipologia (Evento Principale)
                        $"?$filter=cr6ef_tipologia eq {tipoEventoPrincipale}" +
                        
                        // 2. Filtro SOLO ATTIVI (Esclude i record disattivati)
                        $" and statecode eq {statoAttivo}" +
                        
                        // 3. Filtro STATUS EVENTO = IN VENDITA (Tramite la relazione cr6ef_Evento)
                        $" and cr6ef_Evento/cr6ef_status eq {statusInVendita}" +

                        // EXPAND: Recuperiamo i dati dell'evento (inclusa la Venue che è lì dentro)
                        "&$expand=cr6ef_Evento($select=cr6ef_eventoid,cr6ef_nomeevento,cr6ef_status,cr6ef_venue)" +
                        
                        // SELECT: Campi del calendario
                        "&$select=cr6ef_calendariovenueid,cr6ef_inizio,cr6ef_tipologia";
                    
                    var response = await client.GetAsync(query);

                    if (!response.IsSuccessStatusCode)
                    {
                        var errorContent = await response.Content.ReadAsStringAsync();
                        throw new HttpRequestException($"Dataverse List Error: {response.StatusCode} - {errorContent}", null, response.StatusCode);
                    }

                    var jsonContent = await response.Content.ReadAsStringAsync();
                    var rootNode = JsonNode.Parse(jsonContent);
                    
                    var value = rootNode?["value"]?.AsArray();
                    if (value != null)
                    {
                        var transformed = new JsonArray();
                        
                        foreach (var item in value)
                        {
                            var evento = item?["cr6ef_Evento"];
                            if (evento == null) continue;
                            
                            var transformedItem = new JsonObject
                            {
                                ["calendariovenueid"] = item?["cr6ef_calendariovenueid"]?.DeepClone(),
                                ["dataEvento"] = item?["cr6ef_inizio"]?.DeepClone(),
                                
                                // VENUE (Presa dall'evento collegato)
                                ["venueId"] = evento?["cr6ef_venue"]?.DeepClone(),
                                ["venueName"] = evento?["cr6ef_venue@OData.Community.Display.V1.FormattedValue"]?.DeepClone(),
                                
                                // Tipologia Calendario
                                ["tipologia"] = item?["cr6ef_tipologia"]?.DeepClone(),
                                ["tipologiaLabel"] = item?["cr6ef_tipologia@OData.Community.Display.V1.FormattedValue"]?.DeepClone(),
                                
                                // Dati Evento
                                ["eventoId"] = evento?["cr6ef_eventoid"]?.DeepClone(),
                                ["nomeEvento"] = evento?["cr6ef_nomeevento"]?.DeepClone(),
                                ["status"] = evento?["cr6ef_status"]?.DeepClone(),
                                ["statusLabel"] = evento?["cr6ef_status@OData.Community.Display.V1.FormattedValue"]?.DeepClone()
                            };
                            
                            transformed.Add(transformedItem);
                        }
                        
                        return new OkObjectResult(transformed);
                    }
                    
                    return new OkObjectResult(new JsonArray());
                }
                
                // *** LOGICA PER IL SINGOLO RECORD ***
                // Qui non mettiamo filtri di stato, perché se chiedo un ID specifico voglio vederlo anche se è bozza/inattivo
                var singleQuery = $"cr6ef_calendariovenues({id})" +
                    "?$expand=cr6ef_Evento($select=cr6ef_eventoid,cr6ef_nomeevento,cr6ef_status,cr6ef_venue)" +
                    "&$select=cr6ef_calendariovenueid,cr6ef_inizio,cr6ef_tipologia";
                
                var singleResponse = await client.GetAsync(singleQuery);
                
                if (!singleResponse.IsSuccessStatusCode)
                {
                    if (singleResponse.StatusCode == System.Net.HttpStatusCode.NotFound) return new NotFoundResult();
                    var errorDetail = await singleResponse.Content.ReadAsStringAsync();
                    throw new HttpRequestException($"Dataverse Single Error: {singleResponse.StatusCode} - {errorDetail}", null, singleResponse.StatusCode);
                }
                
                var singleJson = await singleResponse.Content.ReadAsStringAsync();
                var calendario = JsonNode.Parse(singleJson);
                var eventoData = calendario?["cr6ef_Evento"];
                
                var transformedSingle = new JsonObject
                {
                    ["calendariovenueid"] = calendario?["cr6ef_calendariovenueid"]?.DeepClone(),
                    ["dataEvento"] = calendario?["cr6ef_inizio"]?.DeepClone(),
                    
                    ["venueId"] = eventoData?["cr6ef_venue"]?.DeepClone(),
                    ["venueName"] = eventoData?["cr6ef_venue@OData.Community.Display.V1.FormattedValue"]?.DeepClone(),
                    
                    ["tipologia"] = calendario?["cr6ef_tipologia"]?.DeepClone(),
                    ["tipologiaLabel"] = calendario?["cr6ef_tipologia@OData.Community.Display.V1.FormattedValue"]?.DeepClone(),
                    
                    ["eventoId"] = eventoData?["cr6ef_eventoid"]?.DeepClone(),
                    ["nomeEvento"] = eventoData?["cr6ef_nomeevento"]?.DeepClone(),
                    ["status"] = eventoData?["cr6ef_status"]?.DeepClone(),
                    ["statusLabel"] = eventoData?["cr6ef_status@OData.Community.Display.V1.FormattedValue"]?.DeepClone()
                };
                
                return new OkObjectResult(transformedSingle);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Errore Function Eventi");
                var errorResponse = new 
                {
                    error = "Errore esecuzione",
                    details = ex.Message,
                    type = ex.GetType().Name
                };
                return new ObjectResult(errorResponse) { StatusCode = 500 };
            }
        }
    }
}
