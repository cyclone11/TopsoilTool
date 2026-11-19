using System;
using Mafi.Unity;
using Mafi.Unity.InputControl;
using UnityEngine;

namespace TopsoilTool
{
    internal sealed class TopsoilKeyBindings : IDisposable
    {
        private static readonly KbHandle ToggleTool = KbHandleBuilder
            .Start("TopsoilTool.Toggle", "Replace topsoil")
            .SetPrimary(KbModifiers.Shift, KeyCode.T)
            .Build();

        private readonly IUnityInputMgr m_input;
        private readonly TopsoilToolController m_tool;

        internal TopsoilKeyBindings(TopsoilToolMod mod, IShortcutsRegistry registry,
            IUnityInputMgr input, TopsoilToolController tool)
        {
            m_input = input;
            m_tool = tool;
            registry.Add(mod, ToggleTool);
            m_input.RegisterGlobalShortcut(ToggleTool, Toggle);
        }

        private bool Toggle()
        {
            m_input.ToggleController(m_tool);
            return true;
        }

        public void Dispose() => m_input.RemoveGlobalShortcut(Toggle);
    }
}
