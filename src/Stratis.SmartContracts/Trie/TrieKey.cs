﻿using System;
using System.Collections.Generic;
using System.Text;

namespace Stratis.SmartContracts.Trie
{
    public class TrieKey
    {
        public const int ODD_OFFSET_FLAG = 0x1;
        public const int TERMINATOR_FLAG = 0x2;
        private readonly byte[] key;
        private readonly int off;
        private readonly bool terminal;

        public int Length
        {
            get
            {
                return (this.key.Length << 1) - this.off;
            }
        }

        public bool IsEmpty
        {
            get
            {
                return this.Length == 0;
            }
        }

        public bool IsTerminal
        {
            get
            {
                return this.terminal;
            }
        }

        public static TrieKey FromNormal(byte[] key)
        {
            return new TrieKey(key);
        }

        public static TrieKey FromPacked(byte[] key)
        {
            return new TrieKey(key, ((key[0] >> 4) & ODD_OFFSET_FLAG) != 0 ? 1 : 2, ((key[0] >> 4) & TERMINATOR_FLAG) != 0);
        }

        public static TrieKey Empty(bool terminal)
        {
            return new TrieKey(new byte[0], 0, terminal);
        }

        public static TrieKey SingleHex(int hex)
        {
            TrieKey ret = new TrieKey(new byte[1], 1, false);
            ret.SetHex(0, hex);
            return ret;
        }

        public TrieKey(byte[] key, int off, bool terminal)
        {
            this.terminal = terminal;
            this.off = off;
            this.key = key;
        }

        private TrieKey(byte[] key) : this(key, 0, true)
        {
        }

        public byte[] ToPacked()
        {
            int flags = ((off & 1) != 0 ? ODD_OFFSET_FLAG : 0) | (terminal ? TERMINATOR_FLAG : 0);
            byte[] ret = new byte[Length / 2 + 1];
            int toCopy = (flags & ODD_OFFSET_FLAG) != 0 ? ret.Length : ret.Length - 1;
            Array.Copy(key, key.Length - toCopy, ret, ret.Length - toCopy, toCopy); // absolutely no idea if this is right
            ret[0] &= 0x0F;
            ret[0] |= (byte) (flags << 4);
            return ret;
        }

        public int GetHex(int idx)
        {
            int adjustedIndex = (off + idx) >> 1;
            byte b = key[(off + idx) >> 1];
            return (((off + idx) & 1) == 0 ? (b >> 4) : b) & 0xF;
        }

        public TrieKey Shift(int hexCnt)
        {
            return new TrieKey(this.key, this.off + hexCnt, terminal);
        }

        private void SetHex(int idx, int hex)
        {
            int byteIdx = (off + idx) >> 1;
            if (((off + idx) & 1) == 0)
            {
                this.key[byteIdx] &= 0x0F;
                this.key[byteIdx] |= (byte) (hex << 4);
            }
            else
            {
                key[byteIdx] &= 0xF0;
                key[byteIdx] |= (byte) hex;
            }
        }

        public TrieKey MatchAndShift(TrieKey k)
        {
            int len = this.Length;
            int kLen = k.Length;
            if (len < kLen) return null;

            if ((off & 1) == (k.off & 1))
            {
                // optimization to compare whole keys bytes
                if ((off & 1) == 1)
                {
                    if (GetHex(0) != k.GetHex(0)) return null;
                }
                int idx1 = (off + 1) >> 1;
                int idx2 = (k.off + 1) >> 1;
                int l = kLen >> 1;
                for (int i = 0; i < l; i++, idx1++, idx2++)
                {
                    if (key[idx1] != k.key[idx2]) return null;
                }
            }
            else
            {
                for (int i = 0; i < kLen; i++)
                {
                    if (GetHex(i) != k.GetHex(i)) return null;
                }
            }
            return Shift(kLen);
        }

        public TrieKey Concat(TrieKey k)
        {
            if (this.IsTerminal) throw new Exception("Can' append to terminal key: " + this + " + " + k);
            int len = this.Length;
            int kLen = k.Length;
            int newLen = len + kLen;
            byte[] newKeyBytes = new byte[(newLen + 1) >> 1];
            TrieKey ret = new TrieKey(newKeyBytes, newLen & 1, k.IsTerminal);
            for (int i = 0; i < len; i++)
            {
                ret.SetHex(i, GetHex(i));
            }
            for (int i = 0; i < kLen; i++)
            {
                ret.SetHex(len + i, k.GetHex(i));
            }
            return ret;
        }

        public TrieKey GetCommonPrefix(TrieKey k)
        {
            // TODO can be optimized
            int prefixLen = 0;
            int thisLenght = this.Length;
            int kLength = k.Length;
            while (prefixLen < thisLenght && prefixLen < kLength && GetHex(prefixLen) == k.GetHex(prefixLen))
                prefixLen++;
            byte[] prefixKey = new byte[(prefixLen + 1) >> 1];
            TrieKey ret = new TrieKey(prefixKey, (prefixLen & 1) == 0 ? 0 : 1,
                    prefixLen == this.Length && prefixLen == k.Length && this.IsTerminal && k.IsTerminal);
            for (int i = 0; i < prefixLen; i++)
            {
                ret.SetHex(i, k.GetHex(i));
            }
            return ret;
        }

        public override bool Equals(object obj)
        {
            TrieKey k = (TrieKey)obj;
            int len = this.Length;

            if (len != k.Length) return false;
            // TODO can be optimized
            for (int i = 0; i < len; i++)
            {
                if (GetHex(i) != k.GetHex(i)) return false;
            }
            return this.IsTerminal == k.IsTerminal;
        }

    }
}
