namespace MOBA.Core.Definitions
{
    public readonly struct PassiveLoadoutValidationResult
    {
        public bool IsValid { get; }
        public string Message { get; }
        public PassiveDefinition OffendingPassive { get; }
        public PassiveDefinition ConflictingPassive { get; }

        public PassiveLoadoutValidationResult(
            bool isValid,
            string message,
            PassiveDefinition offendingPassive,
            PassiveDefinition conflictingPassive)
        {
            IsValid = isValid;
            Message = message;
            OffendingPassive = offendingPassive;
            ConflictingPassive = conflictingPassive;
        }

        public static PassiveLoadoutValidationResult Valid()
        {
            return new PassiveLoadoutValidationResult(true, string.Empty, null, null);
        }

        public static PassiveLoadoutValidationResult Invalid(
            string message,
            PassiveDefinition offendingPassive,
            PassiveDefinition conflictingPassive = null)
        {
            return new PassiveLoadoutValidationResult(false, message, offendingPassive, conflictingPassive);
        }
    }
}