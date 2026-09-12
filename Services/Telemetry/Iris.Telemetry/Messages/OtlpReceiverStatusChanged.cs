namespace Iris.Telemetry.Messages;

public sealed record OtlpReceiverStatusChanged(OtlpReceiverStatus Status, string? Endpoint, string? Error);
