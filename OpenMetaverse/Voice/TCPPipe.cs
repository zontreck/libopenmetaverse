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
using System.Net.Sockets;
using System.Text;

namespace OpenMetaverse.Voice;

public class TCPPipe
{
    public delegate void OnDisconnectedCallback(SocketException se);

    public delegate void OnReceiveLineCallback(string line);

    private static readonly char[] splitNull = { '\0' };
    private static readonly string[] splitLines = { "\r", "\n", "\r\n" };
    protected string _Buffer = string.Empty;
    protected AsyncCallback _Callback;
    protected IAsyncResult _Result;

    protected Socket _TCPSocket;

    public bool Connected
    {
        get
        {
            if (_TCPSocket != null && _TCPSocket.Connected)
                return true;
            return false;
        }
    }

    public event OnReceiveLineCallback OnReceiveLine;
    public event OnDisconnectedCallback OnDisconnected;

    public SocketException Connect(string address, int port)
    {
        if (_TCPSocket != null && _TCPSocket.Connected)
            Disconnect();

        try
        {
            IPAddress ip;
            if (!IPAddress.TryParse(address, out ip))
            {
                var ips = Dns.GetHostAddresses(address);
                ip = ips[0];
            }

            _TCPSocket = new Socket(ip.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            var ipEndPoint = new IPEndPoint(ip, port);
            _TCPSocket.Connect(ipEndPoint);
            if (_TCPSocket.Connected)
            {
                WaitForData();
                return null;
            }

            return new SocketException(10000);
        }
        catch (SocketException se)
        {
            return se;
        }
    }

    public void Disconnect()
    {
        _TCPSocket.Disconnect(true);
    }

    public void SendData(byte[] data)
    {
        if (Connected)
            _TCPSocket.Send(data);
        else
            throw new InvalidOperationException("socket is not connected");
    }

    public void SendLine(string message)
    {
        if (Connected)
        {
            var byData = Encoding.ASCII.GetBytes(message + "\n");
            _TCPSocket.Send(byData);
        }
        else
        {
            throw new InvalidOperationException("socket is not connected");
        }
    }

    private void WaitForData()
    {
        try
        {
            if (_Callback == null) _Callback = OnDataReceived;
            var packet = new SocketPacket();
            packet.TCPSocket = _TCPSocket;
            _Result = _TCPSocket.BeginReceive(packet.DataBuffer, 0, packet.DataBuffer.Length, SocketFlags.None,
                _Callback, packet);
        }
        catch (SocketException se)
        {
            Console.WriteLine(se.Message);
        }
    }

    private void ReceiveData(string data)
    {
        if (OnReceiveLine == null) return;

        //string[] splitNull = { "\0" };
        var line = data.Split(splitNull, StringSplitOptions.None);
        _Buffer += line[0];
        //string[] splitLines = { "\r\n", "\r", "\n" };
        var lines = _Buffer.Split(splitLines, StringSplitOptions.None);
        if (lines.Length > 1)
        {
            int i;
            for (i = 0; i < lines.Length - 1; i++)
                if (lines[i].Trim().Length > 0)
                    OnReceiveLine(lines[i]);
            _Buffer = lines[i];
        }
    }

    private void OnDataReceived(IAsyncResult asyn)
    {
        try
        {
            var packet = (SocketPacket)asyn.AsyncState;
            var end = packet.TCPSocket.EndReceive(asyn);
            var chars = new char[end + 1];
            var d = Encoding.UTF8.GetDecoder();
            d.GetChars(packet.DataBuffer, 0, end, chars, 0);
            var data = new string(chars);
            ReceiveData(data);
            WaitForData();
        }
        catch (ObjectDisposedException)
        {
            Console.WriteLine("WARNING: Socket closed unexpectedly");
        }
        catch (SocketException se)
        {
            if (!_TCPSocket.Connected)
                if (OnDisconnected != null)
                    OnDisconnected(se);
        }
    }

    protected class SocketPacket
    {
        public byte[] DataBuffer = new byte[1];
        public Socket TCPSocket;
    }
}