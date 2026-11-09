using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoreLibrary;

public sealed record GenerationReport(
    int Generation,
    double BestLength,
    double AverageLength,
    int[] BestTour,
    bool IsFinal
);