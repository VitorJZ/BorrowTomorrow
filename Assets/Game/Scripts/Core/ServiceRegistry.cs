using System;
using System.Collections.Generic;
using UnityEngine;

namespace BorrowTomorrow.Core
{
    /// <summary>
    /// 跨模块服务注册表。
    /// 各模块服务组件在 Awake 中向本表注册 Core 中定义的接口实例，
    /// 其他模块只通过接口协作，不产生对具体实现类的横向依赖。
    /// 依据策划方案 10.3：全局管理对象通过显式注册获取，不做每帧全场景查找。
    /// </summary>
    public static class ServiceRegistry
    {
        private static readonly Dictionary<Type, object> services = new Dictionary<Type, object>();

        /// <summary>注册（或覆盖）一个服务实例。服务应在 OnDestroy 中注销自身。</summary>
        public static void Register<T>(T service) where T : class
        {
            if (service == null)
            {
                Debug.LogWarning($"[ServiceRegistry] 拒绝注册空服务：{typeof(T).Name}");
                return;
            }
            services[typeof(T)] = service;
        }

        public static void Unregister<T>(T service) where T : class
        {
            if (services.TryGetValue(typeof(T), out object current) && ReferenceEquals(current, service))
            {
                services.Remove(typeof(T));
            }
        }

        public static bool TryGet<T>(out T service) where T : class
        {
            if (services.TryGetValue(typeof(T), out object raw) && raw is T typed)
            {
                // Interface references bypass Unity's destroyed-object null operator.
                if (raw is UnityEngine.Object unityObject && unityObject == null)
                {
                    services.Remove(typeof(T));
                    service = null;
                    return false;
                }
                service = typed;
                return true;
            }
            service = null;
            return false;
        }

        /// <summary>获取服务；未注册时抛出异常以便尽早暴露装配问题。</summary>
        public static T Get<T>() where T : class
        {
            if (TryGet<T>(out T service))
            {
                return service;
            }
            throw new InvalidOperationException($"[ServiceRegistry] 服务未注册：{typeof(T).Name}。请确认 Bootstrap 场景的系统根已装配。");
        }

        /// <summary>编辑器域重载或测试间清理，防止残留 Fake Null。</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            services.Clear();
        }
    }
}
