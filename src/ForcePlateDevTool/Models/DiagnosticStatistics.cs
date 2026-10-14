namespace ForcePlateDevTool.Models;

public sealed record DiagnosticStatistics(
    long ReceivedDatagrams,
    long ReceivedBytes,
    long DecodedFrames,
    long InvalidDataEvents,
    long DecodeFailures,
    long DecodeExceptions,
    DateTime? LastReceiveTime,
    DateTime? LastDecodedTime);