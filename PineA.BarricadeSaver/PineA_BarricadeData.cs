using UnityEngine;
using System;

namespace PineA.BarricadeSaver
{
    /// <summary>
    /// 该类用于在数据库中读写 Barricade 的信息。
    /// </summary>
    public class PineA_BarricadeData
    {
        /// <summary>
        /// 数据库自增主键 (与表中的 ID 字段对应)
        /// </summary>
        public int ID { get; set; }

        /// <summary>唯一实例ID，用于识别同一个Barricade实体。</summary>
        public uint InstanceID { get; set; }

        /// <summary>Barricade物品的短ID(ushort)或其他标识；若使用GUID则可改成string。</summary>
        public ushort BarricadeItemID { get; set; }

        /// <summary>拥有者的SteamID。</summary>
        public ulong Owner { get; set; }

        /// <summary>所属Group的ID。</summary>
        public ulong Group { get; set; }

        /// <summary>当前血量（或耐久度）</summary>
        public float Health { get; set; }

        /// <summary>Barricade的State字节流(存储内部物品等)。</summary>
        public byte[] State { get; set; }

        /// <summary>世界坐标。</summary>
        public Vector3 Position { get; set; }

        /// <summary>世界旋转。</summary>
        public Quaternion Rotation { get; set; }

        /// <summary>
        /// 本次保存的时间(数据库字段: SavedTime)，用于比较是否超过不恢复天数。
        /// </summary>
        public DateTime SavedTime { get; set; }
    }
}