public class JobRunner
{
    private readonly IEnumerable<IJob> _jobs;
    private static SemaphoreSlim _lock = new(1, 1);

    public JobRunner(IEnumerable<IJob> jobs)
    {
        _jobs = jobs;
    }

    public async Task RunSingle(string name)
    {
        var job = _jobs.FirstOrDefault(j => j.Name == name);
        if (job == null) throw new Exception("not found");

        await Run(job);
    }

    public async Task RunAll()
    {
        foreach (var j in _jobs)
            await Run(j);
    }

    private async Task Run(IJob job)
    {
        if (!await _lock.WaitAsync(0))
            throw new Exception("busy");

        try
        {
            await job.Run();
        }
        finally
        {
            _lock.Release();
        }
    }
}
