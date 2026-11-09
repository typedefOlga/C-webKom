namespace CoreLibrary;

public static class Fitness
{
    public static double TourLength(
        ReadOnlySpan<int> tour,
        double[,] d)
    {
        if (tour.Length < 2)
            return 0;

        double s = 0;

        for (int i = 0; i < tour.Length - 1; i++)
            s += d[tour[i], tour[i + 1]];

        s += d[tour[^1], tour[0]];

        return s;
    }

    public static void EvaluateAllSequential(
        int[][] pop,
        double[,] d,
        double[] fit,
        CancellationToken ct = default)
    {
        for (int i = 0; i < pop.Length; i++)
        {
            ct.ThrowIfCancellationRequested();

            fit[i] = TourLength(pop[i], d);
        }
    }
    public static void EvaluateAll(
    int[][] pop,
    double[,] d,
    double[] fit,
    int parallelThreshold = 32,
    CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (pop.Length < parallelThreshold)
        {
            for (int i = 0; i < pop.Length; i++)
            {
                ct.ThrowIfCancellationRequested();
                fit[i] = TourLength(pop[i], d);
            }

            return;
        }

        var options = new ParallelOptions
        {
            CancellationToken = ct
        };

        Parallel.For(
            0,
            pop.Length,
            options,
            i =>
            {
                fit[i] = TourLength(pop[i], d);
            });
    }

    public static (int index, double value) ArgMin(
        double[] a)
    {
        if (a.Length == 0)
            throw new ArgumentException(
                "Массив пуст.");

        int index = 0;
        double value = a[0];

        for (int i = 1; i < a.Length; i++)
        {
            if (a[i] < value)
            {
                value = a[i];
                index = i;
            }
        }

        return (index, value);
    }
}
