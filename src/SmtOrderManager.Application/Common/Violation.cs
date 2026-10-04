namespace SmtOrderManager.Application.Common;

/// <summary>
/// A business rule violated by one item of an operation.
/// </summary>
/// <param name="Target">The affected item, readable for users, for example <c>Item 2</c> or <c>Component 'RES-10K-0402'</c>.</param>
/// <param name="Message">What is wrong with the item.</param>
public sealed record Violation(string Target, string Message);
