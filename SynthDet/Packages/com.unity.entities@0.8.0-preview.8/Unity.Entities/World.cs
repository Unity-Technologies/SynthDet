using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using Unity.Core;
using UnityEngine.Assertions;

namespace Unity.Entities
{
    /// <summary>
    /// Specify all traits a <see cref="World"/> can have.
    /// </summary>
    [Flags]
    public enum WorldFlags : byte
    {
        /// <summary>
        /// Default WorldFlags value.
        /// </summary>
        None       = 0,

        /// <summary>
        /// The main <see cref="World"/> for a game/application.
        /// This flag is combined with <see cref="Editor"/>, <see cref="Game"/> and <see cref="Simulation"/>.
        /// </summary>
        Live       = 1,

        /// <summary>
        /// Main <see cref="Live"/> <see cref="World"/> running in the Editor.
        /// </summary>
        Editor     = 1 << 1 | Live,

        /// <summary>
        /// Main <see cref="Live"/> <see cref="World"/> running in the Player.
        /// </summary>
        Game       = 1 << 2 | Live,

        /// <summary>
        /// Any additional <see cref="Live"/> <see cref="World"/> running in the application for background processes that
        /// queue up data for other <see cref="Live"/> <see cref="World"/> (ie. physics, AI simulation, networking, etc.).
        /// </summary>
        Simulation = 1 << 3 | Live,

        /// <summary>
        /// <see cref="World"/> on which conversion systems run to transform authoring data to runtime data.
        /// </summary>
        Conversion = 1 << 4,

        /// <summary>
        /// <see cref="World"/> in which temporary results are staged before being moved into a <see cref="Live"/> <see cref="World"/>.
        /// Typically combined with <see cref="Conversion"/> to represent an intermediate step in the full conversion process.
        /// </summary>
        Staging    = 1 << 5,

        /// <summary>
        /// <see cref="World"/> representing a previous state of another <see cref="World"/> typically to compute
        /// a diff of runtime data - for example useful for undo/redo or Live Link.
        /// </summary>
        Shadow     = 1 << 6,

        /// <summary>
        /// Dedicated <see cref="World"/> for managing incoming streamed data to the Player.
        /// </summary>
        Streaming  = 1 << 7,
    }

    /// <summary>
    /// When entering playmode or the game starts in the Player a default world is created.
    /// Sometimes you need multiple worlds to be setup when the game starts or perform some
    /// custom world initialization. This lets you override the bootstrap of game code world creation.
    /// </summary>
    public interface ICustomBootstrap
    {
        // Returns true if the bootstrap has performed initialization.
        // Returns false if default world initialization should be performed.
        bool Initialize(string defaultWorldName);
    }

    [DebuggerDisplay("{Name} - {Flags} (#{SequenceNumber})")]
    public partial class World : IDisposable
    {
        internal static readonly List<World> s_AllWorlds = new List<World>();

        public static World DefaultGameObjectInjectionWorld { get; set; }

    #if UNITY_DOTSPLAYER
        [Obsolete("use World.All instead. (RemovedAfter 2020-06-02)")]
        public static World[] AllWorlds => s_AllWorlds.ToArray();
    #else
        [Obsolete("use World.All instead. (RemovedAfter 2020-06-02)")]
        public static System.Collections.ObjectModel.ReadOnlyCollection<World> AllWorlds => new System.Collections.ObjectModel.ReadOnlyCollection<World>(s_AllWorlds);

        Dictionary<Type, ComponentSystemBase> m_SystemLookup = new Dictionary<Type, ComponentSystemBase>();
    #endif
    #if ENABLE_UNITY_COLLECTIONS_CHECKS
        bool m_AllowGetSystem = true;
    #endif
        public static NoAllocReadOnlyCollection<World> All { get; } = new NoAllocReadOnlyCollection<World>(s_AllWorlds);

        List<ComponentSystemBase> m_Systems = new List<ComponentSystemBase>();
        public NoAllocReadOnlyCollection<ComponentSystemBase> Systems { get; }

        EntityManager m_EntityManager;
        readonly ulong m_SequenceNumber;

        static int ms_SystemIDAllocator = 0;
        static ulong ms_NextSequenceNumber = 0;

        public readonly WorldFlags Flags;

        public string Name { get; }

        public override string ToString()
        {
            return Name;
        }

        public int Version { get; private set; }

        public EntityManager EntityManager => m_EntityManager;

        public bool IsCreated => m_Systems != null;

        public ulong SequenceNumber => m_SequenceNumber;

        protected TimeData m_CurrentTime;

        public ref TimeData Time => ref m_CurrentTime;

        protected EntityQuery m_TimeSingletonQuery;

        protected Entity TimeSingleton
        {
            get
            {
                if (m_TimeSingletonQuery.IsEmptyIgnoreFilter)
                {
        #if UNITY_EDITOR
                    var entity = EntityManager.CreateEntity(typeof(WorldTime), typeof(WorldTimeQueue));
                    EntityManager.SetName(entity , "WorldTime");
        #else
                    EntityManager.CreateEntity(typeof(WorldTime), typeof(WorldTimeQueue));
        #endif
                }

                return m_TimeSingletonQuery.GetSingletonEntity();
            }
        }

        public void SetTime(TimeData newTimeData)
        {
            EntityManager.SetComponentData(TimeSingleton, new WorldTime() {Time = newTimeData});
            m_CurrentTime = newTimeData;
        }

        public void PushTime(TimeData newTimeData)
        {
            var queue = EntityManager.GetBuffer<WorldTimeQueue>(TimeSingleton);
            queue.Add(new WorldTimeQueue() { Time = m_CurrentTime });
            SetTime(newTimeData);
        }

        public void PopTime()
        {
            var queue = EntityManager.GetBuffer<WorldTimeQueue>(TimeSingleton);

            Assert.IsTrue(queue.Length > 0, "PopTime without a matching PushTime");

            var prevTime = queue[queue.Length - 1];
            queue.RemoveAt(queue.Length - 1);
            SetTime(prevTime.Time);
        }

        public World(string name) : this(name, WorldFlags.Simulation)
        { }

        internal World(string name, WorldFlags flags)
        {
            Systems = new NoAllocReadOnlyCollection<ComponentSystemBase>(m_Systems);
            m_SequenceNumber = ms_NextSequenceNumber;
            ms_NextSequenceNumber++;

            // Debug.LogError("Create World "+ name + " - " + GetHashCode());
            Name = name;
            Flags = flags;
            s_AllWorlds.Add(this);

            m_EntityManager = new EntityManager(this);
            m_TimeSingletonQuery = EntityManager.CreateEntityQuery(ComponentType.ReadWrite<WorldTime>(),
                ComponentType.ReadWrite<WorldTimeQueue>());
        }

        public void Dispose()
        {
            if (!IsCreated)
                throw new ArgumentException("The World has already been Disposed.");
            // Debug.LogError("Dispose World "+ Name + " - " + GetHashCode());

            m_EntityManager.PreDisposeCheck();
            s_AllWorlds.Remove(this);

        #if ENABLE_UNITY_COLLECTIONS_CHECKS
            m_AllowGetSystem = false;
        #endif
            // Destruction should happen in reverse order to construction
            for (int i = m_Systems.Count - 1; i >= 0; --i)
            {
                try
                {
                    m_Systems[i].DestroyInstance();
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }

            // Destroy EntityManager last
            m_EntityManager.DestroyInstance();
            m_EntityManager = null;

            m_Systems.Clear();
            m_Systems = null;

        #if !UNITY_DOTSPLAYER
            m_SystemLookup.Clear();
            m_SystemLookup = null;
        #endif

            if (DefaultGameObjectInjectionWorld == this)
                DefaultGameObjectInjectionWorld = null;
        }

        public static void DisposeAllWorlds()
        {
            while (s_AllWorlds.Count != 0)
            {
                s_AllWorlds[0].Dispose();
            }
        }

        void AddTypeLookup(Type type, ComponentSystemBase system)
        {
        #if !UNITY_DOTSPLAYER
            while (type != typeof(ComponentSystemBase))
            {
                if (!m_SystemLookup.ContainsKey(type))
                    m_SystemLookup.Add(type, system);

                type = type.BaseType;
            }
        #endif
        }


    #if UNITY_DOTSPLAYER
        private ComponentSystemBase CreateSystemInternal<T>() where T : new()
        {
        #if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (!m_AllowGetSystem)
                throw new ArgumentException(
                    "During destruction of a system you are not allowed to create more systems.");

            m_AllowGetSystem = true;
        #endif
            ComponentSystemBase system;
            try
            {
        #if !NET_DOTS
                system = new T() as ComponentSystemBase;
        #else
                system = TypeManager.ConstructSystem(typeof(T));
        #endif
            }
            catch
            {
        #if ENABLE_UNITY_COLLECTIONS_CHECKS
                m_AllowGetSystem = false;
        #endif
                throw;
            }

            return AddSystem(system);
        }

        private ComponentSystemBase GetExistingSystemInternal<T>()
        {
            return GetExistingSystem(typeof(T));
        }

        private ComponentSystemBase GetExistingSystemInternal(Type type)
        {
        #if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (!IsCreated)
                throw new ArgumentException("During destruction ");
            if (!m_AllowGetSystem)
                throw new ArgumentException(
                    "During destruction of a system you are not allowed to get or create more systems.");
        #endif

            for (int i = 0; i < m_Systems.Count; ++i) {
                var mgr = m_Systems[i];
                if (type.IsAssignableFrom(mgr.GetType()))
                    return mgr;
            }

            return null;
        }

        private ComponentSystemBase GetOrCreateSystemInternal<T>() where T : new()
        {
            var system = GetExistingSystemInternal<T>();
            return system ?? CreateSystemInternal<T>();
        }

        public T CreateSystem<T>() where T : ComponentSystemBase, new()
        {
            return (T) CreateSystemInternal<T>();
        }

        public T GetOrCreateSystem<T>() where T : ComponentSystemBase, new()
        {
            return (T) GetOrCreateSystemInternal<T>();
        }

        public ComponentSystemBase GetOrCreateSystem(Type type)
        {
            CheckGetOrCreateSystem();

            var system = GetExistingSystem(type);
            return system ?? TypeManager.ConstructSystem(type);
        }
    #else
        ComponentSystemBase CreateSystemInternal(Type type, object[] constructorArguments)
        {
            if (!typeof(ComponentSystemBase).IsAssignableFrom(type))
            {
                throw new ArgumentException($"Type {type} must be derived from ComponentSystem or JobComponentSystem.");
            }

        #if ENABLE_UNITY_COLLECTIONS_CHECKS

            if (constructorArguments != null && constructorArguments.Length != 0)
            {
                var constructors =
                    type.GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (constructors.Length == 1 && constructors[0].IsPrivate)
                    throw new MissingMethodException(
                        $"Constructing {type} failed because the constructor was private, it must be public.");
            }

            m_AllowGetSystem = false;
        #endif
            ComponentSystemBase system;
            try
            {
                system = Activator.CreateInstance(type, constructorArguments) as ComponentSystemBase;
            }
            catch (MissingMethodException mme)
            {
                throw new MissingMethodException($"Constructing {type} failed because CreateSystem " +
                                $"parameters did not match its constructor.  [Job]ComponentSystem {type} must " +
                                "be mentioned in a link.xml file, or annotated with a [Preserve] attribute to " +
                                "prevent its constructor from being stripped.  See " +
                                "https://docs.unity3d.com/Manual/ManagedCodeStripping.html for more information.", mme);
            }
        #if ENABLE_UNITY_COLLECTIONS_CHECKS
            finally
            {
                m_AllowGetSystem = true;
            }
        #endif
            return AddSystem(system);
        }

        ComponentSystemBase GetExistingSystemInternal(Type type)
        {
            return m_SystemLookup.TryGetValue(type, out var system) ? system : null;
        }

        ComponentSystemBase GetOrCreateSystemInternal(Type type)
        {
            var system = GetExistingSystemInternal(type);

            return system ?? CreateSystemInternal(type, null);
        }

        public ComponentSystemBase CreateSystem(Type type, params object[] constructorArguments)
        {
            CheckGetOrCreateSystem();

            return CreateSystemInternal(type, constructorArguments);
        }

        public T CreateSystem<T>(params object[] constructorArguments) where T : ComponentSystemBase
        {
            CheckGetOrCreateSystem();

            return (T) CreateSystemInternal(typeof(T), constructorArguments);
        }

        public T GetOrCreateSystem<T>() where T : ComponentSystemBase
        {
            CheckGetOrCreateSystem();

            return (T) GetOrCreateSystemInternal(typeof(T));
        }

        public ComponentSystemBase GetOrCreateSystem(Type type)
        {
            CheckGetOrCreateSystem();

            return GetOrCreateSystemInternal(type);
        }
    #endif

        void RemoveSystemInternal(ComponentSystemBase system)
        {
            if (!m_Systems.Remove(system))
                throw new ArgumentException($"System does not exist in the world");
            ++Version;

        #if !UNITY_DOTSPLAYER
            var type = system.GetType();
            while (type != typeof(ComponentSystemBase))
            {
                if (m_SystemLookup[type] == system)
                {
                    m_SystemLookup.Remove(type);

                    foreach (var otherSystem in m_Systems)
                        if (otherSystem.GetType().IsSubclassOf(type))
                            AddTypeLookup(otherSystem.GetType(), otherSystem);
                }

                type = type.BaseType;
            }
        #endif
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        void CheckGetOrCreateSystem()
        {
            if (!IsCreated)
            {
                throw new ArgumentException("The World has already been Disposed.");
            }
        #if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (!m_AllowGetSystem)
            {
                throw new ArgumentException("You are not allowed to get or create more systems during destruction and constructor of a system.");
            }
        #endif
        }

        public T AddSystem<T>(T system) where T : ComponentSystemBase
        {
            CheckGetOrCreateSystem();

            m_Systems.Add(system);
            AddTypeLookup(system.GetType(), system);

            try
            {
                system.CreateInstance(this);
            }
            catch
            {
                RemoveSystemInternal(system);
                throw;
            }
            ++Version;
            return system;
        }

        public T GetExistingSystem<T>() where T : ComponentSystemBase
        {
            CheckGetOrCreateSystem();

            return (T)GetExistingSystemInternal(typeof(T));
        }

        public ComponentSystemBase GetExistingSystem(Type type)
        {
            CheckGetOrCreateSystem();

            return GetExistingSystemInternal(type);
        }

        public void DestroySystem(ComponentSystemBase system)
        {
            CheckGetOrCreateSystem();

            RemoveSystemInternal(system);
            system.DestroyInstance();
        }

        internal static int AllocateSystemID()
        {
            return ++ms_SystemIDAllocator;
        }

        public bool QuitUpdate { get; set; }

        public void Update()
        {
            GetExistingSystem<InitializationSystemGroup>()?.Update();
            GetExistingSystem<SimulationSystemGroup>()?.Update();
            GetExistingSystem<PresentationSystemGroup>()?.Update();

        #if ENABLE_UNITY_COLLECTIONS_CHECKS
            Assert.IsTrue(EntityManager.GetBuffer<WorldTimeQueue>(TimeSingleton).Length == 0, "PushTime without matching PopTime");
        #endif
        }

        /// <summary>
        /// Read only collection that doesn't generate garbage when used in a foreach.
        /// </summary>
        public struct NoAllocReadOnlyCollection<T> : IEnumerable<T>
        {
            readonly List<T> m_Source;

            public NoAllocReadOnlyCollection(List<T> source) => m_Source = source;

            public int Count => m_Source.Count;

            public T this[int index] => m_Source[index];

            public List<T>.Enumerator GetEnumerator() => m_Source.GetEnumerator();

            public bool Contains(T item) => m_Source.Contains(item);

            IEnumerator<T> IEnumerable<T>.GetEnumerator()
                => throw new NotSupportedException($"To avoid boxing, do not cast {nameof(NoAllocReadOnlyCollection<T>)} to IEnumerable<T>.");
            IEnumerator IEnumerable.GetEnumerator()
                => throw new NotSupportedException($"To avoid boxing, do not cast {nameof(NoAllocReadOnlyCollection<T>)} to IEnumerable.");
        }
    }
}
