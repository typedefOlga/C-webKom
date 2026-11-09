using System.Text.Json;
using KomPloblemApp.Services;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

public class ComputeServiceScenarioTests
    : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public ComputeServiceScenarioTests(
        WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Evaluate_ValidRequest_ReturnsCorrectFitness()
    {
        var request = new
        {
            Distances = new[]
            {
                new[] { 0.0, 1.0, 4.0 },
                new[] { 2.0, 0.0, 3.0 },
                new[] { 5.0, 6.0, 0.0 }
            },

            Routes = new[]
            {
                new[] { 0, 1, 2 },
                new[] { 0, 2, 1 }
            }
        };

        var response = await _client.PostAsJsonAsync(
            "/api/compute/evaluate",
            request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content
            .ReadFromJsonAsync<EvaluateResponse>();

        Assert.NotNull(result);

        Assert.Equal(
            new[] { 9.0, 12.0 },
            result.Fitness);
    }

    [Fact]
    public async Task Evaluate_NonSquareMatrix_ReturnsBadRequest()
    {
        // Arrange: матрица имеет 2 строки и 3 столбца.
        var request = new
        {
            Distances = new[]
            {
                new[] { 0.0, 1.0, 2.0 },
                new[] { 1.0, 0.0, 3.0 }
            },

            Routes = new[]
            {
                new[] { 0, 1 }
            }
        };

        // Act: отправляем некорректный запрос.
        var response = await _client.PostAsJsonAsync(
            "/api/compute/evaluate",
            request);

        // Assert: сервис должен отклонить запрос.
        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);
    }


    [Fact]
    public async Task Evaluate_RouteWithDuplicateCity_ReturnsBadRequest()
    {
        // Arrange: в маршруте город 1 встречается дважды.
        var request = new
        {
            Distances = new[]
            {
            new[] { 0.0, 1.0, 4.0 },
            new[] { 2.0, 0.0, 3.0 },
            new[] { 5.0, 6.0, 0.0 }
        },

            Routes = new[]
            {
            new[] { 0, 1, 1 }
        }
        };

        // Act: отправляем запрос сервису.
        var response = await _client.PostAsJsonAsync(
            "/api/compute/evaluate",
            request);

        // Assert: ожидаем HTTP 400.
        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);
    }


    [Fact]
    public async Task Evaluate_RouteWithOutOfRangeCity_ReturnsBadRequest()
    {
        // Arrange: индекс 3 отсутствует в матрице из трёх городов.
        var request = new
        {
            Distances = new[]
            {
            new[] { 0.0, 1.0, 4.0 },
            new[] { 2.0, 0.0, 3.0 },
            new[] { 5.0, 6.0, 0.0 }
        },

            Routes = new[]
            {
            new[] { 0, 1, 3 }
        }
        };

        // Act.
        var response = await _client.PostAsJsonAsync(
            "/api/compute/evaluate",
            request);

        // Assert.
        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);
    }


    [Fact]
    public async Task Evaluate_NegativeDistance_ReturnsBadRequest()
    {
        // Arrange: расстояние между городами 0 и 1 отрицательное.
        var request = new
        {
            Distances = new[]
            {
            new[] { 0.0, -1.0, 4.0 },
            new[] { 2.0, 0.0, 3.0 },
            new[] { 5.0, 6.0, 0.0 }
        },

            Routes = new[]
            {
            new[] { 0, 1, 2 }
        }
        };

        // Act.
        var response = await _client.PostAsJsonAsync(
            "/api/compute/evaluate",
            request);

        // Assert.
        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);
    }


    [Fact]
    public async Task Evaluate_RouteWithMissingCity_ReturnsBadRequest()
    {
        // Arrange: в маршруте отсутствует город 2.
        var request = new
        {
            Distances = new[]
            {
            new[] { 0.0, 1.0, 4.0 },
            new[] { 2.0, 0.0, 3.0 },
            new[] { 5.0, 6.0, 0.0 }
        },

            Routes = new[]
            {
            new[] { 0, 1 }
        }
        };

        // Act.
        var response = await _client.PostAsJsonAsync(
            "/api/compute/evaluate",
            request);

        // Assert.
        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);
    }

    [Fact]
    public async Task ServiceFailsTest()
    {
        // Arrange: первый сервис всегда возвращает HTTP 503.
        var failedHandler = new FailingServiceHandler();

        // Второй сервис успешно вычисляет длины маршрутов.
        var workingHandler = new WorkingServiceHandler();

        var failedHttpClient = new HttpClient(failedHandler)
        {
            BaseAddress = new Uri("http://failed-service/")
        };

        var workingHttpClient = new HttpClient(workingHandler)
        {
            BaseAddress = new Uri("http://working-service/")
        };

        var clients = new[]
        {
        new ComputeServiceClient(failedHttpClient),
        new ComputeServiceClient(workingHttpClient)
    };

        using var evaluator = new DistributedFitnessEvaluator(clients);

        double[,] distances =
        {
        { 0, 1, 4 },
        { 2, 0, 3 },
        { 5, 6, 0 }
    };

        // Четыре маршрута распределятся по два между сервисами.
        int[][] routes =
        {
        new[] { 0, 1, 2 },
        new[] { 0, 2, 1 },
        new[] { 0, 1, 2 },
        new[] { 0, 2, 1 }
    };

        // Act: запускаем распределённое вычисление.
        double[] result = await evaluator.EvaluateAsync(
            distances,
            routes);

        // Assert: все маршруты вычислены.
        Assert.Equal(
            new[] { 9.0, 12.0, 9.0, 12.0 },
            result);

        // Неработающий сервис действительно получил запрос.
        Assert.Equal(1, failedHandler.RequestCount);

        // Работающий сервис получил свою часть и повторно
        // отправленную часть отказавшего сервиса.
        Assert.Equal(2, workingHandler.RequestCount);
        Assert.Equal(4, workingHandler.TotalRoutesReceived);
    }
    [Fact]
    public async Task AllServicesFail()
    {
        var firstHandler = new FailingServiceHandler();
        var secondHandler = new FailingServiceHandler();

        using var firstHttpClient = new HttpClient(firstHandler)
        {
            BaseAddress = new Uri("http://first-service/")
        };

        using var secondHttpClient = new HttpClient(secondHandler)
        {
            BaseAddress = new Uri("http://second-service/")
        };

        var clients = new[]
        {
        new ComputeServiceClient(firstHttpClient),
        new ComputeServiceClient(secondHttpClient)
    };

        using var evaluator = new DistributedFitnessEvaluator(clients);

        double[,] distances =
        {
        { 0, 1, 4 },
        { 2, 0, 3 },
        { 5, 6, 0 }
    };

        int[][] routes =
        {
        new[] { 0, 1, 2 },
        new[] { 0, 2, 1 },
        new[] { 0, 1, 2 },
        new[] { 0, 2, 1 }
    };

        // Проверяем, что при отказе всех сервисов возникает исключение.
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => evaluator.EvaluateAsync(distances, routes));

        // Проверяем, что оба сервиса действительно получили запросы.
        Assert.True(
            firstHandler.RequestCount >= 1,
            $"Первый сервис получил {firstHandler.RequestCount} запросов.");

        Assert.True(
            secondHandler.RequestCount >= 1,
            $"Второй сервис получил {secondHandler.RequestCount} запросов.");
    }
    private class FailingServiceHandler : HttpMessageHandler
    {
        private int _requestCount;

        public int RequestCount => _requestCount;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _requestCount);

            return Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        }
    }

    private class WorkingServiceHandler : HttpMessageHandler
    {
        private int _requestCount;
        private int _totalRoutesReceived;

        public int RequestCount => _requestCount;

        public int TotalRoutesReceived => _totalRoutesReceived;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _requestCount);

            string json = await request.Content!
                .ReadAsStringAsync(cancellationToken);

            using JsonDocument document = JsonDocument.Parse(json);

            JsonElement root = document.RootElement;

            JsonElement distancesElement = root.GetProperty("distances");
            JsonElement routesElement = root.GetProperty("routes");

            double[][] distances = distancesElement
                .EnumerateArray()
                .Select(row => row.EnumerateArray()
                    .Select(value => value.GetDouble())
                    .ToArray())
                .ToArray();

            int[][] routes = routesElement
                .EnumerateArray()
                .Select(route => route.EnumerateArray()
                    .Select(city => city.GetInt32())
                    .ToArray())
                .ToArray();

            Interlocked.Add(ref _totalRoutesReceived, routes.Length);

            // Вычисляем длину каждого цикла.
            double[] fitness = routes.Select(route =>
            {
                double length = 0;

                for (int i = 0; i < route.Length; i++)
                {
                    int from = route[i];
                    int to = route[(i + 1) % route.Length];

                    length += distances[from][to];
                }

                return length;
            }).ToArray();

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new { Fitness = fitness })
            };
        }
    }

    private class EvaluateResponse
    {
        public double[] Fitness { get; set; } = [];
    }
}
