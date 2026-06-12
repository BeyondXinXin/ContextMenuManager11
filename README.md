# ContextMenuManager11

一个专注于 **Windows 11 右键菜单管理** 的小工具。

主要用于扫描和禁用各种软件添加的右键菜单，清理臃肿、杂乱的右键选项。

## 功能

* 扫描文件、文件夹、桌面、驱动器等右键菜单
* 扫描 Shell 与 ShellEx（ContextMenuHandlers）菜单项
* 启用、禁用、编辑右键菜单
* 管理「新建」「发送到」「打开方式」
* 一键切换 Windows 11 经典右键菜单
* 一键重启 Explorer

## 特点

* 纯注册表方案
* 不注入、不 Hook Explorer
* 不安装 Shell Extension
* 支持管理大多数第三方软件注册的右键菜单
* 尽量避免对系统和 Explorer 本身进行修改

## 运行环境

* Windows 11
* .NET 8 Desktop Runtime

## 编译

```bat
build.cmd
```

或

```bat
dotnet build src\ContextMenuManager11.csproj -c Release
```

## 说明

本工具仅管理基于注册表的 Windows 右键菜单配置，适用于清理和管理大多数软件添加的右键菜单项，不修改系统核心组件。
