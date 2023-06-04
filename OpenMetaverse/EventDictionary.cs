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

using System;
using System.Collections.Generic;
using System.Threading;
using OpenMetaverse.Interfaces;
using OpenMetaverse.Packets;

namespace OpenMetaverse;

/// <summary>
///     Registers, unregisters, and fires events generated by incoming packets
/// </summary>
public class PacketEventDictionary
{
    private readonly Dictionary<PacketType, PacketCallback> _EventTable = new();

    /// <summary>Reference to the GridClient object</summary>
    public GridClient Client;

    /// <summary>
    ///     Default constructor
    /// </summary>
    /// <param name="client"></param>
    public PacketEventDictionary(GridClient client)
    {
        Client = client;
    }

    /// <summary>
    ///     Register an event handler
    /// </summary>
    /// <remarks>
    ///     Use PacketType.Default to fire this event on every
    ///     incoming packet
    /// </remarks>
    /// <param name="packetType">Packet type to register the handler for</param>
    /// <param name="eventHandler">Callback to be fired</param>
    /// <param name="isAsync">
    ///     True if this callback should be ran
    ///     asynchronously, false to run it synchronous
    /// </param>
    public void RegisterEvent(PacketType packetType, EventHandler<PacketReceivedEventArgs> eventHandler, bool isAsync)
    {
        lock (_EventTable)
        {
            PacketCallback callback;
            if (_EventTable.TryGetValue(packetType, out callback))
            {
                callback.Callback += eventHandler;
                callback.IsAsync = callback.IsAsync || isAsync;
            }
            else
            {
                callback = new PacketCallback(eventHandler, isAsync);
                _EventTable[packetType] = callback;
            }
        }
    }

    /// <summary>
    ///     Unregister an event handler
    /// </summary>
    /// <param name="packetType">Packet type to unregister the handler for</param>
    /// <param name="eventHandler">Callback to be unregistered</param>
    public void UnregisterEvent(PacketType packetType, EventHandler<PacketReceivedEventArgs> eventHandler)
    {
        lock (_EventTable)
        {
            PacketCallback callback;
            if (_EventTable.TryGetValue(packetType, out callback))
            {
                callback.Callback -= eventHandler;
                if (callback.Callback == null || callback.Callback.GetInvocationList().Length == 0)
                    _EventTable.Remove(packetType);
            }
        }
    }

    /// <summary>
    ///     Fire the events registered for this packet type
    /// </summary>
    /// <param name="packetType">Incoming packet type</param>
    /// <param name="packet">Incoming packet</param>
    /// <param name="simulator">Simulator this packet was received from</param>
    internal void RaiseEvent(PacketType packetType, Packet packet, Simulator simulator)
    {
        PacketCallback callback;

        // Default handler first, if one exists
        if (_EventTable.TryGetValue(PacketType.Default, out callback) && callback.Callback != null)
        {
            if (callback.IsAsync)
            {
                PacketCallbackWrapper wrapper;
                wrapper.Callback = callback.Callback;
                wrapper.Packet = packet;
                wrapper.Simulator = simulator;
                WorkPool.QueueUserWorkItem(ThreadPoolDelegate, wrapper);
            }
            else
            {
                try
                {
                    callback.Callback(this, new PacketReceivedEventArgs(packet, simulator));
                }
                catch (Exception ex)
                {
                    Logger.Log("Default packet event handler: " + ex, Helpers.LogLevel.Error, Client);
                }
            }
        }

        if (_EventTable.TryGetValue(packetType, out callback) && callback.Callback != null)
        {
            if (callback.IsAsync)
            {
                PacketCallbackWrapper wrapper;
                wrapper.Callback = callback.Callback;
                wrapper.Packet = packet;
                wrapper.Simulator = simulator;
                WorkPool.QueueUserWorkItem(ThreadPoolDelegate, wrapper);
            }
            else
            {
                try
                {
                    callback.Callback(this, new PacketReceivedEventArgs(packet, simulator));
                }
                catch (Exception ex)
                {
                    Logger.Log("Packet event handler: " + ex, Helpers.LogLevel.Error, Client);
                }
            }

            return;
        }

        if (packetType != PacketType.Default && packetType != PacketType.PacketAck)
            Logger.DebugLog("No handler registered for packet event " + packetType, Client);
    }

    private void ThreadPoolDelegate(object state)
    {
        var wrapper = (PacketCallbackWrapper)state;

        try
        {
            wrapper.Callback(this, new PacketReceivedEventArgs(wrapper.Packet, wrapper.Simulator));
        }
        catch (Exception ex)
        {
            Logger.Log("Async Packet Event Handler: " + ex, Helpers.LogLevel.Error, Client);
        }
    }

    private sealed class PacketCallback
    {
        public EventHandler<PacketReceivedEventArgs> Callback;
        public bool IsAsync;

        public PacketCallback(EventHandler<PacketReceivedEventArgs> callback, bool isAsync)
        {
            Callback = callback;
            IsAsync = isAsync;
        }
    }

    /// <summary>
    ///     Object that is passed to worker threads in the ThreadPool for
    ///     firing packet callbacks
    /// </summary>
    private struct PacketCallbackWrapper
    {
        /// <summary>Callback to fire for this packet</summary>
        public EventHandler<PacketReceivedEventArgs> Callback;

        /// <summary>Reference to the simulator that this packet came from</summary>
        public Simulator Simulator;

        /// <summary>The packet that needs to be processed</summary>
        public Packet Packet;
    }
}

/// <summary>
///     Registers, unregisters, and fires events generated by the Capabilities
///     event queue
/// </summary>
public class CapsEventDictionary
{
    private readonly Dictionary<string, Caps.EventQueueCallback> _EventTable = new();

    private WaitCallback _ThreadPoolCallback;

    /// <summary>Reference to the GridClient object</summary>
    public GridClient Client;

    /// <summary>
    ///     Default constructor
    /// </summary>
    /// <param name="client">Reference to the GridClient object</param>
    public CapsEventDictionary(GridClient client)
    {
        Client = client;
        _ThreadPoolCallback = ThreadPoolDelegate;
    }

    /// <summary>
    ///     Register an new event handler for a capabilities event sent via the EventQueue
    /// </summary>
    /// <remarks>Use String.Empty to fire this event on every CAPS event</remarks>
    /// <param name="capsEvent">
    ///     Capability event name to register the
    ///     handler for
    /// </param>
    /// <param name="eventHandler">Callback to fire</param>
    public void RegisterEvent(string capsEvent, Caps.EventQueueCallback eventHandler)
    {
        // TODO: Should we add support for synchronous CAPS handlers?
        lock (_EventTable)
        {
            if (_EventTable.ContainsKey(capsEvent))
                _EventTable[capsEvent] += eventHandler;
            else
                _EventTable[capsEvent] = eventHandler;
        }
    }

    /// <summary>
    ///     Unregister a previously registered capabilities handler
    /// </summary>
    /// <param name="capsEvent">
    ///     Capability event name unregister the
    ///     handler for
    /// </param>
    /// <param name="eventHandler">Callback to unregister</param>
    public void UnregisterEvent(string capsEvent, Caps.EventQueueCallback eventHandler)
    {
        lock (_EventTable)
        {
            if (_EventTable.ContainsKey(capsEvent) && _EventTable[capsEvent] != null)
                _EventTable[capsEvent] -= eventHandler;
        }
    }

    /// <summary>
    ///     Fire the events registered for this event type synchronously
    /// </summary>
    /// <param name="capsEvent">Capability name</param>
    /// <param name="message">Decoded event body</param>
    /// <param name="simulator">
    ///     Reference to the simulator that
    ///     generated this event
    /// </param>
    internal void RaiseEvent(string capsEvent, IMessage message, Simulator simulator)
    {
        var specialHandler = false;
        Caps.EventQueueCallback callback;

        // Default handler first, if one exists
        if (_EventTable.TryGetValue(capsEvent, out callback))
            if (callback != null)
                try
                {
                    callback(capsEvent, message, simulator);
                }
                catch (Exception ex)
                {
                    Logger.Log("CAPS Event Handler: " + ex, Helpers.LogLevel.Error, Client);
                }

        // Explicit handler next
        if (_EventTable.TryGetValue(capsEvent, out callback) && callback != null)
        {
            try
            {
                callback(capsEvent, message, simulator);
            }
            catch (Exception ex)
            {
                Logger.Log("CAPS Event Handler: " + ex, Helpers.LogLevel.Error, Client);
            }

            specialHandler = true;
        }

        if (!specialHandler)
            Logger.Log("Unhandled CAPS event " + capsEvent, Helpers.LogLevel.Warning, Client);
    }

    /// <summary>
    ///     Fire the events registered for this event type asynchronously
    /// </summary>
    /// <param name="capsEvent">Capability name</param>
    /// <param name="message">Decoded event body</param>
    /// <param name="simulator">
    ///     Reference to the simulator that
    ///     generated this event
    /// </param>
    internal void BeginRaiseEvent(string capsEvent, IMessage message, Simulator simulator)
    {
        var specialHandler = false;
        Caps.EventQueueCallback callback;

        // Default handler first, if one exists
        if (_EventTable.TryGetValue(string.Empty, out callback))
            if (callback != null)
                callback(capsEvent, message, simulator);
        //                    CapsCallbackWrapper wrapper;
        //                    wrapper.Callback = callback;
        //                    wrapper.CapsEvent = capsEvent;
        //                    wrapper.Message = message;
        //                    wrapper.Simulator = simulator;
        //                    WorkPool.QueueUserWorkItem(_ThreadPoolCallback, wrapper);
        // Explicit handler next
        if (_EventTable.TryGetValue(capsEvent, out callback) && callback != null)
        {
            callback(capsEvent, message, simulator);

//                CapsCallbackWrapper wrapper;
//                wrapper.Callback = callback;
//                wrapper.CapsEvent = capsEvent;
//                wrapper.Message = message;
//                wrapper.Simulator = simulator;
//                WorkPool.QueueUserWorkItem(_ThreadPoolCallback, wrapper);

            specialHandler = true;
        }

        if (!specialHandler)
            Logger.Log("Unhandled CAPS event " + capsEvent, Helpers.LogLevel.Warning, Client);
    }

    private void ThreadPoolDelegate(object state)
    {
        var wrapper = (CapsCallbackWrapper)state;

        try
        {
            wrapper.Callback(wrapper.CapsEvent, wrapper.Message, wrapper.Simulator);
        }
        catch (Exception ex)
        {
            Logger.Log("Async CAPS Event Handler: " + ex, Helpers.LogLevel.Error, Client);
        }
    }

    /// <summary>
    ///     Object that is passed to worker threads in the ThreadPool for
    ///     firing CAPS callbacks
    /// </summary>
    private struct CapsCallbackWrapper
    {
        /// <summary>Callback to fire for this packet</summary>
        public Caps.EventQueueCallback Callback;

        /// <summary>Name of the CAPS event</summary>
        public string CapsEvent;

        /// <summary>Strongly typed decoded data</summary>
        public IMessage Message;

        /// <summary>Reference to the simulator that generated this event</summary>
        public Simulator Simulator;
    }
}