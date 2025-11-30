using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace 模拟扫码枪
{
    using System;
    using System.Collections.Concurrent;
    using System.Reflection;

    public static class ObjectCopyExtensions
    {
        // 缓存： (TSource,TTarget) -> 需要拷贝的属性数组
        private static readonly ConcurrentDictionary<(Type, Type), PropertyInfo[]> _mapCache = new ConcurrentDictionary<(Type, Type), PropertyInfo[]>();

        /// <summary>
        /// 将源对象中同名、同类型、可写的公开属性值拷贝到目标对象。
        /// </summary>
        public static void CopyTo<TSource, TTarget>(this TSource source, TTarget target)
            where TSource : class
            where TTarget : class
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (target == null) throw new ArgumentNullException(nameof(target));

            var key = (typeof(TSource), typeof(TTarget));
            var propsToCopy = _mapCache.GetOrAdd(key, k =>
            {
                var sProps = k.Item1.GetProperties(BindingFlags.Public | BindingFlags.Instance);
                var tProps = k.Item2.GetProperties(BindingFlags.Public | BindingFlags.Instance);

                var list = new System.Collections.Generic.List<PropertyInfo>();
                foreach (var sp in sProps)
                {
                    if (!sp.CanRead) continue;
                    var tp = Array.Find(tProps, p =>
                        p.Name == sp.Name &&
                        p.PropertyType == sp.PropertyType &&
                        p.CanWrite);
                    if (tp != null) list.Add(tp);
                }
                return list.ToArray();
            });

            foreach (var p in propsToCopy)
            {
                var value = p.DeclaringType == typeof(TTarget)
                    ? typeof(TSource).GetProperty(p.Name)?.GetValue(source)
                    : source.GetType().GetProperty(p.Name)?.GetValue(source);
                p.SetValue(target, value);
            } 
        }

        public static void CopyTo<T>(this T source, T target)
    where T : class
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (target == null) throw new ArgumentNullException(nameof(target));

            var key = (typeof(T), typeof(T));
            var propsToCopy = _mapCache.GetOrAdd(key, k =>
            {
                var sProps = k.Item1.GetProperties(BindingFlags.Public | BindingFlags.Instance);
                var tProps = k.Item2.GetProperties(BindingFlags.Public | BindingFlags.Instance);

                var list = new System.Collections.Generic.List<PropertyInfo>();
                foreach (var sp in sProps)
                {
                    if (!sp.CanRead) continue;
                    var tp = Array.Find(tProps, p =>
                        p.Name == sp.Name &&
                        p.PropertyType == sp.PropertyType &&
                        p.CanWrite);
                    if (tp != null) list.Add(tp);
                }
                return list.ToArray();
            });

            foreach (var p in propsToCopy)
            {
                var value = p.DeclaringType == typeof(T)
                    ? typeof(T).GetProperty(p.Name)?.GetValue(source)
                    : source.GetType().GetProperty(p.Name)?.GetValue(source);
                p.SetValue(target, value);
            }
             
        }
    }

}
