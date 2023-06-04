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
using OpenMetaverse.Packets;

namespace OpenMetaverse;

public class TerrainManager
{
    private readonly GridClient Client;

    /// <summary>
    ///     Default constructor
    /// </summary>
    /// <param name="client"></param>
    public TerrainManager(GridClient client)
    {
        Client = client;
        Client.Network.RegisterCallback(PacketType.LayerData, LayerDataHandler);
    }

    private void DecompressLand(Simulator simulator, BitPack bitpack, TerrainPatch.GroupHeader group)
    {
        int x;
        int y;
        var patches = new int[32 * 32];
        var count = 0;

        while (true)
        {
            var header = TerrainCompressor.DecodePatchHeader(bitpack);

            if (header.QuantWBits == TerrainCompressor.END_OF_PATCHES)
                break;

            x = header.X;
            y = header.Y;

            if (x >= TerrainCompressor.PATCHES_PER_EDGE || y >= TerrainCompressor.PATCHES_PER_EDGE)
            {
                Logger.Log(string.Format(
                        "Invalid LayerData land packet, x={0}, y={1}, dc_offset={2}, range={3}, quant_wbits={4}, patchids={5}, count={6}",
                        x, y, header.DCOffset, header.Range, header.QuantWBits, header.PatchIDs, count),
                    Helpers.LogLevel.Warning, Client);
                return;
            }

            // Decode this patch
            TerrainCompressor.DecodePatch(patches, bitpack, header, group.PatchSize);

            // Decompress this patch
            var heightmap = TerrainCompressor.DecompressPatch(patches, header, group);

            count++;

            try
            {
                OnLandPatchReceived(new LandPatchReceivedEventArgs(simulator, x, y, group.PatchSize, heightmap));
            }
            catch (Exception e)
            {
                Logger.Log(e.Message, Helpers.LogLevel.Error, Client, e);
            }

            if (Client.Settings.STORE_LAND_PATCHES)
            {
                var patch = new TerrainPatch();
                patch.Data = heightmap;
                patch.X = x;
                patch.Y = y;
                simulator.Terrain[y * 16 + x] = patch;
            }
        }
    }

    private void DecompressWind(Simulator simulator, BitPack bitpack, TerrainPatch.GroupHeader group)
    {
        var patches = new int[32 * 32];

        // Ignore the simulator stride value
        group.Stride = group.PatchSize;

        // Each wind packet contains the wind speeds and direction for the entire simulator
        // stored as two float arrays. The first array is the X value of the wind speed at
        // each 16x16m block, second is the Y value.
        // wind_speed = distance(x,y to 0,0)
        // wind_direction = vec2(x,y)

        // X values
        var header = TerrainCompressor.DecodePatchHeader(bitpack);
        TerrainCompressor.DecodePatch(patches, bitpack, header, group.PatchSize);
        var xvalues = TerrainCompressor.DecompressPatch(patches, header, group);

        // Y values
        header = TerrainCompressor.DecodePatchHeader(bitpack);
        TerrainCompressor.DecodePatch(patches, bitpack, header, group.PatchSize);
        var yvalues = TerrainCompressor.DecompressPatch(patches, header, group);

        if (simulator.Client.Settings.STORE_LAND_PATCHES)
            for (var i = 0; i < 256; i++)
                simulator.WindSpeeds[i] = new Vector2(xvalues[i], yvalues[i]);
    }

    private void DecompressCloud(Simulator simulator, BitPack bitpack, TerrainPatch.GroupHeader group)
    {
        // FIXME:
    }

    private void LayerDataHandler(object sender, PacketReceivedEventArgs e)
    {
        var layer = (LayerDataPacket)e.Packet;
        var bitpack = new BitPack(layer.LayerData.Data, 0);
        var header = new TerrainPatch.GroupHeader();
        var type = (TerrainPatch.LayerType)layer.LayerID.Type;

        // Stride
        header.Stride = bitpack.UnpackBits(16);
        // Patch size
        header.PatchSize = bitpack.UnpackBits(8);
        // Layer type
        header.Type = (TerrainPatch.LayerType)bitpack.UnpackBits(8);

        switch (type)
        {
            case TerrainPatch.LayerType.Land:
                if (m_LandPatchReceivedEvent != null || Client.Settings.STORE_LAND_PATCHES)
                    DecompressLand(e.Simulator, bitpack, header);
                break;
            case TerrainPatch.LayerType.Water:
                Logger.Log("Got a Water LayerData packet, implement me!", Helpers.LogLevel.Error, Client);
                break;
            case TerrainPatch.LayerType.Wind:
                DecompressWind(e.Simulator, bitpack, header);
                break;
            case TerrainPatch.LayerType.Cloud:
                DecompressCloud(e.Simulator, bitpack, header);
                break;
            default:
                Logger.Log("Unrecognized LayerData type " + type, Helpers.LogLevel.Warning, Client);
                break;
        }
    }

    #region EventHandling

    /// <summary>The event subscribers. null if no subcribers</summary>
    private EventHandler<LandPatchReceivedEventArgs> m_LandPatchReceivedEvent;

    /// <summary>Raises the LandPatchReceived event</summary>
    /// <param name="e">
    ///     A LandPatchReceivedEventArgs object containing the
    ///     data returned from the simulator
    /// </param>
    protected virtual void OnLandPatchReceived(LandPatchReceivedEventArgs e)
    {
        var handler = m_LandPatchReceivedEvent;
        if (handler != null)
            handler(this, e);
    }

    /// <summary>Thread sync lock object</summary>
    private readonly object m_LandPatchReceivedLock = new();

    /// <summary>Raised when the simulator responds sends </summary>
    public event EventHandler<LandPatchReceivedEventArgs> LandPatchReceived
    {
        add
        {
            lock (m_LandPatchReceivedLock)
            {
                m_LandPatchReceivedEvent += value;
            }
        }
        remove
        {
            lock (m_LandPatchReceivedLock)
            {
                m_LandPatchReceivedEvent -= value;
            }
        }
    }

    #endregion
}

#region EventArgs classes

// <summary>Provides data for LandPatchReceived</summary>
public class LandPatchReceivedEventArgs : EventArgs
{
    public LandPatchReceivedEventArgs(Simulator simulator, int x, int y, int patchSize, float[] heightMap)
    {
        Simulator = simulator;
        X = x;
        Y = y;
        PatchSize = patchSize;
        HeightMap = heightMap;
    }

    /// <summary>Simulator from that sent tha data</summary>
    public Simulator Simulator { get; }

    /// <summary>Sim coordinate of the patch</summary>
    public int X { get; }

    /// <summary>Sim coordinate of the patch</summary>
    public int Y { get; }

    /// <summary>Size of tha patch</summary>
    public int PatchSize { get; }

    /// <summary>Heightmap for the patch</summary>
    public float[] HeightMap { get; }
}

#endregion