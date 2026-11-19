using Mafi;
using Mafi.Unity;
using Mafi.Unity.InputControl;
using UnityEngine;

namespace TopsoilTool
{
    /// <summary>
    /// Shift+T 全局快捷键：切换“表土工具”。
    /// API 依据官方文档 https://coigame.com/Modding/Key-Bindings
    /// </summary>
    [GlobalDependency(RegistrationMode.AsSelf)]
    public sealed class TopsoilKeyBindings
    {
        public static readonly KbHandle ToggleTool = KbHandleBuilder
            .Start("TopsoilTool.Toggle", "表土工具")
            .SetPrimary(KbModifiers.Shift, KeyCode.T)
            .Build();

        private readonly IUnityInputMgr m_input;
        private readonly TopsoilToolController m_tool;

        public TopsoilKeyBindings(TopsoilToolMod mod,
                                  IShortcutsRegistry shortcutsRegistry,
                                  IUnityInputMgr inputMgr,
                                  TopsoilToolController tool)
        {
            m_input = inputMgr;
            m_tool = tool;
            shortcutsRegistry.Add(mod, ToggleTool);
            inputMgr.RegisterGlobalShortcut(ToggleTool, Toggle);
        }

        private bool Toggle()
        {
            if (m_tool.IsActive)
            {
                m_tool.Deactivate();
            }
            else
            {
                // TODO(API): 确认 IUnityInputMgr 的激活方法名（DeactivateController 已知存在）。
                m_input.ActivateController(m_tool);
            }
            return true;
        }
    }
}
