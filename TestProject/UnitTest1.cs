using CoreLibrary;
using Xunit;

namespace TestProject;

public class FitnessTests
{
    private static readonly double[,] Distances =
    {
        {  0, 10, 15, 20 },
        { 10,  0, 35, 25 },
        { 15, 35,  0, 30 },
        { 20, 25, 30,  0 }
    };

    [Theory]
    [InlineData(new[] { 0, 1, 2, 3 }, 10 + 35 + 30 + 20)] // 0→1→2→3→0 = 95
    [InlineData(new[] { 0, 2, 1, 3 }, 15 + 35 + 25 + 20)] // 0→2→1→3→0 = 95
    [InlineData(new[] { 3, 2, 1, 0 }, 30 + 35 + 10 + 20)] // 3→2→1→0→3 = 95
    [InlineData(new[] { 0, 1 }, 10 + 10)]                  // 0→1→0 = 20
    public void TourLength_ReturnsCorrectLength(int[] tour, double expected)
    {
        double actual = Fitness.TourLength(tour, Distances);

        Assert.Equal(expected, actual, precision: 6);
    }

    [Fact]
    public void TourLength_EmptyTour_ReturnsZero()
    {

        int[] tour = Array.Empty<int>();

        double actual = Fitness.TourLength(tour, Distances);

        Assert.Equal(0, actual);
    }

    [Fact]
    public void TourLength_SingleCity_ReturnsZero()
    {
        int[] tour = { 0 };

        double actual = Fitness.TourLength(tour, Distances);

        Assert.Equal(0, actual);
    }

    [Fact]
    public void ArgMin_ReturnsCorrectIndexAndValue()
    {
        double[] values = { 5.5, 3.2, 8.1, 1.0, 4.4 };

        var (index, value) = Fitness.ArgMin(values);

        Assert.Equal(3, index);
        Assert.Equal(1.0, value);
    }

    [Fact]
    public void ArgMin_EmptyArray_Throws()
    {
        Assert.Throws<ArgumentException>(
            () => Fitness.ArgMin(Array.Empty<double>()));
    }
}