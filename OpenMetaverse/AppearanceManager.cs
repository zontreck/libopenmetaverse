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
using OpenMetaverse.Assets;
using OpenMetaverse.Http;
using OpenMetaverse.Imaging;
using OpenMetaverse.Packets;
using OpenMetaverse.StructuredData;

namespace OpenMetaverse;

#region Enums

/// <summary>
///     Index of TextureEntry slots for avatar appearances
/// </summary>
public enum AvatarTextureIndex
{
    Unknown = -1,
    HeadBodypaint = 0,
    UpperShirt,
    LowerPants,
    EyesIris,
    Hair,
    UpperBodypaint,
    LowerBodypaint,
    LowerShoes,
    HeadBaked,
    UpperBaked,
    LowerBaked,
    EyesBaked,
    LowerSocks,
    UpperJacket,
    LowerJacket,
    UpperGloves,
    UpperUndershirt,
    LowerUnderpants,
    Skirt,
    SkirtBaked,
    HairBaked,
    LowerAlpha,
    UpperAlpha,
    HeadAlpha,
    EyesAlpha,
    HairAlpha,
    HeadTattoo,
    UpperTattoo,
    LowerTattoo,
    HeadUnivTattoo,
    UpperUnivTattoo,
    LowerUnivTattoo,
    SkirtTattoo,
    HairTattoo,
    EyesTattoo,
    LeftArmTattoo,
    LeftLegTattoo,
    Aux1Tattoo,
    Aux2Tattoo,
    Aux3Tattoo,
    LeftArmBaked,
    LeftLegBaked,
    Aux1Baked,
    Aux2Baked,
    Aux3Baked,
    NumberOfEntries
}

/// <summary>
///     Bake layers for avatar appearance
/// </summary>
public enum BakeType
{
    Unknown = -1,
    Head = 0,
    UpperBody = 1,
    LowerBody = 2,
    Eyes = 3,
    Skirt = 4,
    Hair = 5,
    LeftArm = 6,
    LeftLeg = 7,
    Aux1 = 8,
    Aux2 = 9,
    Aux3 = 10,
    NumberOfEntries
}

/// <summary>
///     Appearance Flags, introdued with server side baking, currently unused
/// </summary>
[Flags]
public enum AppearanceFlags : uint
{
    None = 0
}

#endregion Enums

public class AppearanceManager
{
    /// <summary>
    ///     Default constructor
    /// </summary>
    /// <param name="client">A reference to our agent</param>
    public AppearanceManager(GridClient client)
    {
        Client = client;

        Client.Network.RegisterCallback(PacketType.AgentWearablesUpdate, AgentWearablesUpdateHandler);
        Client.Network.RegisterCallback(PacketType.AgentCachedTextureResponse, AgentCachedTextureResponseHandler);
        Client.Network.RegisterCallback(PacketType.RebakeAvatarTextures, RebakeAvatarTexturesHandler);

        Client.Network.EventQueueRunning += Network_OnEventQueueRunning;
        Client.Network.Disconnected += Network_OnDisconnected;
    }

    #region Constants

    /// <summary>Mask for multiple attachments</summary>
    public static readonly byte ATTACHMENT_ADD = 0x80;

    /// <summary>Mapping between BakeType and AvatarTextureIndex</summary>
    public static readonly byte[] BakeIndexToTextureIndex = new byte[BAKED_TEXTURE_COUNT]
        { 8, 9, 10, 11, 19, 20, 40, 41, 42, 43, 44 };

    /// <summary>Maximum number of concurrent downloads for wearable assets and textures</summary>
    private const int MAX_CONCURRENT_DOWNLOADS = 5;

    /// <summary>Maximum number of concurrent uploads for baked textures</summary>
    private const int MAX_CONCURRENT_UPLOADS = 6;

    /// <summary>Timeout for fetching inventory listings</summary>
    private const int INVENTORY_TIMEOUT = 1000 * 30;

    /// <summary>Timeout for fetching a single wearable, or receiving a single packet response</summary>
    private const int WEARABLE_TIMEOUT = 1000 * 30;

    /// <summary>Timeout for fetching a single texture</summary>
    private const int TEXTURE_TIMEOUT = 1000 * 120;

    /// <summary>Timeout for uploading a single baked texture</summary>
    private const int UPLOAD_TIMEOUT = 1000 * 90;

    /// <summary>Number of times to retry bake upload</summary>
    private const int UPLOAD_RETRIES = 2;

    /// <summary>
    ///     When changing outfit, kick off rebake after
    ///     20 seconds has passed since the last change
    /// </summary>
    private const int REBAKE_DELAY = 1000 * 20;

    /// <summary>Total number of wearables for each avatar</summary>
    public const int WEARABLE_COUNT = 17;

    /// <summary>Total number of baked textures on each avatar</summary>
    public const int BAKED_TEXTURE_COUNT = 11;

    /// <summary>Total number of wearables per bake layer</summary>
    public const int WEARABLES_PER_LAYER = 9;

    /// <summary>Map of what wearables are included in each bake</summary>
    public static readonly WearableType[][] WEARABLE_BAKE_MAP =
    {
        new[]
        {
            WearableType.Shape, WearableType.Skin, WearableType.Tattoo, WearableType.Hair, WearableType.Alpha,
            WearableType.Invalid, WearableType.Invalid, WearableType.Invalid, WearableType.Invalid
        },
        new[]
        {
            WearableType.Shape, WearableType.Skin, WearableType.Tattoo, WearableType.Shirt, WearableType.Jacket,
            WearableType.Gloves, WearableType.Undershirt, WearableType.Alpha, WearableType.Invalid
        },
        new[]
        {
            WearableType.Shape, WearableType.Skin, WearableType.Tattoo, WearableType.Pants, WearableType.Shoes,
            WearableType.Socks, WearableType.Jacket, WearableType.Underpants, WearableType.Alpha
        },
        new[]
        {
            WearableType.Eyes, WearableType.Invalid, WearableType.Invalid, WearableType.Invalid, WearableType.Invalid,
            WearableType.Invalid, WearableType.Invalid, WearableType.Invalid, WearableType.Invalid
        },
        new[]
        {
            WearableType.Skirt, WearableType.Invalid, WearableType.Invalid, WearableType.Invalid, WearableType.Invalid,
            WearableType.Invalid, WearableType.Invalid, WearableType.Invalid, WearableType.Invalid
        },
        new[]
        {
            WearableType.Hair, WearableType.Invalid, WearableType.Invalid, WearableType.Invalid, WearableType.Invalid,
            WearableType.Invalid, WearableType.Invalid, WearableType.Invalid, WearableType.Invalid
        },

        new[]
        {
            WearableType.Invalid, WearableType.Invalid, WearableType.Invalid, WearableType.Invalid,
            WearableType.Invalid, WearableType.Invalid, WearableType.Invalid, WearableType.Invalid, WearableType.Invalid
        },
        new[]
        {
            WearableType.Invalid, WearableType.Invalid, WearableType.Invalid, WearableType.Invalid,
            WearableType.Invalid, WearableType.Invalid, WearableType.Invalid, WearableType.Invalid, WearableType.Invalid
        },
        new[]
        {
            WearableType.Invalid, WearableType.Invalid, WearableType.Invalid, WearableType.Invalid,
            WearableType.Invalid, WearableType.Invalid, WearableType.Invalid, WearableType.Invalid, WearableType.Invalid
        },
        new[]
        {
            WearableType.Invalid, WearableType.Invalid, WearableType.Invalid, WearableType.Invalid,
            WearableType.Invalid, WearableType.Invalid, WearableType.Invalid, WearableType.Invalid, WearableType.Invalid
        },
        new[]
        {
            WearableType.Invalid, WearableType.Invalid, WearableType.Invalid, WearableType.Invalid,
            WearableType.Invalid, WearableType.Invalid, WearableType.Invalid, WearableType.Invalid, WearableType.Invalid
        }
    };

    /// <summary>
    ///     Magic values to finalize the cache check hashes for each
    ///     bake
    /// </summary>
    public static readonly UUID[] BAKED_TEXTURE_HASH =
    {
        new("18ded8d6-bcfc-e415-8539-944c0f5ea7a6"),
        new("338c29e3-3024-4dbb-998d-7c04cf4fa88f"),
        new("91b4a2c7-1b1a-ba16-9a16-1f8f8dcc1c3f"),
        new("b2cf28af-b840-1071-3c6a-78085d8128b5"),
        new("ea800387-ea1a-14e0-56cb-24f2022f969a"),
        new("0af1ef7c-ad24-11dd-8790-001f5bf833e8"),

        new("e2658fa6-a47b-11ec-b909-0242ac120002"),
        new("ea8620e2-a47b-11ec-b909-0242ac120002"),
        new("f175510c-a47b-11ec-b909-0242ac120002"),
        new("f8011ec0-a47b-11ec-b909-0242ac120002"),
        new("ff7b4dce-a47b-11ec-b909-0242ac120002")
    };

    /// <summary>
    ///     Default avatar texture, used to detect when a custom
    ///     texture is not set for a face
    /// </summary>
    public static readonly UUID DEFAULT_AVATAR_TEXTURE = new("c228d1cf-4b5d-4ba8-84f4-899a0796aa97");

    #endregion Constants

    #region Structs / Classes

    /// <summary>
    ///     Contains information about a wearable inventory item
    /// </summary>
    public class WearableData
    {
        /// <summary>Asset data for the wearable</summary>
        public AssetWearable Asset;

        /// <summary>AssetID of the wearable asset</summary>
        public UUID AssetID;

        /// <summary>AssetType of the wearable</summary>
        public AssetType AssetType;

        /// <summary>Inventory ItemID of the wearable</summary>
        public UUID ItemID;

        /// <summary>WearableType of the wearable</summary>
        public WearableType WearableType;

        public override string ToString()
        {
            return string.Format("ItemID: {0}, AssetID: {1}, WearableType: {2}, AssetType: {3}, Asset: {4}",
                ItemID, AssetID, WearableType, AssetType, Asset != null ? Asset.Name : "(null)");
        }
    }

    /// <summary>
    ///     Data collected from visual params for each wearable
    ///     needed for the calculation of the color
    /// </summary>
    public struct ColorParamInfo
    {
        public VisualParam VisualParam;
        public VisualColorParam VisualColorParam;
        public float Value;
        public WearableType WearableType;
    }

    /// <summary>
    ///     Holds a texture assetID and the data needed to bake this layer into
    ///     an outfit texture. Used to keep track of currently worn textures
    ///     and baking data
    /// </summary>
    public struct TextureData
    {
        /// <summary>A texture AssetID</summary>
        public UUID TextureID;

        /// <summary>Asset data for the texture</summary>
        public AssetTexture Texture;

        /// <summary>Collection of alpha masks that needs applying</summary>
        public Dictionary<VisualAlphaParam, float> AlphaMasks;

        /// <summary>Tint that should be applied to the texture</summary>
        public Color4 Color;

        /// <summary>Where on avatar does this texture belong</summary>
        public AvatarTextureIndex TextureIndex;

        public override string ToString()
        {
            return string.Format("TextureID: {0}, Texture: {1}",
                TextureID, Texture != null ? Texture.AssetData.Length + " bytes" : "(null)");
        }
    }

    #endregion Structs / Classes

    #region Event delegates, Raise Events

    /// <summary>The event subscribers. null if no subcribers</summary>
    private EventHandler<AgentWearablesReplyEventArgs> m_AgentWearablesReply;

    /// <summary>Raises the AgentWearablesReply event</summary>
    /// <param name="e">
    ///     An AgentWearablesReplyEventArgs object containing the
    ///     data returned from the data server
    /// </param>
    protected virtual void OnAgentWearables(AgentWearablesReplyEventArgs e)
    {
        var handler = m_AgentWearablesReply;
        if (handler != null)
            handler(this, e);
    }

    /// <summary>Thread sync lock object</summary>
    private readonly object m_AgentWearablesLock = new();

    /// <summary>
    ///     Triggered when an AgentWearablesUpdate packet is received,
    ///     telling us what our avatar is currently wearing
    ///     <see cref="RequestAgentWearables" /> request.
    /// </summary>
    public event EventHandler<AgentWearablesReplyEventArgs> AgentWearablesReply
    {
        add
        {
            lock (m_AgentWearablesLock)
            {
                m_AgentWearablesReply += value;
            }
        }
        remove
        {
            lock (m_AgentWearablesLock)
            {
                m_AgentWearablesReply -= value;
            }
        }
    }


    /// <summary>The event subscribers. null if no subcribers</summary>
    private EventHandler<AgentCachedBakesReplyEventArgs> m_AgentCachedBakesReply;

    /// <summary>Raises the CachedBakesReply event</summary>
    /// <param name="e">
    ///     An AgentCachedBakesReplyEventArgs object containing the
    ///     data returned from the data server AgentCachedTextureResponse
    /// </param>
    protected virtual void OnAgentCachedBakes(AgentCachedBakesReplyEventArgs e)
    {
        var handler = m_AgentCachedBakesReply;
        if (handler != null)
            handler(this, e);
    }


    /// <summary>Thread sync lock object</summary>
    private readonly object m_AgentCachedBakesLock = new();

    /// <summary>
    ///     Raised when an AgentCachedTextureResponse packet is
    ///     received, giving a list of cached bakes that were found on the
    ///     simulator
    ///     <seealso cref="RequestCachedBakes" /> request.
    /// </summary>
    public event EventHandler<AgentCachedBakesReplyEventArgs> CachedBakesReply
    {
        add
        {
            lock (m_AgentCachedBakesLock)
            {
                m_AgentCachedBakesReply += value;
            }
        }
        remove
        {
            lock (m_AgentCachedBakesLock)
            {
                m_AgentCachedBakesReply -= value;
            }
        }
    }

    /// <summary>The event subscribers. null if no subcribers</summary>
    private EventHandler<AppearanceSetEventArgs> m_AppearanceSet;

    /// <summary>Raises the AppearanceSet event</summary>
    /// <param name="e">An AppearanceSetEventArgs object indicating if the operatin was successfull</param>
    protected virtual void OnAppearanceSet(AppearanceSetEventArgs e)
    {
        var handler = m_AppearanceSet;
        if (handler != null)
            handler(this, e);
    }

    /// <summary>Thread sync lock object</summary>
    private readonly object m_AppearanceSetLock = new();

    /// <summary>
    ///     Raised when appearance data is sent to the simulator, also indicates
    ///     the main appearance thread is finished.
    /// </summary>
    /// <seealso cref="RequestAgentSetAppearance" />
    /// request.
    public event EventHandler<AppearanceSetEventArgs> AppearanceSet
    {
        add
        {
            lock (m_AppearanceSetLock)
            {
                m_AppearanceSet += value;
            }
        }
        remove
        {
            lock (m_AppearanceSetLock)
            {
                m_AppearanceSet -= value;
            }
        }
    }


    /// <summary>The event subscribers. null if no subcribers</summary>
    private EventHandler<RebakeAvatarTexturesEventArgs> m_RebakeAvatarReply;

    /// <summary>Raises the RebakeAvatarRequested event</summary>
    /// <param name="e">
    ///     An RebakeAvatarTexturesEventArgs object containing the
    ///     data returned from the data server
    /// </param>
    protected virtual void OnRebakeAvatar(RebakeAvatarTexturesEventArgs e)
    {
        var handler = m_RebakeAvatarReply;
        if (handler != null)
            handler(this, e);
    }

    /// <summary>Thread sync lock object</summary>
    private readonly object m_RebakeAvatarLock = new();

    /// <summary>
    ///     Triggered when the simulator requests the agent rebake its appearance.
    /// </summary>
    /// <seealso cref="RebakeAvatarRequest" />
    public event EventHandler<RebakeAvatarTexturesEventArgs> RebakeAvatarRequested
    {
        add
        {
            lock (m_RebakeAvatarLock)
            {
                m_RebakeAvatarReply += value;
            }
        }
        remove
        {
            lock (m_RebakeAvatarLock)
            {
                m_RebakeAvatarReply -= value;
            }
        }
    }

    #endregion

    #region Properties and public fields

    /// <summary>
    ///     Returns true if AppearanceManager is busy and trying to set or change appearance will fail
    /// </summary>
    public bool ManagerBusy => AppearanceThreadRunning != 0;

    /// <summary>Visual parameters last sent to the sim</summary>
    public byte[] MyVisualParameters;

    /// <summary>Textures about this client sent to the sim</summary>
    public Primitive.TextureEntry MyTextures;

    #endregion Properties

    #region Private Members

    /// <summary>A cache of wearables currently being worn</summary>
    private Dictionary<WearableType, WearableData> Wearables = new();

    /// <summary>A cache of textures currently being worn</summary>
    private TextureData[] Textures = new TextureData[(int)AvatarTextureIndex.NumberOfEntries];

    /// <summary>Incrementing serial number for AgentCachedTexture packets</summary>
    private int CacheCheckSerialNum = -1;

    /// <summary>Incrementing serial number for AgentSetAppearance packets</summary>
    private int SetAppearanceSerialNum;

    /// <summary>Indicates if WearablesRequest succeeded</summary>
    private bool GotWearables;

    /// <summary>
    ///     Indicates whether or not the appearance thread is currently
    ///     running, to prevent multiple appearance threads from running
    ///     simultaneously
    /// </summary>
    private int AppearanceThreadRunning;

    /// <summary>Reference to our agent</summary>
    private readonly GridClient Client;

    /// <summary>
    ///     Timer used for delaying rebake on changing outfit
    /// </summary>
    private Timer RebakeScheduleTimer;

    /// <summary>
    ///     Main appearance thread
    /// </summary>
    private Thread AppearanceThread;

    /// <summary>
    ///     Is server baking complete. It needs doing only once
    /// </summary>
    private bool ServerBakingDone;

    #endregion Private Members

    #region Publics Methods

    /// <summary>
    ///     Obsolete method for setting appearance. This function no longer does anything.
    ///     Use RequestSetAppearance() to manually start the appearance thread
    /// </summary>
    [Obsolete("Appearance is now handled automatically")]
    public void SetPreviousAppearance()
    {
    }

    /// <summary>
    ///     Obsolete method for setting appearance. This function no longer does anything.
    ///     Use RequestSetAppearance() to manually start the appearance thread
    /// </summary>
    /// <param name="allowBake">Unused parameter</param>
    [Obsolete("Appearance is now handled automatically")]
    public void SetPreviousAppearance(bool allowBake)
    {
    }

    /// <summary>
    ///     Starts the appearance setting thread
    /// </summary>
    public void RequestSetAppearance()
    {
        RequestSetAppearance(false);
    }

    /// <summary>
    ///     Starts the appearance setting thread
    /// </summary>
    /// <param name="forceRebake">True to force rebaking, otherwise false</param>
    public void RequestSetAppearance(bool forceRebake)
    {
        if (Interlocked.CompareExchange(ref AppearanceThreadRunning, 1, 0) != 0)
        {
            Logger.Log("Appearance thread is already running, skipping", Helpers.LogLevel.Warning);
            return;
        }

        // If we have an active delayed scheduled appearance bake, we dispose of it
        if (RebakeScheduleTimer != null)
        {
            RebakeScheduleTimer.Dispose();
            RebakeScheduleTimer = null;
        }

        // This is the first time setting appearance, run through the entire sequence
        AppearanceThread = new Thread(
            delegate()
            {
                var success = true;
                try
                {
                    if (forceRebake)
                        // Set all of the baked textures to UUID.Zero to force rebaking
                        for (var bakedIndex = 0; bakedIndex < BAKED_TEXTURE_COUNT; bakedIndex++)
                            Textures[(int)BakeTypeToAgentTextureIndex((BakeType)bakedIndex)].TextureID = UUID.Zero;

                    // Is this server side baking enabled sim
                    if (ServerBakingRegion())
                    {
                        if (!GotWearables)
                            // Fetch a list of the current agent wearables
                            if (GetAgentWearables())
                                GotWearables = true;

                        if (!ServerBakingDone || forceRebake)
                        {
                            if (UpdateAvatarAppearance())
                                ServerBakingDone = true;
                            else
                                success = false;
                        }
                    }
                    else // Classic client side baking
                    {
                        if (!GotWearables)
                        {
                            // Fetch a list of the current agent wearables
                            if (!GetAgentWearables())
                            {
                                Logger.Log(
                                    "Failed to retrieve a list of current agent wearables, appearance cannot be set",
                                    Helpers.LogLevel.Error, Client);
                                throw new Exception(
                                    "Failed to retrieve a list of current agent wearables, appearance cannot be set");
                            }

                            GotWearables = true;
                        }

                        // If we get back to server side backing region re-request server bake
                        ServerBakingDone = false;

                        // Download and parse all of the agent wearables
                        if (!DownloadWearables())
                        {
                            success = false;
                            Logger.Log("One or more agent wearables failed to download, appearance will be incomplete",
                                Helpers.LogLevel.Warning, Client);
                        }

                        // If this is the first time setting appearance and we're not forcing rebakes, check the server
                        // for cached bakes
                        if (SetAppearanceSerialNum == 0 && !forceRebake)
                            // Compute hashes for each bake layer and compare against what the simulator currently has
                            if (!GetCachedBakes())
                                Logger.Log(
                                    "Failed to get a list of cached bakes from the simulator, appearance will be rebaked",
                                    Helpers.LogLevel.Warning, Client);

                        // Download textures, compute bakes, and upload for any cache misses
                        if (!CreateBakes())
                        {
                            success = false;
                            Logger.Log("Failed to create or upload one or more bakes, appearance will be incomplete",
                                Helpers.LogLevel.Warning, Client);
                        }

                        // Send the appearance packet
                        RequestAgentSetAppearance();
                    }
                }
                catch (Exception e)
                {
                    Logger.Log(
                        string.Format("Failed to set appearance with exception {0}", e), Helpers.LogLevel.Warning,
                        Client);

                    success = false;
                }
                finally
                {
                    AppearanceThreadRunning = 0;

                    OnAppearanceSet(new AppearanceSetEventArgs(success));
                }
            }
        );

        AppearanceThread.Name = "Appearance";
        AppearanceThread.IsBackground = true;
        AppearanceThread.Start();
    }

    /// <summary>
    ///     Check if current region supports server side baking
    /// </summary>
    /// <returns>True if server side baking support is detected</returns>
    public bool ServerBakingRegion()
    {
        return Client.Network.CurrentSim != null &&
               (Client.Network.CurrentSim.Protocols & RegionProtocols.AgentAppearanceService) != 0;
    }

    /// <summary>
    ///     Ask the server what textures our agent is currently wearing
    /// </summary>
    public void RequestAgentWearables()
    {
        var request = new AgentWearablesRequestPacket();
        request.AgentData.AgentID = Client.Self.AgentID;
        request.AgentData.SessionID = Client.Self.SessionID;

        Client.Network.SendPacket(request);
    }

    /// <summary>
    ///     Build hashes out of the texture assetIDs for each baking layer to
    ///     ask the simulator whether it has cached copies of each baked texture
    /// </summary>
    public void RequestCachedBakes()
    {
        var hashes = new List<AgentCachedTexturePacket.WearableDataBlock>();

        // Build hashes for each of the bake layers from the individual components
        lock (Wearables)
        {
            for (var bakedIndex = 0; bakedIndex < BAKED_TEXTURE_COUNT; bakedIndex++)
            {
                // Don't do a cache request for a skirt bake if we're not wearing a skirt
                if (bakedIndex == (int)BakeType.Skirt && !Wearables.ContainsKey(WearableType.Skirt))
                    continue;

                // Build a hash of all the texture asset IDs in this baking layer
                var hash = UUID.Zero;
                for (var wearableIndex = 0; wearableIndex < WEARABLES_PER_LAYER; wearableIndex++)
                {
                    var type = WEARABLE_BAKE_MAP[bakedIndex][wearableIndex];

                    WearableData wearable;
                    if (type != WearableType.Invalid && Wearables.TryGetValue(type, out wearable))
                        hash ^= wearable.AssetID;
                }

                if (hash.IsNotZero())
                {
                    // Hash with our secret value for this baked layer
                    hash ^= BAKED_TEXTURE_HASH[bakedIndex];

                    // Add this to the list of hashes to send out
                    var block = new AgentCachedTexturePacket.WearableDataBlock();
                    block.ID = hash;
                    block.TextureIndex = (byte)bakedIndex;
                    hashes.Add(block);

                    Logger.DebugLog("Checking cache for " + (BakeType)block.TextureIndex + ", hash=" + block.ID,
                        Client);
                }
            }
        }

        // Only send the packet out if there's something to check
        if (hashes.Count > 0)
        {
            var cache = new AgentCachedTexturePacket();
            cache.AgentData.AgentID = Client.Self.AgentID;
            cache.AgentData.SessionID = Client.Self.SessionID;
            cache.AgentData.SerialNum = Interlocked.Increment(ref CacheCheckSerialNum);

            cache.WearableData = hashes.ToArray();

            Client.Network.SendPacket(cache);
        }
    }

    /// <summary>
    ///     Returns the AssetID of the asset that is currently being worn in a
    ///     given WearableType slot
    /// </summary>
    /// <param name="type">WearableType slot to get the AssetID for</param>
    /// <returns>
    ///     The UUID of the asset being worn in the given slot, or
    ///     UUID.Zero if no wearable is attached to the given slot or wearables
    ///     have not been downloaded yet
    /// </returns>
    public UUID GetWearableAsset(WearableType type)
    {
        WearableData wearable;

        if (Wearables.TryGetValue(type, out wearable))
            return wearable.AssetID;
        return UUID.Zero;
    }

    /// <summary>
    ///     Add a wearable to the current outfit and set appearance
    /// </summary>
    /// <param name="wearableItem">Wearable to be added to the outfit</param>
    public void AddToOutfit(InventoryItem wearableItem)
    {
        var wearableItems = new List<InventoryItem> { wearableItem };
        AddToOutfit(wearableItems);
    }

    /// <summary>
    ///     Add a wearable to the current outfit and set appearance
    /// </summary>
    /// <param name="wearableItem">Wearable to be added to the outfit</param>
    /// <param name="replace">Should existing item on the same point or of the same type be replaced</param>
    public void AddToOutfit(InventoryItem wearableItem, bool replace)
    {
        var wearableItems = new List<InventoryItem> { wearableItem };
        AddToOutfit(wearableItems, true);
    }

    /// <summary>
    ///     Add a list of wearables to the current outfit and set appearance
    /// </summary>
    /// <param name="wearableItems">
    ///     List of wearable inventory items to
    ///     be added to the outfit
    /// </param>
    /// <param name="replace">Should existing item on the same point or of the same type be replaced</param>
    public void AddToOutfit(List<InventoryItem> wearableItems)
    {
        AddToOutfit(wearableItems, true);
    }

    /// <summary>
    ///     Add a list of wearables to the current outfit and set appearance
    /// </summary>
    /// <param name="wearableItems">
    ///     List of wearable inventory items to
    ///     be added to the outfit
    /// </param>
    /// <param name="replace">Should existing item on the same point or of the same type be replaced</param>
    public void AddToOutfit(List<InventoryItem> wearableItems, bool replace)
    {
        var wearables = new List<InventoryWearable>();
        var attachments = new List<InventoryItem>();

        for (var i = 0; i < wearableItems.Count; i++)
        {
            var item = wearableItems[i];

            if (item is InventoryWearable)
                wearables.Add((InventoryWearable)item);
            else if (item is InventoryAttachment || item is InventoryObject)
                attachments.Add(item);
        }

        lock (Wearables)
        {
            // Add the given wearables to the wearables collection
            for (var i = 0; i < wearables.Count; i++)
            {
                var wearableItem = wearables[i];

                var wd = new WearableData();
                wd.AssetID = wearableItem.AssetUUID;
                wd.AssetType = wearableItem.AssetType;
                wd.ItemID = wearableItem.UUID;
                wd.WearableType = wearableItem.WearableType;

                Wearables[wearableItem.WearableType] = wd;
            }
        }

        if (attachments.Count > 0) AddAttachments(attachments, false, replace);

        if (wearables.Count > 0)
        {
            SendAgentIsNowWearing();
            DelayedRequestSetAppearance();
        }
    }

    /// <summary>
    ///     Remove a wearable from the current outfit and set appearance
    /// </summary>
    /// <param name="wearableItem">Wearable to be removed from the outfit</param>
    public void RemoveFromOutfit(InventoryItem wearableItem)
    {
        var wearableItems = new List<InventoryItem>();
        wearableItems.Add(wearableItem);
        RemoveFromOutfit(wearableItems);
    }


    /// <summary>
    ///     Removes a list of wearables from the current outfit and set appearance
    /// </summary>
    /// <param name="wearableItems">
    ///     List of wearable inventory items to
    ///     be removed from the outfit
    /// </param>
    public void RemoveFromOutfit(List<InventoryItem> wearableItems)
    {
        var wearables = new List<InventoryWearable>();
        var attachments = new List<InventoryItem>();

        for (var i = 0; i < wearableItems.Count; i++)
        {
            var item = wearableItems[i];

            if (item is InventoryWearable)
                wearables.Add((InventoryWearable)item);
            else if (item is InventoryAttachment || item is InventoryObject)
                attachments.Add(item);
        }

        var needSetAppearance = false;
        lock (Wearables)
        {
            // Remove the given wearables from the wearables collection
            for (var i = 0; i < wearables.Count; i++)
            {
                var wearableItem = wearables[i];
                if (wearables[i].AssetType != AssetType.Bodypart // Remove if it's not a body part
                    && Wearables.ContainsKey(wearableItem.WearableType) // And we have that wearabe type
                    && Wearables[wearableItem.WearableType].ItemID == wearableItem.UUID // And we are wearing it
                   )
                {
                    Wearables.Remove(wearableItem.WearableType);
                    needSetAppearance = true;
                }
            }
        }

        for (var i = 0; i < attachments.Count; i++) Detach(attachments[i].UUID);

        if (needSetAppearance)
        {
            SendAgentIsNowWearing();
            DelayedRequestSetAppearance();
        }
    }

    /// <summary>
    ///     Replace the current outfit with a list of wearables and set appearance
    /// </summary>
    /// <param name="wearableItems">
    ///     List of wearable inventory items that
    ///     define a new outfit
    /// </param>
    public void ReplaceOutfit(List<InventoryItem> wearableItems)
    {
        ReplaceOutfit(wearableItems, true);
    }

    /// <summary>
    ///     Replace the current outfit with a list of wearables and set appearance
    /// </summary>
    /// <param name="wearableItems">
    ///     List of wearable inventory items that
    ///     define a new outfit
    /// </param>
    /// <param name="safe">
    ///     Check if we have all body parts, set this to false only
    ///     if you know what you're doing
    /// </param>
    public void ReplaceOutfit(List<InventoryItem> wearableItems, bool safe)
    {
        var wearables = new List<InventoryWearable>();
        var attachments = new List<InventoryItem>();

        for (var i = 0; i < wearableItems.Count; i++)
        {
            var item = wearableItems[i];

            if (item is InventoryWearable)
                wearables.Add((InventoryWearable)item);
            else if (item is InventoryAttachment || item is InventoryObject)
                attachments.Add(item);
        }

        if (safe)
        {
            // If we don't already have a the current agent wearables downloaded, updating to a
            // new set of wearables that doesn't have all of the bodyparts can leave the avatar
            // in an inconsistent state. If any bodypart entries are empty, we need to fetch the
            // current wearables first
            var needsCurrentWearables = false;
            lock (Wearables)
            {
                for (var i = 0; i < WEARABLE_COUNT; i++)
                {
                    var wearableType = (WearableType)i;
                    if (WearableTypeToAssetType(wearableType) == AssetType.Bodypart &&
                        !Wearables.ContainsKey(wearableType))
                    {
                        needsCurrentWearables = true;
                        break;
                    }
                }
            }

            if (needsCurrentWearables && !GetAgentWearables())
            {
                Logger.Log("Failed to fetch the current agent wearables, cannot safely replace outfit",
                    Helpers.LogLevel.Error);
                return;
            }
        }

        // Replace our local Wearables collection, send the packet(s) to update our
        // attachments, tell sim what we are wearing now, and start the baking process
        if (!safe) SetAppearanceSerialNum++;
        ReplaceOutfit(wearables);
        AddAttachments(attachments, true, false);
        SendAgentIsNowWearing();
        DelayedRequestSetAppearance();
    }

    /// <summary>
    ///     Checks if an inventory item is currently being worn
    /// </summary>
    /// <param name="item">
    ///     The inventory item to check against the agent
    ///     wearables
    /// </param>
    /// <returns>
    ///     The WearableType slot that the item is being worn in,
    ///     or WearbleType.Invalid if it is not currently being worn
    /// </returns>
    public WearableType IsItemWorn(InventoryItem item)
    {
        lock (Wearables)
        {
            foreach (var entry in Wearables)
                if (entry.Value.ItemID == item.UUID)
                    return entry.Key;
        }

        return WearableType.Invalid;
    }

    /// <summary>
    ///     Returns a copy of the agents currently worn wearables
    /// </summary>
    /// <returns>A copy of the agents currently worn wearables</returns>
    /// <remarks>
    ///     Avoid calling this function multiple times as it will make
    ///     a copy of all of the wearable data each time
    /// </remarks>
    public Dictionary<WearableType, WearableData> GetWearables()
    {
        lock (Wearables)
        {
            return new Dictionary<WearableType, WearableData>(Wearables);
        }
    }

    /// <summary>
    ///     Calls either <seealso cref="ReplaceOutfit" /> or
    ///     <seealso cref="AddToOutfit" /> depending on the value of
    ///     replaceItems
    /// </summary>
    /// <param name="wearables">
    ///     List of wearable inventory items to add
    ///     to the outfit or become a new outfit
    /// </param>
    /// <param name="replaceItems">
    ///     True to replace existing items with the
    ///     new list of items, false to add these items to the existing outfit
    /// </param>
    public void WearOutfit(List<InventoryBase> wearables, bool replaceItems)
    {
        var wearableItems = new List<InventoryItem>(wearables.Count);
        for (var i = 0; i < wearables.Count; i++)
            if (wearables[i] is InventoryItem)
                wearableItems.Add((InventoryItem)wearables[i]);

        if (replaceItems)
            ReplaceOutfit(wearableItems);
        else
            AddToOutfit(wearableItems);
    }

    #endregion Publics Methods

    #region Attachments

    /// <summary>
    ///     Adds a list of attachments to our agent
    /// </summary>
    /// <param name="attachments">A List containing the attachments to add</param>
    /// <param name="removeExistingFirst">
    ///     If true, tells simulator to remove existing attachment
    ///     first
    /// </param>
    public void AddAttachments(List<InventoryItem> attachments, bool removeExistingFirst)
    {
        AddAttachments(attachments, removeExistingFirst, true);
    }

    /// <summary>
    ///     Adds a list of attachments to our agent
    /// </summary>
    /// <param name="attachments">A List containing the attachments to add</param>
    /// <param name="removeExistingFirst">
    ///     If true, tells simulator to remove existing attachment
    ///     <param name="replace">
    ///         If true replace existing attachment on this attachment point, otherwise add to it
    ///         (multi-attachments)
    ///     </param>
    ///     first
    /// </param>
    public void AddAttachments(List<InventoryItem> attachments, bool removeExistingFirst, bool replace)
    {
        // Use RezMultipleAttachmentsFromInv  to clear out current attachments, and attach new ones
        var attachmentsPacket = new RezMultipleAttachmentsFromInvPacket();
        attachmentsPacket.AgentData.AgentID = Client.Self.AgentID;
        attachmentsPacket.AgentData.SessionID = Client.Self.SessionID;

        attachmentsPacket.HeaderData.CompoundMsgID = UUID.Random();
        attachmentsPacket.HeaderData.FirstDetachAll = removeExistingFirst;
        attachmentsPacket.HeaderData.TotalObjects = (byte)attachments.Count;

        attachmentsPacket.ObjectData = new RezMultipleAttachmentsFromInvPacket.ObjectDataBlock[attachments.Count];
        for (var i = 0; i < attachments.Count; i++)
            if (attachments[i] is InventoryAttachment)
            {
                var attachment = (InventoryAttachment)attachments[i];
                attachmentsPacket.ObjectData[i] = new RezMultipleAttachmentsFromInvPacket.ObjectDataBlock();
                attachmentsPacket.ObjectData[i].AttachmentPt = replace
                    ? (byte)attachment.AttachmentPoint
                    : (byte)(ATTACHMENT_ADD | (byte)attachment.AttachmentPoint);
                attachmentsPacket.ObjectData[i].EveryoneMask = (uint)attachment.Permissions.EveryoneMask;
                attachmentsPacket.ObjectData[i].GroupMask = (uint)attachment.Permissions.GroupMask;
                attachmentsPacket.ObjectData[i].ItemFlags = attachment.Flags;
                attachmentsPacket.ObjectData[i].ItemID = attachment.UUID;
                attachmentsPacket.ObjectData[i].Name = Utils.StringToBytes(attachment.Name);
                attachmentsPacket.ObjectData[i].Description = Utils.StringToBytes(attachment.Description);
                attachmentsPacket.ObjectData[i].NextOwnerMask = (uint)attachment.Permissions.NextOwnerMask;
                attachmentsPacket.ObjectData[i].OwnerID = attachment.OwnerID;
            }
            else if (attachments[i] is InventoryObject)
            {
                var attachment = (InventoryObject)attachments[i];
                attachmentsPacket.ObjectData[i] = new RezMultipleAttachmentsFromInvPacket.ObjectDataBlock();
                attachmentsPacket.ObjectData[i].AttachmentPt = replace ? (byte)0 : ATTACHMENT_ADD;
                attachmentsPacket.ObjectData[i].EveryoneMask = (uint)attachment.Permissions.EveryoneMask;
                attachmentsPacket.ObjectData[i].GroupMask = (uint)attachment.Permissions.GroupMask;
                attachmentsPacket.ObjectData[i].ItemFlags = attachment.Flags;
                attachmentsPacket.ObjectData[i].ItemID = attachment.UUID;
                attachmentsPacket.ObjectData[i].Name = Utils.StringToBytes(attachment.Name);
                attachmentsPacket.ObjectData[i].Description = Utils.StringToBytes(attachment.Description);
                attachmentsPacket.ObjectData[i].NextOwnerMask = (uint)attachment.Permissions.NextOwnerMask;
                attachmentsPacket.ObjectData[i].OwnerID = attachment.OwnerID;
            }
            else
            {
                Logger.Log("Cannot attach inventory item " + attachments[i].Name, Helpers.LogLevel.Warning, Client);
            }

        Client.Network.SendPacket(attachmentsPacket);
    }

    /// <summary>
    ///     Attach an item to our agent at a specific attach point
    /// </summary>
    /// <param name="item">A <seealso cref="OpenMetaverse.InventoryItem" /> to attach</param>
    /// <param name="attachPoint">
    ///     the <seealso cref="OpenMetaverse.AttachmentPoint" /> on the avatar
    ///     to attach the item to
    /// </param>
    public void Attach(InventoryItem item, AttachmentPoint attachPoint)
    {
        Attach(item, attachPoint, true);
    }

    /// <summary>
    ///     Attach an item to our agent at a specific attach point
    /// </summary>
    /// <param name="item">A <seealso cref="OpenMetaverse.InventoryItem" /> to attach</param>
    /// <param name="attachPoint">
    ///     the <seealso cref="OpenMetaverse.AttachmentPoint" /> on the avatar
    ///     <param name="replace">
    ///         If true replace existing attachment on this attachment point, otherwise add to it
    ///         (multi-attachments)
    ///     </param>
    ///     to attach the item to
    /// </param>
    public void Attach(InventoryItem item, AttachmentPoint attachPoint, bool replace)
    {
        Attach(item.UUID, item.OwnerID, item.Name, item.Description, item.Permissions, item.Flags,
            attachPoint, replace);
    }

    /// <summary>
    ///     Attach an item to our agent specifying attachment details
    /// </summary>
    /// <param name="itemID">The <seealso cref="OpenMetaverse.UUID" /> of the item to attach</param>
    /// <param name="ownerID">The <seealso cref="OpenMetaverse.UUID" /> attachments owner</param>
    /// <param name="name">The name of the attachment</param>
    /// <param name="description">The description of the attahment</param>
    /// <param name="perms">The <seealso cref="OpenMetaverse.Permissions" /> to apply when attached</param>
    /// <param name="itemFlags">The <seealso cref="OpenMetaverse.InventoryItemFlags" /> of the attachment</param>
    /// <param name="attachPoint">
    ///     The <seealso cref="OpenMetaverse.AttachmentPoint" /> on the agent
    ///     to attach the item to
    /// </param>
    public void Attach(UUID itemID, UUID ownerID, string name, string description,
        Permissions perms, uint itemFlags, AttachmentPoint attachPoint)
    {
        Attach(itemID, ownerID, name, description, perms, itemFlags, attachPoint, true);
    }

    /// <summary>
    ///     Attach an item to our agent specifying attachment details
    /// </summary>
    /// <param name="itemID">The <seealso cref="OpenMetaverse.UUID" /> of the item to attach</param>
    /// <param name="ownerID">The <seealso cref="OpenMetaverse.UUID" /> attachments owner</param>
    /// <param name="name">The name of the attachment</param>
    /// <param name="description">The description of the attahment</param>
    /// <param name="perms">The <seealso cref="OpenMetaverse.Permissions" /> to apply when attached</param>
    /// <param name="itemFlags">The <seealso cref="OpenMetaverse.InventoryItemFlags" /> of the attachment</param>
    /// <param name="attachPoint">
    ///     The <seealso cref="OpenMetaverse.AttachmentPoint" /> on the agent
    ///     <param name="replace">
    ///         If true replace existing attachment on this attachment point, otherwise add to it
    ///         (multi-attachments)
    ///     </param>
    ///     to attach the item to
    /// </param>
    public void Attach(UUID itemID, UUID ownerID, string name, string description,
        Permissions perms, uint itemFlags, AttachmentPoint attachPoint, bool replace)
    {
        // TODO: At some point it might be beneficial to have AppearanceManager track what we
        // are currently wearing for attachments to make enumeration and detachment easier
        var attach = new RezSingleAttachmentFromInvPacket();

        attach.AgentData.AgentID = Client.Self.AgentID;
        attach.AgentData.SessionID = Client.Self.SessionID;

        attach.ObjectData.AttachmentPt = replace ? (byte)attachPoint : (byte)(ATTACHMENT_ADD | (byte)attachPoint);
        attach.ObjectData.Description = Utils.StringToBytes(description);
        attach.ObjectData.EveryoneMask = (uint)perms.EveryoneMask;
        attach.ObjectData.GroupMask = (uint)perms.GroupMask;
        attach.ObjectData.ItemFlags = itemFlags;
        attach.ObjectData.ItemID = itemID;
        attach.ObjectData.Name = Utils.StringToBytes(name);
        attach.ObjectData.NextOwnerMask = (uint)perms.NextOwnerMask;
        attach.ObjectData.OwnerID = ownerID;

        Client.Network.SendPacket(attach);
    }

    /// <summary>
    ///     Detach an item from our agent using an <seealso cref="OpenMetaverse.InventoryItem" /> object
    /// </summary>
    /// <param name="item">An <seealso cref="OpenMetaverse.InventoryItem" /> object</param>
    public void Detach(InventoryItem item)
    {
        Detach(item.UUID);
    }

    /// <summary>
    ///     Detach an item from our agent
    /// </summary>
    /// <param name="itemID">The inventory itemID of the item to detach</param>
    public void Detach(UUID itemID)
    {
        var detach = new DetachAttachmentIntoInvPacket();
        detach.ObjectData.AgentID = Client.Self.AgentID;
        detach.ObjectData.ItemID = itemID;

        Client.Network.SendPacket(detach);
    }

    #endregion Attachments

    #region Appearance Helpers

    /// <summary>
    ///     Inform the sim which wearables are part of our current outfit
    /// </summary>
    private void SendAgentIsNowWearing()
    {
        var wearing = new AgentIsNowWearingPacket();
        wearing.AgentData.AgentID = Client.Self.AgentID;
        wearing.AgentData.SessionID = Client.Self.SessionID;
        wearing.WearableData = new AgentIsNowWearingPacket.WearableDataBlock[WEARABLE_COUNT];

        lock (Wearables)
        {
            for (var i = 0; i < WEARABLE_COUNT; i++)
            {
                var type = (WearableType)i;
                wearing.WearableData[i] = new AgentIsNowWearingPacket.WearableDataBlock();
                wearing.WearableData[i].WearableType = (byte)i;

                if (Wearables.ContainsKey(type))
                    wearing.WearableData[i].ItemID = Wearables[type].ItemID;
                else
                    wearing.WearableData[i].ItemID = UUID.Zero;
            }
        }

        Client.Network.SendPacket(wearing);
    }

    /// <summary>
    ///     Replaces the Wearables collection with a list of new wearable items
    /// </summary>
    /// <param name="wearableItems">Wearable items to replace the Wearables collection with</param>
    private void ReplaceOutfit(List<InventoryWearable> wearableItems)
    {
        var newWearables = new Dictionary<WearableType, WearableData>();

        lock (Wearables)
        {
            // Preserve body parts from the previous set of wearables. They may be overwritten,
            // but cannot be missing in the new set
            foreach (var entry in Wearables)
                if (entry.Value.AssetType == AssetType.Bodypart)
                    newWearables[entry.Key] = entry.Value;

            // Add the given wearables to the new wearables collection
            for (var i = 0; i < wearableItems.Count; i++)
            {
                var wearableItem = wearableItems[i];

                var wd = new WearableData();
                wd.AssetID = wearableItem.AssetUUID;
                wd.AssetType = wearableItem.AssetType;
                wd.ItemID = wearableItem.UUID;
                wd.WearableType = wearableItem.WearableType;

                newWearables[wearableItem.WearableType] = wd;
            }

            // Replace the Wearables collection
            Wearables = newWearables;
        }
    }

    /// <summary>
    ///     Calculates base color/tint for a specific wearable
    ///     based on its params
    /// </summary>
    /// <param name="param">
    ///     All the color info gathered from wearable's VisualParams
    ///     passed as list of ColorParamInfo tuples
    /// </param>
    /// <returns>Base color/tint for the wearable</returns>
    public static Color4 GetColorFromParams(List<ColorParamInfo> param)
    {
        // Start off with a blank slate, black, fully transparent
        var res = new Color4(0, 0, 0, 0);

        // Apply color modification from each color parameter
        foreach (var p in param)
        {
            var n = p.VisualColorParam.Colors.Length;

            var paramColor = new Color4(0, 0, 0, 0);

            if (n == 1)
            {
                // We got only one color in this param, use it for application
                // to the final color
                paramColor = p.VisualColorParam.Colors[0];
            }
            else if (n > 1)
            {
                // We have an array of colors in this parameter
                // First, we need to find out, based on param value
                // between which two elements of the array our value lands

                // Size of the step using which we iterate from Min to Max
                var step = (p.VisualParam.MaxValue - p.VisualParam.MinValue) / ((float)n - 1);

                // Our color should land inbetween colors in the array with index a and b
                var indexa = 0;
                var indexb = 0;

                var i = 0;

                for (var a = p.VisualParam.MinValue; a <= p.VisualParam.MaxValue; a += step)
                {
                    if (a <= p.Value)
                        indexa = i;
                    else
                        break;

                    i++;
                }

                // Sanity check that we don't go outside bounds of the array
                if (indexa > n - 1)
                    indexa = n - 1;

                indexb = indexa == n - 1 ? indexa : indexa + 1;

                // How far is our value from Index A on the 
                // line from Index A to Index B
                var distance = p.Value - indexa * step;

                // We are at Index A (allowing for some floating point math fuzz),
                // use the color on that index
                if (distance < 0.00001f || indexa == indexb)
                {
                    paramColor = p.VisualColorParam.Colors[indexa];
                }
                else
                {
                    // Not so simple as being precisely on the index eh? No problem.
                    // We take the two colors that our param value places us between
                    // and then find the value for each ARGB element that is
                    // somewhere on the line between color1 and color2 at some
                    // distance from the first color
                    var c1 = paramColor = p.VisualColorParam.Colors[indexa];
                    var c2 = paramColor = p.VisualColorParam.Colors[indexb];

                    // Distance is some fraction of the step, use that fraction
                    // to find the value in the range from color1 to color2
                    paramColor = Color4.Lerp(c1, c2, distance / step);
                }

                // Please leave this fragment even if its commented out
                // might prove useful should ($deity forbid) there be bugs in this code
                //string carray = "";
                //foreach (Color c in p.VisualColorParam.Colors)
                //{
                //    carray += c.ToString() + " - ";
                //}
                //Logger.DebugLog("Calculating color for " + p.WearableType + " from " + p.VisualParam.Name + ", value is " + p.Value + " in range " + p.VisualParam.MinValue + " - " + p.VisualParam.MaxValue + " step " + step + " with " + n + " elements " + carray + " A: " + indexa + " B: " + indexb + " at distance " + distance);
            }

            // Now that we have calculated color from the scale of colors
            // that visual params provided, lets apply it to the result
            switch (p.VisualColorParam.Operation)
            {
                case VisualColorOperation.Add:
                    res += paramColor;
                    break;
                case VisualColorOperation.Multiply:
                    res *= paramColor;
                    break;
                case VisualColorOperation.Blend:
                    res = Color4.Lerp(res, paramColor, p.Value);
                    break;
            }
        }

        return res;
    }

    /// <summary>
    ///     Blocking method to populate the Wearables dictionary
    /// </summary>
    /// <returns>True on success, otherwise false</returns>
    private bool GetAgentWearables()
    {
        var wearablesEvent = new AutoResetEvent(false);
        EventHandler<AgentWearablesReplyEventArgs> wearablesCallback = (s, e) => wearablesEvent.Set();

        AgentWearablesReply += wearablesCallback;

        RequestAgentWearables();

        var success = wearablesEvent.WaitOne(WEARABLE_TIMEOUT, false);

        AgentWearablesReply -= wearablesCallback;

        return success;
    }

    /// <summary>
    ///     Blocking method to populate the Textures array with cached bakes
    /// </summary>
    /// <returns>True on success, otherwise false</returns>
    private bool GetCachedBakes()
    {
        var cacheCheckEvent = new AutoResetEvent(false);
        EventHandler<AgentCachedBakesReplyEventArgs> cacheCallback = (sender, e) => cacheCheckEvent.Set();

        CachedBakesReply += cacheCallback;

        RequestCachedBakes();

        var success = cacheCheckEvent.WaitOne(WEARABLE_TIMEOUT, false);

        CachedBakesReply -= cacheCallback;

        return success;
    }

    /// <summary>
    ///     Populates textures and visual params from a decoded asset
    /// </summary>
    /// <param name="wearable">Wearable to decode</param>
    /// <summary>
    ///     Populates textures and visual params from a decoded asset
    /// </summary>
    /// <param name="textures">textures to decode</param>
    public static void DecodeWearableParams(WearableData wearable, ref TextureData[] textures)
    {
        var alphaMasks = new Dictionary<VisualAlphaParam, float>();
        var colorParams = new List<ColorParamInfo>();

        // Populate collection of alpha masks from visual params
        // also add color tinting information
        foreach (var kvp in wearable.Asset.Params)
        {
            if (!VisualParams.Params.ContainsKey(kvp.Key)) continue;

            var p = VisualParams.Params[kvp.Key];

            var colorInfo = new ColorParamInfo();
            colorInfo.WearableType = wearable.WearableType;
            colorInfo.VisualParam = p;
            colorInfo.Value = kvp.Value;

            // Color params
            if (p.ColorParams.HasValue)
            {
                colorInfo.VisualColorParam = p.ColorParams.Value;

                if (wearable.WearableType == WearableType.Tattoo)
                {
                    if (kvp.Key == 1062 || kvp.Key == 1063 || kvp.Key == 1064) colorParams.Add(colorInfo);
                }
                else if (wearable.WearableType == WearableType.Jacket)
                {
                    if (kvp.Key == 809 || kvp.Key == 810 || kvp.Key == 811) colorParams.Add(colorInfo);
                }
                else if (wearable.WearableType == WearableType.Hair)
                {
                    // Param 112 - Rainbow
                    // Param 113 - Red
                    // Param 114 - Blonde
                    // Param 115 - White
                    if (kvp.Key == 112 || kvp.Key == 113 || kvp.Key == 114 || kvp.Key == 115)
                        colorParams.Add(colorInfo);
                }
                else if (wearable.WearableType == WearableType.Skin)
                {
                    // For skin we skip makeup params for now and use only the 3
                    // that are used to determine base skin tone
                    // Param 108 - Rainbow Color
                    // Param 110 - Red Skin (Ruddiness)
                    // Param 111 - Pigment
                    if (kvp.Key == 108 || kvp.Key == 110 || kvp.Key == 111) colorParams.Add(colorInfo);
                }
                else
                {
                    colorParams.Add(colorInfo);
                }
            }

            // Add alpha mask
            if (p.AlphaParams.HasValue && p.AlphaParams.Value.TGAFile != string.Empty && !p.IsBumpAttribute &&
                !alphaMasks.ContainsKey(p.AlphaParams.Value))
                alphaMasks.Add(p.AlphaParams.Value, kvp.Value == 0 ? 0.01f : kvp.Value);

            // Alhpa masks can also be specified in sub "driver" params
            if (p.Drivers != null)
                for (var i = 0; i < p.Drivers.Length; i++)
                    if (VisualParams.Params.ContainsKey(p.Drivers[i]))
                    {
                        var driver = VisualParams.Params[p.Drivers[i]];
                        if (driver.AlphaParams.HasValue && driver.AlphaParams.Value.TGAFile != string.Empty &&
                            !driver.IsBumpAttribute &&
                            !alphaMasks.ContainsKey(driver.AlphaParams.Value))
                            alphaMasks.Add(driver.AlphaParams.Value, kvp.Value == 0 ? 0.01f : kvp.Value);
                    }
        }

        var wearableColor = Color4.White; // Never actually used
        if (colorParams.Count > 0)
        {
            wearableColor = GetColorFromParams(colorParams);
            Logger.DebugLog("Setting tint " + wearableColor + " for " + wearable.WearableType);
        }

        // Loop through all of the texture IDs in this decoded asset and put them in our cache of worn textures
        foreach (var entry in wearable.Asset.Textures)
        {
            var i = (int)entry.Key;

            // Update information about color and alpha masks for this texture
            textures[i].AlphaMasks = alphaMasks;
            textures[i].Color = wearableColor;

            // If this texture changed, update the TextureID and clear out the old cached texture asset
            if (textures[i].TextureID != entry.Value)
            {
                // Treat DEFAULT_AVATAR_TEXTURE as null
                if (entry.Value != DEFAULT_AVATAR_TEXTURE)
                    textures[i].TextureID = entry.Value;
                else
                    textures[i].TextureID = UUID.Zero;

                textures[i].Texture = null;
            }
        }
    }

    /// <summary>
    ///     Blocking method to download and parse currently worn wearable assets
    /// </summary>
    /// <returns>True on success, otherwise false</returns>
    private bool DownloadWearables()
    {
        var success = true;

        // Make a copy of the wearables dictionary to enumerate over
        Dictionary<WearableType, WearableData> wearables;
        lock (Wearables)
        {
            wearables = new Dictionary<WearableType, WearableData>(Wearables);
        }

        // We will refresh the textures (zero out all non bake textures)
        for (var i = 0; i < Textures.Length; i++)
        {
            var isBake = false;
            for (var j = 0; j < BakeIndexToTextureIndex.Length; j++)
                if (BakeIndexToTextureIndex[j] == i)
                {
                    isBake = true;
                    break;
                }

            if (!isBake)
                Textures[i] = new TextureData();
        }

        var pendingWearables = wearables.Count;
        foreach (var wearable in wearables.Values)
            if (wearable.Asset != null)
            {
                DecodeWearableParams(wearable, ref Textures);
                --pendingWearables;
            }

        if (pendingWearables == 0)
            return true;

        Logger.DebugLog("Downloading " + pendingWearables + " wearable assets");

        Parallel.ForEach(Math.Min(pendingWearables, MAX_CONCURRENT_DOWNLOADS), wearables.Values,
            delegate(WearableData wearable)
            {
                if (wearable.Asset == null)
                {
                    var downloadEvent = new AutoResetEvent(false);

                    // Fetch this wearable asset
                    Client.Assets.RequestAsset(wearable.AssetID, wearable.AssetType, true,
                        delegate(AssetDownload transfer, Asset asset)
                        {
                            if (transfer.Success && asset is AssetWearable)
                            {
                                // Update this wearable with the freshly downloaded asset 
                                wearable.Asset = (AssetWearable)asset;

                                if (wearable.Asset.Decode())
                                {
                                    DecodeWearableParams(wearable, ref Textures);
                                    Logger.DebugLog("Downloaded wearable asset " + wearable.WearableType + " with " +
                                                    wearable.Asset.Params.Count +
                                                    " visual params and " + wearable.Asset.Textures.Count + " textures",
                                        Client);
                                }
                                else
                                {
                                    wearable.Asset = null;
                                    Logger.Log("Failed to decode asset:" + Environment.NewLine +
                                               Utils.BytesToString(asset.AssetData), Helpers.LogLevel.Error, Client);
                                }
                            }
                            else
                            {
                                Logger.Log("Wearable " + wearable.AssetID + "(" + wearable.WearableType +
                                           ") failed to download, " +
                                           transfer.Status, Helpers.LogLevel.Warning, Client);
                            }

                            downloadEvent.Set();
                        }
                    );

                    if (!downloadEvent.WaitOne(WEARABLE_TIMEOUT, false))
                    {
                        Logger.Log(
                            "Timed out downloading wearable asset " + wearable.AssetID + " (" + wearable.WearableType +
                            ")",
                            Helpers.LogLevel.Error, Client);
                        success = false;
                    }

                    --pendingWearables;
                }
            }
        );

        return success;
    }

    /// <summary>
    ///     Get a list of all of the textures that need to be downloaded for a
    ///     single bake layer
    /// </summary>
    /// <param name="bakeType">Bake layer to get texture AssetIDs for</param>
    /// <returns>A list of texture AssetIDs to download</returns>
    private List<UUID> GetTextureDownloadList(BakeType bakeType)
    {
        var indices = BakeTypeToTextures(bakeType);
        var textures = new List<UUID>();

        for (var i = 0; i < indices.Count; i++)
        {
            var index = indices[i];

            if (index == AvatarTextureIndex.Skirt && !Wearables.ContainsKey(WearableType.Skirt))
                continue;

            AddTextureDownload(index, textures);
        }

        return textures;
    }

    /// <summary>
    ///     Helper method to lookup the TextureID for a single layer and add it
    ///     to a list if it is not already present
    /// </summary>
    /// <param name="index"></param>
    /// <param name="textures"></param>
    private void AddTextureDownload(AvatarTextureIndex index, List<UUID> textures)
    {
        var textureData = Textures[(int)index];
        // Add the textureID to the list if this layer has a valid textureID set, it has not already
        // been downloaded, and it is not already in the download list
        if (textureData.TextureID != UUID.Zero && textureData.Texture == null &&
            !textures.Contains(textureData.TextureID))
            textures.Add(textureData.TextureID);
    }

    /// <summary>
    ///     Blocking method to download all of the textures needed for baking
    ///     the given bake layers
    /// </summary>
    /// <param name="bakeLayers">A list of layers that need baking</param>
    /// <remarks>
    ///     No return value is given because the baking will happen
    ///     whether or not all textures are successfully downloaded
    /// </remarks>
    private void DownloadTextures(List<BakeType> bakeLayers)
    {
        var textureIDs = new List<UUID>();

        for (var i = 0; i < bakeLayers.Count; i++)
        {
            var layerTextureIDs = GetTextureDownloadList(bakeLayers[i]);

            for (var j = 0; j < layerTextureIDs.Count; j++)
            {
                var uuid = layerTextureIDs[j];
                if (!textureIDs.Contains(uuid))
                    textureIDs.Add(uuid);
            }
        }

        Logger.DebugLog("Downloading " + textureIDs.Count + " textures for baking");

        Parallel.ForEach(MAX_CONCURRENT_DOWNLOADS, textureIDs,
            delegate(UUID textureID)
            {
                try
                {
                    var downloadEvent = new AutoResetEvent(false);

                    Client.Assets.RequestImage(textureID,
                        delegate(TextureRequestState state, AssetTexture assetTexture)
                        {
                            if (state == TextureRequestState.Finished)
                            {
                                assetTexture.Decode();

                                for (var i = 0; i < Textures.Length; i++)
                                    if (Textures[i].TextureID == textureID)
                                        Textures[i].Texture = assetTexture;
                            }
                            else
                            {
                                Logger.Log(
                                    "Texture " + textureID +
                                    " failed to download, one or more bakes will be incomplete",
                                    Helpers.LogLevel.Warning);
                            }

                            downloadEvent.Set();
                        }
                    );

                    downloadEvent.WaitOne(TEXTURE_TIMEOUT, false);
                }
                catch (Exception e)
                {
                    Logger.Log(
                        string.Format("Download of texture {0} failed with exception {1}", textureID, e),
                        Helpers.LogLevel.Warning, Client);
                }
            }
        );
    }

    /// <summary>
    ///     Blocking method to create and upload baked textures for all of the
    ///     missing bakes
    /// </summary>
    /// <returns>True on success, otherwise false</returns>
    private bool CreateBakes()
    {
        var success = true;
        var pendingBakes = new List<BakeType>();

        // Check each bake layer in the Textures array for missing bakes
        for (var bakedIndex = 0; bakedIndex < BAKED_TEXTURE_COUNT; bakedIndex++)
        {
            var textureIndex = BakeTypeToAgentTextureIndex((BakeType)bakedIndex);

            if (Textures[(int)textureIndex].TextureID.IsZero())
            {
                // If this is the skirt layer and we're not wearing a skirt then skip it
                if (bakedIndex == (int)BakeType.Skirt && !Wearables.ContainsKey(WearableType.Skirt))
                    continue;

                pendingBakes.Add((BakeType)bakedIndex);
            }
        }

        if (pendingBakes.Count > 0)
        {
            DownloadTextures(pendingBakes);

            Parallel.ForEach(Math.Min(MAX_CONCURRENT_UPLOADS, pendingBakes.Count), pendingBakes,
                delegate(BakeType bakeType)
                {
                    if (!CreateBake(bakeType))
                        success = false;
                }
            );
        }

        // Free up all the textures we're holding on to
        for (var i = 0; i < Textures.Length; i++) Textures[i].Texture = null;

        // We just allocated and freed a ridiculous amount of memory while 
        // baking. Signal to the GC to clean up
        GC.Collect();

        return success;
    }

    /// <summary>
    ///     Blocking method to create and upload a baked texture for a single
    ///     bake layer
    /// </summary>
    /// <param name="bakeType">Layer to bake</param>
    /// <returns>True on success, otherwise false</returns>
    private bool CreateBake(BakeType bakeType)
    {
        var textureIndices = BakeTypeToTextures(bakeType);
        var oven = new Baker(bakeType);

        for (var i = 0; i < textureIndices.Count; i++)
        {
            var textureIndex = textureIndices[i];
            var texture = Textures[(int)textureIndex];
            texture.TextureIndex = textureIndex;

            oven.AddTexture(texture);
        }

        var start = Environment.TickCount;
        oven.Bake();
        Logger.DebugLog("Baking " + bakeType + " took " + (Environment.TickCount - start) + "ms");

        var newAssetID = UUID.Zero;
        var retries = UPLOAD_RETRIES;

        while (newAssetID.IsZero() && retries > 0)
        {
            newAssetID = UploadBake(oven.BakedTexture.AssetData);
            --retries;
        }

        Textures[(int)BakeTypeToAgentTextureIndex(bakeType)].TextureID = newAssetID;

        if (newAssetID.IsZero())
        {
            Logger.Log("Failed uploading bake " + bakeType, Helpers.LogLevel.Warning);
            return false;
        }

        return true;
    }

    /// <summary>
    ///     Blocking method to upload a baked texture
    /// </summary>
    /// <param name="textureData">Five channel JPEG2000 texture data to upload</param>
    /// <returns>UUID of the newly created asset on success, otherwise UUID.Zero</returns>
    private UUID UploadBake(byte[] textureData)
    {
        var bakeID = UUID.Zero;
        var uploadEvent = new AutoResetEvent(false);

        Client.Assets.RequestUploadBakedTexture(textureData,
            delegate(UUID newAssetID)
            {
                bakeID = newAssetID;
                uploadEvent.Set();
            }
        );

        // FIXME: evalute the need for timeout here, RequestUploadBakedTexture() will
        // timout either on Client.Settings.TRANSFER_TIMEOUT or Client.Settings.CAPS_TIMEOUT
        // depending on which upload method is used.
        uploadEvent.WaitOne(UPLOAD_TIMEOUT, false);

        return bakeID;
    }

    /// <summary>
    ///     Creates a dictionary of visual param values from the downloaded wearables
    /// </summary>
    /// <returns>
    ///     A dictionary of visual param indices mapping to visual param
    ///     values for our agent that can be fed to the Baker class
    /// </returns>
    private Dictionary<int, float> MakeParamValues()
    {
        var paramValues = new Dictionary<int, float>(VisualParams.Params.Count);

        lock (Wearables)
        {
            foreach (var kvp in VisualParams.Params)
                // Only Group-0 parameters are sent in AgentSetAppearance packets
                if (kvp.Value.Group == 0)
                {
                    var found = false;
                    var vp = kvp.Value;

                    // Try and find this value in our collection of downloaded wearables
                    foreach (var data in Wearables.Values)
                    {
                        float paramValue;
                        if (data.Asset != null && data.Asset.Params.TryGetValue(vp.ParamID, out paramValue))
                        {
                            paramValues.Add(vp.ParamID, paramValue);
                            found = true;
                            break;
                        }
                    }

                    // Use a default value if we don't have one set for it
                    if (!found) paramValues.Add(vp.ParamID, vp.DefaultValue);
                }
        }

        return paramValues;
    }

    /// <summary>
    ///     Initate server baking process
    /// </summary>
    /// <returns>True if the server baking was successful</returns>
    private bool UpdateAvatarAppearance()
    {
        var caps = Client.Network.CurrentSim.Caps;
        if (caps == null) return false;

        var url = caps.CapabilityURI("UpdateAvatarAppearance");
        if (url == null) return false;

        var COF = GetCOF();
        if (COF == null) return false;

        // TODO: create Current Outfit Folder
        var capsRequest = new CapsClient(url);
        var request = new OSDMap(1);
        request["cof_version"] = COF.Version;

        var msg = "Setting server side baking failed";

        var res = capsRequest.GetResponse(request, OSDFormat.Xml, Client.Settings.CAPS_TIMEOUT * 2);

        if (res != null && res is OSDMap)
        {
            var result = (OSDMap)res;
            if (result["success"])
            {
                Logger.Log("Successfully set appearance", Helpers.LogLevel.Info, Client);
                // TODO: Set local visual params and baked textures based on the result here
                return true;
            }

            if (result.ContainsKey("error")) msg += ": " + result["error"].AsString();
        }

        Logger.Log(msg, Helpers.LogLevel.Error, Client);

        return false;
    }

    /// <summary>
    ///     Get the latest version of COF
    /// </summary>
    /// <returns>Current Outfit Folder (or null if getting the data failed)</returns>
    private InventoryFolder GetCOF()
    {
        List<InventoryBase> root = null;
        var folderReceived = new AutoResetEvent(false);

        EventHandler<FolderUpdatedEventArgs> callback = (sender, e) =>
        {
            if (e.FolderID == Client.Inventory.Store.RootFolder.UUID)
            {
                if (e.Success) root = Client.Inventory.Store.GetContents(Client.Inventory.Store.RootFolder.UUID);
                folderReceived.Set();
            }
        };

        Client.Inventory.FolderUpdated += callback;
        Client.Inventory.RequestFolderContentsCap(Client.Inventory.Store.RootFolder.UUID, Client.Self.AgentID, true,
            true, InventorySortOrder.ByDate);
        folderReceived.WaitOne(Client.Settings.CAPS_TIMEOUT);
        Client.Inventory.FolderUpdated -= callback;

        InventoryFolder COF = null;

        // COF should be in the root folder. Request update to get the latest versio number
        if (root != null)
            foreach (var baseItem in root)
                if (baseItem is InventoryFolder &&
                    ((InventoryFolder)baseItem).PreferredType == FolderType.CurrentOutfit)
                {
                    COF = (InventoryFolder)baseItem;
                    break;
                }

        return COF;
    }

    /// <summary>
    ///     Create an AgentSetAppearance packet from Wearables data and the
    ///     Textures array and send it
    /// </summary>
    private void RequestAgentSetAppearance()
    {
        var set = MakeAppearancePacket();
        Client.Network.SendPacket(set);
        Logger.DebugLog("Send AgentSetAppearance packet");
    }

    public AgentSetAppearancePacket MakeAppearancePacket()
    {
        var set = new AgentSetAppearancePacket();
        set.AgentData.AgentID = Client.Self.AgentID;
        set.AgentData.SessionID = Client.Self.SessionID;
        set.AgentData.SerialNum = (uint)Interlocked.Increment(ref SetAppearanceSerialNum);

        // Visual params used in the agent height calculation
        var agentSizeVPHeight = 0.0f;
        var agentSizeVPHeelHeight = 0.0f;
        var agentSizeVPPlatformHeight = 0.0f;
        var agentSizeVPHeadSize = 0.5f;
        var agentSizeVPLegLength = 0.0f;
        var agentSizeVPNeckLength = 0.0f;
        var agentSizeVPHipLength = 0.0f;

        lock (Wearables)
        {
            #region VisualParam

            var vpIndex = 0;
            int nrParams;
            var wearingPhysics = false;

            foreach (var wearable in Wearables.Values)
                if (wearable.WearableType == WearableType.Physics)
                {
                    wearingPhysics = true;
                    break;
                }

            if (wearingPhysics)
                nrParams = 251;
            else
                nrParams = 218;

            set.VisualParam = new AgentSetAppearancePacket.VisualParamBlock[nrParams];

            foreach (var kvp in VisualParams.Params)
            {
                var vp = kvp.Value;
                var paramValue = 0f;
                var found = false;

                // Try and find this value in our collection of downloaded wearables
                foreach (var data in Wearables.Values)
                    if (data.Asset != null && data.Asset.Params.TryGetValue(vp.ParamID, out paramValue))
                    {
                        found = true;
                        break;
                    }

                // Use a default value if we don't have one set for it
                if (!found)
                    paramValue = vp.DefaultValue;

                // Only Group-0 parameters are sent in AgentSetAppearance packets
                if (kvp.Value.Group == 0)
                {
                    set.VisualParam[vpIndex] = new AgentSetAppearancePacket.VisualParamBlock();
                    set.VisualParam[vpIndex].ParamValue = Utils.FloatToByte(paramValue, vp.MinValue, vp.MaxValue);
                    ++vpIndex;
                }

                // Check if this is one of the visual params used in the agent height calculation
                switch (vp.ParamID)
                {
                    case 33:
                        agentSizeVPHeight = paramValue;
                        break;
                    case 198:
                        agentSizeVPHeelHeight = paramValue;
                        break;
                    case 503:
                        agentSizeVPPlatformHeight = paramValue;
                        break;
                    case 682:
                        agentSizeVPHeadSize = paramValue;
                        break;
                    case 692:
                        agentSizeVPLegLength = paramValue;
                        break;
                    case 756:
                        agentSizeVPNeckLength = paramValue;
                        break;
                    case 842:
                        agentSizeVPHipLength = paramValue;
                        break;
                }

                if (vpIndex == nrParams) break;
            }

            MyVisualParameters = new byte[set.VisualParam.Length];
            for (var i = 0; i < set.VisualParam.Length; i++) MyVisualParameters[i] = set.VisualParam[i].ParamValue;

            #endregion VisualParam

            #region TextureEntry

            var te = new Primitive.TextureEntry(DEFAULT_AVATAR_TEXTURE);

            for (uint i = 0; i < Textures.Length; i++)
                if ((i == 0 || i == 5 || i == 6) && Client.Settings.CLIENT_IDENTIFICATION_TAG != UUID.Zero)
                {
                    var face = te.CreateFace(i);
                    face.TextureID = Client.Settings.CLIENT_IDENTIFICATION_TAG;
                    Logger.DebugLog("Sending client identification tag: " + Client.Settings.CLIENT_IDENTIFICATION_TAG,
                        Client);
                }
                else if (Textures[i].TextureID != UUID.Zero)
                {
                    var face = te.CreateFace(i);
                    face.TextureID = Textures[i].TextureID;
                    Logger.DebugLog(
                        "Sending texture entry for " + (AvatarTextureIndex)i + " to " + Textures[i].TextureID, Client);
                }

            set.ObjectData.TextureEntry = te.GetBytes();
            MyTextures = te;

            #endregion TextureEntry

            #region WearableData

            set.WearableData = new AgentSetAppearancePacket.WearableDataBlock[BAKED_TEXTURE_COUNT];

            // Build hashes for each of the bake layers from the individual components
            for (var bakedIndex = 0; bakedIndex < BAKED_TEXTURE_COUNT; bakedIndex++)
            {
                var hash = UUID.Zero;

                for (var wearableIndex = 0; wearableIndex < WEARABLES_PER_LAYER; wearableIndex++)
                {
                    var type = WEARABLE_BAKE_MAP[bakedIndex][wearableIndex];

                    WearableData wearable;
                    if (type != WearableType.Invalid && Wearables.TryGetValue(type, out wearable))
                        hash ^= wearable.AssetID;
                }

                if (hash.IsNotZero())
                    // Hash with our magic value for this baked layer
                    hash ^= BAKED_TEXTURE_HASH[bakedIndex];

                // Tell the server what cached texture assetID to use for each bake layer
                set.WearableData[bakedIndex] = new AgentSetAppearancePacket.WearableDataBlock();
                set.WearableData[bakedIndex].TextureIndex = BakeIndexToTextureIndex[bakedIndex];
                set.WearableData[bakedIndex].CacheID = hash;
                Logger.DebugLog("Sending TextureIndex " + (BakeType)bakedIndex + " with CacheID " + hash, Client);
            }

            #endregion WearableData

            #region Agent Size

            // Takes into account the Shoe Heel/Platform offsets but not the HeadSize offset. Seems to work.
            var agentSizeBase = 1.706;

            // The calculation for the HeadSize scalar may be incorrect, but it seems to work
            var agentHeight = agentSizeBase + agentSizeVPLegLength * .1918 + agentSizeVPHipLength * .0375 +
                              agentSizeVPHeight * .12022 + agentSizeVPHeadSize * .01117 + agentSizeVPNeckLength * .038 +
                              agentSizeVPHeelHeight * .08 + agentSizeVPPlatformHeight * .07;

            set.AgentData.Size = new Vector3(0.45f, 0.6f, (float)agentHeight);

            #endregion Agent Size

            if (Client.Settings.AVATAR_TRACKING)
            {
                Avatar me;
                if (Client.Network.CurrentSim.ObjectsAvatars.TryGetValue(Client.Self.LocalID, out me))
                {
                    me.Textures = MyTextures;
                    me.VisualParameters = MyVisualParameters;
                }
            }
        }

        return set;
    }

    private void DelayedRequestSetAppearance()
    {
        if (RebakeScheduleTimer == null) RebakeScheduleTimer = new Timer(RebakeScheduleTimerTick);
        try
        {
            RebakeScheduleTimer.Change(REBAKE_DELAY, Timeout.Infinite);
        }
        catch
        {
        }
    }

    private void RebakeScheduleTimerTick(object state)
    {
        RequestSetAppearance(true);
    }

    #endregion Appearance Helpers

    #region Inventory Helpers

    private bool GetFolderWearables(string[] folderPath, out List<InventoryWearable> wearables,
        out List<InventoryItem> attachments)
    {
        var folder = Client.Inventory.FindObjectByPath(
            Client.Inventory.Store.RootFolder.UUID, Client.Self.AgentID, string.Join("/", folderPath),
            INVENTORY_TIMEOUT);

        if (folder != UUID.Zero) return GetFolderWearables(folder, out wearables, out attachments);

        Logger.Log("Failed to resolve outfit folder path " + folderPath, Helpers.LogLevel.Error, Client);
        wearables = null;
        attachments = null;
        return false;
    }

    private bool GetFolderWearables(UUID folder, out List<InventoryWearable> wearables,
        out List<InventoryItem> attachments)
    {
        wearables = new List<InventoryWearable>();
        attachments = new List<InventoryItem>();
        var objects = Client.Inventory.FolderContents(folder, Client.Self.AgentID, false, true,
            InventorySortOrder.ByName, INVENTORY_TIMEOUT);

        if (objects != null)
        {
            foreach (var ib in objects)
                if (ib is InventoryWearable)
                {
                    Logger.DebugLog("Adding wearable " + ib.Name, Client);
                    wearables.Add((InventoryWearable)ib);
                }
                else if (ib is InventoryAttachment)
                {
                    Logger.DebugLog("Adding attachment (attachment) " + ib.Name, Client);
                    attachments.Add((InventoryItem)ib);
                }
                else if (ib is InventoryObject)
                {
                    Logger.DebugLog("Adding attachment (object) " + ib.Name, Client);
                    attachments.Add((InventoryItem)ib);
                }
                else
                {
                    Logger.DebugLog("Ignoring inventory item " + ib.Name, Client);
                }
        }
        else
        {
            Logger.Log("Failed to download folder contents of + " + folder, Helpers.LogLevel.Error, Client);
            return false;
        }

        return true;
    }

    #endregion Inventory Helpers

    #region Callbacks

    protected void AgentWearablesUpdateHandler(object sender, PacketReceivedEventArgs e)
    {
        var changed = false;
        var update = (AgentWearablesUpdatePacket)e.Packet;

        lock (Wearables)
        {
            #region Test if anything changed in this update

            for (var i = 0; i < update.WearableData.Length; i++)
            {
                var block = update.WearableData[i];

                if (block.AssetID != UUID.Zero)
                {
                    WearableData wearable;
                    if (Wearables.TryGetValue((WearableType)block.WearableType, out wearable))
                    {
                        if (wearable.AssetID != block.AssetID || wearable.ItemID != block.ItemID)
                        {
                            // A different wearable is now set for this index
                            changed = true;
                            break;
                        }
                    }
                    else
                    {
                        // A wearable is now set for this index
                        changed = true;
                        break;
                    }
                }
                else if (Wearables.ContainsKey((WearableType)block.WearableType))
                {
                    // This index is now empty
                    changed = true;
                    break;
                }
            }

            #endregion Test if anything changed in this update

            if (changed)
            {
                Logger.DebugLog("New wearables received in AgentWearablesUpdate");
                Wearables.Clear();

                for (var i = 0; i < update.WearableData.Length; i++)
                {
                    var block = update.WearableData[i];

                    if (block.AssetID != UUID.Zero)
                    {
                        var type = (WearableType)block.WearableType;

                        var data = new WearableData();
                        data.Asset = null;
                        data.AssetID = block.AssetID;
                        data.AssetType = WearableTypeToAssetType(type);
                        data.ItemID = block.ItemID;
                        data.WearableType = type;

                        // Add this wearable to our collection
                        Wearables[type] = data;
                    }
                }
            }
            else
            {
                Logger.DebugLog("Duplicate AgentWearablesUpdate received, discarding");
            }
        }

        if (changed)
            // Fire the callback
            OnAgentWearables(new AgentWearablesReplyEventArgs());
    }

    protected void RebakeAvatarTexturesHandler(object sender, PacketReceivedEventArgs e)
    {
        var rebake = (RebakeAvatarTexturesPacket)e.Packet;

        // allow the library to do the rebake
        if (Client.Settings.SEND_AGENT_APPEARANCE) RequestSetAppearance(true);

        OnRebakeAvatar(new RebakeAvatarTexturesEventArgs(rebake.TextureData.TextureID));
    }

    protected void AgentCachedTextureResponseHandler(object sender, PacketReceivedEventArgs e)
    {
        var response = (AgentCachedTextureResponsePacket)e.Packet;

        for (var i = 0; i < response.WearableData.Length; i++)
        {
            var block = response.WearableData[i];
            var bakeType = (BakeType)block.TextureIndex;
            var index = BakeTypeToAgentTextureIndex(bakeType);

            Logger.DebugLog("Cache response for " + bakeType + ", TextureID=" + block.TextureID, Client);

            if (block.TextureID != UUID.Zero)
            {
                // A simulator has a cache of this bake layer

                // FIXME: Use this. Right now we don't bother to check if this is a foreign host
                var host = Utils.BytesToString(block.HostName);

                Textures[(int)index].TextureID = block.TextureID;
            }
            // The server does not have a cache of this bake layer
            // FIXME:
        }

        OnAgentCachedBakes(new AgentCachedBakesReplyEventArgs());
    }

    private void Network_OnEventQueueRunning(object sender, EventQueueRunningEventArgs e)
    {
        if (e.Simulator == Client.Network.CurrentSim && Client.Settings.SEND_AGENT_APPEARANCE)
            // Update appearance each time we enter a new sim and capabilities have been retrieved
            Client.Appearance.RequestSetAppearance();
    }

    private void Network_OnDisconnected(object sender, DisconnectedEventArgs e)
    {
        if (RebakeScheduleTimer != null)
        {
            RebakeScheduleTimer.Dispose();
            RebakeScheduleTimer = null;
        }

        if (AppearanceThread != null)
        {
            if (AppearanceThread.IsAlive) AppearanceThread.Abort();
            AppearanceThread = null;
            AppearanceThreadRunning = 0;
        }
    }

    #endregion Callbacks

    #region Static Helpers

    /// <summary>
    ///     Converts a WearableType to a bodypart or clothing WearableType
    /// </summary>
    /// <param name="type">A WearableType</param>
    /// <returns>AssetType.Bodypart or AssetType.Clothing or AssetType.Unknown</returns>
    public static AssetType WearableTypeToAssetType(WearableType type)
    {
        switch (type)
        {
            case WearableType.Shape:
            case WearableType.Skin:
            case WearableType.Hair:
            case WearableType.Eyes:
                return AssetType.Bodypart;
            case WearableType.Shirt:
            case WearableType.Pants:
            case WearableType.Shoes:
            case WearableType.Socks:
            case WearableType.Jacket:
            case WearableType.Gloves:
            case WearableType.Undershirt:
            case WearableType.Underpants:
            case WearableType.Skirt:
            case WearableType.Tattoo:
            case WearableType.Alpha:
            case WearableType.Physics:
            case WearableType.Universal:
                return AssetType.Clothing;
            default:
                return AssetType.Unknown;
        }
    }

    /// <summary>
    ///     Converts a BakeType to the corresponding baked texture slot in AvatarTextureIndex
    /// </summary>
    /// <param name="index">A BakeType</param>
    /// <returns>The AvatarTextureIndex slot that holds the given BakeType</returns>
    public static AvatarTextureIndex BakeTypeToAgentTextureIndex(BakeType index)
    {
        switch (index)
        {
            case BakeType.Head:
                return AvatarTextureIndex.HeadBaked;
            case BakeType.UpperBody:
                return AvatarTextureIndex.UpperBaked;
            case BakeType.LowerBody:
                return AvatarTextureIndex.LowerBaked;
            case BakeType.Eyes:
                return AvatarTextureIndex.EyesBaked;
            case BakeType.Skirt:
                return AvatarTextureIndex.SkirtBaked;
            case BakeType.Hair:
                return AvatarTextureIndex.HairBaked;
            case BakeType.LeftArm:
                return AvatarTextureIndex.LeftArmBaked;
            case BakeType.LeftLeg:
                return AvatarTextureIndex.LeftLegBaked;
            case BakeType.Aux1:
                return AvatarTextureIndex.Aux1Baked;
            case BakeType.Aux2:
                return AvatarTextureIndex.Aux2Baked;
            case BakeType.Aux3:
                return AvatarTextureIndex.Aux3Baked;


            default:
                return AvatarTextureIndex.Unknown;
        }
    }

    /// <summary>
    ///     Gives the layer number that is used for morph mask
    /// </summary>
    /// <param name="bakeType">>A BakeType</param>
    /// <returns>Which layer number as defined in BakeTypeToTextures is used for morph mask</returns>
    public static AvatarTextureIndex MorphLayerForBakeType(BakeType bakeType)
    {
        // Indexes return here correspond to those returned
        // in BakeTypeToTextures(), those two need to be in sync.
        // Which wearable layer is used for morph is defined in avatar_lad.xml
        // by looking for <layer> that has <morph_mask> defined in it, and
        // looking up which wearable is defined in that layer. Morph mask
        // is never combined, it's always a straight copy of one single clothing
        // item's alpha channel per bake.
        switch (bakeType)
        {
            case BakeType.Head:
                return AvatarTextureIndex.Hair; // hair
            case BakeType.UpperBody:
                return AvatarTextureIndex.UpperShirt; // shirt
            case BakeType.LowerBody:
                return AvatarTextureIndex.LowerPants; // lower pants
            case BakeType.Skirt:
                return AvatarTextureIndex.Skirt; // skirt
            case BakeType.Hair:
                return AvatarTextureIndex.Hair; // hair
            default:
                return AvatarTextureIndex.Unknown;
        }
    }

    /// <summary>
    ///     Converts a BakeType to a list of the texture slots that make up that bake
    /// </summary>
    /// <param name="bakeType">A BakeType</param>
    /// <returns>A list of texture slots that are inputs for the given bake</returns>
    public static List<AvatarTextureIndex> BakeTypeToTextures(BakeType bakeType)
    {
        var textures = new List<AvatarTextureIndex>();

        switch (bakeType)
        {
            case BakeType.Head:
                textures.Add(AvatarTextureIndex.HeadBodypaint);
                textures.Add(AvatarTextureIndex.HeadTattoo);
                textures.Add(AvatarTextureIndex.Hair);
                textures.Add(AvatarTextureIndex.HeadAlpha);
                break;
            case BakeType.UpperBody:
                textures.Add(AvatarTextureIndex.UpperBodypaint);
                textures.Add(AvatarTextureIndex.UpperTattoo);
                textures.Add(AvatarTextureIndex.UpperGloves);
                textures.Add(AvatarTextureIndex.UpperUndershirt);
                textures.Add(AvatarTextureIndex.UpperShirt);
                textures.Add(AvatarTextureIndex.UpperJacket);
                textures.Add(AvatarTextureIndex.UpperAlpha);
                break;
            case BakeType.LowerBody:
                textures.Add(AvatarTextureIndex.LowerBodypaint);
                textures.Add(AvatarTextureIndex.LowerTattoo);
                textures.Add(AvatarTextureIndex.LowerUnderpants);
                textures.Add(AvatarTextureIndex.LowerSocks);
                textures.Add(AvatarTextureIndex.LowerShoes);
                textures.Add(AvatarTextureIndex.LowerPants);
                textures.Add(AvatarTextureIndex.LowerJacket);
                textures.Add(AvatarTextureIndex.LowerAlpha);
                break;
            case BakeType.Eyes:
                textures.Add(AvatarTextureIndex.EyesIris);
                textures.Add(AvatarTextureIndex.EyesAlpha);
                break;
            case BakeType.Skirt:
                textures.Add(AvatarTextureIndex.Skirt);
                break;
            case BakeType.Hair:
                textures.Add(AvatarTextureIndex.Hair);
                textures.Add(AvatarTextureIndex.HairAlpha);
                break;
        }

        return textures;
    }

    #endregion Static Helpers
}

#region AppearanceManager EventArgs Classes

/// <summary>Contains the Event data returned from the data server from an AgentWearablesRequest</summary>
public class AgentWearablesReplyEventArgs : EventArgs
{
    /// <summary>Construct a new instance of the AgentWearablesReplyEventArgs class</summary>
    public AgentWearablesReplyEventArgs()
    {
    }
}

/// <summary>Contains the Event data returned from the data server from an AgentCachedTextureResponse</summary>
public class AgentCachedBakesReplyEventArgs : EventArgs
{
    /// <summary>Construct a new instance of the AgentCachedBakesReplyEventArgs class</summary>
    public AgentCachedBakesReplyEventArgs()
    {
    }
}

/// <summary>Contains the Event data returned from an AppearanceSetRequest</summary>
public class AppearanceSetEventArgs : EventArgs
{
    /// <summary>
    ///     Triggered when appearance data is sent to the sim and
    ///     the main appearance thread is done.
    /// </summary>
    /// <param name="success">Indicates whether appearance setting was successful</param>
    public AppearanceSetEventArgs(bool success)
    {
        Success = success;
    }

    /// <summary>Indicates whether appearance setting was successful</summary>
    public bool Success { get; }
}

/// <summary>Contains the Event data returned from the data server from an RebakeAvatarTextures</summary>
public class RebakeAvatarTexturesEventArgs : EventArgs
{
    /// <summary>
    ///     Triggered when the simulator sends a request for this agent to rebake
    ///     its appearance
    /// </summary>
    /// <param name="textureID">The ID of the Texture Layer to bake</param>
    public RebakeAvatarTexturesEventArgs(UUID textureID)
    {
        TextureID = textureID;
    }

    /// <summary>The ID of the Texture Layer to bake</summary>
    public UUID TextureID { get; }
}

#endregion