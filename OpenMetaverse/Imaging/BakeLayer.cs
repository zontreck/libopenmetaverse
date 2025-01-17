/*
 * Copyright (c) 2007-2008, openmetaverse.co
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
using System.Reflection;
using IronSoftware.Drawing;
using log4net;
using OpenMetaverse.Assets;

namespace OpenMetaverse.Imaging;

/// <summary>
///     A set of textures that are layered on texture of each other and "baked"
///     in to a single texture, for avatar appearances
/// </summary>
public class Baker
{
    private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

    #region Constructor

    /// <summary>
    ///     Default constructor
    /// </summary>
    /// <param name="bakeType">Bake type</param>
    public Baker(BakeType bakeType)
    {
        this.bakeType = bakeType;

        if (bakeType == BakeType.Eyes)
        {
            bakeWidth = 128;
            bakeHeight = 128;
        }
        else
        {
            bakeWidth = 512;
            bakeHeight = 512;
        }
    }

    #endregion

    #region Properties

    /// <summary>Final baked texture</summary>
    public AssetTexture BakedTexture => bakedTexture;

    /// <summary>Component layers</summary>
    public List<AppearanceManager.TextureData> Textures => textures;

    /// <summary>Width of the final baked image and scratchpad</summary>
    public int BakeWidth => bakeWidth;

    /// <summary>Height of the final baked image and scratchpad</summary>
    public int BakeHeight => bakeHeight;

    /// <summary>Bake type</summary>
    public BakeType BakeType => bakeType;

    /// <summary>Is this one of the 3 skin bakes</summary>
    private bool IsSkin =>
        bakeType == BakeType.Head || bakeType == BakeType.LowerBody || bakeType == BakeType.UpperBody;

    #endregion

    #region Private fields

    /// <summary>Final baked texture</summary>
    private AssetTexture bakedTexture;

    /// <summary>Component layers</summary>
    private readonly List<AppearanceManager.TextureData> textures = new();

    /// <summary>Width of the final baked image and scratchpad</summary>
    private readonly int bakeWidth;

    /// <summary>Height of the final baked image and scratchpad</summary>
    private readonly int bakeHeight;

    /// <summary>Bake type</summary>
    private readonly BakeType bakeType;

    #endregion

    #region Public methods

    /// <summary>
    ///     Adds layer for baking
    /// </summary>
    /// <param name="tdata">TexturaData struct that contains texture and its params</param>
    public void AddTexture(AppearanceManager.TextureData tdata)
    {
        lock (textures)
        {
            textures.Add(tdata);
        }
    }

    public void Bake()
    {
        bakedTexture = new AssetTexture(new ManagedImage(bakeWidth, bakeHeight,
            ManagedImage.ImageChannels.Color | ManagedImage.ImageChannels.Alpha | ManagedImage.ImageChannels.Bump));

        // These are for head baking, they get special treatment
        var skinTexture = new AppearanceManager.TextureData();
        var tattooTextures = new List<AppearanceManager.TextureData>();
        var alphaWearableTextures = new List<ManagedImage>();

        // Base color for eye bake is white, color of layer0 for others
        if (bakeType == BakeType.Eyes)
            InitBakedLayerColor(Color4.White);
        else if (textures.Count > 0) InitBakedLayerColor(textures[0].Color);

        // Sort out the special layers we need for head baking and alpha
        foreach (var tex in textures)
        {
            if (tex.Texture == null)
                continue;

            if (tex.TextureIndex == AvatarTextureIndex.HeadBodypaint ||
                tex.TextureIndex == AvatarTextureIndex.UpperBodypaint ||
                tex.TextureIndex == AvatarTextureIndex.LowerBodypaint)
                skinTexture = tex;

            if (tex.TextureIndex == AvatarTextureIndex.HeadTattoo ||
                tex.TextureIndex == AvatarTextureIndex.UpperTattoo ||
                tex.TextureIndex == AvatarTextureIndex.LowerTattoo)
                tattooTextures.Add(tex);

            if (tex.TextureIndex >= AvatarTextureIndex.LowerAlpha &&
                tex.TextureIndex <= AvatarTextureIndex.HairAlpha)
                if (tex.Texture.Image.Alpha != null)
                    alphaWearableTextures.Add(tex.Texture.Image.Clone());
        }

        if (bakeType == BakeType.Head)
        {
            DrawLayer(LoadResourceLayer("head_color.tga"), false);
            AddAlpha(bakedTexture.Image, LoadResourceLayer("head_alpha.tga"));
            MultiplyLayerFromAlpha(bakedTexture.Image, LoadResourceLayer("head_skingrain.tga"));
        }

        if (skinTexture.Texture == null)
        {
            if (bakeType == BakeType.UpperBody) DrawLayer(LoadResourceLayer("upperbody_color.tga"), false);

            if (bakeType == BakeType.LowerBody) DrawLayer(LoadResourceLayer("lowerbody_color.tga"), false);
        }

        // Layer each texture on top of one other, applying alpha masks as we go
        for (var i = 0; i < textures.Count; i++)
        {
            // Skip if we have no texture on this layer
            if (textures[i].Texture == null) continue;

            // Is this Alpha wearable and does it have an alpha channel?
            if (textures[i].TextureIndex >= AvatarTextureIndex.LowerAlpha &&
                textures[i].TextureIndex <= AvatarTextureIndex.HairAlpha)
                continue;

            // Don't draw skin and tattoo on head bake first
            // For head bake the skin and texture are drawn last, go figure
            if (bakeType == BakeType.Head &&
                (textures[i].TextureIndex == AvatarTextureIndex.HeadBodypaint ||
                 textures[i].TextureIndex == AvatarTextureIndex.HeadTattoo))
                continue;

            var texture = textures[i].Texture.Image.Clone();
            //File.WriteAllBytes(bakeType + "-texture-layer-" + textures[i].TextureIndex + "-" + i + ".tga", texture.ExportTGA());

            // Resize texture to the size of baked layer
            // FIXME: if texture is smaller than the layer, don't stretch it, tile it
            if (texture.Width != bakeWidth || texture.Height != bakeHeight)
                try
                {
                    texture.ResizeNearestNeighbor(bakeWidth, bakeHeight);
                }
                catch (Exception)
                {
                    continue;
                }

            // Special case for hair layer for the head bake
            // If we don't have skin texture, we discard hair alpha
            // and apply hair(i==2) pattern over the texture
            if (skinTexture.Texture == null && bakeType == BakeType.Head &&
                textures[i].TextureIndex == AvatarTextureIndex.Hair)
            {
                if (texture.Alpha != null)
                    for (var j = 0; j < texture.Alpha.Length; j++)
                        texture.Alpha[j] = 255;
                MultiplyLayerFromAlpha(texture, LoadResourceLayer("head_hair.tga"));
            }

            var processingSkin = true;

            // Aply tint and alpha masks except for skin that has a texture
            // on layer 0 which always overrides other skin settings
            if (!(textures[i].TextureIndex == AvatarTextureIndex.HeadBodypaint ||
                  textures[i].TextureIndex == AvatarTextureIndex.UpperBodypaint ||
                  textures[i].TextureIndex == AvatarTextureIndex.LowerBodypaint))
            {
                processingSkin = false;

                ApplyTint(texture, textures[i].Color);

                // For hair bake, we skip all alpha masks
                // and use one from the texture, for both
                // alpha and morph layers
                if (bakeType == BakeType.Hair)
                {
                    if (texture.Alpha != null)
                        bakedTexture.Image.Bump = texture.Alpha;
                    else
                        for (var j = 0; j < bakedTexture.Image.Bump.Length; j++)
                            bakedTexture.Image.Bump[j] = byte.MaxValue;
                }
                // Apply parametrized alpha masks
                else if (textures[i].AlphaMasks != null && textures[i].AlphaMasks.Count > 0)
                {
                    // Combined mask for the layer, fully transparent to begin with
                    var combinedMask = new ManagedImage(bakeWidth, bakeHeight, ManagedImage.ImageChannels.Alpha);

                    var addedMasks = 0;

                    // First add mask in normal blend mode
                    foreach (var kvp in textures[i].AlphaMasks)
                    {
                        if (!MaskBelongsToBake(kvp.Key.TGAFile)) continue;

                        if (kvp.Key.MultiplyBlend == false && (kvp.Value > 0f || !kvp.Key.SkipIfZero))
                        {
                            ApplyAlpha(combinedMask, kvp.Key, kvp.Value);
                            //File.WriteAllBytes(bakeType + "-layer-" + i + "-mask-" + addedMasks + ".tga", combinedMask.ExportTGA());
                            addedMasks++;
                        }
                    }

                    // If there were no mask in normal blend mode make aplha fully opaque
                    if (addedMasks == 0)
                        for (var l = 0; l < combinedMask.Alpha.Length; l++)
                            combinedMask.Alpha[l] = 255;

                    // Add masks in multiply blend mode
                    foreach (var kvp in textures[i].AlphaMasks)
                    {
                        if (!MaskBelongsToBake(kvp.Key.TGAFile)) continue;

                        if (kvp.Key.MultiplyBlend && (kvp.Value > 0f || !kvp.Key.SkipIfZero))
                        {
                            ApplyAlpha(combinedMask, kvp.Key, kvp.Value);
                            //File.WriteAllBytes(bakeType + "-layer-" + i + "-mask-" + addedMasks + ".tga", combinedMask.ExportTGA());
                            addedMasks++;
                        }
                    }

                    if (addedMasks > 0)
                        // Apply combined alpha mask to the cloned texture
                        AddAlpha(texture, combinedMask);

                    // Is this layer used for morph mask? If it is, use its
                    // alpha as the morth for the whole bake
                    if (Textures[i].TextureIndex == AppearanceManager.MorphLayerForBakeType(bakeType))
                        bakedTexture.Image.Bump = texture.Alpha;

                    //File.WriteAllBytes(bakeType + "-masked-texture-" + i + ".tga", texture.ExportTGA());
                }
            }

            var useAlpha = i == 0 && (BakeType == BakeType.Skirt || BakeType == BakeType.Hair);
            DrawLayer(texture, useAlpha);
            //File.WriteAllBytes(bakeType + "-layer-" + i + ".tga", texture.ExportTGA());
        }

        // For head and tattoo, we add skin last
        if (bakeType == BakeType.Head)
        {
            if (skinTexture.Texture != null)
            {
                var texture = skinTexture.Texture.Image.Clone();
                if (texture.Width != bakeWidth || texture.Height != bakeHeight)
                    try
                    {
                        texture.ResizeNearestNeighbor(bakeWidth, bakeHeight);
                    }
                    catch (Exception)
                    {
                    }

                DrawLayer(texture, false);
            }

            foreach (var tex in tattooTextures)
                // Add head tattoo here (if available, order-dependant)
                if (tex.Texture != null)
                {
                    var texture = tex.Texture.Image.Clone();
                    if (texture.Width != bakeWidth || texture.Height != bakeHeight)
                        try
                        {
                            texture.ResizeNearestNeighbor(bakeWidth, bakeHeight);
                        }
                        catch (Exception)
                        {
                        }

                    DrawLayer(texture, false);
                }
        }

        // Apply any alpha wearable textures to make parts of the avatar disappear
        m_log.DebugFormat("[XBakes]: Number of alpha wearable textures: {0}", alphaWearableTextures.Count);
        foreach (var img in alphaWearableTextures)
            AddAlpha(bakedTexture.Image, img);

        // We are done, encode asset for finalized bake
        bakedTexture.Encode();
        //File.WriteAllBytes(bakeType + ".tga", bakedTexture.Image.ExportTGA());
    }

    private static readonly object ResourceSync = new();

    public static ManagedImage LoadResourceLayer(string fileName)
    {
        try
        {
            AnyBitmap bitmap = null;
            lock (ResourceSync)
            {
                using (var stream = Helpers.GetResourceStream(fileName, Settings.RESOURCE_DIR))
                {
                    bitmap = LoadTGAClass.LoadTGA(stream);
                }
            }

            if (bitmap == null)
            {
                Logger.Log(string.Format("Failed loading resource file: {0}", fileName), Helpers.LogLevel.Error);
                return null;
            }

            return new ManagedImage(bitmap);
        }
        catch (Exception e)
        {
            Logger.Log(string.Format("Failed loading resource file: {0} ({1})", fileName, e.Message),
                Helpers.LogLevel.Error, e);
            return null;
        }
    }

    /// <summary>
    ///     Converts avatar texture index (face) to Bake type
    /// </summary>
    /// <param name="index">Face number (AvatarTextureIndex)</param>
    /// <returns>BakeType, layer to which this texture belongs to</returns>
    public static BakeType BakeTypeFor(AvatarTextureIndex index)
    {
        switch (index)
        {
            case AvatarTextureIndex.HeadBodypaint:
                return BakeType.Head;

            case AvatarTextureIndex.UpperBodypaint:
            case AvatarTextureIndex.UpperGloves:
            case AvatarTextureIndex.UpperUndershirt:
            case AvatarTextureIndex.UpperShirt:
            case AvatarTextureIndex.UpperJacket:
                return BakeType.UpperBody;

            case AvatarTextureIndex.LowerBodypaint:
            case AvatarTextureIndex.LowerUnderpants:
            case AvatarTextureIndex.LowerSocks:
            case AvatarTextureIndex.LowerShoes:
            case AvatarTextureIndex.LowerPants:
            case AvatarTextureIndex.LowerJacket:
                return BakeType.LowerBody;

            case AvatarTextureIndex.EyesIris:
                return BakeType.Eyes;

            case AvatarTextureIndex.Skirt:
                return BakeType.Skirt;

            case AvatarTextureIndex.Hair:
                return BakeType.Hair;

            default:
                return BakeType.Unknown;
        }
    }

    #endregion

    #region Private layer compositing methods

    private bool MaskBelongsToBake(string mask)
    {
        if ((bakeType == BakeType.LowerBody && mask.Contains("upper"))
            || (bakeType == BakeType.LowerBody && mask.Contains("shirt"))
            || (bakeType == BakeType.UpperBody && mask.Contains("lower")))
            return false;
        return true;
    }

    private bool DrawLayer(ManagedImage source, bool addSourceAlpha)
    {
        if (source == null) return false;

        bool sourceHasColor;
        bool sourceHasAlpha;
        bool sourceHasBump;
        var i = 0;

        sourceHasColor = (source.Channels & ManagedImage.ImageChannels.Color) != 0 &&
                         source.Red != null && source.Green != null && source.Blue != null;
        sourceHasAlpha = (source.Channels & ManagedImage.ImageChannels.Alpha) != 0 && source.Alpha != null;
        sourceHasBump = (source.Channels & ManagedImage.ImageChannels.Bump) != 0 && source.Bump != null;

        addSourceAlpha = addSourceAlpha && sourceHasAlpha;

        var alpha = byte.MaxValue;
        var alphaInv = (byte)(byte.MaxValue - alpha);

        var bakedRed = bakedTexture.Image.Red;
        var bakedGreen = bakedTexture.Image.Green;
        var bakedBlue = bakedTexture.Image.Blue;
        var bakedAlpha = bakedTexture.Image.Alpha;
        var bakedBump = bakedTexture.Image.Bump;

        var sourceRed = source.Red;
        var sourceGreen = source.Green;
        var sourceBlue = source.Blue;
        var sourceAlpha = sourceHasAlpha ? source.Alpha : null;
        var sourceBump = sourceHasBump ? source.Bump : null;

        for (var y = 0; y < bakeHeight; y++)
        for (var x = 0; x < bakeWidth; x++)
        {
            if (sourceHasAlpha)
            {
                alpha = sourceAlpha[i];
                alphaInv = (byte)(byte.MaxValue - alpha);
            }

            if (sourceHasColor)
            {
                bakedRed[i] = (byte)((bakedRed[i] * alphaInv + sourceRed[i] * alpha) >> 8);
                bakedGreen[i] = (byte)((bakedGreen[i] * alphaInv + sourceGreen[i] * alpha) >> 8);
                bakedBlue[i] = (byte)((bakedBlue[i] * alphaInv + sourceBlue[i] * alpha) >> 8);
            }

            if (addSourceAlpha)
                if (sourceAlpha[i] < bakedAlpha[i])
                    bakedAlpha[i] = sourceAlpha[i];

            if (sourceHasBump)
                bakedBump[i] = sourceBump[i];

            ++i;
        }

        return true;
    }

    /// <summary>
    ///     Make sure images exist, resize source if needed to match the destination
    /// </summary>
    /// <param name="dest">Destination image</param>
    /// <param name="src">Source image</param>
    /// <returns>Sanitization was succefull</returns>
    private bool SanitizeLayers(ManagedImage dest, ManagedImage src)
    {
        if (dest == null || src == null) return false;

        if ((dest.Channels & ManagedImage.ImageChannels.Alpha) == 0)
            dest.ConvertChannels(dest.Channels | ManagedImage.ImageChannels.Alpha);

        if (dest.Width != src.Width || dest.Height != src.Height)
            try
            {
                src.ResizeNearestNeighbor(dest.Width, dest.Height);
            }
            catch (Exception)
            {
                return false;
            }

        return true;
    }


    private void ApplyAlpha(ManagedImage dest, VisualAlphaParam param, float val)
    {
        var src = LoadResourceLayer(param.TGAFile);

        if (dest == null || src == null || src.Alpha == null) return;

        if ((dest.Channels & ManagedImage.ImageChannels.Alpha) == 0)
            dest.ConvertChannels(ManagedImage.ImageChannels.Alpha | dest.Channels);

        if (dest.Width != src.Width || dest.Height != src.Height)
            try
            {
                src.ResizeNearestNeighbor(dest.Width, dest.Height);
            }
            catch (Exception)
            {
                return;
            }

        for (var i = 0; i < dest.Alpha.Length; i++)
        {
            var alpha = src.Alpha[i] <= (1 - val) * 255 ? (byte)0 : (byte)255;

            if (param.MultiplyBlend)
            {
                dest.Alpha[i] = (byte)((dest.Alpha[i] * alpha) >> 8);
            }
            else
            {
                if (alpha > dest.Alpha[i]) dest.Alpha[i] = alpha;
            }
        }
    }

    private void AddAlpha(ManagedImage dest, ManagedImage src)
    {
        if (!SanitizeLayers(dest, src)) return;

        for (var i = 0; i < dest.Alpha.Length; i++)
            if (src.Alpha[i] < dest.Alpha[i])
                dest.Alpha[i] = src.Alpha[i];
    }

    private void MultiplyLayerFromAlpha(ManagedImage dest, ManagedImage src)
    {
        if (!SanitizeLayers(dest, src)) return;

        for (var i = 0; i < dest.Red.Length; i++)
        {
            dest.Red[i] = (byte)((dest.Red[i] * src.Alpha[i]) >> 8);
            dest.Green[i] = (byte)((dest.Green[i] * src.Alpha[i]) >> 8);
            dest.Blue[i] = (byte)((dest.Blue[i] * src.Alpha[i]) >> 8);
        }
    }

    private void ApplyTint(ManagedImage dest, Color4 src)
    {
        if (dest == null) return;

        for (var i = 0; i < dest.Red.Length; i++)
        {
            dest.Red[i] = (byte)((dest.Red[i] * Utils.FloatZeroOneToByte(src.R)) >> 8);
            dest.Green[i] = (byte)((dest.Green[i] * Utils.FloatZeroOneToByte(src.G)) >> 8);
            dest.Blue[i] = (byte)((dest.Blue[i] * Utils.FloatZeroOneToByte(src.B)) >> 8);
        }
    }

    /// <summary>
    ///     Fills a baked layer as a solid *appearing* color. The colors are
    ///     subtly dithered on a 16x16 grid to prevent the JPEG2000 stage from
    ///     compressing it too far since it seems to cause upload failures if
    ///     the image is a pure solid color
    /// </summary>
    /// <param name="color">Color of the base of this layer</param>
    private void InitBakedLayerColor(Color4 color)
    {
        InitBakedLayerColor(color.R, color.G, color.B);
    }

    /// <summary>
    ///     Fills a baked layer as a solid *appearing* color. The colors are
    ///     subtly dithered on a 16x16 grid to prevent the JPEG2000 stage from
    ///     compressing it too far since it seems to cause upload failures if
    ///     the image is a pure solid color
    /// </summary>
    /// <param name="r">Red value</param>
    /// <param name="g">Green value</param>
    /// <param name="b">Blue value</param>
    private void InitBakedLayerColor(float r, float g, float b)
    {
        var rByte = Utils.FloatZeroOneToByte(r);
        var gByte = Utils.FloatZeroOneToByte(g);
        var bByte = Utils.FloatZeroOneToByte(b);

        byte rAlt, gAlt, bAlt;

        rAlt = rByte;
        gAlt = gByte;
        bAlt = bByte;

        if (rByte < byte.MaxValue)
            rAlt++;
        else rAlt--;

        if (gByte < byte.MaxValue)
            gAlt++;
        else gAlt--;

        if (bByte < byte.MaxValue)
            bAlt++;
        else bAlt--;

        var i = 0;

        var red = bakedTexture.Image.Red;
        var green = bakedTexture.Image.Green;
        var blue = bakedTexture.Image.Blue;
        var alpha = bakedTexture.Image.Alpha;
        var bump = bakedTexture.Image.Bump;

        for (var y = 0; y < bakeHeight; y++)
        for (var x = 0; x < bakeWidth; x++)
        {
            if (((x ^ y) & 0x10) == 0)
            {
                red[i] = rAlt;
                green[i] = gByte;
                blue[i] = bByte;
                alpha[i] = byte.MaxValue;
                bump[i] = 0;
            }
            else
            {
                red[i] = rByte;
                green[i] = gAlt;
                blue[i] = bAlt;
                alpha[i] = byte.MaxValue;
                bump[i] = 0;
            }

            ++i;
        }
    }

    #endregion
}