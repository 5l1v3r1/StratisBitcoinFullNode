﻿using System;
using System.Collections.Generic;
using System.Text;

namespace Stratis.SmartContracts.State
{
    public class SourceChainBox<Key, Value, SourceKey, SourceValue> : AbstractChainedSource<Key, Value, SourceKey, SourceValue>
    {
        private List<ISource<Key, Value>> chain = new List<ISource<Key, Value>>();
        private ISource<Key, Value> lastSource;

        public SourceChainBox(ISource<SourceKey, SourceValue> source) : base(source) {}

        public void Add(ISource<Key, Value> src)
        {
            this.chain.Add(src);
            this.lastSource = src;
        }

        public override void Put(Key key, Value val)
        {
            this.lastSource.Put(key, val);
        }

        public override Value Get(Key key)
        {
            return this.lastSource.Get(key);
        }

        public override void Delete(Key key)
        {
            this.lastSource.Delete(key);
        }

        protected override bool FlushImpl()
        {
            return this.lastSource.Flush();
        }
    }
}
