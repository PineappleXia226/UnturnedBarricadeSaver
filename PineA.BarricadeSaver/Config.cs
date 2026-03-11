using System.Collections.Generic;
using Rocket.API; // 关键：必须有这句

namespace PineA.BarricadeSaver
{
    /// <summary>
    /// 继承 IRocketPluginConfiguration，RocketMod 才能识别该类作为插件配置
    /// </summary>
    public class Config : IRocketPluginConfiguration
    {
        public string MySqlConnectionString { get; set; }
            = "Server=127.0.0.1;Port=3306;Database=myDB;Uid=user;Pwd=pass;";

        public string BarricadeTableName { get; set; } = "PineA_Barricade";
        public bool GrowWhenSaved { get; set; }
        public bool UseSpaceCheck { get; set; } = false;
        public bool UseGroupID { get; set; } = false;

        public List<string> BarricadeType { get; set; } = new List<string>();
        public List<ushort> BarricadeWhitelist { get; set; } = new List<ushort>();
        public List<ushort> BarricadeBlacklist { get; set; } = new List<ushort>();
        public List<ulong> NotSaveSteamID { get; set; } = new List<ulong>();
        public string MessageIconURL { get; set; }

        /// <summary>
        /// 单人模式恢复提示：部分恢复时的提示信息
        /// </summary>
        public string Message_RestorePartial { get; set; }
            = "<b><size=15><color=#FFFACD>[提示]放置物已复原：成功恢复<color=#FFFFFF> {success} </color>个，失败<color=#FFFFFF> {fail} </color>个</color></size></b>";

        /// <summary>
        /// 单人模式恢复提示：全部恢复成功时的提示信息
        /// </summary>
        public string Message_RestoreAll { get; set; }
            = "<b><size=15><color=#FFFACD>[提示]放置物已全部成功恢复，共<color=#FFFFFF> {success} </color>个</color></size></b>";

        /// <summary>
        /// 组模式恢复提示：部分恢复时的提示信息，区分个人与组的恢复数量
        /// </summary>
        public string Message_RestorePartial_Group { get; set; }
            = "<b><size=15><color=#FFFACD>[提示]组内建筑复原：个人恢复<color=#FFFFFF>{personal_success}</color>个，组恢复<color=#FFFFFF>{group_success}</color>个，个人失败<color=#FFFFFF>{personal_fail}</color>个，组失败<color=#FFFFFF>{group_fail}</color>个</color></size></b>";

        /// <summary>
        /// 组模式恢复提示：全部恢复成功时的提示信息，区分个人与组的恢复数量
        /// </summary>
        public string Message_RestoreAll_Group { get; set; }
            = "<b><size=15><color=#FFFACD>[提示]组内建筑全部复原：个人恢复<color=#FFFFFF>{personal_success}</color>个，组恢复<color=#FFFFFF>{group_success}</color>个</color></size></b>";

        /// <summary>
        /// 不恢复的离线天数阈值
        /// 当玩家再次上线时，若与障碍物保存时间间隔超过该天数，则不再恢复。
        /// </summary>
        public int NotRestoreIfOfflineDays { get; set; } = 7;

        /// <summary>
        /// IRocketPluginConfiguration 接口要求实现的 LoadDefaults 方法
        /// 在这里你可以设置默认值。
        /// </summary>
        public void LoadDefaults()
        {
            MySqlConnectionString = "Server=127.0.0.1;Port=3306;Database=myDB;Uid=user;Pwd=pass;";
            BarricadeTableName = "PineA_Barricade";
            GrowWhenSaved = true;
            UseSpaceCheck = true;
            UseGroupID = false;

            BarricadeType = new List<string>
            {
                "DOOR",
                "BED",
                "STORAGE",
                "FARM"
            };

            BarricadeWhitelist = new List<ushort>
            {
                288
            };

            BarricadeBlacklist = new List<ushort>
            {
                289
            };

            NotSaveSteamID = new List<ulong>
            {
                76561198020988945
            };

            NotRestoreIfOfflineDays = 7;
            MessageIconURL = "http://example.com";

            Message_RestorePartial = "<b><size=15><color=#FFFACD>[提示]放置物已复原：成功恢复<color=#FFFFFF> {success} </color>个，失败<color=#FFFFFF> {fail} </color>个</color></size></b>";
            Message_RestoreAll = "<b><size=15><color=#FFFACD>[提示]放置物已全部成功恢复，共<color=#FFFFFF> {success} </color>个</color></size></b>";

            Message_RestorePartial_Group = "<b><size=15><color=#FFFACD>[提示]组内建筑复原：个人恢复<color=#FFFFFF>{personal_success}</color>个，组恢复<color=#FFFFFF>{group_success}</color>个，个人失败<color=#FFFFFF>{personal_fail}</color>个，组失败<color=#FFFFFF>{group_fail}</color>个</color></size></b>";
            Message_RestoreAll_Group = "<b><size=15><color=#FFFACD>[提示]组内建筑全部复原：个人恢复<color=#FFFFFF>{personal_success}</color>个，组恢复<color=#FFFFFF>{group_success}</color>个</color></size></b>";
        }
    }
}