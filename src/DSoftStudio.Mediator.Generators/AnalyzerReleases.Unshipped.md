; Unshipped analyzer changes
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
DSOFT008 | DSoftStudio.Mediator.Usage | Warning | AddMediator() registers core services but no handlers
DSOFT009 | DSoftStudio.Mediator | Warning | Handler skipped because generated code cannot name it
DSOFT010 | DSoftStudio.Mediator.Usage | Warning | Pipeline component registered after the mediator pipeline scan
