
using CoreLibrary;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Text.Json;

namespace KomPloblemApp.Services;

/// <summary>
/// Распределяет вычисление Fitness между несколькими сервисами.
/// </summary>
public class DistributedFitnessEvaluator : IFitnessEvaluator, IDisposable
{
    private readonly List<ComputeServiceClient> _clients = [];

    // Храним недоступные сервисы на протяжении всего запуска ГА.
    private readonly ConcurrentDictionary<int, Exception> _unavailableServices = new();

    public DistributedFitnessEvaluator(IEnumerable<string> serviceUrls)
    {
        foreach (string url in serviceUrls)
        {
            if (string.IsNullOrWhiteSpace(url))
                throw new ArgumentException("Адрес сервиса не может быть пустым.");

            _clients.Add(new ComputeServiceClient(url));
        }

        if (_clients.Count == 0)
        {
            throw new ArgumentException(
                "Необходимо указать хотя бы один вычислительный сервис.");
        }
    }

    public DistributedFitnessEvaluator(
        IEnumerable<ComputeServiceClient> clients)
    {
        _clients = clients?.ToList()
            ?? throw new ArgumentNullException(nameof(clients));

        if (_clients.Count == 0)
        {
            throw new ArgumentException(
                "Необходимо указать хотя бы один вычислительный сервис.");
        }
    }

    public async Task<double[]> EvaluateAsync(
        double[,] distances,
        int[][] routes,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (routes.Length == 0)
            return [];

        // Учитываем только те сервисы, которые ещё не отказали.
        int availableServices = _clients.Count - _unavailableServices.Count;

        if (availableServices == 0)
        {
            throw new InvalidOperationException(
                "Все вычислительные сервисы недоступны.");
        }

        int activeServices = Math.Min(availableServices, routes.Length);

        int routesPerService = routes.Length / activeServices;
        int remainder = routes.Length % activeServices;

        var fitness = new double[routes.Length];
        var tasks = new List<Task>();

        // Собираем индексы сервисов, которые ещё доступны.
        int[] availableServiceIndexes = Enumerable
            .Range(0, _clients.Count)
            .Where(i => !_unavailableServices.ContainsKey(i))
            .ToArray();

        int startIndex = 0;

        for (int i = 0; i < activeServices; i++)
        {
            int count = routesPerService;

            if (i < remainder)
                count++;

            var part = new int[count][];

            Array.Copy(routes, startIndex, part, 0, count);

            int currentStartIndex = startIndex;
            int initialServiceIndex = availableServiceIndexes[i];

            tasks.Add(
                EvaluatePartWithFailoverAsync(
                    distances,
                    part,
                    currentStartIndex,
                    initialServiceIndex,
                    fitness,
                    cancellationToken));

            startIndex += count;
        }

        await Task.WhenAll(tasks);

        return fitness;
    }

    private async Task EvaluatePartWithFailoverAsync(
        double[,] distances,
        int[][] routes,
        int startIndex,
        int initialServiceIndex,
        double[] fitness,
        CancellationToken cancellationToken)
    {
        var errors = new List<Exception>();

        for (int attempt = 0; attempt < _clients.Count; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            int serviceIndex =
                (initialServiceIndex + attempt) % _clients.Count;

            // Не обращаемся к сервису, который уже отказал.
            if (_unavailableServices.ContainsKey(serviceIndex))
                continue;

            try
            {
                double[] result = await _clients[serviceIndex].EvaluateAsync(
                    distances,
                    routes,
                    cancellationToken);

                cancellationToken.ThrowIfCancellationRequested();

                Array.Copy(
                    result,
                    0,
                    fitness,
                    startIndex,
                    result.Length);

                return;
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex) when (
                ex is HttpRequestException ||
                ex is TaskCanceledException ||
                ex is JsonException ||
                ex is InvalidOperationException)
            {
                // Запоминаем отказ. В следующих поколениях
                // этот сервис больше не будет получать запросы.
                _unavailableServices.TryAdd(serviceIndex, ex);

                errors.Add(ex);
            }
        }

        throw new InvalidOperationException(
            "Не удалось вычислить часть маршрутов: все доступные " +
            "вычислительные сервисы завершили запрос с ошибкой.",
            new AggregateException(errors));
    }

    public void Dispose()
    {
        foreach (var client in _clients)
        {
            client.Dispose();
        }
    }
}
