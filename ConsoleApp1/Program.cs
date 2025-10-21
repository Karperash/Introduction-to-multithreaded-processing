using System;
using System.Diagnostics;
using System.Threading;

class Program
{
    // === 1) Параметры экспериментов ===
    static readonly int[] Ns = new[] { 10, 100, 1000, 100000 };         // размеры вектора (из задания)
    static readonly int[] Ms = new[] { 2, 3, 4, 5, 10 };                 // число потоков (из задания)
    const int Trials = 5;                                              

    // Случайные данные для воспроизводимости
    static double[] MakeVector(int n, int seed = 42)
    {
        var rnd = new Random(seed);
        var a = new double[n];
        for (int i = 0; i < n; i++) a[i] = rnd.NextDouble() * 100.0;
        return a;
    }

    
    static double BaseOp(double x) => x * 1.789;

    // Усложнение через параметр K
    static double HardOp(double x, int K)
    {
        double acc = 0;
        for (int j = 0; j < K; j++) acc += BaseOp(x);
        return acc;
    }

    // Неравномерная нагрузка: «сколько работы» зависит от индекса i (пример из методички)
    static double NonUniformOp(double x, int i)
    {
        double acc = 0;
        for (int j = 0; j < i; j++) acc += BaseOp(x);
        return acc;
    }

    // === 3) Последовательная обработка ===
    static void ProcessSequential(double[] a, double[] b)
    {
        for (int i = 0; i < a.Length; i++)
            b[i] = BaseOp(a[i]);
    }

    static void ProcessSequentialHard(double[] a, double[] b, int K)
    {
        for (int i = 0; i < a.Length; i++)
            b[i] = HardOp(a[i], K);
    }

    static void ProcessSequentialNonUniform(double[] a, double[] b)
    {
        for (int i = 0; i < a.Length; i++)
            b[i] = NonUniformOp(a[i], i);
    }

    // === 4) Многопоточно: разделение по диапазонам [start, end) ===
    static void ProcessParallelRanges(double[] a, double[] b, int M)
    {
        int n = a.Length;
        var threads = new Thread[M];

        for (int t = 0; t < M; t++)
        {
            int tCopy = t;
            threads[t] = new Thread(_ =>
            {
                int start = (tCopy * n) / M;
                int end = ((tCopy + 1) * n) / M;
                for (int i = start; i < end; i++)
                    b[i] = BaseOp(a[i]);
            });
            threads[t].Start();
        }
        // Ждём завершения всех потоков
        foreach (var thr in threads) thr.Join();
    }

    static void ProcessParallelRangesHard(double[] a, double[] b, int M, int K)
    {
        int n = a.Length;
        var threads = new Thread[M];

        for (int t = 0; t < M; t++)
        {
            int tCopy = t;
            threads[t] = new Thread(_ =>
            {
                int start = (tCopy * n) / M;
                int end = ((tCopy + 1) * n) / M;
                for (int i = start; i < end; i++)
                    b[i] = HardOp(a[i], K);
            });
            threads[t].Start();
        }
        foreach (var thr in threads) thr.Join();
    }

    // Неравномерная нагрузка + диапазоны (специально, чтобы показать дисбаланс)
    static void ProcessParallelRangesNonUniform(double[] a, double[] b, int M)
    {
        int n = a.Length;
        var threads = new Thread[M];

        for (int t = 0; t < M; t++)
        {
            int tCopy = t;
            threads[t] = new Thread(_ =>
            {
                int start = (tCopy * n) / M;
                int end = ((tCopy + 1) * n) / M;
                for (int i = start; i < end; i++)
                    b[i] = NonUniformOp(a[i], i);
            });
            threads[t].Start();
        }
        foreach (var thr in threads) thr.Join();
    }

    // === 5) Многопоточно: «круговая» декомпозиция (indices i = t, t+M, t+2M, ...) ===
    static void ProcessParallelCyclic(double[] a, double[] b, int M)
    {
        int n = a.Length;
        var threads = new Thread[M];

        for (int t = 0; t < M; t++)
        {
            int tCopy = t;
            threads[t] = new Thread(_ =>
            {
                for (int i = tCopy; i < n; i += M)
                    b[i] = BaseOp(a[i]);
            });
            threads[t].Start();
        }
        foreach (var thr in threads) thr.Join();
    }

    static void ProcessParallelCyclicNonUniform(double[] a, double[] b, int M)
    {
        int n = a.Length;
        var threads = new Thread[M];

        for (int t = 0; t < M; t++)
        {
            int tCopy = t;
            threads[t] = new Thread(_ =>
            {
                for (int i = tCopy; i < n; i += M)
                    b[i] = NonUniformOp(a[i], i);
            });
            threads[t].Start();
        }
        foreach (var thr in threads) thr.Join();
    }

    // === 6) Вспомогательное: замер времени нескольких прогонов ===
    static double MeasureMs(Action action, int trials = Trials, bool warmup = true)
    {
        var sw = new Stopwatch();
        double sum = 0;

        int startTrial = warmup ? 1 : 0;
        for (int t = 0; t < trials; t++)
        {
            sw.Restart();
            action();
            sw.Stop();
            if (t >= startTrial) sum += sw.Elapsed.TotalMilliseconds; // пропускаем первый прогон как «разогрев»
        }
        int denom = trials - startTrial;
        return sum / Math.Max(1, denom);
    }

    static void Main()
    {
        Console.WriteLine($"CPU logical processors: {Environment.ProcessorCount}");
        // === A. Демонстрация базовых режимов на одном размере ===
        {
            int N = 100000;
            int M = Math.Min(4, Environment.ProcessorCount); // разумный дефолт
            var a = MakeVector(N);
            var b = new double[N];

            Console.WriteLine("\nДемонстрация на N=100000:");

            double tSeq = MeasureMs(() => { Array.Clear(b, 0, b.Length); ProcessSequential(a, b); });
            double tParRange = MeasureMs(() => { Array.Clear(b, 0, b.Length); ProcessParallelRanges(a, b, M); });
            double tParCyc = MeasureMs(() => { Array.Clear(b, 0, b.Length); ProcessParallelCyclic(a, b, M); });

            Console.WriteLine($"Последовательно: {tSeq:F2} ms");
            Console.WriteLine($"Параллельно (диапазоны, M={M}): {tParRange:F2} ms");
            Console.WriteLine($"Параллельно (круговая,  M={M}): {tParCyc:F2} ms");
        }

        // === B. Таблица эффективности при разных N и M (задание 3) ===
        Console.WriteLine("\n=== Таблица: N,M, T_seq_ms, T_par_range_ms, Speedup_range ===");
        Console.WriteLine("N,M,T_seq_ms,T_par_range_ms,Speedup_range");
        foreach (var N in Ns)
        {
            var a = MakeVector(N);
            var b = new double[N];

            double tSeq = MeasureMs(() => { Array.Clear(b, 0, b.Length); ProcessSequential(a, b); });

            foreach (var M in Ms)
            {
                double tPar = MeasureMs(() => { Array.Clear(b, 0, b.Length); ProcessParallelRanges(a, b, M); });
                double speedup = tSeq / tPar;
                Console.WriteLine($"{N},{M},{tSeq:F3},{tPar:F3},{speedup:F2}");
            }
        }

        // === C. Влияние «сложности» обработки K (задание 4) ===
        Console.WriteLine("\n=== Влияние K (N=100000, M в {2,4,8}): K,T_seq_ms,T_par_ms(M=2),T_par_ms(M=4),T_par_ms(M=8) ===");
        {
            int N = 100000;
            var a = MakeVector(N);
            var b = new double[N];
            int[] Ks = { 1, 2, 5, 10, 20 };

            foreach (var K in Ks)
            {
                double tSeq = MeasureMs(() => { Array.Clear(b, 0, b.Length); ProcessSequentialHard(a, b, K); });
                double tM2 = MeasureMs(() => { Array.Clear(b, 0, b.Length); ProcessParallelRangesHard(a, b, 2, K); });
                double tM4 = MeasureMs(() => { Array.Clear(b, 0, b.Length); ProcessParallelRangesHard(a, b, 4, K); });
                double tM8 = MeasureMs(() => { Array.Clear(b, 0, b.Length); ProcessParallelRangesHard(a, b, 8, K); });

                Console.WriteLine($"{K},{tSeq:F2},{tM2:F2},{tM4:F2},{tM8:F2}");
            }
        }

        // === D. Неравномерная нагрузка: диапазоны vs круговая (задания 5 и 6) ===
        Console.WriteLine("\n=== Неравномерная нагрузка (NonUniform): N=100000, сравнение диапазонов и круговой ===");
        {
            int N = 100000;
            var a = MakeVector(N);
            var b = new double[N];
            int[] MsLocal = { 2, 4, 8 };

            double tSeq = MeasureMs(() => { Array.Clear(b, 0, b.Length); ProcessSequentialNonUniform(a, b); });
            Console.WriteLine($"Последовательно: {tSeq:F2} ms");

            foreach (var M in MsLocal)
            {
                double tRange = MeasureMs(() => { Array.Clear(b, 0, b.Length); ProcessParallelRangesNonUniform(a, b, M); });
                double tCyc = MeasureMs(() => { Array.Clear(b, 0, b.Length); ProcessParallelCyclicNonUniform(a, b, M); });
                Console.WriteLine($"M={M}: Диапазоны={tRange:F2} ms, Круговая={tCyc:F2} ms (лучше меньше)");
            }
        }
    }
}
