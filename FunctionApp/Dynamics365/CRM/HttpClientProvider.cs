using Microsoft.Extensions.Options;
using Microsoft.Identity.Client;
using Plumsail.DataSource.Dynamics365.CRM.Settings;
using System.Net.Http.Headers;

namespace Plumsail.DataSource.Dynamics365.CRM
{
    public class HttpClientProvider(IOptions<AppSettings> settings)
    {
        private readonly AzureApp _azureAppSettings = settings.Value.AzureApp;

        public HttpClient Create()
        {
            var client = new HttpClient(new OAuthMessageHandler(_azureAppSettings, new HttpClientHandler()));
            client.BaseAddress = new Uri($"{_azureAppSettings.DynamicsUrl}/api/data/v9.2/");
            client.Timeout = new TimeSpan(0, 2, 0);
            client.DefaultRequestHeaders.Add("OData-MaxVersion", "4.0");
            client.DefaultRequestHeaders.Add("OData-Version", "4.0");
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            return client;
        }
    }

    class OAuthMessageHandler(AzureApp azureAppSettings, HttpMessageHandler innerHandler)
        : DelegatingHandler(innerHandler)
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var app = ConfidentialClientApplicationBuilder.Create(azureAppSettings.ClientId)
               .WithClientSecret(azureAppSettings.ClientSecret)
               .WithAuthority($"https://login.microsoftonline.com/{azureAppSettings.Tenant}")
               .Build();

            var result = await app.AcquireTokenForClient(new[] { $"{azureAppSettings.DynamicsUrl}/.default" })
                .ExecuteAsync(cancellationToken);

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", result.AccessToken);
            return await base.SendAsync(request, cancellationToken);
        }
    }
}
