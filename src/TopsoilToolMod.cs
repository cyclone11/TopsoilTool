using Mafi;
using Mafi.Core.Mods;
using Mafi.Core.Prototypes;

namespace TopsoilTool
{
    /// <summary>
    /// 模组入口。因为要注册 UI 工具/快捷键，直接实现 IMod（不用 DataOnlyMod）。
    /// </summary>
    public sealed class TopsoilToolMod : IMod
    {
        public string Name => "TopsoilTool";
        public int Version => 1;
        public bool IsUiOnly => false;

        public void Initialize(DependencyResolver resolver, bool gameWasLoaded)
        {
            Log.Info("[TopsoilTool] initialized");
        }

        public void RegisterPrototypes(ProtoRegistrator registrator)
        {
            // 本模组不新增原型。
        }

        public void RegisterDependencies(DependencyResolverBuilder depBuilder, ProtosDb protosDb, bool wasLoaded)
        {
            // 控制器和快捷键类都带 [GlobalDependency]，游戏会自动创建，无需手动注册。
        }
    }
}
