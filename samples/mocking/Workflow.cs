// System Under Test — a workflow that sends commands via ISender.
namespace MockingExample;

public class Workflow(ISender sender)
{
    public async Task RunAsync(CancellationToken ct = default)
    {
        await sender.Send<RunTask1Command, Unit>(new RunTask1Command(), ct);
        await sender.Send<RunTask2Command, Unit>(new RunTask2Command(), ct);
    }
}
