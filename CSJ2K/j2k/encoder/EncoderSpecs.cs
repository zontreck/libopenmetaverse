/*
* CVS identifier:
*
* $Id: EncoderSpecs.java,v 1.35 2001/05/08 16:10:40 grosbois Exp $
*
* Class:                   EncoderSpecs
*
* Description:             Hold all encoder specifications
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

using CSJ2K.j2k.entropy;
using CSJ2K.j2k.image;
using CSJ2K.j2k.image.forwcomptransf;
using CSJ2K.j2k.quantization;
using CSJ2K.j2k.quantization.quantizer;
using CSJ2K.j2k.roi;
using CSJ2K.j2k.util;
using CSJ2K.j2k.wavelet.analysis;

namespace CSJ2K.j2k.encoder;

/// <summary>
///     This class holds references to each module specifications used in the
///     encoding chain. This avoid big amount of arguments in method calls. A
///     specification contains values of each tile-component for one module. All
///     members must be instance of ModuleSpec class (or its children).
/// </summary>
/// <seealso cref="ModuleSpec">
/// </seealso>
public class EncoderSpecs
{
    /// <summary>By-pass mode specifications </summary>
    public StringSpec bms;

    /// <summary>Code-blocks sizes specification </summary>
    public CBlkSizeSpec cblks;

    /// <summary>Causal stripes specifications </summary>
    public StringSpec css;

    /// <summary>Component transformation specifications </summary>
    public CompTransfSpec cts;

    /// <summary>Number of decomposition levels specifications </summary>
    public IntegerSpec dls;

    /// <summary>End of packet header (EPH) marker use specification </summary>
    public StringSpec ephs;

    /// <summary>Number of guard bits specifications </summary>
    public GuardBitsSpec gbs;

    /// <summary>The length calculation specifications </summary>
    public StringSpec lcs;

    /// <summary>MQ reset specifications </summary>
    public StringSpec mqrs;

    /// <summary>The number of components within the image </summary>
    public int nComp;

    /// <summary>The number of tiles within the image </summary>
    public int nTiles;

    /// <summary>Progression/progression changes specification </summary>
    public ProgressionSpec pocs;

    /// <summary>Precinct partition specifications </summary>
    public PrecinctSizeSpec pss;

    /// <summary>Quantization normalized base step size specifications </summary>
    public QuantStepSizeSpec qsss;

    /// <summary>Quantization type specifications </summary>
    public QuantTypeSpec qts;

    /// <summary>ROI maxshift value specifications </summary>
    public MaxShiftSpec rois;

    /// <summary>Regular termination specifications </summary>
    public StringSpec rts;

    /// <summary>Start of packet (SOP) marker use specification </summary>
    public StringSpec sops;

    /// <summary>Error resilience segment symbol use specifications </summary>
    public StringSpec sss;

    /// <summary>The termination type specifications </summary>
    public StringSpec tts;

    /// <summary>Analysis wavelet filters specifications </summary>
    public AnWTFilterSpec wfs;

    /// <summary>
    ///     Initialize all members with the given number of tiles and components
    ///     and the command-line arguments stored in a ParameterList instance
    /// </summary>
    /// <param name="nt">
    ///     Number of tiles
    /// </param>
    /// <param name="nc">
    ///     Number of components
    /// </param>
    /// <param name="imgsrc">
    ///     The image source (used to get the image size)
    /// </param>
    /// <param name="pl">
    ///     The ParameterList instance
    /// </param>
    public EncoderSpecs(int nt, int nc, BlkImgDataSrc imgsrc, ParameterList pl)
    {
        nTiles = nt;
        nComp = nc;

        // ROI
        rois = new MaxShiftSpec(nt, nc, ModuleSpec.SPEC_TYPE_TILE_COMP);

        // Quantization
        pl.checkList(Quantizer.OPT_PREFIX, ParameterList.toNameArray(Quantizer.ParameterInfo));
        qts = new QuantTypeSpec(nt, nc, ModuleSpec.SPEC_TYPE_TILE_COMP, pl);
        qsss = new QuantStepSizeSpec(nt, nc, ModuleSpec.SPEC_TYPE_TILE_COMP, pl);
        gbs = new GuardBitsSpec(nt, nc, ModuleSpec.SPEC_TYPE_TILE_COMP, pl);

        // Wavelet transform
        wfs = new AnWTFilterSpec(nt, nc, ModuleSpec.SPEC_TYPE_TILE_COMP, qts, pl);
        dls = new IntegerSpec(nt, nc, ModuleSpec.SPEC_TYPE_TILE_COMP, pl, "Wlev");

        // Component transformation
        cts = new ForwCompTransfSpec(nt, nc, ModuleSpec.SPEC_TYPE_TILE, wfs, pl);

        // Entropy coder
        string[] strLcs = { "near_opt", "lazy_good", "lazy" };
        lcs = new StringSpec(nt, nc, ModuleSpec.SPEC_TYPE_TILE_COMP, "Clen_calc", strLcs, pl);
        string[] strTerm = { "near_opt", "easy", "predict", "full" };
        tts = new StringSpec(nt, nc, ModuleSpec.SPEC_TYPE_TILE_COMP, "Cterm_type", strTerm, pl);
        string[] strBoolean = { "on", "off" };
        sss = new StringSpec(nt, nc, ModuleSpec.SPEC_TYPE_TILE_COMP, "Cseg_symbol", strBoolean, pl);
        css = new StringSpec(nt, nc, ModuleSpec.SPEC_TYPE_TILE_COMP, "Ccausal", strBoolean, pl);
        rts = new StringSpec(nt, nc, ModuleSpec.SPEC_TYPE_TILE_COMP, "Cterminate", strBoolean, pl);
        mqrs = new StringSpec(nt, nc, ModuleSpec.SPEC_TYPE_TILE_COMP, "CresetMQ", strBoolean, pl);
        bms = new StringSpec(nt, nc, ModuleSpec.SPEC_TYPE_TILE_COMP, "Cbypass", strBoolean, pl);
        cblks = new CBlkSizeSpec(nt, nc, ModuleSpec.SPEC_TYPE_TILE_COMP, pl);

        // Precinct partition
        pss = new PrecinctSizeSpec(nt, nc, ModuleSpec.SPEC_TYPE_TILE_COMP, imgsrc, dls, pl);

        // Codestream
        sops = new StringSpec(nt, nc, ModuleSpec.SPEC_TYPE_TILE, "Psop", strBoolean, pl);
        ephs = new StringSpec(nt, nc, ModuleSpec.SPEC_TYPE_TILE, "Peph", strBoolean, pl);
    }
}