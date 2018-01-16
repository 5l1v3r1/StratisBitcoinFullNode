﻿using System;
using System.Collections.Generic;
using System.Text;

namespace Stratis.SmartContracts.State
{
    public interface IMemSizeEstimator<E>
    {
        long EstimateSize(E e);
    }

    public class ByteArrayEstimator : IMemSizeEstimator<byte[]>
    {
        public long EstimateSize(byte[] bytes)
        {
            return bytes == null ? 0 : bytes.Length + 4;
        }
    }
}

