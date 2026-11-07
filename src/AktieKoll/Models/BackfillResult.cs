namespace AktieKoll.Models;

/// <param name="FromFi">Trades in FI's register for the period (after filters).</param>
/// <param name="Matched">Of those, how many were found in the database.</param>
/// <param name="Updated">Of the matched, how many rows were changed.</param>
public record BackfillResult(int FromFi, int Matched, int Updated);
