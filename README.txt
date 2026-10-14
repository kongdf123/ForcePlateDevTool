# KWFS02 起跑器诊断工具 (ForcePlateDevTool)

WPF (.NET 8.0-windows) 诊断工具，用于 KWFS02 三维起跑器（76B 帧格式）的连接、接收、解析与力值解码全链路排查。

## 项目结构

```
ForcePlateDevTool/
├── src/ForcePlateDevTool/          主应用（WPF）
│   ├── MainWindow.xaml(.cs)        主窗口：六阶段诊断链路 + 四个工作区标签页
│   ├── Models/
│   │   ├── DiagnosticsModels.cs    DiagnosticConfig + 事件参数（RawBytes/FrameDecoded/LinkState）
│   │   ├── DiagnosticStatistics.cs 诊断统计快照（字节/帧/异常/时间戳）
│   │   ├── ForceSample.cs          单样本力值（Timestamp + Fx/Fy/Fz）
│   │   └── Pedal.cs                踏板枚举（Left/Right/Unknown）
│   ├── Services/
│   │   ├── DiagnosticEngine.cs     核心引擎：UDP 连接、接收循环、帧映射、解码、统计
│   │   ├── EndpointValidator.cs    IP/端口输入校验
│   │   ├── BoundedQueue.cs         有界队列（带丢弃计数）
│   │   ├── ForceSampleBuffer.cs    图表样本缓冲（1200 条 / 10 秒窗口）
│   │   ├── ForceSampleRouter.cs    地址路由：0xAA→左踏板，0xAB→右踏板
│   │   └── CommandSendResult.cs    指令发送结果（Success/Message/Command）
│   └── Controls/
│       └── ForceTrendChart.cs      自绘实时力值趋势图（Fx/Fy/Fz 三线）
├── KWLib/                          共享库（通过 csproj 通配编译进主项目）
│   ├── CommunicationLib/
│   │   ├── Decoder/                DecoderBase + Decoder_76B_StartingBlock
│   │   ├── Linker/Socket/          SocketLinker（UDP 模式）
│   │   └── Configuration/          DecoderConfig / LinkerConfig / CollectionMode
│   └── Sensor.DataModel/           SensorData / SensorData_76B_StartingBlock
├── ProductionRef/                  生产代码参考副本
├── doc/                            需求与架构设计文档
└── ForcePlateDevTool.sln
```

## 主窗口布局

- **设备与连接**：设备 IP/端口、本地 IP/端口、采集模式（被动 48 / 主动 49 / 仅外部触发 58）；网络检测、连接、断开按钮。
- **诊断链路（六阶段）**：
  1. 网络可达 — ICMP Ping 结果
  2. UDP Socket — Socket 初始化状态
  3. 指令交互 — 开始/停止指令发送结果
  4. 数据接收 — 接收中 / 等待 / 停滞检测
  5. 帧解析 — 有效帧识别 / 异常数据
  6. 力值解码 — 已解码 / 解码异常
- **工作区标签页**：
  1. **解码帧与力值** — DataGrid 展示帧序号、踏板、时间、地址、Fx/Fy/Fz、原始浮点数据
  2. **原始字节流** — HEX 字节流（有界显示）
  3. **采集与高级指令** — 开始/停止/仅接收按钮 + 预设 HEX 指令下拉 + 自定义 HEX 发送
  4. **实时力值趋势** — 左/右踏板 Fx/Fy/Fz 实时曲线图 + 当前数值
- **运行统计与日志**：接收字节、有效帧、异常事件计数 + 滚动日志

## 诊断引擎 (DiagnosticEngine)

核心职责：
- `ConnectAsync()` — 创建 `SocketLinker`（UDP，绑定本地端点），构建 `Decoder_76B_StartingBlock`
- `StartReceiveLoop()` — 后台 `Task` 循环调用 `SocketLinker.Receive`
- `ProcessBytes()` — 累积缓冲，调用 `DecoderBase.GetMapping()` 切帧；无效字节回调计数
- `DecodeFrame()` — 调用 `Decoder_76B_StartingBlock.Decode()`，输出 `SensorData[]`
- `SendStartCommandAsync()` / `SendStopCommandAsync()` / `SendCommandAsync(byte[])` — 发送指令并返回 `CommandSendResult`

事件：`RawBytesReceived`、`FrameDecoded`、`LinkStateChanged`、`LogMessage`

统计计数器（`DiagnosticStatistics`）：
`ReceivedDatagrams`、`ReceivedBytes`、`DecodedFrames`、`InvalidDataEvents`、`DecodeFailures`、`DecodeExceptions`、`LastReceiveTime`、`LastDecodedTime`

## 帧格式（Decoder_76B_StartingBlock）

- 帧长：76 字节
- 帧头：`0x48`，帧尾：`0x0D 0x0A`
- 地址位：支持；`0xAA` = 左踏板，`0xAB` = 右踏板
- 数据区：72 字节 = 18 个 float（每踏板 9 个，前 3 个为 Fx/Fy/Fz）
- 采集模式对应开始指令：被动 `0x48`、主动 `0x49`、仅触发 `0x58`；停止 `0x43`

## 内存与性能边界

- 原始字节显示缓冲：超过 200,000 字符时截断保留最新 100,000 字符
- 解码帧 DataGrid：最多保留 500 行
- 待显示力值样本队列：`BoundedQueue` 容量 5000，溢出时丢弃最旧样本并累计 `DroppedCount`
- 趋势图样本缓冲：1200 条或 10 秒时间窗，超出即丢弃
- UI 刷新：`DispatcherTimer` 每 100ms 批量 flush 原始字节、帧、力值样本与统计，避免高频跨线程更新

## 重要实现说明

1. **Ping 仅作网络可达性证据**。ICMP 成功不代表 UDP 端口可通；连接流程中即使 Ping 失败也会继续尝试 UDP Socket 初始化。
2. **指令发送结果为本地调用结果**。`CommandSendResult.Success` 表示 `Socket.Send` 调用未抛异常，并非设备已确认接收。指令阶段显示"本地发送成功，等待设备数据"或"开始指令已提交"。
3. **异常事件计数**。"异常事件" = `InvalidDataEvents`（帧映射丢弃的无效字节段）+ `DecodeFailures`（解码返回 false）+ `DecodeExceptions`（解码抛异常）。三者均为 `DiagnosticEngine` 内的专用计数器，非日志匹配。
4. **数据停滞检测**。已连接但超过 2 秒无新解码帧时，接收阶段显示"数据已停滞"。
5. **关闭流程**。窗口关闭时隐藏窗口，以 2 秒超时等待 `DiagnosticEngine.DisposeAsync()`，并清空重控件（帧列表、原始字节、日志）后再关闭，避免 WPF 视觉树析构卡顿。
6. **保留协议语义**。本工具不修改 `KWLib` 中的解码器/通讯协议行为，仅复用其 `GetMapping`/`Decode`/`GetCommand` 接口。

## 构建

Visual Studio 2022 打开 `ForcePlateDevTool.sln`，目标框架 `net8.0-windows`，启用 WPF。`KWLib` 通过 `<Compile Include="..\..\KWLib\**\*.cs" />` 直接编译进主程序集，无需单独项目引用。