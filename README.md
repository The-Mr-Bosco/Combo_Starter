# ComboStarter

文件连携启动器：打开一个文件时，自动把它的「同伴文件」一起打开；已经打开过的目标会自动跳过。

A file-association launcher for Windows (cross-platform design) that opens a file together with its companions.

## 特性

- **规则驱动**：按扩展名、文件名通配（`*.sln`）或路径通配（`D:/Code/**`）匹配，多条规则可同时生效。
- **目标灵活**：文档、目录、可执行文件，或任意命令行程序；文档可以指定专用打开器。
- **进程级去重**：可执行文件按运行中的进程路径判断；文档按启动记录与打开器进程存活状态判断。
- **Windows 文件关联**：`combo register .md` 一键写入 HKCU（无需管理员），包含右键菜单与「打开方式」列表。
- **递归防护**：ComboStarter 不会在关联给自己时反复启动自己，并会提示配置真正的打开器。
- **零第三方运行时依赖**：只用 .NET 10 基础类库；Core 与 CLI 分离，后续可直接复用做 GUI。
- **跨平台预留**：配置目录、状态目录、进程检测均已按 Windows / Linux 抽象。

## 快速开始

要求 [.NET 10 SDK](https://dotnet.microsoft.com/download)。

```powershell
git clone https://github.com/The-Mr-Bosco/Combo_Starter.git
cd Combo_Starter

# 构建
dotnet build ComboStarter.slnx -c Release

# 生成默认配置（Windows: %APPDATA%\ComboStarter\config.json）
dotnet run --project src/ComboStarter.Cli -- config init
```

编辑生成的 `config.json`，加入你的规则：

```jsonc
{
  "version": 1,
  "openers": {
    ".md": "code"
  },
  "rules": [
    {
      "name": "Markdown",
      "match": { "extensions": [".md"] },
      "open": [
        { "path": "{fileDir}/README.md" }
      ]
    }
  ]
}
```

然后：

```powershell
# 只看计划，不启动任何程序
dotnet run --project src/ComboStarter.Cli -- resolve C:\notes\todo.md

# 真正打开
dotnet run --project src/ComboStarter.Cli -- open C:\notes\todo.md
```

### 发布单文件

```powershell
dotnet publish src/ComboStarter.Cli -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -o publish
```

`publish\combo.exe` 可直接复制到任意目录使用，目标机器无需安装 .NET 运行时。把该目录加入 `PATH` 后即可用 `combo` 命令。

> 注意：通过 `dotnet run` 启动时，`register` 会因为宿主是 `dotnet.exe` 而被拒绝；请使用构建产物 `src\ComboStarter.Cli\bin\Release\net10.0\combo.exe` 或发布后的 `combo.exe`。

## 命令

| 命令 | 说明 |
| --- | --- |
| `combo open <path> [--force] [--dry-run] [--verbose]` | 打开文件及其连携目标 |
| `combo resolve <path> [--json]` | 只计算并展示计划，不启动任何程序 |
| `combo config init [--force]` | 生成默认配置文件 |
| `combo config path` | 打印配置文件位置 |
| `combo register <ext> [--opener <program>]` | 注册扩展名关联（Windows） |
| `combo unregister <ext>` | 取消注册，并还原此前的默认值 |
| `combo status [--json]` | 查看配置、规则、关联与状态文件概况 |
| `combo --help` / `combo version` | 帮助与版本 |

- 第一个参数不是已知命令、且是存在的路径时，等价于 `open`，例如 `combo notes.md`。
- 全局选项 `--config <file>` 可替换默认配置文件；环境变量 `COMBOSTARTER_CONFIG` 也可指定。
- 退出码：`0` 成功，`1` 失败，`2` 用法或配置错误，`3` 部分目标失败。

一次实际运行的输出示例：

```text
ComboStarter 0.1.0
Trigger: C:\notes\todo.md
Rules:
  - Markdown (extension .md)
Plan:
  [open]     C:\notes\todo.md  (document, opener: code)
  [skip]     C:\notes\README.md  (document, already open (pid 1234))
  [open]     C:\Tools\Typora\Typora.exe  (executable, opener: C:\Tools\Typora\Typora.exe)
```

## 配置

配置文件位置（优先级从高到低）：

1. `--config <file>`
2. 环境变量 `COMBOSTARTER_CONFIG`
3. Windows：`%APPDATA%\ComboStarter\config.json`；Linux：`$XDG_CONFIG_HOME/ComboStarter/config.json`（默认 `~/.config`）

格式是带注释的 JSON（JSONC）。配置里的相对路径**相对于配置文件所在目录**解析。完整字段说明见 [docs/config.md](docs/config.md)，示例见 [docs/config.example.json](docs/config.example.json)。

规则要点：

- 同一字段（如 `extensions`）内是「任一命中」；不同字段之间是「同时满足」。
- 所有命中的规则都会执行，目标取并集并按路径去重。
- 占位符：`{file}`、`{fileDir}`、`{fileName}`、`{fileStem}`；支持 `~`、`%VAR%`（Windows）、`$VAR`（Linux）。
- `path` 目标：可执行文件直接运行；目录用文件管理器打开；文档使用指定打开器或系统默认关联。
- `program` 目标：运行任意命令，不参与重复打开检测（适合「总是执行」的动作）。
- 打开器优先级：规则/目标的 `openWith` → 配置 `openers` → 注册时保存的打开器 → 系统默认关联。
- 目标不存在时默认警告并跳过；加 `"required": true` 会让本次运行以失败结束。

## 重复打开检测

- **可执行文件**：枚举运行中的进程并比较可执行文件完整路径；同路径已有实例则跳过。`--force` 可以强制再开一个。
- **文档**：成功启动后记录「目标路径、打开器、PID、进程启动时间」到状态文件（Windows `%LOCALAPPDATA%\ComboStarter\state.json`，可用 `COMBOSTARTER_STATE` 覆盖）。下次运行时：
  - 记录的 PID 仍存活且启动时间一致 → 视为已打开；
  - 否则打开器进程仍在运行，且记录不超过 `detection.maxAgeMinutes` → 视为已打开（应对 VS Code 这类单实例/代理进程）；
  - 否则重新打开。
- 可在配置里用 `"detection": { "enabled": false }` 完全关闭检测。

已知限制：外部程序打开文档时 ComboStarter 无法得知；单实例编辑器里关闭了文件但进程仍驻留时可能误判；以更高权限运行的进程通常无法读取其路径。

## Windows 文件关联

```powershell
# 为 .md 注册关联，并记住用 VS Code 打开
combo register .md --opener "C:\Program Files\Microsoft VS Code\Code.exe"

combo status
combo unregister .md
```

`register` 会写入：

1. ProgID `HKCU\Software\Classes\ComboStarter.<ext>`，命令为 `"<combo.exe>" open "%1"`；
2. `HKCU\Software\Classes\.<ext>\OpenWithProgids`，让 ComboStarter 出现在「打开方式」；
3. 右键菜单项「Open with ComboStarter」；
4. 若该扩展名没有 `UserChoice`、也没有机器级默认值，则把 ComboStarter 设为当前用户默认。

Windows 10/11 对默认应用选择有哈希保护（`UserChoice`），程序无法静默抢占。若提示「保留了当前默认应用」，请在资源管理器中右键文件 →「打开方式」→ 选择 ComboStarter 并勾选「始终使用此应用」，或直接使用右键菜单。

`register` 会探测该扩展名原本的默认程序并保存为打开器，避免「ComboStarter 打开 .md 又调用 ComboStarter」的死循环。如果探测不到，请用 `--opener` 指定，或之后在配置的 `openers` 中补上。`unregister` 只删除自己写入的键值，并还原注册前的默认值；如果你曾通过系统设置把 ComboStarter 设为默认应用，取消注册后请到系统设置里改回其他应用（`UserChoice` 受系统保护，程序不能修改）。

Linux 的桌面关联（`xdg-mime` + `.desktop`）尚未实现。

## 项目结构

```text
src/ComboStarter.Core/   配置、规则匹配、计划、启动、去重、平台关联
src/ComboStarter.Cli/    combo 命令行入口
docs/                    配置示例与字段文档
```

后续 GUI 只需引用 `ComboStarter.Core`，无需复制业务逻辑。

## 路线图

- [ ] Linux 桌面文件关联（xdg-mime / .desktop）
- [ ] GUI（复用 Core）
- [ ] 单元测试与 CI
- [ ] 托盘常驻、开机自启
- [ ] 配置 JSON Schema 与编辑器补全
- [ ] 消息本地化

## 开发

```powershell
dotnet build ComboStarter.slnx -c Release
dotnet run --project src/ComboStarter.Cli -- --help
```

环境变量：

| 变量 | 作用 |
| --- | --- |
| `COMBOSTARTER_CONFIG` | 覆盖配置文件路径 |
| `COMBOSTARTER_STATE` | 覆盖状态文件路径（便于测试） |
| `NO_COLOR` | 关闭彩色输出 |

## License

[MIT](LICENSE) © 2026 Mr-Bosco
