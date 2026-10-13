using System.Runtime.CompilerServices;
using KW.LoggerService;

namespace KW.Commons.Tools.Utilities;

/// <summary>
/// 兼容壳：请改用 <see cref="AppLog"/>。
/// </summary>
public class Log
{
    public static void LogDebug(
        string msg,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        AppLog.Debug(msg, memberName: memberName, filePath: filePath, lineNumber: lineNumber);
    }

    public static void LogDebug<T>(
        string msg,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        AppLog.Debug<T>(msg, memberName: memberName, filePath: filePath, lineNumber: lineNumber);
    }

    public static void LogInfo(
        string msg,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        AppLog.Info(msg, memberName: memberName, filePath: filePath, lineNumber: lineNumber);
    }

    public static void LogInfo<T>(
        string msg,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        AppLog.Info<T>(msg, memberName: memberName, filePath: filePath, lineNumber: lineNumber);
    }

    public static void LogWarn(
        string msg,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        AppLog.Warn(msg, memberName: memberName, filePath: filePath, lineNumber: lineNumber);
    }

    public static void LogWarn<T>(
        string msg,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        AppLog.Warn<T>(msg, memberName: memberName, filePath: filePath, lineNumber: lineNumber);
    }

    public static void LogError(
        string msg,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        AppLog.Error(msg, memberName: memberName, filePath: filePath, lineNumber: lineNumber);
    }

    public static void LogError<T>(
        string msg,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        AppLog.Error<T>(msg, memberName: memberName, filePath: filePath, lineNumber: lineNumber);
    }

    public static void LogError(
        Exception ex,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        AppLog.Error(ex, memberName: memberName, filePath: filePath, lineNumber: lineNumber);
    }

    public static void LogError<T>(
        Exception ex,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        AppLog.Error<T>(ex, memberName: memberName, filePath: filePath, lineNumber: lineNumber);
    }

    public static void LogFatal(
        string msg,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        AppLog.Fatal(msg, memberName: memberName, filePath: filePath, lineNumber: lineNumber);
    }

    public static void LogFatal<T>(
        string msg,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        AppLog.Fatal<T>(msg, memberName: memberName, filePath: filePath, lineNumber: lineNumber);
    }
}
