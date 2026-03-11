# PineA.BarricadeSaver

一个基于 **RocketMod / Unturned** 的建筑（Barricade）离线保存与恢复插件。

该插件可以在玩家离线后将符合条件的 Barricade 保存到 MySQL 数据库，并在玩家重新上线后自动恢复到地图，同时支持：

- 个人模式 / 组模式恢复
- 旧组建筑恢复
- 农作物离线生长补偿
- 建筑数量限制系统
- 白名单 / 黑名单 / 类型过滤
- 离线过久不恢复
- 空间碰撞检测，防止恢复重叠
- 建筑回收 / 摧毁后的数量自动修正

---

## 功能介绍

### 1. 建筑离线保存
服务器地图加载完成后，插件会扫描全地图 Barricade：

- 将符合条件的建筑保存到数据库
- 从地图上移除这些建筑
- 对未移除的建筑重新进行内存计数

适合用于离线建筑托管、建筑休眠、减少地图常驻建筑数量等用途。

---

### 2. 玩家上线自动恢复
当玩家连接服务器时：

- 如果为**个人模式**，恢复该玩家保存的建筑
- 如果为**组模式**：
  - 无组玩家按个人模式恢复
  - 有组玩家按组恢复
  - 若该组已有成员在线，则不会重复恢复该组建筑
- 插件还会额外尝试恢复玩家在“旧组”中的建筑，并保持原 Group 不变

---

### 3. 玩家离线自动保存
当玩家离线时：

- 个人模式下，保存并移除该玩家的建筑
- 组模式下：
  - 无组玩家按个人模式处理
  - 有组玩家会延迟检测组内是否还有人在线
  - 若组内无人在线，则保存并移除该组建筑

---

### 4. 旧组建筑恢复
插件支持恢复玩家在过去组 ID 下保存的建筑：

- 读取该玩家名下全部建筑记录
- 筛选 `Group != 当前组ID` 的记录
- 使用原始 Group 恢复，不会改写归属

---

### 5. 农作物离线生长补偿
恢复农作物类 Barricade（`EBuild.FARM`）时：

- 根据保存时间与当前时间差
- 自动补偿离线期间的生长时间

该功能由配置项 `GrowWhenSaved` 控制。

---

### 6. 空间碰撞检测
恢复建筑时可启用空间检测（`UseSpaceCheck`）：

- 门 / 闸 / 百叶 / 舱门：使用 OverlapBox
- 床：使用特殊球体检测
- 大多数建筑：使用 OverlapSphere
- 农作物 / 梯子 / 小物件：使用专门半径检测

若检测到位置被阻挡，则该建筑不会恢复，并计入失败数量。

---

### 7. 建筑过滤规则
插件支持以下过滤方式：

- `NotSaveSteamID`：不保存 / 不恢复的玩家
- `BarricadeWhitelist`：白名单物品 ID
- `BarricadeBlacklist`：黑名单物品 ID
- `BarricadeType`：允许处理的建筑类型列表

逻辑优先级如下：

1. 玩家在 `NotSaveSteamID` 中 -> 不处理
2. 建筑在黑名单中 -> 不处理
3. 建筑在白名单中 -> 直接处理
4. 否则按 `BarricadeType` 判断是否处理

---

### 8. 建筑数量限制系统
插件使用独立 XML 配置文件进行建筑限制：

- 支持按类型限制，如 `DOOR`、`BED`、`FARM`
- 支持 `ALL` 总数限制
- 支持不同权限对应不同上限
- 支持某类型是否计入 `ALL`
- 放置时即时判断是否超过上限
- 放置成功后发送数量提示
- 回收 / 摧毁后自动修正计数

---

### 9. 建筑摧毁 / 回收计数修正
插件会维护每个玩家的建筑数量缓存：

- 放置成功时 +1
- 回收时 -1
- 爆炸、管理员移除等非回收摧毁通过 Harmony Patch 自动 -1
- 内置去重逻辑，避免回收事件重复扣减

---

### 10. 玩家最后上线时间记录
插件会额外维护一个最后上线时间表：

- 玩家连接时自动更新最后上线时间
- 恢复建筑前判断离线时长
- 超过 `NotRestoreIfOfflineDays` 的建筑将不再恢复

---

## 工作流程

### 地图加载完成后
1. 扫描全地图 Barricade
2. 保存符合条件的建筑到数据库
3. 从地图移除这些建筑
4. 对剩余建筑重新计数

### 玩家上线时
1. 更新最后上线时间
2. 按个人模式或组模式恢复建筑
3. 尝试恢复旧组建筑
4. 根据空间检测决定是否允许恢复
5. 恢复成功后删除数据库中对应记录
6. 向玩家发送恢复结果提示

### 玩家离线时
1. 按配置决定保存个人建筑或组建筑
2. 扫描对应 Barricade
3. 保存到数据库
4. 若为储物建筑，先清空物品避免掉落
5. 从地图中销毁建筑

---

## 配置说明

## 主配置 `Config`

| 配置项 | 说明 |
|---|---|
| `MySqlConnectionString` | MySQL 连接字符串 |
| `BarricadeTableName` | 建筑数据表名 |
| `GrowWhenSaved` | 是否启用农作物离线生长补偿 |
| `UseSpaceCheck` | 是否启用恢复碰撞检测 |
| `UseGroupID` | 是否按 GroupID 保存 / 恢复 |
| `BarricadeType` | 允许处理的建筑类型 |
| `BarricadeWhitelist` | 白名单物品 ID |
| `BarricadeBlacklist` | 黑名单物品 ID |
| `NotSaveSteamID` | 不保存 / 不恢复玩家列表 |
| `MessageIconURL` | 聊天提示图标 |
| `Message_RestorePartial` | 单人模式部分恢复提示 |
| `Message_RestoreAll` | 单人模式全部恢复提示 |
| `Message_RestorePartial_Group` | 组模式部分恢复提示 |
| `Message_RestoreAll_Group` | 组模式全部恢复提示 |
| `NotRestoreIfOfflineDays` | 超过该离线天数后不恢复 |

---

## 默认配置示例

```xml
<Config>
  <MySqlConnectionString>Server=127.0.0.1;Port=3306;Database=myDB;Uid=user;Pwd=pass;</MySqlConnectionString>
  <BarricadeTableName>PineA_Barricade</BarricadeTableName>

  <GrowWhenSaved>true</GrowWhenSaved>
  <UseSpaceCheck>true</UseSpaceCheck>
  <UseGroupID>false</UseGroupID>

  <BarricadeType>
    <string>DOOR</string>
    <string>BED</string>
    <string>STORAGE</string>
    <string>FARM</string>
  </BarricadeType>

  <BarricadeWhitelist>
    <ushort>288</ushort>
  </BarricadeWhitelist>

  <BarricadeBlacklist>
    <ushort>289</ushort>
  </BarricadeBlacklist>

  <NotSaveSteamID>
    <ulong>76561198020988945</ulong>
  </NotSaveSteamID>

  <NotRestoreIfOfflineDays>7</NotRestoreIfOfflineDays>
  <MessageIconURL>http://example.com</MessageIconURL>
</Config>
```

---

## 限制配置文件

插件会手动加载：

```text
Plugins/PineA.BarricadeSaver/PineA.BarricadeLimit.xml
```

### 示例

```xml
<LimitConfig>
  <BasicSetting Enabled="true"
                PlacementMessage="&lt;b&gt;&lt;size=15&gt;&lt;color=#FFFACD&gt;[建筑限制]您的建筑物上限:&lt;color=#FFFFFF&gt;{Current}&lt;/color&gt;/&lt;color=#FFFFFF&gt;{Limit}&lt;/color&gt;&lt;/color&gt;&lt;/size&gt;&lt;/b&gt;"
                PlacementIconURL="" />

  <BarricadeLimit Type="ALL" Count="400" RequiredPermission="平民" CountInToALL="true" />
  <BarricadeLimit Type="ALL" Count="500" RequiredPermission="VIP" CountInToALL="true" />
</LimitConfig>
```

---

## 限制规则说明

每条 `BarricadeLimit` 包含：

| 字段 | 说明 |
|---|---|
| `Type` | 类型，如 `ALL`、`DOOR`、`FARM` |
| `Count` | 上限数量 |
| `RequiredPermission` | 所需权限 |
| `CountInToALL` | 是否计入 `ALL` |

### 限制逻辑
- 读取玩家当前建筑类型上限
- 读取玩家 `ALL` 总上限
- 判断该类型是否计入 `ALL`
- 若达到限制则阻止放置
- 放置成功后更新内存计数并发送提示

---

## 数据库表

### 1. 建筑表
表名由配置项 `BarricadeTableName` 决定，默认：

```text
PineA_Barricade
```

字段包含：

- `ID`
- `InstanceID`
- `BarricadeItemID`
- `Owner`
- `Group`
- `Health`
- `State`
- `PosX`
- `PosY`
- `PosZ`
- `RotX`
- `RotY`
- `RotZ`
- `SavedTime`

### 2. 最后上线时间表

```text
PineA.LastOnlineTime
```

字段包含：

- `SteamID`
- `LastOnline`

---

## 主要模块

当前源码包含以下核心文件：

- `BarricadeHelper.cs`
- `BarricadeLimit.cs`
- `BarricadeSaverLogic.cs`
- `Config.cs`
- `ConfigurationHelper.cs`
- `DatabaseManager.cs`
- `LastOnlineTimeManager.cs`
- `LimitConfig.cs`
- `Main.cs`
- `BarricadeDestroyedPatch.cs`
- `PineA_BarricadeData.cs`
- `Tool.cs`

---

## 依赖

### 运行环境
- Unturned Server
- RocketMod
- MySQL

### 依赖库
- `MySql.Data`
- `HarmonyLib`

### 主要命名空间
- `Rocket.API`
- `Rocket.Core`
- `Rocket.Unturned`
- `SDG.Unturned`
- `Steamworks`
- `UnityEngine`
- `HarmonyLib`

---

## 命令说明

当前这份源码中**没有自定义聊天命令**。  
插件主要通过以下事件自动运行：

- 地图加载事件
- 玩家连接事件
- 玩家断开事件
- Barricade 放置事件
- Barricade 回收事件
- Barricade 销毁补丁事件

---

## 权限说明

插件没有硬编码固定权限节点。  
但数量限制系统会读取 `PineA.BarricadeLimit.xml` 中的 `RequiredPermission` 字段来决定玩家能使用的限制规则。

例如：

```xml
<BarricadeLimit Type="ALL" Count="400" RequiredPermission="平民" CountInToALL="true" />
<BarricadeLimit Type="ALL" Count="500" RequiredPermission="VIP" CountInToALL="true" />
```

表示：

- 拥有 `平民` 权限的玩家总上限为 400
- 拥有 `VIP` 权限的玩家总上限为 500

---

## 安装方法

1. 编译插件生成 DLL
2. 将 DLL 放入服务器插件目录
3. 安装 RocketMod
4. 确保服务器可连接 MySQL
5. 安装依赖 `MySql.Data` 与 `HarmonyLib`
6. 启动服务器
7. 插件会自动生成配置文件并初始化数据库表
8. 修改配置后重启服务器使配置生效

---

## 注意事项

1. 插件会在地图加载后扫描并移除符合条件的建筑  
2. 若启用 `UseGroupID`，则建筑保存 / 恢复优先按组处理  
3. 若玩家离线超过 `NotRestoreIfOfflineDays`，则不会恢复其建筑  
4. 若启用 `UseSpaceCheck`，则部分建筑可能因碰撞检测失败而无法恢复  
5. 农作物恢复时可自动补偿离线生长时间  
6. 储物类建筑在移除前会清空物品，避免掉落  
7. 成功恢复后会删除数据库中对应记录  
8. 本 README 根据你当前提供的源码整理，后续新增模块时可继续补充

---

## 适用场景

- 离线建筑托管
- 分组建筑恢复
- 建筑数量上限控制
- 降低地图建筑负载
- 农场离线生长补偿
- 基于权限分级的建筑限制系统