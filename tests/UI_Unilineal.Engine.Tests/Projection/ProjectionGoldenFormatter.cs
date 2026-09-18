using System.Text;
using UI_Unilineal.Domain.Semantics;
using UI_Unilineal.Engine.Projection;

namespace UI_Unilineal.Engine.Tests.Projection;

internal static class ProjectionGoldenFormatter
{
    public static string Format(SingleLineProjection projection)
    {
        ArgumentNullException.ThrowIfNull(projection);

        var builder = new StringBuilder();

        Append(builder, "PROJECT", projection.ProjectUid);

        foreach (EntityReference root in projection.Summary.Roots)
        {
            Append(builder, "ROOT", root.Kind, root.Uid);
        }

        foreach (SummaryNode node in projection.Summary.Nodes)
        {
            Append(
                builder,
                "SUMMARY_NODE",
                node.Entity.Kind,
                node.Entity.Uid,
                node.Code,
                node.Status,
                node.IssueCount);
        }

        foreach (SummaryConnection connection in projection.Summary.Connections)
        {
            Append(
                builder,
                "SUMMARY_CONNECTION",
                connection.SupplyConnectionUid,
                connection.Origin.Kind,
                connection.Origin.Uid,
                connection.ThroughCircuitUid?.ToString() ?? "-",
                connection.Destination.Uid,
                connection.Role,
                connection.IsNormallyActive,
                connection.Status);
        }

        foreach (BoardDetailProjection detail in projection.BoardDetails)
        {
            ProjectionStatus detailStatus = ProjectionStatusResolver.Resolve(
                detail.Board.DataState,
                null,
                detail.Issues);

            Append(
                builder,
                "DETAIL",
                detail.Board.Uid,
                detail.Board.Code,
                detailStatus);

            Append(
                builder,
                "BUS",
                detail.MainBus.Bus.Uid,
                detail.MainBus.Bus.Code,
                detail.MainBus.Status,
                detail.MainBus.MainProtections.Count);

            foreach (BranchProjection branch in detail.Branches)
            {
                Append(
                    builder,
                    "BRANCH",
                    branch.Circuit.Uid,
                    branch.Circuit.Code,
                    branch.Kind,
                    branch.Destination.Entity?.Uid.ToString() ?? "-",
                    branch.Status,
                    branch.ProtectionChain.Count);
            }
        }

        return builder.ToString();
    }

    private static void Append(StringBuilder builder, params object?[] values)
    {
        builder.AppendJoin('|', values.Select(value => value?.ToString() ?? "-"));
        builder.Append('\n');
    }
}
