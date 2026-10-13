# ForcePlateDevTool Main Window Refresh

This package contains a proposed first-pass UI refresh for the existing WPF project.

## Files
- `MainWindow.xaml`: reorganized main window; keeps the existing named controls and event-handler names.
- `MainWindow.xaml.cs`: wires the new pipeline status labels to the existing `DiagnosticEngine` events.

## Design goals
- Make decoded frames the default workspace.
- Keep network/TCP configuration and connection actions easy to find.
- Add a six-stage diagnostic pipeline: network, TCP, command, receive, parse, decode.
- Move manual HEX commands into a secondary tab.
- Keep raw bytes and logs available without competing with the primary data view.
- Bound raw-stream display memory.

## Important implementation notes
1. The pipeline statuses are deliberately conservative. A successful Ping is only network reachability evidence, not proof that the TCP port is open.
2. The current `DiagnosticEngine` exposes malformed-data information through log messages, not a dedicated invalid-frame event. Therefore `异常事件` counts matching log events; it is **not** a precise count of invalid frames. For exact invalid-frame metrics, add a dedicated event/counter in `DiagnosticEngine`.
3. The current engine's command-send method logs send failures instead of returning a success result. Consequently the command stage is marked as “已发送开始指令” after the send request, not as a verified device acknowledgement.
4. This patch preserves existing event-handler names and decoder model bindings, but it has not been built against your local checkout. Please build and test it in Visual Studio before use with hardware.
5. Existing protocol semantics have intentionally not been changed.
