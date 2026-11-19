using System;
using Mafi;
using Mafi.Core;
using Mafi.Core.Products;
using Mafi.Core.Prototypes;
using Mafi.Core.Terrain;
using Mafi.Unity;
using Mafi.Unity.InputControl;
using Mafi.Unity.InputControl.AreaTool;
using Mafi.Unity.UiFramework;
using UnityEngine;
using UnityEngine.EventSystems;

namespace TopsoilTool
{
    /// <summary>
    /// 矩形选区工具：左键拖拽框选，松开后把选区内每格顶层换成土。
    /// 结构参考 CaptainOfCheats 的 BaseEntityCursorInputController（游戏内建基类是 internal）。
    /// </summary>
    [GlobalDependency(RegistrationMode.AsAllInterfaces)]
    public sealed class TopsoilToolController : IToolbarItemInputController, IUnityUi
    {
        public static readonly RelTile1i MaxAreaEdgeSize = new RelTile1i(200);

        private readonly IUnityInputMgr m_input;
        private readonly ShortcutsManager m_shortcuts;
        private readonly TerrainManager m_terrain;
        private readonly ProtosDb m_protos;
        private readonly AreaSelectionTool m_areaTool;

        public bool IsActive { get; private set; }
        public event Action<IToolbarItemInputController>? VisibilityChanged;
        public ControllerConfig Config => ControllerConfig.Tool;
        public bool IsVisible => true;

        public TopsoilToolController(ShortcutsManager shortcuts,
                                     IUnityInputMgr input,
                                     AreaSelectionToolFactory areaFactory,
                                     TerrainManager terrain,
                                     ProtosDb protos)
        {
            m_shortcuts = shortcuts;
            m_input = input;
            m_terrain = terrain;
            m_protos = protos;

            m_areaTool = areaFactory.CreateInstance(OnAreaChanged, OnAreaSelected, OnAreaCleared, Deactivate);
            m_areaTool.SetEdgeSizeLimit(MaxAreaEdgeSize);
            m_areaTool.SetLeftClickColor(ColorRgba.White);   // 合规位置白色高亮
        }

        public void Activate()
        {
            if (IsActive) return;
            IsActive = true;
            m_areaTool.SetLeftClickColor(ColorRgba.White);
            m_areaTool.TerrainCursor.Activate();
            Log.Info("[TopsoilTool] 已激活：左键拖拽框选，右键取消");
        }

        public void Deactivate()
        {
            if (!IsActive) return;
            m_areaTool.TerrainCursor.Deactivate();
            m_areaTool.Deactivate();
            IsActive = false;
        }

        public bool InputUpdate(IInputScheduler inputScheduler)
        {
            if (!IsActive) return false;

            // 右键取消
            if (m_shortcuts.IsSecondaryActionDown)
            {
                Deactivate();
                return true;
            }

            if (m_areaTool.IsActive) return true;

            if (m_shortcuts.IsPrimaryActionDown && !EventSystem.current.IsPointerOverGameObject())
            {
                m_areaTool.Activate(true);
                return true;
            }

            return false;
        }

        public void RegisterUi(UiBuilder builder)
        {
            // v1 不需要额外 UI。
        }

        private void OnAreaChanged(RectangleTerrainArea2i area, bool leftClick)
        {
            // 拖拽中由 AreaSelectionTool 负责画白色选区，这里不需要额外处理。
        }

        private void OnAreaCleared()
        {
        }

        private void OnAreaSelected(RectangleTerrainArea2i area, bool leftClick)
        {
            try
            {
                if (leftClick)
                {
                    int changed = TerrainTopsoil.Apply(m_terrain, m_protos, area);
                    Log.Info($"[TopsoilTool] 换土完成：{changed} 格");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[TopsoilTool] 换土失败：{ex}");
            }
            finally
            {
                Deactivate();
                m_input.DeactivateController(this);
            }
        }
    }
}
