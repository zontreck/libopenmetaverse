/*
 * Copyright (c) 2006-2016, openmetaverse.co
 * All rights reserved.
 *
 * - Redistribution and use in source and binary forms, with or without
 *   modification, are permitted provided that the following conditions are met:
 *
 * - Redistributions of source code must retain the above copyright notice, this
 *   list of conditions and the following disclaimer.
 * - Neither the name of the openmetaverse.co nor the names
 *   of its contributors may be used to endorse or promote products derived from
 *   this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
 * AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
 * IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
 * ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE
 * LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR
 * CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF
 * SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS
 * INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN
 * CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
 * ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
 * POSSIBILITY OF SUCH DAMAGE.
 */

namespace OpenMetaverse;

public class CircularQueue<T>
{
    public readonly T[] Items;
    private readonly int capacity;

    private readonly object syncRoot;

    public CircularQueue(int capacity)
    {
        this.capacity = capacity;
        Items = new T[capacity];
        syncRoot = new object();
    }

    /// <summary>
    ///     Copy constructor
    /// </summary>
    /// <param name="queue">Circular queue to copy</param>
    public CircularQueue(CircularQueue<T> queue)
    {
        lock (queue.syncRoot)
        {
            capacity = queue.capacity;
            Items = new T[capacity];
            syncRoot = new object();

            for (var i = 0; i < capacity; i++)
                Items[i] = queue.Items[i];

            First = queue.First;
            Next = queue.Next;
        }
    }

    public int First { get; private set; }

    public int Next { get; private set; }

    public void Clear()
    {
        lock (syncRoot)
        {
            // Explicitly remove references to help garbage collection
            for (var i = 0; i < capacity; i++)
                Items[i] = default;

            First = Next;
        }
    }

    public void Enqueue(T value)
    {
        lock (syncRoot)
        {
            Items[Next] = value;
            Next = (Next + 1) % capacity;
            if (Next == First) First = (First + 1) % capacity;
        }
    }

    public T Dequeue()
    {
        lock (syncRoot)
        {
            var value = Items[First];
            Items[First] = default;

            if (First != Next)
                First = (First + 1) % capacity;

            return value;
        }
    }

    public T DequeueLast()
    {
        lock (syncRoot)
        {
            // If the next element is right behind the first element (queue is full),
            // back up the first element by one
            var firstTest = First - 1;
            if (firstTest < 0) firstTest = capacity - 1;

            if (firstTest == Next)
            {
                --Next;
                if (Next < 0) Next = capacity - 1;

                --First;
                if (First < 0) First = capacity - 1;
            }
            else if (First != Next)
            {
                --Next;
                if (Next < 0) Next = capacity - 1;
            }

            var value = Items[Next];
            Items[Next] = default;

            return value;
        }
    }
}