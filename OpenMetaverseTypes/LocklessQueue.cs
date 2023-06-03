﻿/*
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

using System.Threading;

namespace OpenMetaverse;

/// <summary>
///     A thread-safe lockless queue that supports multiple readers and
///     multiple writers
/// </summary>
public sealed class LocklessQueue<T>
{
    /// <summary>Queue item count</summary>
    private int count;

    /// <summary>Queue head</summary>
    private SingleLinkNode head;

    /// <summary>Queue tail</summary>
    private SingleLinkNode tail;

    /// <summary>
    ///     Constructor
    /// </summary>
    public LocklessQueue()
    {
        count = 0;
        head = tail = new SingleLinkNode();
    }

    /// <summary>
    ///     Gets the current number of items in the queue. Since this
    ///     is a lockless collection this value should be treated as a close
    ///     estimate
    /// </summary>
    public int Count => count;

    /// <summary>
    ///     Enqueue an item
    /// </summary>
    /// <param name="item">Item to enqeue</param>
    public void Enqueue(T item)
    {
        var newNode = new SingleLinkNode { Item = item };

        while (true)
        {
            var oldTail = tail;
            var oldTailNext = oldTail.Next;

            if (tail == oldTail)
            {
                if (oldTailNext != null)
                {
                    CAS(ref tail, oldTail, oldTailNext);
                }
                else if (CAS(ref tail.Next, null, newNode))
                {
                    CAS(ref tail, oldTail, newNode);
                    Interlocked.Increment(ref count);
                    return;
                }
            }
        }
    }

    /// <summary>
    ///     Try to dequeue an item
    /// </summary>
    /// <param name="item">Dequeued item if the dequeue was successful</param>
    /// <returns>True if an item was successfully deqeued, otherwise false</returns>
    public bool TryDequeue(out T item)
    {
        while (true)
        {
            var oldHead = head;
            var oldHeadNext = oldHead.Next;

            if (oldHead == head)
            {
                if (oldHeadNext == null)
                {
                    item = default;
                    count = 0;
                    return false;
                }

                if (CAS(ref head, oldHead, oldHeadNext))
                {
                    item = oldHeadNext.Item;
                    Interlocked.Decrement(ref count);
                    return true;
                }
            }
        }
    }

    private static bool CAS(ref SingleLinkNode location, SingleLinkNode comparand, SingleLinkNode newValue)
    {
        return
            comparand ==
            Interlocked.CompareExchange(ref location, newValue, comparand);
    }

    /// <summary>
    ///     Provides a node container for data in a singly linked list
    /// </summary>
    private sealed class SingleLinkNode
    {
        /// <summary>The data contained by the node</summary>
        public T Item;

        /// <summary>Pointer to the next node in list</summary>
        public SingleLinkNode Next;

        /// <summary>
        ///     Constructor
        /// </summary>
        public SingleLinkNode()
        {
        }

        /// <summary>
        ///     Constructor
        /// </summary>
        public SingleLinkNode(T item)
        {
            Item = item;
        }
    }
}