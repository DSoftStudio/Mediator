; Shipped analyzer releases
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

## Release 1.0.0

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
DSOFT001 | DSoftStudio.Mediator | Warning | No handler found for request type

## Release 1.1.8-rc.1

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
DSOFT002 | DSoftStudio.Mediator | Warning | Duplicate request handler registration
DSOFT003 | DSoftStudio.Mediator | Warning | Duplicate stream handler registration
DSOFT004 | DSoftStudio.Mediator | Warning | Mocking library detected with interceptors enabled
DSOFT005 | DSoftStudio.Mediator | Warning | Internal handler skipped in external assembly
DSOFT006 | DSoftStudio.Mediator.Usage | Info | Consider using ICommand<T> or IQuery<T> instead of IRequest<T>
