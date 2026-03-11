using SDG.Unturned;
using UnityEngine;

namespace PineA.BarricadeSaver
{
    public static class BarricadeHelper
    {
        /// <summary>
        /// 从数据库数据还原并在地图上放置一个Barricade。
        /// </summary>
        /// <param name="data">数据库里存的Barricade字段</param>
        /// <returns>返回放置后对应的Transform，如果失败则null</returns>
        public static Transform PlaceBarricade(PineA_BarricadeData data)
        {
            // 1. 找到Barricade的资源 (ItemBarricadeAsset)
            var asset = Assets.find(EAssetType.ITEM, data.BarricadeItemID) as ItemBarricadeAsset;
            if (asset == null)
            {
                return null;
            }

            // 2. 构造位置 & 旋转
            Vector3 position = data.Position;
            Quaternion rotation = data.Rotation;

            // 3. 基础Barricade
            Barricade barricade = new Barricade(asset);
            // 如果State不为空，则还原；否则使用默认
            if (data.State != null && data.State.Length > 0)
                barricade.state = data.State;
            else
                barricade.state = asset.getState(EItemOrigin.ADMIN);

            // 4. 构造服务器所需的 BarricadeData
            BarricadeData serversideData = new BarricadeData(
                barricade,
                position,
                rotation,
                data.Owner,
                data.Group,
                0,  // serverInstanceID
                0   // slot
            )
            {
                barricade = { health = (ushort)data.Health }
            };

            // 5. 放置到地图上
            Transform placedTransform = BarricadeManager.dropNonPlantedBarricade(
                serversideData.barricade,
                position,
                rotation,
                data.Owner,
                data.Group
            );
            if (placedTransform == null)
            {
                return null;
            }

            // 6. 同步一下Serverside Data (可选，尤其是State/health等字段)
            Regions.tryGetCoordinate(position, out byte x, out byte y);
            var region = BarricadeManager.regions[x, y];
            var foundDrop = region.FindBarricadeByRootTransform(placedTransform);
            if (foundDrop != null)
            {
                var sd = foundDrop.GetServersideData();
                if (sd != null)
                {
                    sd.barricade.health = (ushort)data.Health;

                    if (data.State != null && data.State.Length > 0)
                    {
                        sd.barricade.state = data.State;
                        BarricadeManager.updateReplicatedState(placedTransform, sd.barricade.state, sd.barricade.state.Length);
                    }

                    sd.owner = data.Owner;
                    sd.group = data.Group;
                }
            }

            return placedTransform;
        }
    }
}