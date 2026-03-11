using HarmonyLib;
using Rocket.Core.Plugins;
using Rocket.Unturned;
using Rocket.Unturned.Player;
using SDG.Unturned;
using Steamworks;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;

namespace PineA.BarricadeSaver
{
    public class Main : RocketPlugin<Config>
    {
        public static Main Instance { get; private set; }

        // 主配置（Rocket自动加载）
        public Config MainConfig { get; private set; }

        // 限制配置（手动加载）
        internal LimitConfig limitConfig;

        /// <summary>
        /// 记录每个玩家当前已放置的各种Barricade数量。
        /// 外层key是玩家SteamID，内层key是Barricade类型字符串，值是已放置数量。
        /// </summary>
        private Dictionary<ulong, Dictionary<string, int>> playerBarricadeCounts
            = new Dictionary<ulong, Dictionary<string, int>>();

        /// <summary>
        /// 用来存放“已在Salvage事件中做过 -1”的Barricade的instanceID，
        /// 以便 Harmony补丁时跳过重复扣减。
        /// </summary>
        public HashSet<uint> salvageDestroyedBarricades = new HashSet<uint>();

        /// <summary>
        /// 让其他类可以访问 playerBarricadeCounts
        /// </summary>
        public Dictionary<ulong, Dictionary<string, int>> PlayerBarricadeCounts => playerBarricadeCounts;

        /// <summary>
        /// 用来管理针对“某个实体ID”(可能是steamID，也可能是groupID)的Barricade恢复操作CancelToken。
        /// </summary>
        public ConcurrentDictionary<ulong, CancellationTokenSource> spawnCancellationMapByEntity
            = new ConcurrentDictionary<ulong, CancellationTokenSource>();

        /// <summary>
        /// 记录当前在线的实体(entityID)的数量（如：某个Group在线人数）。
        /// Key = entityID (可能是steamID，也可能是groupID),
        /// Value = 在线人数。
        /// </summary>
        public Dictionary<ulong, int> entityOnlineCount = new Dictionary<ulong, int>();

        /// <summary>
        /// 记录某个实体(entityID)的Barricade是否已经生成过，用于避免重复生成。
        /// </summary>
        public HashSet<ulong> entityBarricadeSpawned = new HashSet<ulong>();

        protected override void Load()
        {
            Instance = this;

            // 1) Rocket自动加载主配置 => this.Configuration
            MainConfig = this.Configuration.Instance;

            // 2) 手动加载限制配置 => PineA.BarricadeLimit.xml
            limitConfig = ConfigurationHelper.LoadConfiguration<LimitConfig>(
                "PineA.BarricadeSaver",   // 文件夹
                "PineA.BarricadeLimit"   // 文件名，不带 .xml
            );

            DatabaseManager.Initialize(MainConfig.MySqlConnectionString, MainConfig.BarricadeTableName);
            LastOnlineTimeManager.Initialize(MainConfig.MySqlConnectionString);

            // 4) 订阅事件
            Level.onPostLevelLoaded += OnPostLevelLoaded;
            U.Events.OnPlayerConnected += OnPlayerConnected;
            U.Events.OnPlayerDisconnected += OnPlayerDisconnected;

            // 打 Harmony 补丁
            var harmony = new Harmony("PineA.BarricadeSaver");
            harmony.PatchAll();

            // 放置/回收事件
            BarricadeManager.onDeployBarricadeRequested += OnDeployBarricadeRequested;
            BarricadeDrop.OnSalvageRequested_Global += BarricadeDrop_OnSalvageRequested_Global;
        }

        protected override void Unload()
        {
            // 取消事件订阅
            Level.onPostLevelLoaded -= OnPostLevelLoaded;
            U.Events.OnPlayerConnected -= OnPlayerConnected;
            U.Events.OnPlayerDisconnected -= OnPlayerDisconnected;

            // 取消所有Barricade生成操作
            foreach (var pair in spawnCancellationMapByEntity)
            {
                pair.Value.Cancel();
                pair.Value.Dispose();
            }
            spawnCancellationMapByEntity.Clear();

            // 取消订阅
            BarricadeManager.onDeployBarricadeRequested -= OnDeployBarricadeRequested;
            BarricadeDrop.OnSalvageRequested_Global -= BarricadeDrop_OnSalvageRequested_Global;

            Instance = null;
        }

        private void OnPostLevelLoaded(int level)
        {
            if (level <= Level.BUILD_INDEX_SETUP) return;

            // 1) 将地图上可存储的Barricade全部保存并移除
            BarricadeSaverLogic.SaveAndRemoveAllQualifiedBarricades();

            // 2) 对地图上剩余的所有Barricade进行内存计数
            BarricadeSaverLogic.RecountAllOnMap();
        }

        private void OnPlayerConnected(UnturnedPlayer player)
        {
            ulong steamID = player.CSteamID.m_SteamID;

            // 更新玩家最后一次上线的时间
            _ = LastOnlineTimeManager.UpdateLastOnlineTimeAsync(steamID);

            var steamPlayer = player.SteamPlayer();

            if (MainConfig.UseGroupID)
            {
                ulong groupID = steamPlayer.player.quests.groupID.m_SteamID;

                if (groupID == 0)
                {
                    // 玩家没有组，按个人模式恢复
                    _ = BarricadeSaverLogic.LoadAndPlaceBarricadesForEntityAsync(steamID, false, CancellationToken.None);

                    // 额外恢复玩家“旧组”中的建筑（保持其原Group）
                    _ = BarricadeSaverLogic.LoadAndPlaceBarricadesForOldGroupsAsync(steamID, CancellationToken.None);
                }
                else
                {
                    // 检查是否已有其他玩家同组在线
                    bool groupAlreadyOnline = Provider.clients.Any(sp =>
                        sp.player.quests.groupID.m_SteamID == groupID &&
                        sp.playerID.steamID.m_SteamID != steamID);

                    // 如果当前组中还没有其他玩家在线，则恢复该组的建筑
                    if (!groupAlreadyOnline)
                    {
                        _ = BarricadeSaverLogic.LoadAndPlaceBarricadesForEntityAsync(groupID, true, CancellationToken.None);
                    }

                    // 额外恢复玩家“旧组”中的建筑
                    _ = BarricadeSaverLogic.LoadAndPlaceBarricadesForOldGroupsAsync(steamID, CancellationToken.None);
                }
            }
            else
            {
                // 个人模式
                _ = BarricadeSaverLogic.LoadAndPlaceBarricadesForEntityAsync(steamID, false, CancellationToken.None);
            }
        }

        private void OnPlayerDisconnected(UnturnedPlayer player)
        {
            ulong steamID = player.CSteamID.m_SteamID;
            var steamPlayer = player.SteamPlayer();

            if (MainConfig.UseGroupID)
            {
                ulong groupID = steamPlayer.player.quests.groupID.m_SteamID;

                if (groupID == 0)
                {
                    _ = BarricadeSaverLogic.RemoveAndSaveBarricadesForEntityAsync(steamID, false);
                }
                else
                {
                    // 对有组的玩家，延迟1秒判断组内是否还有人在线
                    EffectManager.instance.StartCoroutine(DelayCheckGroupAndSave(groupID, steamPlayer));
                }
            }
            else
            {
                _ = BarricadeSaverLogic.RemoveAndSaveBarricadesForEntityAsync(steamID, false);
            }
        }

        private IEnumerator DelayCheckGroupAndSave(ulong groupID, SteamPlayer disconnectingPlayer)
        {
            // 等待 1 秒
            yield return new WaitForSeconds(1f);

            // 一秒后再判断同组在线玩家
            bool anyOnline = false;
            foreach (var sp in Provider.clients)
            {
                if (sp == disconnectingPlayer) continue;
                if (sp.player.quests.groupID.m_SteamID == groupID)
                {
                    anyOnline = true;
                    break;
                }
            }

            // 如果无人在线，则保存并移除该组 Barricade
            if (!anyOnline)
            {
                _ = BarricadeSaverLogic.RemoveAndSaveBarricadesForEntityAsync(groupID, true);
            }
        }

        /// <summary>
        /// 放置Barricade时，检查是否超过限制
        /// </summary>
        private void OnDeployBarricadeRequested(
            Barricade barricade,
            ItemBarricadeAsset asset,
            Transform hit,
            ref Vector3 point,
            ref float angle_x,
            ref float angle_y,
            ref float angle_z,
            ref ulong owner,
            ref ulong group,
            ref bool shouldAllow)
        {
            try
            {
                // 如果限制未启用
                if (!limitConfig.BasicSetting.Enabled)
                {
                    return;
                }

                UnturnedPlayer player = UnturnedPlayer.FromCSteamID(new CSteamID(owner));
                if (player == null) return;

                string barricadeType = Tool.GetBarricadeType(asset);

                // 取对应的上限
                int typeLimit = Tool.GetTypeLimitForPlayer(player, barricadeType);
                int allLimit = Tool.GetAllLimitForPlayer(player);

                // 是否要计入ALL
                bool countInAll = Tool.ShouldCountTypeInALLForPlayer(player, barricadeType);

                // 若既没有该类型限制，也没有ALL限制 => 不限
                if (typeLimit < 0 && (!countInAll || allLimit < 0))
                {
                    return;
                }

                // 当前已放置数量
                int currentTypeCount = Tool.GetPlayerCurrentCount(owner, barricadeType);
                int currentAllCount = Tool.GetPlayerCurrentCount(owner, "ALL");

                // 检查类型限制
                if (typeLimit >= 0 && currentTypeCount >= typeLimit)
                {
                    shouldAllow = false;
                    return;
                }

                // 检查ALL限制
                if (allLimit >= 0 && countInAll && currentAllCount >= allLimit)
                {
                    shouldAllow = false;
                    return;
                }

                // 放置允许 => 计数 +1
                if (typeLimit >= 0)
                {
                    Tool.IncrementPlayerCount(owner, barricadeType);
                    currentTypeCount++;
                }
                if (allLimit >= 0 && countInAll)
                {
                    Tool.IncrementPlayerCount(owner, "ALL");
                    currentAllCount++;
                }

                // 计算最终要显示在提示里的上限
                int effectiveLimit;
                int effectiveCount;

                /*
                 * 逻辑说明：
                 * - 如果该类型有限制(typeLimit&ge;0)且ALL也有限制(allLimit&ge;0)，并且需要计入ALL(countInAll=true)，
                 *   则取二者的最小值作为“最终限制”，并根据是哪一个限制更小来确定当前计数以哪一个为准。
                 * - 如果只有typeLimit，则显示typeLimit。
                 * - 如果只有allLimit(且countInAll)，则显示allLimit。
                 * - 如果都没有 => -1 (再转为&infin;)
                 */
                if (typeLimit >= 0 && allLimit >= 0 && countInAll)
                {
                    effectiveLimit = Math.Min(typeLimit, allLimit);
                    if (effectiveLimit == typeLimit)
                    {
                        effectiveCount = currentTypeCount;
                    }
                    else
                    {
                        effectiveCount = currentAllCount;
                    }
                }
                else if (typeLimit >= 0)
                {
                    effectiveLimit = typeLimit;
                    effectiveCount = currentTypeCount;
                }
                else if (allLimit >= 0 && countInAll)
                {
                    effectiveLimit = allLimit;
                    effectiveCount = currentAllCount;
                }
                else
                {
                    effectiveLimit = -1;
                    effectiveCount = currentTypeCount;
                }

                string msgTemplate = limitConfig.BasicSetting.PlacementMessage;
                string limitStr = (effectiveLimit < 0) ? "&infin;" : effectiveLimit.ToString();

                string finalMessage = msgTemplate
                    .Replace("{Type}", barricadeType)
                    .Replace("{Current}", effectiveCount.ToString())
                    .Replace("{Limit}", limitStr);

                ChatManager.serverSendMessage(
                    finalMessage,
                    UnityEngine.Color.white,
                    null,
                    player.SteamPlayer(),
                    EChatMode.SAY,
                    limitConfig.BasicSetting.PlacementIconURL,
                    true
                );
            }
            catch (Exception ex)
            {
                Rocket.Core.Logging.Logger.LogError($"[BarricadeSaver] [OnDeployBarricadeRequested] 异常: {ex}");
            }
        }

        /// <summary>
        /// 回收(拆除)Barricade时触发
        /// </summary>
        private void BarricadeDrop_OnSalvageRequested_Global(
            BarricadeDrop barricade,
            SteamPlayer instigatorClient,
            ref bool shouldAllow)
        {
            try
            {
                if (!shouldAllow) return;

                var data = barricade.GetServersideData();
                if (data == null) return;

                var asset = barricade.asset as ItemBarricadeAsset;
                if (asset == null) return;

                string type = Tool.GetBarricadeType(asset);
                ulong owner = data.owner;

                // 计数 -1
                Tool.DecrementPlayerCount(owner, type);

                UnturnedPlayer player = UnturnedPlayer.FromCSteamID(new CSteamID(owner));
                if (Tool.ShouldCountTypeInALLForPlayer(player, type))
                {
                    Tool.DecrementPlayerCount(owner, "ALL");
                }

                salvageDestroyedBarricades.Add(data.instanceID);
            }
            catch (Exception ex)
            {
            }
        }
    }
}