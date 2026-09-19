namespace UI_Unilineal.Engine.Interaction.Electrical;

public sealed record ElectricalCommandRequest
{
    public ElectricalCommandRequest(
        IElectricalCommand command,
        string expectedRevision,
        bool confirmationGranted = false)
    {
        Command = command ??
            throw new ArgumentNullException(
                nameof(command));

        if (string.IsNullOrWhiteSpace(expectedRevision))
        {
            throw new ArgumentException(
                "Expected input revision is required.",
                nameof(expectedRevision));
        }

        ExpectedRevision = expectedRevision;
        ConfirmationGranted = confirmationGranted;
    }

    public IElectricalCommand Command { get; }

    public string ExpectedRevision { get; }

    public bool ConfirmationGranted { get; }
}
