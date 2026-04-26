public interface IJob
{
    string Name { get; }
    Task Run();
}
