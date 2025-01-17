/*
* CVS identifier:
*
* $Id: CoordInfo.java,v 1.10 2001/09/14 09:32:11 grosbois Exp $
*
* Class:                   CoordInfo
*
* Description: Used to store the coordinates of code-blocks/packets
*
*
*
* COPYRIGHT:
* 
* This software module was originally developed by Rapha�l Grosbois and
* Diego Santa Cruz (Swiss Federal Institute of Technology-EPFL); Joel
* Askel�f (Ericsson Radio Systems AB); and Bertrand Berthelot, David
* Bouchard, F�lix Henry, Gerard Mozelle and Patrice Onno (Canon Research
* Centre France S.A) in the course of development of the JPEG2000
* standard as specified by ISO/IEC 15444 (JPEG 2000 Standard). This
* software module is an implementation of a part of the JPEG 2000
* Standard. Swiss Federal Institute of Technology-EPFL, Ericsson Radio
* Systems AB and Canon Research Centre France S.A (collectively JJ2000
* Partners) agree not to assert against ISO/IEC and users of the JPEG
* 2000 Standard (Users) any of their rights under the copyright, not
* including other intellectual property rights, for this software module
* with respect to the usage by ISO/IEC and Users of this software module
* or modifications thereof for use in hardware or software products
* claiming conformance to the JPEG 2000 Standard. Those intending to use
* this software module in hardware or software products are advised that
* their use may infringe existing patents. The original developers of
* this software module, JJ2000 Partners and ISO/IEC assume no liability
* for use of this software module or modifications thereof. No license
* or right to this software module is granted for non JPEG 2000 Standard
* conforming products. JJ2000 Partners have full right to use this
* software module for his/her own purpose, assign or donate this
* software module to any third party and to inhibit third parties from
* using this software module for non JPEG 2000 Standard conforming
* products. This copyright notice must be included in all copies or
* derivative works of this software module.
* 
* Copyright (c) 1999/2000 JJ2000 Partners.
* */

namespace CSJ2K.j2k.codestream;

/// <summary>
///     This class is used to store the coordinates of objects such as code-blocks
///     or precincts. As this is an abstract class, it cannot be used directly but
///     derived classes have been created for code-blocks and packets
///     (CBlkCoordInfo and PrecCoordInfo).
/// </summary>
/// <seealso cref="PrecCoordInfo">
/// </seealso>
/// <seealso cref="CBlkCoordInfo">
/// </seealso>
public abstract class CoordInfo
{
    /// <summary>Object's height </summary>
    public int h;

    /// <summary>Horizontal upper left coordinate in the subband </summary>
    public int ulx;

    /// <summary>Vertical upper left coordinate in the subband </summary>
    public int uly;

    /// <summary>Object's width </summary>
    public int w;

    /// <summary>
    ///     Constructor. Creates a CoordInfo object.
    /// </summary>
    /// <param name="ulx">
    ///     The horizontal upper left coordinate in the subband
    /// </param>
    /// <param name="uly">
    ///     The vertical upper left coordinate in the subband
    /// </param>
    /// <param name="w">
    ///     The width
    /// </param>
    /// <param name="h">
    ///     The height
    /// </param>
    /// <param name="idx">
    ///     The object's index
    /// </param>
    public CoordInfo(int ulx, int uly, int w, int h)
    {
        this.ulx = ulx;
        this.uly = uly;
        this.w = w;
        this.h = h;
    }

    /// <summary>Empty contructor </summary>
    public CoordInfo()
    {
    }

    /// <summary>
    ///     Returns object's information in a String
    /// </summary>
    /// <returns>
    ///     String with object's information
    /// </returns>
    public override string ToString()
    {
        return "ulx=" + ulx + ",uly=" + uly + ",w=" + w + ",h=" + h;
    }
}