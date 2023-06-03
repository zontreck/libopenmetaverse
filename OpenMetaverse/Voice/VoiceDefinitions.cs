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
using System.Xml.Serialization;

namespace OpenMetaverse.Voice;

public partial class VoiceGateway
{
    /// <summary>
    ///     Event for most mundane request reposnses.
    /// </summary>
    public event EventHandler<VoiceResponseEventArgs> OnVoiceResponse;

    #region Connector Events

    /// <summary>Response to Connector.Create request</summary>
    public event EventHandler<VoiceConnectorEventArgs> OnConnectorCreateResponse;

    #endregion Connector Events

    #region Logging

    public class VoiceLoggingSettings
    {
        /// <summary>Enable logging</summary>
        public bool Enabled;

        /// <summary>This will be prepended to beginning of each log file</summary>
        public string FileNamePrefix;

        /// <summary>The suffix or extension to be appended to each log file</summary>
        public string FileNameSuffix;

        /// <summary>The folder where any logs will be created</summary>
        public string Folder;

        /// <summary>
        ///     0: NONE - No logging
        ///     1: ERROR - Log errors only
        ///     2: WARNING - Log errors and warnings
        ///     3: INFO - Log errors, warnings and info
        ///     4: DEBUG - Log errors, warnings, info and debug
        /// </summary>
        public int LogLevel;

        /// <summary>
        ///     Constructor for default logging settings
        /// </summary>
        public VoiceLoggingSettings()
        {
            Enabled = false;
            Folder = string.Empty;
            FileNamePrefix = "Connector";
            FileNameSuffix = ".log";
            LogLevel = 0;
        }
    }

    #endregion Logging

    public class VoiceResponseEventArgs : EventArgs
    {
        public readonly string Message;
        public readonly int ReturnCode;
        public readonly int StatusCode;
        public readonly ResponseType Type;

        // All Voice Response events carry these properties.
        public VoiceResponseEventArgs(ResponseType type, int rcode, int scode, string text)
        {
            Type = type;
            ReturnCode = rcode;
            StatusCode = scode;
            Message = text;
        }
    }

    #region Connector Delegates

    public class VoiceConnectorEventArgs : VoiceResponseEventArgs
    {
        public VoiceConnectorEventArgs(int rcode, int scode, string text, string version, string handle) :
            base(ResponseType.ConnectorCreate, rcode, scode, text)
        {
            Version = version;
            Handle = handle;
        }

        public string Version { get; }

        public string Handle { get; }
    }

    #endregion Connector Delegates

    #region Enums

    public enum LoginState
    {
        LoggedOut = 0,
        LoggedIn = 1,
        Error = 4
    }

    public enum SessionState
    {
        Idle = 1,
        Answering = 2,
        InProgress = 3,
        Connected = 4,
        Disconnected = 5,
        Hold = 6,
        Refer = 7,
        Ringing = 8
    }

    public enum ParticipantState
    {
        Idle = 1,
        Pending = 2,
        Incoming = 3,
        Answering = 4,
        InProgress = 5,
        Ringing = 6,
        Connected = 7,
        Disconnecting = 8,
        Disconnected = 9
    }

    public enum ParticipantType
    {
        User = 0,
        Moderator = 1,
        Focus = 2
    }

    public enum ResponseType
    {
        None = 0,
        ConnectorCreate,
        ConnectorInitiateShutdown,
        MuteLocalMic,
        MuteLocalSpeaker,
        SetLocalMicVolume,
        SetLocalSpeakerVolume,
        GetCaptureDevices,
        GetRenderDevices,
        SetRenderDevice,
        SetCaptureDevice,
        CaptureAudioStart,
        CaptureAudioStop,
        SetMicLevel,
        SetSpeakerLevel,
        AccountLogin,
        AccountLogout,
        RenderAudioStart,
        RenderAudioStop,
        SessionCreate,
        SessionConnect,
        SessionTerminate,
        SetParticipantVolumeForMe,
        SetParticipantMuteForMe,
        Set3DPosition
    }

    #endregion Enums

    #region Session Event Args

    public class VoiceSessionEventArgs : VoiceResponseEventArgs
    {
        public readonly string SessionHandle;

        public VoiceSessionEventArgs(int rcode, int scode, string text, string shandle) :
            base(ResponseType.SessionCreate, rcode, scode, text)
        {
            SessionHandle = shandle;
        }
    }

    public class NewSessionEventArgs : EventArgs
    {
        public readonly string AccountHandle;
        public readonly string AudioMedia;
        public readonly string Name;
        public readonly string SessionHandle;
        public readonly string URI;

        public NewSessionEventArgs(string AccountHandle, string SessionHandle, string URI, bool IsChannel, string Name,
            string AudioMedia)
        {
            this.AccountHandle = AccountHandle;
            this.SessionHandle = SessionHandle;
            this.URI = URI;
            this.Name = Name;
            this.AudioMedia = AudioMedia;
        }
    }

    public class SessionMediaEventArgs : EventArgs
    {
        public readonly bool HasAudio;
        public readonly bool HasText;
        public readonly bool HasVideo;
        public readonly string SessionHandle;
        public readonly bool Terminated;

        public SessionMediaEventArgs(string SessionHandle, bool HasText, bool HasAudio, bool HasVideo, bool Terminated)
        {
            this.SessionHandle = SessionHandle;
            this.HasText = HasText;
            this.HasAudio = HasAudio;
            this.HasVideo = HasVideo;
            this.Terminated = Terminated;
        }
    }

    public class SessionStateChangeEventArgs : EventArgs
    {
        public readonly string ChannelName;
        public readonly bool IsChannel;
        public readonly string SessionHandle;
        public readonly SessionState State;
        public readonly int StatusCode;
        public readonly string StatusString;
        public readonly string URI;

        public SessionStateChangeEventArgs(string SessionHandle, int StatusCode, string StatusString,
            SessionState State, string URI, bool IsChannel, string ChannelName)
        {
            this.SessionHandle = SessionHandle;
            this.StatusCode = StatusCode;
            this.StatusString = StatusString;
            this.State = State;
            this.URI = URI;
            this.IsChannel = IsChannel;
            this.ChannelName = ChannelName;
        }
    }

    // Participants
    public class ParticipantAddedEventArgs : EventArgs
    {
        public readonly string AccountName;
        public readonly string Appllication;
        public readonly string DisplayName;
        public readonly string SessionGroupHandle;
        public readonly string SessionHandle;
        public readonly ParticipantType Type;
        public readonly string URI;

        public ParticipantAddedEventArgs(
            string SessionGroupHandle,
            string SessionHandle,
            string ParticipantUri,
            string AccountName,
            string DisplayName,
            ParticipantType type,
            string Application)
        {
            this.SessionGroupHandle = SessionGroupHandle;
            this.SessionHandle = SessionHandle;
            URI = ParticipantUri;
            this.AccountName = AccountName;
            this.DisplayName = DisplayName;
            Type = type;
            Appllication = Application;
        }
    }

    public class ParticipantRemovedEventArgs : EventArgs
    {
        public readonly string AccountName;
        public readonly string Reason;
        public readonly string SessionGroupHandle;
        public readonly string SessionHandle;
        public readonly string URI;

        public ParticipantRemovedEventArgs(
            string SessionGroupHandle,
            string SessionHandle,
            string ParticipantUri,
            string AccountName,
            string Reason)
        {
            this.SessionGroupHandle = SessionGroupHandle;
            this.SessionHandle = SessionHandle;
            URI = ParticipantUri;
            this.AccountName = AccountName;
            this.Reason = Reason;
        }
    }

    public class ParticipantStateChangeEventArgs : EventArgs
    {
        public readonly string AccountName;
        public readonly string DisplayName;
        public readonly string SessionHandle;
        public readonly ParticipantState State;
        public readonly int StatusCode;
        public readonly string StatusString;
        public readonly ParticipantType Type;
        public readonly string URI;

        public ParticipantStateChangeEventArgs(string SessionHandle, int StatusCode, string StatusString,
            ParticipantState State, string ParticipantURI, string AccountName,
            string DisplayName, ParticipantType ParticipantType)
        {
            this.SessionHandle = SessionHandle;
            this.StatusCode = StatusCode;
            this.StatusString = StatusString;
            this.State = State;
            URI = ParticipantURI;
            this.AccountName = AccountName;
            this.DisplayName = DisplayName;
            Type = ParticipantType;
        }
    }

    public class ParticipantPropertiesEventArgs : EventArgs
    {
        public readonly float Energy;
        public readonly bool IsLocallyMuted;
        public readonly bool IsModeratorMuted;
        public readonly bool IsSpeaking;
        public readonly string SessionHandle;
        public readonly string URI;
        public readonly int Volume;

        public ParticipantPropertiesEventArgs(string SessionHandle, string ParticipantURI,
            bool IsLocallyMuted, bool IsModeratorMuted, bool IsSpeaking, int Volume, float Energy)
        {
            this.SessionHandle = SessionHandle;
            URI = ParticipantURI;
            this.IsLocallyMuted = IsLocallyMuted;
            this.IsModeratorMuted = IsModeratorMuted;
            this.IsSpeaking = IsSpeaking;
            this.Volume = Volume;
            this.Energy = Energy;
        }
    }

    public class ParticipantUpdatedEventArgs : EventArgs
    {
        public readonly float Energy;
        public readonly bool IsMuted;
        public readonly bool IsSpeaking;
        public readonly string SessionHandle;
        public readonly string URI;
        public readonly int Volume;

        public ParticipantUpdatedEventArgs(string sessionHandle, string URI, bool isMuted, bool isSpeaking, int volume,
            float energy)
        {
            SessionHandle = sessionHandle;
            this.URI = URI;
            IsMuted = isMuted;
            IsSpeaking = isSpeaking;
            Volume = volume;
            Energy = energy;
        }
    }

    public class SessionAddedEventArgs : EventArgs
    {
        public readonly bool IsChannel;
        public readonly bool IsIncoming;
        public readonly string SessionGroupHandle;
        public readonly string SessionHandle;
        public readonly string URI;

        public SessionAddedEventArgs(string sessionGroupHandle, string sessionHandle,
            string URI, bool isChannel, bool isIncoming)
        {
            SessionGroupHandle = sessionGroupHandle;
            SessionHandle = sessionHandle;
            this.URI = URI;
            IsChannel = isChannel;
            IsIncoming = isIncoming;
        }
    }

    public class SessionRemovedEventArgs : EventArgs
    {
        public readonly string SessionGroupHandle;
        public readonly string SessionHandle;
        public readonly string URI;

        public SessionRemovedEventArgs(
            string SessionGroupHandle,
            string SessionHandle,
            string Uri)
        {
            this.SessionGroupHandle = SessionGroupHandle;
            this.SessionHandle = SessionHandle;
            URI = Uri;
        }
    }

    public class SessionUpdatedEventArgs : EventArgs
    {
        public readonly bool IsFocused;
        public readonly bool IsMuted;
        public readonly string SessionGroupHandle;
        public readonly string SessionHandle;
        public readonly bool TransmitEnabled;
        public readonly string URI;
        public readonly int Volume;

        public SessionUpdatedEventArgs(string SessionGroupHandle,
            string SessionHandle, string URI, bool IsMuted, int Volume,
            bool TransmitEnabled, bool IsFocused)
        {
            this.SessionGroupHandle = SessionGroupHandle;
            this.SessionHandle = SessionHandle;
            this.URI = URI;
            this.IsMuted = IsMuted;
            this.Volume = Volume;
            this.TransmitEnabled = TransmitEnabled;
            this.IsFocused = IsFocused;
        }
    }

    public class SessionGroupAddedEventArgs : EventArgs
    {
        public readonly string AccountHandle;
        public readonly string SessionGroupHandle;
        public readonly string Type;

        public SessionGroupAddedEventArgs(string acctHandle, string sessionGroupHandle, string type)
        {
            AccountHandle = acctHandle;
            SessionGroupHandle = sessionGroupHandle;
            Type = type;
        }
    }

    #endregion Session Event Args


    #region Aux Event Args

    public class VoiceDevicesEventArgs : VoiceResponseEventArgs
    {
        public VoiceDevicesEventArgs(ResponseType type, int rcode, int scode, string text, string current,
            List<string> avail) :
            base(type, rcode, scode, text)
        {
            CurrentDevice = current;
            Devices = avail;
        }

        public string CurrentDevice { get; }

        public List<string> Devices { get; }
    }


    /// Audio Properties Events are sent after audio capture is started. These events are used to display a microphone VU meter
    public class AudioPropertiesEventArgs : EventArgs
    {
        public readonly bool IsMicActive;
        public readonly float MicEnergy;
        public readonly int MicVolume;
        public readonly int SpeakerVolume;

        public AudioPropertiesEventArgs(bool MicIsActive, float MicEnergy, int MicVolume, int SpeakerVolume)
        {
            IsMicActive = MicIsActive;
            this.MicEnergy = MicEnergy;
            this.MicVolume = MicVolume;
            this.SpeakerVolume = SpeakerVolume;
        }
    }

    #endregion Aux Event Args

    #region Account Event Args

    public class VoiceAccountEventArgs : VoiceResponseEventArgs
    {
        public VoiceAccountEventArgs(int rcode, int scode, string text, string ahandle) :
            base(ResponseType.AccountLogin, rcode, scode, text)
        {
            AccountHandle = ahandle;
        }

        public string AccountHandle { get; }
    }

    public class AccountLoginStateChangeEventArgs : EventArgs
    {
        public readonly string AccountHandle;
        public readonly LoginState State;
        public readonly int StatusCode;
        public readonly string StatusString;

        public AccountLoginStateChangeEventArgs(string AccountHandle, int StatusCode, string StatusString,
            LoginState State)
        {
            this.AccountHandle = AccountHandle;
            this.StatusCode = StatusCode;
            this.StatusString = StatusString;
            this.State = State;
        }
    }

    #endregion Account Event Args

    #region Session Events

    public event EventHandler<VoiceSessionEventArgs> OnSessionCreateResponse;
    public event EventHandler<NewSessionEventArgs> OnSessionNewEvent;
    public event EventHandler<SessionStateChangeEventArgs> OnSessionStateChangeEvent;
    public event EventHandler<ParticipantStateChangeEventArgs> OnSessionParticipantStateChangeEvent;
    public event EventHandler<ParticipantPropertiesEventArgs> OnSessionParticipantPropertiesEvent;
    public event EventHandler<ParticipantUpdatedEventArgs> OnSessionParticipantUpdatedEvent;
    public event EventHandler<ParticipantAddedEventArgs> OnSessionParticipantAddedEvent;
    public event EventHandler<ParticipantRemovedEventArgs> OnSessionParticipantRemovedEvent;
    public event EventHandler<SessionGroupAddedEventArgs> OnSessionGroupAddedEvent;
    public event EventHandler<SessionAddedEventArgs> OnSessionAddedEvent;
    public event EventHandler<SessionRemovedEventArgs> OnSessionRemovedEvent;
    public event EventHandler<SessionUpdatedEventArgs> OnSessionUpdatedEvent;
    public event EventHandler<SessionMediaEventArgs> OnSessionMediaEvent;

    #endregion Session Events

    #region Aux Events

    /// <summary>Response to Aux.GetCaptureDevices request</summary>
    public event EventHandler<VoiceDevicesEventArgs> OnAuxGetCaptureDevicesResponse;

    /// <summary>Response to Aux.GetRenderDevices request</summary>
    public event EventHandler<VoiceDevicesEventArgs> OnAuxGetRenderDevicesResponse;

    /// <summary>
    ///     Audio Properties Events are sent after audio capture is started.
    ///     These events are used to display a microphone VU meter
    /// </summary>
    public event EventHandler<AudioPropertiesEventArgs> OnAuxAudioPropertiesEvent;

    #endregion Aux Events

    #region Account Events

    /// <summary>Response to Account.Login request</summary>
    public event EventHandler<VoiceAccountEventArgs> OnAccountLoginResponse;

    /// <summary>
    ///     This event message is sent whenever the login state of the
    ///     particular Account has transitioned from one value to another
    /// </summary>
    public event EventHandler<AccountLoginStateChangeEventArgs> OnAccountLoginStateChangeEvent;

    #endregion Account Events

    #region XML Serialization Classes

    private readonly XmlSerializer EventSerializer = new(typeof(VoiceEvent));
    private readonly XmlSerializer ResponseSerializer = new(typeof(VoiceResponse));

    [XmlRoot("Event")]
    public class VoiceEvent
    {
        public string AccountHandle;
        public string AccountName;
        public string Application;
        public string AudioMedia;
        public string ChannelName;
        public string DisplayName;
        public string Energy;
        public string HasAudio;
        public string HasText;
        public string HasVideo;
        public string Incoming;
        public string IsChannel;
        public string IsFocused;
        public string IsIncoming;
        public string IsLocallyMuted;
        public string IsModeratorMuted;
        public string IsMuted;
        public string IsSpeaking;
        public string MicEnergy;
        public string MicIsActive;
        public string MicVolume;
        public string Name;
        public string ParticipantType;
        public string ParticipantUri;
        public string Reason;
        public string SessionGroupHandle;
        public string SessionHandle;
        public string SpeakerVolume;
        public string State;
        public string StatusCode;
        public string StatusString;
        public string Terminated;
        public string TransmitEnabled;

        [XmlAttribute("type")] public string Type;

        public string Uri; // Yes, they send it with both capitalizations
        public string URI;
        public string Volume;
    }

    [XmlRoot("Response")]
    public class VoiceResponse
    {
        [XmlAttribute("action")] public string Action;

        public VoiceInputXml InputXml;

        [XmlAttribute("requestId")] public string RequestId;

        public VoiceResponseResults Results;
        public string ReturnCode;
    }

    public class CaptureDevice
    {
        public string Device;
    }

    public class RenderDevice
    {
        public string Device;
    }

    public class VoiceResponseResults
    {
        public string AccountHandle;
        public List<CaptureDevice> CaptureDevices;
        public string ConnectorHandle;
        public CaptureDevice CurrentCaptureDevice;
        public RenderDevice CurrentRenderDevice;
        public List<RenderDevice> RenderDevices;
        public string SessionHandle;
        public string StatusCode;
        public string StatusString;
        public string VersionID;
    }

    public class VoiceInputXml
    {
        public VoiceRequest Request;
    }

    [XmlRoot("Request")]
    public class VoiceRequest
    {
        public string AccountManagementServer;
        public string AccountName;
        public string AccountPassword;
        public string AccountURI;

        [XmlAttribute("action")] public string Action;

        public string AudioSessionAnswerMode;
        public string CaptureDeviceSpecifier;
        public string ClientName;
        public string ConnectorHandle;
        public string Duration;
        public string EnableBuddiesAndPresence;
        public string JoinAudio;
        public string JoinText;
        public string Level;
        public VoicePosition ListenerPosition;
        public VoiceLoggingSettings Logging;
        public string Loop;
        public string MaximumPort;
        public string MinimumPort;
        public string Name;
        public string OrientationType;
        public string ParticipantPropertyFrequency;
        public string ParticipantURI;
        public string Password;
        public string PasswordHashAlgorithm;
        public string RenderDeviceSpecifier;

        [XmlAttribute("requestId")] public string RequestId;

        public string SessionHandle;
        public string SoundFilePath;
        public VoicePosition SpeakerPosition;
        public string URI;
        public string Value;
        public string Volume;
    }

    #endregion XML Serialization Classes
}

public class VoicePosition
{
    /// <summary>At Orientation (X axis) of the position</summary>
    public Vector3d AtOrientation;

    /// <summary>Left Orientation (Z axis) of the position</summary>
    public Vector3d LeftOrientation;

    /// <summary>Positional vector of the users position</summary>
    public Vector3d Position;

    /// <summary>Up Orientation (Y axis) of the position</summary>
    public Vector3d UpOrientation;

    /// <summary>Velocity vector of the position</summary>
    public Vector3d Velocity;
}