using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using UI_Unilineal.Engine.Layout;

namespace UI_Unilineal.Engine.Interaction.Electrical;

public static class ElectricalCommandProposalFactory
{
    public static ElectricalCommandProposal Create(
        IElectricalCommand command,
        string expectedRevision,
        ElectricalAnchorEndpoint? source = null,
        ElectricalAnchorEndpoint? target = null)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (string.IsNullOrWhiteSpace(expectedRevision))
        {
            throw new ArgumentException(
                "Expected input revision is required.",
                nameof(expectedRevision));
        }

        bool hasSource =
            source is not null;
        bool hasTarget =
            target is not null;

        if (hasSource != hasTarget)
        {
            throw new ArgumentException(
                "Source and target anchors must be supplied together.");
        }

        if (command is CreateSupplyConnectionCommand &&
            (!hasSource || !hasTarget))
        {
            throw new ArgumentException(
                "Creating a supply connection requires semantic source and target anchors.");
        }

        if (source is not null &&
            target is not null &&
            !AnchorCompatibility.CanConnect(
                source.Role,
                target.Role))
        {
            throw new ArgumentException(
                $"Anchor roles '{source.Role}' and '{target.Role}' are not electrically compatible.");
        }

        CommandImpact impact =
            command.Impact;
        bool requiresConfirmation =
            impact is
                CommandImpact.CascadingElectrical or
                CommandImpact.Destructive;

        string fingerprint =
            ComputeFingerprint(
                command,
                expectedRevision,
                source,
                target);

        return new ElectricalCommandProposal(
            command,
            expectedRevision,
            source,
            target,
            impact,
            requiresConfirmation,
            fingerprint);
    }

    private static string ComputeFingerprint(
        IElectricalCommand command,
        string expectedRevision,
        ElectricalAnchorEndpoint? source,
        ElectricalAnchorEndpoint? target)
    {
        string commandJson =
            JsonSerializer.Serialize(
                command,
                command.GetType());

        string canonical =
            string.Join(
                "\n",
                command.GetType().FullName ?? command.GetType().Name,
                commandJson,
                expectedRevision,
                EndpointKey(source),
                EndpointKey(target));

        byte[] hash =
            SHA256.HashData(
                Encoding.UTF8.GetBytes(
                    canonical));

        return Convert.ToHexString(hash);
    }

    private static string EndpointKey(
        ElectricalAnchorEndpoint? endpoint) =>
        endpoint is null
            ? string.Empty
            : string.Join(
                "|",
                endpoint.Entity.Uid.Value,
                endpoint.Entity.Kind,
                endpoint.AnchorId,
                endpoint.Role);
}
