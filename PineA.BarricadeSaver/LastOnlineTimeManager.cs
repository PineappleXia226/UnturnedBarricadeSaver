using MySql.Data.MySqlClient;
using System;
using System.Threading.Tasks;
using UnityEngine;

namespace PineA.BarricadeSaver
{
    /// <summary>
    /// 用于管理 PineA.LastOnlineTime 表，此表用于存储玩家 SteamID 及其最后一次上线时间。
    /// </summary>
    public static class LastOnlineTimeManager
    {
        private static string _connectionString;
        private static readonly string _tableName = "PineA.LastOnlineTime";

        /// <summary>
        /// 初始化数据库连接，并检查/创建 LastOnlineTime 表。
        /// </summary>
        /// <param name="connectionString">数据库连接字符串</param>
        public static void Initialize(string connectionString)
        {
            // 同样，给连接字符串加上必要的超时、Keepalive等
            var builder = new MySqlConnectionStringBuilder(connectionString)
            {
                ConnectionTimeout = 30,
                DefaultCommandTimeout = 60,
                Keepalive = 10
            };
            _connectionString = builder.ConnectionString;

            try
            {
                CreateTableIfNotExists();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PineA.LastOnlineTimeManager] 初始化时检查/创建表失败: {ex}");
            }
        }

        /// <summary>
        /// 如果表不存在则创建，表结构包含 SteamID（主键）和 LastOnline（最后上线时间）。
        /// </summary>
        private static void CreateTableIfNotExists()
        {
            using (var connection = new MySqlConnection(_connectionString))
            {
                connection.Open();
                string sql = $@"
CREATE TABLE IF NOT EXISTS `{_tableName}` (
    `SteamID` BIGINT NOT NULL,
    `LastOnline` DATETIME NOT NULL,
    PRIMARY KEY (`SteamID`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;
";
                using (var cmd = new MySqlCommand(sql, connection))
                {
                    cmd.CommandTimeout = 60;
                    cmd.ExecuteNonQuery();
                }
            }
        }

        /// <summary>
        /// 该方法提供一个带重试的打开连接逻辑
        /// </summary>
        /// <returns></returns>
        private static async Task<MySqlConnection> OpenConnectionWithRetryAsync()
        {
            var conn = new MySqlConnection(_connectionString);
            const int maxRetries = 2;
            int attempt = 0;

            while (true)
            {
                try
                {
                    await conn.OpenAsync();
                    // 测试ping
                    return conn;
                }
                catch (Exception ex)
                {
                    attempt++;
                    Debug.LogWarning($"[PineA.LastOnlineTimeManager] 打开连接失败，第 {attempt} 次: {ex.Message}");
                    conn.Dispose();

                    if (attempt >= maxRetries)
                    {
                        throw;
                    }

                    await Task.Delay(1000);
                    conn = new MySqlConnection(_connectionString);
                }
            }
        }

        /// <summary>
        /// 异步更新玩家最后一次上线的时间。如果该 SteamID 已存在则更新，否则插入新记录。
        /// </summary>
        /// <param name="steamID">玩家 SteamID</param>
        public static async Task UpdateLastOnlineTimeAsync(ulong steamID)
        {
            try
            {
                using (var connection = await OpenConnectionWithRetryAsync())
                {
                    string sql = $@"
INSERT INTO `{_tableName}` (`SteamID`, `LastOnline`)
VALUES (@SteamID, @LastOnline)
ON DUPLICATE KEY UPDATE `LastOnline` = @LastOnline;
";
                    using (var cmd = new MySqlCommand(sql, connection))
                    {
                        cmd.CommandTimeout = 60;
                        cmd.Parameters.AddWithValue("@SteamID", steamID);
                        cmd.Parameters.AddWithValue("@LastOnline", DateTime.Now);
                        await cmd.ExecuteNonQueryAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PineA.LastOnlineTimeManager] UpdateLastOnlineTimeAsync异常: {ex}");
            }
        }

        /// <summary>
        /// 获取玩家的最后上线时间。如果没有查到则返回 null。
        /// </summary>
        /// <param name="steamID"></param>
        /// <returns></returns>
        public static async Task<DateTime?> GetLastOnlineTimeAsync(ulong steamID)
        {
            try
            {
                using (var connection = await OpenConnectionWithRetryAsync())
                {
                    string sql = $@"
SELECT `LastOnline`
FROM `{_tableName}`
WHERE `SteamID` = @SteamID
LIMIT 1;
";
                    using (var cmd = new MySqlCommand(sql, connection))
                    {
                        cmd.CommandTimeout = 60;
                        cmd.Parameters.AddWithValue("@SteamID", steamID);
                        var result = await cmd.ExecuteScalarAsync();
                        if (result != null && result != DBNull.Value)
                        {
                            return Convert.ToDateTime(result);
                        }
                    }
                }
            }
            catch (Exception)
            {
                // 此处可以不记录日志，避免刷屏
            }
            return null;
        }
    }
}