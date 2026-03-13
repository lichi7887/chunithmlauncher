# CHUNITHM Launcher

基于 WebView2 的 CHUNITHM HDD 快捷启动器：启动前切换分辨率，检测游戏窗口关闭后恢复，并提供首次配置向导与主题化 UI。

## 项目声明

- 本项目由 **vibecoding** 完整开发
- 项目作者在法律允许范围内放弃对本项目代码及相关内容的全部权利，并贡献至公共领域（Public Domain）

## 功能特性

- 启动游戏前自动切换目标分辨率
- 监测游戏窗口关闭后自动恢复原分辨率
- 首次配置向导与可视化设置界面
- 主题化 UI，支持背景图自定义

## 轻量化策略

为精简项目体积，仓库与发布内容不再内置完整运行时：

- `WebView2 Runtime` 由用户在运行时按提示自行下载并安装
- 发布采用 framework-dependent 模式（不打包 .NET Runtime）

## 使用方式

### 1. 本地编译

直接运行`publish.ps1`即可，产物在`artifacts\publish`目录下

### 2. 发布

```powershell
powershell -ExecutionPolicy Bypass -File .\\publish.ps1
```

## 界面截图

### 默认背景

![默认背景](https://raw.githubusercontent.com/lichi7887/chunithmlauncher/refs/heads/main/docs/image/screenshot-dark.png)

### 自定义背景

![自定义背景](https://raw.githubusercontent.com/lichi7887/chunithmlauncher/refs/heads/main/docs/image/screenshot-wallpaper.png)

## 开源许可证

本项目采用 **The Unlicense**。

- 你可以自由使用、复制、修改、发布、分发、再许可或出售本项目
- 无需授权、无需署名（法律另有强制规定的情况除外）
- 本项目按“现状”提供，不附带任何明示或暗示担保

完整许可证文本见 [LICENSE](./LICENSE)。
