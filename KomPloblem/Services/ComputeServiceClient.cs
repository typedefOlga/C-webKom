
using System.Net.Http;
using System.Net.Http.Json;

namespace KomPloblemApp.Services;

public class ComputeServiceClient : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;

    public ComputeServiceClient(string serviceUrl)
    {
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(serviceUrl),
            Timeout = TimeSpan.FromSeconds(5)
        };

        _ownsHttpClient = true;
    }

    public ComputeServiceClient(HttpClient httpClient)
    {
        _httpClient = httpClient
            ?? throw new ArgumentNullException(nameof(httpClient));

        _ownsHttpClient = false;
    }

    public async Task<double[]> EvaluateAsync(
        double[,] distances,
        int[][] routes,
        CancellationToken cancellationToken = default)
    {
        int n = distances.GetLength(0);

        var distancesArray = new double[n][];

        for (int i = 0; i < n; i++)
        {
            distancesArray[i] = new double[n];

            for (int j = 0; j < n; j++)
            {
                distancesArray[i][j] = distances[i, j];
            }
        }

        var request = new EvaluateRequest
        {
            Distances = distancesArray,
            Routes = routes
        };

        using var response = await _httpClient.PostAsJsonAsync(
            "api/compute/evaluate",
            request,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<EvaluateResponse>(
            cancellationToken: cancellationToken);

        if (result?.Fitness == null)
        {
            throw new InvalidOperationException(
                "Сервис вернул пустой результат.");
        }

        if (result.Fitness.Length != routes.Length)
        {
            throw new InvalidOperationException(
                "Количество результатов не совпадает с количеством маршрутов.");
        }

        return result.Fitness;
    }

    public void Dispose()
    {
        if (_ownsHttpClient)
            _httpClient.Dispose();
    }

    private class EvaluateRequest
    {
        public double[][] Distances { get; set; } = [];
        public int[][] Routes { get; set; } = [];
    }

    private class EvaluateResponse
    {
        public double[] Fitness { get; set; } = [];
    }
}
