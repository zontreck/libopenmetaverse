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
using System.Text;
using System.Text.RegularExpressions;

namespace OpenMetaverse.Voice;

public class VoiceParticipant
{
    private readonly VoiceSession session;
    private bool muted;
    private int volume;

    public VoiceParticipant(string puri, VoiceSession s)
    {
        ID = IDFromName(puri);
        URI = puri;
        session = s;
    }

    private string AvatarName { get; set; }

    public float Energy { get; private set; }

    public bool IsSpeaking { get; private set; }

    public string URI { get; }

    public UUID ID { get; }

    public string Name
    {
        get => AvatarName;
        set => AvatarName = value;
    }

    public bool IsMuted
    {
        get => muted;
        set
        {
            muted = value;
            var sb = new StringBuilder();
            sb.Append(VoiceGateway.MakeXML("SessionHandle", session.Handle));
            sb.Append(VoiceGateway.MakeXML("ParticipantURI", URI));
            sb.Append(VoiceGateway.MakeXML("Mute", muted ? "1" : "0"));
            session.Connector.Request("Session.SetParticipantMuteForMe.1", sb.ToString());
        }
    }

    public int Volume
    {
        get => volume;
        set
        {
            volume = value;
            var sb = new StringBuilder();
            sb.Append(VoiceGateway.MakeXML("SessionHandle", session.Handle));
            sb.Append(VoiceGateway.MakeXML("ParticipantURI", URI));
            sb.Append(VoiceGateway.MakeXML("Volume", volume.ToString()));
            session.Connector.Request("Session.SetParticipantVolumeForMe.1", sb.ToString());
        }
    }

    /// <summary>
    ///     Extract the avatar UUID encoded in a SIP URI
    /// </summary>
    /// <param name="inName"></param>
    /// <returns></returns>
    public static UUID IDFromName(string inName)
    {
        // The "name" may actually be a SIP URI such as: "sip:xFnPP04IpREWNkuw1cOXlhw==@bhr.vivox.com"
        // If it is, convert to a bare name before doing the transform.
        var name = nameFromsipURI(inName);

        // Doesn't look like a SIP URI, assume it's an actual name.
        if (name == null)
            name = inName;

        // This will only work if the name is of the proper form.
        // As an example, the account name for Monroe Linden (UUID 1673cfd3-8229-4445-8d92-ec3570e5e587) is:
        // "xFnPP04IpREWNkuw1cOXlhw=="

        if (name.Length == 25 && name[0] == 'x' && name[23] == '=' && name[24] == '=')
        {
            // The name appears to have the right form.

            // Reverse the transforms done by nameFromID
            var temp = name.Replace('-', '+');
            temp = temp.Replace('_', '/');

            var binary = Convert.FromBase64String(temp.Substring(1));
            var u = UUID.Zero;
            u.FromBytes(binary, 0);
            return u;
        }

        return UUID.Zero;
    }

    private static string Encode64(string str)
    {
        var encbuff = Encoding.UTF8.GetBytes(str);
        return Convert.ToBase64String(encbuff);
    }

    private static byte[] Decode64(string str)
    {
        return Convert.FromBase64String(str);
        //            return System.Text.Encoding.UTF8.GetString(decbuff);
    }

    private static string nameFromsipURI(string uri)
    {
        var sip = new Regex("^sip:([^@]*)@.*$");
        var m = sip.Match(uri);
        if (m.Success)
        {
            var g = m.Groups;
            return g[1].Value;
        }

        return null;
    }

    internal void SetProperties(bool speak, bool mute, float en)
    {
        IsSpeaking = speak;
        muted = mute;
        Energy = en;
    }
}