using Mafi;
using Mafi.Collections;
using Mafi.Core.Game;
using Mafi.Core.Mods;
using Mafi.Core.Prototypes;
using Mafi.Core.Simulation;
using Mafi.Unity;
using Mafi.Unity.InputControl;

namespace TopsoilTool
{
    public sealed class TopsoilToolMod : IMod
    {
        private ISimLoopEvents? m_simEvents;
        private TerrainTopsoil? m_topsoil;
        private TopsoilKeyBindings? m_keyBindings;

        public TopsoilToolMod(ModManifest manifest)
        {
            Manifest = manifest;
            JsonConfig = new ModJsonConfig(this);
        }

        public ModManifest Manifest { get; }
        public ModJsonConfig JsonConfig { get; }
        bool IMod.IsUiOnly => false;
        Option<IConfig> IMod.ModConfig => Option.None;

        void IMod.RegisterPrototypes(ProtoRegistrator registrator)
        {
            // Register the result texts while the game is collecting this mod's translations.
            _ = TopsoilToolController.ResultText;
            _ = TopsoilToolController.EmptyText;
        }

        void IMod.RegisterDependencies(DependencyResolverBuilder builder, ProtosDb protos, bool wasLoaded) { }
        void IMod.EarlyInit(DependencyResolver resolver) { }
        void IMod.MigrateJsonConfig(VersionSlim savedVersion, Dict<string, object> savedValues) { }

        void IMod.Initialize(DependencyResolver resolver, bool gameWasLoaded)
        {
            var input = resolver.TryResolve<IUnityInputMgr>().ValueOrNull;
            if (input == null)
                return;

            m_simEvents = resolver.Resolve<ISimLoopEvents>();
            m_topsoil = resolver.Resolve<TerrainTopsoil>();
            m_simEvents.UpdateAfterCmdProc.AddNonSaveable(this, m_topsoil.ProcessPending);

            m_keyBindings = new TopsoilKeyBindings(this, resolver.Resolve<IShortcutsRegistry>(), input,
                resolver.Resolve<TopsoilToolController>());
        }

        public void Dispose()
        {
            m_keyBindings?.Dispose();
            if (m_simEvents != null && m_topsoil != null)
                m_simEvents.UpdateAfterCmdProc.RemoveNonSaveable(this, m_topsoil.ProcessPending);
        }
    }
}
