# 配置参考

配置文件使用带注释的 JSON（JSONC）：允许 `//`、`/* */` 注释和尾随逗号，未知字段会导致加载失败。

## 位置与覆盖

| 优先级 | 来源 |
| --- | --- |
| 1 | 命令行 `--config <file>` |
| 2 | 环境变量 `COMBOSTARTER_CONFIG` |
| 3 | Windows `%APPDATA%\ComboStarter\config.json` |
| 4 | Linux `$XDG_CONFIG_HOME/ComboStarter/config.json`（默认 `~/.config/...`） |

配置中的相对路径相对于**配置文件所在目录**解析。可用 `combo config path` 查看当前生效路径。

## 顶层字段

```jsonc
{
  "version": 1,
  "openers": { ".md": "code" },
  "detection": { "enabled": true, "maxAgeMinutes": 720 },
  "rules": [ /* ... */ ]
}
```

| 字段 | 类型 | 默认值 | 说明 |
| --- | --- | --- | --- |
| `version` | number | `1` | 配置格式版本，目前只支持 `1` |
| `openers` | object | `{}` | 扩展名 → 打开器，例如 `".md": "code"` 或完整路径 |
| `detection.enabled` | bool | `true` | 是否跳过已打开的目标 |
| `detection.maxAgeMinutes` | number | `720` | 打开器仍存活时，最多信任多少分钟内的启动记录；`0` 表示只认 PID |
| `rules` | array | `[]` | 连携规则列表 |

`openers` 的键会统一为小写、带前导点；值可以是 PATH 中的命令名（`code`、`notepad.exe`）或完整路径。

## 规则

```jsonc
{
  "name": "Markdown",
  "enabled": true,
  "match": {
    "extensions": [".md", ".markdown"],
    "fileNames": ["README*"],
    "pathGlobs": ["D:/Notes/**"]
  },
  "opener": "code",
  "openSelf": true,
  "open": [
    { "path": "{fileDir}/README.md" },
    { "path": "C:/Tools/Typora/Typora.exe", "args": ["--new-window"] },
    { "program": "code", "args": ["{fileDir}"] }
  ]
}
```

| 字段 | 类型 | 默认值 | 说明 |
| --- | --- | --- | --- |
| `name` | string | `Rule N` | 规则名称，仅用于输出 |
| `enabled` | bool | `true` | 设为 `false` 可临时停用 |
| `match` | object | 必填 | 匹配条件，至少填一项 |
| `opener` | string | 无 | 打开**被触发文件本身**时使用的打开器，优先于全局 `openers` |
| `openSelf` | bool | `true` | 是否打开触发本次运行的文件本身 |
| `open` | array | `[]` | 需要连带打开的目标 |

### 匹配语义

- `match.extensions`：扩展名列表，不区分大小写，`md` 与 `.md` 等价。
- `match.fileNames`：对文件名做通配匹配，支持 `*` 和 `?`（不区分大小写，仅 Windows）。
- `match.pathGlobs`：对完整路径做通配匹配，建议用正斜杠；`*` 可以跨目录，例如 `D:/Code/**`。
- 同一字段内是「任一命中」；不同字段之间是「同时满足」。
- 多条规则可以同时命中，所有规则的 `open` 目标会合并、按规范化路径去重。
- 没有任何规则命中时，`combo open <file>` 仍然会打开该文件本身（相当于普通的默认关联打开）。

## 目标

每个目标只能是 `path` 或 `program` 之一。

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `path` | string | 文件、目录或可执行文件 |
| `program` | string | 要运行的命令；不做重复打开检测 |
| `openWith` | string | 仅用于 `path`：用指定程序打开该文件，目标路径会作为最后一个参数追加 |
| `args` | array | 传给程序的参数；支持占位符 |
| `required` | bool | `path` 不存在时是否视为失败，默认 `false`（警告并跳过） |

`path` 的行为：

- 目录 → 用系统文件管理器打开；
- `.exe` / `.com`（Windows）或带执行权限的文件（Linux）→ 直接启动，`args` 原样传入；
- 其他文件 → 有 `openWith` 时用指定程序打开，否则用打开器链（见下）或系统默认关联。

`program` 的行为：先尝试在 PATH 中解析；解析到可执行文件就直接启动，否则交给系统 shell。命令名可以是 `code`、`notepad.exe`，也可以是完整路径。

打开器解析顺序（用于文档和触发文件本身）：

1. 规则的 `opener`（仅触发文件）或目标的 `openWith`；
2. 配置顶层的 `openers[扩展名]`；
3. `combo register` 时探测并保存的打开器；
4. Windows 系统默认关联。

若解析结果指向 ComboStarter 自己，本次目标会被拒绝并提示配置真正的打开器，避免无限递归。

## 占位符与变量

| 写法 | 展开为 |
| --- | --- |
| `{file}` | 被触发文件的完整路径 |
| `{fileDir}` | 被触发文件所在目录 |
| `{fileName}` | 被触发文件名（含扩展名） |
| `{fileStem}` | 被触发文件名（不含扩展名） |
| `~` / `~/...` | 用户主目录 |
| `%NAME%`（Windows） | 环境变量 |
| `$NAME` / `${NAME}`（Linux） | 环境变量 |

缺失的环境变量保持原样，不会报错。

## 完整示例

```jsonc
{
  "version": 1,

  "openers": {
    ".md": "code",
    ".txt": "notepad.exe"
  },

  "detection": {
    "enabled": true,
    "maxAgeMinutes": 720
  },

  "rules": [
    {
      "name": "Markdown 写作",
      "match": { "extensions": [".md", ".markdown"] },
      "opener": "code",
      "open": [
        { "path": "{fileDir}/README.md" },
        { "path": "~/notes/todo.md" },
        { "path": "C:/Tools/Typora/Typora.exe", "args": ["--new-window"] }
      ]
    },
    {
      "name": "解决方案",
      "match": { "fileNames": ["*.sln", "*.slnx"], "pathGlobs": ["D:/Code/**"] },
      "openSelf": false,
      "open": [
        { "path": "{fileDir}", "required": true },
        { "program": "code", "args": ["{fileDir}"] }
      ]
    }
  ]
}
```

## 校验与错误

加载时会检查：版本号、未知字段、规则是否至少有一个匹配条件、`path` / `program` 是否二选一、`args` / 扩展名格式、`maxAgeMinutes` 非负等。错误会带配置文件路径和行列号（JSON 语法错误）或规则位置（语义错误），退出码为 `2`。

目标文件不存在默认只产生警告；`"required": true` 的目标会让本次运行返回失败（退出码 `1` 或 `3`）。
