// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoft.Sample.MinimalApi.Application.Models;
using DSoftStudio.Mediator.Abstractions;

namespace DSoft.Sample.MinimalApi.Application.Commands;

// Recipe 5: Command with authorization → POST with RequireAuthorization
public record RefundOrderCommand(int OrderId, decimal Amount, string Reason) : ICommand<RefundResult>;

public class RefundOrderCommandHandler : ICommandHandler<RefundOrderCommand, RefundResult>
{
    public ValueTask<RefundResult> Handle(RefundOrderCommand request, CancellationToken cancellationToken)
    {
        // Simulate refund processing
        var approved = request.Amount <= 500m;
        var referenceId = $"REF-{DateTime.UtcNow:yyyyMMdd}-{request.OrderId}";
        return new ValueTask<RefundResult>(new RefundResult(approved, referenceId));
    }
}
