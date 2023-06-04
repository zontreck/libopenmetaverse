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
using OpenMetaverse.StructuredData;

namespace OpenMetaverse;

#region Enums

/// <summary>
///     Avatar profile flags
/// </summary>
[Flags]
public enum ProfileFlags : uint
{
    AllowPublish = 1,
    MaturePublish = 2,
    Identified = 4,
    Transacted = 8,
    Online = 16,
    AgeVerified = 32
}

#endregion Enums

/// <summary>
///     Represents an avatar (other than your own)
/// </summary>
public class Avatar : Primitive
{
    protected string groupName;

    protected string name;

    #region Constructors

    /// <summary>
    ///     Default constructor
    /// </summary>
    public Avatar()
    {
    }

    #endregion Constructors

    #region Subclasses

    /// <summary>
    ///     Positive and negative ratings
    /// </summary>
    public struct Statistics
    {
        /// <summary>Positive ratings for Behavior</summary>
        public int BehaviorPositive;

        /// <summary>Negative ratings for Behavior</summary>
        public int BehaviorNegative;

        /// <summary>Positive ratings for Appearance</summary>
        public int AppearancePositive;

        /// <summary>Negative ratings for Appearance</summary>
        public int AppearanceNegative;

        /// <summary>Positive ratings for Building</summary>
        public int BuildingPositive;

        /// <summary>Negative ratings for Building</summary>
        public int BuildingNegative;

        /// <summary>Positive ratings given by this avatar</summary>
        public int GivenPositive;

        /// <summary>Negative ratings given by this avatar</summary>
        public int GivenNegative;

        public OSD GetOSD()
        {
            var tex = new OSDMap(8);
            tex["behavior_positive"] = OSD.FromInteger(BehaviorPositive);
            tex["behavior_negative"] = OSD.FromInteger(BehaviorNegative);
            tex["appearance_positive"] = OSD.FromInteger(AppearancePositive);
            tex["appearance_negative"] = OSD.FromInteger(AppearanceNegative);
            tex["buildings_positive"] = OSD.FromInteger(BuildingPositive);
            tex["buildings_negative"] = OSD.FromInteger(BuildingNegative);
            tex["given_positive"] = OSD.FromInteger(GivenPositive);
            tex["given_negative"] = OSD.FromInteger(GivenNegative);
            return tex;
        }

        public static Statistics FromOSD(OSD O)
        {
            var S = new Statistics();
            var tex = (OSDMap)O;

            S.BehaviorPositive = tex["behavior_positive"].AsInteger();
            S.BuildingNegative = tex["behavior_negative"].AsInteger();
            S.AppearancePositive = tex["appearance_positive"].AsInteger();
            S.AppearanceNegative = tex["appearance_negative"].AsInteger();
            S.BuildingPositive = tex["buildings_positive"].AsInteger();
            S.BuildingNegative = tex["buildings_negative"].AsInteger();
            S.GivenPositive = tex["given_positive"].AsInteger();
            S.GivenNegative = tex["given_negative"].AsInteger();


            return S;
        }
    }

    /// <summary>
    ///     Avatar properties including about text, profile URL, image IDs and
    ///     publishing settings
    /// </summary>
    public struct AvatarProperties
    {
        /// <summary>First Life about text</summary>
        public string FirstLifeText;

        /// <summary>First Life image ID</summary>
        public UUID FirstLifeImage;

        /// <summary></summary>
        public UUID Partner;

        /// <summary></summary>
        public string AboutText;

        /// <summary></summary>
        public string BornOn;

        /// <summary></summary>
        public string CharterMember;

        /// <summary>Profile image ID</summary>
        public UUID ProfileImage;

        /// <summary>Flags of the profile</summary>
        public ProfileFlags Flags;

        /// <summary>Web URL for this profile</summary>
        public string ProfileURL;

        #region Properties

        /// <summary>Should this profile be published on the web</summary>
        public bool AllowPublish
        {
            get => (Flags & ProfileFlags.AllowPublish) != 0;
            set
            {
                if (value)
                    Flags |= ProfileFlags.AllowPublish;
                else
                    Flags &= ~ProfileFlags.AllowPublish;
            }
        }

        /// <summary>Avatar Online Status</summary>
        public bool Online
        {
            get => (Flags & ProfileFlags.Online) != 0;
            set
            {
                if (value)
                    Flags |= ProfileFlags.Online;
                else
                    Flags &= ~ProfileFlags.Online;
            }
        }

        /// <summary>Is this a mature profile</summary>
        public bool MaturePublish
        {
            get => (Flags & ProfileFlags.MaturePublish) != 0;
            set
            {
                if (value)
                    Flags |= ProfileFlags.MaturePublish;
                else
                    Flags &= ~ProfileFlags.MaturePublish;
            }
        }

        /// <summary></summary>
        public bool Identified
        {
            get => (Flags & ProfileFlags.Identified) != 0;
            set
            {
                if (value)
                    Flags |= ProfileFlags.Identified;
                else
                    Flags &= ~ProfileFlags.Identified;
            }
        }

        /// <summary></summary>
        public bool Transacted
        {
            get => (Flags & ProfileFlags.Transacted) != 0;
            set
            {
                if (value)
                    Flags |= ProfileFlags.Transacted;
                else
                    Flags &= ~ProfileFlags.Transacted;
            }
        }

        public OSD GetOSD()
        {
            var tex = new OSDMap(9);
            tex["first_life_text"] = OSD.FromString(FirstLifeText);
            tex["first_life_image"] = OSD.FromUUID(FirstLifeImage);
            tex["partner"] = OSD.FromUUID(Partner);
            tex["about_text"] = OSD.FromString(AboutText);
            tex["born_on"] = OSD.FromString(BornOn);
            tex["charter_member"] = OSD.FromString(CharterMember);
            tex["profile_image"] = OSD.FromUUID(ProfileImage);
            tex["flags"] = OSD.FromInteger((byte)Flags);
            tex["profile_url"] = OSD.FromString(ProfileURL);
            return tex;
        }

        public static AvatarProperties FromOSD(OSD O)
        {
            var A = new AvatarProperties();
            var tex = (OSDMap)O;

            A.FirstLifeText = tex["first_life_text"].AsString();
            A.FirstLifeImage = tex["first_life_image"].AsUUID();
            A.Partner = tex["partner"].AsUUID();
            A.AboutText = tex["about_text"].AsString();
            A.BornOn = tex["born_on"].AsString();
            A.CharterMember = tex["chart_member"].AsString();
            A.ProfileImage = tex["profile_image"].AsUUID();
            A.Flags = (ProfileFlags)tex["flags"].AsInteger();
            A.ProfileURL = tex["profile_url"].AsString();

            return A;
        }

        #endregion Properties
    }

    /// <summary>
    ///     Avatar interests including spoken languages, skills, and "want to"
    ///     choices
    /// </summary>
    public struct Interests
    {
        /// <summary>Languages profile field</summary>
        public string LanguagesText;

        /// <summary></summary>
        // FIXME:
        public uint SkillsMask;

        /// <summary></summary>
        public string SkillsText;

        /// <summary></summary>
        // FIXME:
        public uint WantToMask;

        /// <summary></summary>
        public string WantToText;

        public OSD GetOSD()
        {
            var InterestsOSD = new OSDMap(5);
            InterestsOSD["languages_text"] = OSD.FromString(LanguagesText);
            InterestsOSD["skills_mask"] = OSD.FromUInteger(SkillsMask);
            InterestsOSD["skills_text"] = OSD.FromString(SkillsText);
            InterestsOSD["want_to_mask"] = OSD.FromUInteger(WantToMask);
            InterestsOSD["want_to_text"] = OSD.FromString(WantToText);
            return InterestsOSD;
        }

        public static Interests FromOSD(OSD O)
        {
            var I = new Interests();
            var tex = (OSDMap)O;

            I.LanguagesText = tex["languages_text"].AsString();
            I.SkillsMask = tex["skills_mask"].AsUInteger();
            I.SkillsText = tex["skills_text"].AsString();
            I.WantToMask = tex["want_to_mask"].AsUInteger();
            I.WantToText = tex["want_to_text"].AsString();

            return I;
        }
    }

    #endregion Subclasses

    #region Public Members

    /// <summary>Groups that this avatar is a member of</summary>
    public List<UUID> Groups = new();

    /// <summary>Positive and negative ratings</summary>
    public Statistics ProfileStatistics;

    /// <summary>
    ///     Avatar properties including about text, profile URL, image IDs and
    ///     publishing settings
    /// </summary>
    public AvatarProperties ProfileProperties;

    /// <summary>
    ///     Avatar interests including spoken languages, skills, and "want to"
    ///     choices
    /// </summary>
    public Interests ProfileInterests;

    /// <summary>
    ///     Movement control flags for avatars. Typically not set or used by
    ///     clients. To move your avatar, use Client.Self.Movement instead
    /// </summary>
    public AgentManager.ControlFlags ControlFlags;

    /// <summary>
    ///     Contains the visual parameters describing the deformation of the avatar
    /// </summary>
    public byte[] VisualParameters;

    /// <summary>
    ///     Appearance version. Value greater than 0 indicates using server side baking
    /// </summary>
    public byte AppearanceVersion = 0;

    /// <summary>
    ///     Version of the Current Outfit Folder that the appearance is based on
    /// </summary>
    public int COFVersion = 0;

    /// <summary>
    ///     Appearance flags. Introduced with server side baking, currently unused.
    /// </summary>
    public AppearanceFlags AppearanceFlags = AppearanceFlags.None;

    /// <summary>
    ///     List of current avatar animations
    /// </summary>
    public List<Animation> Animations;

    #endregion Public Members

    #region Properties

    /// <summary>First name</summary>
    public string FirstName
    {
        get
        {
            for (var i = 0; i < NameValues.Length; i++)
                if (NameValues[i].Name == "FirstName" && NameValues[i].Type == NameValue.ValueType.String)
                    return (string)NameValues[i].Value;

            return string.Empty;
        }
    }

    /// <summary>Last name</summary>
    public string LastName
    {
        get
        {
            for (var i = 0; i < NameValues.Length; i++)
                if (NameValues[i].Name == "LastName" && NameValues[i].Type == NameValue.ValueType.String)
                    return (string)NameValues[i].Value;

            return string.Empty;
        }
    }

    /// <summary>Full name</summary>
    public string Name
    {
        get
        {
            if (!string.IsNullOrEmpty(name))
                return name;
            if (NameValues != null && NameValues.Length > 0)
                lock (NameValues)
                {
                    var firstName = string.Empty;
                    var lastName = string.Empty;

                    for (var i = 0; i < NameValues.Length; i++)
                        if (NameValues[i].Name == "FirstName" && NameValues[i].Type == NameValue.ValueType.String)
                            firstName = (string)NameValues[i].Value;
                        else if (NameValues[i].Name == "LastName" && NameValues[i].Type == NameValue.ValueType.String)
                            lastName = (string)NameValues[i].Value;

                    if (firstName != string.Empty && lastName != string.Empty)
                    {
                        name = string.Format("{0} {1}", firstName, lastName);
                        return name;
                    }

                    return string.Empty;
                }

            return string.Empty;
        }
    }

    /// <summary>Active group</summary>
    public string GroupName
    {
        get
        {
            if (!string.IsNullOrEmpty(groupName)) return groupName;

            if (NameValues == null || NameValues.Length == 0) return string.Empty;

            lock (NameValues)
            {
                for (var i = 0; i < NameValues.Length; i++)
                    if (NameValues[i].Name == "Title" && NameValues[i].Type == NameValue.ValueType.String)
                    {
                        groupName = (string)NameValues[i].Value;
                        return groupName;
                    }
            }

            return string.Empty;
        }
    }

    public override OSD GetOSD()
    {
        var Avi = (OSDMap)base.GetOSD();

        var grp = new OSDArray();
        Groups.ForEach(delegate(UUID u) { grp.Add(OSD.FromUUID(u)); });

        var vp = new OSDArray();

        for (var i = 0; i < VisualParameters.Length; i++) vp.Add(OSD.FromInteger(VisualParameters[i]));

        Avi["groups"] = grp;
        Avi["profile_statistics"] = ProfileStatistics.GetOSD();
        Avi["profile_properties"] = ProfileProperties.GetOSD();
        Avi["profile_interest"] = ProfileInterests.GetOSD();
        Avi["control_flags"] = OSD.FromInteger((byte)ControlFlags);
        Avi["visual_parameters"] = vp;
        Avi["first_name"] = OSD.FromString(FirstName);
        Avi["last_name"] = OSD.FromString(LastName);
        Avi["group_name"] = OSD.FromString(GroupName);

        return Avi;
    }

    public new static Avatar FromOSD(OSD O)
    {
        var tex = (OSDMap)O;

        var A = new Avatar();

        var P = Primitive.FromOSD(O);

        var Prim = typeof(Primitive);

        var Fields = Prim.GetFields();

        for (var x = 0; x < Fields.Length; x++)
        {
            Logger.Log("Field Matched in FromOSD: " + Fields[x].Name, Helpers.LogLevel.Debug);
            Fields[x].SetValue(A, Fields[x].GetValue(P));
        }

        A.Groups = new List<UUID>();

        foreach (var U in (OSDArray)tex["groups"]) A.Groups.Add(U.AsUUID());

        A.ProfileStatistics = Statistics.FromOSD(tex["profile_statistics"]);
        A.ProfileProperties = AvatarProperties.FromOSD(tex["profile_properties"]);
        A.ProfileInterests = Interests.FromOSD(tex["profile_interest"]);
        A.ControlFlags = (AgentManager.ControlFlags)tex["control_flags"].AsInteger();

        var vp = (OSDArray)tex["visual_parameters"];
        A.VisualParameters = new byte[vp.Count];

        for (var i = 0; i < vp.Count; i++) A.VisualParameters[i] = (byte)vp[i].AsInteger();

        // *********************From Code Above *******************************
        /*if (NameValues[i].Name == "FirstName" && NameValues[i].Type == NameValue.ValueType.String)
                          firstName = (string)NameValues[i].Value;
                      else if (NameValues[i].Name == "LastName" && NameValues[i].Type == NameValue.ValueType.String)
                          lastName = (string)NameValues[i].Value;*/
        // ********************************************************************

        A.NameValues = new NameValue[3];

        var First = new NameValue();
        First.Name = "FirstName";
        First.Type = NameValue.ValueType.String;
        First.Value = tex["first_name"].AsString();

        var Last = new NameValue();
        Last.Name = "LastName";
        Last.Type = NameValue.ValueType.String;
        Last.Value = tex["last_name"].AsString();

        // ***************From Code Above***************
        // if (NameValues[i].Name == "Title" && NameValues[i].Type == NameValue.ValueType.String)
        // *********************************************

        var Group = new NameValue();
        Group.Name = "Title";
        Group.Type = NameValue.ValueType.String;
        Group.Value = tex["group_name"].AsString();


        A.NameValues[0] = First;
        A.NameValues[1] = Last;
        A.NameValues[2] = Group;

        return A;
    }

    #endregion Properties
}