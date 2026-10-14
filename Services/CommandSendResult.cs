namespace ForcePlateDevTool.Services;

public sealed record CommandSendResult(
    bool Success,
    string Message,
    byte[] Command);