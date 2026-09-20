using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using UI_Unilineal.Domain.Semantics;

namespace UI_Unilineal.Engine.Projection;

public static class SingleLineInputFingerprint
{
    public static string Compute(SingleLineInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var normalized = new
        {
            input.Project,
            Sources = input.Sources
                .OrderBy(x => x.Uid.Value, StringComparer.Ordinal)
                .ToArray(),
            Boards = input.Boards
                .OrderBy(x => x.Uid.Value, StringComparer.Ordinal)
                .ToArray(),
            Buses = input.Buses
                .OrderBy(x => x.Uid.Value, StringComparer.Ordinal)
                .ToArray(),
            Circuits = input.Circuits
                .OrderBy(x => x.Uid.Value, StringComparer.Ordinal)
                .ToArray(),
            SupplyConnections = input.SupplyConnections
                .OrderBy(x => x.Uid.Value, StringComparer.Ordinal)
                .ToArray(),
            Protections = input.Protections
                .OrderBy(x => x.Uid.Value, StringComparer.Ordinal)
                .ToArray(),
            Grounding = input.Grounding
                .OrderBy(x => x.Uid.Value, StringComparer.Ordinal)
                .ToArray(),
            ServiceEntrances = input.ServiceEntrances
                .OrderBy(x => x.Uid.Value, StringComparer.Ordinal)
                .ToArray(),
            Results = input.Results
                .OrderBy(x => x.Entity.Kind)
                .ThenBy(x => x.Entity.Uid.Value, StringComparer.Ordinal)
                .ThenBy(CanonicalResultSortKey, StringComparer.Ordinal)
                .ToArray(),
            input.Metadata
        };

        string json = JsonSerializer.Serialize(normalized);
        byte[] bytes = Encoding.UTF8.GetBytes(json);
        byte[] hash = SHA256.HashData(bytes);

        return Convert.ToHexString(hash);
    }

    private static string CanonicalResultSortKey(ElectricalResultInput result) =>
        JsonSerializer.Serialize(result);
}

