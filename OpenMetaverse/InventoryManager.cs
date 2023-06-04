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
using System.Net;
using System.Runtime.Serialization;
using System.Threading;
using OpenMetaverse.Http;
using OpenMetaverse.Interfaces;
using OpenMetaverse.Messages.Linden;
using OpenMetaverse.Packets;
using OpenMetaverse.StructuredData;

namespace OpenMetaverse;

#region Enums

[Flags]
public enum InventorySortOrder
{
    /// <summary>Sort by name</summary>
    ByName = 0,

    /// <summary>Sort by date</summary>
    ByDate = 1,

    /// <summary>
    ///     Sort folders by name, regardless of whether items are
    ///     sorted by name or date
    /// </summary>
    FoldersByName = 2,

    /// <summary>Place system folders at the top</summary>
    SystemFoldersToTop = 4
}

/// <summary>
///     Possible destinations for DeRezObject request
/// </summary>
public enum DeRezDestination : byte
{
    /// <summary></summary>
    AgentInventorySave = 0,

    /// <summary>Copy from in-world to agent inventory</summary>
    AgentInventoryCopy = 1,

    /// <summary>Derez to TaskInventory</summary>
    TaskInventory = 2,

    /// <summary></summary>
    Attachment = 3,

    /// <summary>Take Object</summary>
    AgentInventoryTake = 4,

    /// <summary></summary>
    ForceToGodInventory = 5,

    /// <summary>Delete Object</summary>
    TrashFolder = 6,

    /// <summary>Put an avatar attachment into agent inventory</summary>
    AttachmentToInventory = 7,

    /// <summary></summary>
    AttachmentExists = 8,

    /// <summary>Return an object back to the owner's inventory</summary>
    ReturnToOwner = 9,

    /// <summary>Return a deeded object back to the last owner's inventory</summary>
    ReturnToLastOwner = 10
}

/// <summary>
///     Upper half of the Flags field for inventory items
/// </summary>
[Flags]
public enum InventoryItemFlags : uint
{
    None = 0,

    /// <summary>
    ///     Indicates that the NextOwner permission will be set to the
    ///     most restrictive set of permissions found in the object set
    ///     (including linkset items and object inventory items) on next rez
    /// </summary>
    ObjectSlamPerm = 0x100,

    /// <summary>
    ///     Indicates that the object sale information has been
    ///     changed
    /// </summary>
    ObjectSlamSale = 0x1000,

    /// <summary>If set, and a slam bit is set, indicates BaseMask will be overwritten on Rez</summary>
    ObjectOverwriteBase = 0x010000,

    /// <summary>If set, and a slam bit is set, indicates OwnerMask will be overwritten on Rez</summary>
    ObjectOverwriteOwner = 0x020000,

    /// <summary>If set, and a slam bit is set, indicates GroupMask will be overwritten on Rez</summary>
    ObjectOverwriteGroup = 0x040000,

    /// <summary>If set, and a slam bit is set, indicates EveryoneMask will be overwritten on Rez</summary>
    ObjectOverwriteEveryone = 0x080000,

    /// <summary>If set, and a slam bit is set, indicates NextOwnerMask will be overwritten on Rez</summary>
    ObjectOverwriteNextOwner = 0x100000,

    /// <summary>
    ///     Indicates whether this object is composed of multiple
    ///     items or not
    /// </summary>
    ObjectHasMultipleItems = 0x200000,

    /// <summary>
    ///     Indicates that the asset is only referenced by this
    ///     inventory item. If this item is deleted or updated to reference a
    ///     new assetID, the asset can be deleted
    /// </summary>
    SharedSingleReference = 0x40000000
}

#endregion Enums

#region Inventory Object Classes

/// <summary>
///     Base Class for Inventory Items
/// </summary>
[Serializable]
public abstract class InventoryBase : ISerializable
{
    /// <summary>Name of item/folder</summary>
    public string Name;

    /// <summary>Item/Folder Owners <seealso cref="OpenMetaverse.UUID" /></summary>
    public UUID OwnerID;

    /// <summary><seealso cref="OpenMetaverse.UUID" /> of parent folder</summary>
    public UUID ParentUUID;

    /// <summary><seealso cref="OpenMetaverse.UUID" /> of item/folder</summary>
    public UUID UUID;

    /// <summary>
    ///     Constructor, takes an itemID as a parameter
    /// </summary>
    /// <param name="itemID">The <seealso cref="OpenMetaverse.UUID" /> of the item</param>
    public InventoryBase(UUID itemID)
    {
        if (itemID.IsZero())
            Logger.Log("Initializing an InventoryBase with UUID.Zero", Helpers.LogLevel.Warning);
        UUID = itemID;
    }

    /// <summary>
    /// </summary>
    /// <returns></returns>
    public InventoryBase(SerializationInfo info, StreamingContext ctxt)
    {
        UUID = (UUID)info.GetValue("UUID", typeof(UUID));
        ParentUUID = (UUID)info.GetValue("ParentUUID", typeof(UUID));
        Name = (string)info.GetValue("Name", typeof(string));
        OwnerID = (UUID)info.GetValue("OwnerID", typeof(UUID));
    }

    /// <summary>
    /// </summary>
    /// <returns></returns>
    public virtual void GetObjectData(SerializationInfo info, StreamingContext ctxt)
    {
        info.AddValue("UUID", UUID);
        info.AddValue("ParentUUID", ParentUUID);
        info.AddValue("Name", Name);
        info.AddValue("OwnerID", OwnerID);
    }

    /// <summary>
    ///     Generates a number corresponding to the value of the object to support the use of a hash table,
    ///     suitable for use in hashing algorithms and data structures such as a hash table
    /// </summary>
    /// <returns>A Hashcode of all the combined InventoryBase fields</returns>
    public override int GetHashCode()
    {
        return UUID.GetHashCode() ^ ParentUUID.GetHashCode() ^ Name.GetHashCode() ^ OwnerID.GetHashCode();
    }

    /// <summary>
    ///     Determine whether the specified <seealso cref="OpenMetaverse.InventoryBase" /> object is equal to the current
    ///     object
    /// </summary>
    /// <param name="o">InventoryBase object to compare against</param>
    /// <returns>true if objects are the same</returns>
    public override bool Equals(object o)
    {
        var inv = o as InventoryBase;
        return inv != null && Equals(inv);
    }

    /// <summary>
    ///     Determine whether the specified <seealso cref="OpenMetaverse.InventoryBase" /> object is equal to the current
    ///     object
    /// </summary>
    /// <param name="o">InventoryBase object to compare against</param>
    /// <returns>true if objects are the same</returns>
    public virtual bool Equals(InventoryBase o)
    {
        return o.UUID == UUID
               && o.ParentUUID == ParentUUID
               && o.Name == Name
               && o.OwnerID == OwnerID;
    }

    /// <summary>
    ///     Convert inventory to OSD
    /// </summary>
    /// <returns>OSD representation</returns>
    public abstract OSD GetOSD();
}

/// <summary>
///     An Item in Inventory
/// </summary>
[Serializable]
public class InventoryItem : InventoryBase
{
    /// <summary>The type of item from <seealso cref="OpenMetaverse.AssetType" /></summary>
    public AssetType AssetType;

    /// <summary>The <seealso cref="OpenMetaverse.UUID" /> of this item</summary>
    public UUID AssetUUID;

    /// <summary>
    ///     Time and date this inventory item was created, stored as
    ///     UTC (Coordinated Universal Time)
    /// </summary>
    public DateTime CreationDate;

    /// <summary>The <seealso cref="OpenMetaverse.UUID" /> of the creator of this item</summary>
    public UUID CreatorID;

    /// <summary>A Description of this item</summary>
    public string Description;

    /// <summary>Combined flags from <seealso cref="OpenMetaverse.InventoryItemFlags" /></summary>
    public uint Flags;

    /// <summary>
    ///     The <seealso cref="OpenMetaverse.Group" />s <seealso cref="OpenMetaverse.UUID" /> this item is set to or owned
    ///     by
    /// </summary>
    public UUID GroupID;

    /// <summary>If true, item is owned by a group</summary>
    public bool GroupOwned;

    /// <summary>The type of item from the <seealso cref="OpenMetaverse.InventoryType" /> enum</summary>
    public InventoryType InventoryType;

    /// <summary>The <seealso cref="OpenMetaverse.UUID" /> of the previous owner of the item</summary>
    public UUID LastOwnerID;

    /// <summary>The combined <seealso cref="OpenMetaverse.Permissions" /> of this item</summary>
    public Permissions Permissions;

    /// <summary>The price this item can be purchased for</summary>
    public int SalePrice;

    /// <summary>The type of sale from the <seealso cref="OpenMetaverse.SaleType" /> enum</summary>
    public SaleType SaleType;

    /// <summary>Used to update the AssetID in requests sent to the server</summary>
    public UUID TransactionID;

    /// <summary>
    ///     Construct a new InventoryItem object
    /// </summary>
    /// <param name="itemID">The <seealso cref="OpenMetaverse.UUID" /> of the item</param>
    public InventoryItem(UUID itemID)
        : base(itemID)
    {
    }

    /// <summary>
    ///     Construct a new InventoryItem object of a specific Type
    /// </summary>
    /// <param name="type">The type of item from <seealso cref="OpenMetaverse.InventoryType" /></param>
    /// <param name="itemID"><seealso cref="OpenMetaverse.UUID" /> of the item</param>
    public InventoryItem(InventoryType type, UUID itemID) : base(itemID)
    {
        InventoryType = type;
    }

    /// <summary>
    /// </summary>
    /// <returns></returns>
    public InventoryItem(SerializationInfo info, StreamingContext ctxt)
        : base(info, ctxt)
    {
        AssetUUID = (UUID)info.GetValue("AssetUUID", typeof(UUID));
        Permissions = (Permissions)info.GetValue("Permissions", typeof(Permissions));
        AssetType = (AssetType)info.GetValue("AssetType", typeof(AssetType));
        InventoryType = (InventoryType)info.GetValue("InventoryType", typeof(InventoryType));
        CreatorID = (UUID)info.GetValue("CreatorID", typeof(UUID));
        Description = (string)info.GetValue("Description", typeof(string));
        GroupID = (UUID)info.GetValue("GroupID", typeof(UUID));
        GroupOwned = (bool)info.GetValue("GroupOwned", typeof(bool));
        SalePrice = (int)info.GetValue("SalePrice", typeof(int));
        SaleType = (SaleType)info.GetValue("SaleType", typeof(SaleType));
        Flags = (uint)info.GetValue("Flags", typeof(uint));
        CreationDate = (DateTime)info.GetValue("CreationDate", typeof(DateTime));
        LastOwnerID = (UUID)info.GetValue("LastOwnerID", typeof(UUID));
    }

    public override string ToString()
    {
        return AssetType + " " + AssetUUID + " (" + InventoryType + " " + UUID + ") '" + Name + "'/'" +
               Description + "' " + Permissions;
    }

    /// <summary>
    ///     Indicates inventory item is a link
    /// </summary>
    /// <returns>True if inventory item is a link to another inventory item</returns>
    public bool IsLink()
    {
        return AssetType == AssetType.Link || AssetType == AssetType.LinkFolder;
    }

    /// <summary>
    /// </summary>
    /// <returns></returns>
    public override void GetObjectData(SerializationInfo info, StreamingContext ctxt)
    {
        base.GetObjectData(info, ctxt);
        info.AddValue("AssetUUID", AssetUUID, typeof(UUID));
        info.AddValue("Permissions", Permissions, typeof(Permissions));
        info.AddValue("AssetType", AssetType);
        info.AddValue("InventoryType", InventoryType);
        info.AddValue("CreatorID", CreatorID);
        info.AddValue("Description", Description);
        info.AddValue("GroupID", GroupID);
        info.AddValue("GroupOwned", GroupOwned);
        info.AddValue("SalePrice", SalePrice);
        info.AddValue("SaleType", SaleType);
        info.AddValue("Flags", Flags);
        info.AddValue("CreationDate", CreationDate);
        info.AddValue("LastOwnerID", LastOwnerID);
    }

    /// <summary>
    ///     Generates a number corresponding to the value of the object to support the use of a hash table.
    ///     Suitable for use in hashing algorithms and data structures such as a hash table
    /// </summary>
    /// <returns>A Hashcode of all the combined InventoryItem fields</returns>
    public override int GetHashCode()
    {
        return AssetUUID.GetHashCode() ^ Permissions.GetHashCode() ^ AssetType.GetHashCode() ^
               InventoryType.GetHashCode() ^ Description.GetHashCode() ^ GroupID.GetHashCode() ^
               GroupOwned.GetHashCode() ^ SalePrice.GetHashCode() ^ SaleType.GetHashCode() ^
               Flags.GetHashCode() ^ CreationDate.GetHashCode() ^ LastOwnerID.GetHashCode();
    }

    /// <summary>
    ///     Compares an object
    /// </summary>
    /// <param name="o">The object to compare</param>
    /// <returns>true if comparison object matches</returns>
    public override bool Equals(object o)
    {
        var item = o as InventoryItem;
        return item != null && Equals(item);
    }

    /// <summary>
    ///     Determine whether the specified <seealso cref="OpenMetaverse.InventoryBase" /> object is equal to the current
    ///     object
    /// </summary>
    /// <param name="o">The <seealso cref="OpenMetaverse.InventoryBase" /> object to compare against</param>
    /// <returns>true if objects are the same</returns>
    public override bool Equals(InventoryBase o)
    {
        var item = o as InventoryItem;
        return item != null && Equals(item);
    }

    /// <summary>
    ///     Determine whether the specified <seealso cref="OpenMetaverse.InventoryItem" /> object is equal to the current
    ///     object
    /// </summary>
    /// <param name="o">The <seealso cref="OpenMetaverse.InventoryItem" /> object to compare against</param>
    /// <returns>true if objects are the same</returns>
    public bool Equals(InventoryItem o)
    {
        return base.Equals(o)
               && o.AssetType == AssetType
               && o.AssetUUID == AssetUUID
               && o.CreationDate == CreationDate
               && o.Description == Description
               && o.Flags == Flags
               && o.GroupID == GroupID
               && o.GroupOwned == GroupOwned
               && o.InventoryType == InventoryType
               && o.Permissions.Equals(Permissions)
               && o.SalePrice == SalePrice
               && o.SaleType == SaleType
               && o.LastOwnerID == LastOwnerID;
    }

    /// <summary>
    ///     Create InventoryItem from OSD
    /// </summary>
    /// <param name="data">OSD Data that makes up InventoryItem</param>
    /// <returns>Inventory item created</returns>
    public static InventoryItem FromOSD(OSD data)
    {
        var descItem = (OSDMap)data;

        var type = (InventoryType)descItem["inv_type"].AsInteger();
        if (type == InventoryType.Texture && (AssetType)descItem["type"].AsInteger() == AssetType.Object)
            type = InventoryType.Attachment;
        var item = InventoryManager.CreateInventoryItem(type, descItem["item_id"]);

        item.ParentUUID = descItem["parent_id"];
        item.Name = descItem["name"];
        item.Description = descItem["desc"];
        item.OwnerID = descItem["agent_id"];
        item.ParentUUID = descItem["parent_id"];
        item.AssetUUID = descItem["asset_id"];
        item.AssetType = (AssetType)descItem["type"].AsInteger();
        item.CreationDate = Utils.UnixTimeToDateTime(descItem["created_at"]);
        item.Flags = descItem["flags"];

        var perms = (OSDMap)descItem["permissions"];
        item.CreatorID = perms["creator_id"];
        item.LastOwnerID = perms["last_owner_id"];
        item.Permissions = new Permissions(perms["base_mask"], perms["everyone_mask"], perms["group_mask"],
            perms["next_owner_mask"], perms["owner_mask"]);
        item.GroupOwned = perms["is_owner_group"];
        item.GroupID = perms["group_id"];

        var sale = (OSDMap)descItem["sale_info"];
        item.SalePrice = sale["sale_price"];
        item.SaleType = (SaleType)sale["sale_type"].AsInteger();

        return item;
    }

    /// <summary>
    ///     Convert InventoryItem to OSD
    /// </summary>
    /// <returns>OSD representation of InventoryItem</returns>
    public override OSD GetOSD()
    {
        var map = new OSDMap();
        map["inv_type"] = (int)InventoryType;
        map["parent_id"] = ParentUUID;
        map["name"] = Name;
        map["desc"] = Description;
        map["agent_id"] = OwnerID;
        map["parent_id"] = ParentUUID;
        map["asset_id"] = AssetUUID;
        map["type"] = (int)AssetType;
        map["created_at"] = CreationDate;
        map["flags"] = Flags;

        var perms = (OSDMap)Permissions.GetOSD();
        perms["creator_id"] = CreatorID;
        perms["last_owner_id"] = LastOwnerID;
        perms["is_owner_group"] = GroupOwned;
        perms["group_id"] = GroupID;
        map["permissions"] = perms;

        var sale = new OSDMap();
        sale["sale_price"] = SalePrice;
        sale["sale_type"] = (int)SaleType;
        map["sale_info"] = sale;

        return map;
    }
}

/// <summary>
///     InventoryTexture Class representing a graphical image
/// </summary>
/// <seealso cref="ManagedImage" />
[Serializable]
public class InventoryTexture : InventoryItem
{
    /// <summary>
    ///     Construct an InventoryTexture object
    /// </summary>
    /// <param name="itemID">
    ///     A <seealso cref="OpenMetaverse.UUID" /> which becomes the
    ///     <seealso cref="OpenMetaverse.InventoryItem" /> objects AssetUUID
    /// </param>
    public InventoryTexture(UUID itemID)
        : base(itemID)
    {
        InventoryType = InventoryType.Texture;
    }

    /// <summary>
    ///     Construct an InventoryTexture object from a serialization stream
    /// </summary>
    public InventoryTexture(SerializationInfo info, StreamingContext ctxt)
        : base(info, ctxt)
    {
        InventoryType = InventoryType.Texture;
    }
}

/// <summary>
///     InventorySound Class representing a playable sound
/// </summary>
[Serializable]
public class InventorySound : InventoryItem
{
    /// <summary>
    ///     Construct an InventorySound object
    /// </summary>
    /// <param name="itemID">
    ///     A <seealso cref="OpenMetaverse.UUID" /> which becomes the
    ///     <seealso cref="OpenMetaverse.InventoryItem" /> objects AssetUUID
    /// </param>
    public InventorySound(UUID itemID)
        : base(itemID)
    {
        InventoryType = InventoryType.Sound;
    }

    /// <summary>
    ///     Construct an InventorySound object from a serialization stream
    /// </summary>
    public InventorySound(SerializationInfo info, StreamingContext ctxt)
        : base(info, ctxt)
    {
        InventoryType = InventoryType.Sound;
    }
}

/// <summary>
///     InventoryCallingCard Class, contains information on another avatar
/// </summary>
[Serializable]
public class InventoryCallingCard : InventoryItem
{
    /// <summary>
    ///     Construct an InventoryCallingCard object
    /// </summary>
    /// <param name="itemID">
    ///     A <seealso cref="OpenMetaverse.UUID" /> which becomes the
    ///     <seealso cref="OpenMetaverse.InventoryItem" /> objects AssetUUID
    /// </param>
    public InventoryCallingCard(UUID itemID)
        : base(itemID)
    {
        InventoryType = InventoryType.CallingCard;
    }

    /// <summary>
    ///     Construct an InventoryCallingCard object from a serialization stream
    /// </summary>
    public InventoryCallingCard(SerializationInfo info, StreamingContext ctxt)
        : base(info, ctxt)
    {
        InventoryType = InventoryType.CallingCard;
    }
}

/// <summary>
///     InventoryLandmark Class, contains details on a specific location
/// </summary>
[Serializable]
public class InventoryLandmark : InventoryItem
{
    /// <summary>
    ///     Construct an InventoryLandmark object
    /// </summary>
    /// <param name="itemID">
    ///     A <seealso cref="OpenMetaverse.UUID" /> which becomes the
    ///     <seealso cref="OpenMetaverse.InventoryItem" /> objects AssetUUID
    /// </param>
    public InventoryLandmark(UUID itemID)
        : base(itemID)
    {
        InventoryType = InventoryType.Landmark;
    }

    /// <summary>
    ///     Construct an InventoryLandmark object from a serialization stream
    /// </summary>
    public InventoryLandmark(SerializationInfo info, StreamingContext ctxt)
        : base(info, ctxt)
    {
        InventoryType = InventoryType.Landmark;
    }

    /// <summary>
    ///     Landmarks use the InventoryItemFlags struct and will have a flag of 1 set if they have been visited
    /// </summary>
    public bool LandmarkVisited
    {
        get => (Flags & 1) != 0;
        set
        {
            if (value) Flags |= 1;
            else Flags &= ~1u;
        }
    }
}

/// <summary>
///     InventoryObject Class contains details on a primitive or coalesced set of primitives
/// </summary>
[Serializable]
public class InventoryObject : InventoryItem
{
    /// <summary>
    ///     Construct an InventoryObject object
    /// </summary>
    /// <param name="itemID">
    ///     A <seealso cref="OpenMetaverse.UUID" /> which becomes the
    ///     <seealso cref="OpenMetaverse.InventoryItem" /> objects AssetUUID
    /// </param>
    public InventoryObject(UUID itemID)
        : base(itemID)
    {
        InventoryType = InventoryType.Object;
    }

    /// <summary>
    ///     Construct an InventoryObject object from a serialization stream
    /// </summary>
    public InventoryObject(SerializationInfo info, StreamingContext ctxt)
        : base(info, ctxt)
    {
        InventoryType = InventoryType.Object;
    }

    /// <summary>
    ///     Gets or sets the upper byte of the Flags value
    /// </summary>
    public InventoryItemFlags ItemFlags
    {
        get => (InventoryItemFlags)(Flags & ~0xFF);
        set => Flags = (uint)value | (Flags & 0xFF);
    }

    /// <summary>
    ///     Gets or sets the object attachment point, the lower byte of the Flags value
    /// </summary>
    public AttachmentPoint AttachPoint
    {
        get => (AttachmentPoint)(Flags & 0xFF);
        set => Flags = (uint)value | (Flags & 0xFFFFFF00);
    }
}

/// <summary>
///     InventoryNotecard Class, contains details on an encoded text document
/// </summary>
[Serializable]
public class InventoryNotecard : InventoryItem
{
    /// <summary>
    ///     Construct an InventoryNotecard object
    /// </summary>
    /// <param name="itemID">
    ///     A <seealso cref="OpenMetaverse.UUID" /> which becomes the
    ///     <seealso cref="OpenMetaverse.InventoryItem" /> objects AssetUUID
    /// </param>
    public InventoryNotecard(UUID itemID)
        : base(itemID)
    {
        InventoryType = InventoryType.Notecard;
    }

    /// <summary>
    ///     Construct an InventoryNotecard object from a serialization stream
    /// </summary>
    public InventoryNotecard(SerializationInfo info, StreamingContext ctxt)
        : base(info, ctxt)
    {
        InventoryType = InventoryType.Notecard;
    }
}

/// <summary>
///     InventoryCategory Class
/// </summary>
/// <remarks>TODO: Is this even used for anything?</remarks>
[Serializable]
public class InventoryCategory : InventoryItem
{
    /// <summary>
    ///     Construct an InventoryCategory object
    /// </summary>
    /// <param name="itemID">
    ///     A <seealso cref="OpenMetaverse.UUID" /> which becomes the
    ///     <seealso cref="OpenMetaverse.InventoryItem" /> objects AssetUUID
    /// </param>
    public InventoryCategory(UUID itemID)
        : base(itemID)
    {
        InventoryType = InventoryType.Category;
    }

    /// <summary>
    ///     Construct an InventoryCategory object from a serialization stream
    /// </summary>
    public InventoryCategory(SerializationInfo info, StreamingContext ctxt)
        : base(info, ctxt)
    {
        InventoryType = InventoryType.Category;
    }
}

/// <summary>
///     InventoryLSL Class, represents a Linden Scripting Language object
/// </summary>
[Serializable]
public class InventoryLSL : InventoryItem
{
    /// <summary>
    ///     Construct an InventoryLSL object
    /// </summary>
    /// <param name="itemID">
    ///     A <seealso cref="OpenMetaverse.UUID" /> which becomes the
    ///     <seealso cref="OpenMetaverse.InventoryItem" /> objects AssetUUID
    /// </param>
    public InventoryLSL(UUID itemID)
        : base(itemID)
    {
        InventoryType = InventoryType.LSL;
    }

    /// <summary>
    ///     Construct an InventoryLSL object from a serialization stream
    /// </summary>
    public InventoryLSL(SerializationInfo info, StreamingContext ctxt)
        : base(info, ctxt)
    {
        InventoryType = InventoryType.LSL;
    }
}

/// <summary>
///     InventorySnapshot Class, an image taken with the viewer
/// </summary>
[Serializable]
public class InventorySnapshot : InventoryItem
{
    /// <summary>
    ///     Construct an InventorySnapshot object
    /// </summary>
    /// <param name="itemID">
    ///     A <seealso cref="OpenMetaverse.UUID" /> which becomes the
    ///     <seealso cref="OpenMetaverse.InventoryItem" /> objects AssetUUID
    /// </param>
    public InventorySnapshot(UUID itemID)
        : base(itemID)
    {
        InventoryType = InventoryType.Snapshot;
    }

    /// <summary>
    ///     Construct an InventorySnapshot object from a serialization stream
    /// </summary>
    public InventorySnapshot(SerializationInfo info, StreamingContext ctxt)
        : base(info, ctxt)
    {
        InventoryType = InventoryType.Snapshot;
    }
}

/// <summary>
///     InventoryAttachment Class, contains details on an attachable object
/// </summary>
[Serializable]
public class InventoryAttachment : InventoryItem
{
    /// <summary>
    ///     Construct an InventoryAttachment object
    /// </summary>
    /// <param name="itemID">
    ///     A <seealso cref="OpenMetaverse.UUID" /> which becomes the
    ///     <seealso cref="OpenMetaverse.InventoryItem" /> objects AssetUUID
    /// </param>
    public InventoryAttachment(UUID itemID)
        : base(itemID)
    {
        InventoryType = InventoryType.Attachment;
    }

    /// <summary>
    ///     Construct an InventoryAttachment object from a serialization stream
    /// </summary>
    public InventoryAttachment(SerializationInfo info, StreamingContext ctxt)
        : base(info, ctxt)
    {
        InventoryType = InventoryType.Attachment;
    }

    /// <summary>
    ///     Get the last AttachmentPoint this object was attached to
    /// </summary>
    public AttachmentPoint AttachmentPoint
    {
        get => (AttachmentPoint)Flags;
        set => Flags = (uint)value;
    }
}

/// <summary>
///     InventoryWearable Class, details on a clothing item or body part
/// </summary>
[Serializable]
public class InventoryWearable : InventoryItem
{
    /// <summary>
    ///     Construct an InventoryWearable object
    /// </summary>
    /// <param name="itemID">
    ///     A <seealso cref="OpenMetaverse.UUID" /> which becomes the
    ///     <seealso cref="OpenMetaverse.InventoryItem" /> objects AssetUUID
    /// </param>
    public InventoryWearable(UUID itemID) : base(itemID)
    {
        InventoryType = InventoryType.Wearable;
    }

    /// <summary>
    ///     Construct an InventoryWearable object from a serialization stream
    /// </summary>
    public InventoryWearable(SerializationInfo info, StreamingContext ctxt)
        : base(info, ctxt)
    {
        InventoryType = InventoryType.Wearable;
    }

    /// <summary>
    ///     The <seealso cref="OpenMetaverse.WearableType" />, Skin, Shape, Skirt, Etc
    /// </summary>
    public WearableType WearableType
    {
        get => (WearableType)Flags;
        set => Flags = (uint)value;
    }
}

/// <summary>
///     InventoryAnimation Class, A bvh encoded object which animates an avatar
/// </summary>
[Serializable]
public class InventoryAnimation : InventoryItem
{
    /// <summary>
    ///     Construct an InventoryAnimation object
    /// </summary>
    /// <param name="itemID">
    ///     A <seealso cref="OpenMetaverse.UUID" /> which becomes the
    ///     <seealso cref="OpenMetaverse.InventoryItem" /> objects AssetUUID
    /// </param>
    public InventoryAnimation(UUID itemID)
        : base(itemID)
    {
        InventoryType = InventoryType.Animation;
    }

    /// <summary>
    ///     Construct an InventoryAnimation object from a serialization stream
    /// </summary>
    public InventoryAnimation(SerializationInfo info, StreamingContext ctxt)
        : base(info, ctxt)
    {
        InventoryType = InventoryType.Animation;
    }
}

/// <summary>
///     InventoryGesture Class, details on a series of animations, sounds, and actions
/// </summary>
[Serializable]
public class InventoryGesture : InventoryItem
{
    /// <summary>
    ///     Construct an InventoryGesture object
    /// </summary>
    /// <param name="itemID">
    ///     A <seealso cref="OpenMetaverse.UUID" /> which becomes the
    ///     <seealso cref="OpenMetaverse.InventoryItem" /> objects AssetUUID
    /// </param>
    public InventoryGesture(UUID itemID)
        : base(itemID)
    {
        InventoryType = InventoryType.Gesture;
    }

    /// <summary>
    ///     Construct an InventoryGesture object from a serialization stream
    /// </summary>
    public InventoryGesture(SerializationInfo info, StreamingContext ctxt)
        : base(info, ctxt)
    {
        InventoryType = InventoryType.Gesture;
    }
}

/// <summary>
///     A folder contains <seealso cref="T:OpenMetaverse.InventoryItem" />s and has certain attributes specific
///     to itself
/// </summary>
[Serializable]
public class InventoryFolder : InventoryBase
{
    /// <summary>Number of child items this folder contains.</summary>
    public int DescendentCount;

    /// <summary>The Preferred <seealso cref="T:OpenMetaverse.FolderType" /> for a folder.</summary>
    public FolderType PreferredType;

    /// <summary>The Version of this folder</summary>
    public int Version;

    /// <summary>
    ///     Constructor
    /// </summary>
    /// <param name="itemID">UUID of the folder</param>
    public InventoryFolder(UUID itemID)
        : base(itemID)
    {
        PreferredType = FolderType.None;
        Version = 1;
        DescendentCount = 0;
    }

    /// <summary>
    ///     Construct an InventoryFolder object from a serialization stream
    /// </summary>
    public InventoryFolder(SerializationInfo info, StreamingContext ctxt)
        : base(info, ctxt)
    {
        PreferredType = (FolderType)info.GetValue("PreferredType", typeof(FolderType));
        Version = (int)info.GetValue("Version", typeof(int));
        DescendentCount = (int)info.GetValue("DescendentCount", typeof(int));
    }

    /// <summary>
    /// </summary>
    /// <returns></returns>
    public override string ToString()
    {
        return Name;
    }

    /// <summary>
    ///     Get Serilization data for this InventoryFolder object
    /// </summary>
    public override void GetObjectData(SerializationInfo info, StreamingContext ctxt)
    {
        base.GetObjectData(info, ctxt);
        info.AddValue("PreferredType", PreferredType, typeof(FolderType));
        info.AddValue("Version", Version);
        info.AddValue("DescendentCount", DescendentCount);
    }

    /// <summary>
    /// </summary>
    /// <returns></returns>
    public override int GetHashCode()
    {
        return PreferredType.GetHashCode() ^ Version.GetHashCode() ^ DescendentCount.GetHashCode();
    }

    /// <summary>
    /// </summary>
    /// <param name="o"></param>
    /// <returns></returns>
    public override bool Equals(object o)
    {
        var folder = o as InventoryFolder;
        return folder != null && Equals(folder);
    }

    /// <summary>
    /// </summary>
    /// <param name="o"></param>
    /// <returns></returns>
    public override bool Equals(InventoryBase o)
    {
        var folder = o as InventoryFolder;
        return folder != null && Equals(folder);
    }

    /// <summary>
    /// </summary>
    /// <param name="o"></param>
    /// <returns></returns>
    public bool Equals(InventoryFolder o)
    {
        return base.Equals(o)
               && o.DescendentCount == DescendentCount
               && o.PreferredType == PreferredType
               && o.Version == Version;
    }

    /// <summary>
    ///     Create InventoryFolder from OSD
    /// </summary>
    /// <param name="data">OSD Data that makes up InventoryFolder</param>
    /// <returns>Inventory folder created</returns>
    public static InventoryFolder FromOSD(OSD data)
    {
        var res = (OSDMap)data;
        UUID folderID = res.ContainsKey("category_id") ? res["category_id"] : res["folder_id"];
        var folder = new InventoryFolder(folderID);
        folder.Name = res["name"];
        folder.DescendentCount = res["descendents"];
        folder.Version = res["version"];
        folder.OwnerID = res.ContainsKey("agent_id") ? res["agent_id"] : res["owner_id"];
        folder.ParentUUID = res["parent_id"];
        folder.PreferredType = (FolderType)(int)res["type_default"];
        return folder;
    }

    /// <summary>
    ///     Convert InventoryItem to OSD
    /// </summary>
    /// <returns>OSD representation of InventoryItem</returns>
    public override OSD GetOSD()
    {
        var res = new OSDMap(4);
        res["name"] = Name;
        res["type_default"] = (int)PreferredType;
        res["folder_id"] = UUID;
        res["descendents"] = DescendentCount;
        res["version"] = Version;
        res["owner_id"] = OwnerID;
        res["parent_id"] = ParentUUID;
        return res;
    }
}

#endregion Inventory Object Classes

/// <summary>
///     Tools for dealing with agents inventory
/// </summary>
[Serializable]
public class InventoryManager
{
    /// <summary>Used for converting shadow_id to asset_id</summary>
    public static readonly UUID MAGIC_ID = new("3c115e51-04f4-523c-9fa6-98aff1034730");

    #region String Arrays

    /// <summary>Partial mapping of FolderTypes to folder names</summary>
    private static readonly string[] _NewFolderNames =
    {
        "Textures", //  0
        "Sounds", //  1
        "Calling Cards", //  2
        "Landmarks", //  3
        string.Empty, //  4
        "Clothing", //  5
        "Objects", //  6
        "Notecards", //  7
        "My Inventory", //  8
        string.Empty, //  9
        "Scripts", // 10
        string.Empty, // 11
        string.Empty, // 12
        "Body Parts", // 13
        "Trash", // 14
        "Photo Album", // 15
        "Lost And Found", // 16
        string.Empty, // 17
        string.Empty, // 18
        string.Empty, // 19
        "Animations", // 20
        "Gestures", // 21
        string.Empty, // 22
        "Favorites", // 23
        string.Empty, // 24
        string.Empty, // 25
        "New Folder", // 26
        "New Folder", // 27
        "New Folder", // 28
        "New Folder", // 29
        "New Folder", // 30
        "New Folder", // 31
        "New Folder", // 32
        "New Folder", // 33
        "New Folder", // 34
        "New Folder", // 35
        "New Folder", // 36
        "New Folder", // 37
        "New Folder", // 38
        "New Folder", // 39
        "New Folder", // 40
        "New Folder", // 41
        "New Folder", // 42
        "New Folder", // 43
        "New Folder", // 44
        "New Folder", // 45
        "Current Outfit", // 46
        "New Outfit", // 47
        "My Outfits", // 48
        "Meshes", // 49
        "Received Items", // 50
        "Merchant Outbox", // 51
        "Basic Root", // 52
        "Marketplace Listings", // 53
        "New Stock" // 54
    };

    #endregion String Arrays

    private uint _CallbackPos;

    //private Random _RandNumbers = new Random();
    private object _CallbacksLock = new();
    private Dictionary<uint, ItemCopiedCallback> _ItemCopiedCallbacks = new();
    private Dictionary<uint, ItemCreatedCallback> _ItemCreatedCallbacks = new();
    private Dictionary<uint, InventoryType> _ItemInventoryTypeRequest = new();
    private List<InventorySearch> _Searches = new();

    private GridClient Client;

    /// <summary>
    ///     Default constructor
    /// </summary>
    /// <param name="client">Reference to the GridClient object</param>
    public InventoryManager(GridClient client)
    {
        Client = client;

        Client.Network.RegisterCallback(PacketType.UpdateCreateInventoryItem, UpdateCreateInventoryItemHandler);
        Client.Network.RegisterCallback(PacketType.SaveAssetIntoInventory, SaveAssetIntoInventoryHandler);
        Client.Network.RegisterCallback(PacketType.BulkUpdateInventory, BulkUpdateInventoryHandler);
        Client.Network.RegisterEventCallback("BulkUpdateInventory", BulkUpdateInventoryCapHandler);
        Client.Network.RegisterCallback(PacketType.MoveInventoryItem, MoveInventoryItemHandler);
        Client.Network.RegisterCallback(PacketType.InventoryDescendents, InventoryDescendentsHandler);
        Client.Network.RegisterCallback(PacketType.FetchInventoryReply, FetchInventoryReplyHandler);
        Client.Network.RegisterCallback(PacketType.ReplyTaskInventory, ReplyTaskInventoryHandler);
        Client.Network.RegisterEventCallback("ScriptRunningReply", ScriptRunningReplyMessageHandler);

        // Watch for inventory given to us through instant message            
        Client.Self.IM += Self_IM;

        // Register extra parameters with login and parse the inventory data that comes back
        Client.Network.RegisterLoginResponseCallback(
            Network_OnLoginResponse,
            new[]
            {
                "inventory-root", "inventory-skeleton", "inventory-lib-root",
                "inventory-lib-owner", "inventory-skel-lib"
            });
    }

    #region Properties

    /// <summary>
    ///     Get this agents Inventory data
    /// </summary>
    public Inventory Store { get; private set; }

    #endregion Properties

    protected struct InventorySearch
    {
        public UUID Folder;
        public UUID Owner;
        public string[] Path;
        public int Level;
    }

    #region Delegates

    /// <summary>
    ///     Callback for inventory item creation finishing
    /// </summary>
    /// <param name="success">
    ///     Whether the request to create an inventory
    ///     item succeeded or not
    /// </param>
    /// <param name="item">
    ///     Inventory item being created. If success is
    ///     false this will be null
    /// </param>
    public delegate void ItemCreatedCallback(bool success, InventoryItem item);

    /// <summary>
    ///     Callback for an inventory item being create from an uploaded asset
    /// </summary>
    /// <param name="success">true if inventory item creation was successful</param>
    /// <param name="status"></param>
    /// <param name="itemID"></param>
    /// <param name="assetID"></param>
    public delegate void ItemCreatedFromAssetCallback(bool success, string status, UUID itemID, UUID assetID);

    /// <summary>
    /// </summary>
    /// <param name="item"></param>
    public delegate void ItemCopiedCallback(InventoryBase item);

    /// <summary>The event subscribers, null of no subscribers</summary>
    private EventHandler<ItemReceivedEventArgs> m_ItemReceived;

    /// <summary>Raises the ItemReceived Event</summary>
    /// <param name="e">
    ///     A ItemReceivedEventArgs object containing
    ///     the data sent from the simulator
    /// </param>
    protected virtual void OnItemReceived(ItemReceivedEventArgs e)
    {
        var handler = m_ItemReceived;
        if (handler != null)
            handler(this, e);
    }

    /// <summary>Thread sync lock object</summary>
    private readonly object m_ItemReceivedLock = new();

    /// <summary>
    ///     Raised when the simulator sends us data containing
    ///     ...
    /// </summary>
    public event EventHandler<ItemReceivedEventArgs> ItemReceived
    {
        add
        {
            lock (m_ItemReceivedLock)
            {
                m_ItemReceived += value;
            }
        }
        remove
        {
            lock (m_ItemReceivedLock)
            {
                m_ItemReceived -= value;
            }
        }
    }


    /// <summary>The event subscribers, null of no subscribers</summary>
    private EventHandler<FolderUpdatedEventArgs> m_FolderUpdated;

    /// <summary>Raises the FolderUpdated Event</summary>
    /// <param name="e">
    ///     A FolderUpdatedEventArgs object containing
    ///     the data sent from the simulator
    /// </param>
    protected virtual void OnFolderUpdated(FolderUpdatedEventArgs e)
    {
        var handler = m_FolderUpdated;
        if (handler != null)
            handler(this, e);
    }

    /// <summary>Thread sync lock object</summary>
    private readonly object m_FolderUpdatedLock = new();

    /// <summary>
    ///     Raised when the simulator sends us data containing
    ///     ...
    /// </summary>
    public event EventHandler<FolderUpdatedEventArgs> FolderUpdated
    {
        add
        {
            lock (m_FolderUpdatedLock)
            {
                m_FolderUpdated += value;
            }
        }
        remove
        {
            lock (m_FolderUpdatedLock)
            {
                m_FolderUpdated -= value;
            }
        }
    }


    /// <summary>The event subscribers, null of no subscribers</summary>
    private EventHandler<InventoryObjectOfferedEventArgs> m_InventoryObjectOffered;

    /// <summary>Raises the InventoryObjectOffered Event</summary>
    /// <param name="e">
    ///     A InventoryObjectOfferedEventArgs object containing
    ///     the data sent from the simulator
    /// </param>
    protected virtual void OnInventoryObjectOffered(InventoryObjectOfferedEventArgs e)
    {
        var handler = m_InventoryObjectOffered;
        if (handler != null)
            handler(this, e);
    }

    /// <summary>Thread sync lock object</summary>
    private readonly object m_InventoryObjectOfferedLock = new();

    /// <summary>
    ///     Raised when the simulator sends us data containing
    ///     an inventory object sent by another avatar or primitive
    /// </summary>
    public event EventHandler<InventoryObjectOfferedEventArgs> InventoryObjectOffered
    {
        add
        {
            lock (m_InventoryObjectOfferedLock)
            {
                m_InventoryObjectOffered += value;
            }
        }
        remove
        {
            lock (m_InventoryObjectOfferedLock)
            {
                m_InventoryObjectOffered -= value;
            }
        }
    }

    /// <summary>The event subscribers, null of no subscribers</summary>
    private EventHandler<TaskItemReceivedEventArgs> m_TaskItemReceived;

    /// <summary>Raises the TaskItemReceived Event</summary>
    /// <param name="e">
    ///     A TaskItemReceivedEventArgs object containing
    ///     the data sent from the simulator
    /// </param>
    protected virtual void OnTaskItemReceived(TaskItemReceivedEventArgs e)
    {
        var handler = m_TaskItemReceived;
        if (handler != null)
            handler(this, e);
    }

    /// <summary>Thread sync lock object</summary>
    private readonly object m_TaskItemReceivedLock = new();

    /// <summary>
    ///     Raised when the simulator sends us data containing
    ///     ...
    /// </summary>
    public event EventHandler<TaskItemReceivedEventArgs> TaskItemReceived
    {
        add
        {
            lock (m_TaskItemReceivedLock)
            {
                m_TaskItemReceived += value;
            }
        }
        remove
        {
            lock (m_TaskItemReceivedLock)
            {
                m_TaskItemReceived -= value;
            }
        }
    }


    /// <summary>The event subscribers, null of no subscribers</summary>
    private EventHandler<FindObjectByPathReplyEventArgs> m_FindObjectByPathReply;

    /// <summary>Raises the FindObjectByPath Event</summary>
    /// <param name="e">
    ///     A FindObjectByPathEventArgs object containing
    ///     the data sent from the simulator
    /// </param>
    protected virtual void OnFindObjectByPathReply(FindObjectByPathReplyEventArgs e)
    {
        var handler = m_FindObjectByPathReply;
        if (handler != null)
            handler(this, e);
    }

    /// <summary>Thread sync lock object</summary>
    private readonly object m_FindObjectByPathReplyLock = new();

    /// <summary>
    ///     Raised when the simulator sends us data containing
    ///     ...
    /// </summary>
    public event EventHandler<FindObjectByPathReplyEventArgs> FindObjectByPathReply
    {
        add
        {
            lock (m_FindObjectByPathReplyLock)
            {
                m_FindObjectByPathReply += value;
            }
        }
        remove
        {
            lock (m_FindObjectByPathReplyLock)
            {
                m_FindObjectByPathReply -= value;
            }
        }
    }


    /// <summary>The event subscribers, null of no subscribers</summary>
    private EventHandler<TaskInventoryReplyEventArgs> m_TaskInventoryReply;

    /// <summary>Raises the TaskInventoryReply Event</summary>
    /// <param name="e">
    ///     A TaskInventoryReplyEventArgs object containing
    ///     the data sent from the simulator
    /// </param>
    protected virtual void OnTaskInventoryReply(TaskInventoryReplyEventArgs e)
    {
        var handler = m_TaskInventoryReply;
        if (handler != null)
            handler(this, e);
    }

    /// <summary>Thread sync lock object</summary>
    private readonly object m_TaskInventoryReplyLock = new();

    /// <summary>
    ///     Raised when the simulator sends us data containing
    ///     ...
    /// </summary>
    public event EventHandler<TaskInventoryReplyEventArgs> TaskInventoryReply
    {
        add
        {
            lock (m_TaskInventoryReplyLock)
            {
                m_TaskInventoryReply += value;
            }
        }
        remove
        {
            lock (m_TaskInventoryReplyLock)
            {
                m_TaskInventoryReply -= value;
            }
        }
    }

    /// <summary>
    ///     Reply received when uploading an inventory asset
    /// </summary>
    /// <param name="success">Has upload been successful</param>
    /// <param name="status">Error message if upload failed</param>
    /// <param name="itemID">Inventory asset UUID</param>
    /// <param name="assetID">New asset UUID</param>
    public delegate void InventoryUploadedAssetCallback(bool success, string status, UUID itemID, UUID assetID);


    /// <summary>The event subscribers, null of no subscribers</summary>
    private EventHandler<SaveAssetToInventoryEventArgs> m_SaveAssetToInventory;

    /// <summary>Raises the SaveAssetToInventory Event</summary>
    /// <param name="e">
    ///     A SaveAssetToInventoryEventArgs object containing
    ///     the data sent from the simulator
    /// </param>
    protected virtual void OnSaveAssetToInventory(SaveAssetToInventoryEventArgs e)
    {
        var handler = m_SaveAssetToInventory;
        if (handler != null)
            handler(this, e);
    }

    /// <summary>Thread sync lock object</summary>
    private readonly object m_SaveAssetToInventoryLock = new();

    /// <summary>
    ///     Raised when the simulator sends us data containing
    ///     ...
    /// </summary>
    public event EventHandler<SaveAssetToInventoryEventArgs> SaveAssetToInventory
    {
        add
        {
            lock (m_SaveAssetToInventoryLock)
            {
                m_SaveAssetToInventory += value;
            }
        }
        remove
        {
            lock (m_SaveAssetToInventoryLock)
            {
                m_SaveAssetToInventory -= value;
            }
        }
    }

    /// <summary>
    ///     Delegate that is invoked when script upload is completed
    /// </summary>
    /// <param name="uploadSuccess">Has upload succeded (note, there still might be compile errors)</param>
    /// <param name="uploadStatus">Upload status message</param>
    /// <param name="compileSuccess">Is compilation successful</param>
    /// <param name="compileMessages">If compilation failed, list of error messages, null on compilation success</param>
    /// <param name="itemID">Script inventory UUID</param>
    /// <param name="assetID">Script's new asset UUID</param>
    public delegate void ScriptUpdatedCallback(bool uploadSuccess, string uploadStatus, bool compileSuccess,
        List<string> compileMessages, UUID itemID, UUID assetID);

    /// <summary>The event subscribers, null of no subscribers</summary>
    private EventHandler<ScriptRunningReplyEventArgs> m_ScriptRunningReply;

    /// <summary>Raises the ScriptRunningReply Event</summary>
    /// <param name="e">
    ///     A ScriptRunningReplyEventArgs object containing
    ///     the data sent from the simulator
    /// </param>
    protected virtual void OnScriptRunningReply(ScriptRunningReplyEventArgs e)
    {
        var handler = m_ScriptRunningReply;
        if (handler != null)
            handler(this, e);
    }

    /// <summary>Thread sync lock object</summary>
    private readonly object m_ScriptRunningReplyLock = new();

    /// <summary>
    ///     Raised when the simulator sends us data containing
    ///     ...
    /// </summary>
    public event EventHandler<ScriptRunningReplyEventArgs> ScriptRunningReply
    {
        add
        {
            lock (m_ScriptRunningReplyLock)
            {
                m_ScriptRunningReply += value;
            }
        }
        remove
        {
            lock (m_ScriptRunningReplyLock)
            {
                m_ScriptRunningReply -= value;
            }
        }
    }

    #endregion Delegates


    #region Fetch

    /// <summary>
    ///     Fetch an inventory item from the dataserver
    /// </summary>
    /// <param name="itemID">The items <seealso cref="UUID" /></param>
    /// <param name="ownerID">The item Owners <seealso cref="OpenMetaverse.UUID" /></param>
    /// <param name="timeoutMS">a integer representing the number of milliseconds to wait for results</param>
    /// <returns>An <seealso cref="InventoryItem" /> object on success, or null if no item was found</returns>
    /// <remarks>Items will also be sent to the <seealso cref="InventoryManager.OnItemReceived" /> event</remarks>
    public InventoryItem FetchItem(UUID itemID, UUID ownerID, int timeoutMS)
    {
        var fetchEvent = new AutoResetEvent(false);
        InventoryItem fetchedItem = null;

        EventHandler<ItemReceivedEventArgs> callback =
            delegate(object sender, ItemReceivedEventArgs e)
            {
                if (e.Item.UUID == itemID)
                {
                    fetchedItem = e.Item;
                    fetchEvent.Set();
                }
            };

        ItemReceived += callback;
        RequestFetchInventory(itemID, ownerID);

        fetchEvent.WaitOne(timeoutMS, false);
        ItemReceived -= callback;

        return fetchedItem;
    }

    /// <summary>
    ///     Request A single inventory item
    /// </summary>
    /// <param name="itemID">The items <seealso cref="OpenMetaverse.UUID" /></param>
    /// <param name="ownerID">The item Owners <seealso cref="OpenMetaverse.UUID" /></param>
    /// <seealso cref="InventoryManager.OnItemReceived" />
    public void RequestFetchInventory(UUID itemID, UUID ownerID)
    {
        RequestFetchInventory(new List<UUID>(1) { itemID }, new List<UUID>(1) { ownerID });
    }

    /// <summary>
    ///     Request inventory items
    /// </summary>
    /// <param name="itemIDs">Inventory items to request</param>
    /// <param name="ownerIDs">Owners of the inventory items</param>
    /// <seealso cref="InventoryManager.OnItemReceived" />
    public void RequestFetchInventory(List<UUID> itemIDs, List<UUID> ownerIDs)
    {
        if (itemIDs.Count != ownerIDs.Count)
            throw new ArgumentException("itemIDs and ownerIDs must contain the same number of entries");

        if (Client.Settings.HTTP_INVENTORY &&
            Client.Network.CurrentSim.Caps != null &&
            Client.Network.CurrentSim.Caps.CapabilityURI("FetchInventory2") != null)
        {
            RequestFetchInventoryCap(itemIDs, ownerIDs);
            return;
        }


        var fetch = new FetchInventoryPacket();
        fetch.AgentData = new FetchInventoryPacket.AgentDataBlock();
        fetch.AgentData.AgentID = Client.Self.AgentID;
        fetch.AgentData.SessionID = Client.Self.SessionID;

        fetch.InventoryData = new FetchInventoryPacket.InventoryDataBlock[itemIDs.Count];
        for (var i = 0; i < itemIDs.Count; i++)
        {
            fetch.InventoryData[i] = new FetchInventoryPacket.InventoryDataBlock();
            fetch.InventoryData[i].ItemID = itemIDs[i];
            fetch.InventoryData[i].OwnerID = ownerIDs[i];
        }

        Client.Network.SendPacket(fetch);
    }

    /// <summary>
    ///     Request inventory items via Capabilities
    /// </summary>
    /// <param name="itemIDs">Inventory items to request</param>
    /// <param name="ownerIDs">Owners of the inventory items</param>
    /// <seealso cref="InventoryManager.OnItemReceived" />
    private void RequestFetchInventoryCap(List<UUID> itemIDs, List<UUID> ownerIDs)
    {
        if (itemIDs.Count != ownerIDs.Count)
            throw new ArgumentException("itemIDs and ownerIDs must contain the same number of entries");

        if (Client.Settings.HTTP_INVENTORY &&
            Client.Network.CurrentSim.Caps != null &&
            Client.Network.CurrentSim.Caps.CapabilityURI("FetchInventory2") != null)
        {
            var url = Client.Network.CurrentSim.Caps.CapabilityURI("FetchInventory2");
            var request = new CapsClient(url);

            request.OnComplete += (client, result, error) =>
            {
                if (error == null)
                    try
                    {
                        var res = (OSDMap)result;
                        var itemsOSD = (OSDArray)res["items"];

                        for (var i = 0; i < itemsOSD.Count; i++)
                        {
                            var item = InventoryItem.FromOSD(itemsOSD[i]);
                            Store[item.UUID] = item;
                            OnItemReceived(new ItemReceivedEventArgs(item));
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Log("Failed getting data from FetchInventory2 capability.", Helpers.LogLevel.Error,
                            Client, ex);
                    }
            };

            var OSDRequest = new OSDMap();
            OSDRequest["agent_id"] = Client.Self.AgentID;

            var items = new OSDArray(itemIDs.Count);
            for (var i = 0; i < itemIDs.Count; i++)
            {
                var item = new OSDMap(2);
                item["item_id"] = itemIDs[i];
                item["owner_id"] = ownerIDs[i];
                items.Add(item);
            }

            OSDRequest["items"] = items;

            request.BeginGetResponse(OSDRequest, OSDFormat.Xml, Client.Settings.CAPS_TIMEOUT);
        }
    }

    /// <summary>
    ///     Get contents of a folder
    /// </summary>
    /// <param name="folder">The <seealso cref="UUID" /> of the folder to search</param>
    /// <param name="owner">The <seealso cref="UUID" /> of the folders owner</param>
    /// <param name="folders">true to retrieve folders</param>
    /// <param name="items">true to retrieve items</param>
    /// <param name="order">sort order to return results in</param>
    /// <param name="timeoutMS">a integer representing the number of milliseconds to wait for results</param>
    /// <returns>A list of inventory items matching search criteria within folder</returns>
    /// <seealso cref="InventoryManager.RequestFolderContents" />
    /// <remarks>
    ///     InventoryFolder.DescendentCount will only be accurate if both folders and items are
    ///     requested
    /// </remarks>
    public List<InventoryBase> FolderContents(UUID folder, UUID owner, bool folders, bool items,
        InventorySortOrder order, int timeoutMS)
    {
        List<InventoryBase> objects = null;
        var fetchEvent = new AutoResetEvent(false);

        EventHandler<FolderUpdatedEventArgs> callback =
            delegate(object sender, FolderUpdatedEventArgs e)
            {
                if (e.FolderID == folder
                    && Store[folder] is InventoryFolder)
                {
                    // InventoryDescendentsHandler only stores DescendendCount if both folders and items are fetched.
                    if (Store.GetContents(folder).Count >= ((InventoryFolder)Store[folder]).DescendentCount)
                        fetchEvent.Set();
                }
                else
                {
                    fetchEvent.Set();
                }
            };

        FolderUpdated += callback;

        RequestFolderContents(folder, owner, folders, items, order);
        if (fetchEvent.WaitOne(timeoutMS, false))
            objects = Store.GetContents(folder);

        FolderUpdated -= callback;

        return objects;
    }

    /// <summary>
    ///     Request the contents of an inventory folder
    /// </summary>
    /// <param name="folder">The folder to search</param>
    /// <param name="owner">The folder owners <seealso cref="UUID" /></param>
    /// <param name="folders">true to return <seealso cref="InventoryManager.InventoryFolder" />s contained in folder</param>
    /// <param name="items">true to return <seealso cref="InventoryManager.InventoryItem" />s containd in folder</param>
    /// <param name="order">the sort order to return items in</param>
    /// <seealso cref="InventoryManager.FolderContents" />
    public void RequestFolderContents(UUID folder, UUID owner, bool folders, bool items,
        InventorySortOrder order)
    {
        var cap = owner == Client.Self.AgentID ? "FetchInventoryDescendents2" : "FetchLibDescendents2";

        if (Client.Settings.HTTP_INVENTORY &&
            Client.Network.CurrentSim.Caps != null &&
            Client.Network.CurrentSim.Caps.CapabilityURI(cap) != null)
        {
            RequestFolderContentsCap(folder, owner, folders, items, order);
            return;
        }

        var fetch = new FetchInventoryDescendentsPacket();
        fetch.AgentData.AgentID = Client.Self.AgentID;
        fetch.AgentData.SessionID = Client.Self.SessionID;

        fetch.InventoryData.FetchFolders = folders;
        fetch.InventoryData.FetchItems = items;
        fetch.InventoryData.FolderID = folder;
        fetch.InventoryData.OwnerID = owner;
        fetch.InventoryData.SortOrder = (int)order;

        Client.Network.SendPacket(fetch);
    }

    /// <summary>
    ///     Request the contents of an inventory folder using HTTP capabilities
    /// </summary>
    /// <param name="folderID">The folder to search</param>
    /// <param name="ownerID">The folder owners <seealso cref="UUID" /></param>
    /// <param name="fetchFolders">true to return <seealso cref="InventoryManager.InventoryFolder" />s contained in folder</param>
    /// <param name="fetchItems">true to return <seealso cref="InventoryManager.InventoryItem" />s containd in folder</param>
    /// <param name="order">the sort order to return items in</param>
    /// <seealso cref="InventoryManager.FolderContents" />
    public void RequestFolderContentsCap(UUID folderID, UUID ownerID, bool fetchFolders, bool fetchItems,
        InventorySortOrder order)
    {
        Uri url = null;
        var cap = ownerID == Client.Self.AgentID ? "FetchInventoryDescendents2" : "FetchLibDescendents2";
        if (Client.Network.CurrentSim.Caps == null ||
            null == (url = Client.Network.CurrentSim.Caps.CapabilityURI(cap)))
        {
            Logger.Log(cap + " capability not available in the current sim", Helpers.LogLevel.Warning, Client);
            OnFolderUpdated(new FolderUpdatedEventArgs(folderID, false));
            return;
        }

        var folder = new InventoryFolder(folderID);
        folder.OwnerID = ownerID;
        folder.UUID = folderID;
        RequestFolderContentsCap(new List<InventoryFolder> { folder }, url, fetchFolders, fetchItems, order);
    }

    public void RequestFolderContentsCap(List<InventoryFolder> batch, Uri url, bool fetchFolders, bool fetchItems,
        InventorySortOrder order)
    {
        try
        {
            var request = new CapsClient(url);
            request.OnComplete += (client, result, error) =>
            {
                try
                {
                    if (error != null) throw error;

                    var resultMap = (OSDMap)result;
                    if (resultMap.ContainsKey("folders"))
                    {
                        var fetchedFolders = (OSDArray)resultMap["folders"];
                        for (var fetchedFolderNr = 0; fetchedFolderNr < fetchedFolders.Count; fetchedFolderNr++)
                        {
                            var res = (OSDMap)fetchedFolders[fetchedFolderNr];
                            InventoryFolder fetchedFolder = null;

                            if (Store.Contains(res["folder_id"])
                                && Store[res["folder_id"]] is InventoryFolder)
                            {
                                fetchedFolder = (InventoryFolder)Store[res["folder_id"]];
                            }
                            else
                            {
                                fetchedFolder = new InventoryFolder(res["folder_id"]);
                                Store[res["folder_id"]] = fetchedFolder;
                            }

                            fetchedFolder.DescendentCount = res["descendents"];
                            fetchedFolder.Version = res["version"];
                            fetchedFolder.OwnerID = res["owner_id"];
                            Store.GetNodeFor(fetchedFolder.UUID).NeedsUpdate = false;

                            // Do we have any descendants
                            if (fetchedFolder.DescendentCount > 0)
                                // Fetch descendent folders
                                if (res["categories"] is OSDArray)
                                {
                                    var folders = (OSDArray)res["categories"];
                                    for (var i = 0; i < folders.Count; i++)
                                    {
                                        var descFolder = (OSDMap)folders[i];
                                        InventoryFolder folder;
                                        UUID folderID = descFolder.ContainsKey("category_id")
                                            ? descFolder["category_id"]
                                            : descFolder["folder_id"];
                                        if (!Store.Contains(folderID))
                                        {
                                            folder = new InventoryFolder(folderID);
                                            folder.ParentUUID = descFolder["parent_id"];
                                            Store[folderID] = folder;
                                        }
                                        else
                                        {
                                            folder = (InventoryFolder)Store[folderID];
                                        }

                                        folder.OwnerID = descFolder["agent_id"];
                                        folder.ParentUUID = descFolder["parent_id"];
                                        folder.Name = descFolder["name"];
                                        folder.Version = descFolder["version"];
                                        folder.PreferredType = (FolderType)(int)descFolder["type_default"];
                                    }

                                    // Fetch descendent items
                                    var items = (OSDArray)res["items"];
                                    for (var i = 0; i < items.Count; i++)
                                    {
                                        var item = InventoryItem.FromOSD(items[i]);
                                        Store[item.UUID] = item;
                                    }
                                }

                            OnFolderUpdated(new FolderUpdatedEventArgs(res["folder_id"], true));
                        }
                    }
                }
                catch (Exception exc)
                {
                    Logger.Log(
                        string.Format("Failed to fetch inventory descendants: {0}\n{1}", exc.Message, exc.StackTrace),
                        Helpers.LogLevel.Warning, Client);
                    foreach (var f in batch) OnFolderUpdated(new FolderUpdatedEventArgs(f.UUID, false));
                }
            };

            // Construct request
            var requestedFolders = new OSDArray(1);
            foreach (var f in batch)
            {
                var requestedFolder = new OSDMap(1);
                requestedFolder["folder_id"] = f.UUID;
                requestedFolder["owner_id"] = f.OwnerID;
                requestedFolder["fetch_folders"] = fetchFolders;
                requestedFolder["fetch_items"] = fetchItems;
                requestedFolder["sort_order"] = (int)order;

                requestedFolders.Add(requestedFolder);
            }

            var req = new OSDMap(1);
            req["folders"] = requestedFolders;

            request.BeginGetResponse(req, OSDFormat.Xml, Client.Settings.CAPS_TIMEOUT);
        }
        catch (Exception ex)
        {
            Logger.Log(string.Format("Failed to fetch inventory descendants: {0}\n{1}", ex.Message, ex.StackTrace),
                Helpers.LogLevel.Warning, Client);
            foreach (var f in batch) OnFolderUpdated(new FolderUpdatedEventArgs(f.UUID, false));
        }
    }

    #endregion Fetch

    #region Find

    /// <summary>
    ///     Returns the UUID of the folder (category) that defaults to
    ///     containing 'type'. The folder is not necessarily only for that
    ///     type
    /// </summary>
    /// <remarks>This will return the root folder if one does not exist</remarks>
    /// <param name="type"></param>
    /// <returns>
    ///     The UUID of the desired folder if found, the UUID of the RootFolder
    ///     if not found, or UUID.Zero on failure
    /// </returns>
    public UUID FindFolderForType(AssetType type)
    {
        if (Store == null)
        {
            Logger.Log("Inventory is null, FindFolderForType() lookup cannot continue",
                Helpers.LogLevel.Error, Client);
            return UUID.Zero;
        }

        // Folders go in the root
        if (type == AssetType.Folder)
            return Store.RootFolder.UUID;

        // Loop through each top-level directory and check if PreferredType
        // matches the requested type
        var contents = Store.GetContents(Store.RootFolder.UUID);
        foreach (var inv in contents)
            if (inv is InventoryFolder)
            {
                var folder = inv as InventoryFolder;

                if (folder.PreferredType == (FolderType)type)
                    return folder.UUID;
            }

        // No match found, return Root Folder ID
        return Store.RootFolder.UUID;
    }

    public UUID FindFolderForType(FolderType type)
    {
        if (Store == null)
        {
            Logger.Log("Inventory is null, FindFolderForType() lookup cannot continue",
                Helpers.LogLevel.Error, Client);
            return UUID.Zero;
        }

        var contents = Store.GetContents(Store.RootFolder.UUID);
        foreach (var inv in contents)
            if (inv is InventoryFolder)
            {
                var folder = inv as InventoryFolder;

                if (folder.PreferredType == type)
                    return folder.UUID;
            }

        // No match found, return Root Folder ID
        return Store.RootFolder.UUID;
    }

    /// <summary>
    ///     Find an object in inventory using a specific path to search
    /// </summary>
    /// <param name="baseFolder">The folder to begin the search in</param>
    /// <param name="inventoryOwner">The object owners <seealso cref="UUID" /></param>
    /// <param name="path">A string path to search</param>
    /// <param name="timeoutMS">milliseconds to wait for a reply</param>
    /// <returns>
    ///     Found items <seealso cref="UUID" /> or <seealso cref="UUID.Zero" /> if
    ///     timeout occurs or item is not found
    /// </returns>
    public UUID FindObjectByPath(UUID baseFolder, UUID inventoryOwner, string path, int timeoutMS)
    {
        var findEvent = new AutoResetEvent(false);
        var foundItem = UUID.Zero;

        EventHandler<FindObjectByPathReplyEventArgs> callback =
            delegate(object sender, FindObjectByPathReplyEventArgs e)
            {
                if (e.Path == path)
                {
                    foundItem = e.InventoryObjectID;
                    findEvent.Set();
                }
            };

        FindObjectByPathReply += callback;

        RequestFindObjectByPath(baseFolder, inventoryOwner, path);
        findEvent.WaitOne(timeoutMS, false);

        FindObjectByPathReply -= callback;

        return foundItem;
    }

    /// <summary>
    ///     Find inventory items by path
    /// </summary>
    /// <param name="baseFolder">The folder to begin the search in</param>
    /// <param name="inventoryOwner">The object owners <seealso cref="UUID" /></param>
    /// <param name="path">A string path to search, folders/objects separated by a '/'</param>
    /// <remarks>Results are sent to the <seealso cref="InventoryManager.OnFindObjectByPath" /> event</remarks>
    public void RequestFindObjectByPath(UUID baseFolder, UUID inventoryOwner, string path)
    {
        if (path == null || path.Length == 0)
            throw new ArgumentException("Empty path is not supported");

        // Store this search
        InventorySearch search;
        search.Folder = baseFolder;
        search.Owner = inventoryOwner;
        search.Path = path.Split('/');
        search.Level = 0;
        lock (_Searches)
        {
            _Searches.Add(search);
        }

        // Start the search
        RequestFolderContents(baseFolder, inventoryOwner, true, true, InventorySortOrder.ByName);
    }

    /// <summary>
    ///     Search inventory Store object for an item or folder
    /// </summary>
    /// <param name="baseFolder">The folder to begin the search in</param>
    /// <param name="path">An array which creates a path to search</param>
    /// <param name="level">Number of levels below baseFolder to conduct searches</param>
    /// <param name="firstOnly">if True, will stop searching after first match is found</param>
    /// <returns>A list of inventory items found</returns>
    public List<InventoryBase> LocalFind(UUID baseFolder, string[] path, int level, bool firstOnly)
    {
        var objects = new List<InventoryBase>();
        //List<InventoryFolder> folders = new List<InventoryFolder>();
        var contents = Store.GetContents(baseFolder);

        foreach (var inv in contents)
            if (inv.Name.CompareTo(path[level]) == 0)
            {
                if (level == path.Length - 1)
                {
                    objects.Add(inv);
                    if (firstOnly) return objects;
                }
                else if (inv is InventoryFolder)
                {
                    objects.AddRange(LocalFind(inv.UUID, path, level + 1, firstOnly));
                }
            }

        return objects;
    }

    #endregion Find

    #region Move/Rename

    /// <summary>
    ///     Move an inventory item or folder to a new location
    /// </summary>
    /// <param name="item">The <seealso cref="T:InventoryBase" /> item or folder to move</param>
    /// <param name="newParent">The <seealso cref="T:InventoryFolder" /> to move item or folder to</param>
    public void Move(InventoryBase item, InventoryFolder newParent)
    {
        if (item is InventoryFolder)
            MoveFolder(item.UUID, newParent.UUID);
        else
            MoveItem(item.UUID, newParent.UUID);
    }

    /// <summary>
    ///     Move an inventory item or folder to a new location and change its name
    /// </summary>
    /// <param name="item">The <seealso cref="T:InventoryBase" /> item or folder to move</param>
    /// <param name="newParent">The <seealso cref="T:InventoryFolder" /> to move item or folder to</param>
    /// <param name="newName">The name to change the item or folder to</param>
    public void Move(InventoryBase item, InventoryFolder newParent, string newName)
    {
        if (item is InventoryFolder)
            MoveFolder(item.UUID, newParent.UUID, newName);
        else
            MoveItem(item.UUID, newParent.UUID, newName);
    }

    /// <summary>
    ///     Move and rename a folder
    /// </summary>
    /// <param name="folderID">The source folders <seealso cref="UUID" /></param>
    /// <param name="newparentID">The destination folders <seealso cref="UUID" /></param>
    /// <param name="newName">The name to change the folder to</param>
    public void MoveFolder(UUID folderID, UUID newparentID, string newName)
    {
        UpdateFolderProperties(folderID, newparentID, newName, FolderType.None);
    }

    /// <summary>
    ///     Update folder properties
    /// </summary>
    /// <param name="folderID"><seealso cref="UUID" /> of the folder to update</param>
    /// <param name="parentID">Sets folder's parent to <seealso cref="UUID" /></param>
    /// <param name="name">Folder name</param>
    /// <param name="type">Folder type</param>
    public void UpdateFolderProperties(UUID folderID, UUID parentID, string name, FolderType type)
    {
        lock (Store)
        {
            if (Store.Contains(folderID))
            {
                var inv = (InventoryFolder)Store[folderID];
                inv.Name = name;
                inv.ParentUUID = parentID;
                inv.PreferredType = type;
                Store.UpdateNodeFor(inv);
            }
        }

        var invFolder = new UpdateInventoryFolderPacket();
        invFolder.AgentData.AgentID = Client.Self.AgentID;
        invFolder.AgentData.SessionID = Client.Self.SessionID;
        invFolder.FolderData = new UpdateInventoryFolderPacket.FolderDataBlock[1];
        invFolder.FolderData[0] = new UpdateInventoryFolderPacket.FolderDataBlock();
        invFolder.FolderData[0].FolderID = folderID;
        invFolder.FolderData[0].ParentID = parentID;
        invFolder.FolderData[0].Name = Utils.StringToBytes(name);
        invFolder.FolderData[0].Type = (sbyte)type;

        Client.Network.SendPacket(invFolder);
    }

    /// <summary>
    ///     Move a folder
    /// </summary>
    /// <param name="folderID">The source folders <seealso cref="UUID" /></param>
    /// <param name="newParentID">The destination folders <seealso cref="UUID" /></param>
    public void MoveFolder(UUID folderID, UUID newParentID)
    {
        lock (Store)
        {
            if (Store.Contains(folderID))
            {
                var inv = Store[folderID];
                inv.ParentUUID = newParentID;
                Store.UpdateNodeFor(inv);
            }
        }

        var move = new MoveInventoryFolderPacket();
        move.AgentData.AgentID = Client.Self.AgentID;
        move.AgentData.SessionID = Client.Self.SessionID;
        move.AgentData.Stamp = false; //FIXME: ??

        move.InventoryData = new MoveInventoryFolderPacket.InventoryDataBlock[1];
        move.InventoryData[0] = new MoveInventoryFolderPacket.InventoryDataBlock();
        move.InventoryData[0].FolderID = folderID;
        move.InventoryData[0].ParentID = newParentID;

        Client.Network.SendPacket(move);
    }

    /// <summary>
    ///     Move multiple folders, the keys in the Dictionary parameter,
    ///     to a new parents, the value of that folder's key.
    /// </summary>
    /// <param name="foldersNewParents">
    ///     A Dictionary containing the
    ///     <seealso cref="UUID" /> of the source as the key, and the
    ///     <seealso cref="UUID" /> of the destination as the value
    /// </param>
    public void MoveFolders(Dictionary<UUID, UUID> foldersNewParents)
    {
        // FIXME: Use two List<UUID> to stay consistent

        lock (Store)
        {
            foreach (var entry in foldersNewParents)
                if (Store.Contains(entry.Key))
                {
                    var inv = Store[entry.Key];
                    inv.ParentUUID = entry.Value;
                    Store.UpdateNodeFor(inv);
                }
        }

        //TODO: Test if this truly supports multiple-folder move
        var move = new MoveInventoryFolderPacket();
        move.AgentData.AgentID = Client.Self.AgentID;
        move.AgentData.SessionID = Client.Self.SessionID;
        move.AgentData.Stamp = false; //FIXME: ??

        move.InventoryData = new MoveInventoryFolderPacket.InventoryDataBlock[foldersNewParents.Count];

        var index = 0;
        foreach (var folder in foldersNewParents)
        {
            var block = new MoveInventoryFolderPacket.InventoryDataBlock();
            block.FolderID = folder.Key;
            block.ParentID = folder.Value;
            move.InventoryData[index++] = block;
        }

        Client.Network.SendPacket(move);
    }


    /// <summary>
    ///     Move an inventory item to a new folder
    /// </summary>
    /// <param name="itemID">The <seealso cref="UUID" /> of the source item to move</param>
    /// <param name="folderID">The <seealso cref="UUID" /> of the destination folder</param>
    public void MoveItem(UUID itemID, UUID folderID)
    {
        MoveItem(itemID, folderID, string.Empty);
    }

    /// <summary>
    ///     Move and rename an inventory item
    /// </summary>
    /// <param name="itemID">The <seealso cref="UUID" /> of the source item to move</param>
    /// <param name="folderID">The <seealso cref="UUID" /> of the destination folder</param>
    /// <param name="newName">The name to change the folder to</param>
    public void MoveItem(UUID itemID, UUID folderID, string newName)
    {
        lock (Store)
        {
            if (Store.Contains(itemID))
            {
                var inv = Store[itemID];
                if (!string.IsNullOrEmpty(newName)) inv.Name = newName;
                inv.ParentUUID = folderID;
                Store.UpdateNodeFor(inv);
            }
        }

        var move = new MoveInventoryItemPacket();
        move.AgentData.AgentID = Client.Self.AgentID;
        move.AgentData.SessionID = Client.Self.SessionID;
        move.AgentData.Stamp = false; //FIXME: ??

        move.InventoryData = new MoveInventoryItemPacket.InventoryDataBlock[1];
        move.InventoryData[0] = new MoveInventoryItemPacket.InventoryDataBlock();
        move.InventoryData[0].ItemID = itemID;
        move.InventoryData[0].FolderID = folderID;
        move.InventoryData[0].NewName = Utils.StringToBytes(newName);

        Client.Network.SendPacket(move);
    }

    /// <summary>
    ///     Move multiple inventory items to new locations
    /// </summary>
    /// <param name="itemsNewParents">
    ///     A Dictionary containing the
    ///     <seealso cref="UUID" /> of the source item as the key, and the
    ///     <seealso cref="UUID" /> of the destination folder as the value
    /// </param>
    public void MoveItems(Dictionary<UUID, UUID> itemsNewParents)
    {
        lock (Store)
        {
            foreach (var entry in itemsNewParents)
                if (Store.Contains(entry.Key))
                {
                    var inv = Store[entry.Key];
                    inv.ParentUUID = entry.Value;
                    Store.UpdateNodeFor(inv);
                }
        }

        var move = new MoveInventoryItemPacket();
        move.AgentData.AgentID = Client.Self.AgentID;
        move.AgentData.SessionID = Client.Self.SessionID;
        move.AgentData.Stamp = false; //FIXME: ??

        move.InventoryData = new MoveInventoryItemPacket.InventoryDataBlock[itemsNewParents.Count];

        var index = 0;
        foreach (var entry in itemsNewParents)
        {
            var block = new MoveInventoryItemPacket.InventoryDataBlock();
            block.ItemID = entry.Key;
            block.FolderID = entry.Value;
            block.NewName = Utils.EmptyBytes;
            move.InventoryData[index++] = block;
        }

        Client.Network.SendPacket(move);
    }

    #endregion Move

    #region Remove

    /// <summary>
    ///     Remove descendants of a folder
    /// </summary>
    /// <param name="folder">The <seealso cref="UUID" /> of the folder</param>
    public void RemoveDescendants(UUID folder)
    {
        var purge = new PurgeInventoryDescendentsPacket();
        purge.AgentData.AgentID = Client.Self.AgentID;
        purge.AgentData.SessionID = Client.Self.SessionID;
        purge.InventoryData.FolderID = folder;
        Client.Network.SendPacket(purge);

        // Update our local copy
        lock (Store)
        {
            if (Store.Contains(folder))
            {
                var contents = Store.GetContents(folder);
                foreach (var obj in contents) Store.RemoveNodeFor(obj);
            }
        }
    }

    /// <summary>
    ///     Remove a single item from inventory
    /// </summary>
    /// <param name="item">The <seealso cref="UUID" /> of the inventory item to remove</param>
    public void RemoveItem(UUID item)
    {
        var items = new List<UUID>(1);
        items.Add(item);

        Remove(items, null);
    }

    /// <summary>
    ///     Remove a folder from inventory
    /// </summary>
    /// <param name="folder">The <seealso cref="UUID" /> of the folder to remove</param>
    public void RemoveFolder(UUID folder)
    {
        var folders = new List<UUID>(1);
        folders.Add(folder);

        Remove(null, folders);
    }

    /// <summary>
    ///     Remove multiple items or folders from inventory
    /// </summary>
    /// <param name="items">A List containing the <seealso cref="UUID" />s of items to remove</param>
    /// <param name="folders">A List containing the <seealso cref="UUID" />s of the folders to remove</param>
    public void Remove(List<UUID> items, List<UUID> folders)
    {
        if ((items == null || items.Count == 0) && (folders == null || folders.Count == 0))
            return;

        var rem = new RemoveInventoryObjectsPacket();
        rem.AgentData.AgentID = Client.Self.AgentID;
        rem.AgentData.SessionID = Client.Self.SessionID;

        if (items == null || items.Count == 0)
        {
            // To indicate that we want no items removed:
            rem.ItemData = new RemoveInventoryObjectsPacket.ItemDataBlock[1];
            rem.ItemData[0] = new RemoveInventoryObjectsPacket.ItemDataBlock();
            rem.ItemData[0].ItemID = UUID.Zero;
        }
        else
        {
            lock (Store)
            {
                rem.ItemData = new RemoveInventoryObjectsPacket.ItemDataBlock[items.Count];
                for (var i = 0; i < items.Count; i++)
                {
                    rem.ItemData[i] = new RemoveInventoryObjectsPacket.ItemDataBlock();
                    rem.ItemData[i].ItemID = items[i];

                    // Update local copy
                    if (Store.Contains(items[i]))
                        Store.RemoveNodeFor(Store[items[i]]);
                }
            }
        }

        if (folders == null || folders.Count == 0)
        {
            // To indicate we want no folders removed:
            rem.FolderData = new RemoveInventoryObjectsPacket.FolderDataBlock[1];
            rem.FolderData[0] = new RemoveInventoryObjectsPacket.FolderDataBlock();
            rem.FolderData[0].FolderID = UUID.Zero;
        }
        else
        {
            lock (Store)
            {
                rem.FolderData = new RemoveInventoryObjectsPacket.FolderDataBlock[folders.Count];
                for (var i = 0; i < folders.Count; i++)
                {
                    rem.FolderData[i] = new RemoveInventoryObjectsPacket.FolderDataBlock();
                    rem.FolderData[i].FolderID = folders[i];

                    // Update local copy
                    if (Store.Contains(folders[i]))
                        Store.RemoveNodeFor(Store[folders[i]]);
                }
            }
        }

        Client.Network.SendPacket(rem);
    }

    /// <summary>
    ///     Empty the Lost and Found folder
    /// </summary>
    public void EmptyLostAndFound()
    {
        EmptySystemFolder(FolderType.LostAndFound);
    }

    /// <summary>
    ///     Empty the Trash folder
    /// </summary>
    public void EmptyTrash()
    {
        EmptySystemFolder(FolderType.Trash);
    }

    private void EmptySystemFolder(FolderType folderType)
    {
        var items = Store.GetContents(Store.RootFolder);

        var folderKey = UUID.Zero;
        foreach (var item in items)
            if (item as InventoryFolder != null)
            {
                var folder = item as InventoryFolder;
                if (folder.PreferredType == folderType)
                {
                    folderKey = folder.UUID;
                    break;
                }
            }

        items = Store.GetContents(folderKey);
        var remItems = new List<UUID>();
        var remFolders = new List<UUID>();
        foreach (var item in items)
            if (item as InventoryFolder != null)
                remFolders.Add(item.UUID);
            else
                remItems.Add(item.UUID);
        Remove(remItems, remFolders);
    }

    #endregion Remove

    #region Create

    /// <summary>
    /// </summary>
    /// <param name="parentFolder"></param>
    /// <param name="name"></param>
    /// <param name="description"></param>
    /// <param name="type"></param>
    /// <param name="assetTransactionID">
    ///     Proper use is to upload the inventory's asset first, then provide the Asset's
    ///     TransactionID here.
    /// </param>
    /// <param name="invType"></param>
    /// <param name="nextOwnerMask"></param>
    /// <param name="callback"></param>
    public void RequestCreateItem(UUID parentFolder, string name, string description, AssetType type,
        UUID assetTransactionID,
        InventoryType invType, PermissionMask nextOwnerMask, ItemCreatedCallback callback)
    {
        // Even though WearableType 0 is Shape, in this context it is treated as NOT_WEARABLE
        RequestCreateItem(parentFolder, name, description, type, assetTransactionID, invType, 0, nextOwnerMask,
            callback);
    }

    /// <summary>
    /// </summary>
    /// <param name="parentFolder"></param>
    /// <param name="name"></param>
    /// <param name="description"></param>
    /// <param name="type"></param>
    /// <param name="assetTransactionID">
    ///     Proper use is to upload the inventory's asset first, then provide the Asset's
    ///     TransactionID here.
    /// </param>
    /// <param name="invType"></param>
    /// <param name="wearableType"></param>
    /// <param name="nextOwnerMask"></param>
    /// <param name="callback"></param>
    public void RequestCreateItem(UUID parentFolder, string name, string description, AssetType type,
        UUID assetTransactionID,
        InventoryType invType, WearableType wearableType, PermissionMask nextOwnerMask, ItemCreatedCallback callback)
    {
        var create = new CreateInventoryItemPacket();
        create.AgentData.AgentID = Client.Self.AgentID;
        create.AgentData.SessionID = Client.Self.SessionID;

        create.InventoryBlock.CallbackID = RegisterItemCreatedCallback(callback);
        create.InventoryBlock.FolderID = parentFolder;
        create.InventoryBlock.TransactionID = assetTransactionID;
        create.InventoryBlock.NextOwnerMask = (uint)nextOwnerMask;
        create.InventoryBlock.Type = (sbyte)type;
        create.InventoryBlock.InvType = (sbyte)invType;
        create.InventoryBlock.WearableType = (byte)wearableType;
        create.InventoryBlock.Name = Utils.StringToBytes(name);
        create.InventoryBlock.Description = Utils.StringToBytes(description);

        Client.Network.SendPacket(create);
    }

    /// <summary>
    ///     Creates a new inventory folder
    /// </summary>
    /// <param name="parentID">ID of the folder to put this folder in</param>
    /// <param name="name">Name of the folder to create</param>
    /// <returns>The UUID of the newly created folder</returns>
    public UUID CreateFolder(UUID parentID, string name)
    {
        return CreateFolder(parentID, name, FolderType.None);
    }

    /// <summary>
    ///     Creates a new inventory folder
    /// </summary>
    /// <param name="parentID">ID of the folder to put this folder in</param>
    /// <param name="name">Name of the folder to create</param>
    /// <param name="preferredType">
    ///     Sets this folder as the default folder
    ///     for new assets of the specified type. Use <code>FolderType.None</code>
    ///     to create a normal folder, otherwise it will likely create a
    ///     duplicate of an existing folder type
    /// </param>
    /// <returns>The UUID of the newly created folder</returns>
    /// <remarks>
    ///     If you specify a preferred type of <code>AsseType.Folder</code>
    ///     it will create a new root folder which may likely cause all sorts
    ///     of strange problems
    /// </remarks>
    public UUID CreateFolder(UUID parentID, string name, FolderType preferredType)
    {
        var id = UUID.Random();

        // Assign a folder name if one is not already set
        if (string.IsNullOrEmpty(name))
        {
            if (preferredType >= FolderType.Texture && preferredType <= FolderType.MarkplaceStock)
                name = _NewFolderNames[(int)preferredType];
            else
                name = "New Folder";
            if (name == string.Empty) name = "New Folder";
        }

        // Create the new folder locally
        var newFolder = new InventoryFolder(id);
        newFolder.Version = 1;
        newFolder.DescendentCount = 0;
        newFolder.ParentUUID = parentID;
        newFolder.PreferredType = preferredType;
        newFolder.Name = name;
        newFolder.OwnerID = Client.Self.AgentID;

        // Update the local store
        try
        {
            Store[newFolder.UUID] = newFolder;
        }
        catch (InventoryException ie)
        {
            Logger.Log(ie.Message, Helpers.LogLevel.Warning, Client, ie);
        }

        // Create the create folder packet and send it
        var create = new CreateInventoryFolderPacket();
        create.AgentData.AgentID = Client.Self.AgentID;
        create.AgentData.SessionID = Client.Self.SessionID;

        create.FolderData.FolderID = id;
        create.FolderData.ParentID = parentID;
        create.FolderData.Type = (sbyte)preferredType;
        create.FolderData.Name = Utils.StringToBytes(name);

        Client.Network.SendPacket(create);

        return id;
    }

    /// <summary>
    ///     Create an inventory item and upload asset data
    /// </summary>
    /// <param name="data">Asset data</param>
    /// <param name="name">Inventory item name</param>
    /// <param name="description">Inventory item description</param>
    /// <param name="assetType">Asset type</param>
    /// <param name="invType">Inventory type</param>
    /// <param name="folderID">Put newly created inventory in this folder</param>
    /// <param name="callback">Delegate that will receive feedback on success or failure</param>
    public void RequestCreateItemFromAsset(byte[] data, string name, string description, AssetType assetType,
        InventoryType invType, UUID folderID, ItemCreatedFromAssetCallback callback)
    {
        var permissions = new Permissions();
        permissions.EveryoneMask = PermissionMask.None;
        permissions.GroupMask = PermissionMask.None;
        permissions.NextOwnerMask = PermissionMask.All;

        RequestCreateItemFromAsset(data, name, description, assetType, invType, folderID, permissions, callback);
    }

    /// <summary>
    ///     Create an inventory item and upload asset data
    /// </summary>
    /// <param name="data">Asset data</param>
    /// <param name="name">Inventory item name</param>
    /// <param name="description">Inventory item description</param>
    /// <param name="assetType">Asset type</param>
    /// <param name="invType">Inventory type</param>
    /// <param name="folderID">Put newly created inventory in this folder</param>
    /// <param name="permissions">
    ///     Permission of the newly created item
    ///     (EveryoneMask, GroupMask, and NextOwnerMask of Permissions struct are supported)
    /// </param>
    /// <param name="callback">Delegate that will receive feedback on success or failure</param>
    public void RequestCreateItemFromAsset(byte[] data, string name, string description, AssetType assetType,
        InventoryType invType, UUID folderID, Permissions permissions, ItemCreatedFromAssetCallback callback)
    {
        if (Client.Network.CurrentSim == null || Client.Network.CurrentSim.Caps == null)
            throw new Exception("NewFileAgentInventory capability is not currently available");

        var url = Client.Network.CurrentSim.Caps.CapabilityURI("NewFileAgentInventory");

        if (url != null)
        {
            var query = new OSDMap();
            query.Add("folder_id", OSD.FromUUID(folderID));
            query.Add("asset_type", OSD.FromString(Utils.AssetTypeToString(assetType)));
            query.Add("inventory_type", OSD.FromString(Utils.InventoryTypeToString(invType)));
            query.Add("name", OSD.FromString(name));
            query.Add("description", OSD.FromString(description));
            query.Add("everyone_mask", OSD.FromInteger((int)permissions.EveryoneMask));
            query.Add("group_mask", OSD.FromInteger((int)permissions.GroupMask));
            query.Add("next_owner_mask", OSD.FromInteger((int)permissions.NextOwnerMask));
            query.Add("expected_upload_cost", OSD.FromInteger(Client.Settings.UPLOAD_COST));

            // Make the request
            var request = new CapsClient(url);
            request.OnComplete += CreateItemFromAssetResponse;
            request.UserData = new object[] { callback, data, Client.Settings.CAPS_TIMEOUT, query };

            request.BeginGetResponse(query, OSDFormat.Xml, Client.Settings.CAPS_TIMEOUT);
        }
        else
        {
            throw new Exception("NewFileAgentInventory capability is not currently available");
        }
    }

    /// <summary>
    ///     Creates inventory link to another inventory item or folder
    /// </summary>
    /// <param name="folderID">Put newly created link in folder with this UUID</param>
    /// <param name="bse">Inventory item or folder</param>
    /// <param name="callback">Method to call upon creation of the link</param>
    public void CreateLink(UUID folderID, InventoryBase bse, ItemCreatedCallback callback)
    {
        if (bse is InventoryFolder)
        {
            var folder = (InventoryFolder)bse;
            CreateLink(folderID, folder, callback);
        }
        else if (bse is InventoryItem)
        {
            var item = (InventoryItem)bse;
            CreateLink(folderID, item.UUID, item.Name, item.Description, AssetType.Link, item.InventoryType,
                UUID.Random(), callback);
        }
    }

    /// <summary>
    ///     Creates inventory link to another inventory item
    /// </summary>
    /// <param name="folderID">Put newly created link in folder with this UUID</param>
    /// <param name="item">Original inventory item</param>
    /// <param name="callback">Method to call upon creation of the link</param>
    public void CreateLink(UUID folderID, InventoryItem item, ItemCreatedCallback callback)
    {
        CreateLink(folderID, item.UUID, item.Name, item.Description, AssetType.Link, item.InventoryType, UUID.Random(),
            callback);
    }

    /// <summary>
    ///     Creates inventory link to another inventory folder
    /// </summary>
    /// <param name="folderID">Put newly created link in folder with this UUID</param>
    /// <param name="folder">Original inventory folder</param>
    /// <param name="callback">Method to call upon creation of the link</param>
    public void CreateLink(UUID folderID, InventoryFolder folder, ItemCreatedCallback callback)
    {
        CreateLink(folderID, folder.UUID, folder.Name, "", AssetType.LinkFolder, InventoryType.Folder, UUID.Random(),
            callback);
    }

    /// <summary>
    ///     Creates inventory link to another inventory item or folder
    /// </summary>
    /// <param name="folderID">Put newly created link in folder with this UUID</param>
    /// <param name="itemID">Original item's UUID</param>
    /// <param name="name">Name</param>
    /// <param name="description">Description</param>
    /// <param name="assetType">Asset Type</param>
    /// <param name="invType">Inventory Type</param>
    /// <param name="transactionID">Transaction UUID</param>
    /// <param name="callback">Method to call upon creation of the link</param>
    public void CreateLink(UUID folderID, UUID itemID, string name, string description, AssetType assetType,
        InventoryType invType, UUID transactionID, ItemCreatedCallback callback)
    {
        var create = new LinkInventoryItemPacket();
        create.AgentData.AgentID = Client.Self.AgentID;
        create.AgentData.SessionID = Client.Self.SessionID;

        create.InventoryBlock.CallbackID = RegisterItemCreatedCallback(callback);
        lock (_ItemInventoryTypeRequest)
        {
            _ItemInventoryTypeRequest[create.InventoryBlock.CallbackID] = invType;
        }

        create.InventoryBlock.FolderID = folderID;
        create.InventoryBlock.TransactionID = transactionID;
        create.InventoryBlock.OldItemID = itemID;
        create.InventoryBlock.Type = (sbyte)assetType;
        create.InventoryBlock.InvType = (sbyte)invType;
        create.InventoryBlock.Name = Utils.StringToBytes(name);
        create.InventoryBlock.Description = Utils.StringToBytes(description);

        Client.Network.SendPacket(create);
    }

    #endregion Create

    #region Copy

    /// <summary>
    /// </summary>
    /// <param name="item"></param>
    /// <param name="newParent"></param>
    /// <param name="newName"></param>
    /// <param name="callback"></param>
    public void RequestCopyItem(UUID item, UUID newParent, string newName, ItemCopiedCallback callback)
    {
        RequestCopyItem(item, newParent, newName, Client.Self.AgentID, callback);
    }

    /// <summary>
    /// </summary>
    /// <param name="item"></param>
    /// <param name="newParent"></param>
    /// <param name="newName"></param>
    /// <param name="oldOwnerID"></param>
    /// <param name="callback"></param>
    public void RequestCopyItem(UUID item, UUID newParent, string newName, UUID oldOwnerID,
        ItemCopiedCallback callback)
    {
        var items = new List<UUID>(1);
        items.Add(item);

        var folders = new List<UUID>(1);
        folders.Add(newParent);

        var names = new List<string>(1);
        names.Add(newName);

        RequestCopyItems(items, folders, names, oldOwnerID, callback);
    }

    /// <summary>
    /// </summary>
    /// <param name="items"></param>
    /// <param name="targetFolders"></param>
    /// <param name="newNames"></param>
    /// <param name="oldOwnerID"></param>
    /// <param name="callback"></param>
    public void RequestCopyItems(List<UUID> items, List<UUID> targetFolders, List<string> newNames,
        UUID oldOwnerID, ItemCopiedCallback callback)
    {
        if (items.Count != targetFolders.Count || (newNames != null && items.Count != newNames.Count))
            throw new ArgumentException("All list arguments must have an equal number of entries");

        var callbackID = RegisterItemsCopiedCallback(callback);

        var copy = new CopyInventoryItemPacket();
        copy.AgentData.AgentID = Client.Self.AgentID;
        copy.AgentData.SessionID = Client.Self.SessionID;

        copy.InventoryData = new CopyInventoryItemPacket.InventoryDataBlock[items.Count];
        for (var i = 0; i < items.Count; ++i)
        {
            copy.InventoryData[i] = new CopyInventoryItemPacket.InventoryDataBlock();
            copy.InventoryData[i].CallbackID = callbackID;
            copy.InventoryData[i].NewFolderID = targetFolders[i];
            copy.InventoryData[i].OldAgentID = oldOwnerID;
            copy.InventoryData[i].OldItemID = items[i];

            if (newNames != null && !string.IsNullOrEmpty(newNames[i]))
                copy.InventoryData[i].NewName = Utils.StringToBytes(newNames[i]);
            else
                copy.InventoryData[i].NewName = Utils.EmptyBytes;
        }

        Client.Network.SendPacket(copy);
    }

    /// <summary>
    ///     Request a copy of an asset embedded within a notecard
    /// </summary>
    /// <param name="objectID">Usually UUID.Zero for copying an asset from a notecard</param>
    /// <param name="notecardID">UUID of the notecard to request an asset from</param>
    /// <param name="folderID">Target folder for asset to go to in your inventory</param>
    /// <param name="itemID">UUID of the embedded asset</param>
    /// <param name="callback">callback to run when item is copied to inventory</param>
    public void RequestCopyItemFromNotecard(UUID objectID, UUID notecardID, UUID folderID, UUID itemID,
        ItemCopiedCallback callback)
    {
        _ItemCopiedCallbacks[0] = callback; //Notecards always use callback ID 0

        var url = Client.Network.CurrentSim.Caps.CapabilityURI("CopyInventoryFromNotecard");

        if (url != null)
        {
            var message = new CopyInventoryFromNotecardMessage();
            message.CallbackID = 0;
            message.FolderID = folderID;
            message.ItemID = itemID;
            message.NotecardID = notecardID;
            message.ObjectID = objectID;

            var request = new CapsClient(url);
            request.BeginGetResponse(message.Serialize(), OSDFormat.Xml, Client.Settings.CAPS_TIMEOUT);
        }
        else
        {
            var copy = new CopyInventoryFromNotecardPacket();
            copy.AgentData.AgentID = Client.Self.AgentID;
            copy.AgentData.SessionID = Client.Self.SessionID;

            copy.NotecardData.ObjectID = objectID;
            copy.NotecardData.NotecardItemID = notecardID;

            copy.InventoryData = new CopyInventoryFromNotecardPacket.InventoryDataBlock[1];
            copy.InventoryData[0] = new CopyInventoryFromNotecardPacket.InventoryDataBlock();
            copy.InventoryData[0].FolderID = folderID;
            copy.InventoryData[0].ItemID = itemID;

            Client.Network.SendPacket(copy);
        }
    }

    #endregion Copy

    #region Update

    /// <summary>
    /// </summary>
    /// <param name="item"></param>
    public void RequestUpdateItem(InventoryItem item)
    {
        var items = new List<InventoryItem>(1);
        items.Add(item);

        RequestUpdateItems(items, UUID.Random());
    }

    /// <summary>
    /// </summary>
    /// <param name="items"></param>
    public void RequestUpdateItems(List<InventoryItem> items)
    {
        RequestUpdateItems(items, UUID.Random());
    }

    /// <summary>
    /// </summary>
    /// <param name="items"></param>
    /// <param name="transactionID"></param>
    public void RequestUpdateItems(List<InventoryItem> items, UUID transactionID)
    {
        var update = new UpdateInventoryItemPacket();
        update.AgentData.AgentID = Client.Self.AgentID;
        update.AgentData.SessionID = Client.Self.SessionID;
        update.AgentData.TransactionID = transactionID;

        update.InventoryData = new UpdateInventoryItemPacket.InventoryDataBlock[items.Count];
        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];

            var block = new UpdateInventoryItemPacket.InventoryDataBlock();
            block.BaseMask = (uint)item.Permissions.BaseMask;
            block.CRC = ItemCRC(item);
            block.CreationDate = (int)Utils.DateTimeToUnixTime(item.CreationDate);
            block.CreatorID = item.CreatorID;
            block.Description = Utils.StringToBytes(item.Description);
            block.EveryoneMask = (uint)item.Permissions.EveryoneMask;
            block.Flags = item.Flags;
            block.FolderID = item.ParentUUID;
            block.GroupID = item.GroupID;
            block.GroupMask = (uint)item.Permissions.GroupMask;
            block.GroupOwned = item.GroupOwned;
            block.InvType = (sbyte)item.InventoryType;
            block.ItemID = item.UUID;
            block.Name = Utils.StringToBytes(item.Name);
            block.NextOwnerMask = (uint)item.Permissions.NextOwnerMask;
            block.OwnerID = item.OwnerID;
            block.OwnerMask = (uint)item.Permissions.OwnerMask;
            block.SalePrice = item.SalePrice;
            block.SaleType = (byte)item.SaleType;
            block.TransactionID = item.TransactionID;
            block.Type = (sbyte)item.AssetType;

            update.InventoryData[i] = block;
        }

        Client.Network.SendPacket(update);
    }

    /// <summary>
    /// </summary>
    /// <param name="data"></param>
    /// <param name="notecardID"></param>
    /// <param name="callback"></param>
    public void RequestUploadNotecardAsset(byte[] data, UUID notecardID, InventoryUploadedAssetCallback callback)
    {
        if (Client.Network.CurrentSim == null || Client.Network.CurrentSim.Caps == null)
            throw new Exception("UpdateNotecardAgentInventory capability is not currently available");

        var url = Client.Network.CurrentSim.Caps.CapabilityURI("UpdateNotecardAgentInventory");

        if (url != null)
        {
            var query = new OSDMap();
            query.Add("item_id", OSD.FromUUID(notecardID));

            // Make the request
            var request = new CapsClient(url);
            request.OnComplete += UploadInventoryAssetResponse;
            request.UserData = new object[]
                { new KeyValuePair<InventoryUploadedAssetCallback, byte[]>(callback, data), notecardID };
            request.BeginGetResponse(query, OSDFormat.Xml, Client.Settings.CAPS_TIMEOUT);
        }
        else
        {
            throw new Exception("UpdateNotecardAgentInventory capability is not currently available");
        }
    }

    /// <summary>
    ///     Save changes to notecard embedded in object contents
    /// </summary>
    /// <param name="data">Encoded notecard asset data</param>
    /// <param name="notecardID">Notecard UUID</param>
    /// <param name="taskID">Object's UUID</param>
    /// <param name="callback">Called upon finish of the upload with status information</param>
    public void RequestUpdateNotecardTask(byte[] data, UUID notecardID, UUID taskID,
        InventoryUploadedAssetCallback callback)
    {
        if (Client.Network.CurrentSim == null || Client.Network.CurrentSim.Caps == null)
            throw new Exception("UpdateNotecardTaskInventory capability is not currently available");

        var url = Client.Network.CurrentSim.Caps.CapabilityURI("UpdateNotecardTaskInventory");

        if (url != null)
        {
            var query = new OSDMap();
            query.Add("item_id", OSD.FromUUID(notecardID));
            query.Add("task_id", OSD.FromUUID(taskID));

            // Make the request
            var request = new CapsClient(url);
            request.OnComplete += UploadInventoryAssetResponse;
            request.UserData = new object[]
                { new KeyValuePair<InventoryUploadedAssetCallback, byte[]>(callback, data), notecardID };
            request.BeginGetResponse(query, OSDFormat.Xml, Client.Settings.CAPS_TIMEOUT);
        }
        else
        {
            throw new Exception("UpdateNotecardTaskInventory capability is not currently available");
        }
    }

    /// <summary>
    ///     Upload new gesture asset for an inventory gesture item
    /// </summary>
    /// <param name="data">Encoded gesture asset</param>
    /// <param name="gestureID">Gesture inventory UUID</param>
    /// <param name="callback">Callback whick will be called when upload is complete</param>
    public void RequestUploadGestureAsset(byte[] data, UUID gestureID, InventoryUploadedAssetCallback callback)
    {
        if (Client.Network.CurrentSim == null || Client.Network.CurrentSim.Caps == null)
            throw new Exception("UpdateGestureAgentInventory capability is not currently available");

        var url = Client.Network.CurrentSim.Caps.CapabilityURI("UpdateGestureAgentInventory");

        if (url != null)
        {
            var query = new OSDMap();
            query.Add("item_id", OSD.FromUUID(gestureID));

            // Make the request
            var request = new CapsClient(url);
            request.OnComplete += UploadInventoryAssetResponse;
            request.UserData = new object[]
                { new KeyValuePair<InventoryUploadedAssetCallback, byte[]>(callback, data), gestureID };
            request.BeginGetResponse(query, OSDFormat.Xml, Client.Settings.CAPS_TIMEOUT);
        }
        else
        {
            throw new Exception("UpdateGestureAgentInventory capability is not currently available");
        }
    }

    /// <summary>
    ///     Update an existing script in an agents Inventory
    /// </summary>
    /// <param name="data">A byte[] array containing the encoded scripts contents</param>
    /// <param name="itemID">the itemID of the script</param>
    /// <param name="mono">if true, sets the script content to run on the mono interpreter</param>
    /// <param name="callback"></param>
    public void RequestUpdateScriptAgentInventory(byte[] data, UUID itemID, bool mono, ScriptUpdatedCallback callback)
    {
        var url = Client.Network.CurrentSim.Caps.CapabilityURI("UpdateScriptAgent");

        if (url != null)
        {
            var msg = new UpdateScriptAgentRequestMessage();
            msg.ItemID = itemID;
            msg.Target = mono ? "mono" : "lsl2";

            var request = new CapsClient(url);
            request.OnComplete += UpdateScriptAgentInventoryResponse;
            request.UserData = new object[2]
                { new KeyValuePair<ScriptUpdatedCallback, byte[]>(callback, data), itemID };
            request.BeginGetResponse(msg.Serialize(), OSDFormat.Xml, Client.Settings.CAPS_TIMEOUT);
        }
        else
        {
            throw new Exception("UpdateScriptAgent capability is not currently available");
        }
    }

    /// <summary>
    ///     Update an existing script in an task Inventory
    /// </summary>
    /// <param name="data">A byte[] array containing the encoded scripts contents</param>
    /// <param name="itemID">the itemID of the script</param>
    /// <param name="taskID">UUID of the prim containting the script</param>
    /// <param name="mono">if true, sets the script content to run on the mono interpreter</param>
    /// <param name="running">if true, sets the script to running</param>
    /// <param name="callback"></param>
    public void RequestUpdateScriptTask(byte[] data, UUID itemID, UUID taskID, bool mono, bool running,
        ScriptUpdatedCallback callback)
    {
        var url = Client.Network.CurrentSim.Caps.CapabilityURI("UpdateScriptTask");

        if (url != null)
        {
            var msg = new UpdateScriptTaskUpdateMessage();
            msg.ItemID = itemID;
            msg.TaskID = taskID;
            msg.ScriptRunning = running;
            msg.Target = mono ? "mono" : "lsl2";

            var request = new CapsClient(url);
            request.OnComplete += UpdateScriptAgentInventoryResponse;
            request.UserData = new object[2]
                { new KeyValuePair<ScriptUpdatedCallback, byte[]>(callback, data), itemID };
            request.BeginGetResponse(msg.Serialize(), OSDFormat.Xml, Client.Settings.CAPS_TIMEOUT);
        }
        else
        {
            throw new Exception("UpdateScriptTask capability is not currently available");
        }
    }

    #endregion Update

    #region Rez/Give

    /// <summary>
    ///     Rez an object from inventory
    /// </summary>
    /// <param name="simulator">Simulator to place object in</param>
    /// <param name="rotation">Rotation of the object when rezzed</param>
    /// <param name="position">Vector of where to place object</param>
    /// <param name="item">InventoryItem object containing item details</param>
    public UUID RequestRezFromInventory(Simulator simulator, Quaternion rotation, Vector3 position,
        InventoryItem item)
    {
        return RequestRezFromInventory(simulator, rotation, position, item, Client.Self.ActiveGroup,
            UUID.Random(), true);
    }

    /// <summary>
    ///     Rez an object from inventory
    /// </summary>
    /// <param name="simulator">Simulator to place object in</param>
    /// <param name="rotation">Rotation of the object when rezzed</param>
    /// <param name="position">Vector of where to place object</param>
    /// <param name="item">InventoryItem object containing item details</param>
    /// <param name="groupOwner">UUID of group to own the object</param>
    public UUID RequestRezFromInventory(Simulator simulator, Quaternion rotation, Vector3 position,
        InventoryItem item, UUID groupOwner)
    {
        return RequestRezFromInventory(simulator, rotation, position, item, groupOwner, UUID.Random(), true);
    }

    /// <summary>
    ///     Rez an object from inventory
    /// </summary>
    /// <param name="simulator">Simulator to place object in</param>
    /// <param name="rotation">Rotation of the object when rezzed</param>
    /// <param name="position">Vector of where to place object</param>
    /// <param name="item">InventoryItem object containing item details</param>
    /// <param name="groupOwner">UUID of group to own the object</param>
    /// <param name="queryID">User defined queryID to correlate replies</param>
    /// <param name="rezSelected">
    ///     If set to true, the CreateSelected flag
    ///     will be set on the rezzed object
    /// </param>
    public UUID RequestRezFromInventory(Simulator simulator, Quaternion rotation, Vector3 position,
        InventoryItem item, UUID groupOwner, UUID queryID, bool rezSelected)
    {
        return RequestRezFromInventory(simulator, UUID.Zero, rotation, position, item, groupOwner, queryID,
            rezSelected);
    }

    /// <summary>
    ///     Rez an object from inventory
    /// </summary>
    /// <param name="simulator">Simulator to place object in</param>
    /// <param name="taskID">TaskID object when rezzed</param>
    /// <param name="rotation">Rotation of the object when rezzed</param>
    /// <param name="position">Vector of where to place object</param>
    /// <param name="item">InventoryItem object containing item details</param>
    /// <param name="groupOwner">UUID of group to own the object</param>
    /// <param name="queryID">User defined queryID to correlate replies</param>
    /// <param name="rezSelected">
    ///     If set to true, the CreateSelected flag
    ///     will be set on the rezzed object
    /// </param>
    public UUID RequestRezFromInventory(Simulator simulator, UUID taskID, Quaternion rotation, Vector3 position,
        InventoryItem item, UUID groupOwner, UUID queryID, bool rezSelected)
    {
        var add = new RezObjectPacket();

        add.AgentData.AgentID = Client.Self.AgentID;
        add.AgentData.SessionID = Client.Self.SessionID;
        add.AgentData.GroupID = groupOwner;

        add.RezData.FromTaskID = taskID;
        add.RezData.BypassRaycast = 1;
        add.RezData.RayStart = position;
        add.RezData.RayEnd = position;
        add.RezData.RayTargetID = UUID.Zero;
        add.RezData.RayEndIsIntersection = false;
        add.RezData.RezSelected = rezSelected;
        add.RezData.RemoveItem = false;
        add.RezData.ItemFlags = item.Flags;
        add.RezData.GroupMask = (uint)item.Permissions.GroupMask;
        add.RezData.EveryoneMask = (uint)item.Permissions.EveryoneMask;
        add.RezData.NextOwnerMask = (uint)item.Permissions.NextOwnerMask;

        add.InventoryData.ItemID = item.UUID;
        add.InventoryData.FolderID = item.ParentUUID;
        add.InventoryData.CreatorID = item.CreatorID;
        add.InventoryData.OwnerID = item.OwnerID;
        add.InventoryData.GroupID = item.GroupID;
        add.InventoryData.BaseMask = (uint)item.Permissions.BaseMask;
        add.InventoryData.OwnerMask = (uint)item.Permissions.OwnerMask;
        add.InventoryData.GroupMask = (uint)item.Permissions.GroupMask;
        add.InventoryData.EveryoneMask = (uint)item.Permissions.EveryoneMask;
        add.InventoryData.NextOwnerMask = (uint)item.Permissions.NextOwnerMask;
        add.InventoryData.GroupOwned = item.GroupOwned;
        add.InventoryData.TransactionID = queryID;
        add.InventoryData.Type = (sbyte)item.InventoryType;
        add.InventoryData.InvType = (sbyte)item.InventoryType;
        add.InventoryData.Flags = item.Flags;
        add.InventoryData.SaleType = (byte)item.SaleType;
        add.InventoryData.SalePrice = item.SalePrice;
        add.InventoryData.Name = Utils.StringToBytes(item.Name);
        add.InventoryData.Description = Utils.StringToBytes(item.Description);
        add.InventoryData.CreationDate = (int)Utils.DateTimeToUnixTime(item.CreationDate);

        Client.Network.SendPacket(add, simulator);

        // Remove from store if the item is no copy
        if (Store.Items.ContainsKey(item.UUID) && Store[item.UUID] is InventoryItem)
        {
            var invItem = (InventoryItem)Store[item.UUID];
            if ((invItem.Permissions.OwnerMask & PermissionMask.Copy) == PermissionMask.None)
                Store.RemoveNodeFor(invItem);
        }

        return queryID;
    }

    /// <summary>
    ///     DeRez an object from the simulator to the agents Objects folder in the agents Inventory
    /// </summary>
    /// <param name="objectLocalID">The simulator Local ID of the object</param>
    /// <remarks>If objectLocalID is a child primitive in a linkset, the entire linkset will be derezzed</remarks>
    public void RequestDeRezToInventory(uint objectLocalID)
    {
        RequestDeRezToInventory(objectLocalID, DeRezDestination.AgentInventoryTake,
            Client.Inventory.FindFolderForType(AssetType.Object), UUID.Random());
    }

    /// <summary>
    ///     DeRez an object from the simulator and return to inventory
    /// </summary>
    /// <param name="objectLocalID">The simulator Local ID of the object</param>
    /// <param name="destType">The type of destination from the <seealso cref="DeRezDestination" /> enum</param>
    /// <param name="destFolder">
    ///     The destination inventory folders <seealso cref="UUID" /> -or-
    ///     if DeRezzing object to a tasks Inventory, the Tasks <seealso cref="UUID" />
    /// </param>
    /// <param name="transactionID">
    ///     The transaction ID for this request which
    ///     can be used to correlate this request with other packets
    /// </param>
    /// <remarks>If objectLocalID is a child primitive in a linkset, the entire linkset will be derezzed</remarks>
    public void RequestDeRezToInventory(uint objectLocalID, DeRezDestination destType, UUID destFolder,
        UUID transactionID)
    {
        var take = new DeRezObjectPacket();

        take.AgentData.AgentID = Client.Self.AgentID;
        take.AgentData.SessionID = Client.Self.SessionID;
        take.AgentBlock = new DeRezObjectPacket.AgentBlockBlock();
        take.AgentBlock.GroupID = UUID.Zero;
        take.AgentBlock.Destination = (byte)destType;
        take.AgentBlock.DestinationID = destFolder;
        take.AgentBlock.PacketCount = 1;
        take.AgentBlock.PacketNumber = 1;
        take.AgentBlock.TransactionID = transactionID;

        take.ObjectData = new DeRezObjectPacket.ObjectDataBlock[1];
        take.ObjectData[0] = new DeRezObjectPacket.ObjectDataBlock();
        take.ObjectData[0].ObjectLocalID = objectLocalID;

        Client.Network.SendPacket(take);
    }

    /// <summary>
    ///     Rez an item from inventory to its previous simulator location
    /// </summary>
    /// <param name="simulator"></param>
    /// <param name="item"></param>
    /// <param name="queryID"></param>
    /// <returns></returns>
    public UUID RequestRestoreRezFromInventory(Simulator simulator, InventoryItem item, UUID queryID)
    {
        var add = new RezRestoreToWorldPacket();

        add.AgentData.AgentID = Client.Self.AgentID;
        add.AgentData.SessionID = Client.Self.SessionID;

        add.InventoryData.ItemID = item.UUID;
        add.InventoryData.FolderID = item.ParentUUID;
        add.InventoryData.CreatorID = item.CreatorID;
        add.InventoryData.OwnerID = item.OwnerID;
        add.InventoryData.GroupID = item.GroupID;
        add.InventoryData.BaseMask = (uint)item.Permissions.BaseMask;
        add.InventoryData.OwnerMask = (uint)item.Permissions.OwnerMask;
        add.InventoryData.GroupMask = (uint)item.Permissions.GroupMask;
        add.InventoryData.EveryoneMask = (uint)item.Permissions.EveryoneMask;
        add.InventoryData.NextOwnerMask = (uint)item.Permissions.NextOwnerMask;
        add.InventoryData.GroupOwned = item.GroupOwned;
        add.InventoryData.TransactionID = queryID;
        add.InventoryData.Type = (sbyte)item.InventoryType;
        add.InventoryData.InvType = (sbyte)item.InventoryType;
        add.InventoryData.Flags = item.Flags;
        add.InventoryData.SaleType = (byte)item.SaleType;
        add.InventoryData.SalePrice = item.SalePrice;
        add.InventoryData.Name = Utils.StringToBytes(item.Name);
        add.InventoryData.Description = Utils.StringToBytes(item.Description);
        add.InventoryData.CreationDate = (int)Utils.DateTimeToUnixTime(item.CreationDate);

        Client.Network.SendPacket(add, simulator);

        return queryID;
    }

    /// <summary>
    ///     Give an inventory item to another avatar
    /// </summary>
    /// <param name="itemID">The <seealso cref="UUID" /> of the item to give</param>
    /// <param name="itemName">The name of the item</param>
    /// <param name="assetType">The type of the item from the <seealso cref="AssetType" /> enum</param>
    /// <param name="recipient">The <seealso cref="UUID" /> of the recipient</param>
    /// <param name="doEffect">true to generate a beameffect during transfer</param>
    public void GiveItem(UUID itemID, string itemName, AssetType assetType, UUID recipient,
        bool doEffect)
    {
        byte[] bucket;


        bucket = new byte[17];
        bucket[0] = (byte)assetType;
        Buffer.BlockCopy(itemID.GetBytes(), 0, bucket, 1, 16);

        Client.Self.InstantMessage(
            Client.Self.Name,
            recipient,
            itemName,
            UUID.Random(),
            InstantMessageDialog.InventoryOffered,
            InstantMessageOnline.Online,
            Client.Self.SimPosition,
            Client.Network.CurrentSim.ID,
            bucket);

        if (doEffect)
            Client.Self.BeamEffect(Client.Self.AgentID, recipient, Vector3d.Zero,
                Client.Settings.DEFAULT_EFFECT_COLOR, 1f, UUID.Random());

        // Remove from store if the item is no copy
        if (Store.Items.ContainsKey(itemID) && Store[itemID] is InventoryItem)
        {
            var invItem = (InventoryItem)Store[itemID];
            if ((invItem.Permissions.OwnerMask & PermissionMask.Copy) == PermissionMask.None)
                Store.RemoveNodeFor(invItem);
        }
    }

    /// <summary>
    ///     Give an inventory Folder with contents to another avatar
    /// </summary>
    /// <param name="folderID">The <seealso cref="UUID" /> of the Folder to give</param>
    /// <param name="folderName">The name of the folder</param>
    /// <param name="assetType">The type of the item from the <seealso cref="AssetType" /> enum</param>
    /// <param name="recipient">The <seealso cref="UUID" /> of the recipient</param>
    /// <param name="doEffect">true to generate a beameffect during transfer</param>
    public void GiveFolder(UUID folderID, string folderName, AssetType assetType, UUID recipient,
        bool doEffect)
    {
        byte[] bucket;

        var folderContents = new List<InventoryItem>();

        Client.Inventory
            .FolderContents(folderID, Client.Self.AgentID, false, true, InventorySortOrder.ByDate, 1000 * 15).ForEach(
                delegate(InventoryBase ib)
                {
                    folderContents.Add(Client.Inventory.FetchItem(ib.UUID, Client.Self.AgentID, 1000 * 10));
                });
        bucket = new byte[17 * (folderContents.Count + 1)];

        //Add parent folder (first item in bucket)
        bucket[0] = (byte)assetType;
        Buffer.BlockCopy(folderID.GetBytes(), 0, bucket, 1, 16);

        //Add contents to bucket after folder
        for (var i = 1; i <= folderContents.Count; ++i)
        {
            bucket[i * 17] = (byte)folderContents[i - 1].AssetType;
            Buffer.BlockCopy(folderContents[i - 1].UUID.GetBytes(), 0, bucket, i * 17 + 1, 16);
        }

        Client.Self.InstantMessage(
            Client.Self.Name,
            recipient,
            folderName,
            UUID.Random(),
            InstantMessageDialog.InventoryOffered,
            InstantMessageOnline.Online,
            Client.Self.SimPosition,
            Client.Network.CurrentSim.ID,
            bucket);

        if (doEffect)
            Client.Self.BeamEffect(Client.Self.AgentID, recipient, Vector3d.Zero,
                Client.Settings.DEFAULT_EFFECT_COLOR, 1f, UUID.Random());

        // Remove from store if items were no copy
        for (var i = 0; i < folderContents.Count; i++)
            if (Store.Items.ContainsKey(folderContents[i].UUID) && Store[folderContents[i].UUID] is InventoryItem)
            {
                var invItem = (InventoryItem)Store[folderContents[i].UUID];
                if ((invItem.Permissions.OwnerMask & PermissionMask.Copy) == PermissionMask.None)
                    Store.RemoveNodeFor(invItem);
            }
    }

    #endregion Rez/Give

    #region Task

    /// <summary>
    ///     Copy or move an <see cref="InventoryItem" /> from agent inventory to a task (primitive) inventory
    /// </summary>
    /// <param name="objectLocalID">The target object</param>
    /// <param name="item">The item to copy or move from inventory</param>
    /// <returns></returns>
    /// <remarks>
    ///     For items with copy permissions a copy of the item is placed in the tasks inventory,
    ///     for no-copy items the object is moved to the tasks inventory
    /// </remarks>
    // DocTODO: what does the return UUID correlate to if anything?
    public UUID UpdateTaskInventory(uint objectLocalID, InventoryItem item)
    {
        var transactionID = UUID.Random();

        var update = new UpdateTaskInventoryPacket();
        update.AgentData.AgentID = Client.Self.AgentID;
        update.AgentData.SessionID = Client.Self.SessionID;
        update.UpdateData.Key = 0;
        update.UpdateData.LocalID = objectLocalID;

        update.InventoryData.ItemID = item.UUID;
        update.InventoryData.FolderID = item.ParentUUID;
        update.InventoryData.CreatorID = item.CreatorID;
        update.InventoryData.OwnerID = item.OwnerID;
        update.InventoryData.GroupID = item.GroupID;
        update.InventoryData.BaseMask = (uint)item.Permissions.BaseMask;
        update.InventoryData.OwnerMask = (uint)item.Permissions.OwnerMask;
        update.InventoryData.GroupMask = (uint)item.Permissions.GroupMask;
        update.InventoryData.EveryoneMask = (uint)item.Permissions.EveryoneMask;
        update.InventoryData.NextOwnerMask = (uint)item.Permissions.NextOwnerMask;
        update.InventoryData.GroupOwned = item.GroupOwned;
        update.InventoryData.TransactionID = transactionID;
        update.InventoryData.Type = (sbyte)item.AssetType;
        update.InventoryData.InvType = (sbyte)item.InventoryType;
        update.InventoryData.Flags = item.Flags;
        update.InventoryData.SaleType = (byte)item.SaleType;
        update.InventoryData.SalePrice = item.SalePrice;
        update.InventoryData.Name = Utils.StringToBytes(item.Name);
        update.InventoryData.Description = Utils.StringToBytes(item.Description);
        update.InventoryData.CreationDate = (int)Utils.DateTimeToUnixTime(item.CreationDate);
        update.InventoryData.CRC = ItemCRC(item);

        Client.Network.SendPacket(update);

        return transactionID;
    }

    /// <summary>
    ///     Retrieve a listing of the items contained in a task (Primitive)
    /// </summary>
    /// <param name="objectID">The tasks <seealso cref="UUID" /></param>
    /// <param name="objectLocalID">The tasks simulator local ID</param>
    /// <param name="timeoutMS">milliseconds to wait for reply from simulator</param>
    /// <returns>
    ///     A list containing the inventory items inside the task or null
    ///     if a timeout occurs
    /// </returns>
    /// <remarks>
    ///     This request blocks until the response from the simulator arrives
    ///     or timeoutMS is exceeded
    /// </remarks>
    public List<InventoryBase> GetTaskInventory(UUID objectID, uint objectLocalID, int timeoutMS)
    {
        string filename = null;
        var taskReplyEvent = new AutoResetEvent(false);

        EventHandler<TaskInventoryReplyEventArgs> callback =
            delegate(object sender, TaskInventoryReplyEventArgs e)
            {
                if (e.ItemID == objectID)
                {
                    filename = e.AssetFilename;
                    taskReplyEvent.Set();
                }
            };

        TaskInventoryReply += callback;

        RequestTaskInventory(objectLocalID);

        if (taskReplyEvent.WaitOne(timeoutMS, false))
        {
            TaskInventoryReply -= callback;

            if (!string.IsNullOrEmpty(filename))
            {
                byte[] assetData = null;
                ulong xferID = 0;
                var taskDownloadEvent = new AutoResetEvent(false);

                EventHandler<XferReceivedEventArgs> xferCallback =
                    delegate(object sender, XferReceivedEventArgs e)
                    {
                        if (e.Xfer.XferID == xferID)
                        {
                            assetData = e.Xfer.AssetData;
                            taskDownloadEvent.Set();
                        }
                    };

                Client.Assets.XferReceived += xferCallback;

                // Start the actual asset xfer
                xferID = Client.Assets.RequestAssetXfer(filename, true, false, UUID.Zero, AssetType.Unknown, true);

                if (taskDownloadEvent.WaitOne(timeoutMS, false))
                {
                    Client.Assets.XferReceived -= xferCallback;

                    var taskList = Utils.BytesToString(assetData);
                    return ParseTaskInventory(taskList);
                }

                Logger.Log("Timed out waiting for task inventory download for " + filename, Helpers.LogLevel.Warning,
                    Client);
                Client.Assets.XferReceived -= xferCallback;
                return null;
            }

            Logger.DebugLog("Task is empty for " + objectLocalID, Client);
            return new List<InventoryBase>(0);
        }

        Logger.Log("Timed out waiting for task inventory reply for " + objectLocalID, Helpers.LogLevel.Warning, Client);
        TaskInventoryReply -= callback;
        return null;
    }

    /// <summary>
    ///     Request the contents of a tasks (primitives) inventory from the
    ///     current simulator
    /// </summary>
    /// <param name="objectLocalID">The LocalID of the object</param>
    /// <seealso cref="TaskInventoryReply" />
    public void RequestTaskInventory(uint objectLocalID)
    {
        RequestTaskInventory(objectLocalID, Client.Network.CurrentSim);
    }

    /// <summary>
    ///     Request the contents of a tasks (primitives) inventory
    /// </summary>
    /// <param name="objectLocalID">The simulator Local ID of the object</param>
    /// <param name="simulator">A reference to the simulator object that contains the object</param>
    /// <seealso cref="TaskInventoryReply" />
    public void RequestTaskInventory(uint objectLocalID, Simulator simulator)
    {
        var request = new RequestTaskInventoryPacket();
        request.AgentData.AgentID = Client.Self.AgentID;
        request.AgentData.SessionID = Client.Self.SessionID;
        request.InventoryData.LocalID = objectLocalID;

        Client.Network.SendPacket(request, simulator);
    }

    /// <summary>
    ///     Move an item from a tasks (Primitive) inventory to the specified folder in the avatars inventory
    /// </summary>
    /// <param name="objectLocalID">LocalID of the object in the simulator</param>
    /// <param name="taskItemID">UUID of the task item to move</param>
    /// <param name="inventoryFolderID">The ID of the destination folder in this agents inventory</param>
    /// <param name="simulator">Simulator Object</param>
    /// <remarks>Raises the <see cref="OnTaskItemReceived" /> event</remarks>
    public void MoveTaskInventory(uint objectLocalID, UUID taskItemID, UUID inventoryFolderID, Simulator simulator)
    {
        var request = new MoveTaskInventoryPacket();
        request.AgentData.AgentID = Client.Self.AgentID;
        request.AgentData.SessionID = Client.Self.SessionID;

        request.AgentData.FolderID = inventoryFolderID;

        request.InventoryData.ItemID = taskItemID;
        request.InventoryData.LocalID = objectLocalID;

        Client.Network.SendPacket(request, simulator);
    }

    /// <summary>
    ///     Remove an item from an objects (Prim) Inventory
    /// </summary>
    /// <param name="objectLocalID">LocalID of the object in the simulator</param>
    /// <param name="taskItemID">UUID of the task item to remove</param>
    /// <param name="simulator">Simulator Object</param>
    /// <remarks>
    ///     You can confirm the removal by comparing the tasks inventory serial before and after the
    ///     request with the <see cref="RequestTaskInventory" /> request combined with
    ///     the <seealso cref="TaskInventoryReply" /> event
    /// </remarks>
    public void RemoveTaskInventory(uint objectLocalID, UUID taskItemID, Simulator simulator)
    {
        var remove = new RemoveTaskInventoryPacket();
        remove.AgentData.AgentID = Client.Self.AgentID;
        remove.AgentData.SessionID = Client.Self.SessionID;

        remove.InventoryData.ItemID = taskItemID;
        remove.InventoryData.LocalID = objectLocalID;

        Client.Network.SendPacket(remove, simulator);
    }

    /// <summary>
    ///     Copy an InventoryScript item from the Agents Inventory into a primitives task inventory
    /// </summary>
    /// <param name="objectLocalID">An unsigned integer representing a primitive being simulated</param>
    /// <param name="item">An <seealso cref="InventoryItem" /> which represents a script object from the agents inventory</param>
    /// <param name="enableScript">true to set the scripts running state to enabled</param>
    /// <returns>A Unique Transaction ID</returns>
    /// <example>
    ///     The following example shows the basic steps necessary to copy a script from the agents inventory into a tasks
    ///     inventory
    ///     and assumes the script exists in the agents inventory.
    ///     <code>
    ///    uint primID = 95899503; // Fake prim ID
    ///    UUID scriptID = UUID.Parse("92a7fe8a-e949-dd39-a8d8-1681d8673232"); // Fake Script UUID in Inventory
    /// 
    ///    Client.Inventory.FolderContents(Client.Inventory.FindFolderForType(AssetType.LSLText), Client.Self.AgentID, 
    ///        false, true, InventorySortOrder.ByName, 10000);
    /// 
    ///    Client.Inventory.RezScript(primID, (InventoryItem)Client.Inventory.Store[scriptID]);
    /// </code>
    /// </example>
    // DocTODO: what does the return UUID correlate to if anything?
    public UUID CopyScriptToTask(uint objectLocalID, InventoryItem item, bool enableScript)
    {
        var transactionID = UUID.Random();

        var ScriptPacket = new RezScriptPacket();
        ScriptPacket.AgentData.AgentID = Client.Self.AgentID;
        ScriptPacket.AgentData.SessionID = Client.Self.SessionID;

        ScriptPacket.UpdateBlock.ObjectLocalID = objectLocalID;
        ScriptPacket.UpdateBlock.Enabled = enableScript;

        ScriptPacket.InventoryBlock.ItemID = item.UUID;
        ScriptPacket.InventoryBlock.FolderID = item.ParentUUID;
        ScriptPacket.InventoryBlock.CreatorID = item.CreatorID;
        ScriptPacket.InventoryBlock.OwnerID = item.OwnerID;
        ScriptPacket.InventoryBlock.GroupID = item.GroupID;
        ScriptPacket.InventoryBlock.BaseMask = (uint)item.Permissions.BaseMask;
        ScriptPacket.InventoryBlock.OwnerMask = (uint)item.Permissions.OwnerMask;
        ScriptPacket.InventoryBlock.GroupMask = (uint)item.Permissions.GroupMask;
        ScriptPacket.InventoryBlock.EveryoneMask = (uint)item.Permissions.EveryoneMask;
        ScriptPacket.InventoryBlock.NextOwnerMask = (uint)item.Permissions.NextOwnerMask;
        ScriptPacket.InventoryBlock.GroupOwned = item.GroupOwned;
        ScriptPacket.InventoryBlock.TransactionID = transactionID;
        ScriptPacket.InventoryBlock.Type = (sbyte)item.AssetType;
        ScriptPacket.InventoryBlock.InvType = (sbyte)item.InventoryType;
        ScriptPacket.InventoryBlock.Flags = item.Flags;
        ScriptPacket.InventoryBlock.SaleType = (byte)item.SaleType;
        ScriptPacket.InventoryBlock.SalePrice = item.SalePrice;
        ScriptPacket.InventoryBlock.Name = Utils.StringToBytes(item.Name);
        ScriptPacket.InventoryBlock.Description = Utils.StringToBytes(item.Description);
        ScriptPacket.InventoryBlock.CreationDate = (int)Utils.DateTimeToUnixTime(item.CreationDate);
        ScriptPacket.InventoryBlock.CRC = ItemCRC(item);

        Client.Network.SendPacket(ScriptPacket);

        return transactionID;
    }


    /// <summary>
    ///     Request the running status of a script contained in a task (primitive) inventory
    /// </summary>
    /// <param name="objectID">The ID of the primitive containing the script</param>
    /// <param name="scriptID">The ID of the script</param>
    /// <remarks>
    ///     The <see cref="ScriptRunningReply" /> event can be used to obtain the results of the
    ///     request
    /// </remarks>
    /// <seealso cref="ScriptRunningReply" />
    public void RequestGetScriptRunning(UUID objectID, UUID scriptID)
    {
        var request = new GetScriptRunningPacket();
        request.Script.ObjectID = objectID;
        request.Script.ItemID = scriptID;

        Client.Network.SendPacket(request);
    }

    /// <summary>
    ///     Send a request to set the running state of a script contained in a task (primitive) inventory
    /// </summary>
    /// <param name="objectID">The ID of the primitive containing the script</param>
    /// <param name="scriptID">The ID of the script</param>
    /// <param name="running">true to set the script running, false to stop a running script</param>
    /// <remarks>
    ///     To verify the change you can use the <see cref="RequestGetScriptRunning" /> method combined
    ///     with the <see cref="ScriptRunningReply" /> event
    /// </remarks>
    public void RequestSetScriptRunning(UUID objectID, UUID scriptID, bool running)
    {
        var request = new SetScriptRunningPacket();
        request.AgentData.AgentID = Client.Self.AgentID;
        request.AgentData.SessionID = Client.Self.SessionID;
        request.Script.Running = running;
        request.Script.ItemID = scriptID;
        request.Script.ObjectID = objectID;

        Client.Network.SendPacket(request);
    }

    #endregion Task

    #region Helper Functions

    private uint RegisterItemCreatedCallback(ItemCreatedCallback callback)
    {
        lock (_CallbacksLock)
        {
            if (_CallbackPos == uint.MaxValue)
                _CallbackPos = 0;

            _CallbackPos++;

            if (_ItemCreatedCallbacks.ContainsKey(_CallbackPos))
                Logger.Log("Overwriting an existing ItemCreatedCallback", Helpers.LogLevel.Warning, Client);

            _ItemCreatedCallbacks[_CallbackPos] = callback;

            return _CallbackPos;
        }
    }

    private uint RegisterItemsCopiedCallback(ItemCopiedCallback callback)
    {
        lock (_CallbacksLock)
        {
            if (_CallbackPos == uint.MaxValue)
                _CallbackPos = 0;

            _CallbackPos++;

            if (_ItemCopiedCallbacks.ContainsKey(_CallbackPos))
                Logger.Log("Overwriting an existing ItemsCopiedCallback", Helpers.LogLevel.Warning, Client);

            _ItemCopiedCallbacks[_CallbackPos] = callback;

            return _CallbackPos;
        }
    }

    /// <summary>
    ///     Create a CRC from an InventoryItem
    /// </summary>
    /// <param name="iitem">The source InventoryItem</param>
    /// <returns>A uint representing the source InventoryItem as a CRC</returns>
    public static uint ItemCRC(InventoryItem iitem)
    {
        uint CRC = 0;

        // IDs
        CRC += iitem.AssetUUID.CRC(); // AssetID
        CRC += iitem.ParentUUID.CRC(); // FolderID
        CRC += iitem.UUID.CRC(); // ItemID

        // Permission stuff
        CRC += iitem.CreatorID.CRC(); // CreatorID
        CRC += iitem.OwnerID.CRC(); // OwnerID
        CRC += iitem.GroupID.CRC(); // GroupID

        // CRC += another 4 words which always seem to be zero -- unclear if this is a UUID or what
        CRC += (uint)iitem.Permissions
            .OwnerMask; //owner_mask;      // Either owner_mask or next_owner_mask may need to be
        CRC += (uint)iitem.Permissions
            .NextOwnerMask; //next_owner_mask; // switched with base_mask -- 2 values go here and in my
        CRC += (uint)iitem.Permissions.EveryoneMask; //everyone_mask;   // study item, the three were identical.
        CRC += (uint)iitem.Permissions.GroupMask; //group_mask;

        // The rest of the CRC fields
        CRC += iitem.Flags; // Flags
        CRC += (uint)iitem.InventoryType; // InvType
        CRC += (uint)iitem.AssetType; // Type 
        CRC += Utils.DateTimeToUnixTime(iitem.CreationDate); // CreationDate
        CRC += (uint)iitem.SalePrice; // SalePrice
        CRC += (uint)iitem.SaleType * 0x07073096; // SaleType

        return CRC;
    }

    /// <summary>
    ///     Reverses a cheesy XORing with a fixed UUID to convert a shadow_id to an asset_id
    /// </summary>
    /// <param name="shadowID">Obfuscated shadow_id value</param>
    /// <returns>Deobfuscated asset_id value</returns>
    public static UUID DecryptShadowID(UUID shadowID)
    {
        return shadowID ^ MAGIC_ID;
    }

    /// <summary>
    ///     Does a cheesy XORing with a fixed UUID to convert an asset_id to a shadow_id
    /// </summary>
    /// <param name="assetID">asset_id value to obfuscate</param>
    /// <returns>Obfuscated shadow_id value</returns>
    public static UUID EncryptAssetID(UUID assetID)
    {
        return assetID ^ MAGIC_ID;
    }

    /// <summary>
    ///     Wrapper for creating a new <seealso cref="InventoryItem" /> object
    /// </summary>
    /// <param name="type">The type of item from the <seealso cref="InventoryType" /> enum</param>
    /// <param name="id">The <seealso cref="UUID" /> of the newly created object</param>
    /// <returns>An <seealso cref="InventoryItem" /> object with the type and id passed</returns>
    public static InventoryItem CreateInventoryItem(InventoryType type, UUID id)
    {
        switch (type)
        {
            case InventoryType.Texture: return new InventoryTexture(id);
            case InventoryType.Sound: return new InventorySound(id);
            case InventoryType.CallingCard: return new InventoryCallingCard(id);
            case InventoryType.Landmark: return new InventoryLandmark(id);
            case InventoryType.Object: return new InventoryObject(id);
            case InventoryType.Notecard: return new InventoryNotecard(id);
            case InventoryType.Category: return new InventoryCategory(id);
            case InventoryType.LSL: return new InventoryLSL(id);
            case InventoryType.Snapshot: return new InventorySnapshot(id);
            case InventoryType.Attachment: return new InventoryAttachment(id);
            case InventoryType.Wearable: return new InventoryWearable(id);
            case InventoryType.Animation: return new InventoryAnimation(id);
            case InventoryType.Gesture: return new InventoryGesture(id);
            default: return new InventoryItem(type, id);
        }
    }

    private InventoryItem SafeCreateInventoryItem(InventoryType InvType, UUID ItemID)
    {
        InventoryItem ret = null;

        if (Store.Contains(ItemID))
            ret = Store[ItemID] as InventoryItem;

        if (ret == null)
            ret = CreateInventoryItem(InvType, ItemID);

        return ret;
    }

    private static bool ParseLine(string line, out string key, out string value)
    {
        // Clean up and convert tabs to spaces
        line = line.Trim();
        line = line.Replace('\t', ' ');

        // Shrink all whitespace down to single spaces
        while (line.IndexOf("  ") > 0)
            line = line.Replace("  ", " ");

        if (line.Length > 2)
        {
            var sep = line.IndexOf(' ');
            if (sep > 0)
            {
                key = line.Substring(0, sep);
                value = line.Substring(sep + 1);

                return true;
            }
        }
        else if (line.Length == 1)
        {
            key = line;
            value = string.Empty;
            return true;
        }

        key = null;
        value = null;
        return false;
    }

    /// <summary>
    ///     Parse the results of a RequestTaskInventory() response
    /// </summary>
    /// <param name="taskData">A string which contains the data from the task reply</param>
    /// <returns>A List containing the items contained within the tasks inventory</returns>
    public static List<InventoryBase> ParseTaskInventory(string taskData)
    {
        var items = new List<InventoryBase>();
        var lineNum = 0;
        var lines = taskData.Replace("\r\n", "\n").Split('\n');

        while (lineNum < lines.Length)
        {
            string key, value;
            if (ParseLine(lines[lineNum++], out key, out value))
            {
                if (key == "inv_object")
                {
                    #region inv_object

                    // In practice this appears to only be used for folders
                    var itemID = UUID.Zero;
                    var parentID = UUID.Zero;
                    var name = string.Empty;
                    var assetType = AssetType.Unknown;

                    while (lineNum < lines.Length)
                        if (ParseLine(lines[lineNum++], out key, out value))
                        {
                            if (key == "{")
                                continue;
                            if (key == "}")
                                break;
                            if (key == "obj_id")
                                UUID.TryParse(value, out itemID);
                            else if (key == "parent_id")
                                UUID.TryParse(value, out parentID);
                            else if (key == "type")
                                assetType = Utils.StringToAssetType(value);
                            else if (key == "name") name = value.Substring(0, value.IndexOf('|'));
                        }

                    if (assetType == AssetType.Folder)
                    {
                        var folder = new InventoryFolder(itemID);
                        folder.Name = name;
                        folder.ParentUUID = parentID;

                        items.Add(folder);
                    }
                    else
                    {
                        var item = new InventoryItem(itemID);
                        item.Name = name;
                        item.ParentUUID = parentID;
                        item.AssetType = assetType;

                        items.Add(item);
                    }

                    #endregion inv_object
                }
                else if (key == "inv_item")
                {
                    #region inv_item

                    // Any inventory item that links to an assetID, has permissions, etc
                    var itemID = UUID.Zero;
                    var assetID = UUID.Zero;
                    var parentID = UUID.Zero;
                    var creatorID = UUID.Zero;
                    var ownerID = UUID.Zero;
                    var lastOwnerID = UUID.Zero;
                    var groupID = UUID.Zero;
                    var groupOwned = false;
                    var name = string.Empty;
                    var desc = string.Empty;
                    var assetType = AssetType.Unknown;
                    var inventoryType = InventoryType.Unknown;
                    var creationDate = Utils.Epoch;
                    uint flags = 0;
                    var perms = Permissions.NoPermissions;
                    var saleType = SaleType.Not;
                    var salePrice = 0;

                    while (lineNum < lines.Length)
                        if (ParseLine(lines[lineNum++], out key, out value))
                        {
                            if (key == "{") continue;

                            if (key == "}") break;

                            if (key == "item_id")
                            {
                                UUID.TryParse(value, out itemID);
                            }
                            else if (key == "parent_id")
                            {
                                UUID.TryParse(value, out parentID);
                            }
                            else if (key == "permissions")
                            {
                                #region permissions

                                while (lineNum < lines.Length)
                                    if (ParseLine(lines[lineNum++], out key, out value))
                                    {
                                        if (key == "{") continue;

                                        if (key == "}") break;

                                        if (key == "creator_mask")
                                        {
                                            // Deprecated
                                            uint val;
                                            if (Utils.TryParseHex(value, out val))
                                                perms.BaseMask = (PermissionMask)val;
                                        }
                                        else if (key == "base_mask")
                                        {
                                            uint val;
                                            if (Utils.TryParseHex(value, out val))
                                                perms.BaseMask = (PermissionMask)val;
                                        }
                                        else if (key == "owner_mask")
                                        {
                                            uint val;
                                            if (Utils.TryParseHex(value, out val))
                                                perms.OwnerMask = (PermissionMask)val;
                                        }
                                        else if (key == "group_mask")
                                        {
                                            uint val;
                                            if (Utils.TryParseHex(value, out val))
                                                perms.GroupMask = (PermissionMask)val;
                                        }
                                        else if (key == "everyone_mask")
                                        {
                                            uint val;
                                            if (Utils.TryParseHex(value, out val))
                                                perms.EveryoneMask = (PermissionMask)val;
                                        }
                                        else if (key == "next_owner_mask")
                                        {
                                            uint val;
                                            if (Utils.TryParseHex(value, out val))
                                                perms.NextOwnerMask = (PermissionMask)val;
                                        }
                                        else if (key == "creator_id")
                                        {
                                            UUID.TryParse(value, out creatorID);
                                        }
                                        else if (key == "owner_id")
                                        {
                                            UUID.TryParse(value, out ownerID);
                                        }
                                        else if (key == "last_owner_id")
                                        {
                                            UUID.TryParse(value, out lastOwnerID);
                                        }
                                        else if (key == "group_id")
                                        {
                                            UUID.TryParse(value, out groupID);
                                        }
                                        else if (key == "group_owned")
                                        {
                                            uint val;
                                            if (uint.TryParse(value, out val))
                                                groupOwned = val != 0;
                                        }
                                    }

                                #endregion permissions
                            }
                            else if (key == "sale_info")
                            {
                                #region sale_info

                                while (lineNum < lines.Length)
                                    if (ParseLine(lines[lineNum++], out key, out value))
                                    {
                                        if (key == "{")
                                            continue;
                                        if (key == "}")
                                            break;
                                        if (key == "sale_type")
                                            saleType = Utils.StringToSaleType(value);
                                        else if (key == "sale_price") int.TryParse(value, out salePrice);
                                    }

                                #endregion sale_info
                            }
                            else if (key == "shadow_id")
                            {
                                UUID shadowID;
                                if (UUID.TryParse(value, out shadowID))
                                    assetID = DecryptShadowID(shadowID);
                            }
                            else if (key == "asset_id")
                            {
                                UUID.TryParse(value, out assetID);
                            }
                            else if (key == "type")
                            {
                                assetType = Utils.StringToAssetType(value);
                            }
                            else if (key == "inv_type")
                            {
                                inventoryType = Utils.StringToInventoryType(value);
                            }
                            else if (key == "flags")
                            {
                                uint.TryParse(value, out flags);
                            }
                            else if (key == "name")
                            {
                                name = value.Substring(0, value.IndexOf('|'));
                            }
                            else if (key == "desc")
                            {
                                desc = value.Substring(0, value.IndexOf('|'));
                            }
                            else if (key == "creation_date")
                            {
                                uint timestamp;
                                if (uint.TryParse(value, out timestamp))
                                    creationDate = Utils.UnixTimeToDateTime(timestamp);
                                else
                                    Logger.Log("Failed to parse creation_date " + value, Helpers.LogLevel.Warning);
                            }
                        }

                    var item = CreateInventoryItem(inventoryType, itemID);
                    item.AssetUUID = assetID;
                    item.AssetType = assetType;
                    item.CreationDate = creationDate;
                    item.CreatorID = creatorID;
                    item.Description = desc;
                    item.Flags = flags;
                    item.GroupID = groupID;
                    item.GroupOwned = groupOwned;
                    item.Name = name;
                    item.OwnerID = ownerID;
                    item.LastOwnerID = lastOwnerID;
                    item.ParentUUID = parentID;
                    item.Permissions = perms;
                    item.SalePrice = salePrice;
                    item.SaleType = saleType;

                    items.Add(item);

                    #endregion inv_item
                }
                else
                {
                    Logger.Log("Unrecognized token " + key + " in: " + Environment.NewLine + taskData,
                        Helpers.LogLevel.Error);
                }
            }
        }

        return items;
    }

    #endregion Helper Functions

    #region Internal Callbacks

    private void Self_IM(object sender, InstantMessageEventArgs e)
    {
        // TODO: MainAvatar.InstantMessageDialog.GroupNotice can also be an inventory offer, should we
        // handle it here?

        if (m_InventoryObjectOffered != null &&
            (e.IM.Dialog == InstantMessageDialog.InventoryOffered
             || e.IM.Dialog == InstantMessageDialog.TaskInventoryOffered))
        {
            var type = AssetType.Unknown;
            var objectID = UUID.Zero;
            var fromTask = false;

            if (e.IM.Dialog == InstantMessageDialog.InventoryOffered)
            {
                if (e.IM.BinaryBucket.Length == 17)
                {
                    type = (AssetType)e.IM.BinaryBucket[0];
                    objectID = new UUID(e.IM.BinaryBucket, 1);
                    fromTask = false;
                }
                else
                {
                    Logger.Log("Malformed inventory offer from agent", Helpers.LogLevel.Warning, Client);
                    return;
                }
            }
            else if (e.IM.Dialog == InstantMessageDialog.TaskInventoryOffered)
            {
                if (e.IM.BinaryBucket.Length == 1)
                {
                    type = (AssetType)e.IM.BinaryBucket[0];
                    fromTask = true;
                }
                else
                {
                    Logger.Log("Malformed inventory offer from object", Helpers.LogLevel.Warning, Client);
                    return;
                }
            }

            // Find the folder where this is going to go
            var destinationFolderID = FindFolderForType(type);

            // Fire the callback
            try
            {
                var imp = new ImprovedInstantMessagePacket();
                imp.AgentData.AgentID = Client.Self.AgentID;
                imp.AgentData.SessionID = Client.Self.SessionID;
                imp.MessageBlock.FromGroup = false;
                imp.MessageBlock.ToAgentID = e.IM.FromAgentID;
                imp.MessageBlock.Offline = 0;
                imp.MessageBlock.ID = e.IM.IMSessionID;
                imp.MessageBlock.Timestamp = 0;
                imp.MessageBlock.FromAgentName = Utils.StringToBytes(Client.Self.Name);
                imp.MessageBlock.Message = Utils.EmptyBytes;
                imp.MessageBlock.ParentEstateID = 0;
                imp.MessageBlock.RegionID = UUID.Zero;
                imp.MessageBlock.Position = Client.Self.SimPosition;

                var args = new InventoryObjectOfferedEventArgs(e.IM, type, objectID, fromTask, destinationFolderID);

                OnInventoryObjectOffered(args);

                if (args.Accept)
                {
                    // Accept the inventory offer
                    switch (e.IM.Dialog)
                    {
                        case InstantMessageDialog.InventoryOffered:
                            imp.MessageBlock.Dialog = (byte)InstantMessageDialog.InventoryAccepted;
                            break;
                        case InstantMessageDialog.TaskInventoryOffered:
                            imp.MessageBlock.Dialog = (byte)InstantMessageDialog.TaskInventoryAccepted;
                            break;
                        case InstantMessageDialog.GroupNotice:
                            imp.MessageBlock.Dialog = (byte)InstantMessageDialog.GroupNoticeInventoryAccepted;
                            break;
                    }

                    imp.MessageBlock.BinaryBucket = args.FolderID.GetBytes();
                }
                else
                {
                    // Decline the inventory offer
                    switch (e.IM.Dialog)
                    {
                        case InstantMessageDialog.InventoryOffered:
                            imp.MessageBlock.Dialog = (byte)InstantMessageDialog.InventoryDeclined;
                            break;
                        case InstantMessageDialog.TaskInventoryOffered:
                            imp.MessageBlock.Dialog = (byte)InstantMessageDialog.TaskInventoryDeclined;
                            break;
                        case InstantMessageDialog.GroupNotice:
                            imp.MessageBlock.Dialog = (byte)InstantMessageDialog.GroupNoticeInventoryDeclined;
                            break;
                    }

                    imp.MessageBlock.BinaryBucket = Utils.EmptyBytes;
                }

                Client.Network.SendPacket(imp, e.Simulator);
            }
            catch (Exception ex)
            {
                Logger.Log(ex.Message, Helpers.LogLevel.Error, Client, ex);
            }
        }
    }

    private void CreateItemFromAssetResponse(CapsClient client, OSD result, Exception error)
    {
        var args = (object[])client.UserData;
        var callback = (ItemCreatedFromAssetCallback)args[0];
        var itemData = (byte[])args[1];
        var millisecondsTimeout = (int)args[2];
        var request = (OSDMap)args[3];

        if (result == null)
        {
            try
            {
                callback(false, error.Message, UUID.Zero, UUID.Zero);
            }
            catch (Exception e)
            {
                Logger.Log(e.Message, Helpers.LogLevel.Error, Client, e);
            }

            return;
        }

        if (result.Type == OSDType.Unknown)
            try
            {
                callback(false, "Failed to parse asset and item UUIDs", UUID.Zero, UUID.Zero);
            }
            catch (Exception e)
            {
                Logger.Log(e.Message, Helpers.LogLevel.Error, Client, e);
            }

        var contents = (OSDMap)result;

        var status = contents["state"].AsString().ToLower();

        if (status == "upload")
        {
            var uploadURL = contents["uploader"].AsString();

            Logger.DebugLog("CreateItemFromAsset: uploading to " + uploadURL);

            // This makes the assumption that all uploads go to CurrentSim, to avoid
            // the problem of HttpRequestState not knowing anything about simulators
            var upload = new CapsClient(new Uri(uploadURL));
            upload.OnComplete += CreateItemFromAssetResponse;
            upload.UserData = new object[] { callback, itemData, millisecondsTimeout, request };
            upload.BeginGetResponse(itemData, "application/octet-stream", millisecondsTimeout);
        }
        else if (status == "complete")
        {
            Logger.DebugLog("CreateItemFromAsset: completed");

            if (contents.ContainsKey("new_inventory_item") && contents.ContainsKey("new_asset"))
            {
                // Request full update on the item in order to update the local store
                RequestFetchInventory(contents["new_inventory_item"].AsUUID(), Client.Self.AgentID);

                try
                {
                    callback(true, string.Empty, contents["new_inventory_item"].AsUUID(),
                        contents["new_asset"].AsUUID());
                }
                catch (Exception e)
                {
                    Logger.Log(e.Message, Helpers.LogLevel.Error, Client, e);
                }
            }
            else
            {
                try
                {
                    callback(false, "Failed to parse asset and item UUIDs", UUID.Zero, UUID.Zero);
                }
                catch (Exception e)
                {
                    Logger.Log(e.Message, Helpers.LogLevel.Error, Client, e);
                }
            }
        }
        else
        {
            // Failure
            try
            {
                callback(false, status, UUID.Zero, UUID.Zero);
            }
            catch (Exception e)
            {
                Logger.Log(e.Message, Helpers.LogLevel.Error, Client, e);
            }
        }
    }


    private void Network_OnLoginResponse(bool loginSuccess, bool redirect, string message, string reason,
        LoginResponseData replyData)
    {
        if (loginSuccess)
        {
            // Initialize the store here so we know who owns it:
            Store = new Inventory(Client, this, Client.Self.AgentID);
            Logger.DebugLog("Setting InventoryRoot to " + replyData.InventoryRoot, Client);
            var rootFolder = new InventoryFolder(replyData.InventoryRoot);
            rootFolder.Name = string.Empty;
            rootFolder.ParentUUID = UUID.Zero;
            Store.RootFolder = rootFolder;

            for (var i = 0; i < replyData.InventorySkeleton.Length; i++)
                Store.UpdateNodeFor(replyData.InventorySkeleton[i]);

            var libraryRootFolder = new InventoryFolder(replyData.LibraryRoot);
            libraryRootFolder.Name = string.Empty;
            libraryRootFolder.ParentUUID = UUID.Zero;
            Store.LibraryFolder = libraryRootFolder;

            for (var i = 0; i < replyData.LibrarySkeleton.Length; i++)
                Store.UpdateNodeFor(replyData.LibrarySkeleton[i]);
        }
    }

    private void UploadInventoryAssetResponse(CapsClient client, OSD result, Exception error)
    {
        var contents = result as OSDMap;
        var kvp = (KeyValuePair<InventoryUploadedAssetCallback, byte[]>)((object[])client.UserData)[0];
        var callback = kvp.Key;
        var itemData = kvp.Value;

        if (error == null && contents != null)
        {
            var status = contents["state"].AsString();

            if (status == "upload")
            {
                var uploadURL = contents["uploader"].AsUri();

                if (uploadURL != null)
                {
                    // This makes the assumption that all uploads go to CurrentSim, to avoid
                    // the problem of HttpRequestState not knowing anything about simulators
                    var upload = new CapsClient(uploadURL);
                    upload.OnComplete += UploadInventoryAssetResponse;
                    upload.UserData = new object[2] { kvp, (UUID)((object[])client.UserData)[1] };
                    upload.BeginGetResponse(itemData, "application/octet-stream", Client.Settings.CAPS_TIMEOUT);
                }
                else
                {
                    try
                    {
                        callback(false, "Missing uploader URL", UUID.Zero, UUID.Zero);
                    }
                    catch (Exception e)
                    {
                        Logger.Log(e.Message, Helpers.LogLevel.Error, Client, e);
                    }
                }
            }
            else if (status == "complete")
            {
                if (contents.ContainsKey("new_asset"))
                {
                    // Request full item update so we keep store in sync
                    RequestFetchInventory((UUID)((object[])client.UserData)[1], contents["new_asset"].AsUUID());

                    try
                    {
                        callback(true, string.Empty, (UUID)((object[])client.UserData)[1],
                            contents["new_asset"].AsUUID());
                    }
                    catch (Exception e)
                    {
                        Logger.Log(e.Message, Helpers.LogLevel.Error, Client, e);
                    }
                }
                else
                {
                    try
                    {
                        callback(false, "Failed to parse asset and item UUIDs", UUID.Zero, UUID.Zero);
                    }
                    catch (Exception e)
                    {
                        Logger.Log(e.Message, Helpers.LogLevel.Error, Client, e);
                    }
                }
            }
            else
            {
                try
                {
                    callback(false, status, UUID.Zero, UUID.Zero);
                }
                catch (Exception e)
                {
                    Logger.Log(e.Message, Helpers.LogLevel.Error, Client, e);
                }
            }
        }
        else
        {
            var message = "Unrecognized or empty response";

            if (error != null)
            {
                if (error is WebException)
                    message = ((HttpWebResponse)((WebException)error).Response).StatusDescription;

                if (message == null || message == "None")
                    message = error.Message;
            }

            try
            {
                callback(false, message, UUID.Zero, UUID.Zero);
            }
            catch (Exception e)
            {
                Logger.Log(e.Message, Helpers.LogLevel.Error, Client, e);
            }
        }
    }

    private void UpdateScriptAgentInventoryResponse(CapsClient client, OSD result, Exception error)
    {
        var kvp = (KeyValuePair<ScriptUpdatedCallback, byte[]>)((object[])client.UserData)[0];
        var callback = kvp.Key;
        var itemData = kvp.Value;

        if (result == null)
        {
            try
            {
                callback(false, error.Message, false, null, UUID.Zero, UUID.Zero);
            }
            catch (Exception e)
            {
                Logger.Log(e.Message, Helpers.LogLevel.Error, Client, e);
            }

            return;
        }

        var contents = (OSDMap)result;

        var status = contents["state"].AsString();
        if (status == "upload")
        {
            var uploadURL = contents["uploader"].AsString();

            var upload = new CapsClient(new Uri(uploadURL));
            upload.OnComplete += UpdateScriptAgentInventoryResponse;
            upload.UserData = new object[2] { kvp, (UUID)((object[])client.UserData)[1] };
            upload.BeginGetResponse(itemData, "application/octet-stream", Client.Settings.CAPS_TIMEOUT);
        }
        else if (status == "complete" && callback != null)
        {
            if (contents.ContainsKey("new_asset"))
            {
                // Request full item update so we keep store in sync
                RequestFetchInventory((UUID)((object[])client.UserData)[1], contents["new_asset"].AsUUID());


                try
                {
                    List<string> compileErrors = null;

                    if (contents.ContainsKey("errors"))
                    {
                        var errors = (OSDArray)contents["errors"];
                        compileErrors = new List<string>(errors.Count);

                        for (var i = 0; i < errors.Count; i++) compileErrors.Add(errors[i].AsString());
                    }

                    callback(true,
                        status,
                        contents["compiled"].AsBoolean(),
                        compileErrors,
                        (UUID)((object[])client.UserData)[1],
                        contents["new_asset"].AsUUID());
                }
                catch (Exception e)
                {
                    Logger.Log(e.Message, Helpers.LogLevel.Error, Client, e);
                }
            }
            else
            {
                try
                {
                    callback(false, "Failed to parse asset UUID", false, null, UUID.Zero, UUID.Zero);
                }
                catch (Exception e)
                {
                    Logger.Log(e.Message, Helpers.LogLevel.Error, Client, e);
                }
            }
        }
        else if (callback != null)
        {
            try
            {
                callback(false, status, false, null, UUID.Zero, UUID.Zero);
            }
            catch (Exception e)
            {
                Logger.Log(e.Message, Helpers.LogLevel.Error, Client, e);
            }
        }
    }

    #endregion Internal Handlers

    #region Packet Handlers

    /// <summary>Process an incoming packet and raise the appropriate events</summary>
    /// <param name="sender">The sender</param>
    /// <param name="e">The EventArgs object containing the packet data</param>
    protected void SaveAssetIntoInventoryHandler(object sender, PacketReceivedEventArgs e)
    {
        if (m_SaveAssetToInventory != null)
        {
            var packet = e.Packet;

            var save = (SaveAssetIntoInventoryPacket)packet;
            OnSaveAssetToInventory(
                new SaveAssetToInventoryEventArgs(save.InventoryData.ItemID, save.InventoryData.NewAssetID));
        }
    }

    /// <summary>Process an incoming packet and raise the appropriate events</summary>
    /// <param name="sender">The sender</param>
    /// <param name="e">The EventArgs object containing the packet data</param>
    protected void InventoryDescendentsHandler(object sender, PacketReceivedEventArgs e)
    {
        var packet = e.Packet;

        var reply = (InventoryDescendentsPacket)packet;

        if (reply.AgentData.Descendents > 0)
        {
            // InventoryDescendantsReply sends a null folder if the parent doesnt contain any folders
            if (reply.FolderData[0].FolderID != UUID.Zero)
                // Iterate folders in this packet
                for (var i = 0; i < reply.FolderData.Length; i++)
                    // If folder already exists then ignore, we assume the version cache
                    // logic is working and if the folder is stale then it should not be present.
                    if (!Store.Contains(reply.FolderData[i].FolderID))
                    {
                        var folder = new InventoryFolder(reply.FolderData[i].FolderID);
                        folder.ParentUUID = reply.FolderData[i].ParentID;
                        folder.Name = Utils.BytesToString(reply.FolderData[i].Name);
                        folder.PreferredType = (FolderType)reply.FolderData[i].Type;
                        folder.OwnerID = reply.AgentData.OwnerID;

                        Store[folder.UUID] = folder;
                    }

            // InventoryDescendantsReply sends a null item if the parent doesnt contain any items.
            if (reply.ItemData[0].ItemID != UUID.Zero)
                // Iterate items in this packet
                for (var i = 0; i < reply.ItemData.Length; i++)
                    if (reply.ItemData[i].ItemID != UUID.Zero)
                    {
                        InventoryItem item;
                        /* 
                         * Objects that have been attached in-world prior to being stored on the 
                         * asset server are stored with the InventoryType of 0 (Texture) 
                         * instead of 17 (Attachment) 
                         * 
                         * This corrects that behavior by forcing Object Asset types that have an 
                         * invalid InventoryType with the proper InventoryType of Attachment.
                         */
                        if ((AssetType)reply.ItemData[i].Type == AssetType.Object
                            && (InventoryType)reply.ItemData[i].InvType == InventoryType.Texture)
                        {
                            item = CreateInventoryItem(InventoryType.Attachment, reply.ItemData[i].ItemID);
                            item.InventoryType = InventoryType.Attachment;
                        }
                        else
                        {
                            item = CreateInventoryItem((InventoryType)reply.ItemData[i].InvType,
                                reply.ItemData[i].ItemID);
                            item.InventoryType = (InventoryType)reply.ItemData[i].InvType;
                        }

                        item.ParentUUID = reply.ItemData[i].FolderID;
                        item.CreatorID = reply.ItemData[i].CreatorID;
                        item.AssetType = (AssetType)reply.ItemData[i].Type;
                        item.AssetUUID = reply.ItemData[i].AssetID;
                        item.CreationDate = Utils.UnixTimeToDateTime((uint)reply.ItemData[i].CreationDate);
                        item.Description = Utils.BytesToString(reply.ItemData[i].Description);
                        item.Flags = reply.ItemData[i].Flags;
                        item.Name = Utils.BytesToString(reply.ItemData[i].Name);
                        item.GroupID = reply.ItemData[i].GroupID;
                        item.GroupOwned = reply.ItemData[i].GroupOwned;
                        item.Permissions = new Permissions(
                            reply.ItemData[i].BaseMask,
                            reply.ItemData[i].EveryoneMask,
                            reply.ItemData[i].GroupMask,
                            reply.ItemData[i].NextOwnerMask,
                            reply.ItemData[i].OwnerMask);
                        item.SalePrice = reply.ItemData[i].SalePrice;
                        item.SaleType = (SaleType)reply.ItemData[i].SaleType;
                        item.OwnerID = reply.AgentData.OwnerID;

                        Store[item.UUID] = item;
                    }
        }

        InventoryFolder parentFolder = null;

        if (Store.Contains(reply.AgentData.FolderID) &&
            Store[reply.AgentData.FolderID] is InventoryFolder)
        {
            parentFolder = Store[reply.AgentData.FolderID] as InventoryFolder;
        }
        else
        {
            Logger.Log("Don't have a reference to FolderID " + reply.AgentData.FolderID +
                       " or it is not a folder", Helpers.LogLevel.Error, Client);
            return;
        }

        if (reply.AgentData.Version < parentFolder.Version)
        {
            Logger.Log("Got an outdated InventoryDescendents packet for folder " + parentFolder.Name +
                       ", this version = " + reply.AgentData.Version + ", latest version = " + parentFolder.Version,
                Helpers.LogLevel.Warning, Client);
            return;
        }

        parentFolder.Version = reply.AgentData.Version;
        // FIXME: reply.AgentData.Descendants is not parentFolder.DescendentCount if we didn't 
        // request items and folders
        parentFolder.DescendentCount = reply.AgentData.Descendents;
        Store.GetNodeFor(reply.AgentData.FolderID).NeedsUpdate = false;

        #region FindObjectsByPath Handling

        if (_Searches.Count > 0)
            lock (_Searches)
            {
                StartSearch:

                // Iterate over all of the outstanding searches
                for (var i = 0; i < _Searches.Count; i++)
                {
                    var search = _Searches[i];
                    var folderContents = Store.GetContents(search.Folder);

                    // Iterate over all of the inventory objects in the base search folder
                    for (var j = 0; j < folderContents.Count; j++)
                        // Check if this inventory object matches the current path node
                        if (folderContents[j].Name == search.Path[search.Level])
                        {
                            if (search.Level == search.Path.Length - 1)
                            {
                                Logger.DebugLog("Finished path search of " + string.Join("/", search.Path), Client);

                                // This is the last node in the path, fire the callback and clean up
                                if (m_FindObjectByPathReply != null)
                                    OnFindObjectByPathReply(new FindObjectByPathReplyEventArgs(
                                        string.Join("/", search.Path),
                                        folderContents[j].UUID));

                                // Remove this entry and restart the loop since we are changing the collection size
                                _Searches.RemoveAt(i);
                                goto StartSearch;
                            }

                            // We found a match but it is not the end of the path, request the next level
                            Logger.DebugLog(string.Format("Matched level {0}/{1} in a path search of {2}",
                                search.Level, search.Path.Length - 1, string.Join("/", search.Path)), Client);

                            search.Folder = folderContents[j].UUID;
                            search.Level++;
                            _Searches[i] = search;

                            RequestFolderContents(search.Folder, search.Owner, true, true,
                                InventorySortOrder.ByName);
                        }
                }
            }

        #endregion FindObjectsByPath Handling

        // Callback for inventory folder contents being updated
        OnFolderUpdated(new FolderUpdatedEventArgs(parentFolder.UUID, true));
    }

    /// <summary>
    ///     UpdateCreateInventoryItem packets are received when a new inventory item
    ///     is created. This may occur when an object that's rezzed in world is
    ///     taken into inventory, when an item is created using the CreateInventoryItem
    ///     packet, or when an object is purchased
    /// </summary>
    /// <param name="sender">The sender</param>
    /// <param name="e">The EventArgs object containing the packet data</param>
    protected void UpdateCreateInventoryItemHandler(object sender, PacketReceivedEventArgs e)
    {
        var packet = e.Packet;

        var reply = packet as UpdateCreateInventoryItemPacket;

        foreach (var dataBlock in reply.InventoryData)
        {
            if (dataBlock.InvType == (sbyte)InventoryType.Folder)
            {
                Logger.Log("Received InventoryFolder in an UpdateCreateInventoryItem packet, this should not happen!",
                    Helpers.LogLevel.Error, Client);
                continue;
            }

            var item = CreateInventoryItem((InventoryType)dataBlock.InvType, dataBlock.ItemID);
            item.AssetType = (AssetType)dataBlock.Type;
            item.AssetUUID = dataBlock.AssetID;
            item.CreationDate = Utils.UnixTimeToDateTime(dataBlock.CreationDate);
            item.CreatorID = dataBlock.CreatorID;
            item.Description = Utils.BytesToString(dataBlock.Description);
            item.Flags = dataBlock.Flags;
            item.GroupID = dataBlock.GroupID;
            item.GroupOwned = dataBlock.GroupOwned;
            item.Name = Utils.BytesToString(dataBlock.Name);
            item.OwnerID = dataBlock.OwnerID;
            item.ParentUUID = dataBlock.FolderID;
            item.Permissions = new Permissions(
                dataBlock.BaseMask,
                dataBlock.EveryoneMask,
                dataBlock.GroupMask,
                dataBlock.NextOwnerMask,
                dataBlock.OwnerMask);
            item.SalePrice = dataBlock.SalePrice;
            item.SaleType = (SaleType)dataBlock.SaleType;

            /* 
             * When attaching new objects, an UpdateCreateInventoryItem packet will be
             * returned by the server that has a FolderID/ParentUUID of zero. It is up
             * to the client to make sure that the item gets a good folder, otherwise
             * it will end up inaccesible in inventory.
             */
            if (item.ParentUUID.IsZero())
            {
                // assign default folder for type
                item.ParentUUID = FindFolderForType(item.AssetType);

                Logger.Log(
                    "Received an item through UpdateCreateInventoryItem with no parent folder, assigning to folder " +
                    item.ParentUUID, Helpers.LogLevel.Info);

                // send update to the sim
                RequestUpdateItem(item);
            }

            // Update the local copy
            Store[item.UUID] = item;

            // Look for an "item created" callback
            ItemCreatedCallback createdCallback;
            if (_ItemCreatedCallbacks.TryGetValue(dataBlock.CallbackID, out createdCallback))
            {
                _ItemCreatedCallbacks.Remove(dataBlock.CallbackID);

                try
                {
                    createdCallback(true, item);
                }
                catch (Exception ex)
                {
                    Logger.Log(ex.Message, Helpers.LogLevel.Error, Client, ex);
                }
            }

            // TODO: Is this callback even triggered when items are copied?
            // Look for an "item copied" callback
            ItemCopiedCallback copyCallback;
            if (_ItemCopiedCallbacks.TryGetValue(dataBlock.CallbackID, out copyCallback))
            {
                _ItemCopiedCallbacks.Remove(dataBlock.CallbackID);

                try
                {
                    copyCallback(item);
                }
                catch (Exception ex)
                {
                    Logger.Log(ex.Message, Helpers.LogLevel.Error, Client, ex);
                }
            }

            //This is triggered when an item is received from a task
            if (m_TaskItemReceived != null)
                OnTaskItemReceived(new TaskItemReceivedEventArgs(item.UUID, dataBlock.FolderID,
                    item.CreatorID, item.AssetUUID, item.InventoryType));
        }
    }

    /// <summary>Process an incoming packet and raise the appropriate events</summary>
    /// <param name="sender">The sender</param>
    /// <param name="e">The EventArgs object containing the packet data</param>
    protected void MoveInventoryItemHandler(object sender, PacketReceivedEventArgs e)
    {
        var packet = e.Packet;

        var move = (MoveInventoryItemPacket)packet;

        for (var i = 0; i < move.InventoryData.Length; i++)
        {
            // FIXME: Do something here
            var newName = Utils.BytesToString(move.InventoryData[i].NewName);

            Logger.Log(string.Format(
                "MoveInventoryItemHandler: Item {0} is moving to Folder {1} with new name \"{2}\". Someone write this function!",
                move.InventoryData[i].ItemID.ToString(), move.InventoryData[i].FolderID.ToString(),
                newName), Helpers.LogLevel.Warning, Client);
        }
    }

    protected void BulkUpdateInventoryCapHandler(string capsKey, IMessage message, Simulator simulator)
    {
        var msg = (BulkUpdateInventoryMessage)message;

        foreach (var newFolder in msg.FolderData)
        {
            if (newFolder.FolderID.IsZero()) continue;

            InventoryFolder folder;
            if (!Store.Contains(newFolder.FolderID))
                folder = new InventoryFolder(newFolder.FolderID);
            else
                folder = (InventoryFolder)Store[newFolder.FolderID];

            folder.Name = newFolder.Name;
            folder.ParentUUID = newFolder.ParentID;
            folder.PreferredType = newFolder.Type;
            Store[folder.UUID] = folder;
        }

        foreach (var newItem in msg.ItemData)
        {
            if (newItem.ItemID.IsZero()) continue;
            var invType = newItem.InvType;

            lock (_ItemInventoryTypeRequest)
            {
                InventoryType storedType = 0;
                if (_ItemInventoryTypeRequest.TryGetValue(newItem.CallbackID, out storedType))
                {
                    _ItemInventoryTypeRequest.Remove(newItem.CallbackID);
                    invType = storedType;
                }
            }

            var item = SafeCreateInventoryItem(invType, newItem.ItemID);

            item.AssetType = newItem.Type;
            item.AssetUUID = newItem.AssetID;
            item.CreationDate = newItem.CreationDate;
            item.CreatorID = newItem.CreatorID;
            item.Description = newItem.Description;
            item.Flags = newItem.Flags;
            item.GroupID = newItem.GroupID;
            item.GroupOwned = newItem.GroupOwned;
            item.Name = newItem.Name;
            item.OwnerID = newItem.OwnerID;
            item.ParentUUID = newItem.FolderID;
            item.Permissions.BaseMask = newItem.BaseMask;
            item.Permissions.EveryoneMask = newItem.EveryoneMask;
            item.Permissions.GroupMask = newItem.GroupMask;
            item.Permissions.NextOwnerMask = newItem.NextOwnerMask;
            item.Permissions.OwnerMask = newItem.OwnerMask;
            item.SalePrice = newItem.SalePrice;
            item.SaleType = newItem.SaleType;

            Store[item.UUID] = item;

            // Look for an "item created" callback
            ItemCreatedCallback callback;
            if (_ItemCreatedCallbacks.TryGetValue(newItem.CallbackID, out callback))
            {
                _ItemCreatedCallbacks.Remove(newItem.CallbackID);

                try
                {
                    callback(true, item);
                }
                catch (Exception ex)
                {
                    Logger.Log(ex.Message, Helpers.LogLevel.Error, Client, ex);
                }
            }

            // Look for an "item copied" callback
            ItemCopiedCallback copyCallback;
            if (_ItemCopiedCallbacks.TryGetValue(newItem.CallbackID, out copyCallback))
            {
                _ItemCopiedCallbacks.Remove(newItem.CallbackID);

                try
                {
                    copyCallback(item);
                }
                catch (Exception ex)
                {
                    Logger.Log(ex.Message, Helpers.LogLevel.Error, Client, ex);
                }
            }
        }
    }

    /// <summary>Process an incoming packet and raise the appropriate events</summary>
    /// <param name="sender">The sender</param>
    /// <param name="e">The EventArgs object containing the packet data</param>
    protected void BulkUpdateInventoryHandler(object sender, PacketReceivedEventArgs e)
    {
        var packet = e.Packet;

        var update = packet as BulkUpdateInventoryPacket;

        if (update.FolderData.Length > 0 && update.FolderData[0].FolderID != UUID.Zero)
            foreach (var dataBlock in update.FolderData)
            {
                InventoryFolder folder;
                if (!Store.Contains(dataBlock.FolderID))
                    folder = new InventoryFolder(dataBlock.FolderID);
                else
                    folder = (InventoryFolder)Store[dataBlock.FolderID];

                if (dataBlock.Name != null) folder.Name = Utils.BytesToString(dataBlock.Name);
                folder.OwnerID = update.AgentData.AgentID;
                folder.ParentUUID = dataBlock.ParentID;
                Store[folder.UUID] = folder;
            }

        if (update.ItemData.Length > 0 && update.ItemData[0].ItemID != UUID.Zero)
            for (var i = 0; i < update.ItemData.Length; i++)
            {
                var dataBlock = update.ItemData[i];

                var item = SafeCreateInventoryItem((InventoryType)dataBlock.InvType, dataBlock.ItemID);

                item.AssetType = (AssetType)dataBlock.Type;
                if (dataBlock.AssetID != UUID.Zero) item.AssetUUID = dataBlock.AssetID;
                item.CreationDate = Utils.UnixTimeToDateTime(dataBlock.CreationDate);
                item.CreatorID = dataBlock.CreatorID;
                item.Description = Utils.BytesToString(dataBlock.Description);
                item.Flags = dataBlock.Flags;
                item.GroupID = dataBlock.GroupID;
                item.GroupOwned = dataBlock.GroupOwned;
                item.Name = Utils.BytesToString(dataBlock.Name);
                item.OwnerID = dataBlock.OwnerID;
                item.ParentUUID = dataBlock.FolderID;
                item.Permissions = new Permissions(
                    dataBlock.BaseMask,
                    dataBlock.EveryoneMask,
                    dataBlock.GroupMask,
                    dataBlock.NextOwnerMask,
                    dataBlock.OwnerMask);
                item.SalePrice = dataBlock.SalePrice;
                item.SaleType = (SaleType)dataBlock.SaleType;

                Store[item.UUID] = item;

                // Look for an "item created" callback
                ItemCreatedCallback callback;
                if (_ItemCreatedCallbacks.TryGetValue(dataBlock.CallbackID, out callback))
                {
                    _ItemCreatedCallbacks.Remove(dataBlock.CallbackID);

                    try
                    {
                        callback(true, item);
                    }
                    catch (Exception ex)
                    {
                        Logger.Log(ex.Message, Helpers.LogLevel.Error, Client, ex);
                    }
                }

                // Look for an "item copied" callback
                ItemCopiedCallback copyCallback;
                if (_ItemCopiedCallbacks.TryGetValue(dataBlock.CallbackID, out copyCallback))
                {
                    _ItemCopiedCallbacks.Remove(dataBlock.CallbackID);

                    try
                    {
                        copyCallback(item);
                    }
                    catch (Exception ex)
                    {
                        Logger.Log(ex.Message, Helpers.LogLevel.Error, Client, ex);
                    }
                }
            }
    }

    /// <summary>Process an incoming packet and raise the appropriate events</summary>
    /// <param name="sender">The sender</param>
    /// <param name="e">The EventArgs object containing the packet data</param>
    protected void FetchInventoryReplyHandler(object sender, PacketReceivedEventArgs e)
    {
        var packet = e.Packet;

        var reply = packet as FetchInventoryReplyPacket;

        foreach (var dataBlock in reply.InventoryData)
        {
            if (dataBlock.InvType == (sbyte)InventoryType.Folder)
            {
                Logger.Log("Received FetchInventoryReply for an inventory folder, this should not happen!",
                    Helpers.LogLevel.Error, Client);
                continue;
            }

            var item = CreateInventoryItem((InventoryType)dataBlock.InvType, dataBlock.ItemID);
            item.AssetType = (AssetType)dataBlock.Type;
            item.AssetUUID = dataBlock.AssetID;
            item.CreationDate = Utils.UnixTimeToDateTime(dataBlock.CreationDate);
            item.CreatorID = dataBlock.CreatorID;
            item.Description = Utils.BytesToString(dataBlock.Description);
            item.Flags = dataBlock.Flags;
            item.GroupID = dataBlock.GroupID;
            item.GroupOwned = dataBlock.GroupOwned;
            item.InventoryType = (InventoryType)dataBlock.InvType;
            item.Name = Utils.BytesToString(dataBlock.Name);
            item.OwnerID = dataBlock.OwnerID;
            item.ParentUUID = dataBlock.FolderID;
            item.Permissions = new Permissions(
                dataBlock.BaseMask,
                dataBlock.EveryoneMask,
                dataBlock.GroupMask,
                dataBlock.NextOwnerMask,
                dataBlock.OwnerMask);
            item.SalePrice = dataBlock.SalePrice;
            item.SaleType = (SaleType)dataBlock.SaleType;
            item.UUID = dataBlock.ItemID;

            Store[item.UUID] = item;

            // Fire the callback for an item being fetched
            OnItemReceived(new ItemReceivedEventArgs(item));
        }
    }

    /// <summary>Process an incoming packet and raise the appropriate events</summary>
    /// <param name="sender">The sender</param>
    /// <param name="e">The EventArgs object containing the packet data</param>
    protected void ReplyTaskInventoryHandler(object sender, PacketReceivedEventArgs e)
    {
        if (m_TaskInventoryReply != null)
        {
            var packet = e.Packet;

            var reply = (ReplyTaskInventoryPacket)packet;

            OnTaskInventoryReply(new TaskInventoryReplyEventArgs(reply.InventoryData.TaskID, reply.InventoryData.Serial,
                Utils.BytesToString(reply.InventoryData.Filename)));
        }
    }

    protected void ScriptRunningReplyMessageHandler(string capsKey, IMessage message, Simulator simulator)
    {
        if (m_ScriptRunningReply != null)
        {
            var msg = (ScriptRunningReplyMessage)message;
            OnScriptRunningReply(new ScriptRunningReplyEventArgs(msg.ObjectID, msg.ItemID, msg.Mono, msg.Running));
        }
    }

    #endregion Packet Handlers
}

#region EventArgs

public class InventoryObjectOfferedEventArgs : EventArgs
{
    public InventoryObjectOfferedEventArgs(InstantMessage offerDetails, AssetType type, UUID objectID, bool fromTask,
        UUID folderID)
    {
        Accept = false;
        FolderID = folderID;
        Offer = offerDetails;
        AssetType = type;
        ObjectID = objectID;
        FromTask = fromTask;
    }

    /// <summary>Set to true to accept offer, false to decline it</summary>
    public bool Accept { get; set; }

    /// <summary>The folder to accept the inventory into, if null default folder for <see cref="AssetType" /> will be used</summary>
    public UUID FolderID { get; set; }

    public InstantMessage Offer { get; }

    public AssetType AssetType { get; }

    public UUID ObjectID { get; }

    public bool FromTask { get; }
}

public class FolderUpdatedEventArgs : EventArgs
{
    public FolderUpdatedEventArgs(UUID folderID, bool success)
    {
        FolderID = folderID;
        Success = success;
    }

    public UUID FolderID { get; }

    public bool Success { get; }
}

public class ItemReceivedEventArgs : EventArgs
{
    public ItemReceivedEventArgs(InventoryItem item)
    {
        Item = item;
    }

    public InventoryItem Item { get; }
}

public class FindObjectByPathReplyEventArgs : EventArgs
{
    public FindObjectByPathReplyEventArgs(string path, UUID inventoryObjectID)
    {
        Path = path;
        InventoryObjectID = inventoryObjectID;
    }

    public string Path { get; }

    public UUID InventoryObjectID { get; }
}

/// <summary>
///     Callback when an inventory object is accepted and received from a
///     task inventory. This is the callback in which you actually get
///     the ItemID, as in ObjectOfferedCallback it is null when received
///     from a task.
/// </summary>
public class TaskItemReceivedEventArgs : EventArgs
{
    public TaskItemReceivedEventArgs(UUID itemID, UUID folderID, UUID creatorID, UUID assetID, InventoryType type)
    {
        ItemID = itemID;
        FolderID = folderID;
        CreatorID = creatorID;
        AssetID = assetID;
        Type = type;
    }

    public UUID ItemID { get; }

    public UUID FolderID { get; }

    public UUID CreatorID { get; }

    public UUID AssetID { get; }

    public InventoryType Type { get; }
}

public class TaskInventoryReplyEventArgs : EventArgs
{
    public TaskInventoryReplyEventArgs(UUID itemID, short serial, string assetFilename)
    {
        ItemID = itemID;
        Serial = serial;
        AssetFilename = assetFilename;
    }

    public UUID ItemID { get; }

    public short Serial { get; }

    public string AssetFilename { get; }
}

public class SaveAssetToInventoryEventArgs : EventArgs
{
    public SaveAssetToInventoryEventArgs(UUID itemID, UUID newAssetID)
    {
        ItemID = itemID;
        NewAssetID = newAssetID;
    }

    public UUID ItemID { get; }

    public UUID NewAssetID { get; }
}

public class ScriptRunningReplyEventArgs : EventArgs
{
    public ScriptRunningReplyEventArgs(UUID objectID, UUID sctriptID, bool isMono, bool isRunning)
    {
        ObjectID = objectID;
        ScriptID = sctriptID;
        IsMono = isMono;
        IsRunning = isRunning;
    }

    public UUID ObjectID { get; }

    public UUID ScriptID { get; }

    public bool IsMono { get; }

    public bool IsRunning { get; }
}

#endregion