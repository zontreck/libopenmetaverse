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
using System.Text;

namespace OpenMetaverse.Voice;

/// <summary>
///     Represents a single Voice Session to the Vivox service.
/// </summary>
public class VoiceSession
{
    private static Dictionary<string, VoiceParticipant> knownParticipants;
    public string RegionName;

    public VoiceSession(VoiceGateway conn, string handle)
    {
        Handle = handle;
        Connector = conn;

        IsSpatial = true;
        knownParticipants = new Dictionary<string, VoiceParticipant>();
    }

    public bool IsSpatial { get; }

    public VoiceGateway Connector { get; }

    public string Handle { get; }

    public event EventHandler OnParticipantAdded;
    public event EventHandler OnParticipantUpdate;
    public event EventHandler OnParticipantRemoved;

    /// <summary>
    ///     Close this session.
    /// </summary>
    internal void Close()
    {
        knownParticipants.Clear();
    }

    internal void ParticipantUpdate(string URI,
        bool isMuted,
        bool isSpeaking,
        int volume,
        float energy)
    {
        lock (knownParticipants)
        {
            // Locate in this session
            var p = FindParticipant(URI);
            if (p == null) return;

            // Set properties
            p.SetProperties(isSpeaking, isMuted, energy);

            // Inform interested parties.
            if (OnParticipantUpdate != null)
                OnParticipantUpdate(p, null);
        }
    }

    internal void AddParticipant(string URI)
    {
        lock (knownParticipants)
        {
            var p = FindParticipant(URI);

            // We expect that to come back null.  If it is not
            // null, this is a duplicate
            if (p != null) return;

            // It was not found, so add it.
            p = new VoiceParticipant(URI, this);
            knownParticipants.Add(URI, p);

            /* TODO
                       // Fill in the name.
                       if (p.Name == null || p.Name.StartsWith("Loading..."))
                               p.Name = control.instance.getAvatarName(p.ID);
                           return p;
           */

            // Inform interested parties.
            if (OnParticipantAdded != null)
                OnParticipantAdded(p, null);
        }
    }

    internal void RemoveParticipant(string URI)
    {
        lock (knownParticipants)
        {
            var p = FindParticipant(URI);
            if (p == null) return;

            // Remove from list for this session.
            knownParticipants.Remove(URI);

            // Inform interested parties.
            if (OnParticipantRemoved != null)
                OnParticipantRemoved(p, null);
        }
    }

    /// <summary>
    ///     Look up an existing Participants in this session
    /// </summary>
    /// <param name="puri"></param>
    /// <returns></returns>
    private VoiceParticipant FindParticipant(string puri)
    {
        if (knownParticipants.ContainsKey(puri))
            return knownParticipants[puri];

        return null;
    }

    public void Set3DPosition(VoicePosition SpeakerPosition, VoicePosition ListenerPosition)
    {
        Connector.SessionSet3DPosition(Handle, SpeakerPosition, ListenerPosition);
    }
}

public partial class VoiceGateway
{
    /// <summary>
    ///     Create a Session
    ///     Sessions typically represent a connection to a media session with one or more
    ///     participants. This is used to generate an �outbound� call to another user or
    ///     channel. The specifics depend on the media types involved. A session handle is
    ///     required to control the local user functions within the session (or remote
    ///     users if the current account has rights to do so). Currently creating a
    ///     session automatically connects to the audio media, there is no need to call
    ///     Session.Connect at this time, this is reserved for future use.
    /// </summary>
    /// <param name="AccountHandle">Handle returned from successful Connector �create� request</param>
    /// <param name="URI">This is the URI of the terminating point of the session (ie who/what is being called)</param>
    /// <param name="Name">This is the display name of the entity being called (user or channel)</param>
    /// <param name="Password">Only needs to be supplied when the target URI is password protected</param>
    /// <param name="PasswordHashAlgorithm">
    ///     This indicates the format of the password as passed in. This can either be
    ///     �ClearText� or �SHA1UserName�. If this element does not exist, it is assumed to be �ClearText�. If it is
    ///     �SHA1UserName�, the password as passed in is the SHA1 hash of the password and username concatenated together,
    ///     then base64 encoded, with the final �=� character stripped off.
    /// </param>
    /// <param name="JoinAudio"></param>
    /// <param name="JoinText"></param>
    /// <returns></returns>
    public int SessionCreate(string AccountHandle, string URI, string Name, string Password,
        bool JoinAudio, bool JoinText, string PasswordHashAlgorithm)
    {
        var sb = new StringBuilder();
        sb.Append(MakeXML("AccountHandle", AccountHandle));
        sb.Append(MakeXML("URI", URI));
        sb.Append(MakeXML("Name", Name));
        if (Password != null && Password != "")
        {
            sb.Append(MakeXML("Password", Password));
            sb.Append(MakeXML("PasswordHashAlgorithm", PasswordHashAlgorithm));
        }

        sb.Append(MakeXML("ConnectAudio", JoinAudio ? "true" : "false"));
        sb.Append(MakeXML("ConnectText", JoinText ? "true" : "false"));
        sb.Append(MakeXML("JoinAudio", JoinAudio ? "true" : "false"));
        sb.Append(MakeXML("JoinText", JoinText ? "true" : "false"));
        sb.Append(MakeXML("VoiceFontID", "0"));

        return Request("Session.Create.1", sb.ToString());
    }

    /// <summary>
    ///     Used to accept a call
    /// </summary>
    /// <param name="SessionHandle">SessionHandle such as received from SessionNewEvent</param>
    /// <param name="AudioMedia">"default"</param>
    /// <returns></returns>
    public int SessionConnect(string SessionHandle, string AudioMedia)
    {
        var sb = new StringBuilder();
        sb.Append(MakeXML("SessionHandle", SessionHandle));
        sb.Append(MakeXML("AudioMedia", AudioMedia));
        return Request("Session.Connect.1", sb.ToString());
    }

    /// <summary>
    ///     This command is used to start the audio render process, which will then play
    ///     the passed in file through the selected audio render device. This command
    ///     should not be issued if the user is on a call.
    /// </summary>
    /// <param name="SoundFilePath">The fully qualified path to the sound file.</param>
    /// <param name="Loop">True if the file is to be played continuously and false if it is should be played once.</param>
    /// <returns></returns>
    public int SessionRenderAudioStart(string SoundFilePath, bool Loop)
    {
        var sb = new StringBuilder();
        sb.Append(MakeXML("SoundFilePath", SoundFilePath));
        sb.Append(MakeXML("Loop", Loop ? "1" : "0"));
        return Request("Session.RenderAudioStart.1", sb.ToString());
    }

    /// <summary>
    ///     This command is used to stop the audio render process.
    /// </summary>
    /// <param name="SoundFilePath">The fully qualified path to the sound file issued in the start render command.</param>
    /// <returns></returns>
    public int SessionRenderAudioStop(string SoundFilePath)
    {
        var RequestXML = MakeXML("SoundFilePath", SoundFilePath);
        return Request("Session.RenderAudioStop.1", RequestXML);
    }

    /// <summary>
    ///     This is used to �end� an established session (i.e. hang-up or disconnect).
    /// </summary>
    /// <param name="SessionHandle">Handle returned from successful Session �create� request or a SessionNewEvent</param>
    /// <returns></returns>
    public int SessionTerminate(string SessionHandle)
    {
        var RequestXML = MakeXML("SessionHandle", SessionHandle);
        return Request("Session.Terminate.1", RequestXML);
    }

    /// <summary>
    ///     Set the combined speaking and listening position in 3D space.
    /// </summary>
    /// <param name="SessionHandle">Handle returned from successful Session �create� request or a SessionNewEvent</param>
    /// <param name="SpeakerPosition">Speaking position</param>
    /// <param name="ListenerPosition">Listening position</param>
    /// <returns></returns>
    public int SessionSet3DPosition(string SessionHandle, VoicePosition SpeakerPosition, VoicePosition ListenerPosition)
    {
        var sb = new StringBuilder();
        sb.Append(MakeXML("SessionHandle", SessionHandle));
        sb.Append("<SpeakerPosition>");
        sb.Append("<Position>");
        sb.Append(MakeXML("X", SpeakerPosition.Position.X.ToString()));
        sb.Append(MakeXML("Y", SpeakerPosition.Position.Y.ToString()));
        sb.Append(MakeXML("Z", SpeakerPosition.Position.Z.ToString()));
        sb.Append("</Position>");
        sb.Append("<Velocity>");
        sb.Append(MakeXML("X", SpeakerPosition.Velocity.X.ToString()));
        sb.Append(MakeXML("Y", SpeakerPosition.Velocity.Y.ToString()));
        sb.Append(MakeXML("Z", SpeakerPosition.Velocity.Z.ToString()));
        sb.Append("</Velocity>");
        sb.Append("<AtOrientation>");
        sb.Append(MakeXML("X", SpeakerPosition.AtOrientation.X.ToString()));
        sb.Append(MakeXML("Y", SpeakerPosition.AtOrientation.Y.ToString()));
        sb.Append(MakeXML("Z", SpeakerPosition.AtOrientation.Z.ToString()));
        sb.Append("</AtOrientation>");
        sb.Append("<UpOrientation>");
        sb.Append(MakeXML("X", SpeakerPosition.UpOrientation.X.ToString()));
        sb.Append(MakeXML("Y", SpeakerPosition.UpOrientation.Y.ToString()));
        sb.Append(MakeXML("Z", SpeakerPosition.UpOrientation.Z.ToString()));
        sb.Append("</UpOrientation>");
        sb.Append("<LeftOrientation>");
        sb.Append(MakeXML("X", SpeakerPosition.LeftOrientation.X.ToString()));
        sb.Append(MakeXML("Y", SpeakerPosition.LeftOrientation.Y.ToString()));
        sb.Append(MakeXML("Z", SpeakerPosition.LeftOrientation.Z.ToString()));
        sb.Append("</LeftOrientation>");
        sb.Append("</SpeakerPosition>");
        sb.Append("<ListenerPosition>");
        sb.Append("<Position>");
        sb.Append(MakeXML("X", ListenerPosition.Position.X.ToString()));
        sb.Append(MakeXML("Y", ListenerPosition.Position.Y.ToString()));
        sb.Append(MakeXML("Z", ListenerPosition.Position.Z.ToString()));
        sb.Append("</Position>");
        sb.Append("<Velocity>");
        sb.Append(MakeXML("X", ListenerPosition.Velocity.X.ToString()));
        sb.Append(MakeXML("Y", ListenerPosition.Velocity.Y.ToString()));
        sb.Append(MakeXML("Z", ListenerPosition.Velocity.Z.ToString()));
        sb.Append("</Velocity>");
        sb.Append("<AtOrientation>");
        sb.Append(MakeXML("X", ListenerPosition.AtOrientation.X.ToString()));
        sb.Append(MakeXML("Y", ListenerPosition.AtOrientation.Y.ToString()));
        sb.Append(MakeXML("Z", ListenerPosition.AtOrientation.Z.ToString()));
        sb.Append("</AtOrientation>");
        sb.Append("<UpOrientation>");
        sb.Append(MakeXML("X", ListenerPosition.UpOrientation.X.ToString()));
        sb.Append(MakeXML("Y", ListenerPosition.UpOrientation.Y.ToString()));
        sb.Append(MakeXML("Z", ListenerPosition.UpOrientation.Z.ToString()));
        sb.Append("</UpOrientation>");
        sb.Append("<LeftOrientation>");
        sb.Append(MakeXML("X", ListenerPosition.LeftOrientation.X.ToString()));
        sb.Append(MakeXML("Y", ListenerPosition.LeftOrientation.Y.ToString()));
        sb.Append(MakeXML("Z", ListenerPosition.LeftOrientation.Z.ToString()));
        sb.Append("</LeftOrientation>");
        sb.Append("</ListenerPosition>");
        return Request("Session.Set3DPosition.1", sb.ToString());
    }

    /// <summary>
    ///     Set User Volume for a particular user. Does not affect how other users hear that user.
    /// </summary>
    /// <param name="SessionHandle">Handle returned from successful Session �create� request or a SessionNewEvent</param>
    /// <param name="ParticipantURI"></param>
    /// <param name="Volume">The level of the audio, a number between -100 and 100 where 0 represents �normal� speaking volume</param>
    /// <returns></returns>
    public int SessionSetParticipantVolumeForMe(string SessionHandle, string ParticipantURI, int Volume)
    {
        var sb = new StringBuilder();
        sb.Append(MakeXML("SessionHandle", SessionHandle));
        sb.Append(MakeXML("ParticipantURI", ParticipantURI));
        sb.Append(MakeXML("Volume", Volume.ToString()));
        return Request("Session.SetParticipantVolumeForMe.1", sb.ToString());
    }
}