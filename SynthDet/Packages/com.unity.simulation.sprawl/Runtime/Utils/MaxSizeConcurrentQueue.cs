using System.Collections.Generic;
using System.Threading;

namespace Sprawl {
    class MaxSizeConcurrentQueue<T> where T : class {
        private Queue<T> queue_ = new Queue<T>();
        private Semaphore semaphore_ = null;
        private int max_size_ = 0;

        public MaxSizeConcurrentQueue(int max_size) {
            max_size_ = max_size;
            if (max_size > 0) {
                semaphore_ = new Semaphore(max_size, max_size);
            }
        }

        public void Push(T item) {
            if (max_size_ > 0) {
                semaphore_.WaitOne();
            }
            lock (queue_) {
                queue_.Enqueue(item);
                Monitor.Pulse(queue_);
            }
        }

        public T Get() {
            T item = null;
            lock (queue_) {
                while (true) {
                    if (queue_.Count > 0) {
                        item = queue_.Dequeue();
                        break;
                    }
                    Monitor.Wait(queue_);
                }
            }
            if (max_size_ > 0) {
                semaphore_.Release();
            }
            return item;
        }

        public bool TryGet(out T item) {
            lock (queue_) {
                if (queue_.Count > 0) {
                    item = queue_.Dequeue();
                } else {
                    item = null;
                    return false;
                }
            }
            if (max_size_ > 0) {
                semaphore_.Release();
            }
            return true;
        }

        public bool Get(out T item, float timeout) {
            lock (queue_) {
                if (queue_.Count > 0) {
                    item = queue_.Dequeue();
                } else {
                    Monitor.Wait(queue_, (int)(timeout * 1000));
                    if (queue_.Count > 0) {
                        item = queue_.Dequeue();
                    } else {
                        item = null;
                        return false;
                    }
                }
            }
            if (max_size_ > 0) {
                semaphore_.Release();
            }
            return true;
        }
    }
}