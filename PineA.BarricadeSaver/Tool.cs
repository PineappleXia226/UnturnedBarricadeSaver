using Rocket.Unturned.Player;
using SDG.Unturned;
using Steamworks;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace PineA.BarricadeSaver
{
    public static class Tool
    {
        /// <summary>
        /// 根据 BarricadeAsset 的 build 属性，返回对应的字符串名称。
        /// 如果没有匹配上就返回 "UNKNOWN"。
        /// </summary>
        public static string GetBarricadeType(ItemBarricadeAsset asset)
        {
            if (asset == null)
                return "UNKNOWN";

            switch (asset.build)
            {
                case EBuild.FORTIFICATION: return "FORTIFICATION";
                case EBuild.BARRICADE: return "BARRICADE";
                case EBuild.DOOR: return "DOOR";
                case EBuild.GATE: return "GATE";
                case EBuild.BED: return "BED";
                case EBuild.LADDER: return "LADDER";
                case EBuild.STORAGE: return "STORAGE";
                case EBuild.FARM: return "FARM";
                case EBuild.TORCH: return "TORCH";
                case EBuild.CAMPFIRE: return "CAMPFIRE";
                case EBuild.SPIKE: return "SPIKE";
                case EBuild.WIRE: return "WIRE";
                case EBuild.GENERATOR: return "GENERATOR";
                case EBuild.SPOT: return "SPOT";
                case EBuild.SAFEZONE: return "SAFEZONE";
                case EBuild.FREEFORM: return "FREEFORM";
                case EBuild.SIGN: return "SIGN";
                case EBuild.VEHICLE: return "VEHICLE";
                case EBuild.CLAIM: return "CLAIM";
                case EBuild.BEACON: return "BEACON";
                case EBuild.STORAGE_WALL: return "STORAGE_WALL";
                case EBuild.BARREL_RAIN: return "BARREL_RAIN";
                case EBuild.OIL: return "OIL";
                case EBuild.CAGE: return "CAGE";
                case EBuild.SHUTTER: return "SHUTTER";
                case EBuild.TANK: return "TANK";
                case EBuild.CHARGE: return "CHARGE";
                case EBuild.SENTRY: return "SENTRY";
                case EBuild.SENTRY_FREEFORM: return "SENTRY_FREEFORM";
                case EBuild.OVEN: return "OVEN";
                case EBuild.LIBRARY: return "LIBRARY";
                case EBuild.OXYGENATOR: return "OXYGENATOR";
                case EBuild.GLASS: return "GLASS";
                case EBuild.NOTE: return "NOTE";
                case EBuild.HATCH: return "HATCH";
                case EBuild.MANNEQUIN: return "MANNEQUIN";
                case EBuild.STEREO: return "STEREO";
                case EBuild.SIGN_WALL: return "SIGN_WALL";
                case EBuild.CLOCK: return "CLOCK";
                case EBuild.BARRICADE_WALL: return "BARRICADE_WALL";
                default:
                    return "UNKNOWN";
            }
        }
        /// <summary>
        /// 根据Barricade的短ID获取其类型字符串。
        /// 如果找不到对应的Asset，就返回 "UNKNOWN"。
        /// 最终会调用已有的 GetBarricadeType(ItemBarricadeAsset) 方法。
        /// </summary>
        public static string GetBarricadeTypeByID(ushort itemID)
        {
            // 1) 查找对应的 ItemBarricadeAsset
            var foundAsset = Assets.find(EAssetType.ITEM, itemID) as ItemBarricadeAsset;
            if (foundAsset == null)
            {
                Rocket.Core.Logging.Logger.Log($"[GetBarricadeTypeByID] 未找到Asset: itemID={itemID}, 返回 UNKNOWN");
                return "UNKNOWN";
            }

            // 2) 如果找到了，就调用你现有的 GetBarricadeType(...) 方法
            string type = GetBarricadeType(foundAsset);
            Rocket.Core.Logging.Logger.Log($"[GetBarricadeTypeByID] itemID={itemID} => type={type}");
            return type;
        }

        /// <summary>
        /// 判断某个物品短ID对应的Barricade是否为 FARM 类型。
        /// </summary>
        public static bool IsFarmAsset(ushort itemID)
        {
            ItemBarricadeAsset foundAsset = Assets.find(EAssetType.ITEM, itemID) as ItemBarricadeAsset;
            if (foundAsset == null)
                return false;
            return foundAsset.build == EBuild.FARM;
        }

        /// <summary>
        /// 将 BarricadeDrop 转为数据库对应的 PineA_BarricadeData。
        /// </summary>
        public static PineA_BarricadeData ConvertToDatabaseData(BarricadeDrop drop, byte x, byte y, ushort plant)
        {
            var asset = drop.asset as ItemBarricadeAsset;
            if (asset == null) return null;

            var serverData = drop.GetServersideData();
            if (serverData == null) return null;

            return new PineA_BarricadeData
            {
                InstanceID = 0,
                BarricadeItemID = asset.id,
                Owner = serverData.owner,
                Group = serverData.group,
                Health = serverData.barricade.health,
                State = serverData.barricade.state,
                Position = serverData.point,
                Rotation = serverData.rotation,
                SavedTime = DateTime.Now
            };
        }

        /// <summary>
        /// 判断是否应当保存/恢复的Barricade（比如黑白名单、NotSaveSteamID等）。
        /// </summary>
        public static bool ShouldHandleBarricade(PineA_BarricadeData data)
        {
            var cfg = Main.Instance.MainConfig;

            // 1) 如果该玩家在“不保存玩家”列表中 => 直接不处理
            if (cfg.NotSaveSteamID.Contains(data.Owner))
            {
                return false;
            }

            // 2) 如果在黑名单 => 不处理
            if (cfg.BarricadeBlacklist.Contains(data.BarricadeItemID))
            {
                return false;
            }

            // 3) 如果在白名单 => 直接处理
            if (cfg.BarricadeWhitelist.Contains(data.BarricadeItemID))
            {
                return true;
            }

            // 4) 到这里，说明该物品不在“不保存玩家”列表、不在黑名单，也不在白名单
            //    先检查配置文件里的 BarricadeType (如果有配置)
            string type = Tool.GetBarricadeTypeByID(data.BarricadeItemID);
            if (cfg.BarricadeType != null && cfg.BarricadeType.Count > 0)
            {
                // 如果类型不在配置文件中 => 不处理
                if (!cfg.BarricadeType.Contains(type))
                {
                    return false;
                }
                else
                {
                }
            }
            return true;
        }

        /// <summary>
        /// 查找 limitConfig 中所有 Type="ALL" 的限制，返回“最大”的Count值。 
        /// 如果玩家拥有对应的RequiredPermission，就以 limit.Count 为候选。
        /// 若没有可用限制，返回 -1 表示无限制。
        /// </summary>
        public static int GetAllLimitForPlayer(UnturnedPlayer player)
        {
            if (Main.Instance.limitConfig == null) return -1;
            int best = -1;
            foreach (var limit in Main.Instance.limitConfig.Limits)
            {
                if (!limit.Type.Equals("ALL", StringComparison.OrdinalIgnoreCase))
                    continue;

                var permList = new List<string>() { limit.RequiredPermission };
                bool hasPerm = Rocket.Core.R.Permissions.HasPermission(player, permList);
                if (!hasPerm) continue;

                if (limit.Count > best)
                    best = limit.Count;
            }
            return best;
        }

        /// <summary>
        /// 获取某个具体类型的限制（取匹配到的最大值）。
        /// </summary>
        public static int GetTypeLimitForPlayer(UnturnedPlayer player, string type)
        {
            if (Main.Instance.limitConfig == null) return -1;
            int best = -1;

            foreach (var limit in Main.Instance.limitConfig.Limits)
            {
                if (!limit.Type.Equals(type, StringComparison.OrdinalIgnoreCase))
                    continue;

                var permList = new List<string>() { limit.RequiredPermission };
                bool hasPerm = Rocket.Core.R.Permissions.HasPermission(player, permList);
                if (!hasPerm) continue;

                if (limit.Count > best)
                    best = limit.Count;
            }
            return best;
        }
        /// <summary>
        /// 仅根据配置中的 CountInToALL 来判断是否应将指定类型计入 ALL。
        /// 去掉了所有的权限判断逻辑。
        /// </summary>
        public static bool ShouldCountTypeInALLForPlayer(UnturnedPlayer player, string type)
        {
            if (Main.Instance.limitConfig == null) return false;

            bool foundSameType = false;

            // 1) 先在所有 Limits 中查找与该 type 相同的限制
            foreach (var limit in Main.Instance.limitConfig.Limits)
            {
                if (!limit.Type.Equals(type, StringComparison.OrdinalIgnoreCase))
                    continue;

                foundSameType = true;
                // 一旦找到相同类型，并且它配置了 CountInToALL = true，就返回true
                if (limit.CountInToALL)
                    return true;
            }

            // 2) 如果找到了同类型的限制，但没有任何一条写了 CountInToALL=true，则直接返回 false
            if (foundSameType)
                return false;

            // 3) 如果根本没找到同类型的限制，则看是否有 Type="ALL" 的限制
            foreach (var limit in Main.Instance.limitConfig.Limits)
            {
                if (!limit.Type.Equals("ALL", StringComparison.OrdinalIgnoreCase))
                    continue;

                // 如果 ALL 的条目里写了 CountInToALL=true，说明要把所有未单独列举的类型都算进 ALL
                if (limit.CountInToALL)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// 获取玩家当前已放置的某种类型数量 (仍旧以 steamID 为索引)
        /// </summary>
        public static int GetPlayerCurrentCount(ulong steamID, string type)
        {
            var dict = Main.Instance.PlayerBarricadeCounts;
            if (!dict.TryGetValue(steamID, out var typeDict))
                return 0;
            if (!typeDict.TryGetValue(type, out int count))
                return 0;
            return count;
        }

        /// <summary>
        /// 让玩家的某类型数量+1
        /// </summary>
        public static void IncrementPlayerCount(ulong steamID, string type)
        {
            var dict = Main.Instance.PlayerBarricadeCounts;
            if (!dict.TryGetValue(steamID, out var typeDict))
            {
                typeDict = new Dictionary<string, int>();
                dict[steamID] = typeDict;
            }
            if (!typeDict.ContainsKey(type))
            {
                typeDict[type] = 0;
            }
            typeDict[type]++;
        }

        /// <summary>
        /// 让玩家的某类型数量-1（不低于0）
        /// </summary>
        public static void DecrementPlayerCount(ulong steamID, string type)
        {
            var dict = Main.Instance.PlayerBarricadeCounts;
            if (!dict.TryGetValue(steamID, out var typeDict))
                return;
            if (!typeDict.ContainsKey(type))
                return;

            typeDict[type]--;
            if (typeDict[type] < 0)
            {
                typeDict[type] = 0;
            }
        }
    }
}