; Unshipped analyzer changes
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
DSOFT002 | DSoftStudio.Mediator | Warning | Duplicate request handler registration
DSOFT003 | DSoftStudio.Mediator | Warning | Duplicate stream handler registration
DSOFT004 | DSoftStudio.Mediator | Warning | Mocking library detected with interceptors enabled
DSOFT005 | DSoftStudio.Mediator | Warning | Internal handler skipped in external assembly
