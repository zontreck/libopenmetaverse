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
using IronSoftware.Drawing;
using OpenSim.Linq;

namespace OpenMetaverse.Imaging;

public class ManagedImage
{
    [Flags]
    public enum ImageChannels
    {
        Gray = 1,
        Color = 2,
        Alpha = 4,
        Bump = 8
    }

    public enum ImageResizeAlgorithm
    {
        NearestNeighbor
    }

    /// <summary>
    ///     Alpha channel data
    /// </summary>
    public byte[] Alpha;

    /// <summary>
    ///     Blue channel data
    /// </summary>
    public byte[] Blue;

    /// <summary>
    ///     Bump channel data
    /// </summary>
    public byte[] Bump;

    /// <summary>
    ///     Image channel flags
    /// </summary>
    public ImageChannels Channels;

    /// <summary>
    ///     Green channel data
    /// </summary>
    public byte[] Green;

    /// <summary>
    ///     Image height
    /// </summary>
    public int Height;

    /// <summary>
    ///     Red channel data
    /// </summary>
    public byte[] Red;

    /// <summary>
    ///     Image width
    /// </summary>
    public int Width;

    /// <summary>
    ///     Create a new blank image
    /// </summary>
    /// <param name="width">width</param>
    /// <param name="height">height</param>
    /// <param name="channels">channel flags</param>
    public ManagedImage(int width, int height, ImageChannels channels)
    {
        Width = width;
        Height = height;
        Channels = channels;

        var n = width * height;

        if ((channels & ImageChannels.Gray) != 0)
        {
            Red = new byte[n];
        }
        else if ((channels & ImageChannels.Color) != 0)
        {
            Red = new byte[n];
            Green = new byte[n];
            Blue = new byte[n];
        }

        if ((channels & ImageChannels.Alpha) != 0)
            Alpha = new byte[n];

        if ((channels & ImageChannels.Bump) != 0)
            Bump = new byte[n];
    }

    /// <summary>
    /// </summary>
    /// <param name="bitmap"></param>
    public ManagedImage(AnyBitmap bitmap)
    {
        Width = bitmap.Width;
        Height = bitmap.Height;

        var pixelCount = Width * Height;

        Channels = ImageChannels.Alpha | ImageChannels.Color;
        Red = new byte[pixelCount];
        Green = new byte[pixelCount];
        Blue = new byte[pixelCount];
        Alpha = new byte[pixelCount];

        var colors = bitmap.GetAllColors();

        var i = 0;
        foreach (var color in colors)
        {
            Red[i] = color.R;
            Green[i] = color.G;
            Blue[i] = color.B;
            Alpha[i] = color.A;

            i++;
        }

        if (Alpha.AllAreIdentical())
        {
            Alpha = null;
            Channels = ImageChannels.Color;
        }
    }

    /// <summary>
    ///     Convert the channels in the image. Channels are created or destroyed as required.
    /// </summary>
    /// <param name="channels">new channel flags</param>
    public void ConvertChannels(ImageChannels channels)
    {
        if (Channels == channels)
            return;

        var n = Width * Height;
        var add = Channels ^ (channels & channels);
        var del = Channels ^ (channels & Channels);

        if ((add & ImageChannels.Color) != 0)
        {
            Red = new byte[n];
            Green = new byte[n];
            Blue = new byte[n];
        }
        else if ((del & ImageChannels.Color) != 0)
        {
            Red = null;
            Green = null;
            Blue = null;
        }

        if ((add & ImageChannels.Alpha) != 0)
        {
            Alpha = new byte[n];
            FillArray(Alpha, 255);
        }
        else if ((del & ImageChannels.Alpha) != 0)
        {
            Alpha = null;
        }

        if ((add & ImageChannels.Bump) != 0)
            Bump = new byte[n];
        else if ((del & ImageChannels.Bump) != 0)
            Bump = null;

        Channels = channels;
    }

    /// <summary>
    ///     Resize or stretch the image using nearest neighbor (ugly) resampling
    /// </summary>
    /// <param name="width">new width</param>
    /// <param name="height">new height</param>
    public void ResizeNearestNeighbor(int width, int height)
    {
        if (width == Width && height == Height)
            return;

        byte[]
            red = null,
            green = null,
            blue = null,
            alpha = null,
            bump = null;
        var n = width * height;
        int di = 0, si;

        if (Red != null) red = new byte[n];
        if (Green != null) green = new byte[n];
        if (Blue != null) blue = new byte[n];
        if (Alpha != null) alpha = new byte[n];
        if (Bump != null) bump = new byte[n];

        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
        {
            si = y * Height / height * Width + x * Width / width;
            if (Red != null) red[di] = Red[si];
            if (Green != null) green[di] = Green[si];
            if (Blue != null) blue[di] = Blue[si];
            if (Alpha != null) alpha[di] = Alpha[si];
            if (Bump != null) bump[di] = Bump[si];
            di++;
        }

        Width = width;
        Height = height;
        Red = red;
        Green = green;
        Blue = blue;
        Alpha = alpha;
        Bump = bump;
    }

    /// <summary>
    ///     Create a byte array containing 32-bit RGBA data with a bottom-left
    ///     origin, suitable for feeding directly into OpenGL
    /// </summary>
    /// <returns>A byte array containing raw texture data</returns>
    public byte[] ExportRaw()
    {
        var raw = new byte[Width * Height * 4];

        if ((Channels & ImageChannels.Alpha) != 0)
        {
            if ((Channels & ImageChannels.Color) != 0)
                // RGBA
                for (var h = 0; h < Height; h++)
                for (var w = 0; w < Width; w++)
                {
                    var pos = (Height - 1 - h) * Width + w;
                    var srcPos = h * Width + w;

                    raw[pos * 4 + 0] = Red[srcPos];
                    raw[pos * 4 + 1] = Green[srcPos];
                    raw[pos * 4 + 2] = Blue[srcPos];
                    raw[pos * 4 + 3] = Alpha[srcPos];
                }
            else
                // Alpha only
                for (var h = 0; h < Height; h++)
                for (var w = 0; w < Width; w++)
                {
                    var pos = (Height - 1 - h) * Width + w;
                    var srcPos = h * Width + w;

                    raw[pos * 4 + 0] = Alpha[srcPos];
                    raw[pos * 4 + 1] = Alpha[srcPos];
                    raw[pos * 4 + 2] = Alpha[srcPos];
                    raw[pos * 4 + 3] = byte.MaxValue;
                }
        }
        else
        {
            // RGB
            for (var h = 0; h < Height; h++)
            for (var w = 0; w < Width; w++)
            {
                var pos = (Height - 1 - h) * Width + w;
                var srcPos = h * Width + w;

                raw[pos * 4 + 0] = Red[srcPos];
                raw[pos * 4 + 1] = Green[srcPos];
                raw[pos * 4 + 2] = Blue[srcPos];
                raw[pos * 4 + 3] = byte.MaxValue;
            }
        }

        return raw;
    }

    /// <summary>
    ///     Create a byte array containing 32-bit RGBA data with a bottom-left
    ///     origin, suitable for feeding directly into OpenGL
    /// </summary>
    /// <returns>A byte array containing raw texture data</returns>
    public AnyBitmap ExportBitmap()
    {
        var raw = new byte[Width * Height * 4];

        if ((Channels & ImageChannels.Alpha) != 0)
        {
            if ((Channels & ImageChannels.Color) != 0)
                // RGBA
                for (var pos = 0; pos < Height * Width; pos++)
                {
                    raw[pos * 4 + 0] = Blue[pos];
                    raw[pos * 4 + 1] = Green[pos];
                    raw[pos * 4 + 2] = Red[pos];
                    raw[pos * 4 + 3] = Alpha[pos];
                }
            else
                // Alpha only
                for (var pos = 0; pos < Height * Width; pos++)
                {
                    raw[pos * 4 + 0] = Alpha[pos];
                    raw[pos * 4 + 1] = Alpha[pos];
                    raw[pos * 4 + 2] = Alpha[pos];
                    raw[pos * 4 + 3] = byte.MaxValue;
                }
        }
        else
        {
            // RGB
            for (var pos = 0; pos < Height * Width; pos++)
            {
                raw[pos * 4 + 0] = Blue[pos];
                raw[pos * 4 + 1] = Green[pos];
                raw[pos * 4 + 2] = Red[pos];
                raw[pos * 4 + 3] = byte.MaxValue;
            }
        }

        return AnyBitmap.FromBytes(raw);
    }

    public byte[] ExportTGA()
    {
        var tga = new byte[Width * Height * ((Channels & ImageChannels.Alpha) == 0 ? 3 : 4) + 32];
        var di = 0;
        tga[di++] = 0; // idlength
        tga[di++] = 0; // colormaptype = 0: no colormap
        tga[di++] = 2; // image type = 2: uncompressed RGB
        tga[di++] = 0; // color map spec is five zeroes for no color map
        tga[di++] = 0; // color map spec is five zeroes for no color map
        tga[di++] = 0; // color map spec is five zeroes for no color map
        tga[di++] = 0; // color map spec is five zeroes for no color map
        tga[di++] = 0; // color map spec is five zeroes for no color map
        tga[di++] = 0; // x origin = two bytes
        tga[di++] = 0; // x origin = two bytes
        tga[di++] = 0; // y origin = two bytes
        tga[di++] = 0; // y origin = two bytes
        tga[di++] = (byte)(Width & 0xFF); // width - low byte
        tga[di++] = (byte)(Width >> 8); // width - hi byte
        tga[di++] = (byte)(Height & 0xFF); // height - low byte
        tga[di++] = (byte)(Height >> 8); // height - hi byte
        tga[di++] = (byte)((Channels & ImageChannels.Alpha) == 0 ? 24 : 32); // 24/32 bits per pixel
        tga[di++] = (byte)((Channels & ImageChannels.Alpha) == 0 ? 32 : 40); // image descriptor byte

        var n = Width * Height;

        if ((Channels & ImageChannels.Alpha) != 0)
        {
            if ((Channels & ImageChannels.Color) != 0)
                // RGBA
                for (var i = 0; i < n; i++)
                {
                    tga[di++] = Blue[i];
                    tga[di++] = Green[i];
                    tga[di++] = Red[i];
                    tga[di++] = Alpha[i];
                }
            else
                // Alpha only
                for (var i = 0; i < n; i++)
                {
                    tga[di++] = Alpha[i];
                    tga[di++] = Alpha[i];
                    tga[di++] = Alpha[i];
                    tga[di++] = byte.MaxValue;
                }
        }
        else
        {
            // RGB
            for (var i = 0; i < n; i++)
            {
                tga[di++] = Blue[i];
                tga[di++] = Green[i];
                tga[di++] = Red[i];
            }
        }

        return tga;
    }

    private static void FillArray(byte[] array, byte value)
    {
        if (array != null)
            for (var i = 0; i < array.Length; i++)
                array[i] = value;
    }

    public void Clear()
    {
        FillArray(Red, 0);
        FillArray(Green, 0);
        FillArray(Blue, 0);
        FillArray(Alpha, 0);
        FillArray(Bump, 0);
    }

    public ManagedImage Clone()
    {
        var image = new ManagedImage(Width, Height, Channels);
        if (Red != null) image.Red = (byte[])Red.Clone();
        if (Green != null) image.Green = (byte[])Green.Clone();
        if (Blue != null) image.Blue = (byte[])Blue.Clone();
        if (Alpha != null) image.Alpha = (byte[])Alpha.Clone();
        if (Bump != null) image.Bump = (byte[])Bump.Clone();
        return image;
    }
}