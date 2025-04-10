using BepInEx.Configuration;
using BepInEx.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using Unity.VisualScripting.FullSerializer;

namespace ItemBundles
{
    internal static class ItemBundlesLogger
    {
        public static ManualLogSource ManualLogSource { get; private set; }

        public static void Init(ManualLogSource manualLogSource)
        {
            ItemBundlesLogger.ManualLogSource = manualLogSource;
        }
        
        public static void Log(LogLevel level, object data, bool debugOnly = false)
        {
            if ( debugOnly && !ItemBundles.Instance.config_debugLogging.Value )
            {
                return;
            }

            ManualLogSource.Log(level, data);
        }

        public static void LogFatal(object data, bool debugOnly = false)
        {
            ItemBundlesLogger.Log(LogLevel.Fatal, data, debugOnly);
        }

        public static void LogError(object data, bool debugOnly = false)
        {
            ItemBundlesLogger.Log(LogLevel.Error, data, debugOnly);
        }

        public static void LogWarning(object data, bool debugOnly = false)
        {
            ItemBundlesLogger.Log(LogLevel.Warning, data, debugOnly);
        }

        public static void LogMessage(object data, bool debugOnly = false)
        {
            ItemBundlesLogger.Log(LogLevel.Message, data, debugOnly);
        }

        public static void LogInfo(object data, bool debugOnly = false)
        {
            ItemBundlesLogger.Log(LogLevel.Info, data, debugOnly);
        }

        public static void LogDebug(object data, bool debugOnly = false)
        {
            ItemBundlesLogger.Log(LogLevel.Debug, data, debugOnly);
        }
    }
}
