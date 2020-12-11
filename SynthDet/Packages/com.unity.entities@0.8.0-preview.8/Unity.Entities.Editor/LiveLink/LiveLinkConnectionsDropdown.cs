using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using Unity.Scenes.Editor;
using UnityEditor;
using UnityEditor.Networking.PlayerConnection;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using PopupWindow = UnityEditor.PopupWindow;

namespace Unity.Entities.Editor
{
    class LiveLinkConnectionsDropdown : PopupWindowContent, IDisposable
    {
        readonly ObservableCollection<LiveLinkConnection> m_LinkConnections = new ObservableCollection<LiveLinkConnection>();
        readonly LiveLinkConnectionsView m_ConnectionsView;

        public LiveLinkConnectionsDropdown()
        {
            EditorSceneLiveLinkToPlayerSendSystem.instance.LiveLinkPlayerConnected += OnPlayerConnected;
            EditorSceneLiveLinkToPlayerSendSystem.instance.LiveLinkPlayerDisconnected += OnPlayerDisconnected;

            foreach (var connectedPlayer in EditorConnection.instance.ConnectedPlayers)
            {
                var playerId = connectedPlayer.playerId;
                var buildConfigurationGuid = EditorSceneLiveLinkToPlayerSendSystem.instance.GetBuildConfigurationGUIDForLiveLinkConnection(playerId);

                m_LinkConnections.Add(new LiveLinkConnection(playerId, connectedPlayer.name, LiveLinkConnectionStatus.Connected, buildConfigurationGuid));
            }

            m_ConnectionsView = new LiveLinkConnectionsView(m_LinkConnections, GetGroupName);
            m_ConnectionsView.DisablePlayer += DisablePlayer;
            m_ConnectionsView.ResetPlayer += ResetPlayer;
        }

        string GetGroupName(Hash128 buildConfigurationGuid)
        {
            if (buildConfigurationGuid != default)
            {
                var path = AssetDatabase.GUIDToAssetPath(buildConfigurationGuid.ToString());
                if (!string.IsNullOrEmpty(path))
                    return Path.GetFileNameWithoutExtension(path);
            }

            return "Unknown";
        }

        void OnPlayerConnected(int playerId, Hash128 buildConfigurationGuid)
        {
            var connectedPlayer = EditorConnection.instance.ConnectedPlayers.Find(x => x.playerId == playerId);
            if (connectedPlayer == null)
                return;

            var existingConnection = m_LinkConnections.FirstOrDefault(x => x.PlayerId == playerId);
            if (existingConnection != null)
            {
                existingConnection.Status = LiveLinkConnectionStatus.Connected;
                existingConnection.BuildConfigurationGuid = buildConfigurationGuid;
                existingConnection.Name = connectedPlayer.name;
            }
            else
                m_LinkConnections.Add(new LiveLinkConnection(connectedPlayer.playerId, connectedPlayer.name, LiveLinkConnectionStatus.Connected, buildConfigurationGuid));
        }

        void OnPlayerDisconnected(int playerId)
            => m_LinkConnections.Remove(m_LinkConnections.Single(x => x.PlayerId == playerId));

        public void DrawDropdown()
        {
            var dropdownRect = new Rect(130, 0, 40, 22);
            var hasConnectedDevices = m_LinkConnections.Any(c => c.Status == LiveLinkConnectionStatus.Connected);
            var icon = hasConnectedDevices ? Icons.LiveLinkOn : Icons.LiveLink;
            icon.tooltip = hasConnectedDevices
                ? "View linked devices."
                : "No devices currently linked. Create a Live Link build to connect a device.";

            if (EditorGUI.DropdownButton(dropdownRect, icon, FocusType.Keyboard, LiveLinkStyles.Dropdown))
            {
                PopupWindow.Show(dropdownRect, this);
            }
        }

        public override Vector2 GetWindowSize() => SizeHelper.GetDropdownSize(m_LinkConnections);

        public override void OnOpen() => m_ConnectionsView.BuildFullUI(editorWindow.rootVisualElement);

        void ResetPlayer(LiveLinkConnection connection)
        {
            connection.Status = LiveLinkConnectionStatus.Reseting;
            EditorSceneLiveLinkToPlayerSendSystem.instance.ResetPlayer(connection.PlayerId);
        }

        void DisablePlayer(LiveLinkConnection connection)
        {
            connection.Status = LiveLinkConnectionStatus.Disabled;
            EditorSceneLiveLinkToPlayerSendSystem.instance.DisableSendForPlayer(connection.PlayerId);
        }

        public override void OnGUI(Rect rect) { }

        public void Dispose()
        {
            EditorSceneLiveLinkToPlayerSendSystem.instance.LiveLinkPlayerConnected -= OnPlayerConnected;
            EditorSceneLiveLinkToPlayerSendSystem.instance.LiveLinkPlayerDisconnected -= OnPlayerDisconnected;
            m_ConnectionsView.DisablePlayer -= DisablePlayer;
            m_ConnectionsView.ResetPlayer -= ResetPlayer;
            m_ConnectionsView.Dispose();
        }

        internal class LiveLinkConnection
        {
            LiveLinkConnectionStatus m_Status;
            Hash128 m_BuildConfigurationGuid;
            string m_Name;

            public event Action<LiveLinkConnection, LiveLinkConnectionStatus> StatusChanged;
            public event Action<LiveLinkConnection> BuildConfigurationGuidChanged;
            public event Action<LiveLinkConnection> NameChanged;

            public LiveLinkConnection(int playerId, string name, LiveLinkConnectionStatus status, Hash128 buildConfigurationGuid)
            {
                PlayerId = playerId;
                Name = name;
                m_Status = status;
                BuildConfigurationGuid = buildConfigurationGuid;
            }

            public int PlayerId { get; }

            public string Name
            {
                get => m_Name;
                set
                {
                    if (m_Name == value) return;

                    m_Name = value;
                    NameChanged?.Invoke(this);
                }
            }

            public LiveLinkConnectionStatus Status
            {
                get => m_Status;
                set
                {
                    if (m_Status == value) return;

                    var previousValue = m_Status;
                    m_Status = value;
                    StatusChanged?.Invoke(this, previousValue);
                }
            }

            public Hash128 BuildConfigurationGuid
            {
                get => m_BuildConfigurationGuid;
                set
                {
                    if (m_BuildConfigurationGuid == value) return;

                    m_BuildConfigurationGuid = value;
                    BuildConfigurationGuidChanged?.Invoke(this);
                }
            }
        }

        internal class LiveLinkConnectionsView : IDisposable
        {
            const string kBaseUssClass = "live-link-connections-dropdown";
            const string kBasePath = "Packages/com.unity.entities/Editor/LiveLink";

            readonly ObservableCollection<LiveLinkConnection> m_Connections;
            readonly Func<Hash128, string> m_GetGroupName;
            readonly UIElementHelpers.VisualElementTemplate m_GroupTemplate;
            readonly UIElementHelpers.VisualElementTemplate m_ConnectionTemplate;
            readonly Dictionary<int, VisualElement> m_ConnectionsUIPerPlayerId = new Dictionary<int, VisualElement>();

            VisualElement m_Root, m_Groups;

            public event Action<LiveLinkConnection> DisablePlayer;
            public event Action<LiveLinkConnection> ResetPlayer;

            public LiveLinkConnectionsView(ObservableCollection<LiveLinkConnection> connections, Func<Hash128, string> getGroupName)
            {
                m_Connections = connections;
                m_GetGroupName = getGroupName;
                m_GroupTemplate = UIElementHelpers.LoadClonableTemplate(kBasePath, "LiveLinkConnectionsDropdown.GroupTemplate");
                m_ConnectionTemplate = UIElementHelpers.LoadClonableTemplate(kBasePath, "LiveLinkConnectionsDropdown.GroupItemTemplate");

                m_Connections.CollectionChanged += OnConnectionsCollectionChanged;
                foreach (var connection in m_Connections) Subscribe(connection);
            }

            void Subscribe(LiveLinkConnection connection)
            {
                connection.NameChanged += OnConnectionNameChanged;
                connection.StatusChanged += OnConnectionStatusChanged;
                connection.BuildConfigurationGuidChanged += OnConnectionBuildConfigurationChanged;
            }

            void Unsubscribe(LiveLinkConnection connection)
            {
                connection.NameChanged -= OnConnectionNameChanged;
                connection.StatusChanged -= OnConnectionStatusChanged;
                connection.BuildConfigurationGuidChanged -= OnConnectionBuildConfigurationChanged;
            }

            void OnConnectionsCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
            {
                if (e.Action == NotifyCollectionChangedAction.Add)
                {
                    foreach (var connection in e.NewItems) Subscribe((LiveLinkConnection) connection);
                }
                else if (e.Action == NotifyCollectionChangedAction.Remove
                         || e.Action == NotifyCollectionChangedAction.Replace
                         || e.Action == NotifyCollectionChangedAction.Reset)
                {
                    foreach (var connection in e.OldItems) Unsubscribe((LiveLinkConnection) connection);
                }
            }

            void OnConnectionNameChanged(LiveLinkConnection connection)
            {
                var visualElement = m_ConnectionsUIPerPlayerId[connection.PlayerId];
                UpdateConnectionNameUI(connection, visualElement);
            }

            void OnConnectionStatusChanged(LiveLinkConnection connection, LiveLinkConnectionStatus previousValue)
            {
                var visualElement = m_ConnectionsUIPerPlayerId[connection.PlayerId];
                UpdateConnectionStatusUI(connection, visualElement);
                UpdateConnectionButtonsUI(connection, visualElement);

                LiveLinkToolbar.RepaintPlaybar();
            }

            void OnConnectionBuildConfigurationChanged(LiveLinkConnection connection)
                => BuildConnectionListUI();

            public void BuildFullUI(VisualElement root)
            {
                if (root == null)
                    return;

                SizeHelper.ResetDropdownSize();
                m_Root = root;
                m_Root.Clear();
                var container = UIElementHelpers.LoadTemplate(kBasePath, "LiveLinkConnectionsDropdown");
                m_Groups = container.Q<VisualElement>(kBaseUssClass + "__Groups");
                var emptyMessage = container.Q<Label>(kBaseUssClass + "__EmptyMessage");

                var resetButton = container.Q<Button>(kBaseUssClass + "__footer__reset");
                resetButton.clickable.clicked += () =>
                {
                    foreach (var connection in m_Connections) ResetPlayer?.Invoke(connection);
                };
                var disableButton = container.Q<Button>(kBaseUssClass + "__footer__disable");
                disableButton.clickable.clicked += () =>
                {
                    foreach (var connection in m_Connections) DisablePlayer?.Invoke(connection);
                };
                container.Q<Button>(kBaseUssClass + "__footer__start").clickable.clicked += StartLiveLinkWindow.OpenWindow;

                if (m_Connections.Count == 0)
                {
                    emptyMessage.SetEnabled(false);
                    resetButton.SetEnabled(false);
                    disableButton.SetEnabled(false);
                    emptyMessage.Show();
                    m_Groups.Hide();
                }
                else
                {
                    resetButton.SetEnabled(true);
                    disableButton.SetEnabled(true);
                    emptyMessage.Hide();
                    m_Groups.Show();

                    BuildConnectionListUI();
                }

                container.visible = false;
                container.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
                m_Root.Add(container);
            }

            void OnGeometryChanged(GeometryChangedEvent evt)
            {
                if (evt.newRect.size != SizeHelper.GetDropdownSize(m_Connections))
                {
                    SizeHelper.OverrideDropdownSize(evt.newRect.size);
                    LiveLinkToolbar.RepaintPlaybar();
                }

                ((VisualElement) evt.target).visible = true;
            }

            void BuildConnectionListUI()
            {
                if (m_Root == null)
                    return;

                m_ConnectionsUIPerPlayerId.Clear();
                m_Groups.Clear();
                foreach (var connectionGroup in m_Connections.GroupBy(x => x.BuildConfigurationGuid))
                {
                    var groupVisualElement = m_GroupTemplate.GetNewInstance();
                    groupVisualElement.Q<Label>().text = m_GetGroupName(connectionGroup.Key);
                    var groupedDevices = groupVisualElement.Q<VisualElement>(kBaseUssClass + "__GroupedDevices");
                    var connectionCount = 0;
                    foreach (var connection in connectionGroup)
                    {
                        connectionCount++;
                        var connectionVisualElement = m_ConnectionTemplate.GetNewInstance();
                        m_ConnectionsUIPerPlayerId.Add(connection.PlayerId, connectionVisualElement);
                        UpdateConnectionStatusUI(connection, connectionVisualElement);
                        UpdateConnectionNameUI(connection, connectionVisualElement);
                        UpdateConnectionButtonsUI(connection, connectionVisualElement);

                        connectionVisualElement.Q<Button>(className: kBaseUssClass + "__groups__item__device__reset-button").clickable.clicked += () => ResetPlayer?.Invoke(connection);
                        connectionVisualElement.Q<Button>(className: kBaseUssClass + "__groups__item__device__disable-button").clickable.clicked += () => DisablePlayer?.Invoke(connection);
                        connectionVisualElement.Q<Button>(className: kBaseUssClass + "__groups__item__device__reset-when-disabled-button").clickable.clicked += () => ResetPlayer?.Invoke(connection);

                        connectionVisualElement.RegisterCallback<MouseEnterEvent>(_ =>
                        {
                            connectionVisualElement.EnableInClassList(kBaseUssClass + "__groups__item__device__hover", true);
                            UpdateConnectionButtonsUI(connection, connectionVisualElement);
                        });
                        connectionVisualElement.RegisterCallback<MouseLeaveEvent>(_ =>
                        {
                            connectionVisualElement.EnableInClassList(kBaseUssClass + "__groups__item__device__hover", false);
                            UpdateConnectionButtonsUI(connection, connectionVisualElement);
                        });
                        groupedDevices.Add(connectionVisualElement);
                    }

                    groupVisualElement.Query<ToolbarMenu>().ForEach(m =>
                    {
                        m.menu.AppendAction(connectionCount > 1 ? "Reset linked players" : "Reset linked player", _ =>
                        {
                            foreach (var connection in connectionGroup) ResetPlayer?.Invoke(connection);
                        });
                        m.menu.AppendAction(connectionCount > 1 ? "Disable links" : "Disable link", _ =>
                        {
                            foreach (var connection in connectionGroup) DisablePlayer?.Invoke(connection);
                        });
                    });

                    m_Groups.Add(groupVisualElement);
                }
            }

            void UpdateConnectionButtonsUI(LiveLinkConnection connection, VisualElement connectionVisualElement)
            {
                var isMouseOver = connectionVisualElement.ClassListContains(kBaseUssClass + "__groups__item__device__hover");
                var resetButton = connectionVisualElement.Q<Button>(className: kBaseUssClass + "__groups__item__device__reset-button");
                var disableButton = connectionVisualElement.Q<Button>(className: kBaseUssClass + "__groups__item__device__disable-button");
                var resetWhenDisabled = connectionVisualElement.Q<Button>(className: kBaseUssClass + "__groups__item__device__reset-when-disabled-button");

                if (connection.Status == LiveLinkConnectionStatus.Disabled)
                {
                    resetButton.Hide();
                    disableButton.Hide();
                    resetWhenDisabled.Show();
                }
                else if (isMouseOver)
                {
                    resetButton.Show();
                    disableButton.Show();
                    resetWhenDisabled.Hide();
                }
                else
                {
                    resetButton.Hide();
                    disableButton.Hide();
                    resetWhenDisabled.Hide();
                }
            }

            void UpdateConnectionNameUI(LiveLinkConnection connection, VisualElement visualElement)
            {
                if (m_Root == null)
                    return;

                var connectionName = visualElement.Q<Label>();
                connectionName.text = connection.Name.Length <= SizeHelper.MaxCharCount ? connection.Name : connection.Name.Substring(0, SizeHelper.MaxCharCount) + "...";
                if (connection.Name.Length > SizeHelper.MaxCharCount)
                    connectionName.tooltip = connection.Name;
            }

            void UpdateConnectionStatusUI(LiveLinkConnection connection, VisualElement visualElement)
            {
                if (m_Root == null)
                    return;

                var connectionIcon = visualElement.Q<Image>();
                connectionIcon.ClearClassList();
                connectionIcon.AddToClassList(GetStatusClass(connection.Status));
            }

            static string GetStatusClass(LiveLinkConnectionStatus connectionStatus)
            {
                switch (connectionStatus)
                {
                    case LiveLinkConnectionStatus.Error:
                        return kBaseUssClass + "__status--error";
                    case LiveLinkConnectionStatus.Connected:
                        return kBaseUssClass + "__status--connected";
                    case LiveLinkConnectionStatus.Disabled:
                        return kBaseUssClass + "__status--disabled";
                    case LiveLinkConnectionStatus.Reseting:
                        return kBaseUssClass + "__status--reseting";
                    default:
                        return null;
                }
            }

            public void Dispose()
            {
                m_Connections.CollectionChanged -= OnConnectionsCollectionChanged;
                foreach (var connection in m_Connections) Unsubscribe(connection);
                m_ConnectionsUIPerPlayerId.Clear();
            }
        }

        internal enum LiveLinkConnectionStatus
        {
            Connected,
            Disabled,
            Error,
            Reseting
        }

        static class SizeHelper
        {
            static readonly Vector2 s_EmptyDropdownSize = new Vector2(256, 104);
            const int Width = 256, ItemHeight = 22, GroupHeaderHeight = 21, FooterHeight = 75, BordersHeight = 4;
            internal const int MaxCharCount = 30;

            static Vector2 s_OverridenSize;

            public static Vector2 GetDropdownSize(IReadOnlyCollection<LiveLinkConnection> connections)
            {
                if (s_OverridenSize != default)
                    return s_OverridenSize;

                if (connections.Count == 0)
                    return s_EmptyDropdownSize;

                var groupCounts = connections.GroupBy(x => x.BuildConfigurationGuid).Count();
                return new Vector2(Width, BordersHeight + FooterHeight + groupCounts * GroupHeaderHeight + connections.Count * ItemHeight);
            }

            public static void OverrideDropdownSize(Vector2 contentSize) => s_OverridenSize = contentSize;

            public static void ResetDropdownSize() => s_OverridenSize = default;
        }
    }
}