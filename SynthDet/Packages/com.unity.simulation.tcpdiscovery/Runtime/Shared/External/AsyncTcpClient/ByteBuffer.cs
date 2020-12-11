// Copyright (c) 2018, Yves Goergen, https://unclassified.software
//
// Copying and distribution of this file, with or without modification, are permitted provided the
// copyright notice and this notice are preserved. This file is offered as-is, without any warranty.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Unclassified.Util
{
	/// <summary>
	/// Represents a first-in, first-out collection of bytes with asynchronous dequeuing.
	/// </summary>
	public class ByteBuffer
	{
		#region Private data

		private readonly object syncObj = new object();

		/// <summary>
		/// The internal buffer.
		/// </summary>
		private byte[] buffer = new byte[1024];

		/// <summary>
		/// The buffer index of the first byte to dequeue.
		/// </summary>
		private int head;

		/// <summary>
		/// The buffer index of the last byte to dequeue.
		/// </summary>
		private int tail = -1;

		/// <summary>
		/// Indicates whether the buffer is empty. The empty state cannot be distinguished from the
		/// full state with <see cref="head"/> and <see cref="tail"/> alone.
		/// </summary>
		private bool isEmpty = true;

		/// <summary>
		/// Used to signal the waiting <see cref="DequeueAsync"/> method.
		/// Set when new data becomes available. Only reset there.
		/// </summary>
		private TaskCompletionSource<bool> dequeueManualTcs =
			new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

		/// <summary>
		/// Used to signal the waiting <see cref="WaitAsync"/> method.
		/// Set when new data becomes availalble. Reset when the queue is empty.
		/// </summary>
		private TaskCompletionSource<bool> availableTcs =
			new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

		#endregion Private data

		#region Constructors

		/// <summary>
		/// Initialises a new instance of the <see cref="ByteBuffer"/> class that is empty and has
		/// the default initial capacity.
		/// </summary>
		public ByteBuffer()
		{
		}

		/// <summary>
		/// Initialises a new instance of the <see cref="ByteBuffer"/> class that contains bytes
		/// copied from the specified collection and has sufficient capacity to accommodate the
		/// number of bytes copied.
		/// </summary>
		/// <param name="bytes">The collection whose bytes are copied to the new <see cref="ByteBuffer"/>.</param>
		public ByteBuffer(byte[] bytes)
		{
			Enqueue(bytes);
		}

		/// <summary>
		/// Initialises a new instance of the <see cref="ByteBuffer"/> class that is empty and has
		/// the specified initial capacity.
		/// </summary>
		/// <param name="capacity">The initial number of bytes that the <see cref="ByteBuffer"/> can contain.</param>
		public ByteBuffer(int capacity)
		{
			SetCapacity(capacity);
		}

		#endregion Constructors

		#region Properties

		/// <summary>
		/// Gets the number of bytes contained in the buffer.
		/// </summary>
		public int Count
		{
			get
			{
				lock (syncObj)
				{
					if (isEmpty)
					{
						return 0;
					}
					if (tail >= head)
					{
						return tail - head + 1;
					}
					return Capacity - head + tail + 1;
				}
			}
		}

		/// <summary>
		/// Gets the current buffer contents.
		/// </summary>
		public byte[] Buffer
		{
			get
			{
				lock (syncObj)
				{
					byte[] bytes = new byte[Count];
					if (!isEmpty)
					{
						if (tail >= head)
						{
							Array.Copy(buffer, head, bytes, 0, tail - head + 1);
						}
						else
						{
							Array.Copy(buffer, head, bytes, 0, Capacity - head);
							Array.Copy(buffer, 0, bytes, Capacity - head, tail + 1);
						}
					}
					return bytes;
				}
			}
		}

		/// <summary>
		/// Gets the capacity of the buffer.
		/// </summary>
		public int Capacity => buffer.Length;

		#endregion Properties

		#region Public methods

		/// <summary>
		/// Removes all bytes from the buffer.
		/// </summary>
		public void Clear()
		{
			lock (syncObj)
			{
				head = 0;
				tail = -1;
				isEmpty = true;
				Reset(ref availableTcs);
			}
		}

		/// <summary>
		/// Sets the buffer capacity. Existing bytes are kept in the buffer.
		/// </summary>
		/// <param name="capacity">The new buffer capacity.</param>
		public void SetCapacity(int capacity)
		{
			if (capacity < 0)
				throw new ArgumentOutOfRangeException(nameof(capacity), "The capacity must not be negative.");
			if (capacity < Count)
				throw new ArgumentOutOfRangeException(nameof(capacity), "The capacity is too small to hold the current buffer content.");

			lock (syncObj)
			{
				if (capacity != buffer.Length)
				{
					byte[] newBuffer = new byte[capacity];
					Array.Copy(Buffer, newBuffer, Count);
					buffer = newBuffer;
				}
			}
		}

		/// <summary>
		/// Sets the capacity to the actual number of bytes in the buffer, if that number is less
		/// than 90 percent of current capacity.
		/// </summary>
		public void TrimExcess()
		{
			lock (syncObj)
			{
				if (Count < Capacity * 0.9)
				{
					SetCapacity(Count);
				}
			}
		}

		/// <summary>
		/// Adds bytes to the end of the buffer.
		/// </summary>
		/// <param name="bytes">The bytes to add to the buffer.</param>
		public void Enqueue(byte[] bytes)
		{
			if (bytes == null)
				throw new ArgumentNullException(nameof(bytes));

			Enqueue(bytes, 0, bytes.Length);
		}

		/// <summary>
		/// Adds bytes to the end of the buffer.
		/// </summary>
		/// <param name="segment">The bytes to add to the buffer.</param>
		public void Enqueue(ArraySegment<byte> segment)
		{
			Enqueue(segment.Array, segment.Offset, segment.Count);
		}

		/// <summary>
		/// Adds bytes to the end of the buffer.
		/// </summary>
		/// <param name="bytes">The bytes to add to the buffer.</param>
		/// <param name="offset">The index in <paramref name="bytes"/> of the first byte to add.</param>
		/// <param name="count">The number of bytes to add.</param>
		public void Enqueue(byte[] bytes, int offset, int count)
		{
			if (bytes == null)
				throw new ArgumentNullException(nameof(bytes));
			if (offset < 0)
				throw new ArgumentOutOfRangeException(nameof(offset));
			if (count < 0)
				throw new ArgumentOutOfRangeException(nameof(offset));
			if (offset + count > bytes.Length)
				throw new ArgumentOutOfRangeException(nameof(count));

			if (count == 0)
				return;   // Nothing to do

			lock (syncObj)
			{
				if (Count + count > Capacity)
				{
					SetCapacity(Math.Max(Capacity * 2, Count + count));
				}

				int tailCount;
				int wrapCount;
				if (tail >= head || isEmpty)
				{
					tailCount = Math.Min(Capacity - 1 - tail, count);
					wrapCount = count - tailCount;
				}
				else
				{
					tailCount = Math.Min(head - 1 - tail, count);
					wrapCount = 0;
				}

				if (tailCount > 0)
				{
					Array.Copy(bytes, offset, buffer, tail + 1, tailCount);
				}
				if (wrapCount > 0)
				{
					Array.Copy(bytes, offset + tailCount, buffer, 0, wrapCount);
				}
				tail = (tail + count) % Capacity;
				isEmpty = false;
				Set(dequeueManualTcs);
				Set(availableTcs);
			}
		}

		/// <summary>
		/// Removes and returns bytes at the beginning of the buffer.
		/// </summary>
		/// <param name="maxCount">The maximum number of bytes to dequeue.</param>
		/// <returns>The dequeued bytes. This can be fewer than requested if no more bytes are available.</returns>
		public byte[] Dequeue(int maxCount)
		{
			return DequeueInternal(maxCount, peek: false);
		}

		/// <summary>
		/// Returns bytes at the beginning of the buffer without removing them.
		/// </summary>
		/// <param name="maxCount">The maximum number of bytes to peek.</param>
		/// <returns>The bytes at the beginning of the buffer. This can be fewer than requested if
		///   no more bytes are available.</returns>
		public byte[] Peek(int maxCount)
		{
			return DequeueInternal(maxCount, peek: true);
		}

		/// <summary>
		/// Removes and returns bytes at the beginning of the buffer. Waits asynchronously until
		/// <paramref name="count"/> bytes are available.
		/// </summary>
		/// <param name="count">The number of bytes to dequeue.</param>
		/// <param name="cancellationToken">A cancellation token used to propagate notification that
		///	  this operation should be canceled.</param>
		/// <returns>The bytes at the beginning of the buffer.</returns>
		public async Task<byte[]> DequeueAsync(int count, CancellationToken cancellationToken = default(CancellationToken))
		{
			if (count < 0)
				throw new ArgumentOutOfRangeException(nameof(count), "The count must not be negative.");

			while (true)
			{
				TaskCompletionSource<bool> myDequeueManualTcs;
				lock (syncObj)
				{
					if (count <= Count)
					{
						return Dequeue(count);
					}
					myDequeueManualTcs = Reset(ref dequeueManualTcs);
				}
				await AwaitAsync(myDequeueManualTcs, cancellationToken);
			}
		}

		/// <summary>
		/// Waits asynchronously until bytes are available.
		/// </summary>
		/// <param name="cancellationToken">A cancellation token used to propagate notification that
		///   this operation should be canceled.</param>
		/// <returns>The task object representing the asynchronous operation.</returns>
		public async Task WaitAsync(CancellationToken cancellationToken = default(CancellationToken))
		{
			TaskCompletionSource<bool> myAvailableTcs;
			lock (syncObj)
			{
				if (Count > 0)
				{
					return;
				}
				myAvailableTcs = Reset(ref availableTcs);
			}
			await AwaitAsync(myAvailableTcs, cancellationToken);
		}

		#endregion Public methods

		#region Private methods

		private byte[] DequeueInternal(int count, bool peek)
		{
			if (count < 0)
				throw new ArgumentOutOfRangeException(nameof(count), "The count must not be negative.");
			if (count == 0)
				return new byte[0];   // Easy

			lock (syncObj)
			{
				if (count > Count)
					count = Count;

				byte[] bytes = new byte[count];
				if (tail >= head)
				{
					Array.Copy(buffer, head, bytes, 0, count);
				}
				else
				{
					if (count <= Capacity - head)
					{
						Array.Copy(buffer, head, bytes, 0, count);
					}
					else
					{
						int headCount = Capacity - head;
						Array.Copy(buffer, head, bytes, 0, headCount);
						int wrapCount = count - headCount;
						Array.Copy(buffer, 0, bytes, headCount, wrapCount);
					}
				}
				if (!peek)
				{
					if (count == Count)
					{
						isEmpty = true;
						head = 0;
						tail = -1;
						Reset(ref availableTcs);
					}
					else
					{
						head = (head + count) % Capacity;
					}
				}
				return bytes;
			}
		}

		// Must be called within the lock
		private TaskCompletionSource<bool> Reset(ref TaskCompletionSource<bool> tcs)
		{
			if (tcs.Task.IsCompleted)
			{
				tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
			}
			return tcs;
		}

		// Must be called within the lock
		private void Set(TaskCompletionSource<bool> tcs)
		{
			tcs.TrySetResult(true);
		}

		// Must NOT be called within the lock
		private async Task AwaitAsync(TaskCompletionSource<bool> tcs, CancellationToken cancellationToken)
		{
			if (await Task.WhenAny(tcs.Task, Task.Delay(-1, cancellationToken)) == tcs.Task)
			{
				await tcs.Task;   // Already completed
				return;
			}
			cancellationToken.ThrowIfCancellationRequested();
		}

		#endregion Private methods
	}
}
