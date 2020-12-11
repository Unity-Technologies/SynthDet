using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Unity.Entities.Editor
{
    internal delegate void SetFilterAction(EntityListQuery entityQuery);

    internal class ComponentTypeFilterUI
    {
        private readonly WorldSelectionGetter getWorldSelection;
        private readonly SetFilterAction setFilter;

        private readonly HashSet<ComponentType> selectedFilterTypes;
        private readonly List<ComponentType> filterTypes = new List<ComponentType>();
        readonly List<ComponentType> previouslySelected = new List<ComponentType>();
        const int k_HistorySize = 30;

        private readonly List<EntityQuery> entityQueries = new List<EntityQuery>();
        private int typeManagerTypeCount;

        class ComponentTypeComparer : IEqualityComparer<ComponentType>
        {
            public bool Equals(ComponentType x, ComponentType y)
                => x.AccessModeType == y.AccessModeType && x.TypeIndex == y.TypeIndex;

            public int GetHashCode(ComponentType obj) => obj.GetHashCode();
        }

        public ComponentTypeFilterUI(SetFilterAction setFilter, WorldSelectionGetter worldSelectionGetter)
        {
            selectedFilterTypes = new HashSet<ComponentType>(new ComponentTypeComparer());
            getWorldSelection = worldSelectionGetter;
            this.setFilter = setFilter;
        }

        internal bool TypeListValid()
        {
            return typeManagerTypeCount == TypeManager.GetTypeCount();
        }

        internal void GetTypes()
        {
            if (getWorldSelection() == null) return;
            if (!TypeListValid())
            {
                typeManagerTypeCount = TypeManager.GetTypeCount();
                filterTypes.Clear();
                selectedFilterTypes.Clear();
                var requiredTypes = new List<ComponentType>();
                var subtractiveTypes = new List<ComponentType>();
                var typeCount = TypeManager.GetTypeCount();
                filterTypes.Capacity = typeCount;
                foreach (var typeInfo in TypeManager.AllTypes)
                {
                    Type type = TypeManager.GetType(typeInfo.TypeIndex);
                    if (type == typeof(Entity)) continue;
                    var typeIndex = typeInfo.TypeIndex;
                    var componentType = ComponentType.FromTypeIndex(typeIndex);
                    if (componentType.GetManagedType() == null) continue;
                    requiredTypes.Add(componentType);
                    componentType.AccessModeType = ComponentType.AccessMode.Exclude;
                    subtractiveTypes.Add(componentType);
                }

                filterTypes.AddRange(requiredTypes);
                filterTypes.AddRange(subtractiveTypes);

                filterTypes.Sort(EntityQueryGUI.CompareTypes);
            }
        }

        int CompareTypes(ComponentType lhs, ComponentType rhs)
        {
            bool isLeftSelected = selectedFilterTypes.Contains(lhs);
            bool isRightSelected = selectedFilterTypes.Contains(rhs);
            if (isLeftSelected == isRightSelected)
                return EntityQueryGUI.CompareTypes(lhs, rhs);
            return isLeftSelected ? -1 : 1;
        }

        public void OnGUI(int matches)
        {
            
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Filter"))
            {
                var filterTypesWithSelected = new List<ComponentType>(selectedFilterTypes);
                filterTypesWithSelected.Sort(EntityQueryGUI.CompareTypes);
                previouslySelected.RemoveAll(selectedFilterTypes.Contains);
                if (previouslySelected.Count > k_HistorySize)
                    previouslySelected.RemoveRange(k_HistorySize, previouslySelected.Count - k_HistorySize);
                filterTypesWithSelected.AddRange(previouslySelected);
                filterTypesWithSelected.AddRange(filterTypes.Where(t => !previouslySelected.Contains(t) && !selectedFilterTypes.Contains(t)));
                ComponentTypeChooser.Open(
                    GUIUtility.GUIToScreenPoint(GUILayoutUtility.GetLastRect().position),
                    filterTypesWithSelected,
                    selectedFilterTypes,
                    previouslySelected,
                    ComponentFilterChanged
                );
            }
            
            if (selectedFilterTypes.Count > 0)
            {
                if (GUILayout.Button("Clear"))
                {
                    selectedFilterTypes.Clear();

                    ComponentFilterChanged();
                }
            }
            GUILayout.FlexibleSpace();
            GUILayout.Label("Matching entities: " + matches);
            GUILayout.EndHorizontal();

            if (selectedFilterTypes.Count > 0)
            {
                GUILayout.BeginHorizontal();
                foreach (var filter in selectedFilterTypes)
                {
                    var style = filter.AccessModeType == ComponentType.AccessMode.Exclude ? EntityDebuggerStyles.ComponentExclude : EntityDebuggerStyles.ComponentRequired;
                    GUILayout.Label(EntityQueryGUI.SpecifiedTypeName(filter.GetManagedType()), style);
                }
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }
        }

        internal EntityQuery GetExistingQuery(ComponentType[] components)
        {
            foreach (var existingGroup in entityQueries)
            {
                if (existingGroup.CompareComponents(components))
                    return existingGroup;
            }

            return null;
        }

        internal EntityQuery GetEntityQuery(ComponentType[] components)
        {
            var group = GetExistingQuery(components);
            if (group != null)
                return group;
            group = getWorldSelection().EntityManager.CreateEntityQuery(components);
            entityQueries.Add(group);

            return group;
        }

        private void ComponentFilterChanged()
        {
            var query = GetEntityQuery(selectedFilterTypes.ToArray());
            setFilter(new EntityListQuery(query));
        }
    }
}
