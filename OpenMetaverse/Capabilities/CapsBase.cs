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
using System.IO;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Threading;

namespace OpenMetaverse.Http;

public static class CapsBase
{
    public delegate void DownloadProgressEventHandler(HttpWebRequest request, HttpWebResponse response,
        int bytesReceived, int totalBytesToReceive);

    public delegate void OpenWriteEventHandler(HttpWebRequest request);

    public delegate void RequestCompletedEventHandler(HttpWebRequest request, HttpWebResponse response,
        byte[] responseData, Exception error);

    static CapsBase()
    {
        // Even though this will compile on Mono 2.4, it throws a runtime exception
        ServicePointManager.ServerCertificateValidationCallback = ValidateServerCertificate;
    }

    public static bool ValidateServerCertificate(
        object sender,
        X509Certificate certificate,
        X509Chain chain,
        SslPolicyErrors sslPolicyErrors)
    {
        //if (m_NoVerifyCertChain)
        sslPolicyErrors &= ~SslPolicyErrors.RemoteCertificateChainErrors;

        //if (m_NoVerifyCertHostname)
        sslPolicyErrors &= ~SslPolicyErrors.RemoteCertificateNameMismatch;

        if (sslPolicyErrors == SslPolicyErrors.None)
            return true;

        return false;
    }

    public static HttpWebRequest UploadDataAsync(Uri address, X509Certificate2 clientCert, string contentType,
        byte[] data,
        int millisecondsTimeout, OpenWriteEventHandler openWriteCallback,
        DownloadProgressEventHandler downloadProgressCallback,
        RequestCompletedEventHandler completedCallback)
    {
        // Create the request
        var request = SetupRequest(address, clientCert);
        request.ContentLength = data.Length;
        if (!string.IsNullOrEmpty(contentType))
            request.ContentType = contentType;
        request.Method = "POST";

        // Create an object to hold all of the state for this request
        var state = new RequestState(request, data, millisecondsTimeout, openWriteCallback,
            downloadProgressCallback, completedCallback);

        // Start the request for a stream to upload to
        var result = request.BeginGetRequestStream(OpenWrite, state);
        // Register a timeout for the request
        ThreadPool.RegisterWaitForSingleObject(result.AsyncWaitHandle, TimeoutCallback, state, millisecondsTimeout,
            true);

        return request;
    }

    public static HttpWebRequest DownloadStringAsync(Uri address, X509Certificate2 clientCert, int millisecondsTimeout,
        DownloadProgressEventHandler downloadProgressCallback, RequestCompletedEventHandler completedCallback)
    {
        // Create the request
        var request = SetupRequest(address, clientCert);
        request.Method = "GET";
        DownloadDataAsync(request, millisecondsTimeout, downloadProgressCallback, completedCallback);
        return request;
    }

    public static void DownloadDataAsync(HttpWebRequest request, int millisecondsTimeout,
        DownloadProgressEventHandler downloadProgressCallback, RequestCompletedEventHandler completedCallback)
    {
        // Create an object to hold all of the state for this request
        var state = new RequestState(request, null, millisecondsTimeout, null, downloadProgressCallback,
            completedCallback);

        // Start the request for the remote server response
        var result = request.BeginGetResponse(GetResponse, state);
        // Register a timeout for the request
        ThreadPool.RegisterWaitForSingleObject(result.AsyncWaitHandle, TimeoutCallback, state, millisecondsTimeout,
            true);
    }

    private static HttpWebRequest SetupRequest(Uri address, X509Certificate2 clientCert)
    {
        if (address == null)
            throw new ArgumentNullException("address");

        // Create the request
        var request = (HttpWebRequest)WebRequest.Create(address);

        // Add the client certificate to the request if one was given
        if (clientCert != null)
            request.ClientCertificates.Add(clientCert);

        // Leave idle connections to this endpoint open for up to 60 seconds
        request.ServicePoint.MaxIdleTime = 1000 * 60;
        // Disable stupid Expect-100: Continue header
        request.ServicePoint.Expect100Continue = false;
        // Crank up the max number of connections per endpoint
        // We set this manually here instead of in ServicePointManager to avoid intereference with callers.
        if (request.ServicePoint.ConnectionLimit < Settings.MAX_HTTP_CONNECTIONS)
        {
            Logger.Log(
                string.Format(
                    "In CapsBase.SetupRequest() setting conn limit for {0}:{1} to {2}",
                    address.Host, address.Port, Settings.MAX_HTTP_CONNECTIONS), Helpers.LogLevel.Debug);
            request.ServicePoint.ConnectionLimit = Settings.MAX_HTTP_CONNECTIONS;
        }

        // Caps requests are never sent as trickles of data, so Nagle's
        // coalescing algorithm won't help us
        request.ServicePoint.UseNagleAlgorithm = false;
        // If not on mono, set accept-encoding header that allows response compression
        request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
        return request;
    }

    private static void OpenWrite(IAsyncResult ar)
    {
        var state = (RequestState)ar.AsyncState;

        try
        {
            // Get the stream to write our upload to
            using (var uploadStream = state.Request.EndGetRequestStream(ar))
            {
                // Fire the callback for successfully opening the stream
                if (state.OpenWriteCallback != null)
                    state.OpenWriteCallback(state.Request);

                // Write our data to the upload stream
                uploadStream.Write(state.UploadData, 0, state.UploadData.Length);
            }

            // Start the request for the remote server response
            var result = state.Request.BeginGetResponse(GetResponse, state);
            // Register a timeout for the request
            ThreadPool.RegisterWaitForSingleObject(result.AsyncWaitHandle, TimeoutCallback, state,
                state.MillisecondsTimeout, true);
        }
        catch (Exception ex)
        {
            //Logger.Log.Debug("CapsBase.OpenWrite(): " + ex.Message);
            if (state.CompletedCallback != null)
                state.CompletedCallback(state.Request, null, null, ex);
        }
    }

    private static void GetResponse(IAsyncResult ar)
    {
        var state = (RequestState)ar.AsyncState;
        HttpWebResponse response = null;
        byte[] responseData = null;
        Exception error = null;

        try
        {
            using (response = (HttpWebResponse)state.Request.EndGetResponse(ar))
            {
                // Get the stream for downloading the response
                using (var responseStream = response.GetResponseStream())
                {
                    #region Read the response

                    // If Content-Length is set we create a buffer of the exact size, otherwise
                    // a MemoryStream is used to receive the response
                    var nolength = response.ContentLength <= 0 || Type.GetType("Mono.Runtime") != null;
                    var size = nolength ? 8192 : (int)response.ContentLength;
                    var ms = nolength ? new MemoryStream() : null;
                    var buffer = new byte[size];

                    var bytesRead = 0;
                    var offset = 0;
                    var totalBytesRead = 0;
                    var totalSize = nolength ? 0 : size;

                    while ((bytesRead = responseStream.Read(buffer, offset, size)) != 0)
                    {
                        totalBytesRead += bytesRead;

                        if (nolength)
                        {
                            totalSize += size - bytesRead;
                            ms.Write(buffer, 0, bytesRead);
                        }
                        else
                        {
                            offset += bytesRead;
                            size -= bytesRead;
                        }

                        // Fire the download progress callback for each chunk of received data
                        if (state.DownloadProgressCallback != null)
                            state.DownloadProgressCallback(state.Request, response, totalBytesRead, totalSize);
                    }

                    if (nolength)
                    {
                        responseData = ms.ToArray();
                        ms.Close();
                        ms.Dispose();
                    }
                    else
                    {
                        responseData = buffer;
                    }

                    #endregion Read the response
                }
            }
        }
        catch (Exception ex)
        {
            // Logger.DebugLog("CapsBase.GetResponse(): " + ex.Message);
            error = ex;
        }

        if (state.CompletedCallback != null)
            state.CompletedCallback(state.Request, response, responseData, error);
    }

    private static void TimeoutCallback(object state, bool timedOut)
    {
        if (timedOut)
        {
            var requestState = state as RequestState;
            //Logger.Log.Debug("CapsBase.TimeoutCallback(): Request to " + requestState.Request.RequestUri +
            //    " timed out after " + requestState.MillisecondsTimeout + " milliseconds");
            if (requestState != null && requestState.Request != null)
                requestState.Request.Abort();
        }
    }

    private class RequestState
    {
        public readonly RequestCompletedEventHandler CompletedCallback;
        public readonly DownloadProgressEventHandler DownloadProgressCallback;
        public readonly int MillisecondsTimeout;
        public readonly OpenWriteEventHandler OpenWriteCallback;
        public readonly HttpWebRequest Request;
        public readonly byte[] UploadData;

        public RequestState(HttpWebRequest request, byte[] uploadData, int millisecondsTimeout,
            OpenWriteEventHandler openWriteCallback,
            DownloadProgressEventHandler downloadProgressCallback, RequestCompletedEventHandler completedCallback)
        {
            Request = request;
            UploadData = uploadData;
            MillisecondsTimeout = millisecondsTimeout;
            OpenWriteCallback = openWriteCallback;
            DownloadProgressCallback = downloadProgressCallback;
            CompletedCallback = completedCallback;
        }
    }
}