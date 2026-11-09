namespace CoreLibrary;

public interface IFitnessEvaluator
{
    Task<double[]> EvaluateAsync(
        double[,] distances,
        int[][] routes,
        CancellationToken cancellationToken = default);
}
