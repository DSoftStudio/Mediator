// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace DSoft.Sample.MinimalApi.Application.Models;

public record UserDto(int Id, string Name, string Email);

public record OrderId(int Value);

public record OrderItem(string ProductId, int Quantity, decimal UnitPrice);

public record OrderSummaryDto(int Id, string CustomerId, string Status, decimal Total);

public record RefundResult(bool Approved, string ReferenceId);

public record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize);
