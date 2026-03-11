using Rocket.API;
using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;

namespace PineA.BarricadeSaver
{
    public static class ConfigurationHelper
    {
        /// <summary>类型 -> XmlSerializer 的缓存</summary>
        private static Dictionary<Type, XmlSerializer> serializers = new Dictionary<Type, XmlSerializer>();

        /// <summary>
        /// 从指定文件夹 (pluginFolder) 和文件名 (fileName) 加载一个配置（T）。
        /// 如果不存在则先创建默认配置文件。
        /// 例：LoadConfiguration<LimitConfig>("PineA.BarricadeSaver", "PineA.BarricadeLimit");
        /// </summary>
        public static T LoadConfiguration<T>(string pluginFolder, string fileName)
            where T : IRocketPluginConfiguration, new()
        {
            // 构造文件夹路径
            string pluginDir = Path.Combine(Directory.GetCurrentDirectory(), "Plugins", pluginFolder);

            // 如果不存在，则先创建
            if (!Directory.Exists(pluginDir))
            {
                Directory.CreateDirectory(pluginDir);
            }

            // 完整文件路径
            string filePath = Path.Combine(pluginDir, fileName + ".xml");

            // 查看是否已有序列化器
            if (!serializers.TryGetValue(typeof(T), out var serializer))
            {
                serializer = new XmlSerializer(typeof(T));
                serializers[typeof(T)] = serializer;
            }

            // 如果配置文件不存在，就创建默认并保存
            if (!File.Exists(filePath))
            {
                T config = new T();
                config.LoadDefaults();
                SaveConfiguration(config, pluginFolder, fileName);
                return config;
            }
            else
            {
                // 存在就读取
                using (StreamReader reader = new StreamReader(filePath))
                {
                    return (T)serializer.Deserialize(reader);
                }
            }
        }

        /// <summary>
        /// 保存配置到 XML 文件
        /// </summary>
        public static void SaveConfiguration<T>(T config, string pluginFolder, string fileName)
        {
            string pluginDir = Path.Combine(Directory.GetCurrentDirectory(), "Plugins", pluginFolder);

            // 若不存在则创建
            if (!Directory.Exists(pluginDir))
            {
                Directory.CreateDirectory(pluginDir);
            }

            string filePath = Path.Combine(pluginDir, fileName + ".xml");

            if (!serializers.TryGetValue(typeof(T), out var serializer))
            {
                serializer = new XmlSerializer(typeof(T));
                serializers[typeof(T)] = serializer;
            }

            using (StreamWriter writer = new StreamWriter(filePath))
            {
                serializer.Serialize(writer, config);
            }
        }
    }
}