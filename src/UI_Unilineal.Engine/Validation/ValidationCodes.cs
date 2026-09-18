namespace UI_Unilineal.Engine.Validation;

public static class ValidationCodes
{
    public const string DuplicateEntityUid = "DUPLICATE_ENTITY_UID";
    public const string MissingReference = "MISSING_REFERENCE";
    public const string InvalidSupplyOrigin = "INVALID_SUPPLY_ORIGIN";
    public const string BoardSupplyRequiresCircuit = "BOARD_SUPPLY_REQUIRES_CIRCUIT";
    public const string SourceSupplyCannotUseBoardCircuit = "SOURCE_SUPPLY_CANNOT_USE_BOARD_CIRCUIT";
    public const string SupplyCircuitOwnerMismatch = "SUPPLY_CIRCUIT_OWNER_MISMATCH";
    public const string SelfSupply = "SELF_SUPPLY";
    public const string SupplyCycle = "SUPPLY_CYCLE";
    public const string MultipleMainBuses = "MULTIPLE_MAIN_BUSES";
    public const string BoardWithoutSupply = "BOARD_WITHOUT_SUPPLY";
    public const string CircuitMissingConductor = "CIRCUIT_MISSING_CONDUCTOR";
    public const string CircuitMissingProtection = "CIRCUIT_MISSING_PROTECTION";
    public const string BoardMissingVoltage = "BOARD_MISSING_VOLTAGE";
    public const string SourceIncomplete = "SOURCE_INCOMPLETE";
    public const string GroundingIncomplete = "GROUNDING_INCOMPLETE";
}
