# DSH Manager

一个面向 Windows 的 DeepSeek Harness（DSH）本地管理器。它提供安装、更新、启动、停止、端口设置、插件中心、美化插件管理，以及 GPT Responses 兼容配置。

项目地址：<https://github.com/falsecsx/dsh-manager>

## 功能

- 管理 DSH 的安装目录、端口和启动方式
- 启动、停止服务并打开本地 Web UI
- 浏览、筛选和下载 DSH 社区插件
- 管理美化插件的启用和停用状态
- 可选启用 GPT 工具调用兼容修复
- 可选为 `gpt-*` 模型显示 `low / medium / high / xhigh / max` 思考强度
- 保留配置备份，关闭补丁时恢复用户原始配置

## 构建

要求：Windows、.NET Framework 4.x、系统自带 `csc.exe`。

```bat
build.cmd
```

构建产物为 `DSH Manager.exe`。运行时需要一个可用的 DSH 安装目录；管理器会按用户选择安装或更新 DSH。

## 测试

兼容补丁测试需要目标 DSH 安装目录中存在 `js-yaml` 和 DSH 的运行时依赖：

```bat
node tests\gpt-compat.test.cjs
node tests\reasoning-compat.test.cjs
powershell -ExecutionPolicy Bypass -File tests\check-layout.ps1
```

## 安全说明

- 不修改 DSH 的 `node_modules`。
- 不把 API 密钥写入仓库；配置文件由本地 DSH 安装目录管理。
- 兼容修复只修改用户明确启用的配置字段，并保存原始 `settings.yaml` 备份。
- 访问令牌只用于当前本地服务，日志显示时会自动打码。

## 许可证

本项目使用 MIT License。详见 [LICENSE](LICENSE)。

