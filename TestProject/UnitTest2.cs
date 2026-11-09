using CoreLibrary;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestProject;
public class FitnessPerformanceTests
{
    [Fact]
    public void EvaluateAll_Parallel_IsFasterThanSequential_ForLargePopulation()
    {
        const int cityCount = 3000;
        const int populationSize = 5000;
        const double side = 100.0;

        var rng = new Random(42);
        var problem = KomProblem.FromRandomPoints(cityCount, side, rng);

        var population = new int[populationSize][];
        for (int i = 0; i < populationSize; i++)
        {
            population[i] = Operators.RandomPermutation(cityCount, rng);
        }

        var popSequential = population.Select(t => (int[])t.Clone()).ToArray();
        var popParallel = population.Select(t => (int[])t.Clone()).ToArray();

        var fitSequential = new double[populationSize];
        var fitParallel = new double[populationSize];

        var swSequential = Stopwatch.StartNew();
        Fitness.EvaluateAllSequential(
            popSequential,
            problem.Distances,
            fitSequential);
        swSequential.Stop();

        Thread.Sleep(100);

        var swParallel = Stopwatch.StartNew();
        Fitness.EvaluateAll(
            popParallel,
            problem.Distances,
            fitParallel,
            parallelThreshold: 32); // порог, при котором включается параллелизм
        swParallel.Stop();

        for (int i = 0; i < populationSize; i++)
        {
            Assert.Equal(fitSequential[i], fitParallel[i], precision: 6);
        }

        double speedup = (double)swSequential.ElapsedMilliseconds / swParallel.ElapsedMilliseconds;

        Assert.True(
            swParallel.ElapsedMilliseconds <= swSequential.ElapsedMilliseconds,
            $"Ожидалось, что параллельное выполнение будет быстрее. " +
            $"Последовательно: {swSequential.ElapsedMilliseconds} мс, " +
            $"Параллельно: {swParallel.ElapsedMilliseconds} мс, " +
            $"Ускорение: {speedup:F2}x. " +
            $"Количество ядер: {Environment.ProcessorCount}");
    }
}
