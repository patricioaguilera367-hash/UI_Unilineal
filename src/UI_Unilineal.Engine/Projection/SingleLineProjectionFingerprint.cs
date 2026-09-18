using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace UI_Unilineal.Engine.Projection;

public static class SingleLineProjectionFingerprint
{
    private static readonly JsonSerializerOptions SerializerOptions =
        new()
        {
            WriteIndented = false
        };

    public static string Compute(SingleLineProjection projection)
    {
        ArgumentNullException.ThrowIfNull(projection);

        string json = JsonSerializer.Serialize(
            projection,
            SerializerOptions);

        return Convert.ToHexString(
            SHA256.HashData(
                Encoding.UTF8.GetBytes(json)));
    }
}
