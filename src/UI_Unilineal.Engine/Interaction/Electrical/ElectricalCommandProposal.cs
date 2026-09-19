namespace UI_Unilineal.Engine.Interaction.Electrical;

public sealed record ElectricalCommandProposal(
    IElectricalCommand Command,
    string ExpectedRevision,
    ElectricalAnchorEndpoint? Source,
    ElectricalAnchorEndpoint? Target,
    CommandImpact Impact,
    bool RequiresConfirmation,
    string ProposalFingerprint);
