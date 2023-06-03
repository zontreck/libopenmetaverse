﻿/*
 * Copyright (c) Microsoft Corporation
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
using System.Threading;

namespace OpenMetaverse;

public class Lazy<T>
{
    private volatile bool _isValueCreated;
    private readonly object _lock;
    private T _value;
    private Func<T> _valueFactory;

    public Lazy()
        : this(() => Activator.CreateInstance<T>())
    {
    }

    public Lazy(bool isThreadSafe)
        : this(() => Activator.CreateInstance<T>(), isThreadSafe)
    {
    }

    public Lazy(Func<T> valueFactory) :
        this(valueFactory, true)
    {
    }

    public Lazy(Func<T> valueFactory, bool isThreadSafe)
    {
        if (isThreadSafe) _lock = new object();

        _valueFactory = valueFactory;
    }

    public bool IsValueCreated => _isValueCreated;


    public T Value
    {
        get
        {
            if (!_isValueCreated)
            {
                if (_lock != null) Monitor.Enter(_lock);

                try
                {
                    var value = _valueFactory.Invoke();
                    _valueFactory = null;
                    Thread.MemoryBarrier();
                    _value = value;
                    _isValueCreated = true;
                }
                finally
                {
                    if (_lock != null) Monitor.Exit(_lock);
                }
            }

            return _value;
        }
    }
}