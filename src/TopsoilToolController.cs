using System.Collections.Generic;
using Mafi;
using Mafi.Core;
using Mafi.Core.GameLoop;
using Mafi.Core.Prototypes;
using Mafi.Core.Terrain;
using Mafi.Core.Terrain.Designation;
using Mafi.Localization;
using Mafi.Unity;
using Mafi.Unity.InputControl;
using Mafi.Unity.Terrain.Designation;
using Mafi.Unity.UiStatic;
using Mafi.Unity.UiStatic.Cursors;
using Mafi.Unity.UiToolkit;
using Mafi.Unity.Utils;
using Mafi.Utils;
using UnityEngine;
using UnityEngine.EventSystems;

namespace TopsoilTool
{
    [GlobalDependency(RegistrationMode.AsSelf)]
    public sealed class TopsoilToolController : IUnityInputController
    {
        private static readonly RelTile2i DragLimit = new(200 - TerrainTopsoil.BlockSize, 200 - TerrainTopsoil.BlockSize);

        internal static readonly LocStr ResultText = Loc.Str("TopsoilTool.Result",
            "Replaced topsoil in {0} of {1} blocks (4×4). Removed {2} decorative rocks.", "Result of replacing the selected terrain.");
        internal static readonly LocStr EmptyText = Loc.Str("TopsoilTool.Empty",
            "No topsoil or decorative rocks needed changing in this selection.", "Result when the selection did not change.");

        private readonly IUnityInputMgr m_input;
        private readonly ShortcutsManager m_shortcuts;
        private readonly IGameLoopEvents m_gameLoopEvents;
        private readonly TerrainTopsoil m_topsoil;
        private readonly TerrainCursor m_terrainCursor;
        private readonly TerrainDesignationsRenderer m_renderer;
        private readonly IActivator m_gridOverlay;
        private readonly TerrainDesignationProto m_bluePreview;
        private readonly TerrainDesignationProto m_dimPreview;
        private Dictionary<Tile2i, (DesignationData Data, bool Eligible)> m_previewedBlocks = new();
        private Dictionary<Tile2i, (DesignationData Data, bool Eligible)> m_nextPreviewBlocks = new();
        private readonly Cursoor m_cursor;
        private readonly ToastNotificationProvider m_toast;

        private Tile2i? m_dragStart;
        private RectangleTerrainArea2i m_selection;
        private RectangleTerrainArea2i m_displayedSelection;
        private float m_nextPreviewRefresh;

        public ControllerConfig Config => ControllerConfig.Tool;

        public TopsoilToolController(IUnityInputMgr input, ShortcutsManager shortcuts, IGameLoopEvents gameLoopEvents,
            TerrainTopsoil topsoil, TerrainCursor terrainCursor, TerrainDesignationsRenderer renderer, ProtosDb protos,
            CursorManager cursorManager, UiRoot uiRoot)
        {
            m_input = input;
            m_shortcuts = shortcuts;
            m_gameLoopEvents = gameLoopEvents;
            m_topsoil = topsoil;
            m_terrainCursor = terrainCursor;
            m_renderer = renderer;
            m_gridOverlay = renderer.CreateActivatorCombinedWithTerrainGrid();
            var leveling = protos.GetOrThrow<TerrainDesignationProto>(new Proto.ID("LevelDesignator"));
            m_bluePreview = CreatePreview(leveling, new ColorRgba(0x3487FF, 198));
            m_dimPreview = CreatePreview(leveling, new ColorRgba(0x315889, 132));
            m_cursor = cursorManager.RegisterCursor(CursorsStyles.SelectArea);
            m_toast = new ToastNotificationProvider(uiRoot);
        }

        private static TerrainDesignationProto CreatePreview(TerrainDesignationProto leveling, ColorRgba blue)
        {
            // The vanilla leveling ID forces the shader to use its terrain-state palette.
            // A separate preview ID keeps the native geometry and height label with our own tint.
            return new TerrainDesignationProto(new Proto.ID("TopsoilTool.BluePreview"), leveling.Strings,
                leveling.PreferInitialBelowTerrain,
                leveling.MaxAssignedEntities, false, default, leveling.IsTerraforming, false,
                new TerrainDesignationProto.Gfx(blue, blue, blue, blue),
                leveling.IsFulfilledFn.ValueOrNull, leveling.IsFulfilledMiningFn.ValueOrNull,
                leveling.IsFulfilledDumpingFn.ValueOrNull);
        }

        public void Activate()
        {
            m_terrainCursor.Activate();
            m_gridOverlay.ActivateIfNotActive();
            m_cursor.Show();
            m_gameLoopEvents.SyncUpdate.AddNonSaveable(this, UpdatePreview);
        }

        public void Deactivate()
        {
            m_gameLoopEvents.SyncUpdate.RemoveNonSaveable(this, UpdatePreview);
            m_selection = default;
            m_dragStart = null;
            foreach (Tile2i origin in m_previewedBlocks.Keys)
                m_renderer.RemovePreviewDesignation(origin);
            m_previewedBlocks.Clear();
            m_nextPreviewBlocks.Clear();
            m_displayedSelection = default;
            m_gridOverlay.DeactivateIfActive();
            m_cursor.Hide();
            m_terrainCursor.Deactivate();
        }

        public bool InputUpdate()
        {
            if (m_shortcuts.IsSecondaryActionDown)
            {
                Exit();
                return true;
            }

            if (!TryGetCursorBlock(out Tile2i origin))
            {
                m_selection = default;
                if (m_dragStart.HasValue && m_shortcuts.IsPrimaryActionUp)
                    Exit();
                return m_dragStart.HasValue;
            }

            if (m_shortcuts.IsPrimaryActionDown)
                m_dragStart = origin;

            m_selection = GetSelection(m_dragStart ?? origin, origin);
            if (m_dragStart.HasValue && m_shortcuts.IsPrimaryActionUp)
            {
                m_topsoil.Enqueue(m_selection, ShowResult);
                Exit();
                return true;
            }

            return m_dragStart.HasValue;
        }

        private bool TryGetCursorBlock(out Tile2i origin)
        {
            origin = default;
            if (EventSystem.current?.IsPointerOverGameObject() == true
                || !m_terrainCursor.TryComputeCurrentPosition(out var position))
            {
                return false;
            }

            origin = TerrainDesignation.GetOrigin(position.Tile2i);
            return m_topsoil.IsBlockInBounds(origin);
        }

        internal static RectangleTerrainArea2i GetSelection(Tile2i start, Tile2i end)
        {
            end = end.Max(start - DragLimit).Min(start + DragLimit);
            Tile2i origin = start.Min(end);
            return new RectangleTerrainArea2i(origin, start.Max(end) - origin + TerrainTopsoil.BlockExtent);
        }

        private void UpdatePreview(GameTime time)
        {
            float now = Time.unscaledTime;
            if (m_displayedSelection.Equals(m_selection) && (m_selection.IsEmpty || now < m_nextPreviewRefresh))
                return;

            m_nextPreviewRefresh = now + 0.25f;
            m_nextPreviewBlocks.Clear();
            var terrain = m_terrainCursor.TerrainManager;
            foreach (Tile2i origin in TerrainTopsoil.EnumerateBlockOrigins(m_selection))
            {
                var data = new DesignationData(origin,
                    terrain.GetHeight(origin).HeightI,
                    terrain.GetHeight(origin.AddX(TerrainTopsoil.BlockSize)).HeightI,
                    terrain.GetHeight(origin.AddXy(TerrainTopsoil.BlockSize)).HeightI,
                    terrain.GetHeight(origin.AddY(TerrainTopsoil.BlockSize)).HeightI);
                var cell = (Data: data, Eligible: m_topsoil.CanConvertBlock(origin));
                m_nextPreviewBlocks.Add(origin, cell);
                if (!m_previewedBlocks.TryGetValue(origin, out var old) || !old.Equals(cell))
                    m_renderer.AddOrUpdatePreviewDesignation(cell.Eligible ? m_bluePreview : m_dimPreview, data);
            }

            foreach (Tile2i origin in m_previewedBlocks.Keys)
            {
                if (!m_nextPreviewBlocks.ContainsKey(origin))
                    m_renderer.RemovePreviewDesignation(origin);
            }

            (m_previewedBlocks, m_nextPreviewBlocks) = (m_nextPreviewBlocks, m_previewedBlocks);
            m_displayedSelection = m_selection;
        }

        private void ShowResult(int changed, int selected, int rocks)
        {
            m_gameLoopEvents.InvokeInSyncNotSaved(() =>
            {
                if (changed == 0 && rocks == 0)
                    m_toast.ShowGeneral(EmptyText);
                else
                    m_toast.ShowSuccess(string.Format(ResultText.TranslatedString, changed, selected, rocks).AsLoc());
            });
        }

        private void Exit() => m_input.DeactivateController(this);
    }
}
