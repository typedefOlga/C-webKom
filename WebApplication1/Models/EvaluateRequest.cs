
namespace ComputeService.Models;

public class EvaluateRequest
{
    public double[][] Distances { get; set; } = [];

    public int[][] Routes { get; set; } = [];
}