using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace NidusVision.Data;

/// <summary>
/// SQLite cannot order by or compare DateTimeOffset columns, so timestamps are stored as UTC ticks.
/// </summary>
public sealed class UtcTicksConverter() : ValueConverter<DateTimeOffset, long>(
    value => value.UtcTicks,
    ticks => new DateTimeOffset(ticks, TimeSpan.Zero));
