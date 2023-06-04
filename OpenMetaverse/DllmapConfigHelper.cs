/*
 * Copyright (c) Contributors, http://opensimulator.org/
 * See CONTRIBUTORS.TXT for a full list of copyright holders.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright
 *       notice, this list of conditions and the following disclaimer in the
 *       documentation and/or other materials provided with the distribution.
 *     * Neither the name of the OpenSimulator Project nor the
 *       names of its contributors may be used to endorse or promote products
 *       derived from this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY
 * EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
 * WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
 * DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.X86;
using System.Xml;
using System.Xml.Serialization;

namespace OpenMetaverse;

public static class DllmapConfigHelper
{
    private static readonly Dictionary<string, IntPtr> LoadedLibs = new();
    private static readonly HashSet<Assembly> RegisteredAssemblies = new();
    private static readonly object m_mainlock = new();

    public static void RegisterAssembly(Assembly assembly)
    {
        lock (m_mainlock)
        {
            if (RegisteredAssemblies.Contains(assembly))
                return;
            RegisteredAssemblies.Add(assembly);
        }

        var assemblyPath = Path.GetDirectoryName(assembly.Location);
        var path = Path.Combine(assemblyPath,
            Path.GetFileNameWithoutExtension(assembly.Location) + ".dll.config");
        var c = ParseConfig(path);
        if (c is null)
            return;

        var matchs = ProcessAssemblyConfiguration(c, out var libstoload);
        if (matchs == 0)
            return;
        if (libstoload.Count == 0)
            return;

        foreach (var (libname, libpath) in libstoload)
        {
            path = Path.Combine(assemblyPath, libpath);
            if (NativeLibrary.TryLoad(path, out var ptr))
                LoadedLibs.Add(libname, ptr);
            else
                LoadedLibs.Add(libname, IntPtr.Zero);
        }

        NativeLibrary.SetDllImportResolver(assembly, AssemblyDllImport);
    }

    private static IntPtr AssemblyDllImport(string libraryName, Assembly assembly,
        DllImportSearchPath? dllImportSearchPath)
    {
        if (LoadedLibs.TryGetValue(libraryName, out var ptr))
            return ptr;
        return IntPtr.Zero;
    }

    public static configuration ParseConfig(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        configuration c;
        try
        {
            if (!File.Exists(path))
                return null;
            using FileStream fs = new(path, FileMode.Open);
            using var reader = XmlReader.Create(fs);
            XmlSerializer serializer = new(typeof(configuration));
            c = (configuration)serializer.Deserialize(reader);
        }
        catch
        {
            return null;
        }

        if (c is null || c.maps is null || c.maps.Count == 0)
            return null;
        return c;
    }

    public static int ProcessAssemblyConfiguration(configuration c,
        out List<(string libname, string libpath)> libsToLoad)
    {
        libsToLoad = new List<(string libname, string libpath)>(c.maps.Count);
        var MatchCount = 0;
        HashSet<string> libsdone = new();
        foreach (var m in c.maps)
        {
            if (string.IsNullOrEmpty(m.target))
                continue;
            if (string.IsNullOrEmpty(m.os))
                continue;
            if (string.IsNullOrEmpty(m.dll))
                continue;
            if (libsdone.Contains(m.dll))
                continue;

            var match = false;
            var negate = m.os[0] == '!';
            if (negate)
                m.os = m.os[1..];
            var tos = m.os.Split(',');
            foreach (var s in tos)
            {
                match = OperatingSystem.IsOSPlatform(s);
                if (match)
                    break;
            }

            if (negate)
                match = !match;
            if (!match)
                continue;

            if (string.IsNullOrEmpty(m.cpu))
            {
                libsdone.Add(m.dll);
                MatchCount++;
                if (!LoadedLibs.ContainsKey(m.dll))
                    libsToLoad.Add((m.dll, m.target));
                break;
            }

            negate = m.cpu[0] == '!';
            if (negate)
                m.cpu = m.cpu[1..];
            m.cpu = m.cpu.ToLower();
            var tcpu = m.cpu.Split(',');
            match = false;
            foreach (var s in tcpu)
            {
                switch (s)
                {
                    case "x86":
                        if (RuntimeInformation.ProcessArchitecture == Architecture.X86)
                            match = true;
                        break;
                    case "x86avx":
                        if (Avx.IsSupported && RuntimeInformation.ProcessArchitecture == Architecture.X86)
                            match = true;
                        break;
                    case "x86-64":
                        if (RuntimeInformation.ProcessArchitecture == Architecture.X64)
                            match = true;
                        break;
                    case "x86-64avx":
                        if (Avx.IsSupported && RuntimeInformation.ProcessArchitecture == Architecture.X64)
                            match = true;
                        break;
                    case "arm":
                    case "aarch32":
                        if (RuntimeInformation.ProcessArchitecture == Architecture.Arm)
                            match = true;
                        break;
                    case "arm64":
                    case "armv8":
                    case "aarch64":
                        if (RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
                            match = true;
                        break;
                    case "s390":
                    case "s390x":
                        if (RuntimeInformation.ProcessArchitecture == Architecture.S390x)
                            match = true;
                        break;
                }

                if (match)
                    break;
            }

            if (negate)
                match = !match;

            if (match)
            {
                MatchCount++;
                libsdone.Add(m.dll);
                if (!LoadedLibs.ContainsKey(m.dll))
                    libsToLoad.Add((m.dll, m.target));
            }
        }

        return MatchCount;
    }

    public static void RegisterDll(Assembly assembly, string libname)
    {
        lock (m_mainlock)
        {
            if (RegisteredAssemblies.Contains(assembly))
                return;
            RegisteredAssemblies.Add(assembly);
        }

        if (!LoadedLibs.TryGetValue(libname, out var ptr))
        {
            var assemblyPath = Path.GetDirectoryName(assembly.Location);
            var path = Path.Combine(assemblyPath, libname + ".dllconfig");
            var c = ParseConfig(path);
            if (c is null)
                return;

            var libpath = ProcessDllConfiguration(c);
            if (string.IsNullOrEmpty(libpath))
            {
                LoadedLibs.Add(libname, IntPtr.Zero);
                return;
            }

            libpath = Path.Combine(assemblyPath, libpath);
            if (!NativeLibrary.TryLoad(libpath, out ptr))
            {
                LoadedLibs.Add(libname, IntPtr.Zero);
                return;
            }

            LoadedLibs.Add(libname, ptr);
        }

        if (ptr != IntPtr.Zero)
            NativeLibrary.SetDllImportResolver(assembly, AssemblyDllImport);
    }

    public static IntPtr LoadDll(string libname)
    {
        if (LoadedLibs.TryGetValue(libname, out var ptr))
            return ptr;

        var execpath = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);

        var path = Path.Combine(execpath, libname + ".dllconfig");
        var c = ParseConfig(path);
        if (c is null)
            return IntPtr.Zero;

        var libpath = ProcessDllConfiguration(c);
        if (string.IsNullOrEmpty(libpath))
        {
            LoadedLibs.Add(libname, IntPtr.Zero);
            return IntPtr.Zero;
        }

        libpath = Path.Combine(execpath, libpath);
        if (NativeLibrary.TryLoad(libpath, out ptr))
        {
            LoadedLibs.Add(libname, ptr);
            return ptr;
        }

        LoadedLibs.Add(libname, IntPtr.Zero);
        return IntPtr.Zero;
    }

    public static string ProcessDllConfiguration(configuration c)
    {
        string newlibname = null;
        foreach (var m in c.maps)
        {
            if (string.IsNullOrEmpty(m.target))
                continue;
            if (string.IsNullOrEmpty(m.os))
                continue;

            var match = false;
            var negate = m.os[0] == '!';
            if (negate)
                m.os = m.os[1..];
            var tos = m.os.Split(',');
            foreach (var s in tos)
            {
                match = OperatingSystem.IsOSPlatform(s);
                if (match)
                {
                    match = !negate;
                    break;
                }
            }

            if (!match)
                continue;

            if (string.IsNullOrEmpty(m.cpu))
            {
                newlibname = m.target;
                break;
            }

            negate = m.cpu[0] == '!';
            if (negate)
                m.cpu = m.cpu[1..];
            m.cpu = m.cpu.ToLower();
            var tcpu = m.cpu.Split(',');
            match = false;
            foreach (var s in tcpu)
            {
                switch (s)
                {
                    case "x86":
                        if (RuntimeInformation.ProcessArchitecture == Architecture.X86)
                            match = true;
                        break;
                    case "x86avx":
                        if (Avx.IsSupported && RuntimeInformation.ProcessArchitecture == Architecture.X86)
                            match = true;
                        break;
                    case "x86-64":
                        if (RuntimeInformation.ProcessArchitecture == Architecture.X64)
                            match = true;
                        break;
                    case "x86-64avx":
                        if (Avx.IsSupported && RuntimeInformation.ProcessArchitecture == Architecture.X64)
                            match = true;
                        break;
                    case "arm":
                    case "aarch32":
                        if (RuntimeInformation.ProcessArchitecture == Architecture.Arm)
                            match = true;
                        break;
                    case "arm64":
                    case "armv8":
                    case "aarch64":
                        if (RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
                            match = true;
                        break;
                    case "s390":
                    case "s390x":
                        if (RuntimeInformation.ProcessArchitecture == Architecture.S390x)
                            match = true;
                        break;
                }

                if (match)
                {
                    match = !negate;
                    break;
                }
            }

            if (match)
            {
                newlibname = m.target;
                break;
            }
        }

        return newlibname;
    }

    [XmlRoot("dllmap")]
    public class dllmap
    {
        [XmlAttribute("cpu")] public string cpu;

        [XmlAttribute("dll")] public string dll;

        [XmlAttribute("os")] public string os;

        [XmlAttribute("target")] public string target;
    }

    [XmlRoot("configuration")]
    public class configuration
    {
        [XmlElement("dllmap")] public List<dllmap> maps = new();
    }
}