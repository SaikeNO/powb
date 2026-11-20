namespace Minucje.Core.Models;

// fałszywa minucja razem z przyczyną odrzucenia
public sealed record RejectedMinutia(Minutia Minutia, RejectionReason Reason);
