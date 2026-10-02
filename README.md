<div align="center">

<img src="src/ClassIsland.Promethium/icon.png" width="150" alt="Pm钷">

# Pm钷

**ClassIsland 综合增强插件**

*着细成器，累土化石。*

</div>

---

## 这是什么

一个从头自主开发的 ClassIsland 增强插件。装好之后，设置窗口里会多出一个入口 **「Pm优化」**，
各项功能作为它下面的分页挂上去。

目前已经做完第一项：

### 更好的天气

- **地图选点** —— 自己实现的 Web 墨卡托瓦片渲染，拖动平移、滚轮以光标为锚点缩放、点击取经纬度
- **按地名搜索** —— 能搜到镇/街道一级，选中即用
- **街道级地点名** —— 由 OpenStreetMap 逆地理编码反查回填（实测能到「xx街道xx路」）
- **三个免密钥数据源** —— Open-Meteo / MET Norway / wttr.in，随时切换
- **四种图标画法** —— 系统图标 / emoji（可指定字体）/ 自定义图片 / 纯文字
- **显示模式预设** —— 标准 / 紧凑 / 详细 / 自定义，也可逐项抠
- **阈值报警** —— 高温、低温、大风、强降水、雷暴、大雾或霾
- **报警模式可选** —— 关闭 / 只弹提醒 / 只实时显示 / 提醒 + 实时显示；报警图标可配
- **文案支持变量** —— 提醒和实时显示各有模板，写错的变量会原样保留，方便发现
- **自定义提示音** —— 走宿主音频服务，可与 CI 通知自带的声音二选一

完整说明、变量表与图标命名约定，见 **[插件说明文档](src/ClassIsland.Promethium/README.md)**。

---

## 必须说清楚的几件事

这些不是免责声明套话，是设计上的硬约束：

1. **网络数据不代表真实天气信息。** 天气数据全部来自第三方公开接口，仅供参考，请以气象台正式发布为准。
2. **插件的「报警」不是气象台发布的预警信号**，只是拿预报数值去比你自己设的阈值。
3. **所有报警都只依据数据源的真实返回值做阈值比较**，不含任何推测性判断 ——
   不报「可能要有暴雨了」这种话，只会说清楚哪个数值越过了哪条线。
4. **本次开发机的连通性测试不代表你机器的表现** —— 开发机不在国内，
   三个天气源在那里都通，但国内网络环境下请自行实测。

---

## 环境与构建

- ClassIsland 2.0.0.1 ~ 2.1.x（API 版本 `2.0.0.0`）
- .NET 8 SDK

```bash
dotnet build src/ClassIsland.Promethium/ClassIsland.Promethium.csproj -c Release
./tools/pack.sh          # 产物：dist/Pm-<版本>.cipx
```

自带校验台，会拉起 headless Avalonia 验证 XAML 能加载，跑判定逻辑断言，
并联网用真实返回验证各数据源的解析：

```bash
dotnet run --project tests/PmXamlCheck/PmXamlCheck.csproj -c Release
```

---

## 声明

### 作者

秋夕月拾旧

### 关于 AI

本项目由 **DSH** 辅助完成 —— 包括代码、说明文档与图标。

### 数据来源

天气来自 [Open-Meteo](https://open-meteo.com/)、[MET Norway](https://api.met.no/)、[wttr.in](https://wttr.in/)；
地名与瓦片来自 [OpenStreetMap](https://www.openstreetmap.org/)（© OpenStreetMap 贡献者，ODbL）。

### 许可

[MIT](LICENSE) © 2026 秋夕月拾旧
