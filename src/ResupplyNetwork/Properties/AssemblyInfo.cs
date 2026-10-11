using System.Reflection;

[assembly: KSPAssembly("ResupplyNetwork", 1, 0)]
// 声明依赖以保证加载顺序：ToolbarControl.RegisterMod 必须在 ToolbarController 就绪后调用。
// 代价是没装 ToolbarController 时本 mod 不会加载。
[assembly: KSPAssemblyDependency("ToolbarController", 1, 0)]
