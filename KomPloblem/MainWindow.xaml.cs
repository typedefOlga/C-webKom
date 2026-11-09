
using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using KomPloblemApp.Services;
using CoreLibrary;

using CorePoint = CoreLibrary.Points;
using WpfPoint = System.Windows.Point;

namespace KomProblemApp;

public partial class MainWindow : Window
{
    private const double TaskSide = 100.0;
    private const int MaxHistory = 50;

    private KomProblem? _problem;
    private CancellationTokenSource? _cts;
    private bool _stopRequested;

    private int[] _bestTour = Array.Empty<int>();

    private readonly List<int> _generationHistory = new();
    private readonly List<double> _bestHistory = new();
    private readonly List<double> _avgHistory = new();

    private double _chartMin;
    private double _chartMax;

    public MainWindow()
    {
        InitializeComponent();
    }

    private void OnGenerateClick(
        object sender,
        RoutedEventArgs e)
    {
        try
        {
            int n = ParseInt(TxtN.Text, "N");
            int seed = ParseInt(TxtSeed.Text, "Seed");

            var rng = new Random(seed);

            _problem = KomProblem.FromRandomPoints(
                n,
                TaskSide,
                rng);

            _stopRequested = false;
            _bestTour = Array.Empty<int>();

            _generationHistory.Clear();
            _bestHistory.Clear();
            _avgHistory.Clear();

            _chartMin = 0;
            _chartMax = 0;

            TxtRoute.Clear();

            RedrawCities();
            RedrawTour();
            RedrawChart();

            BtnStart.IsEnabled = true;
            BtnStop.IsEnabled = false;
            BtnGenerate.IsEnabled = true;

            TxtStatus.Text =
                $"Сгенерировано {n} городов (seed={seed}).";
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                ex.Message,
                "Ошибка",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private async void OnStartClick(
        object sender,
        RoutedEventArgs e)
    {
        if (_problem is null)
        {
            MessageBox.Show(
                this,
                "Сначала сгенерируйте задачу.");

            return;
        }

        if (_stopRequested)
        {
            TxtStatus.Text =
                "Запуск запрещён. " +
                "Сначала сгенерируйте новую задачу.";

            return;
        }

        GaOptions opt;
        List<string> serviceUrls;

        try
        {
            opt = new GaOptions(
                PopulationSize:
                    ParseInt(TxtPop.Text, "Популяция"),

                MaxGenerations:
                    ParseInt(TxtGen.Text, "Поколений"),

                TournamentSize:
                    ParseInt(TxtTournament.Text, "Размер турнира"),

                CrossoverProbability:
                    ParseDouble(TxtPC.Text, "P кроссовера"),

                MutationProbability:
                    ParseDouble(TxtPM.Text, "P мутации"),

                EliteCount:
                    ParseInt(TxtElite.Text, "Элитных особей"),

                Seed:
                    ParseInt(TxtSeed.Text, "Seed"));

            serviceUrls = ParseServiceUrls(TxtServiceUrls.Text);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                ex.Message,
                "Ошибка параметров",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            return;
        }

        BtnStart.IsEnabled = false;
        BtnStop.IsEnabled = true;
        BtnGenerate.IsEnabled = false;

        _stopRequested = false;

        _cts = new CancellationTokenSource();

        CancellationToken token = _cts.Token;

        var progress = new Progress<GenerationReport>(
            r =>
            {
                if (token.IsCancellationRequested)
                    return;

                if (_stopRequested)
                    return;

                OnReport(r);
            });

        var ga = new GeneticAlgorithm();

        try
        {
            // Создаём распределённый вычислитель
            // на основе всех введённых адресов.
            using var evaluator = new DistributedFitnessEvaluator(
                serviceUrls);

            TxtStatus.Text =
                $"Используется вычислительных сервисов: {serviceUrls.Count}";

            // Запускаем генетический алгоритм.
            // Вычисление Fitness распределяется между сервисами.
            var best = await ga.RunAsync(
                _problem,
                opt,
                progress,
                token,
                evaluator: evaluator);

            _stopRequested = false;

            _bestTour = (int[])best.Clone();

            RedrawTour();
            UpdateRouteText();

            double len = Fitness.TourLength(
                best,
                _problem.Distances);

            TxtStatus.Text =
                $"Готово. Лучшая длина = {len:F2}";
        }
        catch (OperationCanceledException)
        {
            TxtStatus.Text =
                "Остановлено пользователем. " +
                $"Лучшая длина = {GetCurrentBestLength():F2}";
        }
        catch (Exception ex)
        {
            string errorMessage = ex.GetBaseException().Message;

            MessageBox.Show(
                this,
                $"Не удалось выполнить вычисления.\n\n{errorMessage}",
                "Ошибка вычислительного сервиса",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            TxtStatus.Text = "Ошибка выполнения.";
        }
        finally
        {
            _cts?.Dispose();
            _cts = null;

            BtnStop.IsEnabled = false;
            BtnGenerate.IsEnabled = true;

            if (!_stopRequested)
                BtnStart.IsEnabled = true;
            else
                BtnStart.IsEnabled = false;
        }
    }

    /// <summary>
    /// Считывает адреса вычислительных сервисов
    /// из текстового поля.
    /// Каждый адрес должен находиться на отдельной строке.
    /// </summary>
    private static List<string> ParseServiceUrls(string text)
    {
        var urls = new List<string>();

        string[] lines = text.Split(
            new[] { '\r', '\n' },
            StringSplitOptions.RemoveEmptyEntries |
            StringSplitOptions.TrimEntries);

        foreach (string line in lines)
        {
            if (!Uri.TryCreate(
                    line,
                    UriKind.Absolute,
                    out Uri? uri))
            {
                throw new FormatException(
                    $"Некорректный адрес вычислительного сервиса: {line}");
            }

            if (uri.Scheme != Uri.UriSchemeHttp &&
                uri.Scheme != Uri.UriSchemeHttps)
            {
                throw new FormatException(
                    $"Адрес должен начинаться с http:// или https://: {line}");
            }

            // Добавляем завершающий слеш,
            // чтобы относительные адреса API формировались правильно.
            string normalizedUrl = uri.AbsoluteUri;

            if (!normalizedUrl.EndsWith('/'))
                normalizedUrl += "/";

            // Не добавляем один и тот же адрес повторно.
            if (!urls.Contains(
                    normalizedUrl,
                    StringComparer.OrdinalIgnoreCase))
            {
                urls.Add(normalizedUrl);
            }
        }

        if (urls.Count == 0)
        {
            throw new FormatException(
                "Укажите хотя бы один адрес вычислительного сервиса.");
        }

        return urls;
    }

    private void OnStopClick(
        object sender,
        RoutedEventArgs e)
    {
        if (_cts is null)
            return;

        _stopRequested = true;

        BtnStart.IsEnabled = false;
        BtnStop.IsEnabled = false;
        BtnGenerate.IsEnabled = false;

        TxtStatus.Text = "Остановка...";

        _cts.Cancel();
    }

    private async void OnReport(GenerationReport r)
    {
        if (_stopRequested)
            return;

        _bestTour = (int[])r.BestTour.Clone();

        _generationHistory.Add(r.Generation);
        _bestHistory.Add(r.BestLength);
        _avgHistory.Add(r.AverageLength);

        if (_generationHistory.Count > MaxHistory)
        {
            _generationHistory.RemoveAt(0);
            _bestHistory.RemoveAt(0);
            _avgHistory.RemoveAt(0);
        }

        _chartMin = Math.Min(
            _bestHistory.Min(),
            _avgHistory.Min());

        _chartMax = Math.Max(
            _bestHistory.Max(),
            _avgHistory.Max());

        RedrawTour();
        RedrawChart();
        UpdateRouteText();

        TxtStatus.Text =
            $"Поколение {r.Generation}: " +
            $"best = {r.BestLength:F2}, " +
            $"avg = {r.AverageLength:F2}";

        await Task.Delay(60);
    }

    private double GetCurrentBestLength()
    {
        if (_problem is null || _bestTour.Length == 0)
            return 0;

        return Fitness.TourLength(
            _bestTour,
            _problem.Distances);
    }

    private void RedrawCities()
    {
        TourCanvas.Children.Clear();

        if (_problem is null)
            return;

        double w = TourCanvas.ActualWidth;
        double h = TourCanvas.ActualHeight;

        if (w < 10 || h < 10)
            return;

        for (int i = 0; i < _problem.Coordinates.Count; i++)
        {
            CorePoint pt = _problem.Coordinates[i];

            WpfPoint p = MapToCanvas(pt, w, h);

            var dot = new Ellipse
            {
                Width = 8,
                Height = 8,
                Fill = Brushes.OrangeRed
            };

            Canvas.SetLeft(dot, p.X - 4);
            Canvas.SetTop(dot, p.Y - 4);

            TourCanvas.Children.Add(dot);

            var label = new TextBlock
            {
                Text = i.ToString(),
                Foreground = Brushes.Black,
                FontWeight = FontWeights.Bold,
                FontSize = 13,
                Background = Brushes.White
            };

            Canvas.SetLeft(label, p.X + 6);
            Canvas.SetTop(label, p.Y - 10);

            TourCanvas.Children.Add(label);
        }
    }

    private void RedrawTour()
    {
        var old = TourCanvas.Children
            .OfType<Polyline>()
            .ToList();

        foreach (var p in old)
            TourCanvas.Children.Remove(p);

        if (_problem is null || _bestTour.Length == 0)
            return;

        double w = TourCanvas.ActualWidth;
        double h = TourCanvas.ActualHeight;

        if (w < 10 || h < 10)
            return;

        var poly = new Polyline
        {
            Stroke = Brushes.SteelBlue,
            StrokeThickness = 1.5
        };

        foreach (int city in _bestTour)
        {
            poly.Points.Add(
                MapToCanvas(
                    _problem.Coordinates[city],
                    w,
                    h));
        }

        poly.Points.Add(
            MapToCanvas(
                _problem.Coordinates[_bestTour[0]],
                w,
                h));

        TourCanvas.Children.Insert(0, poly);
    }

    private void UpdateRouteText()
    {
        if (_problem is null || _bestTour.Length == 0)
        {
            TxtRoute.Clear();
            return;
        }

        var sb = new StringBuilder();

        sb.AppendLine("ПОРЯДОК ОБХОДА");
        sb.AppendLine();

        for (int i = 0; i < _bestTour.Length; i++)
        {
            if (i > 0)
                sb.Append(" → ");

            sb.Append(_bestTour[i]);
        }

        sb.Append(" → ");
        sb.Append(_bestTour[0]);

        sb.AppendLine();
        sb.AppendLine();

        sb.AppendLine("РАССТОЯНИЯ");
        sb.AppendLine();

        double total = 0;

        for (int i = 0; i < _bestTour.Length; i++)
        {
            int cityFrom = _bestTour[i];

            int cityTo = _bestTour[
                (i + 1) % _bestTour.Length];

            double distance = _problem.Distances[
                cityFrom,
                cityTo];

            total += distance;

            sb.AppendLine(
                $"{cityFrom,2} → {cityTo,2} : " +
                $"{distance,8:F2}");
        }

        sb.AppendLine();
        sb.AppendLine($"Общая длина маршрута: {total:F2}");

        TxtRoute.Text = sb.ToString();
    }

    private void RedrawChart()
    {
        ChartCanvas.Children.Clear();

        if (_bestHistory.Count < 2)
            return;

        double w = ChartCanvas.ActualWidth;
        double h = ChartCanvas.ActualHeight;

        if (w < 10 || h < 10)
            return;

        double range = Math.Max(
            _chartMax - _chartMin,
            1e-6);

        ChartCanvas.Children.Add(
            MakeLine(
                _bestHistory,
                w,
                h,
                range,
                Brushes.SeaGreen));

        ChartCanvas.Children.Add(
            MakeLine(
                _avgHistory,
                w,
                h,
                range,
                Brushes.IndianRed));
    }

    private Polyline MakeLine(
        List<double> values,
        double w,
        double h,
        double range,
        Brush color)
    {
        var poly = new Polyline
        {
            Stroke = color,
            StrokeThickness = 1.5
        };

        int count = values.Count;

        for (int i = 0; i < count; i++)
        {
            double x = (double)i /
                Math.Max(1, count - 1) * w;

            double y = h -
                (values[i] - _chartMin) / range * h;

            poly.Points.Add(new WpfPoint(x, y));
        }

        return poly;
    }

    private WpfPoint MapToCanvas(
        CorePoint pt,
        double w,
        double h)
    {
        const double margin = 12;

        double x = margin +
            pt.X / TaskSide * (w - 2 * margin);

        double y = margin +
            (1 - pt.Y / TaskSide) * (h - 2 * margin);

        return new WpfPoint(x, y);
    }

    private void OnTourCanvasSizeChanged(
        object sender,
        SizeChangedEventArgs e)
    {
        RedrawCities();
        RedrawTour();
    }

    private void OnChartCanvasSizeChanged(
        object sender,
        SizeChangedEventArgs e)
    {
        RedrawChart();
    }

    private static int ParseInt(string s, string name)
    {
        if (!int.TryParse(
                s,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out int v))
        {
            throw new FormatException(
                $"Не могу разобрать {name}: «{s}»");
        }

        return v;
    }

    private static double ParseDouble(string s, string name)
    {
        if (!double.TryParse(
                s,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out double v))
        {
            throw new FormatException(
                $"Не могу разобрать {name}: «{s}»");
        }

        return v;
    }
}
