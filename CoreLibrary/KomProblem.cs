using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoreLibrary
{
    public class KomProblem
    {
        public int CityCount { get; }
        public double[,] Distances { get; }
        public IReadOnlyList<Points> Coordinates { get; }

        private KomProblem(int n, double[,] d, IReadOnlyList<Points> coords)
        {
            CityCount = n;
            Distances = d;
            Coordinates = coords;
        }

        public static KomProblem FromRandomPoints(int n, double side, Random rng)
        {
            if (n < 2) throw new ArgumentOutOfRangeException(nameof(n), "Нужно хотя бы 2 города.");
            if (side <= 0) throw new ArgumentOutOfRangeException(nameof(side));

            var pts = new Points[n];
            for (int i = 0; i < n; i++)
                pts[i] = new Points(rng.NextDouble() * side, rng.NextDouble() * side);

            var d = new double[n, n];
            for (int i = 0; i < n; i++)
                for (int j = i + 1; j < n; j++)
                {
                    double dx = pts[i].X - pts[j].X;
                    double dy = pts[i].Y - pts[j].Y;
                    double dist = Math.Sqrt(dx * dx + dy * dy);
                    d[i, j] = dist;
                    d[j, i] = dist;
                }
            return new KomProblem(n, d, pts);
        }
    }
}
