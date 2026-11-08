# CS2 Flashlight

[CounterStrikeSharp](https://github.com/roflmuffin/counterstrikesharp) 动态手电筒插件。玩家存活时可以切换聚光灯，灯光位置和方向会随视角更新；死亡或断开连接时会移除灯光实体。

## 使用方式

- 玩家按 `F`（`+lookatweapon`）单独切换自己的手电筒。
- 管理员可在游戏控制台执行 `css_flashlight 1` 为当前在线玩家开启，执行 `css_flashlight 0` 为当前在线玩家关闭。
- `css_flashlight` 管理命令要求玩家具有 `@css/root` 权限；它只影响执行时在线的玩家。
- 有 `@css/root` 权限的玩家执行 `css_flashlight_reload` 可重新读取配置。重载时当前灯光实体会删除，已启用的玩家会在后续 tick 自动重新创建灯光。

## 配置

首次加载时，插件目录会生成 `flashlight.json`。修改后由管理员执行 `css_flashlight_reload` 生效。

```json
{
  "EnabledByDefault": false,
  "Brightness": 5,
  "Range": 1500,
  "Color": "255 255 255",
  "InnerCone": 20,
  "OuterCone": 50,
  "Exponent": 2,
  "ConstantAttenuation": 0.15,
  "LinearAttenuation": 0.000001,
  "QuadraticAttenuation": 0.0000001,
  "ForwardOffset": 0,
  "HeightOffset": 64
}
```

- `EnabledByDefault`：插件加载时是否为当时在线的玩家默认开启，默认 `false`。
- `Brightness`：动态灯光亮度，默认 `5`。
- `Range`：灯光照射距离，默认 `1500`。
- `Color`：RGB 颜色，格式为 `"红 绿 蓝"`，每个通道范围为 `0` 到 `255`。
- `InnerCone`、`OuterCone`：聚光灯内外锥角，默认分别为 `20` 和 `50`。
- `Exponent`：聚光灯衰减指数，默认 `2`。
- `ForwardOffset`：灯光相对玩家原点沿视线方向的偏移，默认 `0`。
- `HeightOffset`：灯光相对玩家原点的高度偏移，默认 `64`。
- `ConstantAttenuation`、`LinearAttenuation`、`QuadraticAttenuation`：配置字段目前保留兼容，但当前实现未将其应用到 `light_dynamic` 实体。

## 编译与安装

需要 .NET 10 SDK。运行：

```powershell
dotnet publish -c Release
```

将 `bin/Release/net8.0/publish/Flashlight.dll` 放入：

```text
game/csgo/addons/counterstrikesharp/plugins/Flashlight/
```

然后重启服务器或通过 [CounterStrikeSharp](https://github.com/roflmuffin/counterstrikesharp) 加载插件。
