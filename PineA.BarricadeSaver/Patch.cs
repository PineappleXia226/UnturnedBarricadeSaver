using HarmonyLib;
using Rocket.Unturned.Player;
using SDG.Framework.Utilities;
using SDG.Unturned;
using Steamworks;
using System;
using UnityEngine;

namespace PineA.BarricadeSaver
{
    [HarmonyPatch(typeof(BarricadeManager))]
    [HarmonyPatch("destroyBarricade", new[] { typeof(BarricadeDrop), typeof(byte), typeof(byte), typeof(ushort) })]
    public static class BarricadeDestroyedPatch
    {
        [HarmonyPostfix]
        public static void Postfix(BarricadeDrop barricade, byte x, byte y, ushort plant)
        {
            if (Main.Instance == null) return;

            var data = barricade.GetServersideData();
            if (data == null) return;

            // 如果是在回收事件中做过 -1，就跳过
            if (Main.Instance.salvageDestroyedBarricades.Contains(data.instanceID))
            {
                Main.Instance.salvageDestroyedBarricades.Remove(data.instanceID);
                return;
            }

            // 否则说明是非回收的摧毁(爆炸、管理员等)，要在这里扣减一次
            var asset = barricade.asset as ItemBarricadeAsset;
            if (asset == null) return;

            string type = Tool.GetBarricadeType(asset);
            ulong owner = data.owner;

            Tool.DecrementPlayerCount(owner, type);

            UnturnedPlayer player = UnturnedPlayer.FromCSteamID(new CSteamID(owner));
            if (Tool.ShouldCountTypeInALLForPlayer(player, type))
            {
                Tool.DecrementPlayerCount(owner, "ALL");
            }
        }
    }
}