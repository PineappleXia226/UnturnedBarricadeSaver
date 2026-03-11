using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace PineA.BarricadeSaver
{
    /// <summary>
    /// 该类用于在数据库中读写 Barricade 的信息。
    /// </summary>
    public static class DatabaseManager
    {
        private static string _connectionString;
        private static string _tableName;

        /// <summary>
        /// 初始化数据库连接信息，并检查/创建表结构。
        /// </summary>
        /// <param name="connectionString">数据库连接字符串</param>
        /// <param name="tableName">存储Barricade数据的表名</param>
        public static void Initialize(string connectionString, string tableName)
        {
            // 在原连接字符串的基础上，尽量显式指定连接超时、命令超时、以及 Keepalive 等
            // 以减少“主机中止连接”的几率（需 MySQL Connector 8.0.21+ 支持 Keepalive 参数）
            var builder = new MySqlConnectionStringBuilder(connectionString)
            {
                ConnectionTimeout = 30,   // 打开连接的超时时间（秒）
                DefaultCommandTimeout = 60, // 执行命令的超时时间（秒）
                Keepalive = 10,          // 如果空闲超过10秒，则会向服务器发送ping，避免连接被服务器断开
                // Pooling = true,       // 如需连接池，可启用
                // MaximumPoolSize = 100
            };

            _connectionString = builder.ConnectionString;
            _tableName = tableName;

            try
            {
                // 检查并创建表
                CreateTableIfNotExists();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PineA.BarricadeSaver] 在 Initialize 时检查/创建表失败: {ex}");
            }
        }

        /// <summary>
        /// 如果需要，在这里进行简单的Ping或者重复连接的逻辑
        /// </summary>
        /// <returns>可用的 MySqlConnection 对象，注意使用后记得Dispose</returns>
        private static async Task<MySqlConnection> OpenConnectionWithRetryAsync()
        {
            var conn = new MySqlConnection(_connectionString);
            // 简单重试次数
            const int maxRetries = 2;
            int attempt = 0;

            while (true)
            {
                try
                {
                    await conn.OpenAsync();
                    return conn;
                }
                catch (Exception ex)
                {
                    attempt++;
                    Debug.LogWarning($"[PineA.BarricadeSaver] 尝试打开MySQL连接失败，第 {attempt} 次: {ex.Message}");
                    conn.Dispose();

                    if (attempt >= maxRetries)
                    {
                        // 重试多次仍失败，抛出异常
                        throw;
                    }

                    // 等待一会再重试
                    await Task.Delay(1000);
                    conn = new MySqlConnection(_connectionString);
                }
            }
        }

        /// <summary>
        /// 若表不存在则自动创建（使用自增ID作为主键）。
        /// 这里假设表结构是 ID 自增 + 其他字段。
        /// </summary>
        private static void CreateTableIfNotExists()
        {
            // 该方法是同步的，也可以做成异步版本
            using (var connection = new MySqlConnection(_connectionString))
            {
                connection.Open();

                // 创建表示例：ID为自增主键
                string sql = $@"
CREATE TABLE IF NOT EXISTS `{_tableName}` (
    `ID`               INT NOT NULL AUTO_INCREMENT,
    `InstanceID`       INT NOT NULL,
    `BarricadeItemID`  INT NOT NULL,
    `Owner`            BIGINT NOT NULL,
    `Group`            BIGINT NOT NULL,
    `Health`           FLOAT NOT NULL,
    `State`            LONGBLOB,
    `PosX`             FLOAT NOT NULL,
    `PosY`             FLOAT NOT NULL,
    `PosZ`             FLOAT NOT NULL,
    `RotX`             FLOAT NOT NULL,
    `RotY`             FLOAT NOT NULL,
    `RotZ`             FLOAT NOT NULL,
    `SavedTime`        DATETIME NOT NULL,
    PRIMARY KEY (`ID`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;
";
                using (var cmd = new MySqlCommand(sql, connection))
                {
                    // 给命令对象设置一个更长的命令超时时间
                    cmd.CommandTimeout = 60;
                    cmd.ExecuteNonQuery();
                }
            }
        }

        /// <summary>
        /// 异步保存单个 Barricade 数据到数据库。
        /// </summary>
        /// <param name="data">Barricade数据对象</param>
        public static async Task SaveBarricadeDataAsync(PineA_BarricadeData data)
        {
            try
            {
                using (var connection = await OpenConnectionWithRetryAsync())
                {
                    string sql = $@"
INSERT INTO `{_tableName}`
(
    `InstanceID`,
    `BarricadeItemID`,
    `Owner`,
    `Group`,
    `Health`,
    `State`,
    `PosX`,
    `PosY`,
    `PosZ`,
    `RotX`,
    `RotY`,
    `RotZ`,
    `SavedTime`
)
VALUES
(
    @InstanceID,
    @BarricadeItemID,
    @Owner,
    @Group,
    @Health,
    @State,
    @PosX,
    @PosY,
    @PosZ,
    @RotX,
    @RotY,
    @RotZ,
    @SavedTime
);";

                    using (var cmd = new MySqlCommand(sql, connection))
                    {
                        cmd.CommandTimeout = 60; // 设定命令超时
                        cmd.Parameters.AddWithValue("@InstanceID", data.InstanceID);
                        cmd.Parameters.AddWithValue("@BarricadeItemID", data.BarricadeItemID);
                        cmd.Parameters.AddWithValue("@Owner", data.Owner);
                        cmd.Parameters.AddWithValue("@Group", data.Group);
                        cmd.Parameters.AddWithValue("@Health", data.Health);

                        if (data.State != null && data.State.Length > 0)
                            cmd.Parameters.AddWithValue("@State", data.State);
                        else
                            cmd.Parameters.AddWithValue("@State", DBNull.Value);

                        cmd.Parameters.AddWithValue("@PosX", data.Position.x);
                        cmd.Parameters.AddWithValue("@PosY", data.Position.y);
                        cmd.Parameters.AddWithValue("@PosZ", data.Position.z);

                        cmd.Parameters.AddWithValue("@RotX", data.Rotation.eulerAngles.x);
                        cmd.Parameters.AddWithValue("@RotY", data.Rotation.eulerAngles.y);
                        cmd.Parameters.AddWithValue("@RotZ", data.Rotation.eulerAngles.z);

                        cmd.Parameters.AddWithValue("@SavedTime", data.SavedTime);

                        await cmd.ExecuteNonQueryAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PineA.BarricadeSaver] SaveBarricadeData异常: {ex}");
            }
        }

        /// <summary>
        /// 根据玩家的SteamID从数据库加载所有Barricade数据。
        /// </summary>
        /// <param name="steamID">玩家的SteamID</param>
        /// <returns>该玩家的所有Barricade数据列表</returns>
        public static async Task<List<PineA_BarricadeData>> LoadBarricadesByOwnerAsync(ulong steamID)
        {
            var list = new List<PineA_BarricadeData>();

            try
            {
                using (var connection = await OpenConnectionWithRetryAsync())
                {
                    // 一定要把 ID 这个主键也SELECT出来
                    string sql = $@"
SELECT
    `ID`,               -- 这里把ID也查出来
    `InstanceID`,
    `BarricadeItemID`,
    `Owner`,
    `Group`,
    `Health`,
    `State`,
    `PosX`,
    `PosY`,
    `PosZ`,
    `RotX`,
    `RotY`,
    `RotZ`,
    `SavedTime`
FROM `{_tableName}`
WHERE `Owner` = @Owner;
";

                    using (var cmd = new MySqlCommand(sql, connection))
                    {
                        cmd.CommandTimeout = 60; // 命令超时
                        cmd.Parameters.AddWithValue("@Owner", steamID);

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                var data = new PineA_BarricadeData
                                {
                                    // 把数据库字段赋值到 PineA_BarricadeData
                                    ID = Convert.ToInt32(reader["ID"]), // <--- 新增
                                    InstanceID = (uint)(int)reader["InstanceID"],
                                    BarricadeItemID = (ushort)(int)reader["BarricadeItemID"],
                                    Owner = (ulong)(long)reader["Owner"],
                                    Group = (ulong)(long)reader["Group"],
                                    Health = (float)Convert.ToDouble(reader["Health"])
                                };

                                var stateObj = reader["State"];
                                if (stateObj != DBNull.Value)
                                    data.State = (byte[])stateObj;
                                else
                                    data.State = new byte[0];

                                float px = (float)Convert.ToDouble(reader["PosX"]);
                                float py = (float)Convert.ToDouble(reader["PosY"]);
                                float pz = (float)Convert.ToDouble(reader["PosZ"]);
                                data.Position = new Vector3(px, py, pz);

                                float rx = (float)Convert.ToDouble(reader["RotX"]);
                                float ry = (float)Convert.ToDouble(reader["RotY"]);
                                float rz = (float)Convert.ToDouble(reader["RotZ"]);
                                data.Rotation = Quaternion.Euler(rx, ry, rz);

                                var savedTimeObj = reader["SavedTime"];
                                if (savedTimeObj != DBNull.Value)
                                    data.SavedTime = (DateTime)savedTimeObj;
                                else
                                    data.SavedTime = DateTime.MinValue;

                                list.Add(data);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PineA.BarricadeSaver] LoadBarricadesByOwner异常: {ex}");
            }

            return list;
        }

        /// <summary>
        /// 根据组ID从数据库加载所有Barricade数据（当UseGroupID=true时使用）。
        /// </summary>
        /// <param name="groupID">玩家所属的组ID</param>
        /// <returns>该组的所有Barricade数据列表</returns>
        public static async Task<List<PineA_BarricadeData>> LoadBarricadesByGroupAsync(ulong groupID)
        {
            var list = new List<PineA_BarricadeData>();

            try
            {
                using (var connection = await OpenConnectionWithRetryAsync())
                {
                    string sql = $@"
SELECT
    `ID`,
    `InstanceID`,
    `BarricadeItemID`,
    `Owner`,
    `Group`,
    `Health`,
    `State`,
    `PosX`,
    `PosY`,
    `PosZ`,
    `RotX`,
    `RotY`,
    `RotZ`,
    `SavedTime`
FROM `{_tableName}`
WHERE `Group` = @Group;
";

                    using (var cmd = new MySqlCommand(sql, connection))
                    {
                        cmd.CommandTimeout = 60;
                        cmd.Parameters.AddWithValue("@Group", groupID);

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                var data = new PineA_BarricadeData
                                {
                                    ID = Convert.ToInt32(reader["ID"]),
                                    InstanceID = (uint)(int)reader["InstanceID"],
                                    BarricadeItemID = (ushort)(int)reader["BarricadeItemID"],
                                    Owner = (ulong)(long)reader["Owner"],
                                    Group = (ulong)(long)reader["Group"],
                                    Health = (float)Convert.ToDouble(reader["Health"])
                                };

                                var stateObj = reader["State"];
                                if (stateObj != DBNull.Value)
                                    data.State = (byte[])stateObj;
                                else
                                    data.State = new byte[0];

                                float px = (float)Convert.ToDouble(reader["PosX"]);
                                float py = (float)Convert.ToDouble(reader["PosY"]);
                                float pz = (float)Convert.ToDouble(reader["PosZ"]);
                                data.Position = new Vector3(px, py, pz);

                                float rx = (float)Convert.ToDouble(reader["RotX"]);
                                float ry = (float)Convert.ToDouble(reader["RotY"]);
                                float rz = (float)Convert.ToDouble(reader["RotZ"]);
                                data.Rotation = Quaternion.Euler(rx, ry, rz);

                                var savedTimeObj = reader["SavedTime"];
                                if (savedTimeObj != DBNull.Value)
                                    data.SavedTime = (DateTime)savedTimeObj;
                                else
                                    data.SavedTime = DateTime.MinValue;

                                list.Add(data);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PineA.BarricadeSaver] LoadBarricadesByGroup异常: {ex}");
            }

            return list;
        }

        /// <summary>
        /// 删除某个玩家(steamID)的Barricade数据。这里只删除【成功放置】的那几条 ID。
        /// </summary>
        /// <param name="barricadeList">该玩家已成功放置的Barricade列表(含数据库主键ID)</param>
        public static async Task DeleteBarricadesByOwnerAsync(List<PineA_BarricadeData> barricadeList)
        {
            try
            {
                if (barricadeList == null || barricadeList.Count == 0)
                    return;

                // 从成功放置的记录中，提取出它们在表中的自增主键 ID
                var idList = barricadeList.Select(b => b.ID).Distinct().ToList();

                if (idList.Count == 0)
                    return;

                // 拼接成 "1,2,3" 这种IN语句
                string joinedIds = string.Join(",", idList);

                using (var connection = await OpenConnectionWithRetryAsync())
                {
                    string sql = $@"
DELETE FROM `{_tableName}`
WHERE `ID` IN ({joinedIds});
";

                    using (var cmd = new MySqlCommand(sql, connection))
                    {
                        cmd.CommandTimeout = 60;
                        var rows = await cmd.ExecuteNonQueryAsync();
                        // rows 代表实际删除的行数，如有需要可打印日志
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PineA.BarricadeSaver] DeleteBarricadesByOwnerAsync异常: {ex}");
            }
        }

        /// <summary>
        /// 删除某个组(GroupID)的Barricade数据。这里只删除【成功放置】的那几条 ID。
        /// </summary>
        /// <param name="barricadeList">该组已成功放置的Barricade列表(含数据库主键ID)</param>
        public static async Task DeleteBarricadesByGroupAsync(List<PineA_BarricadeData> barricadeList)
        {
            try
            {
                if (barricadeList == null || barricadeList.Count == 0)
                    return;

                var idList = barricadeList.Select(b => b.ID).Distinct().ToList();

                if (idList.Count == 0)
                    return;

                string joinedIds = string.Join(",", idList);

                using (var connection = await OpenConnectionWithRetryAsync())
                {
                    string sql = $@"
DELETE FROM `{_tableName}`
WHERE `ID` IN ({joinedIds});
";
                    using (var cmd = new MySqlCommand(sql, connection))
                    {
                        cmd.CommandTimeout = 60;
                        var rows = await cmd.ExecuteNonQueryAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PineA.BarricadeSaver] DeleteBarricadesByGroupAsync异常: {ex}");
            }
        }
    }
}