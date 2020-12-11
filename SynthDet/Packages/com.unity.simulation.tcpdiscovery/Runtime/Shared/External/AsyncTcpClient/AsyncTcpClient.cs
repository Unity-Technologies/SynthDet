// Copyright (c) 2018, Yves Goergen, https://unclassified.software
//
// Copying and distribution of this file, with or without modification, are permitted provided the
// copyright notice and this notice are preserved. This file is offered as-is, without any warranty.

using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Unclassified.Util;
using UnityEngine;

namespace Unclassified.Net
{
	/// <summary>
	/// Provides asynchronous client connections for TCP network services.
	/// </summary>
	/// <remarks>
	/// This class can be used directly when setting the relevant callback methods
	/// <see cref="ConnectedCallback"/>, <see cref="ClosedCallback"/> or
	/// <see cref="ReceivedCallback"/>. Alternatively, a class inheriting from
	/// <see cref="AsyncTcpClient"/> can implement the client logic by overriding the protected
	/// methods.
	/// </remarks>
	public class AsyncTcpClient : IDisposable
	{
		#region Private data

		private TcpClient tcpClient;
		private NetworkStream stream;
		private TaskCompletionSource<bool> closedTcs = new TaskCompletionSource<bool>();

		#endregion Private data

		#region Constructors

		/// <summary>
		/// Initialises a new instance of the <see cref="AsyncTcpClient"/> class.
		/// </summary>
		public AsyncTcpClient()
		{
			closedTcs.SetResult(true);
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
		/// Gets or sets the <see cref="TcpClient"/> to use. Only for client connections that were
		/// accepted by an <see cref="AsyncTcpListener"/>.
		/// </summary>
		public TcpClient ServerTcpClient { get; set; }

		/// <summary>
		/// Gets or sets the amount of time an <see cref="AsyncTcpClient"/> will wait to connect
		/// once a connection operation is initiated.
		/// </summary>
		public TimeSpan ConnectTimeout { get; set; } = TimeSpan.FromSeconds(5);

		/// <summary>
		/// Gets or sets the maximum amount of time an <see cref="AsyncTcpClient"/> will wait to
		/// connect once a repeated connection operation is initiated. The actual connection
		/// timeout is increased with every try and reset when a connection is established.
		/// </summary>
		public TimeSpan MaxConnectTimeout { get; set; } = TimeSpan.FromMinutes(1);

		/// <summary>
		/// Gets or sets a value indicating whether the client should try to reconnect after the
		/// connection was closed.
		/// </summary>
		public bool AutoReconnect { get; set; }

		/// <summary>
		/// Gets or sets the name of the host to connect to.
		/// </summary>
		public string HostName { get; set; }

		/// <summary>
		/// Gets or sets the IP address of the host to connect to.
		/// Only regarded if <see cref="HostName"/> is null or empty.
		/// </summary>
		public IPAddress IPAddress { get; set; }

		/// <summary>
		/// Gets or sets the port number of the remote host.
		/// </summary>
		public int Port { get; set; }

		/// <summary>
		/// Gets the buffer of data that was received from the remote host.
		/// </summary>
		public ByteBuffer ByteBuffer { get; private set; } = new ByteBuffer();

		/// <summary>
		/// A <see cref="Task"/> that can be awaited to close the connection. This task will
		/// complete when the connection was closed remotely.
		/// </summary>
		public Task ClosedTask => closedTcs.Task;

		/// <summary>
		/// Gets a value indicating whether the <see cref="ClosedTask"/> has completed.
		/// </summary>
		public bool IsClosing => ClosedTask.IsCompleted;

		/// <summary>
		/// Called when the client has connected to the remote host. This method can implement the
		/// communication logic to execute when the connection was established. The connection will
		/// not be closed before this method completes.
		/// </summary>
		/// <remarks>
		/// This callback method may not be called when the <see cref="OnConnectedAsync"/> method
		/// is overridden by a derived class.
		/// </remarks>
		public Func<AsyncTcpClient, bool, Task> ConnectedCallback { get; set; }

		/// <summary>
		/// Called when the connection was closed. The parameter specifies whether the connection
		/// was closed by the remote host.
		/// </summary>
		/// <remarks>
		/// This callback method may not be called when the <see cref="OnClosed"/> method is
		/// overridden by a derived class.
		/// </remarks>
		public Action<AsyncTcpClient, bool> ClosedCallback { get; set; }

		/// <summary>
		/// Called when data was received from the remote host. The parameter specifies the number
		/// of bytes that were received. This method can implement the communication logic to
		/// execute every time data was received. New data will not be received before this method
		/// completes.
		/// </summary>
		/// <remarks>
		/// This callback method may not be called when the <see cref="OnReceivedAsync"/> method
		/// is overridden by a derived class.
		/// </remarks>
		public Func<AsyncTcpClient, int, Task> ReceivedCallback { get; set; }

		#endregion Properties

		#region Public methods

		/// <summary>
		/// Runs the client connection asynchronously.
		/// </summary>
		/// <returns>The task object representing the asynchronous operation.</returns>
		public async Task RunAsync()
		{
			bool isReconnected = false;
			int reconnectTry = -1;
			do
			{
				reconnectTry++;
				ByteBuffer = new ByteBuffer();
				if (ServerTcpClient != null)
				{
					Debug.Log("Take accepted connection from the listener");
					// Take accepted connection from listener
					tcpClient = ServerTcpClient;
				}
				else
				{
					// Try to connect to remote host
					var connectTimeout = TimeSpan.FromTicks(ConnectTimeout.Ticks + (MaxConnectTimeout.Ticks - ConnectTimeout.Ticks) / 20 * Math.Min(reconnectTry, 20));
					tcpClient = new TcpClient(AddressFamily.InterNetworkV6);
					tcpClient.Client.DualMode = true;
					Message?.Invoke(this, new AsyncTcpEventArgs("Connecting to server"));
					Task connectTask;
					if (!string.IsNullOrWhiteSpace(HostName))
					{
						connectTask = tcpClient.ConnectAsync(HostName, Port);
					}
					else
					{
						connectTask = tcpClient.ConnectAsync(IPAddress, Port);
					}
					var timeoutTask = Task.Delay(connectTimeout);
					if (await Task.WhenAny(connectTask, timeoutTask) == timeoutTask)
					{
						Message?.Invoke(this, new AsyncTcpEventArgs("Connection timeout"));
						continue;
					}
					try
					{
						await connectTask;
					}
					catch (Exception ex)
					{
						Message?.Invoke(this, new AsyncTcpEventArgs("Error connecting to remote host", ex));
						await timeoutTask;
						continue;
					}
				}
				reconnectTry = -1;
				stream = tcpClient.GetStream();

				// Read until the connection is closed.
				// A closed connection can only be detected while reading, so we need to read
				// permanently, not only when we might use received data.
				var networkReadTask = Task.Run(async () =>
				{
					// 10 KiB should be enough for every Ethernet packet
					byte[] buffer = new byte[10240];
					while (true)
					{
						int readLength;
						try
						{
							readLength = await stream.ReadAsync(buffer, 0, buffer.Length);
						}
						catch (IOException ex) when ((ex.InnerException as SocketException)?.ErrorCode == (int)SocketError.OperationAborted)
						{
							// Warning: This error code number (995) may change.
							// See https://docs.microsoft.com/en-us/windows/desktop/winsock/windows-sockets-error-codes-2
							Message?.Invoke(this, new AsyncTcpEventArgs("Connection closed locally", ex));
							readLength = -1;
						}
						catch (IOException ex) when ((ex.InnerException as SocketException)?.ErrorCode == (int)SocketError.ConnectionAborted)
						{
							Message?.Invoke(this, new AsyncTcpEventArgs("Connection aborted", ex));
							readLength = -1;
						}
						catch (IOException ex) when ((ex.InnerException as SocketException)?.ErrorCode == (int)SocketError.ConnectionReset)
						{
							Message?.Invoke(this, new AsyncTcpEventArgs("Connection reset remotely", ex));
							readLength = -2;
						}
						if (readLength <= 0)
						{
							if (readLength == 0)
							{
								Message?.Invoke(this, new AsyncTcpEventArgs("Connection closed remotely"));
							}
							closedTcs.TrySetResult(true);
							OnClosed(readLength != -1);
							return;
						}

						using (var ms = new MemoryStream(buffer))
						{
							using (var serializer = new BinaryReader(ms))
							{
								var messageLen = serializer.ReadUInt32();
								var remainingData = messageLen - (readLength + 4);
								var segment = new ArraySegment<byte>(buffer,4 , (int)messageLen);
								ByteBuffer.Enqueue(segment);
								while (remainingData > 0)
								{
									readLength = await stream.ReadAsync(buffer, 0, buffer.Length);
									ByteBuffer.Enqueue(new ArraySegment<byte>(buffer, 0, readLength));
									remainingData -= readLength;
								}
								
								await OnReceivedAsync((int)messageLen);
							}
						}
					}
				});

				closedTcs = new TaskCompletionSource<bool>();
				await OnConnectedAsync(isReconnected);

				// Wait for closed connection
				await networkReadTask;
				tcpClient.Close();

				isReconnected = true;
			}
			while (AutoReconnect && ServerTcpClient == null);
		}

		/// <summary>
		/// Closes the socket connection normally. This does not release the resources used by the
		/// <see cref="AsyncTcpClient"/>.
		/// </summary>
		public void Disconnect()
		{
			tcpClient.Client.Disconnect(false);
		}

		/// <summary>
		/// Releases the managed and unmanaged resources used by the <see cref="AsyncTcpClient"/>.
		/// Closes the connection to the remote host and disabled automatic reconnecting.
		/// </summary>
		public void Dispose()
		{
			AutoReconnect = false;
			tcpClient?.Dispose();
			stream = null;
		}

		/// <summary>
		/// Waits asynchronously until received data is available in the buffer.
		/// </summary>
		/// <param name="cancellationToken">A cancellation token used to propagate notification that this operation should be canceled.</param>
		/// <returns>true, if data is available; false, if the connection is closing.</returns>
		/// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> was canceled.</exception>
		public async Task<bool> WaitAsync(CancellationToken cancellationToken = default(CancellationToken))
		{
			return await Task.WhenAny(ByteBuffer.WaitAsync(cancellationToken), closedTcs.Task) != closedTcs.Task;
		}

		/// <summary>
		/// Sends data to the remote host.
		/// </summary>
		/// <param name="data">The data to send.</param>
		/// <returns>The task object representing the asynchronous operation.</returns>
		public async Task Send(ArraySegment<byte> data)
		{
			if (tcpClient.Client.Connected)
			{
				await stream.WriteAsync(data.Array, data.Offset, data.Count);
			}
		}

		#endregion Public methods

		#region Protected virtual methods

		/// <summary>
		/// Called when the client has connected to the remote host. This method can implement the
		/// communication logic to execute when the connection was established. The connection will
		/// not be closed before this method completes.
		/// </summary>
		/// <param name="isReconnected">true, if the connection was closed and automatically reopened;
		///   false, if this is the first established connection for this client instance.</param>
		/// <returns>The task object representing the asynchronous operation.</returns>
		protected virtual Task OnConnectedAsync(bool isReconnected)
		{
			if (ConnectedCallback != null)
			{
				return ConnectedCallback(this, isReconnected);
			}
			return Task.CompletedTask;
		}

		/// <summary>
		/// Called when the connection was closed.
		/// </summary>
		/// <param name="remote">true, if the connection was closed by the remote host; false, if
		///   the connection was closed locally.</param>
		protected virtual void OnClosed(bool remote)
		{
			ClosedCallback?.Invoke(this, remote);
		}

		/// <summary>
		/// Called when data was received from the remote host. This method can implement the
		/// communication logic to execute every time data was received. New data will not be
		/// received before this method completes.
		/// </summary>
		/// <param name="count">The number of bytes that were received. The actual data is available
		///   through the <see cref="ByteBuffer"/>.</param>
		/// <returns>The task object representing the asynchronous operation.</returns>
		protected virtual Task OnReceivedAsync(int count)
		{
			if (ReceivedCallback != null)
			{
				return ReceivedCallback(this, count);
			}
			return Task.CompletedTask;
		}

		#endregion Protected virtual methods
	}

	/// <summary>
	/// Provides data for the <see cref="AsyncTcpClient.Message"/> event.
	/// </summary>
	public class AsyncTcpEventArgs : EventArgs
	{
		/// <summary>
		/// Initialises a new instance of the <see cref="AsyncTcpEventArgs"/> class.
		/// </summary>
		/// <param name="message">The trace message.</param>
		/// <param name="exception">The exception that was thrown, if any.</param>
		public AsyncTcpEventArgs(string message, Exception exception = null)
		{
			Message = message;
			Exception = exception;
		}

		/// <summary>
		/// Gets the trace message.
		/// </summary>
		public string Message { get; }

		/// <summary>
		/// Gets the exception that was thrown, if any.
		/// </summary>
		public Exception Exception { get; }
	}
}
