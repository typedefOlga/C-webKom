
namespace CoreLibrary;

public class GeneticAlgorithm
{
    // Обычный запуск генетического алгоритма.
    // Если вычислитель не указан, Fitness считается локально.
    public int[] Run(
        KomProblem problem,
        GaOptions opt,
        IProgress<GenerationReport>? progress = null,
        CancellationToken ct = default)
    {
        return RunCoreAsync(
            problem,
            opt,
            progress,
            ct,
            new LocalFitnessEvaluator())
            .GetAwaiter()
            .GetResult();
    }

    // Асинхронный запуск генетического алгоритма.
    // Можно передать внешний вычислитель Fitness.
    public Task<int[]> RunAsync(
        KomProblem problem,
        GaOptions opt,
        IProgress<GenerationReport>? progress = null,
        CancellationToken ct = default,
        IFitnessEvaluator? evaluator = null)
    {
        evaluator ??= new LocalFitnessEvaluator();

        return Task.Run(
            () => RunCoreAsync(
                problem,
                opt,
                progress,
                ct,
                evaluator),
            ct);
    }

    // Основная реализация генетического алгоритма.
    private async Task<int[]> RunCoreAsync(
        KomProblem problem,
        GaOptions opt,
        IProgress<GenerationReport>? progress,
        CancellationToken ct,
        IFitnessEvaluator evaluator)
    {
        ValidateOptions(opt);

        ct.ThrowIfCancellationRequested();

        int n = problem.CityCount;
        var d = problem.Distances;
        var rng = new Random(opt.Seed);

        var pop = new int[opt.PopulationSize][];
        var fit = new double[opt.PopulationSize];

        // Создание начальной популяции.
        for (int i = 0; i < pop.Length; i++)
        {
            ct.ThrowIfCancellationRequested();

            pop[i] = Operators.RandomPermutation(n, rng);
        }

        // Вычисление Fitness начальной популяции.
        fit = await evaluator.EvaluateAsync(d, pop, ct);

        ct.ThrowIfCancellationRequested();

        var (bi, bv) = Fitness.ArgMin(fit);

        var bestTour = (int[])pop[bi].Clone();
        double bestLen = bv;

        progress?.Report(
            new GenerationReport(
                0,
                bestLen,
                Average(fit),
                (int[])bestTour.Clone(),
                false));

        // Основной цикл генетического алгоритма.
        for (int gen = 1;
             gen <= opt.MaxGenerations;
             gen++)
        {
            ct.ThrowIfCancellationRequested();

            // Сортировка индексов особей по Fitness.
            var order = new int[pop.Length];

            for (int i = 0; i < order.Length; i++)
            {
                ct.ThrowIfCancellationRequested();
                order[i] = i;
            }

            Array.Sort(
                (double[])fit.Clone(),
                order);

            ct.ThrowIfCancellationRequested();

            var next = new int[opt.PopulationSize][];
            var nextFit = new double[opt.PopulationSize];

            // Элитизм.
            int elite = Math.Min(
                opt.EliteCount,
                opt.PopulationSize);

            for (int e = 0; e < elite; e++)
            {
                ct.ThrowIfCancellationRequested();

                next[e] = (int[])pop[order[e]].Clone();
                nextFit[e] = fit[order[e]];
            }

            // Создание остальных особей.
            for (int i = elite;
                 i < opt.PopulationSize;
                 i++)
            {
                ct.ThrowIfCancellationRequested();

                // Турнирный отбор первого родителя.
                int p1 = TournamentSelect(
                    fit,
                    opt.TournamentSize,
                    rng);

                ct.ThrowIfCancellationRequested();

                // Турнирный отбор второго родителя.
                int p2 = TournamentSelect(
                    fit,
                    opt.TournamentSize,
                    rng);

                ct.ThrowIfCancellationRequested();

                int[] child;

                // Скрещивание.
                if (rng.NextDouble() < opt.CrossoverProbability)
                {
                    child = Operators.OrderCrossover(
                        pop[p1],
                        pop[p2],
                        rng);
                }
                else
                {
                    child = (int[])pop[p1].Clone();
                }

                ct.ThrowIfCancellationRequested();

                // Мутация.
                if (rng.NextDouble() < opt.MutationProbability)
                {
                    Operators.SwapMutate(child, rng);
                }

                ct.ThrowIfCancellationRequested();

                next[i] = child;
            }

            ct.ThrowIfCancellationRequested();

            // Вычисление Fitness нового поколения.
            nextFit = await evaluator.EvaluateAsync(
                d,
                next,
                ct);

            ct.ThrowIfCancellationRequested();

            pop = next;
            fit = nextFit;

            (bi, bv) = Fitness.ArgMin(fit);

            if (bv < bestLen)
            {
                bestLen = bv;
                bestTour = (int[])pop[bi].Clone();
            }

            ct.ThrowIfCancellationRequested();

            progress?.Report(
                new GenerationReport(
                    gen,
                    bestLen,
                    Average(fit),
                    (int[])bestTour.Clone(),
                    gen == opt.MaxGenerations));
        }

        return (int[])bestTour.Clone();
    }

    private sealed class LocalFitnessEvaluator : IFitnessEvaluator
    {
        public Task<double[]> EvaluateAsync(
            double[,] distances,
            int[][] routes,
            CancellationToken cancellationToken = default)
        {
            var fitness = new double[routes.Length];

            Fitness.EvaluateAll(
                routes,
                distances,
                fitness,
                ct: cancellationToken);

            return Task.FromResult(fitness);
        }
    }

    private static int TournamentSelect(
        double[] fit,
        int k,
        Random rng)
    {
        int best = rng.Next(fit.Length);

        for (int i = 1; i < k; i++)
        {
            int c = rng.Next(fit.Length);

            if (fit[c] < fit[best])
                best = c;
        }

        return best;
    }

    private static double Average(double[] a)
    {
        double s = 0;

        for (int i = 0; i < a.Length; i++)
        {
            s += a[i];
        }

        return s / a.Length;
    }

    private static void ValidateOptions(GaOptions o)
    {
        if (o.PopulationSize < 4)
            throw new ArgumentException("PopulationSize < 4.");

        if (o.MaxGenerations < 1)
            throw new ArgumentException("MaxGenerations < 1.");

        if (o.TournamentSize < 2)
            throw new ArgumentException("TournamentSize < 2.");

        if (o.CrossoverProbability < 0 ||
            o.CrossoverProbability > 1)
        {
            throw new ArgumentException("P_cross вне [0,1].");
        }

        if (o.MutationProbability < 0 ||
            o.MutationProbability > 1)
        {
            throw new ArgumentException("P_mut вне [0,1].");
        }

        if (o.EliteCount < 0 ||
            o.EliteCount >= o.PopulationSize)
        {
            throw new ArgumentException(
                "EliteCount вне [0, PopulationSize).");
        }
    }
}
