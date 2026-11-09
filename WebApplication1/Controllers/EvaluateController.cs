
using CoreLibrary;
using ComputeService.Models;
using Microsoft.AspNetCore.Mvc;

namespace ComputeService.Controllers;

[ApiController]
[Route("api/compute")]
public class EvaluateController : ControllerBase
{
    [HttpPost("evaluate")]
    public ActionResult<EvaluateResponse> Evaluate(
        [FromBody] EvaluateRequest request)
    {
        // Проверяем наличие данных.
        if (request.Distances is null || request.Routes is null)
        {
            return BadRequest(new
            {
                error = "Матрица расстояний и список маршрутов обязательны."
            });
        }

        int n = request.Distances.Length;

        if (n < 2)
        {
            return BadRequest(new
            {
                error = "Матрица должна содержать расстояния хотя бы для двух городов."
            });
        }

        // Проверяем, что матрица квадратная.
        for (int i = 0; i < n; i++)
        {
            if (request.Distances[i] is null ||
                request.Distances[i].Length != n)
            {
                return BadRequest(new
                {
                    error = "Матрица расстояний должна быть квадратной."
                });
            }

            for (int j = 0; j < n; j++)
            {
                double distance = request.Distances[i][j];

                if (!double.IsFinite(distance) || distance < 0)
                {
                    return BadRequest(new
                    {
                        error = "Расстояния должны быть конечными неотрицательными числами."
                    });
                }
            }
        }

        // Проверяем наличие маршрутов.
        if (request.Routes.Length == 0)
        {
            return BadRequest(new
            {
                error = "Список маршрутов пуст."
            });
        }

        // Преобразуем JSON-матрицу в формат CoreLibrary.
        var distances = new double[n, n];

        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j < n; j++)
            {
                distances[i, j] = request.Distances[i][j];
            }
        }

        // Проверяем корректность всех маршрутов.
        for (int i = 0; i < request.Routes.Length; i++)
        {
            int[] route = request.Routes[i];

            if (route is null || route.Length != n)
            {
                return BadRequest(new
                {
                    error = $"Маршрут {i} должен содержать ровно {n} городов."
                });
            }

            // Проверяем, что маршрут является перестановкой городов 0..N-1.
            var visited = new bool[n];

            foreach (int city in route)
            {
                if (city < 0 || city >= n || visited[city])
                {
                    return BadRequest(new
                    {
                        error = $"Маршрут {i} содержит неверные или повторяющиеся города."
                    });
                }

                visited[city] = true;
            }
        }

        // Параллельно вычисляем длины всех маршрутов.
        var fitness = new double[request.Routes.Length];

        Fitness.EvaluateAll(
            request.Routes,
            distances,
            fitness,
            ct: HttpContext.RequestAborted);

        return Ok(new EvaluateResponse
        {
            Fitness = fitness
        });
    }
}
