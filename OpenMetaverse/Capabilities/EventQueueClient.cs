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
using System.Net;
using System.Text;
using System.Threading;
using OpenMetaverse.StructuredData;

namespace OpenMetaverse.Http;

public class EventQueueClient
{
    public delegate void ConnectedCallback();

    public delegate void EventCallback(string eventName, OSDMap body);

    /// <summary>=</summary>
    public const int REQUEST_TIMEOUT = 1000 * 120;

    /// <summary>For exponential backoff on error.</summary>
    private static readonly Random _random = new();

    protected Uri _Address;
    protected bool _Dead;

    /// <summary>Number of times we've received an unknown CAPS exception in series.</summary>
    private int _errorCount;

    protected HttpWebRequest _Request;
    protected bool _Running;

    public ConnectedCallback OnConnected;
    public EventCallback OnEvent;

    public EventQueueClient(Uri eventQueueLocation)
    {
        _Address = eventQueueLocation;
    }

    public bool Running => _Running;

    public void Start()
    {
        _Dead = false;

        // Create an EventQueueGet request
        var request = new OSDMap();
        request["ack"] = new OSD();
        request["done"] = OSD.FromBoolean(false);

        var postData = OSDParser.SerializeLLSDXmlBytes(request);

        _Request = CapsBase.UploadDataAsync(_Address, null, "application/xml", postData, REQUEST_TIMEOUT,
            OpenWriteHandler, null, RequestCompletedHandler);
    }

    public void Stop(bool immediate)
    {
        _Dead = true;

        if (immediate)
            _Running = false;

        if (_Request != null)
            _Request.Abort();
    }

    private void OpenWriteHandler(HttpWebRequest request)
    {
        _Running = true;
        _Request = request;

        Logger.DebugLog("Capabilities event queue connected");

        // The event queue is starting up for the first time
        if (OnConnected != null)
            try
            {
                OnConnected();
            }
            catch (Exception ex)
            {
                Logger.Log(ex.Message, Helpers.LogLevel.Error, ex);
            }
    }

    private void RequestCompletedHandler(HttpWebRequest request, HttpWebResponse response, byte[] responseData,
        Exception error)
    {
        // We don't care about this request now that it has completed
        _Request = null;

        OSDArray events = null;
        var ack = 0;

        if (responseData != null)
        {
            _errorCount = 0;
            // Got a response
            var result = OSDParser.DeserializeLLSDXml(responseData) as OSDMap;

            if (result != null)
            {
                events = result["events"] as OSDArray;
                ack = result["id"].AsInteger();
            }
            else
            {
                Logger.Log("Got an unparseable response from the event queue: \"" +
                           Encoding.UTF8.GetString(responseData) + "\"", Helpers.LogLevel.Warning);
            }
        }
        else if (error != null)
        {
            #region Error handling

            var code = HttpStatusCode.OK;

            if (error is WebException)
            {
                var webException = (WebException)error;

                if (webException.Response != null)
                    code = ((HttpWebResponse)webException.Response).StatusCode;
                else if (webException.Status == WebExceptionStatus.RequestCanceled)
                    goto HandlingDone;
            }

            if (error is WebException && ((WebException)error).Response != null)
                code = ((HttpWebResponse)((WebException)error).Response).StatusCode;

            if (code == HttpStatusCode.NotFound || code == HttpStatusCode.Gone)
            {
                Logger.Log(string.Format("Closing event queue at {0} due to missing caps URI", _Address),
                    Helpers.LogLevel.Info);

                _Running = false;
                _Dead = true;
            }
            else if (code == HttpStatusCode.BadGateway)
            {
                // This is not good (server) protocol design, but it's normal.
                // The EventQueue server is a proxy that connects to a Squid
                // cache which will time out periodically. The EventQueue server
                // interprets this as a generic error and returns a 502 to us
                // that we ignore
            }
            else
            {
                ++_errorCount;

                // Try to log a meaningful error message
                if (code != HttpStatusCode.OK)
                    Logger.Log(string.Format("Unrecognized caps connection problem from {0}: {1}",
                        _Address, code), Helpers.LogLevel.Warning);
                else if (error.InnerException != null)
                    Logger.Log(string.Format("Unrecognized internal caps exception from {0}: {1}",
                        _Address, error.InnerException.Message), Helpers.LogLevel.Warning);
                else
                    Logger.Log(string.Format("Unrecognized caps exception from {0}: {1}",
                        _Address, error.Message), Helpers.LogLevel.Warning);
            }

            #endregion Error handling
        }
        else
        {
            ++_errorCount;

            Logger.Log("No response from the event queue but no reported error either", Helpers.LogLevel.Warning);
        }

        HandlingDone:

        #region Resume the connection

        if (_Running)
        {
            var osdRequest = new OSDMap();
            if (ack != 0) osdRequest["ack"] = OSD.FromInteger(ack);
            else osdRequest["ack"] = new OSD();
            osdRequest["done"] = OSD.FromBoolean(_Dead);

            var postData = OSDParser.SerializeLLSDXmlBytes(osdRequest);

            if (_errorCount > 0) // Exponentially back off, so we don't hammer the CPU
                Thread.Sleep(_random.Next(500 + (int)Math.Pow(2, _errorCount)));

            // Resume the connection. The event handler for the connection opening
            // just sets class _Request variable to the current HttpWebRequest
            CapsBase.UploadDataAsync(_Address, null, "application/xml", postData, REQUEST_TIMEOUT,
                delegate(HttpWebRequest newRequest) { _Request = newRequest; }, null, RequestCompletedHandler);

            // If the event queue is dead at this point, turn it off since
            // that was the last thing we want to do
            if (_Dead)
            {
                _Running = false;
                Logger.DebugLog("Sent event queue shutdown message");
            }
        }

        #endregion Resume the connection

        #region Handle incoming events

        if (OnEvent != null && events != null && events.Count > 0)
            // Fire callbacks for each event received
            foreach (OSDMap evt in events)
            {
                var msg = evt["message"].AsString();
                var body = (OSDMap)evt["body"];

                try
                {
                    OnEvent(msg, body);
                }
                catch (Exception ex)
                {
                    Logger.Log(ex.Message, Helpers.LogLevel.Error, ex);
                }
            }

        #endregion Handle incoming events
    }
}