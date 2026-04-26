using Microsoft.Extensions.Hosting;

public class DailyWorker : BackgroundService
{
    private readonly JobRunner _runner;

    public DailyWorker(JobRunner runner)
    {
        _runner = runner;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var next = DateTime.Today.AddDays(1).AddHours(1);
            var delay = next - DateTime.Now;

            if (delay < TimeSpan.Zero)
                delay = TimeSpan.FromHours(24);

            await Task.Delay(delay, stoppingToken);

            await _runner.RunAll();
        }
    }
}
