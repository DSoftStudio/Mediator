// Commands used in the example
namespace MockingExample;

public record RunTask1Command() : ICommand<Unit>;
public record RunTask2Command() : ICommand<Unit>;
public record RunTask3Command() : ICommand<Unit>;

// ── Messages used by MockSetupTests ──────────────────────────────

public record Ping : IRequest<int>;
public record PingVoid : IRequest<Unit>;
public record SlowPing : IRequest<int>;
public record PingNotification : INotification;
public record PingStream : IStreamRequest<int>;
