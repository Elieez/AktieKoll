namespace AktieKoll.Models;

public record ShortIngestionResult(int NewPositions, int Snapshots, int DroppedOut, int UnresolvedIssuers);
