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

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using OpenMetaverse.StructuredData;

namespace OpenMetaverse.Assets;

/// <summary>
///     A linkset asset, containing a parent primitive and zero or more children
/// </summary>
public class AssetPrim : Asset
{
    public List<PrimObject> Children;

    public PrimObject Parent;

    /// <summary>Initializes a new instance of an AssetPrim object</summary>
    public AssetPrim()
    {
    }

    /// <summary>
    ///     Initializes a new instance of an AssetPrim object
    /// </summary>
    /// <param name="assetID">A unique <see cref="UUID" /> specific to this asset</param>
    /// <param name="assetData">A byte array containing the raw asset data</param>
    public AssetPrim(UUID assetID, byte[] assetData) : base(assetID, assetData)
    {
    }

    public AssetPrim(string xmlData)
    {
        DecodeXml(xmlData);
    }

    public AssetPrim(PrimObject parent, List<PrimObject> children)
    {
        Parent = parent;
        if (children != null)
            Children = children;
        else
            Children = new List<PrimObject>(0);
    }

    /// <summary>Override the base classes AssetType</summary>
    public override AssetType AssetType => AssetType.Object;

    /// <summary>
    /// </summary>
    public override void Encode()
    {
        AssetData = Encoding.UTF8.GetBytes(EncodeXml());
    }

    /// <summary>
    /// </summary>
    /// <returns></returns>
    public override bool Decode()
    {
        if (AssetData != null && AssetData.Length > 0)
            try
            {
                var xmlData = Encoding.UTF8.GetString(AssetData);
                DecodeXml(xmlData);
                return true;
            }
            catch
            {
            }

        return false;
    }

    public string EncodeXml()
    {
        TextWriter textWriter = new StringWriter();
        using (var xmlWriter = new XmlTextWriter(textWriter))
        {
            OarFile.SOGToXml2(xmlWriter, this);
            xmlWriter.Flush();
            return textWriter.ToString();
        }
    }

    public bool DecodeXml(string xmlData)
    {
        using (var reader = new XmlTextReader(new StringReader(xmlData)))
        {
            reader.DtdProcessing = DtdProcessing.Ignore;
            reader.Read();
            reader.ReadStartElement("SceneObjectGroup");
            Parent = LoadPrim(reader);

            if (Parent != null)
            {
                if (AssetID.IsZero())
                    AssetID = Parent.ID;

                var children = new List<PrimObject>();

                reader.Read();

                while (!reader.EOF)
                    switch (reader.NodeType)
                    {
                        case XmlNodeType.Element:
                            if (reader.Name == "SceneObjectPart")
                            {
                                var child = LoadPrim(reader);
                                if (child != null)
                                    children.Add(child);
                            }
                            else
                            {
                                //Logger.Log("Found unexpected prim XML element " + reader.Name, Helpers.LogLevel.Debug);
                                reader.Read();
                            }

                            break;
                        case XmlNodeType.EndElement:
                        default:
                            reader.Read();
                            break;
                    }

                Children = children;
                return true;
            }

            Logger.Log("Failed to load root linkset prim", Helpers.LogLevel.Error);
            return false;
        }
    }

    public static PrimObject LoadPrim(XmlTextReader reader)
    {
        var obj = new PrimObject();
        obj.Shape = new PrimObject.ShapeBlock();
        obj.Inventory = new PrimObject.InventoryBlock();

        reader.ReadStartElement("SceneObjectPart");

        if (reader.Name == "AllowedDrop")
            obj.AllowedDrop = reader.ReadElementContentAsBoolean("AllowedDrop", string.Empty);
        else
            obj.AllowedDrop = true;

        obj.CreatorID = ReadUUID(reader, "CreatorID");
        obj.FolderID = ReadUUID(reader, "FolderID");
        obj.Inventory.Serial = reader.ReadElementContentAsInt("InventorySerial", string.Empty);

        #region Task Inventory

        var invItems = new List<PrimObject.InventoryBlock.ItemBlock>();

        reader.ReadStartElement("TaskInventory", string.Empty);
        while (reader.Name == "TaskInventoryItem")
        {
            var item = new PrimObject.InventoryBlock.ItemBlock();
            reader.ReadStartElement("TaskInventoryItem", string.Empty);

            item.AssetID = ReadUUID(reader, "AssetID");
            item.PermsBase = (uint)reader.ReadElementContentAsInt("BasePermissions", string.Empty);
            item.CreationDate =
                Utils.UnixTimeToDateTime((uint)reader.ReadElementContentAsInt("CreationDate", string.Empty));
            item.CreatorID = ReadUUID(reader, "CreatorID");
            item.Description = reader.ReadElementContentAsString("Description", string.Empty);
            item.PermsEveryone = (uint)reader.ReadElementContentAsInt("EveryonePermissions", string.Empty);
            item.Flags = reader.ReadElementContentAsInt("Flags", string.Empty);
            item.GroupID = ReadUUID(reader, "GroupID");
            item.PermsGroup = (uint)reader.ReadElementContentAsInt("GroupPermissions", string.Empty);
            item.InvType = (InventoryType)reader.ReadElementContentAsInt("InvType", string.Empty);
            item.ID = ReadUUID(reader, "ItemID");
            var oldItemID = ReadUUID(reader, "OldItemID"); // TODO: Is this useful?
            item.LastOwnerID = ReadUUID(reader, "LastOwnerID");
            item.Name = reader.ReadElementContentAsString("Name", string.Empty);
            item.PermsNextOwner = (uint)reader.ReadElementContentAsInt("NextPermissions", string.Empty);
            item.OwnerID = ReadUUID(reader, "OwnerID");
            item.PermsOwner = (uint)reader.ReadElementContentAsInt("CurrentPermissions", string.Empty);
            var parentID = ReadUUID(reader, "ParentID");
            var parentPartID = ReadUUID(reader, "ParentPartID");
            item.PermsGranterID = ReadUUID(reader, "PermsGranter");
            item.PermsBase = (uint)reader.ReadElementContentAsInt("PermsMask", string.Empty);
            item.Type = (AssetType)reader.ReadElementContentAsInt("Type", string.Empty);

            reader.ReadEndElement();
            invItems.Add(item);
        }

        if (reader.NodeType == XmlNodeType.EndElement)
            reader.ReadEndElement();

        obj.Inventory.Items = invItems.ToArray();

        #endregion Task Inventory

        var flags = (PrimFlags)reader.ReadElementContentAsInt("ObjectFlags", string.Empty);
        obj.UsePhysics = (flags & PrimFlags.Physics) != 0;
        obj.Phantom = (flags & PrimFlags.Phantom) != 0;
        obj.DieAtEdge = (flags & PrimFlags.DieAtEdge) != 0;
        obj.ReturnAtEdge = (flags & PrimFlags.ReturnAtEdge) != 0;
        obj.Temporary = (flags & PrimFlags.Temporary) != 0;
        obj.Sandbox = (flags & PrimFlags.Sandbox) != 0;

        obj.ID = ReadUUID(reader, "UUID");
        obj.LocalID = (uint)reader.ReadElementContentAsLong("LocalId", string.Empty);
        obj.Name = reader.ReadElementString("Name");
        obj.Material = reader.ReadElementContentAsInt("Material", string.Empty);

        if (reader.Name == "PassTouches")
            obj.PassTouches = reader.ReadElementContentAsBoolean("PassTouches", string.Empty);
        else
            obj.PassTouches = false;

        obj.RegionHandle = (ulong)reader.ReadElementContentAsLong("RegionHandle", string.Empty);
        obj.RemoteScriptAccessPIN = reader.ReadElementContentAsInt("ScriptAccessPin", string.Empty);

        if (reader.Name == "PlaySoundSlavePrims")
            reader.ReadInnerXml();
        if (reader.Name == "LoopSoundSlavePrims")
            reader.ReadInnerXml();

        var groupPosition = ReadVector(reader, "GroupPosition");
        var offsetPosition = ReadVector(reader, "OffsetPosition");
        obj.Rotation = ReadQuaternion(reader, "RotationOffset");
        obj.Velocity = ReadVector(reader, "Velocity");
        if (reader.Name == "RotationalVelocity")
            ReadVector(reader, "RotationalVelocity");
        obj.AngularVelocity = ReadVector(reader, "AngularVelocity");
        obj.Acceleration = ReadVector(reader, "Acceleration");
        obj.Description = reader.ReadElementString("Description");
        reader.ReadStartElement("Color");
        if (reader.Name == "R")
        {
            obj.TextColor.R = reader.ReadElementContentAsFloat("R", string.Empty);
            obj.TextColor.G = reader.ReadElementContentAsFloat("G", string.Empty);
            obj.TextColor.B = reader.ReadElementContentAsFloat("B", string.Empty);
            obj.TextColor.A = reader.ReadElementContentAsFloat("A", string.Empty);
            reader.ReadEndElement();
        }

        obj.Text = reader.ReadElementString("Text", string.Empty);
        obj.SitName = reader.ReadElementString("SitName", string.Empty);
        obj.TouchName = reader.ReadElementString("TouchName", string.Empty);

        obj.LinkNumber = reader.ReadElementContentAsInt("LinkNum", string.Empty);
        obj.ClickAction = reader.ReadElementContentAsInt("ClickAction", string.Empty);

        reader.ReadStartElement("Shape");
        obj.Shape.ProfileCurve = reader.ReadElementContentAsInt("ProfileCurve", string.Empty);

        var teData = Convert.FromBase64String(reader.ReadElementString("TextureEntry"));
        obj.Textures = new Primitive.TextureEntry(teData, 0, teData.Length);

        reader.ReadInnerXml(); // ExtraParams

        obj.Shape.PathBegin =
            Primitive.UnpackBeginCut((ushort)reader.ReadElementContentAsInt("PathBegin", string.Empty));
        obj.Shape.PathCurve = reader.ReadElementContentAsInt("PathCurve", string.Empty);
        obj.Shape.PathEnd = Primitive.UnpackEndCut((ushort)reader.ReadElementContentAsInt("PathEnd", string.Empty));
        obj.Shape.PathRadiusOffset =
            Primitive.UnpackPathTwist((sbyte)reader.ReadElementContentAsInt("PathRadiusOffset", string.Empty));
        obj.Shape.PathRevolutions =
            Primitive.UnpackPathRevolutions((byte)reader.ReadElementContentAsInt("PathRevolutions", string.Empty));
        obj.Shape.PathScaleX =
            Primitive.UnpackPathScale((byte)reader.ReadElementContentAsInt("PathScaleX", string.Empty));
        obj.Shape.PathScaleY =
            Primitive.UnpackPathScale((byte)reader.ReadElementContentAsInt("PathScaleY", string.Empty));
        obj.Shape.PathShearX =
            Primitive.UnpackPathShear((sbyte)reader.ReadElementContentAsInt("PathShearX", string.Empty));
        obj.Shape.PathShearY =
            Primitive.UnpackPathShear((sbyte)reader.ReadElementContentAsInt("PathShearY", string.Empty));
        obj.Shape.PathSkew = Primitive.UnpackPathTwist((sbyte)reader.ReadElementContentAsInt("PathSkew", string.Empty));
        obj.Shape.PathTaperX =
            Primitive.UnpackPathTaper((sbyte)reader.ReadElementContentAsInt("PathTaperX", string.Empty));
        obj.Shape.PathTaperY =
            Primitive.UnpackPathShear((sbyte)reader.ReadElementContentAsInt("PathTaperY", string.Empty));
        obj.Shape.PathTwist =
            Primitive.UnpackPathTwist((sbyte)reader.ReadElementContentAsInt("PathTwist", string.Empty));
        obj.Shape.PathTwistBegin =
            Primitive.UnpackPathTwist((sbyte)reader.ReadElementContentAsInt("PathTwistBegin", string.Empty));
        obj.PCode = reader.ReadElementContentAsInt("PCode", string.Empty);
        obj.Shape.ProfileBegin =
            Primitive.UnpackBeginCut((ushort)reader.ReadElementContentAsInt("ProfileBegin", string.Empty));
        obj.Shape.ProfileEnd =
            Primitive.UnpackEndCut((ushort)reader.ReadElementContentAsInt("ProfileEnd", string.Empty));
        obj.Shape.ProfileHollow =
            Primitive.UnpackProfileHollow((ushort)reader.ReadElementContentAsInt("ProfileHollow", string.Empty));
        obj.Scale = ReadVector(reader, "Scale");
        obj.State = (byte)reader.ReadElementContentAsInt("State", string.Empty);

        var profileShape = (ProfileShape)Enum.Parse(typeof(ProfileShape), reader.ReadElementString("ProfileShape"));
        var holeType = (HoleType)Enum.Parse(typeof(HoleType), reader.ReadElementString("HollowShape"));
        obj.Shape.ProfileCurve = (int)profileShape | (int)holeType;

        var sculptTexture = ReadUUID(reader, "SculptTexture");
        var sculptType = (SculptType)reader.ReadElementContentAsInt("SculptType", string.Empty);
        if (sculptTexture != UUID.Zero)
        {
            obj.Sculpt = new PrimObject.SculptBlock();
            obj.Sculpt.Texture = sculptTexture;
            obj.Sculpt.Type = (int)sculptType;
        }

        var flexible = new PrimObject.FlexibleBlock();
        var light = new PrimObject.LightBlock();

        reader.ReadInnerXml(); // SculptData

        flexible.Softness = reader.ReadElementContentAsInt("FlexiSoftness", string.Empty);
        flexible.Tension = reader.ReadElementContentAsFloat("FlexiTension", string.Empty);
        flexible.Drag = reader.ReadElementContentAsFloat("FlexiDrag", string.Empty);
        flexible.Gravity = reader.ReadElementContentAsFloat("FlexiGravity", string.Empty);
        flexible.Wind = reader.ReadElementContentAsFloat("FlexiWind", string.Empty);
        flexible.Force.X = reader.ReadElementContentAsFloat("FlexiForceX", string.Empty);
        flexible.Force.Y = reader.ReadElementContentAsFloat("FlexiForceY", string.Empty);
        flexible.Force.Z = reader.ReadElementContentAsFloat("FlexiForceZ", string.Empty);

        light.Color.R = reader.ReadElementContentAsFloat("LightColorR", string.Empty);
        light.Color.G = reader.ReadElementContentAsFloat("LightColorG", string.Empty);
        light.Color.B = reader.ReadElementContentAsFloat("LightColorB", string.Empty);
        light.Color.A = reader.ReadElementContentAsFloat("LightColorA", string.Empty);
        light.Radius = reader.ReadElementContentAsFloat("LightRadius", string.Empty);
        light.Cutoff = reader.ReadElementContentAsFloat("LightCutoff", string.Empty);
        light.Falloff = reader.ReadElementContentAsFloat("LightFalloff", string.Empty);
        light.Intensity = reader.ReadElementContentAsFloat("LightIntensity", string.Empty);

        var hasFlexi = reader.ReadElementContentAsBoolean("FlexiEntry", string.Empty);
        var hasLight = reader.ReadElementContentAsBoolean("LightEntry", string.Empty);
        reader.ReadInnerXml(); // SculptEntry

        if (hasFlexi)
            obj.Flexible = flexible;
        if (hasLight)
            obj.Light = light;

        reader.ReadEndElement();

        obj.Scale = ReadVector(reader, "Scale"); // Yes, again
        reader.ReadInnerXml(); // UpdateFlag

        reader.ReadInnerXml(); // SitTargetOrientation
        reader.ReadInnerXml(); // SitTargetPosition
        obj.SitOffset = ReadVector(reader, "SitTargetPositionLL");
        obj.SitRotation = ReadQuaternion(reader, "SitTargetOrientationLL");
        obj.ParentID = (uint)reader.ReadElementContentAsLong("ParentID", string.Empty);
        obj.CreationDate = Utils.UnixTimeToDateTime(reader.ReadElementContentAsInt("CreationDate", string.Empty));
        var category = reader.ReadElementContentAsInt("Category", string.Empty);
        obj.SalePrice = reader.ReadElementContentAsInt("SalePrice", string.Empty);
        obj.SaleType = reader.ReadElementContentAsInt("ObjectSaleType", string.Empty);
        var ownershipCost = reader.ReadElementContentAsInt("OwnershipCost", string.Empty);
        obj.GroupID = ReadUUID(reader, "GroupID");
        obj.OwnerID = ReadUUID(reader, "OwnerID");
        obj.LastOwnerID = ReadUUID(reader, "LastOwnerID");
        obj.PermsBase = (uint)reader.ReadElementContentAsInt("BaseMask", string.Empty);
        obj.PermsOwner = (uint)reader.ReadElementContentAsInt("OwnerMask", string.Empty);
        obj.PermsGroup = (uint)reader.ReadElementContentAsInt("GroupMask", string.Empty);
        obj.PermsEveryone = (uint)reader.ReadElementContentAsInt("EveryoneMask", string.Empty);
        obj.PermsNextOwner = (uint)reader.ReadElementContentAsInt("NextOwnerMask", string.Empty);

        reader.ReadInnerXml(); // Flags

        obj.CollisionSound = ReadUUID(reader, "CollisionSound");
        obj.CollisionSoundVolume = reader.ReadElementContentAsFloat("CollisionSoundVolume", string.Empty);

        reader.ReadEndElement();

        if (obj.ParentID == 0)
            obj.Position = groupPosition;
        else
            obj.Position = offsetPosition;

        return obj;
    }

    private static UUID ReadUUID(XmlTextReader reader, string name)
    {
        UUID id;
        string idStr;

        reader.ReadStartElement(name);

        if (reader.Name == "Guid")
            idStr = reader.ReadElementString("Guid");
        else // UUID
            idStr = reader.ReadElementString("UUID");

        UUID.TryParse(idStr, out id);
        reader.ReadEndElement();

        return id;
    }

    private static Vector3 ReadVector(XmlTextReader reader, string name)
    {
        reader.ReadStartElement(name);
        var x = reader.ReadElementContentAsFloat("X", string.Empty);
        var y = reader.ReadElementContentAsFloat("Y", string.Empty);
        var z = reader.ReadElementContentAsFloat("Z", string.Empty);
        reader.ReadEndElement();

        return new Vector3(x, y, z);
    }

    private static Quaternion ReadQuaternion(XmlTextReader reader, string name)
    {
        Quaternion quat;

        reader.ReadStartElement(name);
        quat.X = reader.ReadElementContentAsFloat("X", string.Empty);
        quat.Y = reader.ReadElementContentAsFloat("Y", string.Empty);
        quat.Z = reader.ReadElementContentAsFloat("Z", string.Empty);
        quat.W = reader.ReadElementContentAsFloat("W", string.Empty);
        reader.ReadEndElement();

        return quat;
    }

    /// <summary>
    ///     Only used internally for XML serialization/deserialization
    /// </summary>
    internal enum ProfileShape : byte
    {
        Circle = 0,
        Square = 1,
        IsometricTriangle = 2,
        EquilateralTriangle = 3,
        RightTriangle = 4,
        HalfCircle = 5
    }
}

/// <summary>
///     The deserialized form of a single primitive in a linkset asset
/// </summary>
public class PrimObject
{
    public Vector3 Acceleration;
    public bool AllowedDrop;
    public Vector3 AngularVelocity;
    public Vector3 AttachmentPosition;
    public Quaternion AttachmentRotation;
    public Quaternion BeforeAttachmentRotation;
    public Vector3 CameraAtOffset;
    public Vector3 CameraEyeOffset;
    public int ClickAction;
    public UUID CollisionSound;
    public float CollisionSoundVolume;
    public DateTime CreationDate;
    public UUID CreatorID;
    public string Description;
    public bool DieAtEdge;
    public FlexibleBlock Flexible;
    public UUID FolderID;
    public UUID GroupID;

    public UUID ID;
    public InventoryBlock Inventory;
    public int LastAttachmentPoint;
    public UUID LastOwnerID;
    public LightBlock Light;
    public int LinkNumber;
    public uint LocalID;
    public int Material;
    public string Name;
    public UUID OwnerID;
    public uint ParentID;
    public ParticlesBlock Particles;
    public bool PassTouches;
    public int PCode;
    public uint PermsBase;
    public uint PermsEveryone;
    public uint PermsGroup;
    public uint PermsNextOwner;
    public uint PermsOwner;
    public bool Phantom;
    public Vector3 Position;
    public ulong RegionHandle;
    public int RemoteScriptAccessPIN;
    public bool ReturnAtEdge;
    public DateTime RezDate;
    public Quaternion Rotation;
    public int SalePrice;
    public int SaleType;
    public bool Sandbox;
    public Vector3 Scale;
    public byte[] ScriptState;
    public SculptBlock Sculpt;
    public bool Selected;
    public UUID SelectorID;
    public ShapeBlock Shape;
    public string SitName;
    public Vector3 SitOffset;
    public Quaternion SitRotation;
    public int SoundFlags;
    public float SoundGain;
    public UUID SoundID;
    public float SoundRadius;
    public int State;
    public bool Temporary;
    public string Text;
    public Color4 TextColor;
    public Primitive.TextureEntry Textures;
    public string TouchName;
    public bool UsePhysics;
    public Vector3 Velocity;
    public bool VolumeDetect;

    public OSDMap Serialize()
    {
        var map = new OSDMap();
        map["id"] = OSD.FromUUID(ID);
        map["attachment_position"] = OSD.FromVector3(AttachmentPosition);
        map["attachment_rotation"] = OSD.FromQuaternion(AttachmentRotation);
        map["before_attachment_rotation"] = OSD.FromQuaternion(BeforeAttachmentRotation);
        map["name"] = OSD.FromString(Name);
        map["description"] = OSD.FromString(Description);
        map["perms_base"] = OSD.FromInteger(PermsBase);
        map["perms_owner"] = OSD.FromInteger(PermsOwner);
        map["perms_group"] = OSD.FromInteger(PermsGroup);
        map["perms_everyone"] = OSD.FromInteger(PermsEveryone);
        map["perms_next_owner"] = OSD.FromInteger(PermsNextOwner);
        map["creator_identity"] = OSD.FromUUID(CreatorID);
        map["owner_identity"] = OSD.FromUUID(OwnerID);
        map["last_owner_identity"] = OSD.FromUUID(LastOwnerID);
        map["group_identity"] = OSD.FromUUID(GroupID);
        map["folder_id"] = OSD.FromUUID(FolderID);
        map["region_handle"] = OSD.FromULong(RegionHandle);
        map["click_action"] = OSD.FromInteger(ClickAction);
        map["last_attachment_point"] = OSD.FromInteger(LastAttachmentPoint);
        map["link_number"] = OSD.FromInteger(LinkNumber);
        map["local_id"] = OSD.FromInteger(LocalID);
        map["parent_id"] = OSD.FromInteger(ParentID);
        map["position"] = OSD.FromVector3(Position);
        map["rotation"] = OSD.FromQuaternion(Rotation);
        map["velocity"] = OSD.FromVector3(Velocity);
        map["angular_velocity"] = OSD.FromVector3(AngularVelocity);
        map["acceleration"] = OSD.FromVector3(Acceleration);
        map["scale"] = OSD.FromVector3(Scale);
        map["sit_offset"] = OSD.FromVector3(SitOffset);
        map["sit_rotation"] = OSD.FromQuaternion(SitRotation);
        map["camera_eye_offset"] = OSD.FromVector3(CameraEyeOffset);
        map["camera_at_offset"] = OSD.FromVector3(CameraAtOffset);
        map["state"] = OSD.FromInteger(State);
        map["prim_code"] = OSD.FromInteger(PCode);
        map["material"] = OSD.FromInteger(Material);
        map["pass_touches"] = OSD.FromBoolean(PassTouches);
        map["sound_id"] = OSD.FromUUID(SoundID);
        map["sound_gain"] = OSD.FromReal(SoundGain);
        map["sound_radius"] = OSD.FromReal(SoundRadius);
        map["sound_flags"] = OSD.FromInteger(SoundFlags);
        map["text_color"] = OSD.FromColor4(TextColor);
        map["text"] = OSD.FromString(Text);
        map["sit_name"] = OSD.FromString(SitName);
        map["touch_name"] = OSD.FromString(TouchName);
        map["selected"] = OSD.FromBoolean(Selected);
        map["selector_id"] = OSD.FromUUID(SelectorID);
        map["use_physics"] = OSD.FromBoolean(UsePhysics);
        map["phantom"] = OSD.FromBoolean(Phantom);
        map["remote_script_access_pin"] = OSD.FromInteger(RemoteScriptAccessPIN);
        map["volume_detect"] = OSD.FromBoolean(VolumeDetect);
        map["die_at_edge"] = OSD.FromBoolean(DieAtEdge);
        map["return_at_edge"] = OSD.FromBoolean(ReturnAtEdge);
        map["temporary"] = OSD.FromBoolean(Temporary);
        map["sandbox"] = OSD.FromBoolean(Sandbox);
        map["creation_date"] = OSD.FromDate(CreationDate);
        map["rez_date"] = OSD.FromDate(RezDate);
        map["sale_price"] = OSD.FromInteger(SalePrice);
        map["sale_type"] = OSD.FromInteger(SaleType);

        if (Flexible != null)
            map["flexible"] = Flexible.Serialize();
        if (Light != null)
            map["light"] = Light.Serialize();
        if (Sculpt != null)
            map["sculpt"] = Sculpt.Serialize();
        if (Particles != null)
            map["particles"] = Particles.Serialize();
        if (Shape != null)
            map["shape"] = Shape.Serialize();
        if (Textures != null)
            map["textures"] = Textures.GetOSD();
        if (Inventory != null)
            map["inventory"] = Inventory.Serialize();

        return map;
    }

    public void Deserialize(OSDMap map)
    {
        ID = map["id"].AsUUID();
        AttachmentPosition = map["attachment_position"].AsVector3();
        AttachmentRotation = map["attachment_rotation"].AsQuaternion();
        BeforeAttachmentRotation = map["before_attachment_rotation"].AsQuaternion();
        Name = map["name"].AsString();
        Description = map["description"].AsString();
        PermsBase = (uint)map["perms_base"].AsInteger();
        PermsOwner = (uint)map["perms_owner"].AsInteger();
        PermsGroup = (uint)map["perms_group"].AsInteger();
        PermsEveryone = (uint)map["perms_everyone"].AsInteger();
        PermsNextOwner = (uint)map["perms_next_owner"].AsInteger();
        CreatorID = map["creator_identity"].AsUUID();
        OwnerID = map["owner_identity"].AsUUID();
        LastOwnerID = map["last_owner_identity"].AsUUID();
        GroupID = map["group_identity"].AsUUID();
        FolderID = map["folder_id"].AsUUID();
        RegionHandle = map["region_handle"].AsULong();
        ClickAction = map["click_action"].AsInteger();
        LastAttachmentPoint = map["last_attachment_point"].AsInteger();
        LinkNumber = map["link_number"].AsInteger();
        LocalID = (uint)map["local_id"].AsInteger();
        ParentID = (uint)map["parent_id"].AsInteger();
        Position = map["position"].AsVector3();
        Rotation = map["rotation"].AsQuaternion();
        Velocity = map["velocity"].AsVector3();
        AngularVelocity = map["angular_velocity"].AsVector3();
        Acceleration = map["acceleration"].AsVector3();
        Scale = map["scale"].AsVector3();
        SitOffset = map["sit_offset"].AsVector3();
        SitRotation = map["sit_rotation"].AsQuaternion();
        CameraEyeOffset = map["camera_eye_offset"].AsVector3();
        CameraAtOffset = map["camera_at_offset"].AsVector3();
        State = map["state"].AsInteger();
        PCode = map["prim_code"].AsInteger();
        Material = map["material"].AsInteger();
        PassTouches = map["pass_touches"].AsBoolean();
        SoundID = map["sound_id"].AsUUID();
        SoundGain = (float)map["sound_gain"].AsReal();
        SoundRadius = (float)map["sound_radius"].AsReal();
        SoundFlags = map["sound_flags"].AsInteger();
        TextColor = map["text_color"].AsColor4();
        Text = map["text"].AsString();
        SitName = map["sit_name"].AsString();
        TouchName = map["touch_name"].AsString();
        Selected = map["selected"].AsBoolean();
        SelectorID = map["selector_id"].AsUUID();
        UsePhysics = map["use_physics"].AsBoolean();
        Phantom = map["phantom"].AsBoolean();
        RemoteScriptAccessPIN = map["remote_script_access_pin"].AsInteger();
        VolumeDetect = map["volume_detect"].AsBoolean();
        DieAtEdge = map["die_at_edge"].AsBoolean();
        ReturnAtEdge = map["return_at_edge"].AsBoolean();
        Temporary = map["temporary"].AsBoolean();
        Sandbox = map["sandbox"].AsBoolean();
        CreationDate = map["creation_date"].AsDate();
        RezDate = map["rez_date"].AsDate();
        SalePrice = map["sale_price"].AsInteger();
        SaleType = map["sale_type"].AsInteger();
    }

    public static PrimObject FromPrimitive(Primitive obj)
    {
        var prim = new PrimObject();
        prim.Acceleration = obj.Acceleration;
        prim.AllowedDrop = (obj.Flags & PrimFlags.AllowInventoryDrop) == PrimFlags.AllowInventoryDrop;
        prim.AngularVelocity = obj.AngularVelocity;
        //prim.AttachmentPosition
        //prim.AttachmentRotation
        //prim.BeforeAttachmentRotation
        //prim.CameraAtOffset
        //prim.CameraEyeOffset
        prim.ClickAction = (int)obj.ClickAction;
        //prim.CollisionSound
        //prim.CollisionSoundVolume;
        prim.CreationDate = obj.Properties.CreationDate;
        prim.CreatorID = obj.Properties.CreatorID;
        prim.Description = obj.Properties.Description;
        prim.DieAtEdge = (obj.Flags & PrimFlags.DieAtEdge) == PrimFlags.AllowInventoryDrop;
        if (obj.Flexible != null)
        {
            prim.Flexible = new FlexibleBlock();
            prim.Flexible.Drag = obj.Flexible.Drag;
            prim.Flexible.Force = obj.Flexible.Force;
            prim.Flexible.Gravity = obj.Flexible.Gravity;
            prim.Flexible.Softness = obj.Flexible.Softness;
            prim.Flexible.Tension = obj.Flexible.Tension;
            prim.Flexible.Wind = obj.Flexible.Wind;
        }

        prim.FolderID = obj.Properties.FolderID;
        prim.GroupID = obj.Properties.GroupID;
        prim.ID = obj.Properties.ObjectID;
        //prim.Inventory;
        //prim.LastAttachmentPoint;
        prim.LastOwnerID = obj.Properties.LastOwnerID;
        if (obj.Light != null)
        {
            prim.Light = new LightBlock();
            prim.Light.Color = obj.Light.Color;
            prim.Light.Cutoff = obj.Light.Cutoff;
            prim.Light.Falloff = obj.Light.Falloff;
            prim.Light.Intensity = obj.Light.Intensity;
            prim.Light.Radius = obj.Light.Radius;
        }

        //prim.LinkNumber;
        prim.LocalID = obj.LocalID;
        prim.Material = (int)obj.PrimData.Material;
        prim.Name = obj.Properties.Name;
        prim.OwnerID = obj.Properties.OwnerID;
        prim.ParentID = obj.ParentID;

        prim.Particles = new ParticlesBlock();
        prim.Particles.AngularVelocity = obj.ParticleSys.AngularVelocity;
        prim.Particles.Acceleration = obj.ParticleSys.PartAcceleration;
        prim.Particles.BurstParticleCount = obj.ParticleSys.BurstPartCount;
        prim.Particles.BurstRate = obj.ParticleSys.BurstRadius;
        prim.Particles.BurstRate = obj.ParticleSys.BurstRate;
        prim.Particles.BurstSpeedMax = obj.ParticleSys.BurstSpeedMax;
        prim.Particles.BurstSpeedMin = obj.ParticleSys.BurstSpeedMin;
        prim.Particles.DataFlags = (int)obj.ParticleSys.PartDataFlags;
        prim.Particles.Flags = (int)obj.ParticleSys.PartFlags;
        prim.Particles.InnerAngle = obj.ParticleSys.InnerAngle;
        prim.Particles.MaxAge = obj.ParticleSys.MaxAge;
        prim.Particles.OuterAngle = obj.ParticleSys.OuterAngle;
        prim.Particles.ParticleEndColor = obj.ParticleSys.PartEndColor;
        prim.Particles.ParticleEndScale = new Vector2(obj.ParticleSys.PartEndScaleX, obj.ParticleSys.PartEndScaleY);
        prim.Particles.ParticleMaxAge = obj.ParticleSys.MaxAge;
        prim.Particles.ParticleStartColor = obj.ParticleSys.PartStartColor;
        prim.Particles.ParticleStartScale =
            new Vector2(obj.ParticleSys.PartStartScaleX, obj.ParticleSys.PartStartScaleY);
        prim.Particles.Pattern = (int)obj.ParticleSys.Pattern;
        prim.Particles.StartAge = obj.ParticleSys.StartAge;
        prim.Particles.TargetID = obj.ParticleSys.Target;
        prim.Particles.TextureID = obj.ParticleSys.Texture;

        //prim.PassTouches;
        prim.PCode = (int)obj.PrimData.PCode;
        prim.PermsBase = (uint)obj.Properties.Permissions.BaseMask;
        prim.PermsEveryone = (uint)obj.Properties.Permissions.EveryoneMask;
        prim.PermsGroup = (uint)obj.Properties.Permissions.GroupMask;
        prim.PermsNextOwner = (uint)obj.Properties.Permissions.NextOwnerMask;
        prim.PermsOwner = (uint)obj.Properties.Permissions.OwnerMask;
        prim.Phantom = (obj.Flags & PrimFlags.Phantom) == PrimFlags.Phantom;
        prim.Position = obj.Position;
        prim.RegionHandle = obj.RegionHandle;
        //prim.RemoteScriptAccessPIN;
        prim.ReturnAtEdge = (obj.Flags & PrimFlags.ReturnAtEdge) == PrimFlags.ReturnAtEdge;
        //prim.RezDate;
        prim.Rotation = obj.Rotation;
        prim.SalePrice = obj.Properties.SalePrice;
        prim.SaleType = (int)obj.Properties.SaleType;
        prim.Sandbox = (obj.Flags & PrimFlags.Sandbox) == PrimFlags.Sandbox;
        prim.Scale = obj.Scale;
        //prim.ScriptState;
        if (obj.Sculpt != null)
        {
            prim.Sculpt = new SculptBlock();
            prim.Sculpt.Texture = obj.Sculpt.SculptTexture;
            prim.Sculpt.Type = (int)obj.Sculpt.Type;
        }

        prim.Shape = new ShapeBlock();
        prim.Shape.PathBegin = obj.PrimData.PathBegin;
        prim.Shape.PathCurve = (int)obj.PrimData.PathCurve;
        prim.Shape.PathEnd = obj.PrimData.PathEnd;
        prim.Shape.PathRadiusOffset = obj.PrimData.PathRadiusOffset;
        prim.Shape.PathRevolutions = obj.PrimData.PathRevolutions;
        prim.Shape.PathScaleX = obj.PrimData.PathScaleX;
        prim.Shape.PathScaleY = obj.PrimData.PathScaleY;
        prim.Shape.PathShearX = obj.PrimData.PathShearX;
        prim.Shape.PathShearY = obj.PrimData.PathShearY;
        prim.Shape.PathSkew = obj.PrimData.PathSkew;
        prim.Shape.PathTaperX = obj.PrimData.PathTaperX;
        prim.Shape.PathTaperY = obj.PrimData.PathTaperY;

        prim.Shape.PathTwist = obj.PrimData.PathTwist;
        prim.Shape.PathTwistBegin = obj.PrimData.PathTwistBegin;
        prim.Shape.ProfileBegin = obj.PrimData.ProfileBegin;
        prim.Shape.ProfileCurve = obj.PrimData.profileCurve;
        prim.Shape.ProfileEnd = obj.PrimData.ProfileEnd;
        prim.Shape.ProfileHollow = obj.PrimData.ProfileHollow;

        prim.SitName = obj.Properties.SitName;
        //prim.SitOffset;
        //prim.SitRotation;
        prim.SoundFlags = (int)obj.SoundFlags;
        prim.SoundGain = obj.SoundGain;
        prim.SoundID = obj.Sound;
        prim.SoundRadius = obj.SoundRadius;
        prim.State = obj.PrimData.State;
        prim.Temporary = (obj.Flags & PrimFlags.Temporary) == PrimFlags.Temporary;
        prim.Text = obj.Text;
        prim.TextColor = obj.TextColor;
        prim.Textures = obj.Textures;
        //prim.TouchName;
        prim.UsePhysics = (obj.Flags & PrimFlags.Physics) == PrimFlags.Physics;
        prim.Velocity = obj.Velocity;

        return prim;
    }

    public Primitive ToPrimitive()
    {
        var prim = new Primitive();
        prim.Properties = new Primitive.ObjectProperties();

        prim.Acceleration = Acceleration;
        prim.AngularVelocity = AngularVelocity;
        prim.ClickAction = (ClickAction)ClickAction;
        prim.Properties.CreationDate = CreationDate;
        prim.Properties.CreatorID = CreatorID;
        prim.Properties.Description = Description;
        if (DieAtEdge) prim.Flags |= PrimFlags.DieAtEdge;
        prim.Properties.FolderID = FolderID;
        prim.Properties.GroupID = GroupID;
        prim.ID = ID;
        prim.Properties.LastOwnerID = LastOwnerID;
        prim.LocalID = LocalID;
        prim.PrimData.Material = (Material)Material;
        prim.Properties.Name = Name;
        prim.OwnerID = OwnerID;
        prim.ParentID = ParentID;
        prim.PrimData.PCode = (PCode)PCode;
        prim.Properties.Permissions = new Permissions(PermsBase, PermsEveryone, PermsGroup, PermsNextOwner, PermsOwner);
        if (Phantom) prim.Flags |= PrimFlags.Phantom;
        prim.Position = Position;
        if (ReturnAtEdge) prim.Flags |= PrimFlags.ReturnAtEdge;
        prim.Rotation = Rotation;
        prim.Properties.SalePrice = SalePrice;
        prim.Properties.SaleType = (SaleType)SaleType;
        if (Sandbox) prim.Flags |= PrimFlags.Sandbox;
        prim.Scale = Scale;
        prim.SoundFlags = (SoundFlags)SoundFlags;
        prim.SoundGain = SoundGain;
        prim.Sound = SoundID;
        prim.SoundRadius = SoundRadius;
        prim.PrimData.State = (byte)State;
        if (Temporary) prim.Flags |= PrimFlags.Temporary;
        prim.Text = Text;
        prim.TextColor = TextColor;
        prim.Textures = Textures;
        if (UsePhysics) prim.Flags |= PrimFlags.Physics;
        prim.Velocity = Velocity;

        prim.PrimData.PathBegin = Shape.PathBegin;
        prim.PrimData.PathCurve = (PathCurve)Shape.PathCurve;
        prim.PrimData.PathEnd = Shape.PathEnd;
        prim.PrimData.PathRadiusOffset = Shape.PathRadiusOffset;
        prim.PrimData.PathRevolutions = Shape.PathRevolutions;
        prim.PrimData.PathScaleX = Shape.PathScaleX;
        prim.PrimData.PathScaleY = Shape.PathScaleY;
        prim.PrimData.PathShearX = Shape.PathShearX;
        prim.PrimData.PathShearY = Shape.PathShearY;
        prim.PrimData.PathSkew = Shape.PathSkew;
        prim.PrimData.PathTaperX = Shape.PathTaperX;
        prim.PrimData.PathTaperY = Shape.PathTaperY;
        prim.PrimData.PathTwist = Shape.PathTwist;
        prim.PrimData.PathTwistBegin = Shape.PathTwistBegin;
        prim.PrimData.ProfileBegin = Shape.ProfileBegin;
        prim.PrimData.profileCurve = (byte)Shape.ProfileCurve;
        prim.PrimData.ProfileEnd = Shape.ProfileEnd;
        prim.PrimData.ProfileHollow = Shape.ProfileHollow;

        if (Flexible != null)
        {
            prim.Flexible = new Primitive.FlexibleData();
            prim.Flexible.Drag = Flexible.Drag;
            prim.Flexible.Force = Flexible.Force;
            prim.Flexible.Gravity = Flexible.Gravity;
            prim.Flexible.Softness = Flexible.Softness;
            prim.Flexible.Tension = Flexible.Tension;
            prim.Flexible.Wind = Flexible.Wind;
        }

        if (Light != null)
        {
            prim.Light = new Primitive.LightData();
            prim.Light.Color = Light.Color;
            prim.Light.Cutoff = Light.Cutoff;
            prim.Light.Falloff = Light.Falloff;
            prim.Light.Intensity = Light.Intensity;
            prim.Light.Radius = Light.Radius;
        }

        if (Particles != null)
        {
            prim.ParticleSys = new Primitive.ParticleSystem();
            prim.ParticleSys.AngularVelocity = Particles.AngularVelocity;
            prim.ParticleSys.PartAcceleration = Particles.Acceleration;
            prim.ParticleSys.BurstPartCount = (byte)Particles.BurstParticleCount;
            prim.ParticleSys.BurstRate = Particles.BurstRadius;
            prim.ParticleSys.BurstRate = Particles.BurstRate;
            prim.ParticleSys.BurstSpeedMax = Particles.BurstSpeedMax;
            prim.ParticleSys.BurstSpeedMin = Particles.BurstSpeedMin;
            prim.ParticleSys.PartDataFlags = (Primitive.ParticleSystem.ParticleDataFlags)Particles.DataFlags;
            prim.ParticleSys.PartFlags = (uint)Particles.Flags;
            prim.ParticleSys.InnerAngle = Particles.InnerAngle;
            prim.ParticleSys.MaxAge = Particles.MaxAge;
            prim.ParticleSys.OuterAngle = Particles.OuterAngle;
            prim.ParticleSys.PartEndColor = Particles.ParticleEndColor;
            prim.ParticleSys.PartEndScaleX = Particles.ParticleEndScale.X;
            prim.ParticleSys.PartEndScaleY = Particles.ParticleEndScale.Y;
            prim.ParticleSys.MaxAge = Particles.ParticleMaxAge;
            prim.ParticleSys.PartStartColor = Particles.ParticleStartColor;
            prim.ParticleSys.PartStartScaleX = Particles.ParticleStartScale.X;
            prim.ParticleSys.PartStartScaleY = Particles.ParticleStartScale.Y;
            prim.ParticleSys.Pattern = (Primitive.ParticleSystem.SourcePattern)Particles.Pattern;
            prim.ParticleSys.StartAge = Particles.StartAge;
            prim.ParticleSys.Target = Particles.TargetID;
            prim.ParticleSys.Texture = Particles.TextureID;
        }

        if (Sculpt != null)
        {
            prim.Sculpt = new Primitive.SculptData();
            prim.Sculpt.SculptTexture = Sculpt.Texture;
            prim.Sculpt.Type = (SculptType)Sculpt.Type;
        }

        return prim;
    }

    public class FlexibleBlock
    {
        public float Drag;
        public Vector3 Force;
        public float Gravity;
        public int Softness;
        public float Tension;
        public float Wind;

        public OSDMap Serialize()
        {
            var map = new OSDMap();
            map["softness"] = OSD.FromInteger(Softness);
            map["gravity"] = OSD.FromReal(Gravity);
            map["drag"] = OSD.FromReal(Drag);
            map["wind"] = OSD.FromReal(Wind);
            map["tension"] = OSD.FromReal(Tension);
            map["force"] = OSD.FromVector3(Force);
            return map;
        }

        public void Deserialize(OSDMap map)
        {
            Softness = map["softness"].AsInteger();
            Gravity = (float)map["gravity"].AsReal();
            Drag = (float)map["drag"].AsReal();
            Wind = (float)map["wind"].AsReal();
            Tension = (float)map["tension"].AsReal();
            Force = map["force"].AsVector3();
        }
    }

    public class LightBlock
    {
        public Color4 Color;
        public float Cutoff;
        public float Falloff;
        public float Intensity;
        public float Radius;

        public OSDMap Serialize()
        {
            var map = new OSDMap();
            map["color"] = OSD.FromColor4(Color);
            map["intensity"] = OSD.FromReal(Intensity);
            map["radius"] = OSD.FromReal(Radius);
            map["falloff"] = OSD.FromReal(Falloff);
            map["cutoff"] = OSD.FromReal(Cutoff);
            return map;
        }

        public void Deserialize(OSDMap map)
        {
            Color = map["color"].AsColor4();
            Intensity = (float)map["intensity"].AsReal();
            Radius = (float)map["radius"].AsReal();
            Falloff = (float)map["falloff"].AsReal();
            Cutoff = (float)map["cutoff"].AsReal();
        }
    }

    public class SculptBlock
    {
        public UUID Texture;
        public int Type;

        public OSDMap Serialize()
        {
            var map = new OSDMap();
            map["texture"] = OSD.FromUUID(Texture);
            map["type"] = OSD.FromInteger(Type);
            return map;
        }

        public void Deserialize(OSDMap map)
        {
            Texture = map["texture"].AsUUID();
            Type = map["type"].AsInteger();
        }
    }

    public class ParticlesBlock
    {
        public Vector3 Acceleration;
        public Vector3 AngularVelocity;
        public int BurstParticleCount;
        public float BurstRadius;
        public float BurstRate;
        public float BurstSpeedMax;
        public float BurstSpeedMin;
        public int DataFlags;
        public int Flags;
        public float InnerAngle;
        public float MaxAge;
        public float OuterAngle;
        public Color4 ParticleEndColor;
        public Vector2 ParticleEndScale;
        public float ParticleMaxAge;
        public Color4 ParticleStartColor;
        public Vector2 ParticleStartScale;
        public int Pattern;
        public float StartAge;
        public UUID TargetID;
        public UUID TextureID;

        public OSDMap Serialize()
        {
            var map = new OSDMap();
            map["flags"] = OSD.FromInteger(Flags);
            map["pattern"] = OSD.FromInteger(Pattern);
            map["max_age"] = OSD.FromReal(MaxAge);
            map["start_age"] = OSD.FromReal(StartAge);
            map["inner_angle"] = OSD.FromReal(InnerAngle);
            map["outer_angle"] = OSD.FromReal(OuterAngle);
            map["burst_rate"] = OSD.FromReal(BurstRate);
            map["burst_radius"] = OSD.FromReal(BurstRadius);
            map["burst_speed_min"] = OSD.FromReal(BurstSpeedMin);
            map["burst_speed_max"] = OSD.FromReal(BurstSpeedMax);
            map["burst_particle_count"] = OSD.FromInteger(BurstParticleCount);
            map["angular_velocity"] = OSD.FromVector3(AngularVelocity);
            map["acceleration"] = OSD.FromVector3(Acceleration);
            map["texture_id"] = OSD.FromUUID(TextureID);
            map["target_id"] = OSD.FromUUID(TargetID);
            map["data_flags"] = OSD.FromInteger(DataFlags);
            map["particle_max_age"] = OSD.FromReal(ParticleMaxAge);
            map["particle_start_color"] = OSD.FromColor4(ParticleStartColor);
            map["particle_end_color"] = OSD.FromColor4(ParticleEndColor);
            map["particle_start_scale"] = OSD.FromVector2(ParticleStartScale);
            map["particle_end_scale"] = OSD.FromVector2(ParticleEndScale);
            return map;
        }

        public void Deserialize(OSDMap map)
        {
            Flags = map["flags"].AsInteger();
            Pattern = map["pattern"].AsInteger();
            MaxAge = (float)map["max_age"].AsReal();
            StartAge = (float)map["start_age"].AsReal();
            InnerAngle = (float)map["inner_angle"].AsReal();
            OuterAngle = (float)map["outer_angle"].AsReal();
            BurstRate = (float)map["burst_rate"].AsReal();
            BurstRadius = (float)map["burst_radius"].AsReal();
            BurstSpeedMin = (float)map["burst_speed_min"].AsReal();
            BurstSpeedMax = (float)map["burst_speed_max"].AsReal();
            BurstParticleCount = map["burst_particle_count"].AsInteger();
            AngularVelocity = map["angular_velocity"].AsVector3();
            Acceleration = map["acceleration"].AsVector3();
            TextureID = map["texture_id"].AsUUID();
            DataFlags = map["data_flags"].AsInteger();
            ParticleMaxAge = (float)map["particle_max_age"].AsReal();
            ParticleStartColor = map["particle_start_color"].AsColor4();
            ParticleEndColor = map["particle_end_color"].AsColor4();
            ParticleStartScale = map["particle_start_scale"].AsVector2();
            ParticleEndScale = map["particle_end_scale"].AsVector2();
        }
    }

    public class ShapeBlock
    {
        public float PathBegin;
        public int PathCurve;
        public float PathEnd;
        public float PathRadiusOffset;
        public float PathRevolutions;
        public float PathScaleX;
        public float PathScaleY;
        public float PathShearX;
        public float PathShearY;
        public float PathSkew;
        public float PathTaperX;
        public float PathTaperY;
        public float PathTwist;
        public float PathTwistBegin;
        public float ProfileBegin;
        public int ProfileCurve;
        public float ProfileEnd;
        public float ProfileHollow;

        public OSDMap Serialize()
        {
            var map = new OSDMap();
            map["path_curve"] = OSD.FromInteger(PathCurve);
            map["path_begin"] = OSD.FromReal(PathBegin);
            map["path_end"] = OSD.FromReal(PathEnd);
            map["path_scale_x"] = OSD.FromReal(PathScaleX);
            map["path_scale_y"] = OSD.FromReal(PathScaleY);
            map["path_shear_x"] = OSD.FromReal(PathShearX);
            map["path_shear_y"] = OSD.FromReal(PathShearY);
            map["path_twist"] = OSD.FromReal(PathTwist);
            map["path_twist_begin"] = OSD.FromReal(PathTwistBegin);
            map["path_radius_offset"] = OSD.FromReal(PathRadiusOffset);
            map["path_taper_x"] = OSD.FromReal(PathTaperX);
            map["path_taper_y"] = OSD.FromReal(PathTaperY);
            map["path_revolutions"] = OSD.FromReal(PathRevolutions);
            map["path_skew"] = OSD.FromReal(PathSkew);
            map["profile_curve"] = OSD.FromInteger(ProfileCurve);
            map["profile_begin"] = OSD.FromReal(ProfileBegin);
            map["profile_end"] = OSD.FromReal(ProfileEnd);
            map["profile_hollow"] = OSD.FromReal(ProfileHollow);
            return map;
        }

        public void Deserialize(OSDMap map)
        {
            PathCurve = map["path_curve"].AsInteger();
            PathBegin = (float)map["path_begin"].AsReal();
            PathEnd = (float)map["path_end"].AsReal();
            PathScaleX = (float)map["path_scale_x"].AsReal();
            PathScaleY = (float)map["path_scale_y"].AsReal();
            PathShearX = (float)map["path_shear_x"].AsReal();
            PathShearY = (float)map["path_shear_y"].AsReal();
            PathTwist = (float)map["path_twist"].AsReal();
            PathTwistBegin = (float)map["path_twist_begin"].AsReal();
            PathRadiusOffset = (float)map["path_radius_offset"].AsReal();
            PathTaperX = (float)map["path_taper_x"].AsReal();
            PathTaperY = (float)map["path_taper_y"].AsReal();
            PathRevolutions = (float)map["path_revolutions"].AsReal();
            PathSkew = (float)map["path_skew"].AsReal();
            ProfileCurve = map["profile_curve"].AsInteger();
            ProfileBegin = (float)map["profile_begin"].AsReal();
            ProfileEnd = (float)map["profile_end"].AsReal();
            ProfileHollow = (float)map["profile_hollow"].AsReal();
        }
    }

    public class InventoryBlock
    {
        public ItemBlock[] Items;

        public int Serial;

        public OSDMap Serialize()
        {
            var map = new OSDMap();
            map["serial"] = OSD.FromInteger(Serial);

            if (Items != null)
            {
                var array = new OSDArray(Items.Length);
                for (var i = 0; i < Items.Length; i++)
                    array.Add(Items[i].Serialize());
                map["items"] = array;
            }

            return map;
        }

        public void Deserialize(OSDMap map)
        {
            Serial = map["serial"].AsInteger();

            if (map.ContainsKey("items"))
            {
                var array = (OSDArray)map["items"];
                Items = new ItemBlock[array.Count];

                for (var i = 0; i < array.Count; i++)
                {
                    var item = new ItemBlock();
                    item.Deserialize((OSDMap)array[i]);
                    Items[i] = item;
                }
            }
            else
            {
                Items = new ItemBlock[0];
            }
        }

        public class ItemBlock
        {
            public UUID AssetID;
            public DateTime CreationDate;
            public UUID CreatorID;
            public string Description;
            public int Flags;
            public UUID GroupID;
            public UUID ID;
            public InventoryType InvType;
            public UUID LastOwnerID;
            public string Name;
            public UUID OwnerID;
            public uint PermsBase;
            public uint PermsEveryone;
            public UUID PermsGranterID;
            public uint PermsGroup;
            public uint PermsNextOwner;
            public uint PermsOwner;
            public AssetType Type;

            public OSDMap Serialize()
            {
                var map = new OSDMap();
                map["id"] = OSD.FromUUID(ID);
                map["name"] = OSD.FromString(Name);
                map["owner_id"] = OSD.FromUUID(OwnerID);
                map["creator_id"] = OSD.FromUUID(CreatorID);
                map["group_id"] = OSD.FromUUID(GroupID);
                map["last_owner_id"] = OSD.FromUUID(LastOwnerID);
                map["perms_granter_id"] = OSD.FromUUID(PermsGranterID);
                map["asset_id"] = OSD.FromUUID(AssetID);
                map["asset_type"] = OSD.FromInteger((int)Type);
                map["inv_type"] = OSD.FromInteger((int)InvType);
                map["description"] = OSD.FromString(Description);
                map["perms_base"] = OSD.FromInteger(PermsBase);
                map["perms_owner"] = OSD.FromInteger(PermsOwner);
                map["perms_group"] = OSD.FromInteger(PermsGroup);
                map["perms_everyone"] = OSD.FromInteger(PermsEveryone);
                map["perms_next_owner"] = OSD.FromInteger(PermsNextOwner);
                map["flags"] = OSD.FromInteger(Flags);
                map["creation_date"] = OSD.FromDate(CreationDate);
                return map;
            }

            public void Deserialize(OSDMap map)
            {
                ID = map["id"].AsUUID();
                Name = map["name"].AsString();
                OwnerID = map["owner_id"].AsUUID();
                CreatorID = map["creator_id"].AsUUID();
                GroupID = map["group_id"].AsUUID();
                LastOwnerID = map["last_owner_id"].AsUUID();
                PermsGranterID = map["perms_granter_id"].AsUUID();
                AssetID = map["asset_id"].AsUUID();
                Type = (AssetType)map["asset_type"].AsInteger();
                InvType = (InventoryType)map["inv_type"].AsInteger();
                Description = map["description"].AsString();
                PermsBase = (uint)map["perms_base"].AsInteger();
                PermsOwner = (uint)map["perms_owner"].AsInteger();
                PermsGroup = (uint)map["perms_group"].AsInteger();
                PermsEveryone = (uint)map["perms_everyone"].AsInteger();
                PermsNextOwner = (uint)map["perms_next_owner"].AsInteger();
                Flags = map["flags"].AsInteger();
                CreationDate = map["creation_date"].AsDate();
            }

            public static ItemBlock FromInventoryBase(InventoryItem item)
            {
                var block = new ItemBlock();
                block.AssetID = item.AssetUUID;
                block.CreationDate = item.CreationDate;
                block.CreatorID = item.CreatorID;
                block.Description = item.Description;
                block.Flags = (int)item.Flags;
                block.GroupID = item.GroupID;
                block.ID = item.UUID;
                block.InvType = item.InventoryType == InventoryType.Unknown && item.AssetType == AssetType.LSLText
                    ? InventoryType.LSL
                    : item.InventoryType;
                ;
                block.LastOwnerID = item.LastOwnerID;
                block.Name = item.Name;
                block.OwnerID = item.OwnerID;
                block.PermsBase = (uint)item.Permissions.BaseMask;
                block.PermsEveryone = (uint)item.Permissions.EveryoneMask;
                block.PermsGroup = (uint)item.Permissions.GroupMask;
                block.PermsNextOwner = (uint)item.Permissions.NextOwnerMask;
                block.PermsOwner = (uint)item.Permissions.OwnerMask;
                block.PermsGranterID = UUID.Zero;
                block.Type = item.AssetType;
                return block;
            }
        }
    }
}