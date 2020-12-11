// Copyright (c) 2018, Yves Goergen, https://unclassified.software
//
// Copying and distribution of this file, with or without modification, are permitted provided the
// copyright notice and this notice are preserved. This file is offered as-is, without any warranty.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using UnityEngine;

namespace Unclassified.Net
{
	/// <summary>
	/// Listens asynchronously for connections from TCP network clients.
	/// </summary>
	public class AsyncTcpListener
	{
		#region Private data

		private TcpListener tcpListener;
		private volatile bool isStopped;
		private bool closeClients;

		#endregion Private data

		#region Constructors

		/// <summary>
		/// Initialises a new instance of the <see cref="AsyncTcpListener"/> class.
		/// </summary>
		public AsyncTcpListener()
		{
			// Just for the documentation
		}

		#endregion Constructors

		#region Events

		/// <summary>
		/// Occurs when a trace message is available.
		/// </summary>
		public event EventHandler<AsyncTcpEventArgs> Message;

		#endregion Events

		#region Properties

		/// <summary>
		/// Gets or sets the local IP address to listen on. Default is all network interfaces.
		/// </summary>
		public IPAddress IPAddress { get; set; } = IPAddress.IPv6Any;

		/// <summary>
		/// Gets or sets the port on which to listen for incoming connection attempts.
		/// </summary>
		public int Port { get; set; }

		/// <summary>
		/// Called when a pending connection request was accepted. When this method completes, the
		/// client connection will be closed.
		/// </summary>
		/// <remarks>
		/// This callback method may not be called when the <see cref="OnClientConnected"/> method
		/// is overridden by a derived class.
		/// </remarks>
		public Func<TcpClient, Task> ClientConnectedCallback { get; set; }

		#endregion Properties

		#region Public methods

		/// <summary>
		/// Starts listening asynchronously for incoming connection requests.
		/// </summary>
		/// <returns>The task object representing the asynchronous operation.</returns>
		public async Task RunAsync()
		{
			if (tcpListener != null)
				throw new InvalidOperationException("The listener is already running.");
			if (Port <= 0 || Port > ushort.MaxValue)
				throw new ArgumentOutOfRangeException(nameof(Port));

			isStopped = false;
			closeClients = false;

			tcpListener = new TcpListener(IPAddress, Port);
			tcpListener.Server.DualMode = true;
			tcpListener.Start();
			Message?.Invoke(this, new AsyncTcpEventArgs("Waiting for connections"));

			var clients = new ConcurrentDictionary<TcpClient, bool>();   // bool is dummy, never regarded
			var clientTasks = new List<Task>();
			try
			{
				while (true)
				{
					TcpClient tcpClient;
					try
					{
						tcpClient = await tcpListener.AcceptTcpClientAsync();
					}
					catch (ObjectDisposedException) when (isStopped)
					{
						// Listener was stopped
						break;
					}
					var endpoint = tcpClient.Client.RemoteEndPoint;
					Message?.Invoke(this, new AsyncTcpEventArgs("Client connected from " + endpoint));
					clients.TryAdd(tcpClient, true);
					var clientTask = Task.Run(async () =>
					{
						await OnClientConnected(tcpClient);
						tcpClient.Dispose();
						Message?.Invoke(this, new AsyncTcpEventArgs("Client disconnected from " + endpoint));
						clients.TryRemove(tcpClient, out _);
					});
					clientTasks.Add(clientTask);
				}
			}
			finally
			{
				if (closeClients)
				{
					Message?.Invoke(this, new AsyncTcpEventArgs("Shutting down, closing all client connections"));
					foreach (var tcpClient in clients.Keys)
					{
						tcpClient.Dispose();
					}
					await Task.WhenAll(clientTasks);
					Message?.Invoke(this, new AsyncTcpEventArgs("All client connections completed"));
				}
				else
				{
					Message?.Invoke(this, new AsyncTcpEventArgs("Shutting down, client connections remain open"));
				}
				clientTasks.Clear();
				tcpListener = null;
			}
		}

		/// <summary>
		/// Closes the listener.
		/// </summary>
		/// <param name="closeClients">Specifies whether accepted connections should be closed, too.</param>
		public void Stop(bool closeClients)
		{
			if (tcpListener == null)
				throw new InvalidOperationException("The listener is not started.");

			this.closeClients = closeClients;
			isStopped = true;
			tcpListener.Stop();
		}

		#endregion Public methods

		#region Protected virtual methods

		/// <summary>
		/// Called when a pending connection request was accepted. When this method completes, the
		/// client connection will be closed.
		/// </summary>
		/// <param name="tcpClient">The <see cref="TcpClient"/> that represents the accepted connection.</param>
		/// <returns>The task object representing the asynchronous operation.</returns>
		protected virtual Task OnClientConnected(TcpClient tcpClient)
		{
			if (ClientConnectedCallback != null)
			{
				return ClientConnectedCallback(tcpClient);
			}
			return Task.CompletedTask;
		}

		#endregion Protected virtual methods
	}

	/// <summary>
	/// Listens asynchronously for connections from TCP network clients.
	/// </summary>
	/// <typeparam name="TClient">The type to instantiate for accepted connection requests.</typeparam>
	public class AsyncTcpListener<TClient>
		: AsyncTcpListener
		where TClient : AsyncTcpClient, new()
	{
		#region Constructors

		/// <summary>
		/// Initialises a new instance of the <see cref="AsyncTcpListener{TClient}"/> class.
		/// </summary>
		public AsyncTcpListener()
		{
		}

		#endregion Constructors

		#region Overridden methods

		/// <summary>
		/// Instantiates a new <see cref="AsyncTcpClient"/> instance of the type
		/// <typeparamref name="TClient"/> that runs the accepted connection.
		/// </summary>
		/// <param name="tcpClient">The <see cref="TcpClient"/> that represents the accepted connection.</param>
		/// <returns>The task object representing the asynchronous operation.</returns>
		/// <remarks>
		/// This implementation does not call the <see cref="OnClientConnected"/> callback method.
		/// </remarks>
		protected override Task OnClientConnected(TcpClient tcpClient)
		{
			var client = new TClient
			{
				ServerTcpClient = tcpClient
			};
			return client.RunAsync();
		}

		#endregion Overridden methods
	}
}
