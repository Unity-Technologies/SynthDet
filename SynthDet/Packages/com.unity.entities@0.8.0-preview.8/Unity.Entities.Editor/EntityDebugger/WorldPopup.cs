using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Unity.Entities.Editor
{
    
    internal delegate void WorldSelectionSetter(World world);

    internal delegate bool ShowInactiveSystemsGetter();
    
    internal class WorldPopup
    {
        public const string kNoWorldName = "\n\n\n";

        private const string kPlayerLoopName = "Show Full Player Loop";
        private const string kShowInactiveSystemsName = "Show Inactive Systems";

        private GenericMenu Menu
        {
            get
            {
                var currentSelection = getWorldSelection();
                var menu = new GenericMenu();
                foreach (var world in World.All)
                {
                    if (getShowAllWorlds() || (world.Flags & (WorldFlags.Streaming | WorldFlags.Shadow)) == 0)
                        menu.AddItem(new GUIContent(world.Name), currentSelection == world, () => setWorldSelection(world));
                }

                if (menu.GetItemCount() == 0)
                    menu.AddDisabledItem(new GUIContent("No Worlds"));

                menu.AddSeparator("");
                menu.AddItem(new GUIContent("Show All Worlds"), getShowAllWorlds(), ToggleShowAllWorlds);
                menu.AddSeparator("");
                menu.AddItem(new GUIContent(kPlayerLoopName), currentSelection == null, () => setWorldSelection(null));
                menu.AddSeparator("");
                menu.AddItem(new GUIContent(kShowInactiveSystemsName), getShowInactiveSystems(), setShowInactiveSystems);
                return menu;
            }
        }

        void ToggleShowAllWorlds()
        {
            var showAllWorlds = !getShowAllWorlds();
            setShowAllWorlds(showAllWorlds);

            if (showAllWorlds)
                return;

            var currentSelection = getWorldSelection();
            if (currentSelection != null && (currentSelection.Flags & (WorldFlags.Streaming | WorldFlags.Shadow)) != 0)
                setWorldSelection(World.All.Count > 0 ? World.All[0] : null);
        }

        private readonly WorldSelectionGetter getWorldSelection;
        private readonly WorldSelectionSetter setWorldSelection;
        
        private readonly ShowInactiveSystemsGetter getShowInactiveSystems;
        private readonly GenericMenu.MenuFunction setShowInactiveSystems;

        private readonly Func<bool> getShowAllWorlds;
        private readonly Action<bool> setShowAllWorlds;

        public WorldPopup(WorldSelectionGetter getWorld, WorldSelectionSetter setWorld, ShowInactiveSystemsGetter getShowSystems, GenericMenu.MenuFunction setShowSystems, Func<bool> getShowAllWorlds, Action<bool> setShowAllWorlds)
        {
            getWorldSelection = getWorld;
            setWorldSelection = setWorld;

            getShowInactiveSystems = getShowSystems;
            setShowInactiveSystems = setShowSystems;

            this.getShowAllWorlds = getShowAllWorlds;
            this.setShowAllWorlds = setShowAllWorlds;
        }
        
        public void OnGUI(bool showingPlayerLoop, string lastSelectedWorldName, GUIStyle style = null)
        {
            TryRestorePreviousSelection(showingPlayerLoop, lastSelectedWorldName);

            var worldName = getWorldSelection()?.Name ?? kPlayerLoopName;
            if (EditorGUILayout.DropdownButton(new GUIContent(worldName), FocusType.Passive, style ?? (GUIStyle)"MiniPullDown"))
            {
                Menu.ShowAsContext();
            }
        }

        internal void TryRestorePreviousSelection(bool showingPlayerLoop, string lastSelectedWorldName)
        {
            if (!showingPlayerLoop && ScriptBehaviourUpdateOrder.CurrentPlayerLoop.subSystemList != null)
            {
                if (lastSelectedWorldName == kNoWorldName)
                {
                    if (World.All.Count > 0)
                        setWorldSelection(World.All[0]);
                }
                else
                {
                    foreach (var world in World.All)
                    {
                        if (world.Name != lastSelectedWorldName)
                            continue;

                        setWorldSelection(world);
                        return;
                    }
                }
            }
        }
    }
}
