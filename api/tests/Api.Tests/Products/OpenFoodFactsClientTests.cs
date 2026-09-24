using System.Net;
using System.Text;
using Api.Options;
using Api.Services.Products;
using Microsoft.Extensions.Logging.Abstractions;

namespace Api.Tests.Products;

/// <summary>
/// Le client est testé contre un faux serveur (HttpMessageHandler) qui rejoue
/// des réponses réelles d'Open Food Facts : aucun appel réseau.
/// </summary>
public class OpenFoodFactsClientTests
{
    internal const string SkyrJson =
        """
        {"code":"3033490004743","product":{"brands":"DANONE PRODUITS FRAIS FRANCE",
        "categories_tags":["en:dairies","en:fermented-milk-products","en:yogurts"],
        "code":"3033490004743","generic_name_fr":"SKYR","product_name":"Skyr","product_name_fr":"Skyr",
        "product_quantity":140,"product_quantity_unit":"g",
        "image_front_small_url":"https://images.openfoodfacts.org/x.jpg"},"status":1,"status_verbose":"product found"}
        """;

    [Fact]
    public async Task KnownProduct_IsParsed()
    {
        var (client, handler) = CreateClient(_ => Json(HttpStatusCode.OK, SkyrJson));

        var product = await client.GetProductAsync("3033490004743", CancellationToken.None);

        Assert.NotNull(product);
        Assert.Equal("Skyr", product.Name);
        Assert.Equal("DANONE PRODUITS FRAIS FRANCE", product.Brands);
        Assert.Equal(["en:dairies", "en:fermented-milk-products", "en:yogurts"], product.CategoryTags);
        Assert.Equal(140m, product.Quantity);
        Assert.Equal("g", product.QuantityUnit);
        // Seuls les champs utiles sont demandés.
        Assert.StartsWith("/api/v2/product/3033490004743?fields=", handler.LastRequest!.RequestUri!.PathAndQuery);
    }

    [Fact]
    public async Task FrenchName_IsPreferred_ThenGenericName()
    {
        var json = """{"status":1,"product":{"product_name":"Skyr nature","product_name_fr":"","generic_name_fr":"Spécialité laitière"}}""";
        var (client, _) = CreateClient(_ => Json(HttpStatusCode.OK, json));

        var product = await client.GetProductAsync("3033490004743", CancellationToken.None);

        // product_name_fr vide : on passe au nom international.
        Assert.Equal("Skyr nature", product!.Name);
    }

    [Fact]
    public async Task QuantitySentAsText_IsAccepted()
    {
        var json = """{"status":1,"product":{"product_name":"Lait","product_quantity":"1000","product_quantity_unit":"ml"}}""";
        var (client, _) = CreateClient(_ => Json(HttpStatusCode.OK, json));

        var product = await client.GetProductAsync("3000000000001", CancellationToken.None);

        Assert.Equal(1000m, product!.Quantity);
    }

    [Fact]
    public async Task UnknownProduct_WithStatusZero_ReturnsNull()
    {
        // Comportement réel d'OFF : HTTP 200 avec « status: 0 ».
        var (client, _) = CreateClient(_ => Json(HttpStatusCode.OK, """{"code":"3000000000001","status":0,"status_verbose":"product not found"}"""));

        Assert.Null(await client.GetProductAsync("3000000000001", CancellationToken.None));
    }

    [Fact]
    public async Task UnknownProduct_With404_ReturnsNull()
    {
        var (client, _) = CreateClient(_ => Json(HttpStatusCode.NotFound, """{"status":0}"""));

        Assert.Null(await client.GetProductAsync("3000000000001", CancellationToken.None));
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.BadGateway)]
    public async Task ServerError_ThrowsUnavailable(HttpStatusCode status)
    {
        var (client, _) = CreateClient(_ => Json(status, "{}"));

        await Assert.ThrowsAsync<OpenFoodFactsUnavailableException>(() =>
            client.GetProductAsync("3033490004743", CancellationToken.None));
    }

    [Fact]
    public async Task InvalidJson_ThrowsUnavailable()
    {
        var (client, _) = CreateClient(_ => Json(HttpStatusCode.OK, "<html>maintenance</html>"));

        await Assert.ThrowsAsync<OpenFoodFactsUnavailableException>(() =>
            client.GetProductAsync("3033490004743", CancellationToken.None));
    }

    [Fact]
    public async Task NetworkError_ThrowsUnavailable()
    {
        var (client, _) = CreateClient(_ => throw new HttpRequestException("Connexion refusée"));

        await Assert.ThrowsAsync<OpenFoodFactsUnavailableException>(() =>
            client.GetProductAsync("3033490004743", CancellationToken.None));
    }

    [Fact]
    public async Task Timeout_ThrowsUnavailable()
    {
        var (client, _) = CreateClient(
            async ct =>
            {
                await Task.Delay(TimeSpan.FromSeconds(5), ct);
                return Json(HttpStatusCode.OK, SkyrJson);
            },
            timeout: TimeSpan.FromMilliseconds(100));

        await Assert.ThrowsAsync<OpenFoodFactsUnavailableException>(() =>
            client.GetProductAsync("3033490004743", CancellationToken.None));
    }

    [Fact]
    public async Task GlobalLimit_BlocksCallsBeyondTheLimit_WithoutContactingOpenFoodFacts()
    {
        var limiter = Limiter(15);
        var (client, handler) = CreateClient(_ => Task.FromResult(Json(HttpStatusCode.OK, SkyrJson)), limiter: limiter);

        for (var i = 0; i < 15; i++)
        {
            Assert.NotNull(await client.GetProductAsync("3033490004743", CancellationToken.None));
        }

        await Assert.ThrowsAsync<OpenFoodFactsUnavailableException>(() =>
            client.GetProductAsync("3033490004743", CancellationToken.None));
        // Le 16e appel n'est jamais parti vers OFF.
        Assert.Equal(15, handler.Calls);
    }

    [Fact]
    public async Task GlobalLimit_IsSharedByAllClientInstances()
    {
        // ASP.NET crée un client par requête HTTP : la limite doit être commune à tous.
        var limiter = Limiter(2);
        var (first, _) = CreateClient(_ => Task.FromResult(Json(HttpStatusCode.OK, SkyrJson)), limiter: limiter);
        var (second, _) = CreateClient(_ => Task.FromResult(Json(HttpStatusCode.OK, SkyrJson)), limiter: limiter);
        var (third, _) = CreateClient(_ => Task.FromResult(Json(HttpStatusCode.OK, SkyrJson)), limiter: limiter);

        await first.GetProductAsync("3033490004743", CancellationToken.None);
        await second.GetProductAsync("3033490004743", CancellationToken.None);

        await Assert.ThrowsAsync<OpenFoodFactsUnavailableException>(() =>
            third.GetProductAsync("3033490004743", CancellationToken.None));
    }

    [Theory]
    [InlineData("Leftly", "contact@example.com", "Leftly/1.0 (contact@example.com)")]
    [InlineData("Leftly", "", "Leftly/1.0")]
    [InlineData("Mon-Frigo", null, "MonFrigo/1.0")]
    [InlineData("Mon Frigo !", null, "MonFrigo/1.0")]
    public void UserAgent_FollowsOpenFoodFactsFormat(string appName, string? contact, string expected)
    {
        Assert.Equal(expected, OpenFoodFactsUserAgent.Build(appName, contact));
    }

    // ---------- Faux serveur ----------

    internal static HttpResponseMessage Json(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    internal static OpenFoodFactsRateLimiter Limiter(int maxRequestsPerMinute) =>
        new(Microsoft.Extensions.Options.Options.Create(new OpenFoodFactsOptions { MaxRequestsPerMinute = maxRequestsPerMinute }));

    internal static (OpenFoodFactsClient Client, FakeHandler Handler) CreateClient(
        Func<CancellationToken, Task<HttpResponseMessage>> respond,
        TimeSpan? timeout = null,
        OpenFoodFactsRateLimiter? limiter = null)
    {
        var handler = new FakeHandler(respond);
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://world.openfoodfacts.org/"),
            Timeout = timeout ?? TimeSpan.FromSeconds(5),
        };
        var client = new OpenFoodFactsClient(http, limiter ?? Limiter(100), NullLogger<OpenFoodFactsClient>.Instance);
        return (client, handler);
    }

    private static (OpenFoodFactsClient Client, FakeHandler Handler) CreateClient(
        Func<HttpRequestMessage, HttpResponseMessage> respond) =>
        CreateClient(_ => Task.FromResult(respond(null!)));

    internal sealed class FakeHandler(Func<CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        private int _calls;

        public HttpRequestMessage? LastRequest { get; private set; }

        public int Calls => _calls;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Interlocked.Increment(ref _calls);
            LastRequest = request;
            return respond(ct);
        }
    }
}
