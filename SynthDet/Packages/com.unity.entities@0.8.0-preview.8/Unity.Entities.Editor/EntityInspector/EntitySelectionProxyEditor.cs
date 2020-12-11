using Unity.Properties;
using UnityEditor;
using UnityEngine;

namespace Unity.Entities.Editor
{
    [CustomEditor(typeof(EntitySelectionProxy))]
    internal class EntitySelectionProxyEditor : UnityEditor.Editor
    {
        private EntityIMGUIVisitor visitor;
        private readonly RepaintLimiter repaintLimiter = new RepaintLimiter();

        [SerializeField] private SystemInclusionList inclusionList;

        class Styles
        {
            public GUIStyle TitleStyle;

            public Styles()
            {
                TitleStyle = "IN BigTitle";
                TitleStyle.padding = new RectOffset(14, 8, 10, 7);
            }
        }

        Styles styles;
        
        void OnEnable()
        {
            visitor = new EntityIMGUIVisitor((entity) =>
                {
                    var targetProxy = (EntitySelectionProxy) target;
                    if (!targetProxy.Exists)
                        return;
                    targetProxy.OnEntityControlSelectButton(targetProxy.World, entity);
                },
                () => { return callCount++ == 0; },
                entity => currentEntityManager.GetName(entity));

            inclusionList = new SystemInclusionList();
        }

        private int callCount = 0;

        private uint lastVersion;
        EntityManager currentEntityManager;

        private uint GetVersion()
        {
            var container = target as EntitySelectionProxy;
            if (container != null && container.World != null && container.World.IsCreated)
                return container.World.EntityManager.GetChunkVersionHash(container.Entity);
            else
                return 0;
        }

        void InitStyles()
        {
            if (styles == null)
                styles = new Styles();
        }
        
        protected override void OnHeaderGUI()
        {
            InitStyles();
            GUILayout.BeginVertical(styles.TitleStyle);
            var targetProxy = (EntitySelectionProxy) target;
            if (!targetProxy.Exists)
                return;
            
            GUI.enabled = true;
            var entity = targetProxy.Entity;
            var entityName = targetProxy.EntityManager.GetName(entity);
            var newName = EditorGUILayout.DelayedTextField(entityName);
            if (newName != entityName)
            {
                targetProxy.EntityManager.SetName(entity, newName);
                EditorWindow.GetWindow<EntityDebugger>().Repaint();
            }
            GUI.enabled = false;
            
            GUILayout.Space(2f);
            GUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(true))
            {
                GUILayout.Label("Entity Index");
                GUILayout.TextField(entity.Index.ToString(), GUILayout.MinWidth(40f));
                GUILayout.FlexibleSpace();
                GUILayout.Label("Version");
                GUILayout.TextField(entity.Version.ToString(), GUILayout.MinWidth(40f));
            }
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }

        public override void OnInspectorGUI()
        {
            var targetProxy = (EntitySelectionProxy) target;
            if (!targetProxy.Exists)
                return;

            var container = targetProxy.Container;

            currentEntityManager = targetProxy.EntityManager;
            callCount = 0;
            PropertyContainer.Visit(ref container, visitor);

            GUI.enabled = true;

            inclusionList.OnGUI(targetProxy.World, targetProxy.Entity);

            repaintLimiter.RecordRepaint();
            lastVersion = GetVersion();
        }

        public override bool RequiresConstantRepaint()
        {
            return (GetVersion() != lastVersion) && (repaintLimiter.SimulationAdvanced() || !Application.isPlaying);
        }
    }
}
