using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoreLibrary
{
    public static class Operators
    {
        // Случайная перестановка
        public static int[] RandomPermutation(int n, Random rng)
        {
            var a = new int[n];
            for (int i = 0; i < n; i++)
            {
                a[i] = i;
            }
            for (int i = n - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (a[i], a[j]) = (a[j], a[i]);
            }
            return a;
        }

        // Мутация обменом
        public static (int i, int j) SwapMutate(int[] tour, Random rng)
        {
            int n = tour.Length;
            if (n < 2) return (0, 0);
            int i = rng.Next(n);
            int j = rng.Next(n - 1);
            if (j >= i) j++;
            (tour[i], tour[j]) = (tour[j], tour[i]);
            return (i, j);
        }

        // Order Crossover - возвращает ребёнка-перестановку
        public static int[] OrderCrossover(ReadOnlySpan<int> p1, ReadOnlySpan<int> p2, Random rng)
        {
            int n = p1.Length;
            if (p2.Length != n) throw new ArgumentException("Родители разной длины.");
            if (n < 2) return p1.ToArray();

            var child = new int[n];
            Array.Fill(child, -1);

            int a = rng.Next(n), b = rng.Next(n);
            if (a > b) (a, b) = (b, a);

            var used = new bool[n];
            for (int i = a; i <= b; i++) 
            { 
                child[i] = p1[i]; used[p1[i]] = true; 
            }

            int idx = (b + 1) % n;
            for (int k = 0; k < n; k++)
            {
                int gene = p2[(b + 1 + k) % n];
                if (!used[gene])
                {
                    child[idx] = gene;
                    used[gene] = true;
                    idx = (idx + 1) % n;
                }
            }
            return child;
        }
    }
}
