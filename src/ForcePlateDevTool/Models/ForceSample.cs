namespace ForcePlateDevTool.Models;

public sealed record ForceSample(
    DateTime Timestamp,
    byte SensorAddress,
    float Fx,
    float Fy,
    float Fz);