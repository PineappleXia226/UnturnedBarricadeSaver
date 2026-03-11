using Rocket.Unturned;
using Rocket.Unturned.Player;
using SDG.Unturned;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Logger = Rocket.Core.Logging.Logger;

namespace PineA.BarricadeSaver
{
    /// <summary>
    /// 将插件中的主要“保存、加载、移除、碰撞检测”等逻辑方法，拆分到此静态类中。
    /// </summary>
    public static class BarricadeSaverLogic
    {
        /// <summary>
        /// 在服务器地图加载完成后，扫描全地图，将可存进DB的Barricade全部移除并存储到DB
        /// </summary>
        public static void SaveAndRemoveAllQualifiedBarricades()
        {
            Logger.Log("[BarricadeSaver] [SaveAndRemoveAllQualifiedBarricades] 开始全地图扫描并保存移除...");

            int totalCount = 0;
            for (byte x = 0; x < Regions.WORLD_SIZE; x++)
            {
                for (byte y = 0; y < Regions.WORLD_SIZE; y++)
                {
                    var region = BarricadeManager.regions[x, y];
                    if (region == null || region.drops.Count == 0) continue;

                    // 拷贝一份列表
                    var regionDrops = new List<BarricadeDrop>(region.drops);
                    foreach (var drop in regionDrops)
                    {
                        var data = Tool.ConvertToDatabaseData(drop, x, y, ushort.MaxValue);
                        if (data == null) continue;

                        if (Tool.ShouldHandleBarricade(data))
                        {
                            data.SavedTime = DateTime.Now;
                            // 异步存DB，这里为了保证同步顺序，直接 GetAwaiter().GetResult()
                            DatabaseManager.SaveBarricadeDataAsync(data).GetAwaiter().GetResult();

                            // 如果是储物柜/储物箱，则先清空物品，防止掉落
                            if (drop.interactable is InteractableStorage storage)
                            {
                                storage.items.clear();
                            }

                            // 最后销毁
                            BarricadeManager.destroyBarricade(drop, x, y, ushort.MaxValue);
                            totalCount++;
                        }
                    }
                }
            }

            Logger.Log($"[BarricadeSaver] [SaveAndRemoveAllQualifiedBarricades] 完成，全地图共移除并保存 {totalCount} 个Barricade。");
        }

        /// <summary>
        /// 清空内存计数，然后遍历地图上剩余的Barricade（未被存入DB的），为其所属玩家或组计数+1
        /// </summary>
        public static void RecountAllOnMap()
        {
            var main = Main.Instance;
            main.PlayerBarricadeCounts.Clear();

            int totalCount = 0;

            for (byte x = 0; x < Regions.WORLD_SIZE; x++)
            {
                for (byte y = 0; y < Regions.WORLD_SIZE; y++)
                {
                    var region = BarricadeManager.regions[x, y];
                    if (region == null) continue;

                    foreach (var drop in region.drops)
                    {
                        var data = drop.GetServersideData();
                        if (data == null) continue;

                        ulong owner = data.owner;
                        var asset = drop.asset as ItemBarricadeAsset;
                        if (asset == null) continue;

                        // 获取类型并 +1
                        string barricadeType = Tool.GetBarricadeType(asset);
                        Tool.IncrementPlayerCount(owner, barricadeType);

                        // 如果要计入ALL则 +1
                        var pl = UnturnedPlayer.FromCSteamID(new CSteamID(owner));
                        if (pl != null && Tool.ShouldCountTypeInALLForPlayer(pl, barricadeType))
                        {
                            Tool.IncrementPlayerCount(owner, "ALL");
                        }

                        totalCount++;
                    }
                }
            }

            Logger.Log($"[BarricadeSaver] [RecountAllOnMap] 完成，对地图上的 {totalCount} 个Barricade进行了内存计数。");
        }

        /// <summary>
        /// 异步方法：从DB加载某个实体(steamID / groupID)的Barricade并生成到地图
        /// </summary>
        public static async Task LoadAndPlaceBarricadesForEntityAsync(ulong entityID, bool isGroup, CancellationToken token)
        {
            try
            {
                var main = Main.Instance;

                // 如果是个人模式且在 NotSaveSteamID 列表中，则不处理
                if (!isGroup && main.MainConfig.NotSaveSteamID.Contains(entityID))
                {
                    return;
                }

                bool useSpaceCheck = main.MainConfig.UseSpaceCheck;
                List<PineA_BarricadeData> placedSuccessfully = new List<PineA_BarricadeData>();
                List<PineA_BarricadeData> failedBarricades = new List<PineA_BarricadeData>();

                // 从数据库加载
                List<PineA_BarricadeData> barricades = isGroup
                    ? await DatabaseManager.LoadBarricadesByGroupAsync(entityID)
                    : await DatabaseManager.LoadBarricadesByOwnerAsync(entityID);

                if (barricades == null)
                {
                    return;
                }

                // 避免同一个实体重复生成
                if (main.entityBarricadeSpawned.Contains(entityID))
                {
                    return;
                }

                // 缓存离线时间
                Dictionary<ulong, DateTime?> lastOnlineCache = new Dictionary<ulong, DateTime?>();

                foreach (var item in barricades)
                {
                    if (token.IsCancellationRequested)
                        return;

                    // 黑白名单等判断
                    if (!Tool.ShouldHandleBarricade(item))
                        continue;

                    // 离线时长判断
                    if (!lastOnlineCache.TryGetValue(item.Owner, out DateTime? lastOnline))
                    {
                        lastOnline = await LastOnlineTimeManager.GetLastOnlineTimeAsync(item.Owner);
                        lastOnlineCache[item.Owner] = lastOnline;
                    }
                    if (lastOnline.HasValue)
                    {
                        double offlineDays = (DateTime.Now - lastOnline.Value).TotalDays;
                        if (offlineDays > main.MainConfig.NotRestoreIfOfflineDays)
                        {
                            failedBarricades.Add(item);
                            continue;
                        }
                    }

                    // 碰撞检测
                    if (useSpaceCheck && CheckPlacementBlocked(item, out string _))
                    {
                        failedBarricades.Add(item);
                        continue;
                    }

                    // 放置
                    var placedTransform = BarricadeHelper.PlaceBarricade(item);
                    if (placedTransform == null)
                    {
                        failedBarricades.Add(item);
                        continue;
                    }

                    placedSuccessfully.Add(item);

                    // 内存计数 +1
                    var asset = SDG.Unturned.Assets.find(EAssetType.ITEM, item.BarricadeItemID) as ItemBarricadeAsset;
                    if (asset != null)
                    {
                        string type = Tool.GetBarricadeType(asset);
                        Tool.IncrementPlayerCount(item.Owner, type);

                        var player = UnturnedPlayer.FromCSteamID(new CSteamID(item.Owner));
                        if (player != null && Tool.ShouldCountTypeInALLForPlayer(player, type))
                        {
                            Tool.IncrementPlayerCount(item.Owner, "ALL");
                        }

                        // 如果是农作物，则补偿生长
                        if (asset.build == EBuild.FARM)
                        {
                            UpdateFarmGrowth(placedTransform, item);
                        }
                    }
                }

                // 从DB删除已成功放置的记录
                if (!token.IsCancellationRequested && placedSuccessfully.Count > 0)
                {
                    if (isGroup)
                        await DatabaseManager.DeleteBarricadesByGroupAsync(placedSuccessfully);
                    else
                        await DatabaseManager.DeleteBarricadesByOwnerAsync(placedSuccessfully);
                }

                // 提示消息
                if (!token.IsCancellationRequested)
                {
                    main.entityBarricadeSpawned.Add(entityID);

                    if (isGroup)
                    {
                        // 组模式下分个人和组统计
                        foreach (var sp in Provider.clients)
                        {
                            ulong checkGroupID = sp.playerID.group.m_SteamID;
                            if (checkGroupID != entityID)
                                continue;

                            ulong playerSteam = sp.playerID.steamID.m_SteamID;
                            int personal_success = placedSuccessfully.Count(item => item.Owner == playerSteam);
                            int group_success = placedSuccessfully.Count - personal_success;
                            int personal_fail = failedBarricades.Count(item => item.Owner == playerSteam);
                            int group_fail = failedBarricades.Count - personal_fail;

                            string message;
                            if (failedBarricades.Count > 0)
                            {
                                message = main.MainConfig.Message_RestorePartial_Group
                                    .Replace("{personal_success}", personal_success.ToString())
                                    .Replace("{group_success}", group_success.ToString())
                                    .Replace("{personal_fail}", personal_fail.ToString())
                                    .Replace("{group_fail}", group_fail.ToString());
                            }
                            else
                            {
                                message = main.MainConfig.Message_RestoreAll_Group
                                    .Replace("{personal_success}", personal_success.ToString())
                                    .Replace("{group_success}", group_success.ToString());
                            }

                            ChatManager.serverSendMessage(
                                message,
                                (failedBarricades.Count > 0) ? Color.yellow : Color.white,
                                null,
                                sp,
                                EChatMode.SAY,
                                main.MainConfig.MessageIconURL,
                                true
                            );
                        }
                    }
                    else
                    {
                        // 个人模式
                        foreach (var sp in Provider.clients)
                        {
                            ulong checkSteamID = sp.playerID.steamID.m_SteamID;
                            if (checkSteamID != entityID)
                                continue;

                            if (failedBarricades.Count > 0)
                            {
                                string msgPartial = main.MainConfig.Message_RestorePartial
                                    .Replace("{success}", placedSuccessfully.Count.ToString())
                                    .Replace("{fail}", failedBarricades.Count.ToString());
                                ChatManager.serverSendMessage(
                                    msgPartial,
                                    Color.yellow,
                                    null,
                                    sp,
                                    EChatMode.SAY,
                                    main.MainConfig.MessageIconURL,
                                    true
                                );
                            }
                            else
                            {
                                string msgAllSuccess = main.MainConfig.Message_RestoreAll
                                    .Replace("{success}", placedSuccessfully.Count.ToString());
                                ChatManager.serverSendMessage(
                                    msgAllSuccess,
                                    Color.white,
                                    null,
                                    sp,
                                    EChatMode.SAY,
                                    main.MainConfig.MessageIconURL,
                                    true
                                );
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
                // 忽略异常
            }
        }

        /// <summary>
        /// 异步方法：将地图上属于某个实体(steamID / groupID)的Barricade保存到DB并移除
        /// </summary>
        public static async Task RemoveAndSaveBarricadesForEntityAsync(ulong entityID, bool isGroup)
        {
            try
            {
                var main = Main.Instance;

                // 如果是个人模式且在 NotSaveSteamID 里，就跳过
                if (!isGroup && main.MainConfig.NotSaveSteamID.Contains(entityID))
                {
                    return;
                }

                int totalRemoved = 0;
                for (byte x = 0; x < Regions.WORLD_SIZE; x++)
                {
                    for (byte y = 0; y < Regions.WORLD_SIZE; y++)
                    {
                        var region = BarricadeManager.regions[x, y];
                        if (region == null || region.drops.Count == 0)
                            continue;

                        var regionDrops = new List<BarricadeDrop>(region.drops);
                        foreach (var drop in regionDrops)
                        {
                            var sData = drop.GetServersideData();
                            if (sData == null)
                                continue;

                            // 区分是否group模式
                            if (isGroup)
                            {
                                if (sData.group != entityID)
                                    continue;
                            }
                            else
                            {
                                if (sData.owner != entityID)
                                    continue;
                            }

                            var data = Tool.ConvertToDatabaseData(drop, x, y, ushort.MaxValue);
                            if (data == null)
                                continue;

                            if (Tool.ShouldHandleBarricade(data))
                            {
                                data.SavedTime = DateTime.Now;
                                await DatabaseManager.SaveBarricadeDataAsync(data);

                                // 如果是储物，则先清空物品，避免掉落
                                if (drop.interactable is InteractableStorage storage2)
                                {
                                    storage2.items.clear();
                                }

                                // 销毁
                                BarricadeManager.destroyBarricade(drop, x, y, ushort.MaxValue);
                                totalRemoved++;
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
                // 忽略异常
            }
            finally
            {
                // 移除该实体的“已生成”标记
                var main = Main.Instance;
                if (main.entityBarricadeSpawned.Contains(entityID))
                {
                    main.entityBarricadeSpawned.Remove(entityID);
                }
            }
        }

        /// <summary>
        /// 农作物离线生长补偿
        /// </summary>
        private static void UpdateFarmGrowth(Transform placedTransform, PineA_BarricadeData data)
        {
            var main = Main.Instance;
            if (!main.MainConfig.GrowWhenSaved) return;

            InteractableFarm farm = placedTransform.GetComponent<InteractableFarm>();
            if (farm == null) return;

            BarricadeDrop drop = BarricadeManager.FindBarricadeByRootTransform(placedTransform);
            if (drop == null) return;

            var serversideData = drop.GetServersideData();
            if (serversideData == null) return;

            byte[] st = serversideData.barricade.state;
            if (st.Length < 4) return;

            float offlineSeconds = (float)(DateTime.Now - data.SavedTime).TotalSeconds;
            if (offlineSeconds <= 0) return;

            uint oldPlanted = BitConverter.ToUInt32(st, 0);
            uint adjust = (uint)Mathf.RoundToInt(offlineSeconds);
            uint newPlanted = (oldPlanted > adjust) ? (oldPlanted - adjust) : 0;

            BitConverter.GetBytes(newPlanted).CopyTo(st, 0);
            BarricadeManager.updateFarm(placedTransform, newPlanted, shouldSend: true);
        }

        /// <summary>
        /// 离线放置时做的空间碰撞检测，用以防止因重叠导致的不可用情况
        /// </summary>
        public static bool CheckPlacementBlocked(
            PineA_BarricadeData data,
            out string reason)
        {
            reason = null;

            // 找到 ItemBarricadeAsset
            var rawAsset = Assets.find(EAssetType.ITEM, data.BarricadeItemID);
            var barricadeAsset = rawAsset as ItemBarricadeAsset;
            if (barricadeAsset == null)
            {
                reason = $"未找到BarricadeAsset(id={data.BarricadeItemID})";
                return true; // 阻挡
            }

            EBuild buildType = barricadeAsset.build;
            float radius = barricadeAsset.radius;
            float offset = barricadeAsset.offset;
            Quaternion rotation = data.Rotation;
            Vector3 finalPoint = data.Position;

            int maskBarricade = RayMasks.BLOCK_BARRICADE;
            int maskBuildableOverlap = RayMasks.BLOCK_CHAR_BUILDABLE_OVERLAP;

            Collider[] hits = new Collider[30];

            bool CheckOverlapSphere(float extraRadius = 0f)
            {
                float checkRadius = radius + extraRadius;
                int count = Physics.OverlapSphereNonAlloc(finalPoint, checkRadius, hits, maskBarricade);
                if (count > 0)
                {
                    return true;
                }
                return false;
            }

            bool CheckDoorLikeOverlapBox()
            {
                GameObject helper = null;
                try
                {
                    if (barricadeAsset.barricade != null)
                    {
                        helper = UnityEngine.Object.Instantiate(barricadeAsset.barricade, Vector3.zero, Quaternion.identity);
                        Transform placeholder = helper.transform.Find("Placeholder");
                        Collider mainCollider = placeholder != null
                            ? placeholder.GetComponent<Collider>()
                            : helper.GetComponentInChildren<Collider>();

                        if (mainCollider != null)
                        {
                            Vector3 localCenter = helper.transform.InverseTransformPoint(mainCollider.bounds.center);
                            Vector3 localExtents = mainCollider.bounds.extents;

                            Vector3 overlapExtents = localExtents + new Vector3(0.2f, 0.5f, 0.5f);
                            Vector3 worldCenter = finalPoint + rotation * localCenter;

                            int count = Physics.OverlapBoxNonAlloc(
                                worldCenter,
                                overlapExtents,
                                hits,
                                rotation,
                                maskBuildableOverlap,
                                QueryTriggerInteraction.Collide
                            );
                            if (count > 0)
                            {
                                return true;
                            }
                        }
                    }
                }
                finally
                {
                    if (helper != null)
                        UnityEngine.Object.Destroy(helper);
                }
                return false;
            }

            // 根据官方对不同类型的放置检测做不同处理
            switch (buildType)
            {
                // 门/闸/百叶等：OverlapBox
                case EBuild.DOOR:
                case EBuild.GATE:
                case EBuild.SHUTTER:
                case EBuild.HATCH:
                    return CheckDoorLikeOverlapBox();

                // 床特殊
                case EBuild.BED:
                    {
                        float extra = 0.5f;
                        int count = Physics.OverlapSphereNonAlloc(finalPoint + Vector3.up, radius + extra, hits, maskBarricade);
                        if (count > 0)
                        {
                            reason = $"床检测到 {count} 个碰撞体";
                            return true;
                        }
                        break;
                    }

                // 大部分用 OverlapSphere
                case EBuild.GLASS:
                case EBuild.CAMPFIRE:
                case EBuild.OVEN:
                case EBuild.SAFEZONE:
                case EBuild.OXYGENATOR:
                case EBuild.GENERATOR:
                case EBuild.SPIKE:
                case EBuild.TANK:
                case EBuild.VEHICLE:
                case EBuild.CLAIM:
                case EBuild.MANNEQUIN:
                case EBuild.LIBRARY:
                case EBuild.BARREL_RAIN:
                case EBuild.SPOT:
                case EBuild.SIGN:
                case EBuild.SIGN_WALL:
                case EBuild.STORAGE_WALL:
                case EBuild.SENTRY:
                case EBuild.STEREO:
                case EBuild.BARRICADE_WALL:
                case EBuild.CAGE:
                case EBuild.WIRE:
                case EBuild.OIL:
                case EBuild.FORTIFICATION:
                case EBuild.SENTRY_FREEFORM:
                case EBuild.STORAGE:
                case EBuild.BARRICADE:
                case EBuild.BEACON:
                case EBuild.FREEFORM:
                case EBuild.TORCH:
                    return CheckOverlapSphere(0.2f);

                // 农作物
                case EBuild.FARM:
                    return CheckOverlapSphere(0.1f);

                // 梯子
                case EBuild.LADDER:
                    return CheckOverlapSphere(0.2f);

                // 小物件
                case EBuild.CHARGE:
                case EBuild.CLOCK:
                case EBuild.NOTE:
                    return CheckOverlapSphere(0.2f);

                default:
                    return CheckOverlapSphere(0.2f);
            }

            // 默认无碰撞
            return false;
        }

        /// <summary>
        /// 异步方法：恢复玩家在“旧组”中的建筑（仍保持其原Group，不改写）。
        /// </summary>
        public static async Task LoadAndPlaceBarricadesForOldGroupsAsync(ulong steamID, CancellationToken token)
        {
            try
            {
                var main = Main.Instance;
                if (main.MainConfig.NotSaveSteamID.Contains(steamID))
                    return;

                // 获取该玩家所有的数据库记录（不分组）
                List<PineA_BarricadeData> allBarricades = await DatabaseManager.LoadBarricadesByOwnerAsync(steamID);
                if (allBarricades == null || allBarricades.Count == 0)
                    return;

                // 当前组
                UnturnedPlayer pl = UnturnedPlayer.FromCSteamID(new CSteamID(steamID));
                ulong currentGroup = (pl != null) ? pl.SteamPlayer().player.quests.groupID.m_SteamID : 0;

                // 筛选：数据里 group != currentGroup 即“旧组”
                var oldGroupBarricades = allBarricades
                    .Where(b => b.Group != currentGroup)
                    .ToList();

                if (oldGroupBarricades.Count == 0)
                    return;

                bool useSpaceCheck = main.MainConfig.UseSpaceCheck;
                List<PineA_BarricadeData> placedSuccessfully = new List<PineA_BarricadeData>();
                List<PineA_BarricadeData> failedBarricades = new List<PineA_BarricadeData>();

                // 离线时间缓存
                Dictionary<ulong, DateTime?> lastOnlineCache = new Dictionary<ulong, DateTime?>();

                foreach (var item in oldGroupBarricades)
                {
                    if (token.IsCancellationRequested) return;

                    if (!Tool.ShouldHandleBarricade(item))
                    {
                        failedBarricades.Add(item);
                        continue;
                    }

                    if (!lastOnlineCache.TryGetValue(item.Owner, out DateTime? lastOnline))
                    {
                        lastOnline = await LastOnlineTimeManager.GetLastOnlineTimeAsync(item.Owner);
                        lastOnlineCache[item.Owner] = lastOnline;
                    }
                    if (lastOnline.HasValue)
                    {
                        double offlineDays = (DateTime.Now - lastOnline.Value).TotalDays;
                        if (offlineDays > main.MainConfig.NotRestoreIfOfflineDays)
                        {
                            failedBarricades.Add(item);
                            continue;
                        }
                    }

                    if (useSpaceCheck && CheckPlacementBlocked(item, out string _))
                    {
                        failedBarricades.Add(item);
                        continue;
                    }

                    // 直接使用原Group
                    var placedTransform = BarricadeHelper.PlaceBarricade(item);
                    if (placedTransform == null)
                    {
                        failedBarricades.Add(item);
                        continue;
                    }

                    placedSuccessfully.Add(item);

                    // 内存计数 +1
                    var asset = SDG.Unturned.Assets.find(EAssetType.ITEM, item.BarricadeItemID) as ItemBarricadeAsset;
                    if (asset != null)
                    {
                        string type = Tool.GetBarricadeType(asset);
                        Tool.IncrementPlayerCount(item.Owner, type);

                        var thisOwner = UnturnedPlayer.FromCSteamID(new CSteamID(item.Owner));
                        if (thisOwner != null && Tool.ShouldCountTypeInALLForPlayer(thisOwner, type))
                        {
                            Tool.IncrementPlayerCount(item.Owner, "ALL");
                        }

                        if (asset.build == EBuild.FARM)
                        {
                            UpdateFarmGrowth(placedTransform, item);
                        }
                    }
                }

                // 从数据库中删除已成功放置的记录
                if (!token.IsCancellationRequested && placedSuccessfully.Count > 0)
                {
                    await DatabaseManager.DeleteBarricadesByOwnerAsync(placedSuccessfully);
                }

                // 给玩家发提示
                if (!token.IsCancellationRequested && pl != null)
                {
                    var spOnline = pl.SteamPlayer();
                    if (spOnline != null)
                    {
                        if (failedBarricades.Count > 0)
                        {
                            string msgPartial = main.MainConfig.Message_RestorePartial
                                .Replace("{success}", placedSuccessfully.Count.ToString())
                                .Replace("{fail}", failedBarricades.Count.ToString());
                            ChatManager.serverSendMessage(
                                msgPartial,
                                Color.yellow,
                                null,
                                spOnline,
                                EChatMode.SAY,
                                main.MainConfig.MessageIconURL,
                                true
                            );
                        }
                        else if (placedSuccessfully.Count > 0)
                        {
                            string msgAll = main.MainConfig.Message_RestoreAll
                                .Replace("{success}", placedSuccessfully.Count.ToString());
                            ChatManager.serverSendMessage(
                                msgAll,
                                Color.white,
                                null,
                                spOnline,
                                EChatMode.SAY,
                                main.MainConfig.MessageIconURL,
                                true
                            );
                        }
                    }
                }
            }
            catch
            {
                // 不记录日志
            }
        }
    }
}