using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace LogiTracker.Infrastructure.Persistence.Configurations;

internal sealed class UtcDateTimeOffsetConverter : ValueConverter<DateTimeOffset, DateTimeOffset>
{
    public UtcDateTimeOffsetConverter()
        : base(value => value.ToUniversalTime(), value => value)
    {
    }
}
