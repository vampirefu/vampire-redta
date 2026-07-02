using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Rampastring.XNAUI.XNAControls;

namespace ClientGUI
{
    /// <summary>
    /// 此GUI创建器帮助注册基于XNAControl的控件，这些控件可通过依赖注入或INI系统使用。
    /// </summary>
    public static class ClientGUICreator
    {
        private static List<Type> controlTypes = new();

        private static IServiceProvider serviceProvider;

        /// <summary>
        /// 将控件类型作为单例添加到已知控件类型列表中。
        ///
        /// 当控件作为单例添加时，每次通过控件名称请求时都会返回相同的实例。
        /// </summary>
        /// <param name="serviceCollection">用于依赖注入的服务集合。</param>
        /// <param name="controlType">要添加的控件类型。</param>
        /// <returns>IServiceCollection。</returns>
        public static IServiceCollection AddSingletonXnaControl<T>(this IServiceCollection serviceCollection)
        {
            Type controlType = typeof(T);
            AddXnaControl(controlType);
            return serviceCollection.AddSingleton(controlType, provider => GetXnaControl(provider, controlType.Name));
        }

        /// <summary>
        /// 将控件类型作为瞬态添加到已知控件类型列表中。
        ///
        /// 当控件作为瞬态添加时，每次通过控件名称请求时都会创建一个新实例。
        /// </summary>
        /// <param name="serviceCollection">用于依赖注入的服务集合。</param>
        /// <param name="controlType">要添加的控件类型。</param>
        /// <returns>IServiceCollection。</returns>
        public static IServiceCollection AddTransientXnaControl<T>(this IServiceCollection serviceCollection)
        {
            Type controlType = typeof(T);
            AddXnaControl(controlType);
            return serviceCollection.AddTransient(controlType, provider => GetXnaControl(provider, controlType.Name));
        }

        /// <summary>
        /// 通常在通过INI UI系统进行控件初始化时调用。
        /// </summary>
        /// <param name="controlTypeName">要实例化的控件名称。</param>
        /// <returns>XNAControl 实例。</returns>
        public static XNAControl GetXnaControl(string controlTypeName) => GetXnaControl(serviceProvider, controlTypeName);

        /// <summary>
        /// 将控件类型添加到已知控件列表中以供实例化。
        /// </summary>
        /// <param name="controlType">要添加的控件类型。</param>
        /// <exception cref="Exception">
        /// 如果此控件不是XNAControl的子类，也不是XNAControl本身。
        /// 或者，此组件类型被添加了多次。
        /// </exception>
        private static void AddXnaControl(Type controlType)
        {
            if (!controlType.IsSubclassOf(typeof(XNAControl)) && controlType != typeof(XNAControl))
                throw new Exception($"{controlType.Name} is not a sub class of {nameof(XNAControl)}");

            ValidateNonDuplicateControlType(controlType);

            controlTypes.Add(controlType);
        }

        /// <summary>
        /// 因为INI系统通过 <see cref="Type.Name"/> 检索控件，我们需要确保
        /// 不会注册与另一个控件同名（基名）的重复项。
        /// </summary>
        /// <param name="controlType">要验证的类型。</param>
        /// <exception cref="Exception">如果已注册了同名的另一个控件。</exception>
        private static void ValidateNonDuplicateControlType(Type controlType)
        {
            if (controlTypes.Any(c => c.Name == controlType.Name))
                throw new Exception($"A control type with name {controlType.Name} has already been registered.");
        }

        /// <summary>
        /// 这是用于实例化控件的"工厂"。
        ///
        /// 如果此函数用于单例，对于给定的 <see cref="controlTypeName"/> 只会被调用一次。
        /// </summary>
        /// <param name="provider">依赖注入服务提供者。</param>
        /// <param name="controlTypeName">要实例化的控件类型名称。</param>
        /// <returns>XNAControl 实例。</returns>
        /// <exception cref="Exception">如果控件类型未在服务提供者中注册。</exception>
        private static XNAControl GetXnaControl(IServiceProvider provider, string controlTypeName)
        {
            serviceProvider ??= provider;
            Type controlType = controlTypes.SingleOrDefault(control => control.Name == controlTypeName);
            if (controlType == null)
                throw new Exception($"Control type {controlTypeName} was not registered with ServiceCollection in GameClass");

            ConstructorInfo constructor = controlType.GetConstructors().First();
            IEnumerable<object> parameterInstances = constructor.GetParameters().Select(param => GetTypeInstance(param.ParameterType));

            return (XNAControl)constructor.Invoke(parameterInstances.ToArray());
        }

        /// <summary>
        /// 尝试从服务提供者获取特定类型的实例。
        /// </summary>
        /// <param name="type">要实例化的类型。</param>
        /// <returns>指定类型的实例。</returns>
        /// <exception cref="Exception">如果该类型未在服务提供者中注册。</exception>
        private static object GetTypeInstance(Type type)
            => serviceProvider.GetService(type) ?? throw new Exception($"Control type {type.Name} was not registered with ServiceCollection in GameClass");
    }
}