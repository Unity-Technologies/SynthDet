using System;
using UnityEditor.IMGUI.Controls;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

namespace Unity.Entities.Editor
{

    internal delegate void EntitySelectionCallback(Entity selection);
    internal delegate World WorldSelectionGetter();
    internal delegate ComponentSystemBase SystemSelectionGetter();
    internal delegate void ChunkArrayAssignmentCallback(NativeArray<ArchetypeChunk> chunkArray);

    internal class EntityListView : TreeView, IDisposable {

        public EntityListQuery SelectedEntityQuery
        {
            get { return selectedEntityQuery; }
            set
            {
                if (value == null || selectedEntityQuery != value)
                {
                    selectedEntityQuery = value;
                    chunkFilter = null;
                    Reload();
                }
            }
        }

        private EntityListQuery selectedEntityQuery;

        private ChunkFilter chunkFilter;
        public void SetFilter(ChunkFilter filter)
        {
            chunkFilter = filter;
            Reload();
        }

        private readonly EntitySelectionCallback setEntitySelection;
        private readonly WorldSelectionGetter getWorldSelection;
        private readonly SystemSelectionGetter getSystemSelection;
        private readonly ChunkArrayAssignmentCallback setChunkArray;

        private readonly EntityArrayListAdapter rows;

        public NativeArray<ArchetypeChunk> ChunkArray => chunkArray;
        private NativeArray<ArchetypeChunk> chunkArray;

        static MultiColumnHeaderState CreateState()
        {
            var columns = new[]
            {
                new MultiColumnHeaderState.Column
                {
                    headerContent = new GUIContent("Index"),
                    headerTextAlignment = TextAlignment.Left,
                    sortingArrowAlignment = TextAlignment.Right,
                    canSort = false,
                    sortedAscending = false,
                    width = 70,
                    minWidth = 70,
                    maxWidth = 70,
                    autoResize = false,
                    allowToggleVisibility = false
                },
                new MultiColumnHeaderState.Column
                {
                    headerContent = new GUIContent("Name"),
                    headerTextAlignment = TextAlignment.Left,
                    canSort = false,
                    width = 400,
                    minWidth = 100,
                    autoResize = true,
                    allowToggleVisibility = false
                }
            };
            return new MultiColumnHeaderState(columns);
        }

        public EntityListView(TreeViewState state, EntityListQuery entityQuery, EntitySelectionCallback entitySelectionCallback, WorldSelectionGetter getWorldSelection, SystemSelectionGetter getSystemSelection, ChunkArrayAssignmentCallback setChunkArray)
            : base(state, new MultiColumnHeader(CreateState()))
        {
            this.setEntitySelection = entitySelectionCallback;
            this.getWorldSelection = getWorldSelection;
            this.getSystemSelection = getSystemSelection;
            this.setChunkArray = setChunkArray;
            selectedEntityQuery = entityQuery;
            rows = new EntityArrayListAdapter();
            getNewSelectionOverride = (item, selection, shift) => new List<int>() {item.id};
            showAlternatingRowBackgrounds = true;
            Reload();
        }

        internal bool ShowingSomething => getWorldSelection() != null &&
                                       (selectedEntityQuery != null || !(getSystemSelection() is ComponentSystemBase));

        private int lastVersion = -1;

        public bool NeedsReload => ShowingSomething && getWorldSelection().EntityManager.Version != lastVersion;
        
        public void ReloadIfNecessary()
        {
            if (NeedsReload)
                Reload();
        }

        public int EntityCount => rows.Count;

        protected override TreeViewItem BuildRoot()
        {
            var root  = new TreeViewItem { id = 0, depth = -1, displayName = "Root" };

            return root;
        }

        protected override IList<TreeViewItem> BuildRows(TreeViewItem root)
        {
            if (!ShowingSomething)
                return new List<TreeViewItem>();
            
            var entityManager = getWorldSelection().EntityManager;
            
            if (chunkArray.IsCreated)
                chunkArray.Dispose();

            entityManager.CompleteAllJobs();

            var group = SelectedEntityQuery?.Group;

            if (group == null || !group.IsCreated)
            {
                var query = SelectedEntityQuery?.QueryDesc;
                if (query == null)
                    group = entityManager.UniversalQuery;
                else
                {
                    group = entityManager.CreateEntityQuery(query);
                }
            }

            chunkArray = group.CreateArchetypeChunkArray(Allocator.Persistent);

            rows.SetSource(chunkArray, entityManager, chunkFilter);
            setChunkArray(chunkArray);

            lastVersion = entityManager.Version;

            return rows;
        }

        protected override void RowGUI(RowGUIArgs args)
        {
            var index = args.item.id;
            DefaultGUI.Label(args.GetCellRect(0), index.ToString(), args.selected, args.focused);
            DefaultGUI.Label(args.GetCellRect(1), args.label, args.selected, args.focused);
        }

        protected override IList<int> GetAncestors(int id)
        {
            return id == 0 ? new List<int>() : new List<int>() {0};
        }

        protected override IList<int> GetDescendantsThatHaveChildren(int id)
        {
            return new List<int>();
        }

        protected override void SelectionChanged(IList<int> selectedIds)
        {
            if (selectedIds.Count > 0)
            {
                Entity selectedEntity;
                if (rows.GetById(selectedIds[0], out selectedEntity))
                    setEntitySelection(selectedEntity);
            }
            else
            {
                setEntitySelection(Entity.Null);
            }
        }

        protected override bool CanMultiSelect(TreeViewItem item)
        {
            return false;
        }

        public void SelectNothing()
        {
            SetSelection(new List<int>());
        }

        public void SetEntitySelection(Entity entitySelection)
        {
            if (entitySelection != Entity.Null && getWorldSelection().EntityManager.Exists(entitySelection))
                SetSelection(new List<int>{entitySelection.Index});
        }

        public void TouchSelection()
        {
            SetSelection(
                GetSelection()
                , TreeViewSelectionOptions.RevealAndFrame);
        }

        public void FrameSelection()
        {
            var selection = GetSelection();
            if (selection.Count > 0)
            {
                FrameItem(selection[0]);
            }
        }

        public void Dispose()
        {
            if (chunkArray.IsCreated)
                chunkArray.Dispose();
        }
    }
}
